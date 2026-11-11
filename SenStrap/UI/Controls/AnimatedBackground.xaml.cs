using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using SenStrap.Core;

namespace SenStrap.UI.Controls;

/// <summary>Fondo animado: degradado azul noche + aurora de colores + partículas.</summary>
public partial class AnimatedBackground : UserControl
{
    public static readonly DependencyProperty ParticleEffectProperty = DependencyProperty.Register(
        nameof(ParticleEffect), typeof(BackgroundEffect), typeof(AnimatedBackground),
        new PropertyMetadata(BackgroundEffect.Nieve, (d, e) => ((AnimatedBackground)d).Particles.Kind = (BackgroundEffect)e.NewValue));

    public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
        nameof(IsAnimated), typeof(bool), typeof(AnimatedBackground),
        new PropertyMetadata(true, (d, _) => ((AnimatedBackground)d).UpdateAnimation()));

    public BackgroundEffect ParticleEffect
    {
        get => (BackgroundEffect)GetValue(ParticleEffectProperty);
        set => SetValue(ParticleEffectProperty, value);
    }

    public bool IsAnimated
    {
        get => (bool)GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    /// <summary>Las partículas se apartan del ratón.</summary>
    public bool Interactive
    {
        get => Particles.Interactive;
        set => Particles.Interactive = value;
    }

    public AnimatedBackground()
    {
        InitializeComponent();
        SizeChanged += (_, _) => LayoutBlobs();
        Loaded += (_, _) => UpdateAnimation();
        Unloaded += (_, _) => StopBlobs();
    }

    private void LayoutBlobs()
    {
        double w = ActualWidth, h = ActualHeight;
        double size = Math.Max(w, h);
        Place(Blob1, size * 0.9, -size * 0.25, -size * 0.45);
        Place(Blob2, size * 0.85, w - size * 0.5, h - size * 0.45);
        Place(Blob3, size * 0.7, w * 0.15, h * 0.35);

        static void Place(Ellipse blob, double diameter, double left, double top)
        {
            blob.Width = blob.Height = diameter;
            Canvas.SetLeft(blob, left);
            Canvas.SetTop(blob, top);
        }
    }

    private void UpdateAnimation()
    {
        Particles.IsAnimated = IsAnimated;
        if (IsAnimated && IsLoaded) StartBlobs();
        else StopBlobs();
    }

    private void StartBlobs()
    {
        Drift(Blob1, 90, 60, 14);
        Drift(Blob2, -80, -70, 17);
        Drift(Blob3, 70, -50, 21);

        static void Drift(Ellipse blob, double dx, double dy, double seconds)
        {
            var move = (TranslateTransform)blob.RenderTransform;
            var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
            move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, dx, TimeSpan.FromSeconds(seconds))
            {
                AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease,
            });
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, dy, TimeSpan.FromSeconds(seconds * 1.3))
            {
                AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease,
            });
        }
    }

    private void StopBlobs()
    {
        foreach (var blob in new[] { Blob1, Blob2, Blob3 })
        {
            var move = (TranslateTransform)blob.RenderTransform;
            move.BeginAnimation(TranslateTransform.XProperty, null);
            move.BeginAnimation(TranslateTransform.YProperty, null);
        }
    }
}
