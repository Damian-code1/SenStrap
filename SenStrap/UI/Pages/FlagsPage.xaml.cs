using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SenStrap.Core;
using static SenStrap.Core.FastFlagCatalog;

namespace SenStrap.UI.Pages;

public partial class FlagsPage : ShellPage
{
    /// <summary>Una fila del editor.</summary>
    public sealed class FlagItem
    {
        private static readonly Brush OkBrush = Frozen(Color.FromRgb(74, 222, 128));
        private static readonly Brush WarnBrush = Frozen(Color.FromRgb(255, 200, 87));
        private readonly Action<FlagItem> _changed;
        private string _value;

        public FlagItem(string name, string value, Action<FlagItem> changed)
        {
            Name = name;
            _value = value;
            _changed = changed;
        }

        public string Name { get; }

        public string Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                _changed(this);
            }
        }

        public bool Allowed => Allowlist.Contains(Name);
        public string Badge => Allowed ? "" : "";
        public Brush BadgeBrush => Allowed ? OkBrush : WarnBrush;
        public string? BadgeTip => Allowed ? null : Lang.T("flags.notAllowed");

        private static Brush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }

    private readonly ObservableCollection<FlagItem> _items = new();
    private readonly ICollectionView _view;
    private bool _loading;

    private Dictionary<string, JsonElement> Flags => Shell.Settings.FastFlags;

    public FlagsPage(IShell shell) : base(shell)
    {
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(_items);
        _view.Filter = Filter;
        FlagList.ItemsSource = _view;

        QualityCombo.Items.Add(new ComboBoxItem { Content = Lang.T("common.off"), Tag = "" });
        for (int level = 1; level <= 21; level++)
            QualityCombo.Items.Add(new ComboBoxItem { Content = Lang.F("flags.levelN", level), Tag = level.ToString() });

        TextureCombo.Items.Add(new ComboBoxItem { Content = Lang.T("common.default"), Tag = "" });
        string[] textures = ["flags.texLowest", "flags.texLow", "flags.texMedium", "flags.texHigh"];
        for (int i = 0; i < textures.Length; i++)
            TextureCombo.Items.Add(new ComboBoxItem { Content = Lang.T(textures[i]), Tag = i.ToString() });

    }

    public override void OnShown()
    {
        _loading = true;
        ManageToggle.IsChecked = Shell.Settings.ManageFastFlags;
        Body.Opacity = Shell.Settings.ManageFastFlags ? 1 : 0.5;
        LoadPresets();
        RefreshEditor();
        RefreshProfiles();
        _loading = false;
        ShowTab(EditorTab.IsChecked == true);
    }

    // ===================== Pestañas =====================

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (PresetsView is null || !IsLoaded) return;
        ShowTab(EditorTab.IsChecked == true);
    }

    /// <summary>Muestra los ajustes rápidos o el editor (también lo usa la herramienta de capturas).</summary>
    public void ShowTab(bool editor)
    {
        if (editor != (EditorTab.IsChecked == true))
        {
            (editor ? EditorTab : PresetsTab).IsChecked = true;
            return; // Tab_Checked vuelve a llamar aquí
        }
        PresetsView.Visibility = editor ? Visibility.Collapsed : Visibility.Visible;
        EditorView.Visibility = editor ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Manage_Click(object sender, RoutedEventArgs e)
    {
        Shell.Settings.ManageFastFlags = ManageToggle.IsChecked == true;
        Body.Opacity = Shell.Settings.ManageFastFlags ? 1 : 0.5;
        Shell.MarkDirty();
    }

    // ===================== Ajustes rápidos =====================

    private static bool IsTrue(string? value) => string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);

    private static void SelectByTag(ComboBox combo, string tag) =>
        combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == tag);

    private static string SelectedTag(ComboBox combo) => combo.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : "";

    private void LoadPresets()
    {
        bool wasLoading = _loading;
        _loading = true;
        var f = Flags;
        SelectByTag(MsaaCombo, Get(f, Msaa) ?? "");
        VoxelToggle.IsChecked = IsTrue(Get(f, PauseVoxelizer));
        GraySkyToggle.IsChecked = IsTrue(Get(f, GraySky));
        GrassToggle.IsChecked = Get(f, GrassMax) == "0";

        bool mesh = Get(f, Mesh) is not null;
        MeshToggle.IsChecked = mesh;
        MeshPanel.Visibility = mesh ? Visibility.Visible : Visibility.Collapsed;
        if (mesh && int.TryParse(Get(f, Mesh), out int distance))
            MeshSlider.Value = Math.Clamp(distance, 0, 250);
        MeshValue.Text = ((int)MeshSlider.Value).ToString();

        DpiToggle.IsChecked = IsTrue(Get(f, DisableDpiScale));
        SelectByTag(RendererCombo, IsTrue(Get(f, PreferVulkan)) ? "vulkan" : IsTrue(Get(f, PreferOpenGL)) ? "opengl" : "d3d11");
        AltEnterToggle.IsChecked = string.Equals(Get(f, AltEnter), "False", StringComparison.OrdinalIgnoreCase);
        SelectByTag(QualityCombo, Get(f, QualityLevel) ?? "");
        SelectByTag(TextureCombo, IsTrue(Get(f, TextureEnabled)) ? Get(f, TextureLevel) ?? "" : "");
        _loading = wasLoading;
    }

    /// <summary>Solo cambia los flags del control que se ha tocado (los demás se respetan).</summary>
    private void Preset_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var f = Flags;

        if (sender == MsaaCombo)
        {
            Set(f, Msaa, NullIfEmpty(SelectedTag(MsaaCombo)));
        }
        else if (sender == VoxelToggle)
        {
            Set(f, PauseVoxelizer, VoxelToggle.IsChecked == true ? "True" : null);
        }
        else if (sender == GraySkyToggle)
        {
            Set(f, GraySky, GraySkyToggle.IsChecked == true ? "True" : null);
        }
        else if (sender == GrassToggle)
        {
            string? zero = GrassToggle.IsChecked == true ? "0" : null;
            Set(f, GrassMin, zero);
            Set(f, GrassMax, zero);
            Set(f, GrassMotion, zero);
        }
        else if (sender == MeshToggle)
        {
            bool on = MeshToggle.IsChecked == true;
            MeshPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            ApplyMesh();
        }
        else if (sender == DpiToggle)
        {
            Set(f, DisableDpiScale, DpiToggle.IsChecked == true ? "True" : null);
        }
        else if (sender == RendererCombo)
        {
            string renderer = SelectedTag(RendererCombo);
            Set(f, PreferVulkan, renderer == "vulkan" ? "True" : null);
            Set(f, PreferOpenGL, renderer == "opengl" ? "True" : null);
        }
        else if (sender == AltEnterToggle)
        {
            Set(f, AltEnter, AltEnterToggle.IsChecked == true ? "False" : null);
        }
        else if (sender == QualityCombo)
        {
            Set(f, QualityLevel, NullIfEmpty(SelectedTag(QualityCombo)));
        }
        else if (sender == TextureCombo)
        {
            string level = SelectedTag(TextureCombo);
            Set(f, TextureEnabled, level.Length > 0 ? "True" : null);
            Set(f, TextureLevel, NullIfEmpty(level));
        }

        RefreshEditor();
        Shell.MarkDirty();
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private void ApplyMesh()
    {
        var f = Flags;
        if (MeshToggle.IsChecked != true)
        {
            foreach (var name in new[] { Mesh, MeshL12, MeshL23, MeshL34 }) f.Remove(name);
            return;
        }
        int d = (int)MeshSlider.Value;
        Set(f, Mesh, d.ToString());
        Set(f, MeshL12, (d * 3 / 4).ToString());
        Set(f, MeshL23, (d / 2).ToString());
        Set(f, MeshL34, (d / 4).ToString());
    }

    /// <summary>One-click sets of the allowlisted flags (the only ones Roblox reads).</summary>
    private void ApplyPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string preset }) return;
        var f = Flags;
        foreach (var name in PresetFlags) f.Remove(name);
        switch (preset)
        {
            case "performance":
                Set(f, Msaa, "1");
                Set(f, PauseVoxelizer, "True");
                Set(f, GrassMin, "0");
                Set(f, GrassMax, "0");
                Set(f, GrassMotion, "0");
                Set(f, Mesh, "60");
                Set(f, MeshL12, "45");
                Set(f, MeshL23, "30");
                Set(f, MeshL34, "15");
                Set(f, QualityLevel, "1");
                Set(f, TextureEnabled, "True");
                Set(f, TextureLevel, "0");
                Set(f, GraySky, "True");
                break;
            case "balanced":
                Set(f, PauseVoxelizer, "True");
                Set(f, Mesh, "150");
                Set(f, MeshL12, "112");
                Set(f, MeshL23, "75");
                Set(f, MeshL34, "37");
                Set(f, TextureEnabled, "True");
                Set(f, TextureLevel, "2");
                break;
            case "quality":
                Set(f, Msaa, "4");
                Set(f, Mesh, "250");
                Set(f, MeshL12, "187");
                Set(f, MeshL23, "125");
                Set(f, MeshL34, "62");
                Set(f, TextureEnabled, "True");
                Set(f, TextureLevel, "3");
                break;
        }
        LoadPresets();
        RefreshEditor();
        Shell.MarkDirty();
        Shell.ShowToast(Lang.T("flags.presetApplied"));
    }

    private void MeshSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MeshValue is null) return;
        MeshValue.Text = ((int)MeshSlider.Value).ToString();
        if (_loading) return;
        ApplyMesh();
        RefreshEditor();
        Shell.MarkDirty();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm(Lang.T("flags.confirmDeleteAll"))) return;
        Flags.Clear();
        AfterBulkChange();
        Shell.ShowToast(Lang.T("flags.resetDone"));
    }

    // ===================== Editor =====================

    private static string ValueText(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.GetRawText();

    private void RefreshEditor()
    {
        _items.Clear();
        foreach (var (name, value) in Flags.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            _items.Add(new FlagItem(name, ValueText(value), OnItemChanged));
        _view.Refresh();
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        TotalText.Text = Lang.F("flags.total", Flags.Count);
        EmptyText.Visibility = _view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnItemChanged(FlagItem item)
    {
        Set(Flags, item.Name, item.Value);
        if (PresetFlags.Contains(item.Name)) LoadPresets();
        Shell.MarkDirty();
    }

    private bool Filter(object obj)
    {
        var item = (FlagItem)obj;
        if (ShowPresetsToggle?.IsChecked == false && PresetFlags.Contains(item.Name)) return false;
        string search = SearchBox?.Text.Trim() ?? "";
        return search.Length == 0 || item.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        _view?.Refresh();
        if (TotalText is not null) UpdateCounts();
    }

    private void ShowPresets_Click(object sender, RoutedEventArgs e)
    {
        _view.Refresh();
        UpdateCounts();
    }

    private void AfterBulkChange()
    {
        LoadPresets();
        RefreshEditor();
        Shell.MarkDirty();
    }

    private void ShowAdd_Click(object sender, RoutedEventArgs e)
    {
        AddPanel.Visibility = Visibility.Visible;
        NewName.Focus();
    }

    private void CancelAdd_Click(object sender, RoutedEventArgs e)
    {
        AddPanel.Visibility = Visibility.Collapsed;
        NewName.Clear();
        NewValue.Clear();
    }

    private void AddInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Add_Click(sender, e);
        else if (e.Key == Key.Escape) CancelAdd_Click(sender, e);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        string name = NewName.Text.Trim();
        if (name.Length == 0)
        {
            Shell.ShowToast(Lang.T("flags.needName"), ok: false);
            NewName.Focus();
            return;
        }
        string value = NewValue.Text.Trim();
        Set(Flags, name, value.Length == 0 ? "True" : value);
        NewName.Clear();
        NewValue.Clear();
        AddPanel.Visibility = Visibility.Collapsed;
        AfterBulkChange();
        Shell.ShowToast(Lang.T("flags.added"));
    }

    private void DeleteFlag_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: FlagItem item }) return;
        Flags.Remove(item.Name);
        _items.Remove(item);
        UpdateCounts();
        if (PresetFlags.Contains(item.Name)) LoadPresets();
        Shell.MarkDirty();
    }

    private void DeleteAll_Click(object sender, RoutedEventArgs e) => Reset_Click(sender, e);

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(JsonSerializer.Serialize(Flags, JsonStore.Options));
        Shell.ShowToast(Lang.T("flags.copied"));
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        string? json = Clipboard.ContainsText() ? Clipboard.GetText() : null;
        var imported = TryParseFlags(json);
        if (imported is null)
        {
            var dialog = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
            if (dialog.ShowDialog() == true) imported = TryParseFlags(File.ReadAllText(dialog.FileName));
        }
        if (imported is null || imported.Count == 0)
        {
            Shell.ShowToast(Lang.T("flags.noJson"), ok: false);
            return;
        }
        foreach (var (name, value) in imported) Flags[name] = value;
        AfterBulkChange();
        Shell.ShowToast(Lang.F("flags.imported", imported.Count));
    }

    private static Dictionary<string, JsonElement>? TryParseFlags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "SenStrap-FastFlags.json" };
        if (dialog.ShowDialog() != true) return;
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(Flags, JsonStore.Options));
        Shell.ShowToast(Lang.T("flags.exported"));
    }

    // ===================== Perfiles =====================

    private void RefreshProfiles()
    {
        ProfileCombo.ItemsSource = Shell.Settings.FlagProfiles.Keys.OrderBy(k => k).ToList();
        if (ProfileCombo.Items.Count > 0 && ProfileCombo.SelectedIndex < 0) ProfileCombo.SelectedIndex = 0;
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        string name = ProfileName.Text.Trim();
        if (name.Length == 0 && ProfileCombo.SelectedItem is string selected) name = selected;
        if (name.Length == 0)
        {
            ProfileName.Focus();
            return;
        }
        Shell.Settings.FlagProfiles[name] = new Dictionary<string, JsonElement>(Flags);
        ProfileName.Clear();
        RefreshProfiles();
        ProfileCombo.SelectedItem = name;
        Shell.MarkDirty();
        Shell.ShowToast(Lang.T("flags.profileSaved"));
    }

    private void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileCombo.SelectedItem is not string name || !Shell.Settings.FlagProfiles.TryGetValue(name, out var profile)) return;
        Flags.Clear();
        foreach (var (flag, value) in profile) Flags[flag] = value;
        AfterBulkChange();
        Shell.ShowToast(Lang.T("flags.profileLoaded"));
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileCombo.SelectedItem is not string name) return;
        Shell.Settings.FlagProfiles.Remove(name);
        RefreshProfiles();
        Shell.MarkDirty();
    }

    private bool Confirm(string text) =>
        MessageBox.Show(Window.GetWindow(this)!, text, AppInfo.Name, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
