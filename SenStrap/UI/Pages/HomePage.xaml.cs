using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SenStrap.Core;
using SenStrap.Core.Tweaks;

namespace SenStrap.UI.Pages;

public partial class HomePage : ShellPage
{
    private GameEntry? _lastGame;

    public HomePage(IShell shell) : base(shell)
    {
        InitializeComponent();
    }

    public override async void OnShown()
    {
        InstallCard.Visibility = SelfInstaller.IsInstalled() ? Visibility.Collapsed : Visibility.Visible;

        int hour = DateTime.Now.Hour;
        Greeting.Text = Lang.T(hour < 12 ? "home.greetMorning" : hour < 20 ? "home.greetAfternoon" : "home.greetEvening");

        var state = AppState.Load();
        bool hasRoblox = state.VersionGuid is not null && state.RobloxVersion is not null;
        StatusLine.Text = hasRoblox ? $"{Lang.T("home.ready")}  ·  {Lang.F("home.roblox", state.RobloxVersion!)}" : Lang.T("home.robloxPending");

        var games = GameHistory.Load();
        _lastGame = games.FirstOrDefault();
        ShowHero();

        StatPlaytime.Text = Lang.Duration(state.TotalPlaySeconds);
        StatGames.Text = games.Select(g => g.PlaceId).Distinct().Count().ToString();
        StatFlags.Text = Shell.Settings.FastFlags.Count.ToString();
        StatOptimizations.Text = "…";

        BuildRecent(games.Skip(1).GroupBy(g => g.PlaceId).Select(g => g.First()).Take(3).ToList());

        // Optimizations read the registry: off the UI thread
        var store = TweakStore.Load();
        int on = await Task.Run(() => TweakCatalog.All.Count(t =>
        {
            if (t.IsAction || (t.HiddenUnlessApplied && !store.Backups.ContainsKey(t.Id))) return false;
            try { return t.IsAvailable() && t.IsApplied(new TweakContext(store)); }
            catch { return false; }
        }));
        StatOptimizations.Text = on.ToString();

        if (_lastGame is not null && await GameHistory.GetThumbnailUrlAsync(_lastGame.UniverseId) is { } url)
            HeroImage.ImageSource = Image(url);
    }

    private void ShowHero()
    {
        HeroImage.ImageSource = null;
        if (_lastGame is null)
        {
            HeroLabel.Text = "SenStrap";
            HeroTitle.Text = Lang.T("home.ready");
            HeroDetails.Text = Lang.T("home.noGames");
            HeroRejoin.Visibility = Visibility.Collapsed;
            HeroPlay.Style = (Style)FindResource("PrimaryButton");
            HeroLogo.Visibility = Visibility.Visible;
            return;
        }

        HeroLabel.Text = Lang.T("home.continue");
        HeroTitle.Text = _lastGame.DisplayName;
        HeroDetails.Text = HistoryPage.Describe(_lastGame);
        HeroRejoin.Visibility = Visibility.Visible;
        HeroPlay.Style = (Style)FindResource("SecondaryButton");
        HeroLogo.Visibility = Visibility.Collapsed;
        if (_lastGame.IconUrl is { Length: > 0 } icon)
            HeroImage.ImageSource = Image(icon); // until the wide thumbnail arrives
    }

    private void BuildRecent(List<GameEntry> games)
    {
        RecentGrid.Children.Clear();
        RecentHeader.Visibility = RecentGrid.Visibility = games.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        for (int i = 0; i < games.Count; i++)
        {
            var game = games[i];
            var icon = new Border
            {
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(6),
                Background = game.IconUrl is { Length: > 0 } url
                    ? new ImageBrush(Image(url)) { Stretch = Stretch.UniformToFill }
                    : (Brush)FindResource("CardHoverBrush"),
            };
            var text = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = game.DisplayName, Style = (Style)FindResource("CardTitle") });
            text.Children.Add(new TextBlock
            {
                Text = Lang.F("home.recentDetails", Lang.Duration(game.Seconds)),
                Style = (Style)FindResource("CardDesc"),
                Margin = new Thickness(0, 2, 0, 0),
            });
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(icon);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            var tile = new Button
            {
                Style = (Style)FindResource("CardAction"),
                Padding = new Thickness(12, 11, 12, 11),
                Margin = new Thickness(i == 0 ? 0 : 4, 0, i == games.Count - 1 ? 0 : 4, 0),
                Content = grid,
                ToolTip = Lang.T("history.rejoin"),
            };
            tile.Click += (_, _) => Shell.Rejoin(game);
            RecentGrid.Children.Add(tile);
        }
        // Keep tiles the same width when there are fewer than three
        for (int i = games.Count; i < 3 && games.Count > 0; i++)
            RecentGrid.Children.Add(new Border());
    }

    private static BitmapImage Image(string url)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(url);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        return image;
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

            // Opened from Downloads: from now on the installed copy is used
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

    private void Flags_Click(object sender, RoutedEventArgs e) => Shell.Navigate("Flags");

    private void History_Click(object sender, RoutedEventArgs e) => Shell.Navigate("History");

    private void Optimizations_Click(object sender, RoutedEventArgs e) => Shell.Navigate("Optimizations");
}
