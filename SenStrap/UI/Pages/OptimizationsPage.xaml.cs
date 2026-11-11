using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SenStrap.Core;
using SenStrap.Core.Tweaks;

namespace SenStrap.UI.Pages;

/// <summary>
/// Windows and Roblox optimizations. Toggles only mark what you want; "Apply" makes every change at once
/// (with a single administrator prompt for the machine-wide ones). Cleanups run right away.
/// </summary>
public partial class OptimizationsPage : ShellPage
{
    private sealed class Row
    {
        public required Tweak Tweak { get; init; }
        public required Border Card { get; init; }
        public CheckBox? Toggle { get; init; }
        public Button? RunButton { get; init; }
        public required Border Unavailable { get; init; }
        public bool Available { get; set; } = true;
        public bool Applied { get; set; }
        public bool Desired { get; set; }
        public bool Pending => !Tweak.IsAction && Available && Desired != Applied;
    }

    private readonly List<Row> _rows = new();
    private readonly Dictionary<TweakCategory, TextBlock> _headers = new();
    private bool _busy;
    private bool _loaded;

    public OptimizationsPage(IShell shell) : base(shell)
    {
        InitializeComponent();

        CategoryCombo.Items.Add(new ComboBoxItem { Content = Lang.T("opt.allCategories") });
        foreach (var category in Enum.GetValues<TweakCategory>())
            CategoryCombo.Items.Add(new ComboBoxItem { Content = Lang.T($"opt.cat.{category}"), Tag = category });
        CategoryCombo.SelectedIndex = 0;

        BuildList();
    }

    public override async void OnShown()
    {
        if (_busy) return;
        await RefreshAsync(keepPending: true);
    }

    // ---------- Building the list ----------

    private void BuildList()
    {
        foreach (var group in TweakCatalog.All.GroupBy(t => t.Category))
        {
            var header = new TextBlock
            {
                Text = Lang.T($"opt.cat.{group.Key}"),
                Style = (Style)FindResource("SectionTitle"),
                Margin = new Thickness(2, _headers.Count == 0 ? 0 : 18, 0, 8),
            };
            _headers[group.Key] = header;
            List.Children.Add(header);

            foreach (var tweak in group)
            {
                var row = BuildRow(tweak);
                _rows.Add(row);
                List.Children.Add(row.Card);
            }
        }
    }

    private Row BuildRow(Tweak tweak)
    {
        var titleLine = new WrapPanel { Orientation = Orientation.Horizontal };
        titleLine.Children.Add(new TextBlock
        {
            Text = tweak.DisplayName,
            Style = (Style)FindResource("CardTitle"),
            TextTrimming = TextTrimming.None,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (tweak.Recommended) titleLine.Children.Add(Badge(Lang.T("opt.badge.recommended"), "AccentTintBrush", "AccentBrush"));
        if (tweak.Admin) titleLine.Children.Add(Badge(Lang.T("opt.badge.admin"), null, "MutedBrush", ""));
        if (tweak.Caution) titleLine.Children.Add(Badge(Lang.T("opt.badge.caution"), null, "WarnBrush"));
        string? note = tweak.Note switch
        {
            TweakNote.Restart => Lang.T("opt.note.restart"),
            TweakNote.SignOut => Lang.T("opt.note.signout"),
            TweakNote.Explorer => Lang.T("opt.note.explorer"),
            TweakNote.NextLaunch => Lang.T("opt.note.nextLaunch"),
            _ => null,
        };
        if (note is not null) titleLine.Children.Add(Badge(note, null, "SubtleBrush"));
        var unavailable = Badge(Lang.T("opt.unavailable"), null, "SubtleBrush");
        unavailable.Visibility = Visibility.Collapsed;
        titleLine.Children.Add(unavailable);

        var text = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
        text.Children.Add(titleLine);
        text.Children.Add(new TextBlock
        {
            Text = tweak.DisplayDescription,
            Style = (Style)FindResource("CardDesc"),
            Margin = new Thickness(0, 3, 0, 0),
        });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(text);

        var card = new Border
        {
            Style = (Style)FindResource("Card"),
            Padding = new Thickness(16, 11, 16, 11),
            Margin = new Thickness(0, 0, 0, 6),
            Child = grid,
        };

        Row row;
        if (tweak.IsAction)
        {
            var run = new Button
            {
                Style = (Style)FindResource("SecondaryButton"),
                Content = Lang.T("opt.run"),
                MinWidth = 84,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(run, 1);
            grid.Children.Add(run);
            row = new Row { Tweak = tweak, Card = card, RunButton = run, Unavailable = unavailable };
            run.Click += async (_, _) => await RunAsync([new TweakOp(tweak.Id, true)]);
        }
        else
        {
            var toggle = new CheckBox { Style = (Style)FindResource("Toggle"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(toggle, 1);
            grid.Children.Add(toggle);
            row = new Row { Tweak = tweak, Card = card, Toggle = toggle, Unavailable = unavailable };
            toggle.Click += (_, _) => Toggle_Click(row);
        }
        return row;
    }

    private Border Badge(string text, string? backgroundKey, string foregroundKey, string? glyph = null)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (glyph is not null)
        {
            content.Children.Add(new TextBlock
            {
                Text = glyph,
                FontFamily = (FontFamily)FindResource("IconFont"),
                FontSize = 10,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource(foregroundKey),
            });
        }
        content.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource(foregroundKey),
        });
        return new Border
        {
            Style = (Style)FindResource("Badge"),
            Background = backgroundKey is null ? new SolidColorBrush(Color.FromRgb(0x20, 0x23, 0x32)) : (Brush)FindResource(backgroundKey),
            Child = content,
        };
    }

    // ---------- State ----------

    /// <param name="keepPending">Keep toggles the user changed but didn't apply yet (coming back to the page).</param>
    private async Task RefreshAsync(bool keepPending = false)
    {
        var store = TweakStore.Load();
        var tweaks = _rows.Select(r => r.Tweak).ToList();
        var states = await Task.Run(() => tweaks.Select(t =>
        {
            bool available = Safe(t.IsAvailable);
            bool applied = available && !t.IsAction && Safe(() => t.IsApplied(new TweakContext(store)));
            return (available, applied);
        }).ToList());

        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            bool keep = keepPending && row.Pending;
            row.Available = states[i].available;
            row.Applied = states[i].applied;
            if (!keep || !row.Available) row.Desired = row.Applied;
            if (row.Toggle is not null)
            {
                row.Toggle.IsChecked = row.Desired;
                row.Toggle.IsEnabled = row.Available && !_busy;
            }
            if (row.RunButton is not null) row.RunButton.IsEnabled = row.Available && !_busy;
            row.Unavailable.Visibility = row.Available ? Visibility.Collapsed : Visibility.Visible;
            row.Card.Opacity = row.Available ? 1 : 0.55;
        }
        _loaded = true;
        UpdatePending();
        ApplyFilter();
    }

    private static bool Safe(Func<bool> check)
    {
        try
        {
            return check();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Couldn't read a tweak's state: {ex.Message}");
            return false;
        }
    }

    private void Toggle_Click(Row row)
    {
        row.Desired = row.Toggle!.IsChecked == true;

        // Only one power plan can be active
        if (row.Desired && row.Tweak.ExclusiveGroup is { } group)
        {
            foreach (var other in _rows.Where(r => r != row && r.Tweak.ExclusiveGroup == group && r.Desired))
            {
                other.Desired = false;
                other.Toggle!.IsChecked = false;
            }
        }
        UpdatePending();
    }

    private void UpdatePending()
    {
        var pending = _rows.Where(r => r.Pending).ToList();
        foreach (var row in _rows)
            row.Card.BorderBrush = (Brush)FindResource(row.Pending ? "AccentBrush" : "CardBorderBrush");

        PendingBar.Visibility = pending.Count > 0 || _busy ? Visibility.Visible : Visibility.Collapsed;
        if (!_busy)
            PendingText.Text = pending.Count == 1 ? Lang.T("opt.pendingOne") : Lang.F("opt.pending", pending.Count);
        ApplyShield.Visibility = pending.Any(r => r.Tweak.Admin) && !TweakEngine.IsElevated ? Visibility.Visible : Visibility.Collapsed;

        int applicable = _rows.Count(r => !r.Tweak.IsAction && r.Available);
        int applied = _rows.Count(r => !r.Tweak.IsAction && r.Available && r.Applied);
        SummaryText.Text = Lang.F("opt.summary", applied, applicable, _rows.Count);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        foreach (var row in _rows)
        {
            if (row.Toggle is not null) row.Toggle.IsEnabled = !busy && row.Available;
            if (row.RunButton is not null) row.RunButton.IsEnabled = !busy && row.Available;
        }
        RecommendedButton.IsEnabled = RevertAllButton.IsEnabled = ApplyButton.IsEnabled = DiscardButton.IsEnabled = !busy;
        if (busy)
        {
            PendingBar.Visibility = Visibility.Visible;
            PendingText.Text = Lang.T("opt.working");
        }
    }

    // ---------- Running ----------

    private async Task RunAsync(IReadOnlyList<TweakOp> ops)
    {
        if (_busy || ops.Count == 0) return;
        SetBusy(true);
        List<TweakResult> results;
        try
        {
            results = await TweakEngine.ExecuteAsync(ops);
        }
        catch (Exception ex)
        {
            Logger.Error("Tweaks failed", ex);
            results = ops.Select(o => new TweakResult(o.Id, false, ex.Message)).ToList();
        }
        SetBusy(false);
        Report(results);
        await RefreshAsync();
    }

    private void Report(List<TweakResult> results)
    {
        var done = results.Where(r => r.Ok).ToList();
        var failed = results.Where(r => !r.Ok && r.Error != TweakEngine.Cancelled).ToList();
        bool cancelled = results.Any(r => r.Error == TweakEngine.Cancelled);
        var doneTweaks = done.Select(r => TweakCatalog.Find(r.Id)).OfType<Tweak>().ToList();

        if (failed.Count > 0)
        {
            var first = TweakCatalog.Find(failed[0].Id);
            Shell.ShowToast(Lang.F("opt.failed", failed.Count, $"{first?.DisplayName}: {failed[0].Error}"), ok: false);
        }
        else if (cancelled)
        {
            Shell.ShowToast(Lang.T("opt.uacDenied"), ok: false);
        }
        else if (doneTweaks.Count > 0 && doneTweaks.All(t => t.IsAction))
        {
            long freed = done.Sum(r => r.Freed);
            Shell.ShowToast(freed > 0 ? Lang.F("opt.freed", TweakCatalog.FormatSize(freed)) : Lang.T("opt.cleaned"));
        }
        else if (done.Count > 0)
        {
            Shell.ShowToast(Lang.T("opt.done"));
        }

        // Changes that only show up after restarting Explorer or the PC
        bool explorer = doneTweaks.Any(t => t.Note == TweakNote.Explorer);
        bool restart = doneTweaks.Any(t => t.Note == TweakNote.Restart);
        bool signOut = doneTweaks.Any(t => t.Note == TweakNote.SignOut);
        if (explorer || restart || signOut)
        {
            NoticeText.Text = restart ? Lang.T("opt.noticeRestart")
                : signOut ? Lang.T("opt.noticeSignOut")
                : Lang.T("opt.noticeExplorer");
            RestartExplorerButton.Visibility = explorer ? Visibility.Visible : Visibility.Collapsed;
            NoticeCard.Visibility = Visibility.Visible;
        }
    }

    private async void Apply_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(_rows.Where(r => r.Pending).Select(r => new TweakOp(r.Tweak.Id, r.Desired)).ToList());

    private void Discard_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows.Where(r => r.Pending))
        {
            row.Desired = row.Applied;
            row.Toggle!.IsChecked = row.Applied;
        }
        UpdatePending();
    }

    private async void Recommended_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        var ops = _rows.Where(r => r.Tweak.Recommended && !r.Tweak.IsAction && r.Available && !r.Applied)
            .Select(r => new TweakOp(r.Tweak.Id, true))
            .ToList();
        if (ops.Count == 0)
        {
            Shell.ShowToast(Lang.T("opt.allRecommended"));
            return;
        }

        bool admin = ops.Any(o => TweakCatalog.Find(o.Id)!.Admin) && !TweakEngine.IsElevated;
        string message = Lang.F("opt.confirmRecommended", ops.Count) + (admin ? "\n\n" + Lang.T("opt.adminNote") : "");
        if (MessageBox.Show(Window.GetWindow(this)!, message, AppInfo.Name, MessageBoxButton.OKCancel,
                MessageBoxImage.Question) != MessageBoxResult.OK)
            return;
        await RunAsync(ops);
    }

    private async void RevertAll_Click(object sender, RoutedEventArgs e)
    {
        var ops = TweakEngine.RevertAllOps();
        if (ops.Count == 0)
        {
            Shell.ShowToast(Lang.T("opt.nothingToRevert"));
            return;
        }
        if (MessageBox.Show(Window.GetWindow(this)!, Lang.F("opt.confirmRevert", ops.Count), AppInfo.Name,
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;
        await RunAsync(ops);
    }

    private async void RestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Window.GetWindow(this)!, Lang.T("opt.confirmExplorer"), AppInfo.Name,
                MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK)
            return;
        RestartExplorerButton.IsEnabled = false;
        try
        {
            await TweakEngine.RestartExplorerAsync();
            NoticeCard.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Couldn't restart Explorer: {ex.Message}");
        }
        RestartExplorerButton.IsEnabled = true;
    }

    // ---------- Search ----------

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) ApplyFilter();
    }

    private void ApplyFilter()
    {
        string query = SearchBox.Text.Trim();
        var category = (CategoryCombo.SelectedItem as ComboBoxItem)?.Tag as TweakCategory?;
        int shown = 0;

        foreach (var row in _rows)
        {
            var t = row.Tweak;
            bool visible = (category is null || t.Category == category)
                && (query.Length == 0
                    || t.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || t.DisplayDescription.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || t.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
            row.Card.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (visible) shown++;
        }
        foreach (var (key, header) in _headers)
        {
            bool any = _rows.Any(r => r.Tweak.Category == key && r.Card.Visibility == Visibility.Visible);
            header.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        }
        EmptyText.Visibility = shown == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
