using System.Diagnostics;

namespace SenStrap.Core;

/// <summary>
/// Resultado de abrir Roblox: cuándo se abrió (para el seguimiento de actividad)
/// y, si la hay, la actualización que sigue descargándose en segundo plano.
/// </summary>
public sealed record LaunchResult(DateTime StartedAtUtc, Task? BackgroundUpdate = null);

/// <summary>Todo lo que pasa al darle a "Jugar": actualizar Roblox, aplicar ajustes y abrirlo.</summary>
public static class Launcher
{
    /// <param name="launchUri">El enlace roblox-player:/roblox:// de la web, o null para abrir la app de Roblox.</param>
    /// <param name="confirmCloseRunning">Pregunta si se puede cerrar el Roblox que ya está abierto.</param>
    /// <returns>null si el usuario decidió no abrir Roblox.</returns>
    public static async Task<LaunchResult?> RunAsync(string? launchUri, IProgress<BootstrapStatus> progress,
        Func<Task<bool>> confirmCloseRunning, CancellationToken ct)
    {
        Directory.CreateDirectory(AppInfo.BaseDir);
        await using var installLock = await AcquireLockAsync(progress, ct);

        var settings = Settings.Load();
        var state = AppState.Load();

        if (settings.ConfirmLaunch && RobloxInstaller.IsRobloxPlayerRunning() && !await confirmCloseRunning())
        {
            Logger.Info("Ya había un Roblox abierto y el usuario ha decidido no abrir otro.");
            return null;
        }

        try { Cleaner.Run(settings); }
        catch (Exception ex) { Logger.Warn($"Limpieza automática: {ex.Message}"); }

        progress.Report(new BootstrapStatus(Lang.T("status.checking")));
        var version = await RobloxDeployment.GetClientVersionAsync(ct);
        Logger.Info($"Versión actual de Roblox: {version.Version} ({version.VersionGuid})");

        var installer = new RobloxInstaller(progress);
        string versionDir = await installer.EnsureInstalledAsync(version, state, settings, ct);
        string robloxExe = Path.Combine(versionDir, "RobloxPlayerBeta.exe");

        progress.Report(new BootstrapStatus(Lang.T("status.applying")));
        FastFlags.Apply(versionDir, settings.ManageFastFlags ? settings.FastFlags : new());
        Compatibility.Apply(robloxExe, settings);

        ct.ThrowIfCancellationRequested();
        progress.Report(new BootstrapStatus(Lang.T("status.opening")));
        var startedAt = DateTime.UtcNow;
        using var roblox = StartRoblox(versionDir, launchUri);
        await WaitForWindowAsync(roblox);

        if (settings.CloseCrashHandler)
            await CloseCrashHandlerAsync(startedAt);

        return new LaunchResult(startedAt, installer.BackgroundUpdate);
    }

    private static Process StartRoblox(string versionDir, string? launchUri)
    {
        var info = new ProcessStartInfo(Path.Combine(versionDir, "RobloxPlayerBeta.exe"))
        {
            WorkingDirectory = versionDir,
            UseShellExecute = false,
        };
        // Con enlace de la web se le pasa tal cual; sin enlace, se abre la app de Roblox.
        info.ArgumentList.Add(launchUri ?? "--app");

        Logger.Info($"Iniciando Roblox: {info.FileName} {(launchUri is null ? "--app" : "<enlace>")}");
        return Process.Start(info) ?? throw new Exception("Windows no pudo iniciar RobloxPlayerBeta.exe.");
    }

    /// <summary>Espera a que aparezca la ventana de Roblox (máximo 20 s) para cerrar SenStrap justo después.</summary>
    private static async Task WaitForWindowAsync(Process roblox)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(20))
        {
            try
            {
                roblox.Refresh();
                if (roblox.HasExited || roblox.MainWindowHandle != IntPtr.Zero)
                    return;
            }
            catch
            {
                return;
            }
            await Task.Delay(250);
        }
    }

    /// <summary>Cierra el RobloxCrashHandler que se abre con el juego (solo el de esta vez).</summary>
    private static async Task CloseCrashHandlerAsync(DateTime startedAtUtc)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            foreach (var process in Process.GetProcessesByName("RobloxCrashHandler"))
            {
                using (process)
                {
                    try
                    {
                        if (process.StartTime.ToUniversalTime() < startedAtUtc.AddSeconds(-2)) continue;
                        process.Kill();
                        Logger.Info("RobloxCrashHandler cerrado.");
                        return;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"No se pudo cerrar RobloxCrashHandler: {ex.Message}");
                    }
                }
            }
            await Task.Delay(500);
        }
    }

    /// <summary>Evita que dos ventanas de SenStrap (o una actualización en segundo plano) instalen Roblox a la vez.</summary>
    internal static async Task<FileStream> AcquireLockAsync(IProgress<BootstrapStatus> progress, CancellationToken ct)
    {
        string path = Path.Combine(AppInfo.BaseDir, "install.lock");
        bool waiting = false;
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                if (!waiting)
                {
                    progress.Report(new BootstrapStatus(Lang.T("status.waiting")));
                    waiting = true;
                }
                await Task.Delay(500, ct);
            }
        }
    }
}
