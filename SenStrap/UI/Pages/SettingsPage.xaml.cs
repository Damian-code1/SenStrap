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
        _loading = false;
        Anim.Cascade(Root);
        Anim.SpinBorder(About, 12);
    }

    private Window Owner => Window.GetWindow(this)!;

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageCombo.SelectedItem is not ComboBoxItem { Tag: string code }) return;
        Shell.Settings.Language = code;
        Shell.MarkDirty();
        bool changed = code != Lang.Current;
        RestartButton.Visibility = changed ? Visibility.Visible : Visibility.Collapsed;
        if (changed) Anim.FadeIn(RestartButton, 0, 250);
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
