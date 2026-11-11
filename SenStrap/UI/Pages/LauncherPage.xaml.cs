using System.Windows;
using System.Windows.Controls;

namespace SenStrap.UI.Pages;

public partial class LauncherPage : ShellPage
{
    private bool _loading;

    public LauncherPage(IShell shell) : base(shell)
    {
        InitializeComponent();
    }

    public override void OnShown()
    {
        _loading = true;
        var s = Shell.Settings;
        ConfirmToggle.IsChecked = s.ConfirmLaunch;
        CrashToggle.IsChecked = s.CloseCrashHandler;
        BackgroundToggle.IsChecked = s.BackgroundUpdates;
        FsoToggle.IsChecked = s.DisableFullscreenOptimizations;
        DpiToggle.IsChecked = s.HighDpiOverride;
        CleanDownloadsToggle.IsChecked = s.CleanDownloads;
        CleanRobloxLogsToggle.IsChecked = s.CleanRobloxLogs;
        CleanAppLogsToggle.IsChecked = s.CleanAppLogs;
        CleanerCombo.SelectedItem = CleanerCombo.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(i => (string)i.Tag == s.CleanerDays.ToString()) ?? CleanerCombo.Items[2];
        UpdateCleanerRows();
        _loading = false;

        Anim.Cascade(Root);
    }

    private void UpdateCleanerRows()
    {
        bool on = CleanerCombo.SelectedItem is ComboBoxItem { Tag: string tag } && tag != "0";
        CleanDownloadsToggle.IsEnabled = CleanRobloxLogsToggle.IsEnabled = CleanAppLogsToggle.IsEnabled = on;
        CleanDownloadsToggle.Opacity = CleanRobloxLogsToggle.Opacity = CleanAppLogsToggle.Opacity = on ? 1 : 0.4;
    }

    private void Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var s = Shell.Settings;
        s.ConfirmLaunch = ConfirmToggle.IsChecked == true;
        s.CloseCrashHandler = CrashToggle.IsChecked == true;
        s.BackgroundUpdates = BackgroundToggle.IsChecked == true;
        s.DisableFullscreenOptimizations = FsoToggle.IsChecked == true;
        s.HighDpiOverride = DpiToggle.IsChecked == true;
        s.CleanDownloads = CleanDownloadsToggle.IsChecked == true;
        s.CleanRobloxLogs = CleanRobloxLogsToggle.IsChecked == true;
        s.CleanAppLogs = CleanAppLogsToggle.IsChecked == true;
        if (CleanerCombo.SelectedItem is ComboBoxItem { Tag: string days })
            s.CleanerDays = int.Parse(days);
        UpdateCleanerRows();
        Shell.MarkDirty();
    }
}
