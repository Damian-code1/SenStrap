using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SenStrap.Core;

namespace SenStrap.UI;

/// <summary>"Downloading SenStrap update…": shown when a new version is found on GitHub.</summary>
public partial class UpdateWindow : Window
{
    private readonly AppUpdate _update;
    private readonly CancellationTokenSource _cancel = new();

    /// <summary>True if the new version is in place and should be started.</summary>
    public bool Installed { get; private set; }

    public UpdateWindow(AppUpdate update, Settings settings)
    {
        InitializeComponent();
        _update = update;
        Snow.IsSnowing = settings.EffectInLauncher && settings.Snow;
        Clipped.SizeChanged += (_, _) =>
            Clipped.Clip = new RectangleGeometry(new Rect(0, 0, Clipped.ActualWidth, Clipped.ActualHeight), 9, 9);
        Loaded += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        double totalMb = Math.Max(1, _update.Size / 1048576.0);
        StatusText.Text = Lang.F("update.downloading", _update.Version.ToString(3), 0, (int)totalMb);
        var progress = new Progress<(long Done, long Total)>(p =>
        {
            double fraction = p.Total > 0 ? (double)p.Done / p.Total : 0;
            Progress.IsIndeterminate = false;
            Progress.Value = fraction;
            PercentText.Text = $"{fraction:P0}";
            StatusText.Text = Lang.F("update.downloading", _update.Version.ToString(3), (int)(p.Done / 1048576), (int)(p.Total / 1048576));
        });

        try
        {
            await Task.Run(() => AppUpdater.InstallAsync(_update, progress, _cancel.Token));
            StatusText.Text = Lang.T("update.restarting");
            Installed = true;
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
        {
            Logger.Info("Update cancelled by the user.");
        }
        catch (Exception ex)
        {
            Logger.Error("Couldn't update SenStrap", ex);
            MessageBox.Show(this, $"{Lang.T("update.failed")}\n\n{ex.Message}", AppInfo.Name,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        _cancel.Cancel();
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
