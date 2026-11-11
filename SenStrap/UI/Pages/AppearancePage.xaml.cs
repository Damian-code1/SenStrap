using System.Windows;
using System.Windows.Controls;
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
        EffectCombo.SelectedItem = EffectCombo.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(i => (string)i.Tag == settings.Effect.ToString());
        InteractiveToggle.IsChecked = settings.InteractiveParticles;
        AnimationsToggle.IsChecked = settings.Animations;
        LauncherEffectToggle.IsChecked = settings.EffectInLauncher;
        _loading = false;

        Anim.Cascade(Root);
    }

    private void Effect_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || EffectCombo.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        Shell.Settings.Effect = Enum.Parse<BackgroundEffect>(tag);
        Shell.ApplyAppearance();
        Shell.MarkDirty();
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Shell.Settings.InteractiveParticles = InteractiveToggle.IsChecked == true;
        Shell.Settings.Animations = AnimationsToggle.IsChecked == true;
        Shell.Settings.EffectInLauncher = LauncherEffectToggle.IsChecked == true;
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
