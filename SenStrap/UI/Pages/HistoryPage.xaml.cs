using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SenStrap.Core;

namespace SenStrap.UI.Pages;

public partial class HistoryPage : ShellPage
{
    /// <summary>Lo que se enseña de cada partida.</summary>
    public sealed record Item(GameEntry Entry, string Name, string Details, ImageSource? Icon);

    public HistoryPage(IShell shell) : base(shell)
    {
        InitializeComponent();
    }

    /// <summary>"11/11/2026 7:50 · 1 h 20 min · Buenos Aires, AR"</summary>
    public static string Describe(GameEntry entry)
    {
        var parts = new List<string> { entry.JoinedAt.ToString("g") };
        if (entry.Seconds >= 30) parts.Add(Lang.Duration(entry.Seconds));
        if (!string.IsNullOrEmpty(entry.Location)) parts.Add(entry.Location);
        return string.Join("  ·  ", parts);
    }

    public override void OnShown()
    {
        var games = GameHistory.Load();
        GameList.ItemsSource = games.Select(g => new Item(g, g.DisplayName, Describe(g),
            g.IconUrl is { Length: > 0 } url ? new BitmapImage(new Uri(url)) : null)).ToList();

        EmptyState.Visibility = games.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ListScroll.Visibility = games.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        double total = AppState.Load().TotalPlaySeconds;
        TotalChip.Visibility = total >= 60 ? Visibility.Visible : Visibility.Collapsed;
        TotalText.Text = Lang.F("history.total", Lang.Duration(total));
    }

    private void Rejoin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: GameEntry game })
            Shell.Rejoin(game);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        GameHistory.Clear();
        OnShown();
    }
}
