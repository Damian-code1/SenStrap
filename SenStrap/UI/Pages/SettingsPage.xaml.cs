using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SenStrap.Core;

namespace SenStrap.UI.Pages;

public partial class SettingsPage : ShellPage
{
    private bool _loading;

    public SettingsPage(IShell shell) : base(shell)
    {
        InitializeComponent();
        FolderRow.Description = AppInfo.BaseDir;
        AboutTitle.Text = $"SenStrap {AppInfo.Version}";
        foreach (var language in Lang.All)
        {
            string label = language.NativeName == language.EnglishName
                ? language.NativeName
                : $"{language.NativeName}  ·  {language.EnglishName}";
            LanguageCombo.Items.Add(new ComboBoxItem { Content = label, Tag = language.Code });
        }
    }

    public override void OnShown()
    {
        _loading = true;
        string current = string.IsNullOrEmpty(Shell.Settings.Language) ? Lang.Current : Shell.Settings.Language;
        LanguageCombo.SelectedItem = LanguageCombo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == current);
        AutoUpdateToggle.IsChecked = Shell.Settings.AutoUpdate;
        _loading = false;
    }

    private Window Owner => Window.GetWindow(this)!;

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageCombo.SelectedItem is not ComboBoxItem { Tag: string code }) return;
        Shell.Settings.Language = code;
        Shell.MarkDirty();
        bool changed = code != Lang.Current;
        RestartButton.Visibility = changed ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"SenStrap-backup-{DateTime.Now:yyyy-MM-dd}.zip",
            Filter = "ZIP (*.zip)|*.zip",
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            Shell.Settings.Save();
            Backup.Export(dialog.FileName);
            Shell.ShowToast(Lang.T("settings.exported"));
        }
        catch (Exception ex)
        {
            Logger.Error("Backup failed", ex);
            Shell.ShowToast(ex.Message, ok: false);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "ZIP (*.zip)|*.zip", CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        if (MessageBox.Show(Owner, Lang.T("settings.importConfirm"), AppInfo.Name, MessageBoxButton.OKCancel,
                MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;
        try
        {
            if (!Backup.Import(dialog.FileName))
            {
                Shell.ShowToast(Lang.T("settings.importInvalid"), ok: false);
                return;
            }
            // Restart without saving, so the restored settings are the ones that load
            Process.Start(new ProcessStartInfo(AppInfo.CurrentExe) { UseShellExecute = false });
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Logger.Error("Restore failed", ex);
            Shell.ShowToast(ex.Message, ok: false);
        }
    }

    private void AutoUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Shell.Settings.AutoUpdate = AutoUpdateToggle.IsChecked == true;
        Shell.MarkDirty();
    }

    private void Restart_Click(object sender, RoutedEventArgs e) => Shell.SaveAndRestart();

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(AppInfo.BaseDir);

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => OpenFolder(AppInfo.LogsDir);

    private void Reinstall_Click(object sender, RoutedEventArgs e)
    {
        if (RobloxInstaller.IsRobloxRunning())
        {
            Shell.ShowToast(Lang.T("settings.closeRoblox"), ok: false);
            return;
        }

        var state = AppState.Load();
        state.VersionGuid = null;
        state.InstallDir = null;
        state.AppliedModsHash = null;
        state.Save();
        RobloxInstaller.RemoveAllVersions();
        Shell.ShowToast(Lang.T("settings.reinstalled"));
    }

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(Owner, Lang.T("uninstall.confirm"), AppInfo.Name, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            SelfInstaller.Uninstall();
            MessageBox.Show(Owner, Lang.T("uninstall.done"), AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
            SelfInstaller.ScheduleFolderDeletion();
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Logger.Error("Error al desinstalar", ex);
            MessageBox.Show(Owner, $"{Lang.T("uninstall.error")}\n\n{ex.Message}", AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
