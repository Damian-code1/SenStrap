using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SenStrap.Core;
using SenStrap.UI.Controls;
using static SenStrap.Core.RobloxGameSettings;

namespace SenStrap.UI.Pages;

/// <summary>Roblox's own in-game settings, edited from SenStrap and re-applied on every launch.</summary>
public partial class GameSettingsPage : ShellPage
{
    private sealed record Text(string Icon, string Name, string NameEs, string Desc, string DescEs);

    private static readonly (string Section, string SectionEs, string[] Names)[] Sections =
    [
        ("Performance", "Rendimiento", ["FramerateCap", "SavedQualityLevel", "Fullscreen", "StartMaximized"]),
        ("Audio", "Audio", ["MasterVolume", "VoiceChatVolume"]),
        ("Controls", "Controles", ["MouseSensitivity", "CameraYInverted"]),
        ("Interface", "Interfaz", ["PerformanceStatsVisible", "ReducedMotion", "VignetteEnabled", "PreferredTransparency",
            "PreferredTextSize", "PlayerNamesEnabled", "ChatVisible", "PlayerListVisible", "ChatTranslationEnabled", "AllTutorialsDisabled"]),
    ];

    private static readonly Dictionary<string, Text> Texts = new()
    {
        ["FramerateCap"] = new("", "Maximum frame rate", "Frame rate máximo",
            "Roblox's own FPS cap (the in-game Maximum Frame Rate). From 30 to 240, the highest Roblox supports.",
            "El límite de FPS de Roblox (el Frame rate máximo del juego). De 30 a 240, lo máximo que admite Roblox."),
        ["SavedQualityLevel"] = new("", "Graphics quality", "Calidad gráfica",
            "Automatic, or a fixed level from 1 (fastest) to 10 (best looking).",
            "Automática, o un nivel fijo de 1 (más rápido) a 10 (más bonito)."),
        ["Fullscreen"] = new("", "Start in fullscreen", "Empezar en pantalla completa",
            "Roblox opens in fullscreen (F11 still switches).", "Roblox se abre en pantalla completa (F11 sigue cambiando)."),
        ["StartMaximized"] = new("", "Start maximized", "Empezar maximizado",
            "When not fullscreen, the window opens maximized.", "Si no está en pantalla completa, la ventana se abre maximizada."),
        ["MasterVolume"] = new("", "Master volume", "Volumen general",
            "Roblox's overall volume.", "El volumen general de Roblox."),
        ["VoiceChatVolume"] = new("", "Voice chat volume", "Volumen del chat de voz",
            "How loud other players' voices are.", "Lo alto que se oyen las voces de otros jugadores."),
        ["MouseSensitivity"] = new("", "Camera sensitivity", "Sensibilidad de la cámara",
            "How fast the camera turns with the mouse.", "Lo rápido que gira la cámara con el ratón."),
        ["CameraYInverted"] = new("", "Invert camera", "Invertir cámara",
            "Moving the mouse up looks down.", "Mover el ratón hacia arriba mira hacia abajo."),
        ["PerformanceStatsVisible"] = new("", "Performance stats", "Estadísticas de rendimiento",
            "Roblox's own CPU, GPU, memory and ping bar at the top of the screen.",
            "La barra de CPU, GPU, memoria y ping de Roblox en la parte de arriba."),
        ["ReducedMotion"] = new("", "Reduced motion", "Movimiento reducido",
            "Fewer interface animations in Roblox's menus.", "Menos animaciones en los menús de Roblox."),
        ["VignetteEnabled"] = new("", "Vignette", "Viñeta",
            "Dark edges around the screen in some games.", "Bordes oscuros alrededor de la pantalla en algunos juegos."),
        ["PreferredTransparency"] = new("", "Menu background opacity", "Opacidad del fondo de los menús",
            "How see-through Roblox's menus are. Lower is more solid.", "Lo transparentes que son los menús de Roblox. Más bajo es más sólido."),
        ["PreferredTextSize"] = new("", "Text size", "Tamaño del texto",
            "Size of the text in Roblox's menus.", "Tamaño del texto en los menús de Roblox."),
        ["PlayerNamesEnabled"] = new("", "Player names", "Nombres de jugadores",
            "Names above other players.", "Los nombres encima de los demás jugadores."),
        ["ChatVisible"] = new("", "Chat window", "Ventana del chat",
            "Shows the chat when a game starts.", "Muestra el chat al empezar un juego."),
        ["PlayerListVisible"] = new("", "Player list", "Lista de jugadores",
            "Shows the player list (Tab) when a game starts.", "Muestra la lista de jugadores (Tab) al empezar un juego."),
        ["ChatTranslationEnabled"] = new("", "Translate chat", "Traducir el chat",
            "Roblox translates messages in other languages.", "Roblox traduce los mensajes en otros idiomas."),
        ["AllTutorialsDisabled"] = new("", "Skip Roblox tips", "Saltar consejos de Roblox",
            "Hides Roblox's tutorial popups.", "Oculta las ventanas de tutorial de Roblox."),
    };

    private static readonly Dictionary<string, string> Defaults = new()
    {
        ["FramerateCap"] = "60", ["SavedQualityLevel"] = "0", ["Fullscreen"] = "false", ["StartMaximized"] = "true",
        ["MasterVolume"] = "1", ["VoiceChatVolume"] = "1", ["MouseSensitivity"] = "0.5", ["CameraYInverted"] = "false",
        ["PerformanceStatsVisible"] = "false", ["ReducedMotion"] = "false", ["VignetteEnabled"] = "true",
        ["PreferredTransparency"] = "1", ["PreferredTextSize"] = "1", ["PlayerNamesEnabled"] = "true", ["ChatVisible"] = "true",
        ["PlayerListVisible"] = "false", ["ChatTranslationEnabled"] = "false", ["AllTutorialsDisabled"] = "false",
    };

    private bool _loading;

    private static bool Spanish => Lang.Current.StartsWith("es", StringComparison.OrdinalIgnoreCase);

    public GameSettingsPage(IShell shell) : base(shell)
    {
        InitializeComponent();
    }

    public override void OnShown() => Build();

    private string Current(string name) =>
        Shell.Settings.RobloxSettings.TryGetValue(name, out var v) ? v
        : _file.TryGetValue(name, out v) ? v
        : Defaults[name];

    private Dictionary<string, string> _file = new();

    private void Build()
    {
        _loading = true;
        _file = Read();
        MissingCard.Visibility = Exists ? Visibility.Collapsed : Visibility.Visible;
        List.Children.Clear();
        bool first = true;
        foreach (var (section, sectionEs, names) in Sections)
        {
            List.Children.Add(new TextBlock
            {
                Text = Spanish ? sectionEs : section,
                Style = (Style)FindResource("SectionTitle"),
                Margin = new Thickness(2, first ? 0 : 18, 0, 8),
            });
            first = false;
            foreach (var name in names)
                List.Children.Add(Row(name));
        }
        _loading = false;
    }

    private SettingRow Row(string name)
    {
        var option = Find(name)!;
        var text = Texts[name];
        var row = new SettingRow
        {
            Icon = text.Icon,
            Title = Spanish ? text.NameEs : text.Name,
            Description = Spanish ? text.DescEs : text.Desc,
            Content = Control(option),
        };
        return row;
    }

    private FrameworkElement Control(Option option)
    {
        string value = Current(option.Name);
        switch (option.Name)
        {
            case "FramerateCap":
            {
                var box = new TextBox { Style = (Style)FindResource("Input"), Width = 90, Text = value, Tag = "30–240" };
                box.LostFocus += (_, _) => Commit(option, box.Text, box);
                box.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Commit(option, box.Text, box); };
                return box;
            }
            case "SavedQualityLevel":
            {
                var combo = new ComboBox { Style = (Style)FindResource("Combo") };
                combo.Items.Add(new ComboBoxItem { Content = Lang.T("rs.automatic"), Tag = "0" });
                for (int i = 1; i <= 10; i++) combo.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = i.ToString() });
                combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(c => (string)c.Tag == value) ?? combo.Items[0];
                combo.SelectionChanged += (_, _) => Commit(option, (string)((ComboBoxItem)combo.SelectedItem).Tag, null);
                return combo;
            }
            case "PreferredTextSize":
            {
                var combo = new ComboBox { Style = (Style)FindResource("Combo") };
                string[] sizes = Spanish ? ["Mediano", "Grande", "Más grande", "El más grande"] : ["Medium", "Large", "Larger", "Largest"];
                for (int i = 0; i < 4; i++) combo.Items.Add(new ComboBoxItem { Content = sizes[i], Tag = (i + 1).ToString() });
                combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(c => (string)c.Tag == value) ?? combo.Items[0];
                combo.SelectionChanged += (_, _) => Commit(option, (string)((ComboBoxItem)combo.SelectedItem).Tag, null);
                return combo;
            }
        }

        if (option.Kind == Kind.Bool)
        {
            var toggle = new CheckBox { Style = (Style)FindResource("Toggle"), IsChecked = value == "true" };
            toggle.Click += (_, _) => Commit(option, toggle.IsChecked == true ? "true" : "false", null);
            return toggle;
        }

        // Sliders for volumes, sensitivity and opacity
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number);
        bool percent = option.Max <= 1;
        var label = new TextBlock { Width = 46, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"), FontSize = 12.5 };
        var slider = new Slider
        {
            Style = (Style)FindResource("SenSlider"),
            Width = 200,
            Minimum = option.Min,
            Maximum = option.Max,
            Value = Math.Clamp(number, option.Min, option.Max),
            VerticalAlignment = VerticalAlignment.Center,
        };
        void Show() => label.Text = percent ? $"{slider.Value:P0}" : slider.Value.ToString("0.0", CultureInfo.CurrentCulture);
        Show();
        slider.ValueChanged += (_, _) =>
        {
            Show();
            Commit(option, slider.Value.ToString(CultureInfo.InvariantCulture), null);
        };
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(slider);
        panel.Children.Add(label);
        return panel;
    }

    private void Commit(Option option, string raw, TextBox? box)
    {
        if (_loading) return;
        if (Normalize(option, raw) is not { } value)
        {
            if (box is not null) box.Text = Current(option.Name);
            return;
        }
        if (box is not null) box.Text = value;
        if (Shell.Settings.RobloxSettings.TryGetValue(option.Name, out var old) && old == value) return;
        Shell.Settings.RobloxSettings[option.Name] = value;
        Shell.MarkDirty();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (Shell.Settings.RobloxSettings.Count == 0) return;
        Shell.Settings.RobloxSettings.Clear();
        Shell.MarkDirty();
        Build();
        Shell.ShowToast(Lang.T("rs.resetDone"));
    }
}
