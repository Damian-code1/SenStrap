using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace SenStrap.Core;

/// <summary>Mensaje y progreso (0 a 1, o null si no se sabe) que se enseñan en la ventana.</summary>
public record BootstrapStatus(string Message, double? Progress = null);

/// <summary>Descarga, comprueba e instala el cliente oficial de Roblox en la carpeta de SenStrap.</summary>
public class RobloxInstaller(IProgress<BootstrapStatus> progress)
{
    private const int ParallelDownloads = 4;
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Si se ha lanzado una actualización en segundo plano, la tarea que la hace.
    /// Hay que dejar SenStrap abierto hasta que termine.
    /// </summary>
    public Task? BackgroundUpdate { get; private set; }

    /// <summary>Deja instalada la versión indicada (con los mods aplicados) y devuelve su carpeta.</summary>
    public async Task<string> EnsureInstalledAsync(ClientVersion version, AppState state, Settings settings, CancellationToken ct)
    {
        string modsHash = Mods.ComputeHash();
        string? currentDir = state.InstallDir is { Length: > 0 } folder ? Path.Combine(AppInfo.VersionsDir, folder) : null;

        bool playable = currentDir is not null && File.Exists(Path.Combine(currentDir, "RobloxPlayerBeta.exe"));
        bool upToDate = playable && state.VersionGuid == version.VersionGuid;
        bool modsChanged = state.AppliedModsHash != modsHash;

        if (upToDate && !modsChanged)
        {
            Logger.Info("Roblox ya está actualizado.");
            return currentDir!;
        }

        // Actualizaciones en segundo plano: se abre ya la versión que hay y la nueva
        // se descarga mientras juegas; se usará la próxima vez.
        if (settings.BackgroundUpdates && playable && !upToDate && !modsChanged)
        {
            Logger.Info($"Hay una versión nueva ({version.Version}): se descarga en segundo plano y se abre la actual.");
            var background = new RobloxInstaller(new NullProgress());
            BackgroundUpdate = Task.Run(async () =>
            {
                try
                {
                    // Espera a que esta ventana suelte el candado de instalación
                    await using var installLock = await Launcher.AcquireLockAsync(new NullProgress(), CancellationToken.None);
                    await background.InstallAsync(version, state, modsHash, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"La actualización en segundo plano ha fallado (se reintentará al jugar): {ex.Message}");
                }
            });
            return currentDir!;
        }

        Logger.Info(upToDate
            ? "Los mods han cambiado: se reinstala la versión actual."
            : $"Instalando Roblox {version.Version} ({version.VersionGuid}).");
        return await InstallAsync(version, state, modsHash, ct);
    }

    /// <summary>Descarga e instala la versión en una carpeta nueva y la marca como la actual.</summary>
    private async Task<string> InstallAsync(ClientVersion version, AppState state, string modsHash, CancellationToken ct)
    {
        progress.Report(new BootstrapStatus(Lang.T("status.manifest")));
        var packages = await RobloxDeployment.GetPackagesAsync(version.VersionGuid, ct);

        var toInstall = new List<(PackageInfo Package, string Dir)>();
        foreach (var package in packages)
        {
            if (RobloxDeployment.GetExtractDir(package.Name) is { } dir)
                toInstall.Add((package, dir));
        }

        await DownloadAsync(version.VersionGuid, toInstall.Select(x => x.Package).ToList(), ct);

        // Se instala siempre en una carpeta que no existe, así nunca hay que borrar
        // una carpeta que Roblox (o su RobloxCrashHandler) pueda tener abierta.
        // Mientras no termine, State.json sigue apuntando a la versión anterior, que sigue intacta.
        string folderName = ChooseFolderName(version.VersionGuid);
        string versionDir = Path.Combine(AppInfo.VersionsDir, folderName);
        string tempDir = versionDir + ".tmp";
        Directory.CreateDirectory(tempDir);

        progress.Report(new BootstrapStatus(Lang.T("status.installing"), 0));
        Extract(toInstall, tempDir, ct);
        WriteAppSettings(tempDir);
        Mods.Apply(tempDir);
        Directory.Move(tempDir, versionDir);

        state.VersionGuid = version.VersionGuid;
        state.InstallDir = folderName;
        state.RobloxVersion = version.Version;
        state.AppliedModsHash = modsHash;
        state.Save();

        CleanupDownloads(packages);
        CleanupOldVersions(versionDir);
        Logger.Info($"Instalación terminada en {folderName}.");
        return versionDir;
    }

    /// <summary>
    /// ¿Hay algún proceso de Roblox abierto (el juego, su CrashHandler, Studio...)?
    /// Mientras lo haya, SenStrap no borra ni mueve ninguna carpeta de versiones:
    /// un .exe en marcha no impide renombrar su carpeta, pero sí se rompería al moverla.
    /// </summary>
    public static bool IsRobloxRunning()
    {
        var processes = Process.GetProcesses();
        bool running = processes.Any(p => p.ProcessName.StartsWith("Roblox", StringComparison.OrdinalIgnoreCase));
        foreach (var p in processes) p.Dispose();
        return running;
    }

    /// <summary>¿Está abierto el juego (RobloxPlayerBeta)?</summary>
    public static bool IsRobloxPlayerRunning()
    {
        var processes = Process.GetProcessesByName("RobloxPlayerBeta");
        foreach (var p in processes) p.Dispose();
        return processes.Length > 0;
    }

    /// <summary>Borra todas las copias de Roblox de SenStrap. Devuelve false si alguna estaba en uso.</summary>
    public static bool RemoveAllVersions()
    {
        if (!Directory.Exists(AppInfo.VersionsDir)) return true;
        if (IsRobloxRunning()) return false;
        bool all = true;
        foreach (var dir in Directory.EnumerateDirectories(AppInfo.VersionsDir).ToList())
            all &= TryRemoveFolder(dir);
        return all;
    }

    /// <summary>Nombre de carpeta libre para la versión: "version-xxx" o, si está ocupada, "version-xxx-2"...</summary>
    private static string ChooseFolderName(string versionGuid)
    {
        Directory.CreateDirectory(AppInfo.VersionsDir);
        bool robloxOpen = IsRobloxRunning();

        string name = versionGuid;
        for (int i = 2; ; i++)
        {
            string dir = Path.Combine(AppInfo.VersionsDir, name);
            bool free = (!Directory.Exists(dir) || (!robloxOpen && TryRemoveFolder(dir)))
                     && (!Directory.Exists(dir + ".tmp") || TryRemoveFolder(dir + ".tmp"));
            if (free) return name;
            name = $"{versionGuid}-{i}";
        }
    }

    /// <summary>
    /// Borra una carpeta. Llamar solo cuando no hay ningún proceso de Roblox abierto
    /// (<see cref="IsRobloxRunning"/>). Primero la renombra: si algún archivo de dentro
    /// está abierto por otro programa, el renombrado falla y la carpeta se deja intacta
    /// (en vez de quedar medio borrada). Se volverá a intentar otro día.
    /// </summary>
    private static bool TryRemoveFolder(string dir)
    {
        if (!Directory.Exists(dir)) return true;

        string trash = Path.Combine(Path.GetDirectoryName(dir)!, $"_borrar_{Guid.NewGuid():N}");
        try
        {
            Directory.Move(dir, trash);
        }
        catch (Exception ex)
        {
            Logger.Warn($"{Path.GetFileName(dir)} está en uso, no se borra: {ex.Message}");
            return false;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(trash, "*", SearchOption.AllDirectories))
            {
                var attributes = File.GetAttributes(file);
                if (attributes.HasFlag(FileAttributes.ReadOnly))
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
            Directory.Delete(trash, recursive: true);
        }
        catch (Exception ex)
        {
            Logger.Warn($"No se pudo terminar de borrar {Path.GetFileName(dir)} (se reintentará): {ex.Message}");
        }
        return true;
    }

    private static string CachePath(PackageInfo package) => Path.Combine(AppInfo.DownloadsDir, package.Md5);

    private static string Md5Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(MD5.HashData(stream)).ToLowerInvariant();
    }

    private static bool IsCached(PackageInfo package)
    {
        string path = CachePath(package);
        return File.Exists(path)
            && new FileInfo(path).Length == package.PackedSize
            && Md5Of(path) == package.Md5;
    }

    private async Task DownloadAsync(string versionGuid, List<PackageInfo> packages, CancellationToken ct)
    {
        Directory.CreateDirectory(AppInfo.DownloadsDir);
        progress.Report(new BootstrapStatus(Lang.T("status.verifying")));

        var missing = packages.Where(p => !IsCached(p)).ToList();
        if (missing.Count == 0)
            return;

        long total = Math.Max(1, missing.Sum(p => p.PackedSize));
        long done = 0;
        var throttle = new Throttle();

        void Report()
        {
            if (!throttle.Ready()) return;
            long d = Math.Max(0, Interlocked.Read(ref done));
            progress.Report(new BootstrapStatus(
                Lang.F("status.downloading", d / 1048576, total / 1048576),
                Math.Min(1.0, (double)d / total)));
        }

        Logger.Info($"Descargando {missing.Count} paquetes ({total / 1048576} MB).");
        Report();

        var options = new ParallelOptions { MaxDegreeOfParallelism = ParallelDownloads, CancellationToken = ct };
        await Parallel.ForEachAsync(missing, options, async (package, token) =>
        {
            await DownloadPackageAsync(versionGuid, package, bytes =>
            {
                Interlocked.Add(ref done, bytes);
                Report();
            }, token);
        });
    }

    private static async Task DownloadPackageAsync(string versionGuid, PackageInfo package, Action<long> onBytes, CancellationToken ct)
    {
        string path = CachePath(package);
        string partial = path + ".part";

        for (int attempt = 1; ; attempt++)
        {
            long received = 0;
            try
            {
                using (var response = await RobloxDeployment.DownloadPackageAsync(versionGuid, package.Name, ct))
                await using (var source = await response.Content.ReadAsStreamAsync(ct))
                await using (var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
                {
                    var buffer = new byte[1 << 16];
                    using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    while (true)
                    {
                        // Si la conexión se queda colgada 30 s, se corta y se reintenta.
                        readCts.CancelAfter(StallTimeout);
                        int n = await source.ReadAsync(buffer, readCts.Token);
                        if (n == 0) break;
                        await target.WriteAsync(buffer.AsMemory(0, n), ct);
                        received += n;
                        onBytes(n);
                    }
                }

                if (Md5Of(partial) != package.Md5)
                    throw new InvalidDataException($"{package.Name} se ha descargado dañado (el MD5 no coincide).");

                File.Move(partial, path, overwrite: true);
                return;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && attempt < 3)
            {
                onBytes(-received);
                Logger.Warn($"Fallo al descargar {package.Name} (intento {attempt} de 3): {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(attempt), ct);
            }
        }
    }

    private void Extract(List<(PackageInfo Package, string Dir)> items, string root, CancellationToken ct)
    {
        string rootFull = Path.GetFullPath(root).TrimEnd('\\') + "\\";
        long total = Math.Max(1, items.Sum(x => x.Package.UnpackedSize));
        long done = 0;
        var throttle = new Throttle();

        foreach (var (package, dir) in items)
        {
            ct.ThrowIfCancellationRequested();
            using var zip = ZipFile.OpenRead(CachePath(package));

            foreach (var entry in zip.Entries)
            {
                string name = entry.FullName.Replace('/', '\\');
                if (name.EndsWith('\\') || string.IsNullOrEmpty(entry.Name))
                    continue; // es una carpeta

                string target = Path.GetFullPath(Path.Combine(root, dir, name.TrimStart('\\')));
                if (!target.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Warn($"Ruta no válida dentro de {package.Name}: {entry.FullName}");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);

                done += entry.Length;
                if (throttle.Ready())
                    progress.Report(new BootstrapStatus(Lang.T("status.installing"), Math.Min(1.0, (double)done / total)));
            }
        }
    }

    private static void WriteAppSettings(string versionDir)
    {
        File.WriteAllText(Path.Combine(versionDir, "AppSettings.xml"),
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
            "<Settings>\r\n" +
            "\t<ContentFolder>content</ContentFolder>\r\n" +
            "\t<BaseUrl>http://www.roblox.com</BaseUrl>\r\n" +
            "</Settings>\r\n");
    }

    private static void CleanupDownloads(List<PackageInfo> packages)
    {
        var keep = packages.Select(p => p.Md5).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(AppInfo.DownloadsDir))
        {
            if (keep.Contains(Path.GetFileName(file))) continue;
            try { File.Delete(file); } catch (Exception ex) { Logger.Warn($"No se pudo borrar {file}: {ex.Message}"); }
        }
    }

    /// <summary>Borra las demás carpetas de versiones, menos las que estén en uso.</summary>
    private static void CleanupOldVersions(string currentDir)
    {
        if (IsRobloxRunning())
        {
            Logger.Info("Roblox está abierto: las versiones antiguas se borrarán otro día.");
            return;
        }
        foreach (var dir in Directory.EnumerateDirectories(AppInfo.VersionsDir).ToList())
        {
            if (string.Equals(Path.GetFullPath(dir), Path.GetFullPath(currentDir), StringComparison.OrdinalIgnoreCase))
                continue;
            if (TryRemoveFolder(dir))
                Logger.Info($"Carpeta antigua borrada: {Path.GetFileName(dir)}");
        }
    }

    /// <summary>Progreso que no se enseña en ningún sitio (actualización en segundo plano).</summary>
    private sealed class NullProgress : IProgress<BootstrapStatus>
    {
        public void Report(BootstrapStatus value) { }
    }

    /// <summary>Limita cuántas veces por segundo se actualiza la barra de progreso.</summary>
    private sealed class Throttle(int intervalMs = 100)
    {
        private long _last = long.MinValue / 2;

        public bool Ready()
        {
            long now = Environment.TickCount64;
            long last = Interlocked.Read(ref _last);
            return now - last >= intervalMs && Interlocked.CompareExchange(ref _last, now, last) == last;
        }
    }
}
