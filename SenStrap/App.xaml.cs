using System.Windows;
using SenStrap.Core;
using SenStrap.Core.Tweaks;
using SenStrap.UI;

namespace SenStrap;

/// <summary>
/// Punto de entrada. Según los argumentos:
///   (sin argumentos)          -> ventana principal (o la bienvenida la primera vez)
///   -player "roblox-player:…" -> la web pide abrir un juego
///   -play                     -> abrir Roblox
///   -uninstall                -> desinstalar
///   -tweaks "request.json"    -> (como administrador) aplicar optimizaciones del sistema
///   -noupdate                 -> no buscar actualizaciones (la copia recién actualizada)
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Logger.Init();
        Logger.Info($"SenStrap {AppInfo.Version} iniciado. Argumentos: {string.Join(' ', e.Args)}");

        var settings = Settings.Load();
        Lang.Load(string.IsNullOrEmpty(settings.Language) ? Lang.Detect() : settings.Language);

        // Copy started as administrator by the Optimizations page: make the machine-wide changes and exit
        if (e.Args.Length == 2 && e.Args[0].Equals("-tweaks", StringComparison.OrdinalIgnoreCase))
        {
            Shutdown(TweakEngine.RunElevatedRequest(e.Args[1]));
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("Error no controlado", args.Exception);
            MessageBox.Show($"{Lang.T("error.unexpected")}\n\n{args.Exception.Message}\n\n{Lang.T("boot.log")}\n{Logger.FilePath}",
                AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        string? launchUri = null;
        bool play = false, uninstall = false;
        for (int i = 0; i < e.Args.Length; i++)
        {
            string arg = e.Args[i];
            if (arg.Equals("-player", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
                launchUri = e.Args[++i];
            else if (arg.Equals("-play", StringComparison.OrdinalIgnoreCase))
                play = true;
            else if (arg.Equals("-uninstall", StringComparison.OrdinalIgnoreCase))
                uninstall = true;
            else if (arg.StartsWith("roblox", StringComparison.OrdinalIgnoreCase))
                launchUri = arg;
        }

        if (uninstall)
        {
            RunUninstall();
            return;
        }

        // New version on GitHub: download it, swap SenStrap.exe and start again with the same arguments.
        // Settings, mods and the installed Roblox are in %LOCALAPPDATA%\SenStrap and stay as they are.
        AppUpdater.CleanUp();
        bool skipUpdate = e.Args.Any(a => a.Equals(AppUpdater.SkipArgument, StringComparison.OrdinalIgnoreCase));
        if (!skipUpdate && settings.AutoUpdate && AppUpdater.CanUpdate)
        {
            var update = await AppUpdater.CheckAsync();
            if (update is not null)
            {
                var window = new UpdateWindow(update, settings);
                var closed = new TaskCompletionSource();
                window.Closed += (_, _) => closed.TrySetResult();
                window.Show();
                await closed.Task;
                if (window.Installed)
                {
                    AppUpdater.Restart(e.Args);
                    Shutdown();
                    return;
                }
            }
        }

        SelfInstaller.UpdateInstalledCopy();
        SelfInstaller.MigrateShortcuts();

        if (launchUri is not null || play)
        {
            var boot = new BootstrapperWindow(launchUri);
            boot.Closed += (_, _) =>
            {
                var current = Settings.Load();
                var overlay = boot.Result is not null && current.FpsCounter ? FpsOverlay.Start() : null;
                if (boot.Result is { } result && current.ActivityTracking)
                    new TrayHost(current, result).Start();
                else if (overlay is not null)
                    overlay.Closed += (_, _) => ShutdownAfter(boot.Result?.BackgroundUpdate);
                else
                    ShutdownAfter(boot.Result?.BackgroundUpdate);
            };
            boot.Show();
            return;
        }

        if (string.IsNullOrEmpty(settings.Language))
        {
            var welcome = new WelcomeWindow();
            welcome.Closed += (_, _) =>
            {
                if (welcome.Accepted) ShowMainWindow();
                else Shutdown();
            };
            welcome.Show();
            return;
        }

        ShowMainWindow();
    }

    /// <summary>
    /// Cierra SenStrap, pero si hay una actualización en segundo plano descargándose,
    /// espera a que termine (sin ventanas) para no dejarla a medias.
    /// </summary>
    public static async void ShutdownAfter(Task? background)
    {
        if (background is { IsCompleted: false })
        {
            Logger.Info("Esperando a que termine la actualización en segundo plano...");
            await background;
            Logger.Info("Actualización en segundo plano terminada.");
        }
        Current.Shutdown();
    }

    private void ShowMainWindow()
    {
        var main = new MainWindow();
        main.Closed += (_, _) =>
        {
            if (Windows.Count == 0) Shutdown();
        };
        main.Show();
    }

    private void RunUninstall()
    {
        var answer = MessageBox.Show(Lang.T("uninstall.confirm"), AppInfo.Name, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
        {
            try
            {
                SelfInstaller.Uninstall();
                MessageBox.Show(Lang.T("uninstall.done"), AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
                SelfInstaller.ScheduleFolderDeletion();
            }
            catch (Exception ex)
            {
                Logger.Error("Error al desinstalar", ex);
                MessageBox.Show($"{Lang.T("uninstall.error")}\n\n{ex.Message}", AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        Shutdown();
    }
}
