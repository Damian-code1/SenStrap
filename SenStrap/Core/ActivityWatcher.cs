using System.Text.RegularExpressions;

namespace SenStrap.Core;

/// <summary>
/// Seguimiento de actividad: lee el registro (log) que Roblox va escribiendo para saber
/// a qué juego y servidor te unes y cuándo sales. Solo lee archivos, no toca el juego.
/// </summary>
public sealed partial class ActivityWatcher
{
    public event Action<GameEntry>? Joined;
    public event Action<GameEntry>? Left;

    /// <summary>Roblox ha perdido la conexión con el servidor (error de red, servidor caído...).</summary>
    public event Action<GameEntry>? LostConnection;

    /// <summary>Errores de red tras los que tiene sentido volver a entrar.</summary>
    private static readonly string[] NetworkErrorCodes = ["(Error Code: 277)", "(Error Code: 266)", "(Error Code: 279)"];

    /// <summary>Expulsiones y similares: tras estas no se vuelve a entrar automáticamente.</summary>
    private static readonly string[] NoRejoinMarkers =
        ["Error Code: 267", "Error Code: 268", "Error Code: 273", "kicked", "banned"];

    public GameEntry? Current { get; private set; }

    private readonly DateTime _sinceUtc;
    private readonly CancellationTokenSource _cancel = new();
    private GameEntry? _joining;

    [GeneratedRegex(@"! Joining game '([0-9a-fA-F\-]+)' place ([0-9]+) at ([0-9\.]+)")]
    private static partial Regex JoiningRegex();

    [GeneratedRegex(@"RCC Server Address = ([0-9\.]+)")]
    private static partial Regex RccRegex();

    [GeneratedRegex(@"serverId: ([0-9\.]+)\|")]
    private static partial Regex ServerIdRegex();

    public ActivityWatcher(DateTime launchedAtUtc) => _sinceUtc = launchedAtUtc;

    public void Start() => _ = Task.Run(() => RunAsync(_cancel.Token));

    public void Stop() => _cancel.Cancel();

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs");
            string? file = null;
            while (file is null)
            {
                ct.ThrowIfCancellationRequested();
                if (Directory.Exists(dir))
                {
                    // The crash handler writes its own "..._Player_XXXXX_CrashHandler_last.log" a second
                    // after the game's log; it never has the join/leave lines, so it must be skipped.
                    file = new DirectoryInfo(dir).GetFiles("*.log")
                        .Where(f => f.Name.Contains("Player", StringComparison.OrdinalIgnoreCase)
                                    && !f.Name.Contains("CrashHandler", StringComparison.OrdinalIgnoreCase)
                                    && f.LastWriteTimeUtc >= _sinceUtc.AddSeconds(-10))
                        .OrderByDescending(f => f.CreationTimeUtc)
                        .FirstOrDefault()?.FullName;
                }
                if (file is null) await Task.Delay(1000, ct);
            }
            Logger.Info($"Leyendo el log de Roblox: {Path.GetFileName(file)}");

            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (!ct.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(ct);
                if (line is null)
                {
                    await Task.Delay(500, ct);
                    continue;
                }
                ReadLine(line);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.Warn($"El seguimiento de actividad se ha detenido: {ex.Message}");
        }
    }

    private void ReadLine(string line)
    {
        if (line.Contains("! Joining game", StringComparison.Ordinal) && JoiningRegex().Match(line) is { Success: true } join)
        {
            if (Current is not null) FinishCurrent();
            _joining = new GameEntry
            {
                JobId = join.Groups[1].Value,
                PlaceId = long.Parse(join.Groups[2].Value),
                Ip = join.Groups[3].Value,
                JoinedAt = DateTime.Now,
            };
        }
        else if (_joining is not null && line.Contains("RCC Server Address", StringComparison.Ordinal)
                 && RccRegex().Match(line) is { Success: true } rcc)
        {
            _joining.Ip = rcc.Groups[1].Value; // la IP real del servidor (la otra es un intermediario)
        }
        else if (_joining is not null && line.Contains("serverId:", StringComparison.Ordinal)
                 && ServerIdRegex().Match(line) is { Success: true })
        {
            Current = _joining;
            _joining = null;
            Logger.Info($"Unido al juego {Current.PlaceId}.");
            Joined?.Invoke(Current);
        }
        else if (Current is not null && IsLostConnection(line))
        {
            Logger.Info($"Conexión perdida con el servidor del juego {Current.PlaceId}.");
            LostConnection?.Invoke(Current);
        }
        else if (Current is not null && (line.Contains("Time to disconnect replication data", StringComparison.Ordinal)
                                         || line.Contains("leaveUGCGameInternal", StringComparison.Ordinal)))
        {
            FinishCurrent();
        }
    }

    /// <summary>
    /// ¿Es esta línea del log una pérdida de conexión por la red?
    /// Solo cuenta lo que Roblox escribe en su canal de red (no el chat de voz) y nunca las expulsiones.
    /// </summary>
    public static bool IsLostConnection(string line)
    {
        if (NoRejoinMarkers.Any(m => line.Contains(m, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (NetworkErrorCodes.Any(code => line.Contains(code, StringComparison.Ordinal)))
            return true;
        return line.Contains("[FLog::Network]", StringComparison.Ordinal)
               && (line.Contains("Lost connection", StringComparison.OrdinalIgnoreCase)
                   || line.Contains("Connection lost", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Cierra la partida actual (también se llama al cerrarse Roblox).</summary>
    public void FinishCurrent()
    {
        var entry = Current;
        if (entry is null) return;
        Current = null;
        entry.Seconds = (DateTime.Now - entry.JoinedAt).TotalSeconds;
        Logger.Info($"Saliste del juego {entry.PlaceId} tras {entry.Seconds:F0} s.");
        Left?.Invoke(entry);
    }
}
