using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SenStrap.Core;
using SenStrap.UI.Pages;

namespace SenStrap.UI;

public partial class MainWindow : Window, IShell
{
    private readonly Dictionary<string, ShellPage> _pages = new();
    private bool _dirty;

    public Settings Settings { get; } = Settings.Load();

    public MainWindow()
    {
        InitializeComponent();
        VersionLabel.Text = $"SenStrap v{AppInfo.Version}";
        ApplyAppearance();

        StateChanged += (_, _) =>
        {
            // Sin esto, al maximizar una ventana sin marco se sale un poco de la pantalla
            RootGrid.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "" : "";
        };

        Loaded += (_, _) =>
        {
            Anim.PopIn(RootGrid, 0.97, 380);
            Anim.SpinBorder(ContentPanel, 10);
            Anim.Cascade(NavPanel, 35, 10);
            Navigate("Home");
        };
    }

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
                "Flags" => new FlagsPage(this),
                "Mods" => new ModsPage(this),
                "Appearance" => new AppearancePage(this),
                "Settings" => new SettingsPage(this),
                _ => new HomePage(this),
            };
            _pages[key] = page;
        }
        if (ReferenceEquals(PageHost.Content, page)) return;

        PageHost.Content = page;
        foreach (var item in NavPanel.Children.OfType<RadioButton>())
        {
            if ((string)item.CommandParameter == key)
            {
                if (item.IsChecked != true) item.IsChecked = true;
                PageCrumb.Text = $"›  {item.Content}";
            }
        }
        page.OnShown();
    }

    public void ApplyAppearance()
    {
        Anim.Enabled = Settings.Animations;
        Backdrop.ParticleEffect = Settings.Effect;
        Backdrop.IsAnimated = Settings.Animations;
        Backdrop.Interactive = Settings.InteractiveParticles;
    }

    public void MarkDirty() => SetDirty(true);

    public void ShowToast(string message, bool ok = true)
    {
        ToastText.Text = message;
        ToastIcon.Text = ok ? "" : "";
        ToastIcon.Foreground = (Brush)FindResource(ok ? "OkBrush" : "ErrorBrush");

        var fade = new DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2400))));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2800))));
        Toast.BeginAnimation(OpacityProperty, fade);

        var move = (TranslateTransform)Toast.RenderTransform;
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 },
        });
    }

    public void Play() => Launch(null);

    public void Rejoin(GameEntry game) => Launch(game.RejoinUri);

    /// <summary>Abre SenStrap en modo "jugar" (con un enlace o sin él) y cierra esta ventana.</summary>
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

    // ---------- Guardar ----------

    private void SetDirty(bool dirty)
    {
        if (dirty && !_dirty)
        {
            DirtyHint.Visibility = Visibility.Visible;
            Anim.FadeIn(DirtyHint, 0, 250);
            Anim.Pulse(DirtyDot, 0.8, 1.3, 0.9);
        }
        else if (!dirty)
        {
            DirtyHint.Visibility = Visibility.Collapsed;
        }
        _dirty = dirty;
    }

    private bool SaveAll()
    {
        Settings.Save();
        SetDirty(false);
        ShowToast(Lang.T("common.saved"));
        return true;
    }

    // ---------- Eventos ----------

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PageHost is null || !IsLoaded) return;
        if (sender is RadioButton { CommandParameter: string key })
            Navigate(key);
    }

    private void Save_Click(object sender, RoutedEventArgs e) => SaveAll();

    private void SaveAndPlay_Click(object sender, RoutedEventArgs e)
    {
        if (SaveAll()) Play();
    }

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

    /// <summary>Guarda y reinicia SenStrap (para cambiar de idioma).</summary>
    public void SaveAndRestart()
    {
        SaveAll();
        Process.Start(new ProcessStartInfo(AppInfo.CurrentExe) { UseShellExecute = false });
        Application.Current.Shutdown();
    }
}
