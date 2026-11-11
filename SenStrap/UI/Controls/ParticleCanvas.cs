using System.Windows;
using System.Windows.Media;
using SenStrap.Core;

namespace SenStrap.UI.Controls;

/// <summary>
/// Dibuja partículas animadas: nieve que cae, brasas que suben o estrellas que parpadean.
/// Se dibuja a mano en cada fotograma (OnRender), así que es ligero incluso con cientos de partículas.
/// </summary>
public sealed class ParticleCanvas : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(BackgroundEffect), typeof(ParticleCanvas),
        new FrameworkPropertyMetadata(BackgroundEffect.Nieve, (d, _) => ((ParticleCanvas)d).Reset()));

    public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
        nameof(IsAnimated), typeof(bool), typeof(ParticleCanvas),
        new FrameworkPropertyMetadata(true, (d, _) => ((ParticleCanvas)d).UpdateSubscription()));

    public static readonly DependencyProperty DensityProperty = DependencyProperty.Register(
        nameof(Density), typeof(double), typeof(ParticleCanvas),
        new FrameworkPropertyMetadata(1.0, (d, _) => ((ParticleCanvas)d).Reset()));

    public BackgroundEffect Kind
    {
        get => (BackgroundEffect)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public bool IsAnimated
    {
        get => (bool)GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    /// <summary>1 = normal. Más alto, más partículas.</summary>
    public double Density
    {
        get => (double)GetValue(DensityProperty);
        set => SetValue(DensityProperty, value);
    }

    public static readonly DependencyProperty InteractiveProperty = DependencyProperty.Register(
        nameof(Interactive), typeof(bool), typeof(ParticleCanvas), new FrameworkPropertyMetadata(true));

    /// <summary>Las partículas se apartan del ratón.</summary>
    public bool Interactive
    {
        get => (bool)GetValue(InteractiveProperty);
        set => SetValue(InteractiveProperty, value);
    }

    private const double RepelRadius = 110;
    private const double RepelForce = 320;

    private struct Particle
    {
        public double X, Y, Radius, Speed, Phase, Sway, Alpha;
        public int Color;
    }

    private const int AlphaLevels = 10;
    private readonly Random _rng = new();
    private Particle[] _particles = [];
    private Brush[][] _brushes = [];
    private bool _subscribed;
    private TimeSpan _lastFrame = TimeSpan.Zero;
    private double _time;

    public ParticleCanvas()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => { Reset(); UpdateSubscription(); };
        Unloaded += (_, _) => Unsubscribe();
        IsVisibleChanged += (_, _) => UpdateSubscription();
        SizeChanged += (_, _) => EnsureCount();
    }

    private void Reset()
    {
        _particles = [];
        _brushes = BuildBrushes(Kind);
        EnsureCount();
        UpdateSubscription();
        InvalidateVisual();
    }

    private void UpdateSubscription()
    {
        bool shouldRun = IsLoaded && IsVisible && IsAnimated && Kind != BackgroundEffect.Ninguno;
        if (shouldRun && !_subscribed)
        {
            _lastFrame = TimeSpan.Zero;
            CompositionTarget.Rendering += OnRendering;
            _subscribed = true;
        }
        else if (!shouldRun)
        {
            Unsubscribe();
        }
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        CompositionTarget.Rendering -= OnRendering;
        _subscribed = false;
    }

    private int TargetCount()
    {
        if (Kind == BackgroundEffect.Ninguno || ActualWidth < 1 || ActualHeight < 1)
            return 0;
        double pixelsPerParticle = Kind switch
        {
            BackgroundEffect.Nieve => 7000,
            BackgroundEffect.Brasas => 6000,
            _ => 6000,
        };
        return (int)Math.Clamp(ActualWidth * ActualHeight / pixelsPerParticle * Density, 15, 320);
    }

    private void EnsureCount()
    {
        int target = TargetCount();
        if (_particles.Length == target) return;

        var list = new List<Particle>(target);
        list.AddRange(_particles.Take(target));
        while (list.Count < target)
            list.Add(Spawn(anywhere: true));
        _particles = list.ToArray();
        InvalidateVisual();
    }

    private Particle Spawn(bool anywhere)
    {
        double w = Math.Max(1, ActualWidth), h = Math.Max(1, ActualHeight);
        double depth = _rng.NextDouble(); // 0 = lejos (pequeña y lenta), 1 = cerca (grande y rápida)
        var p = new Particle
        {
            X = _rng.NextDouble() * w,
            Phase = _rng.NextDouble() * Math.PI * 2,
            Color = _brushes.Length == 0 ? 0 : _rng.Next(_brushes.Length),
        };

        switch (Kind)
        {
            case BackgroundEffect.Nieve:
                p.Radius = 0.9 + depth * depth * 2.5;
                p.Speed = 16 + depth * 60;
                p.Sway = 8 + depth * 24;
                p.Alpha = 0.3 + depth * 0.65;
                p.Y = anywhere ? _rng.NextDouble() * h : -10;
                break;
            case BackgroundEffect.Brasas:
                p.Radius = 1.1 + depth * 2.6;
                p.Speed = 18 + depth * 55;
                p.Sway = 10 + depth * 22;
                p.Alpha = 0.55 + depth * 0.45;
                p.Y = anywhere ? _rng.NextDouble() * h : h + 10;
                break;
            default: // Estrellas
                p.Radius = 0.6 + depth * depth * 2.0;
                p.Speed = 2 + depth * 7;
                p.Alpha = 0.25 + depth * 0.75;
                p.Y = _rng.NextDouble() * h;
                break;
        }
        return p;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs args || args.RenderingTime == _lastFrame) return;
        double dt = _lastFrame == TimeSpan.Zero ? 0 : Math.Min(0.1, (args.RenderingTime - _lastFrame).TotalSeconds);
        _lastFrame = args.RenderingTime;
        Step(dt);
        InvalidateVisual();
    }

    private void Step(double dt)
    {
        _time += dt;
        double w = ActualWidth, h = ActualHeight;

        // Posición del ratón (si está encima de la ventana) para apartar las partículas
        bool repel = false;
        Point mouse = default;
        if (Interactive && dt > 0)
        {
            mouse = System.Windows.Input.Mouse.GetPosition(this);
            repel = mouse.X >= 0 && mouse.Y >= 0 && mouse.X <= w && mouse.Y <= h
                    && Window.GetWindow(this) is { IsActive: true };
        }

        for (int i = 0; i < _particles.Length; i++)
        {
            ref var p = ref _particles[i];
            if (repel)
            {
                double dx = p.X - mouse.X, dy = p.Y - mouse.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance < RepelRadius && distance > 0.5)
                {
                    double push = (1 - distance / RepelRadius) * RepelForce * dt;
                    p.X += dx / distance * push;
                    p.Y += dy / distance * push;
                }
            }
            switch (Kind)
            {
                case BackgroundEffect.Nieve:
                    p.Y += p.Speed * dt;
                    p.X += (Math.Sin(_time * 0.9 + p.Phase) * p.Sway + 8) * dt; // vaivén + un poco de viento
                    if (p.Y > h + 10) p = Spawn(anywhere: false);
                    break;
                case BackgroundEffect.Brasas:
                    p.Y -= p.Speed * dt;
                    p.X += Math.Sin(_time * 1.4 + p.Phase) * p.Sway * dt;
                    if (p.Y < -10) p = Spawn(anywhere: false);
                    break;
                default:
                    p.X += p.Speed * 0.6 * dt;
                    p.Y += p.Speed * 0.15 * dt;
                    if (p.Y > h + 5) p.Y -= h + 10;
                    break;
            }
            if (p.X > w + 10) p.X -= w + 20;
            else if (p.X < -10) p.X += w + 20;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_particles.Length == 0 || _brushes.Length == 0) return;

        foreach (var p in _particles)
        {
            double alpha = p.Alpha;
            if (Kind == BackgroundEffect.Brasas)
                alpha *= 0.75 + 0.25 * Math.Sin(_time * 5 + p.Phase);
            else if (Kind == BackgroundEffect.Estrellas)
                alpha *= 0.5 + 0.5 * Math.Sin(_time * 1.8 + p.Phase);

            int level = Math.Clamp((int)(alpha * AlphaLevels), 0, AlphaLevels - 1);
            double r = p.Radius * 2.4; // el degradado se difumina hacia fuera, así que se dibuja más grande
            dc.DrawEllipse(_brushes[p.Color % _brushes.Length][level], null, new Point(p.X, p.Y), r, r);
        }
    }

    private static Brush[][] BuildBrushes(BackgroundEffect Kind)
    {
        Color[] colors = Kind switch
        {
            BackgroundEffect.Nieve => [Color.FromRgb(255, 255, 255), Color.FromRgb(214, 228, 255)],
            BackgroundEffect.Brasas => [Color.FromRgb(255, 181, 71), Color.FromRgb(255, 91, 58), Color.FromRgb(255, 46, 122)],
            BackgroundEffect.Estrellas => [Color.FromRgb(255, 255, 255), Color.FromRgb(255, 227, 176), Color.FromRgb(201, 214, 255)],
            _ => [],
        };

        var result = new Brush[colors.Length][];
        for (int c = 0; c < colors.Length; c++)
        {
            result[c] = new Brush[AlphaLevels];
            for (int level = 0; level < AlphaLevels; level++)
            {
                byte a = (byte)(255 * (level + 1) / AlphaLevels);
                var color = colors[c];
                var brush = new RadialGradientBrush();
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(a, color.R, color.G, color.B), 0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(a * 0.8), color.R, color.G, color.B), 0.3));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
                brush.Freeze();
                result[c][level] = brush;
            }
        }
        return result;
    }
}
