using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SenStrap.UI;

/// <summary>Animaciones de entrada (aparecer deslizando, en cascada...). Se pueden desactivar en Apariencia.</summary>
public static class Anim
{
    public static bool Enabled { get; set; } = true;

    private static readonly IEasingFunction EaseOut = Frozen(new CubicEase { EasingMode = EasingMode.EaseOut });
    private static readonly IEasingFunction BackOut = Frozen(new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 });

    private static IEasingFunction Frozen(EasingFunctionBase ease)
    {
        ease.Freeze();
        return ease;
    }

    /// <summary>El elemento aparece desde abajo mientras se vuelve opaco.</summary>
    public static void FadeIn(UIElement element, double fromY = 14, int ms = 320, int delayMs = 0)
    {
        if (!Enabled)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 1;
            return;
        }

        var move = new TranslateTransform(0, fromY);
        element.RenderTransform = move;
        element.Opacity = 0;

        var duration = TimeSpan.FromMilliseconds(ms);
        var begin = TimeSpan.FromMilliseconds(delayMs);
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, duration) { BeginTime = begin, EasingFunction = EaseOut });
        move.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(fromY, 0, duration) { BeginTime = begin, EasingFunction = EaseOut });
    }

    /// <summary>Los hijos de un panel aparecen uno detrás de otro.</summary>
    public static void Cascade(Panel panel, int stepMs = 45, double fromY = 18)
    {
        int i = 0;
        foreach (UIElement child in panel.Children)
        {
            if (child.Visibility != Visibility.Visible) continue;
            FadeIn(child, fromY, 380, Math.Min(i * stepMs, 500));
            i++;
        }
    }

    /// <summary>Entrada de una ventana: crece un poco y aparece.</summary>
    public static void PopIn(FrameworkElement element, double fromScale = 0.94, int ms = 420)
    {
        if (!Enabled) return;
        var scale = new ScaleTransform(fromScale, fromScale);
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = scale;
        var duration = TimeSpan.FromMilliseconds(ms);
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(ms * 0.7)) { EasingFunction = EaseOut });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(fromScale, 1, duration) { EasingFunction = BackOut });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(fromScale, 1, duration) { EasingFunction = BackOut });
    }

    /// <summary>Movimiento suave arriba y abajo para siempre (logos flotando).</summary>
    public static void Float(UIElement element, double distance = 6, double seconds = 2.8)
    {
        if (!Enabled) return;
        var move = new TranslateTransform();
        element.RenderTransform = move;
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-distance, distance, TimeSpan.FromSeconds(seconds))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        });
    }

    /// <summary>Hace girar el degradado del borde (el brillo da vueltas alrededor de la tarjeta).</summary>
    public static void SpinBorder(System.Windows.Controls.Border border, double seconds = 8)
    {
        if (!Enabled || border.BorderBrush is not GradientBrush brush) return;
        if (brush.IsFrozen)
        {
            // Los pinceles que vienen de un estilo están congelados: se usa una copia
            brush = brush.Clone();
            border.BorderBrush = brush;
        }
        if (brush.RelativeTransform is not RotateTransform rotate || rotate.IsFrozen)
        {
            rotate = new RotateTransform(0, 0.5, 0.5);
            brush.RelativeTransform = rotate;
        }
        rotate.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(0, 360, TimeSpan.FromSeconds(seconds)) { RepeatBehavior = RepeatBehavior.Forever });
    }

    /// <summary>Latido: crece y encoge con la opacidad (halos de luz).</summary>
    public static void Pulse(FrameworkElement element, double minScale = 0.88, double maxScale = 1.12, double seconds = 2.2)
    {
        if (!Enabled) return;
        var scale = new ScaleTransform(1, 1);
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = scale;
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        var duration = TimeSpan.FromSeconds(seconds);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(minScale, maxScale, duration) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(minScale, maxScale, duration) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.55, 1, duration) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });
    }
}
