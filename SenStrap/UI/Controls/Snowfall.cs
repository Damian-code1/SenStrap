using System.Windows;
using System.Windows.Media;

namespace SenStrap.UI.Controls;

/// <summary>
/// Snow drawn by hand every frame. Flakes live on three depth layers: far ones are small, dim and slow,
/// near ones are large, soft (out of focus) and fast. A slowly changing wind with gusts pushes the near
/// layers more than the far ones, and every flake has its own sway.
/// </summary>
public sealed class Snowfall : FrameworkElement
{
    public static readonly DependencyProperty IsSnowingProperty = DependencyProperty.Register(
        nameof(IsSnowing), typeof(bool), typeof(Snowfall),
        new FrameworkPropertyMetadata(true, (d, _) => ((Snowfall)d).OnSnowingChanged()));

    public static readonly DependencyProperty DensityProperty = DependencyProperty.Register(
        nameof(Density), typeof(double), typeof(Snowfall),
        new FrameworkPropertyMetadata(1.0, (d, _) => ((Snowfall)d).EnsureCount()));

    public bool IsSnowing
    {
        get => (bool)GetValue(IsSnowingProperty);
        set => SetValue(IsSnowingProperty, value);
    }

    /// <summary>1 = normal. Higher means more flakes.</summary>
    public double Density
    {
        get => (double)GetValue(DensityProperty);
        set => SetValue(DensityProperty, value);
    }

    private enum Layer : byte { Far, Mid, Near }

    private struct Flake
    {
        public double X, Y, Radius, Fall, SwayAmp, SwayFreq, Phase, Alpha, Drift;
        public Layer Layer;
    }

    private const int AlphaLevels = 16;
    private static readonly Brush[][] Brushes = BuildBrushes();

    private readonly Random _rng = new();
    private Flake[] _flakes = [];
    private bool _subscribed;
    private TimeSpan _lastFrame;
    private double _time;
    private double _windSeed;

    public Snowfall()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        _windSeed = _rng.NextDouble() * 1000;
        Loaded += (_, _) => { EnsureCount(); UpdateSubscription(); };
        Unloaded += (_, _) => Unsubscribe();
        IsVisibleChanged += (_, _) => UpdateSubscription();
        SizeChanged += (_, _) => EnsureCount();
    }

    private void OnSnowingChanged()
    {
        if (!IsSnowing) _flakes = [];
        EnsureCount();
        UpdateSubscription();
        InvalidateVisual();
    }

    private void UpdateSubscription()
    {
        bool run = IsLoaded && IsVisible && IsSnowing;
        if (run && !_subscribed)
        {
            _lastFrame = TimeSpan.Zero;
            CompositionTarget.Rendering += OnRendering;
            _subscribed = true;
        }
        else if (!run)
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

    private void EnsureCount()
    {
        int target = !IsSnowing || ActualWidth < 1 || ActualHeight < 1
            ? 0
            : (int)Math.Clamp(ActualWidth * ActualHeight / 5200 * Density, 40, 460);
        if (_flakes.Length == target) return;

        var list = new List<Flake>(target);
        list.AddRange(_flakes.Take(target));
        while (list.Count < target)
            list.Add(Spawn(anywhere: true));
        _flakes = list.ToArray();
        InvalidateVisual();
    }

    private Flake Spawn(bool anywhere)
    {
        double w = Math.Max(1, ActualWidth), h = Math.Max(1, ActualHeight);
        double roll = _rng.NextDouble();
        double t = _rng.NextDouble(); // position inside the layer's range
        var f = new Flake
        {
            Phase = _rng.NextDouble() * Math.PI * 2,
            SwayFreq = 0.18 + _rng.NextDouble() * 0.35,
        };

        if (roll < 0.56)
        {
            f.Layer = Layer.Far;
            f.Radius = 0.6 + t * 0.7;
            f.Fall = 13 + t * 12;
            f.SwayAmp = 3 + _rng.NextDouble() * 5;
            f.Alpha = 0.30 + t * 0.25;
            f.Drift = 0.35;
        }
        else if (roll < 0.91)
        {
            f.Layer = Layer.Mid;
            f.Radius = 1.2 + t * 1.1;
            f.Fall = 27 + t * 22;
            f.SwayAmp = 6 + _rng.NextDouble() * 10;
            f.Alpha = 0.55 + t * 0.30;
            f.Drift = 0.7;
        }
        else
        {
            f.Layer = Layer.Near;
            f.Radius = 3.2 + t * 3.4;
            f.Fall = 52 + t * 42;
            f.SwayAmp = 10 + _rng.NextDouble() * 14;
            f.Alpha = 0.16 + t * 0.20;
            f.Drift = 1.25;
        }

        double margin = f.Radius * 3;
        f.X = -40 + _rng.NextDouble() * (w + 80);
        f.Y = anywhere ? _rng.NextDouble() * h : -margin - _rng.NextDouble() * 40;
        return f;
    }

    /// <summary>Wind in px/s for the nearest layer: a light steady breeze with slow gusts on top.</summary>
    private double Wind(double t)
    {
        t += _windSeed;
        double gusts = Math.Sin(t * 0.071) * 0.55 + Math.Sin(t * 0.163 + 1.7) * 0.3 + Math.Sin(t * 0.41 + 0.6) * 0.15;
        double strength = Math.Max(0, gusts); // gusts only blow one way, then fade back to the breeze
        return 9 + strength * strength * 46;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs args || args.RenderingTime == _lastFrame) return;
        double dt = _lastFrame == TimeSpan.Zero ? 0 : Math.Min(0.05, (args.RenderingTime - _lastFrame).TotalSeconds);
        _lastFrame = args.RenderingTime;
        if (Window.GetWindow(this) is { WindowState: WindowState.Minimized }) return;
        Step(dt);
        InvalidateVisual();
    }

    private void Step(double dt)
    {
        if (dt <= 0) return;
        _time += dt;
        double w = ActualWidth, h = ActualHeight;
        double wind = Wind(_time);

        for (int i = 0; i < _flakes.Length; i++)
        {
            ref var f = ref _flakes[i];
            double angle = _time * f.SwayFreq * Math.PI * 2 + f.Phase;
            // Sideways sway, and the flake falls a little slower at the ends of each swing
            f.X += (wind * f.Drift + Math.Sin(angle) * f.SwayAmp) * dt;
            f.Y += f.Fall * (0.82 + 0.18 * Math.Abs(Math.Cos(angle))) * dt;

            double margin = f.Radius * 3;
            if (f.Y > h + margin) f = Spawn(anywhere: false);
            else if (f.X > w + 40) f.X -= w + 80;
            else if (f.X < -40) f.X += w + 80;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        foreach (var f in _flakes)
        {
            int level = Math.Clamp((int)(f.Alpha * AlphaLevels), 0, AlphaLevels - 1);
            var brush = Brushes[(int)f.Layer][level];
            if (f.Layer == Layer.Near)
            {
                // Out of focus: drawn wider and softer
                double r = f.Radius * 2.1;
                dc.DrawEllipse(brush, null, new Point(f.X, f.Y), r, r * 1.08);
            }
            else
            {
                double r = f.Radius * 1.7;
                dc.DrawEllipse(brush, null, new Point(f.X, f.Y), r, r);
            }
        }
    }

    private static Brush[][] BuildBrushes()
    {
        // Far flakes are slightly lavender (they are seen through more of the violet air)
        var colors = new[] { Color.FromRgb(218, 208, 250), Color.FromRgb(240, 236, 255), Color.FromRgb(255, 255, 255) };
        var result = new Brush[3][];
        for (int layer = 0; layer < 3; layer++)
        {
            result[layer] = new Brush[AlphaLevels];
            var c = colors[layer];
            for (int level = 0; level < AlphaLevels; level++)
            {
                byte a = (byte)(255 * (level + 1) / AlphaLevels);
                var brush = new RadialGradientBrush();
                if (layer == (int)Layer.Near)
                {
                    brush.GradientStops.Add(new GradientStop(Color.FromArgb(a, c.R, c.G, c.B), 0));
                    brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(a * 0.75), c.R, c.G, c.B), 0.45));
                    brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(a * 0.25), c.R, c.G, c.B), 0.8));
                    brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
                }
                else
                {
                    brush.GradientStops.Add(new GradientStop(Color.FromArgb(a, c.R, c.G, c.B), 0));
                    brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(a * 0.92), c.R, c.G, c.B), 0.45));
                    brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
                }
                brush.Freeze();
                result[layer][level] = brush;
            }
        }
        return result;
    }
}
