using System.Windows.Markup;
using SenStrap.Core;

namespace SenStrap.UI;

/// <summary>
/// Texto traducido en XAML: Text="{ui:Loc home.play}".
/// El idioma se elige al arrancar; para cambiarlo hay que reiniciar SenStrap.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public class Loc : MarkupExtension
{
    public Loc() { }

    public Loc(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => Lang.T(Key);
}
