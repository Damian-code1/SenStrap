using System.Globalization;

namespace SenStrap.Core.Tweaks;

/// <summary>
/// Some settings are read by Windows only when you sign in. After writing them to the registry,
/// these calls apply them to the current session right away.
/// </summary>
internal static class LiveSettings
{
    private static int ReadInt(string key, string name, int fallback) =>
        int.TryParse(Sys.GetValue(Hive.HKCU, key, name)?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v : fallback;

    public static void PushMouse()
    {
        int[] values =
        [
            ReadInt(@"Control Panel\Mouse", "MouseThreshold1", 6),
            ReadInt(@"Control Panel\Mouse", "MouseThreshold2", 10),
            ReadInt(@"Control Panel\Mouse", "MouseSpeed", 1),
        ];
        Native.SystemParametersInfo(Native.SPI_SETMOUSE, 0, values, Native.SPIF_SENDCHANGE);
    }

    public static void PushKeyboard()
    {
        uint delay = (uint)Math.Clamp(ReadInt(@"Control Panel\Keyboard", "KeyboardDelay", 1), 0, 3);
        uint speed = (uint)Math.Clamp(ReadInt(@"Control Panel\Keyboard", "KeyboardSpeed", 31), 0, 31);
        Native.SystemParametersInfo(Native.SPI_SETKEYBOARDDELAY, delay, IntPtr.Zero, Native.SPIF_SENDCHANGE);
        Native.SystemParametersInfo(Native.SPI_SETKEYBOARDSPEED, speed, IntPtr.Zero, Native.SPIF_SENDCHANGE);
    }

    public static void PushMenuDelay()
    {
        uint delay = (uint)Math.Max(0, ReadInt(@"Control Panel\Desktop", "MenuShowDelay", 400));
        Native.SystemParametersInfo(Native.SPI_SETMENUSHOWDELAY, delay, IntPtr.Zero, Native.SPIF_SENDCHANGE);
    }

    public static void PushMinAnimate()
    {
        var info = new Native.AnimationInfo
        {
            Size = 8,
            MinAnimate = ReadInt(@"Control Panel\Desktop\WindowMetrics", "MinAnimate", 1),
        };
        Native.SystemParametersInfo(Native.SPI_SETANIMATION, info.Size, ref info, Native.SPIF_SENDCHANGE);
    }

    public static void PushStickyKeys() => PushFlags(Native.SPI_GETSTICKYKEYS, Native.SPI_SETSTICKYKEYS, "StickyKeys");

    public static void PushToggleKeys() => PushFlags(Native.SPI_GETTOGGLEKEYS, Native.SPI_SETTOGGLEKEYS, "ToggleKeys");

    private static void PushFlags(uint get, uint set, string name)
    {
        var value = new Native.FlagsStruct { Size = 8 };
        if (!Native.SystemParametersInfo(get, value.Size, ref value, 0)) return;
        value.Flags = (uint)ReadInt($@"Control Panel\Accessibility\{name}", "Flags", (int)value.Flags);
        Native.SystemParametersInfo(set, value.Size, ref value, Native.SPIF_SENDCHANGE);
    }

    public static void PushFilterKeys()
    {
        var value = new Native.FilterKeys { Size = 24 };
        if (!Native.SystemParametersInfo(Native.SPI_GETFILTERKEYS, value.Size, ref value, 0)) return;
        value.Flags = (uint)ReadInt(@"Control Panel\Accessibility\Keyboard Response", "Flags", (int)value.Flags);
        Native.SystemParametersInfo(Native.SPI_SETFILTERKEYS, value.Size, ref value, Native.SPIF_SENDCHANGE);
    }
}
