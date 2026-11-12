using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SenStrap.Core;

namespace SenStrap.UI.Pages;

public partial class ModsPage : ShellPage
{
    public ModsPage(IShell shell) : base(shell)
    {
        InitializeComponent();
    }

    public override void OnShown()
    {
        Refresh();
    }

    private static string[] PathsFor(string kind) => kind switch
    {
        "death" => [Mods.DeathSound],
        "shift" => [Mods.ShiftLock],
        _ => Mods.Cursor,
    };

    private void Refresh()
    {
        Show(DeathRow, DeathReset, "death", "mods.deathDesc");
        Show(CursorRow, CursorReset, "cursor", "mods.cursorDesc");
        Show(ShiftRow, ShiftReset, "shift", "mods.shiftlockDesc");

        RefreshAvatar();

        var list = Mods.List().Concat(Mods.Removals().Select(r => Lang.F("mods.removedPath", r))).ToList();
        ModList.ItemsSource = list;
        NoMods.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        void Show(Controls.SettingRow row, Button reset, string kind, string descKey)
        {
            bool custom = PathsFor(kind).Any(Mods.Has);
            row.Description = Lang.T(custom ? "mods.customActive" : descKey);
            reset.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // ===================== Avatar Editor =====================

    /// <summary>Roblox's current install folder (to export the original meshes and check what exists).</summary>
    private static string? InstallDir()
    {
        var dir = AppState.Load().InstallDir;
        if (string.IsNullOrEmpty(dir)) return null;
        string path = Path.Combine(AppInfo.VersionsDir, dir);
        return Directory.Exists(path) ? path : null;
    }

    private void RefreshAvatar()
    {
        bool headless = Mods.IsRemoved(Mods.HeadsFolder);
        HeadlessToggle.IsChecked = headless;
        string desc = Lang.T("mods.headlessDesc");
        if (!headless && InstallDir() is { } install && !Directory.Exists(Path.Combine(install, Mods.HeadsFolder)))
            desc += " " + Lang.T("mods.headlessMissing");
        HeadlessRow.Description = desc;

        BodyPartsPanel.Children.Clear();
        foreach (var (key, path) in Mods.BodyParts)
        {
            bool custom = Mods.Has(path);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            if (custom)
            {
                var reset = new Button { Style = (Style)FindResource("GhostButton"), Content = Lang.T("common.remove"), Margin = new Thickness(0, 0, 6, 0) };
                reset.Click += (_, _) => { Mods.Remove(path); Refresh(); Shell.ShowToast(Lang.T("mods.removed")); };
                buttons.Children.Add(reset);
            }
            var export = new Button { Style = (Style)FindResource("GhostButton"), Content = Lang.T("mods.export"), Margin = new Thickness(0, 0, 6, 0) };
            export.Click += (_, _) => ExportOriginal(key, path);
            buttons.Children.Add(export);
            var pick = new Button { Style = (Style)FindResource("SecondaryButton") };
            pick.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = "", Style = (Style)FindResource("BtnIcon") },
                    new TextBlock { Text = Lang.T("mods.pickMesh") },
                },
            };
            pick.Click += (_, _) => PickMesh(path);
            buttons.Children.Add(pick);

            BodyPartsPanel.Children.Add(new Controls.SettingRow
            {
                Icon = "",
                Title = Lang.T($"mods.part.{key}"),
                Description = Lang.T(custom ? "mods.customActive" : "mods.partDefault"),
                Content = buttons,
            });
        }
    }

    private void Headless_Click(object sender, RoutedEventArgs e)
    {
        Mods.SetRemoved(Mods.HeadsFolder, HeadlessToggle.IsChecked == true);
        Refresh();
        Shell.ShowToast(Lang.T("mods.applied"));
    }

    private void PickMesh(string path)
    {
        var dialog = new OpenFileDialog { Filter = "Roblox mesh (*.mesh)|*.mesh", CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        Mods.Set(path, dialog.FileName);
        Refresh();
        Shell.ShowToast(Lang.T("mods.applied"));
    }

    /// <summary>Saves Roblox's original mesh somewhere, as a starting point to edit.</summary>
    private void ExportOriginal(string key, string path)
    {
        string? source = InstallDir() is { } install ? Path.Combine(install, path) : null;
        if (source is null || !File.Exists(source))
        {
            Shell.ShowToast(Lang.T("mods.noRoblox"), ok: false);
            return;
        }
        var dialog = new SaveFileDialog { FileName = Path.GetFileName(path), Filter = "Roblox mesh (*.mesh)|*.mesh" };
        if (dialog.ShowDialog() != true) return;
        File.Copy(source, dialog.FileName, overwrite: true);
        Shell.ShowToast(Lang.T("mods.exported"));
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Mods.EnsureFolder();
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppInfo.ModsDir}\"") { UseShellExecute = true });
    }

    private void RemoveAll_Click(object sender, RoutedEventArgs e)
    {
        if (Mods.List().Count == 0)
        {
            Shell.ShowToast(Lang.T("mods.noneToRemove"));
            return;
        }
        var answer = MessageBox.Show(Window.GetWindow(this)!, Lang.T("mods.confirmRemoveAll"), AppInfo.Name,
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        Mods.RemoveAll();
        Refresh();
        Shell.ShowToast(Lang.T("mods.allRemoved"));
    }

    private void Pick_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string kind }) return;
        var dialog = new OpenFileDialog
        {
            Filter = kind == "death" ? "OGG (*.ogg)|*.ogg" : "PNG (*.png)|*.png",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != true) return;
        foreach (var path in PathsFor(kind))
            Mods.Set(path, dialog.FileName);
        Refresh();
        Shell.ShowToast(Lang.T("mods.applied"));
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string kind }) return;
        foreach (var path in PathsFor(kind))
            Mods.Remove(path);
        Refresh();
        Shell.ShowToast(Lang.T("mods.removed"));
    }
}
