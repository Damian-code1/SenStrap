using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using SenStrap.Core;

namespace SenStrap.UI.Pages;

public partial class HomePage : ShellPage
{
    private bool _animationsStarted;
    private GameEntry? _lastGame;

    public HomePage(IShell shell) : base(shell)
    {
        InitializeComponent();
    }

    public override void OnShown()
    {
        InstallCard.Visibility = SelfInstaller.IsInstalled() ? Visibility.Collapsed : Visibility.Visible;

        var state = AppState.Load();
        RobloxChip.Text = state.VersionGuid is not null && state.RobloxVersion is not null
            ? Lang.F("home.roblox", state.RobloxVersion)
            : Lang.T("home.robloxPending");
        FlagsChip.Text = Lang.F("home.flagsCount", Shell.Settings.FastFlags.Count);
        ModsChip.Text = Lang.F("home.modsCount", Mods.List().Count);
        PlaytimeChipBorder.Visibility = state.TotalPlaySeconds >= 60 ? Visibility.Visible : Visibility.Collapsed;
        PlaytimeChip.Text = Lang.F("home.playtime", Lang.Duration(state.TotalPlaySeconds));

        _lastGame = GameHistory.Load().FirstOrDefault();
        LastGameCard.Visibility = _lastGame is null ? Visibility.Collapsed : Visibility.Visible;
        if (_lastGame is not null)
        {
            LastGameName.Text = _lastGame.DisplayName;
            LastGameDetails.Text = HistoryPage.Describe(_lastGame);
            LastGameIcon.ImageSource = _lastGame.IconUrl is { Length: > 0 } url ? new BitmapImage(new Uri(url)) : null;
        }

        if (!_animationsStarted)
        {
            _animationsStarted = true;
            Anim.Float(Logo);
            Anim.Pulse(Halo);
            Anim.SpinBorder(Hero, 9);
        }
        Anim.Cascade(Root);
        Anim.Cascade(Chips, 60, 8);
    }

    private void Play_Click(object sender, RoutedEventArgs e) => Shell.Play();

    private void Rejoin_Click(object sender, RoutedEventArgs e)
    {
        if (_lastGame is not null) Shell.Rejoin(_lastGame);
    }

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SelfInstaller.Install(DesktopShortcut.IsChecked == true);
            OnShown();
            Shell.ShowToast(Lang.T("home.installed"));

            // Si se abrió desde Descargas, a partir de ahora se usa la copia instalada
            if (!AppInfo.IsRunningFromInstallDir)
            {
                Process.Start(new ProcessStartInfo(AppInfo.InstalledExe) { UseShellExecute = true });
                Window.GetWindow(this)?.Close();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error al instalar", ex);
            MessageBox.Show(Window.GetWindow(this)!, $"{Lang.T("home.installError")}\n\n{ex.Message}", AppInfo.Name,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Mods_Click(object sender, RoutedEventArgs e) => Shell.Navigate("Mods");

    private void Flags_Click(object sender, RoutedEventArgs e) => Shell.Navigate("Flags");

    private void History_Click(object sender, RoutedEventArgs e) => Shell.Navigate("History");
}
