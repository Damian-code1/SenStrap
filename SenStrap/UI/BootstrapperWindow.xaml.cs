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
            // Opens with a fade and a slight zoom; the ambient animations run until it closes
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Frame.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Ms(260)) { EasingFunction = ease });
            FrameScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, Ms(320)) { EasingFunction = ease });
            FrameScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, Ms(320)) { EasingFunction = ease });
            ((Storyboard)Resources["Ambient"]).Begin(this, true);
            if (autoStart)
                await (_demo ? RunDemoAsync() : RunAsync());
        };
    }

    private static Duration Ms(double ms) => new(TimeSpan.FromMilliseconds(ms));

    private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };

    private double? _progress;   // null while there's no percentage
    private string _statusKind = "";

    /// <summary>Updates the text and the progress bar.</summary>
    public void ShowStatus(BootstrapStatus status)
    {
        SetStatusText(status.Message);

        if (status.Progress is double value)
        {
            value = Math.Clamp(value, 0, 1);
            if (_progress is null)
            {
                // Waiting band out, filled bar in from zero
                Sweep.BeginAnimation(OpacityProperty, new DoubleAnimation(0, Ms(200)));
                Fill.BeginAnimation(WidthProperty, null);
                Fill.Width = 0;
                Fill.BeginAnimation(OpacityProperty, new DoubleAnimation(1, Ms(150)));
            }
            _progress = value;
            Fill.BeginAnimation(WidthProperty, new DoubleAnimation(value * Bar.ActualWidth, Ms(260)) { EasingFunction = EaseOut });
            PercentText.Text = $"{value:P0}";
        }
        else if (_progress is not null || Sweep.Opacity < 1)
        {
            _progress = null;
            Fill.BeginAnimation(OpacityProperty, new DoubleAnimation(0, Ms(200)));
            Sweep.BeginAnimation(OpacityProperty, new DoubleAnimation(1, Ms(250)));
            PercentText.Text = "";
        }
    }

    /// <summary>
    /// Sets the status line. When the step changes (not just the numbers in "Downloading 12 / 218 MB")
    /// the new text slides up and fades in.
    /// </summary>
    private void SetStatusText(string text)
    {
        if (StatusText.Text == text) return;
        StatusText.Text = text;

        string kind = new(text.Where(c => !char.IsDigit(c)).ToArray());
        if (kind == _statusKind) return;
        _statusKind = kind;

        StatusText.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Ms(280)) { EasingFunction = EaseOut });
        StatusShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(6, 0, Ms(320)) { EasingFunction = EaseOut });
    }

    private void Bar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_progress is not double value) return;
        Fill.BeginAnimation(WidthProperty, null);
        Fill.Width = value * Bar.ActualWidth;
    }

    private bool _closeAnimated;

    /// <summary>Fades and shrinks a little before closing.</summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || _closeAnimated || !IsLoaded) return;

        e.Cancel = true;
        _closeAnimated = true;
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(0, Ms(180)) { EasingFunction = ease };
        fade.Completed += (_, _) => Close();
        FrameScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.97, Ms(180)) { EasingFunction = ease });
        FrameScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.97, Ms(180)) { EasingFunction = ease });
        Frame.BeginAnimation(OpacityProperty, fade);
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
