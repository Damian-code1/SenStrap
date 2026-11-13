using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SenStrap.Core;
using SenStrap.UI.Pages;

namespace SenStrap.UI;

public partial class MainWindow : Window, IShell
{
    private readonly Dictionary<string, ShellPage> _pages = new();
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(2.6) };
    private Storyboard? _intro;
    private bool _dirty;

    public Settings Settings { get; private set; } = Settings.Load();

    private string _current = "Home";

    public MainWindow()
    {
        InitializeComponent();
        VersionLabel.Text = $"SenStrap {AppInfo.Version}";
        SourceInitialized += (_, _) => RoundCorners();
        ApplyAppearance();

        StateChanged += (_, _) =>
        {
            // Without this a borderless window goes slightly off-screen when maximized
            RootGrid.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "" : "";
        };

        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            Toast.Visibility = Visibility.Collapsed;
        };

        if (Settings.IntroAnimation) PrepareIntro();

        Loaded += (_, _) =>
        {
            Navigate("Home");
            if (Settings.IntroAnimation) PlayIntro();
            // Playtime and history are written by the SenStrap that watches the game (another process):
            // reload them when you come back to this window, e.g. after playing
            Activated += (_, _) =>
            {
                if (PageHost.Content is HomePage or HistoryPage)
                    ((ShellPage)PageHost.Content).OnShown();
            };
        };
    }

    // ---------- Intro ----------

    private void PrepareIntro()
    {
        Shell.Opacity = 0;
        Intro.Visibility = Visibility.Visible;
        IntroMark.Opacity = 0;
        PreviewKeyDown += SkipIntroOnKey;
    }

    /// <summary>The logo fades in over the snow, a line fills under it, then the window appears.</summary>
    private void PlayIntro()
    {
        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        var easeInOut = new CubicEase { EasingMode = EasingMode.EaseInOut };

        var markScale = new ScaleTransform(0.94, 0.94);
        var markMove = new TranslateTransform();
        IntroMark.RenderTransformOrigin = new Point(0.5, 0.5);
        IntroMark.RenderTransform = new TransformGroup { Children = { markScale, markMove } };

        var shellMove = new TranslateTransform(0, 10);
        Shell.RenderTransform = shellMove;

        var story = new Storyboard();

        // Each property gets one timeline of (time in ms, value) steps
        void Add(DependencyObject target, string path, IEasingFunction ease, params (int Ms, double Value)[] steps)
        {
            var animation = new DoubleAnimationUsingKeyFrames();
            foreach (var (ms, value) in steps)
                animation.KeyFrames.Add(new EasingDoubleKeyFrame(value, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms)), ease));
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath(path));
            story.Children.Add(animation);
        }

        Add(IntroMark, nameof(Opacity), easeOut, (0, 0), (420, 1), (1020, 1), (1320, 0));
        Add(IntroMark, "RenderTransform.Children[0].ScaleX", easeOut, (0, 0.94), (520, 1));
        Add(IntroMark, "RenderTransform.Children[0].ScaleY", easeOut, (0, 0.94), (520, 1));
        Add(IntroMark, "RenderTransform.Children[1].Y", easeOut, (1020, 0), (1320, -12));
        Add(IntroBar, nameof(Width), easeInOut, (180, 0), (940, 132));
        Add(Shell, nameof(Opacity), easeOut, (1080, 0), (1460, 1));
        Add(Shell, "RenderTransform.Y", easeOut, (1080, 10), (1500, 0));

        story.Completed += (_, _) => FinishIntro();
        _intro = story;
        story.Begin(this, isControllable: true);
    }

    private void FinishIntro()
    {
        if (Intro.Visibility != Visibility.Visible) return;
        _intro?.Stop(this);
        _intro = null;
        PreviewKeyDown -= SkipIntroOnKey;
        Intro.Visibility = Visibility.Collapsed;
        Shell.Opacity = 1;
        Shell.RenderTransform = Transform.Identity;
    }

    private void Intro_MouseDown(object sender, MouseButtonEventArgs e) => FinishIntro();

    private void SkipIntroOnKey(object sender, KeyEventArgs e) => FinishIntro();

    // ---------- IShell ----------

    public void Navigate(string key)
    {
        if (!_pages.TryGetValue(key, out var page))
        {
            page = key switch
            {
                "Launcher" => new LauncherPage(this),
                "Integrations" => new IntegrationsPage(this),
                "History" => new HistoryPage(this),
                "Optimizations" => new OptimizationsPage(this),
                "Flags" => new FlagsPage(this),
                "GameSettings" => new GameSettingsPage(this),
                "GlobalSettings" => new GameSettingsPage(this, global: true),
                "Mods" => new ModsPage(this),
                "Appearance" => new AppearancePage(this),
                "Settings" => new SettingsPage(this),
                "Credits" => new CreditsPage(this),
                _ => new HomePage(this),
            };
            _pages[key] = page;
        }
        if (ReferenceEquals(PageHost.Content, page)) return;

        _current = key;
        PageHost.Content = page;
        var roblox = AppState.Load().RobloxVersion;
        RobloxLabel.Text = roblox is null ? "" : $"Roblox {roblox}";
        foreach (var item in NavPanel.Children.OfType<RadioButton>())
        {
            if ((string)item.CommandParameter == key)
            {
                if (item.IsChecked != true) item.IsChecked = true;
            }
        }
        page.OnShown();
    }

    public void ApplyAppearance()
    {
        Snow.IsSnowing = Settings.Snow;
    }

    public void MarkDirty() => SetDirty(true);

    public void ShowToast(string message, bool ok = true)
    {
        ToastText.Text = message;
        ToastIcon.Text = ok ? "" : "";
        ToastIcon.Foreground = (Brush)FindResource(ok ? "OkBrush" : "ErrorBrush");
        Toast.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(message.Length / 18.0, 2.6, 6));
        _toastTimer.Start();
    }

    public void Play() => Launch(null);

    public void Rejoin(GameEntry game) => Launch(game.RejoinUri);

    /// <summary>Starts SenStrap in "play" mode (with or without a link) and closes this window.</summary>
    private void Launch(string? uri)
    {
        if (_dirty && !SaveAll()) return;
        string exe = SelfInstaller.IsInstalled() ? AppInfo.InstalledExe : AppInfo.CurrentExe;
        var info = new ProcessStartInfo(exe) { UseShellExecute = false };
        if (uri is null)
        {
            info.ArgumentList.Add("-play");
        }
        else
        {
            info.ArgumentList.Add("-player");
            info.ArgumentList.Add(uri);
        }
        Process.Start(info);
        Close();
    }

    // ---------- Saving ----------

    private void SetDirty(bool dirty)
    {
        DirtyBar.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
        PageHost.Margin = new Thickness(0, 0, 0, dirty ? 64 : 0); // so the bar doesn't cover the last row
        _dirty = dirty;
    }

    private bool SaveAll()
    {
        Settings.Save();
        RobloxGameSettings.Apply(Settings.RobloxSettings);
        SetDirty(false);
        ShowToast(Lang.T("common.saved"));
        return true;
    }

    // ---------- Events ----------

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PageHost is null || !IsLoaded) return;
        if (sender is RadioButton { CommandParameter: string key })
            Navigate(key);
    }

    private void Save_Click(object sender, RoutedEventArgs e) => SaveAll();

    private void SidebarPlay_Click(object sender, RoutedEventArgs e) => Play();

    /// <summary>Throws away the unsaved changes: settings are read again and the pages rebuilt.</summary>
    private void Discard_Click(object sender, RoutedEventArgs e)
    {
        Settings = Settings.Load();
        _pages.Clear();
        PageHost.Content = null;
        SetDirty(false);
        ApplyAppearance();
        Navigate(_current);
    }

    // Windows 11: rounded corners like other windows (ignored on Windows 10)
    private void RoundCorners()
    {
        try
        {
            int round = 2; // DWMWCP_ROUND
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));
        }
        catch
        {
        }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (_dirty)
        {
            var answer = MessageBox.Show(this, Lang.T("common.askSave"), AppInfo.Name,
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) return;
            if (answer == MessageBoxResult.Yes) SaveAll();
        }
        Close();
    }

    /// <summary>Saves and restarts SenStrap (to change the language).</summary>
    public void SaveAndRestart()
    {
        SaveAll();
        Process.Start(new ProcessStartInfo(AppInfo.CurrentExe) { UseShellExecute = false });
        Application.Current.Shutdown();
    }
}
