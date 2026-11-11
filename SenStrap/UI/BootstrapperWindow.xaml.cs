using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SenStrap.Core;

namespace SenStrap.UI;

/// <summary>La ventana con el logo y la barra de progreso que sale al darle a Jugar.</summary>
public partial class BootstrapperWindow : Window
{
    private readonly string? _launchUri;
    private readonly bool _demo;
    private readonly CancellationTokenSource _cancel = new();

    /// <summary>Se rellena si Roblox se abrió (para el seguimiento de actividad).</summary>
    public LaunchResult? Result { get; private set; }

    /// <param name="launchUri">Enlace de roblox.com, o null para abrir la app de Roblox.</param>
    /// <param name="settings">Ajustes de apariencia (por defecto, los guardados).</param>
    /// <param name="demo">Solo enseña una simulación, sin abrir Roblox (vista previa en Apariencia).</param>
    /// <param name="autoStart">Empezar al abrirse la ventana.</param>
    public BootstrapperWindow(string? launchUri, Settings? settings = null, bool demo = false, bool autoStart = true)
    {
        InitializeComponent();
        _launchUri = launchUri;
        _demo = demo;

        settings ??= Settings.Load();
        Snow.IsSnowing = settings.EffectInLauncher && settings.Snow;

        Clipped.SizeChanged += (_, _) =>
            Clipped.Clip = new RectangleGeometry(new Rect(0, 0, Clipped.ActualWidth, Clipped.ActualHeight), 9, 9);

        Loaded += async (_, _) =>
        {
            // Short fade in when the window opens
            Frame.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            if (autoStart)
                await (_demo ? RunDemoAsync() : RunAsync());
        };
    }

    /// <summary>Updates the text and the progress bar.</summary>
    public void ShowStatus(BootstrapStatus status)
    {
        StatusText.Text = status.Message;
        if (status.Progress is double value)
        {
            Progress.IsIndeterminate = false;
            Progress.Value = value;
            PercentText.Text = $"{value:P0}";
        }
        else
        {
            Progress.IsIndeterminate = true;
            PercentText.Text = "";
        }
    }

    private Task<bool> ConfirmCloseRunningAsync() =>
        Dispatcher.InvokeAsync(() => MessageBox.Show(this, Lang.T("launcher.confirmPrompt"), AppInfo.Name,
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes).Task;

    private async Task RunAsync()
    {
        // Progress<T> se crea en el hilo de la ventana: los avisos llegan ya al hilo correcto
        var progress = new Progress<BootstrapStatus>(ShowStatus);
        try
        {
            Result = await Task.Run(() => Launcher.RunAsync(_launchUri, progress, ConfirmCloseRunningAsync, _cancel.Token));
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
        {
            Logger.Info("Cancelado por el usuario.");
        }
        catch (Exception ex)
        {
            Logger.Error("No se pudo iniciar Roblox", ex);
            string detail = ex.GetBaseException() is { } inner && inner != ex ? $"\n({inner.Message})" : "";
            MessageBox.Show(this,
                $"{Lang.T("boot.error")}\n\n{ex.Message}{detail}\n\n{Lang.T("boot.log")}\n{Logger.FilePath}",
                AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        Close();
    }

    /// <summary>Simulación para la vista previa: no descarga ni abre nada.</summary>
    private async Task RunDemoAsync()
    {
        try
        {
            var ct = _cancel.Token;
            ShowStatus(new BootstrapStatus(Lang.T("status.checking")));
            await Task.Delay(1200, ct);
            for (int mb = 0; mb <= 218; mb += 4)
            {
                ShowStatus(new BootstrapStatus(Lang.F("status.downloading", mb, 218), mb / 218.0));
                await Task.Delay(40, ct);
            }
            for (int i = 0; i <= 40; i++)
            {
                ShowStatus(new BootstrapStatus(Lang.T("status.installing"), i / 40.0));
                await Task.Delay(35, ct);
            }
            ShowStatus(new BootstrapStatus(Lang.T("status.applying")));
            await Task.Delay(700, ct);
            ShowStatus(new BootstrapStatus(Lang.T("status.demo")));
            await Task.Delay(1400, ct);
        }
        catch (OperationCanceledException)
        {
        }
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        StatusText.Text = Lang.T("boot.cancelling");
        _cancel.Cancel();
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
