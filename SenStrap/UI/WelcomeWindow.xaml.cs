using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SenStrap.Core;

namespace SenStrap.UI;

/// <summary>Primera vez que se abre SenStrap: elegir idioma.</summary>
public partial class WelcomeWindow : Window
{
    private string _selected;

    /// <summary>true si el usuario pulsó Continuar.</summary>
    public bool Accepted { get; private set; }

    public WelcomeWindow()
    {
        InitializeComponent();
        _selected = Lang.Current;

        Snow.IsSnowing = Settings.Load().Snow;

        foreach (var language in Lang.All)
        {
            var card = new RadioButton
            {
                Style = (Style)FindResource("LangCard"),
                GroupName = "Languages",
                Tag = language.Code,
                IsChecked = language.Code == _selected,
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = language.NativeName, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") },
                        new TextBlock { Text = language.EnglishName, FontSize = 12, Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(0, 2, 0, 0) },
                    },
                },
            };
            card.Checked += (_, _) => Select(language.Code);
            LanguageGrid.Children.Add(card);
        }
        UpdateTexts();

        Clipped.SizeChanged += (_, _) =>
            Clipped.Clip = new RectangleGeometry(new Rect(0, 0, Clipped.ActualWidth, Clipped.ActualHeight), 9, 9);
    }

    private void Select(string code)
    {
        _selected = code;
        Lang.Load(code);
        UpdateTexts();
    }

    private void UpdateTexts()
    {
        TitleText.Text = Lang.T("welcome.title");
        SubtitleText.Text = Lang.T("welcome.subtitle");
        ContinueText.Text = Lang.T("welcome.continue");
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        var settings = Settings.Load();
        settings.Language = _selected;
        settings.Save();
        Accepted = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource is not TextBlock)
            DragMove();
    }
}
