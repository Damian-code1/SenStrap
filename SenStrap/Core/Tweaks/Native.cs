using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SenStrap.Core.Tweaks;

/// <summary>Windows API calls used by the tweaks.</summary>
internal static class Native
{
    public const uint SPIF_UPDATEINIFILE = 0x01;
    public const uint SPIF_SENDCHANGE = 0x02;

    public const uint SPI_SETMOUSE = 0x0004;
    public const uint SPI_SETKEYBOARDSPEED = 0x000B;
    public const uint SPI_SETKEYBOARDDELAY = 0x0017;
    public const uint SPI_GETFILTERKEYS = 0x0032;
    public const uint SPI_SETFILTERKEYS = 0x0033;
    public const uint SPI_GETTOGGLEKEYS = 0x0034;
    public const uint SPI_SETTOGGLEKEYS = 0x0035;
    public const uint SPI_GETSTICKYKEYS = 0x003A;
    public const uint SPI_SETSTICKYKEYS = 0x003B;
    public const uint SPI_SETANIMATION = 0x0049;
    public const uint SPI_SETMENUSHOWDELAY = 0x006B;

    [StructLayout(LayoutKind.Sequential)]
    public struct FlagsStruct
    {
        public uint Size;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FilterKeys
    {
        public uint Size;
        public uint Flags;
        public uint WaitMs, DelayMs, RepeatMs, BounceMs;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AnimationInfo
    {
        public uint Size;
        public int MinAnimate;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint action, uint uiParam, IntPtr pvParam, uint winIni);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint action, uint uiParam, ref int pvParam, uint winIni);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint action, uint uiParam, int[] pvParam, uint winIni);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint action, uint uiParam, ref FlagsStruct pvParam, uint winIni);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint action, uint uiParam, ref FilterKeys pvParam, uint winIni);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint action, uint uiParam, ref AnimationInfo pvParam, uint winIni);

    /// <summary>Reads a boolean SPI setting (SPI_GET...).</summary>
    public static bool GetBool(uint action)
    {
        int value = 0;
        SystemParametersInfo(action, 0, ref value, 0);
        return value != 0;
    }

    /// <summary>Writes a boolean SPI setting and saves it to the user profile.</summary>
    public static void SetBool(uint action, bool value) =>
        SystemParametersInfo(action, 0, value ? (IntPtr)1 : IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

    private const uint HWND_BROADCAST = 0xFFFF;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam,
        uint flags, uint timeout, out IntPtr result);

    /// <summary>Tells open windows (Explorer, the taskbar...) that a setting changed.</summary>
    public static void BroadcastSettingChange(string area)
    {
        try
        {
            SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, area, SMTO_ABORTIFHUNG, 200, out _);
        }
        catch
        {
            // Not important: the setting applies after signing in again anyway
        }
    }

    // ---------- Processes ----------

    private const int ProcessPowerThrottling = 4;
    private const uint PowerThrottlingVersion = 1;
    private const uint ThrottleExecutionSpeed = 0x1;
    private const uint ThrottleIgnoreTimerResolution = 0x4;

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr process, int infoClass, ref PowerThrottlingState info, uint size);

    /// <summary>Tells Windows never to slow this process down to save power (EcoQoS), even in the background.</summary>
    public static bool DisablePowerThrottling(Process process)
    {
        var state = new PowerThrottlingState
        {
            Version = PowerThrottlingVersion,
            ControlMask = ThrottleExecutionSpeed | ThrottleIgnoreTimerResolution,
            StateMask = 0,
        };
        uint size = (uint)Marshal.SizeOf<PowerThrottlingState>();
        if (SetProcessInformation(process.Handle, ProcessPowerThrottling, ref state, size))
            return true;

        // Windows 10 doesn't know the timer flag: try again with execution speed only
        state.ControlMask = ThrottleExecutionSpeed;
        return SetProcessInformation(process.Handle, ProcessPowerThrottling, ref state, size);
    }

    [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
    public static extern bool DnsFlushResolverCache();
}
