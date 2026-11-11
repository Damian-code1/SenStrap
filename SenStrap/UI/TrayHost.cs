using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using SenStrap.Core;
using Forms = System.Windows.Forms;

namespace SenStrap.UI;

/// <summary>
/// Icono en la bandeja del sistema mientras juegas: seguimiento de actividad, ubicación del servidor,
/// tiempo de juego, historial y reconexión automática. Se cierra solo cuando se cierra Roblox.
/// </summary>
public sealed class TrayHost
{
    /// <summary>Máximo de reconexiones automáticas seguidas, para no entrar en bucle si el servidor está caído.</summary>
    private const int MaxRejoins = 3;
    private static readonly TimeSpan RejoinWindow = TimeSpan.FromMinutes(15);

    /// <summary>Tiempo que se espera tras perder la conexión por si Roblox se reconecta solo.</summary>
    private static readonly TimeSpan RejoinDelay = TimeSpan.FromSeconds(8);

    private readonly Settings _settings;
    private readonly LaunchResult _launch;
    private readonly Forms.NotifyIcon _icon;
    private readonly ActivityWatcher _watcher;
    private readonly DispatcherTimer _timer;
    private bool _stopped;
    private bool _rejoining;
    private int _joinCount;

    public TrayHost(Settings settings, LaunchResult launch)
    {
        _settings = settings;
        _launch = launch;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Lang.T("tray.open"), null, (_, _) => OpenSenStrap());
        menu.Items.Add(Lang.T("tray.exit"), null, (_, _) => Stop());
        _icon = new Forms.NotifyIcon
        {
            Text = "SenStrap",
            Icon = LoadIcon(),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => OpenSenStrap();

        _watcher = new ActivityWatcher(launch.StartedAtUtc);
        _watcher.Joined += entry => OnUi(() => OnJoined(entry));
        _watcher.Left += entry => OnUi(() => OnLeft(entry));
        _watcher.LostConnection += entry => OnUi(() => OnLostConnection(entry));

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>Ejecuta en el hilo de la interfaz (directamente si ya estamos en él).</summary>
    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }

    public void Start()
    {
        Logger.Info("Seguimiento de actividad activo (icono en la bandeja).");
        _watcher.Start();
        _timer.Start();
    }

    private void Tick()
    {
        if (_rejoining) return;
        if (!RobloxInstaller.IsRobloxPlayerRunning())
        {
            Stop();
            return;
        }
        if (_watcher.Current is { } game && _settings.PlaytimeCounter)
        {
            string text = $"SenStrap · {game.DisplayName} · {Lang.Duration((DateTime.Now - game.JoinedAt).TotalSeconds)}";
            _icon.Text = text.Length > 63 ? text[..63] : text; // límite de Windows
        }
    }

    private async void OnJoined(GameEntry entry)
    {
        _joinCount++;
        try
        {
            var location = _settings.ServerLocation ? GameHistory.LocateServerAsync(entry.Ip) : Task.FromResult<string?>(null);
            await Task.WhenAll(location, GameHistory.FillGameInfoAsync(entry));
            entry.Location = location.Result;

            if (_settings.SaveGameHistory) GameHistory.Add(entry);

            string body = entry.Location is not null ? Lang.F("notify.server", entry.Location) : entry.DisplayName;
            Notify(entry.DisplayName, body);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Seguimiento de actividad: {ex.Message}");
        }
    }

    private void OnLeft(GameEntry entry)
    {
        if (_settings.SaveGameHistory) GameHistory.Update(entry);
        if (!_settings.PlaytimeCounter) return;

        var state = AppState.Load();
        state.TotalPlaySeconds += entry.Seconds;
        state.Save();
        if (!_stopped && !_rejoining)
            Notify(entry.DisplayName, Lang.F("notify.session", Lang.Duration(entry.Seconds), Lang.Duration(state.TotalPlaySeconds)));
    }

    // ===================== Reconexión automática =====================

    private async void OnLostConnection(GameEntry game)
    {
        if (!_settings.AutoRejoin || _stopped || _rejoining) return;

        // Si Roblox se reconecta solo en estos segundos (botón "Reconectar"), no hacemos nada
        int joinsBefore = _joinCount;
        await Task.Delay(RejoinDelay);
        if (_stopped || _rejoining || _joinCount != joinsBefore) return;

        var state = AppState.Load();
        state.RecentAutoRejoins.RemoveAll(t => DateTime.Now - t > RejoinWindow);
        if (state.RecentAutoRejoins.Count >= MaxRejoins)
        {
            Logger.Warn("Reconexión automática: demasiados intentos seguidos, se deja de intentar.");
            Notify(game.DisplayName, Lang.T("notify.rejoinGaveUp"));
            return;
        }
        state.RecentAutoRejoins.Add(DateTime.Now);
        state.Save();

        _rejoining = true;
        Notify(game.DisplayName, Lang.F("notify.rejoin", game.DisplayName));
        Logger.Info($"Reconexión automática al juego {game.PlaceId} (intento {state.RecentAutoRejoins.Count} de {MaxRejoins}).");

        // El Roblox desconectado se cierra (si no, el nuevo preguntaría "¿cerrar el que ya está abierto?")
        await Task.Run(CloseRoblox);
        _watcher.FinishCurrent();

        string exe = File.Exists(AppInfo.InstalledExe) ? AppInfo.InstalledExe : AppInfo.CurrentExe;
        var info = new ProcessStartInfo(exe) { UseShellExecute = false };
        info.ArgumentList.Add("-player");
        info.ArgumentList.Add(game.RejoinUri);
        Process.Start(info);

        // El SenStrap nuevo abre su propio icono de la bandeja
        Stop();
    }

    private static void CloseRoblox()
    {
        foreach (var process in Process.GetProcessesByName("RobloxPlayerBeta"))
        {
            using (process)
            {
                try
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(3000))
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"No se pudo cerrar Roblox para reconectar: {ex.Message}");
                }
            }
        }
    }

    // ===================== Utilidades =====================

    private void Notify(string title, string text)
    {
        if (_stopped || string.IsNullOrWhiteSpace(text)) return;
        _icon.ShowBalloonTip(5000, title, text, Forms.ToolTipIcon.None);
    }

    private static void OpenSenStrap()
    {
        string exe = File.Exists(AppInfo.InstalledExe) ? AppInfo.InstalledExe : AppInfo.CurrentExe;
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false });
    }

    private void Stop()
    {
        if (_stopped) return;
        _stopped = true;
        _timer.Stop();
        _watcher.FinishCurrent();
        _watcher.Stop();
        _icon.Visible = false;
        _icon.Dispose();
        Logger.Info("Fin del seguimiento de actividad.");
        App.ShutdownAfter(_launch.BackgroundUpdate);
    }

    private static System.Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/SenStrap;component/Resources/senstrap.ico"));
        using var stream = resource!.Stream;
        return new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }
}
