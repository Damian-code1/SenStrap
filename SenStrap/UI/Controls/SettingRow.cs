using System.Windows;
using System.Windows.Controls;

namespace SenStrap.UI.Controls;

/// <summary>
/// Fila de ajustes: icono + título + descripción a la izquierda y un control (botón, interruptor...) a la derecha.
/// El aspecto está en Theme.xaml.
/// </summary>
public class SettingRow : ContentControl
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(SettingRow), new PropertyMetadata(""));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingRow), new PropertyMetadata(""));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingRow), new PropertyMetadata(""));

    /// <summary>Carácter de la fuente de iconos de Windows (Segoe Fluent Icons / MDL2).</summary>
    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
}
