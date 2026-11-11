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

        var list = Mods.List();
        ModList.ItemsSource = list;
        NoMods.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        void Show(Controls.SettingRow row, Button reset, string kind, string descKey)
        {
            bool custom = PathsFor(kind).Any(Mods.Has);
            row.Description = Lang.T(custom ? "mods.customActive" : descKey);
            reset.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        }
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
