using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SenStrap.Core;

/// <summary>
/// Anti-AFK: Roblox disconnects you after 20 minutes without input. When a Roblox window has gone
/// <see cref="Interval"/> without you using it, this presses Space in it (a jump) and gives the focus
/// back to whatever you had in front. Keys are only sent when that Roblox window is really in front,
/// so they never land in another program. Doesn't touch the game: it's the same as pressing the key.
/// One instance handles every open Roblox window (multiple accounts included).
/// </summary>
public sealed class AntiAfk : IDisposable
{
    /// <summary>Time without input in a window before it gets a key press (Roblox kicks at 20 minutes).</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    /// <summary>While you're typing elsewhere it waits for a pause, but never past this.</summary>
    private static readonly TimeSpan ForceAfter = TimeSpan.FromMinutes(18);

    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(20);

    /// <summary>Only one SenStrap at a time presses keys, even with several Roblox windows open.</summary>
    private const string MutexName = @"Local\SenStrap.AntiAfk";

    private const ushort ScanSpace = 0x39;

    private readonly Dictionary<IntPtr, DateTime> _lastActive = new();
    private readonly Timer _timer;
    private readonly Mutex? _mutex;
    private int _busy;
    private bool _warnedFocus;

    private AntiAfk(Mutex mutex)
    {
        _mutex = mutex;
        _timer = new Timer(_ => Tick(), null, CheckEvery, CheckEvery);
    }

    /// <summary>Starts the anti-AFK, or returns null if another SenStrap is already doing it.</summary>
    public static AntiAfk? TryStart()
    {
        var mutex = new Mutex(false, MutexName);
        bool owned;
        try { owned = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { owned = true; } // the previous owner closed without releasing it
        if (!owned)
        {
            mutex.Dispose();
            Logger.Info("Anti-AFK: another SenStrap is already handling it.");
            return null;
        }
        Logger.Info($"Anti-AFK on: Space after {Interval.TotalMinutes:F0} min without input in Roblox.");
        return new AntiAfk(mutex);
    }

    private void Tick()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            var windows = RobloxWindows();
            foreach (var gone in _lastActive.Keys.Where(h => !windows.Contains(h)).ToList())
                _lastActive.Remove(gone);
            if (windows.Count == 0) return;

            var now = DateTime.UtcNow;
            var userIdle = UserIdleTime();
            var foreground = GetForegroundWindow();

            foreach (var window in windows)
            {
                if (!_lastActive.ContainsKey(window)) _lastActive[window] = now;
                // You're playing in it right now
                if (window == foreground && userIdle < CheckEvery) _lastActive[window] = now;
            }

            var due = windows.Where(h => now - _lastActive[h] >= Interval).ToList();
            if (due.Count == 0) return;

            // Typing or moving the mouse in another program: wait for a pause, unless the kick is close
            bool userBusy = userIdle < TimeSpan.FromSeconds(3) && !due.Contains(foreground);
            if (userBusy && due.All(h => now - _lastActive[h] < ForceAfter)) return;

            foreach (var window in due)
            {
                if (Pulse(window))
                {
                    _lastActive[window] = DateTime.UtcNow;
                    Logger.Info("Anti-AFK: pressed Space in Roblox.");
                }
                else if (!_warnedFocus)
                {
                    _warnedFocus = true;
                    Logger.Warn("Anti-AFK: couldn't bring Roblox to the front (is Windows locked?). Will retry.");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Anti-AFK: {ex.Message}");
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }

    private static HashSet<IntPtr> RobloxWindows()
    {
        var result = new HashSet<IntPtr>();
        foreach (var process in Process.GetProcessesByName("RobloxPlayerBeta"))
        {
            using (process)
            {
                try
                {
                    var handle = process.MainWindowHandle;
                    if (handle != IntPtr.Zero && IsWindow(handle)) result.Add(handle);
                }
                catch (InvalidOperationException) { } // closed meanwhile
            }
        }
        return result;
    }

    /// <summary>Brings the window to the front, taps Space, and puts things back as they were.</summary>
    private static bool Pulse(IntPtr window)
    {
        var previous = GetForegroundWindow();
        bool minimized = IsIconic(window);
        if (minimized) ShowWindow(window, SW_RESTORE);

        bool sent = false;
        if (window == previous || BringToFront(window))
        {
            Thread.Sleep(150); // Roblox needs a moment after getting the focus
            if (GetForegroundWindow() == window)
            {
                Key(ScanSpace, down: true);
                Thread.Sleep(70);
                Key(ScanSpace, down: false);
                Thread.Sleep(80);
                sent = true;
            }
        }

        if (minimized) ShowWindow(window, SW_MINIMIZE);
        if (previous != IntPtr.Zero && previous != window && IsWindow(previous)) BringToFront(previous);
        return sent;
    }

    private static bool BringToFront(IntPtr window)
    {
        uint foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        uint thisThread = GetCurrentThreadId();
        bool attached = foregroundThread != 0 && foregroundThread != thisThread
                        && AttachThreadInput(thisThread, foregroundThread, true);
        try
        {
            BringWindowToTop(window);
            SetForegroundWindow(window);
        }
        finally
        {
            if (attached) AttachThreadInput(thisThread, foregroundThread, false);
        }
        if (GetForegroundWindow() == window) return true;

        // Windows refuses focus changes from background programs; holding Alt while asking lifts that
        Key(ScanAlt, down: true);
        SetForegroundWindow(window);
        Key(ScanAlt, down: false);
        return GetForegroundWindow() == window;
    }

    private const ushort ScanAlt = 0x38;

    private static void Key(ushort scanCode, bool down)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT { wScan = scanCode, dwFlags = KEYEVENTF_SCANCODE | (down ? 0 : KEYEVENTF_KEYUP) },
            },
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    private static TimeSpan UserIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
    }

    public void Dispose()
    {
        _timer.Dispose();
        try { _mutex?.ReleaseMutex(); } catch (ApplicationException) { }
        _mutex?.Dispose();
        Logger.Info("Anti-AFK off.");
    }

    // ===================== Win32 =====================

    private const int SW_RESTORE = 9, SW_MINIMIZE = 6;
    private const uint INPUT_KEYBOARD = 1, KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_SCANCODE = 0x0008;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    // The union must be as big as its largest member (MOUSEINPUT) or SendInput rejects the size
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize, dwTime;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
}
