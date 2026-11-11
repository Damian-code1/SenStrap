using System.Windows;

namespace SenStrap.UI.Pages;

public partial class IntegrationsPage : ShellPage
{
    private bool _loading;

    public IntegrationsPage(IShell shell) : base(shell)
    {
        InitializeComponent();
    }

    public override void OnShown()
    {
        _loading = true;
        var s = Shell.Settings;
        TrackingToggle.IsChecked = s.ActivityTracking;
        LocationToggle.IsChecked = s.ServerLocation;
        PlaytimeToggle.IsChecked = s.PlaytimeCounter;
        HistoryToggle.IsChecked = s.SaveGameHistory;
        AutoRejoinToggle.IsChecked = s.AutoRejoin;
        UpdateDependent();
        _loading = false;

    }

    private void UpdateDependent()
    {
        bool on = TrackingToggle.IsChecked == true;
        Dependent.IsEnabled = on;
        Dependent.Opacity = on ? 1 : 0.45;
    }

    private void Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var s = Shell.Settings;
        s.ActivityTracking = TrackingToggle.IsChecked == true;
        s.ServerLocation = LocationToggle.IsChecked == true;
        s.PlaytimeCounter = PlaytimeToggle.IsChecked == true;
        s.SaveGameHistory = HistoryToggle.IsChecked == true;
        s.AutoRejoin = AutoRejoinToggle.IsChecked == true;
        UpdateDependent();
        Shell.MarkDirty();
    }
}
