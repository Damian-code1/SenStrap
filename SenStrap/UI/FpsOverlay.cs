using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SenStrap.Core;

namespace SenStrap.UI;

/// <summary>
/// Small FPS counter drawn by SenStrap on top of Roblox's top-left corner. It's a separate, click-through
/// window: Roblox isn't modified. Only visible while Roblox is the active window. Closes when Roblox closes.
/// </summary>
public sealed class FpsOverlay : Window
{
    private const string RobloxProcess = "RobloxPlayerBeta";

    private readonly TextBlock _text;
    private readonly Border _box;
    private readonly FpsMonitor _monitor;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Stopwatch _sinceStart = Stopwatch.StartNew();
    private readonly Stopwatch _sinceSample = Stopwatch.StartNew();
    private volatile HashSet<int> _robloxPids = new();
    private DateTime _lastSeenRoblox = DateTime.UtcNow;
    private long _lastFrames;
    private double _fps;
    private IntPtr _hwnd;

    private FpsOverlay()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -10000;
        Top = -10000;
        Focusable = false;
        IsHitTestVisible = false;

        _text = new TextBlock
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
        };
        _box = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromArgb(0xB4, 0x0E, 0x0F, 0x17)),
            Padding = new Thickness(7, 3, 7, 3),
            Child = _text,
            Opacity = 0,
        };
        Content = _box;

        _monitor = new FpsMonitor(pid => _robloxPids.Contains(pid));
        _timer.Tick += (_, _) => Tick();
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(_hwnd, GwlExStyle);
            SetWindowLong(_hwnd, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExNoActivate | WsExLayered);
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            _monitor.Dispose();
        };
    }

    /// <summary>Starts the counter if Windows allows reading frame events. Returns null otherwise.</summary>
    public static FpsOverlay? Start()
    {
        if (!FpsMonitor.HasAccess())
        {
            Logger.Info("FPS counter is on, but this account can't read frame events yet (allow it in FastFlags).");
            return null;
        }
        var overlay = new FpsOverlay();
        overlay.RefreshPids();
        if (!overlay._monitor.Start()) return null;
        overlay.Show();
        overlay._timer.Start();
        return overlay;
    }

    private List<Process> RefreshPids()
    {
        var processes = Process.GetProcessesByName(RobloxProcess).ToList();
        _robloxPids = processes.Select(p => p.Id).ToHashSet();
        if (processes.Count > 0) _lastSeenRoblox = DateTime.UtcNow;
        return processes;
    }

    private void Tick()
    {
        var processes = RefreshPids();
        try
        {
            // Roblox closed (or never opened): the counter is done
            if (processes.Count == 0)
            {
                if (DateTime.UtcNow - _lastSeenRoblox > TimeSpan.FromSeconds(5) && _sinceStart.Elapsed > TimeSpan.FromSeconds(10))
                    Close();
                _box.Opacity = 0;
                return;
            }

            if (_sinceSample.ElapsedMilliseconds >= 500)
            {
                long frames = _monitor.Frames;
                _fps = (frames - _lastFrames) / _sinceSample.Elapsed.TotalSeconds;
                _lastFrames = frames;
                _sinceSample.Restart();
                _text.Text = $"{Math.Round(_fps)} FPS";
            }

            IntPtr roblox = processes.Select(p => p.MainWindowHandle).FirstOrDefault(h => h != IntPtr.Zero);
            bool active = roblox != IntPtr.Zero && GetForegroundWindow() == roblox && !IsIconic(roblox);
            _box.Opacity = active && _fps >= 1 ? 1 : 0;
            if (!active || _hwnd == IntPtr.Zero) return;

            // Below Roblox's top bar, in its top-left corner (pixels, scaled for the monitor's DPI)
            var origin = new NativePoint();
            ClientToScreen(roblox, ref origin);
            double scale = GetDpiForWindow(roblox) / 96.0;
            int x = origin.X + (int)(12 * scale);
            int y = origin.Y + (int)(64 * scale);
            SetWindowPos(_hwnd, HwndTopmost, x, y, 0, 0, SwpNoSize | SwpNoActivate);
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    // ---------- Windows API ----------

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExToolWindow = 0x80;
    private const int WsExLayered = 0x80000;
    private const int WsExNoActivate = 0x08000000;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hwnd, ref NativePoint point);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
