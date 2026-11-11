using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using SenStrap.Core;

namespace SenStrap.UI.Pages;

/// <summary>Who made SenStrap, with the developer's Discord profile.</summary>
public partial class CreditsPage : ShellPage
{
    private bool _refreshing;

    public CreditsPage(IShell shell) : base(shell)
    {
        InitializeComponent();
        VersionRow.Title = $"SenStrap {AppInfo.Version}";
        ShowProfile(OwnerProfile.Cached());
    }

    public override async void OnShown()
    {
        if (_refreshing) return;
        _refreshing = true;
        var fresh = await OwnerProfile.RefreshAsync();
        if (fresh is not null) ShowProfile(fresh);
        _refreshing = false;
    }

    private void ShowProfile(OwnerProfile.Info? info)
    {
        if (info is not null)
        {
            DisplayNameText.Text = string.IsNullOrWhiteSpace(info.DisplayName) ? info.Username : info.DisplayName;
            UsernameText.Text = info.Username;
        }
        if (LoadImage(OwnerProfile.AvatarFile) is { } avatar) AvatarBrush.ImageSource = avatar;
        if (LoadImage(OwnerProfile.BannerFile) is { } banner) BannerBrush.ImageSource = banner;
    }

    /// <summary>Loads a picture without keeping the file open, so it can be replaced later.</summary>
    private static BitmapImage? LoadImage(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Warn($"Couldn't open {url}: {ex.Message}");
        }
    }

    private void Discord_Click(object sender, RoutedEventArgs e) => Open(OwnerProfile.ProfileUrl);

    private void CopyUsername_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(UsernameText.Text);
            Shell.ShowToast(Lang.T("credits.copied"));
        }
        catch (Exception ex)
        {
            Logger.Warn($"Couldn't copy to the clipboard: {ex.Message}");
        }
    }

    private void GitHub_Click(object sender, RoutedEventArgs e) => Open(OwnerProfile.RepoUrl);

    private void Issues_Click(object sender, RoutedEventArgs e) => Open(OwnerProfile.RepoUrl + "/issues");

    private void License_Click(object sender, RoutedEventArgs e) => Open(OwnerProfile.RepoUrl + "/blob/main/LICENSE");
}
