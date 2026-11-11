using System.Windows.Controls;
using SenStrap.Core;

namespace SenStrap.UI;

/// <summary>Lo que las páginas pueden pedirle a la ventana principal.</summary>
public interface IShell
{
    /// <summary>Copia de trabajo de los ajustes (se guarda con el botón Guardar).</summary>
    Settings Settings { get; }

    void Navigate(string page);
    void ApplyAppearance();
    void MarkDirty();
    void ShowToast(string message, bool ok = true);
    void Play();
    void Rejoin(GameEntry game);
    void SaveAndRestart();
}

/// <summary>Base de todas las páginas de la ventana principal.</summary>
public class ShellPage : UserControl
{
    protected IShell Shell { get; }

    public ShellPage() : this(null!) { }

    protected ShellPage(IShell shell)
    {
        Shell = shell;
    }

    /// <summary>Se llama cada vez que la página se muestra.</summary>
    public virtual void OnShown() { }
}
