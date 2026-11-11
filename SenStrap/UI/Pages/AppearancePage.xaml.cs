using System.Windows;
using SenStrap.Core;

namespace SenStrap.UI.Pages;

public partial class AppearancePage : ShellPage
{
    private bool _loading;

    public AppearancePage(IShell shell) : base(shell)
    {
        InitializeComponent();
    }

    public override void OnShown()
    {
        _loading = true;
        var settings = Shell.Settings;
        SnowToggle.IsChecked = settings.Snow;
        LauncherSnowToggle.IsChecked = settings.EffectInLauncher;
        IntroToggle.IsChecked = settings.IntroAnimation;
        _loading = false;
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var settings = Shell.Settings;
        settings.Effect = SnowToggle.IsChecked == true ? BackgroundEffect.Nieve : BackgroundEffect.Ninguno;
        settings.EffectInLauncher = LauncherSnowToggle.IsChecked == true;
        settings.IntroAnimation = IntroToggle.IsChecked == true;
        Shell.ApplyAppearance();
        Shell.MarkDirty();
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        var demo = new BootstrapperWindow(null, Shell.Settings, demo: true)
        {
            Owner = Window.GetWindow(this),
        };
        demo.Show();
    }
}
