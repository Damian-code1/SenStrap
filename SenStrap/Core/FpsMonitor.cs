using System.Runtime.InteropServices;
using System.Security.Principal;

namespace SenStrap.Core;

/// <summary>
/// Counts the frames Roblox shows, from Windows' own graphics events (the DXGI "Present" event, the same
/// source Xbox Game Bar and PresentMon use). Nothing is injected into Roblox and its memory isn't read.
/// Reading these events needs administrator rights or the "Performance Log Users" group.
/// Works with Direct3D (Roblox's default); Vulkan and OpenGL don't send these events.
/// </summary>
public sealed class FpsMonitor : IDisposable
{
    /// <summary>Built-in Windows group "Performance Log Users".</summary>
    public const string PerformanceLogUsersSid = "S-1-5-32-559";

    private const string SessionName = "SenStrap FPS counter";
    private static readonly Guid DxgiProvider = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
    private const ushort PresentStart = 42;
    private const ushort PresentMultiplaneOverlayStart = 55;

    private readonly Func<int, bool> _isTarget;
    private readonly EventRecordCallback _callback;
    private Thread? _thread;
    private ulong _traceHandle = InvalidHandle;
    private IntPtr _loggerName;
    private long _frames;
    private bool _disposed;

    /// <param name="isTarget">Tells whether a process id is Roblox. Called from a background thread.</param>
    public FpsMonitor(Func<int, bool> isTarget)
    {
        _isTarget = isTarget;
        _callback = OnEvent;
    }

    /// <summary>Frames counted since the start. The caller turns this into FPS.</summary>
    public long Frames => Interlocked.Read(ref _frames);

    /// <summary>Whether this Windows account can read the graphics events right now.</summary>
    public static bool HasAccess()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator)
                || principal.IsInRole(new SecurityIdentifier(PerformanceLogUsersSid));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Starts listening. Returns false (and logs why) if Windows doesn't allow it.</summary>
    public bool Start()
    {
        int size = Marshal.SizeOf<EventTraceProperties>() + 1024;
        IntPtr props = Marshal.AllocHGlobal(size);
        try
        {
            ulong session = 0;
            uint error = 0;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                FillProperties(props, size);
                error = StartTraceW(out session, SessionName, props);
                if (error != ErrorAlreadyExists) break;

                // A session left behind by an earlier SenStrap that didn't close properly
                FillProperties(props, size);
                ControlTraceW(0, SessionName, props, ControlStop);
            }
            if (error != 0)
            {
                Logger.Info($"FPS counter: Windows didn't allow reading graphics events (error {error}).");
                return false;
            }

            var provider = DxgiProvider;
            error = EnableTraceEx2(session, ref provider, EnableProvider, TraceLevelInformation, 0, 0, 0, IntPtr.Zero);
            if (error != 0)
            {
                Logger.Info($"FPS counter: couldn't enable DXGI events (error {error}).");
                StopSession();
                return false;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(props);
        }

        _loggerName = Marshal.StringToHGlobalUni(SessionName);
        var logfile = new EventTraceLogfile
        {
            LoggerName = _loggerName,
            ProcessTraceMode = ProcessTraceModeRealTime | ProcessTraceModeEventRecord,
            EventRecordCallback = Marshal.GetFunctionPointerForDelegate(_callback),
        };
        _traceHandle = OpenTraceW(ref logfile);
        if (_traceHandle == InvalidHandle)
        {
            Logger.Info($"FPS counter: couldn't open the event session (error {Marshal.GetLastWin32Error()}).");
            StopSession();
            return false;
        }

        _thread = new Thread(() => ProcessTrace([_traceHandle], 1, IntPtr.Zero, IntPtr.Zero))
        {
            IsBackground = true,
            Name = "SenStrap FPS events",
        };
        _thread.Start();
        Logger.Info("FPS counter started.");
        return true;
    }

    private void OnEvent(IntPtr record)
    {
        // EVENT_RECORD starts with EVENT_HEADER: ProcessId at offset 12, EventDescriptor.Id at offset 40
        ushort id = (ushort)Marshal.ReadInt16(record, 40);
        if (id != PresentStart && id != PresentMultiplaneOverlayStart) return;
        int pid = Marshal.ReadInt32(record, 12);
        if (_isTarget(pid)) Interlocked.Increment(ref _frames);
    }

    private static void FillProperties(IntPtr props, int size)
    {
        var bytes = new byte[size];
        Marshal.Copy(bytes, 0, props, size);
        var p = new EventTraceProperties
        {
            Wnode = new WnodeHeader
            {
                BufferSize = (uint)size,
                Flags = WnodeFlagTracedGuid,
                ClientContext = 1, // QPC timestamps
                Guid = Guid.NewGuid(),
            },
            LogFileMode = EventTraceRealTimeMode,
            BufferSize = 64,
            MinimumBuffers = 4,
            MaximumBuffers = 16,
            FlushTimer = 1,
            LoggerNameOffset = (uint)Marshal.SizeOf<EventTraceProperties>(),
        };
        Marshal.StructureToPtr(p, props, false);
    }

    private static void StopSession()
    {
        int size = Marshal.SizeOf<EventTraceProperties>() + 1024;
        IntPtr props = Marshal.AllocHGlobal(size);
        try
        {
            FillProperties(props, size);
            ControlTraceW(0, SessionName, props, ControlStop);
        }
        finally
        {
            Marshal.FreeHGlobal(props);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopSession(); // makes ProcessTrace return
        if (_traceHandle != InvalidHandle) CloseTrace(_traceHandle);
        _thread?.Join(2000);
        if (_loggerName != IntPtr.Zero) Marshal.FreeHGlobal(_loggerName);
    }

    // ---------- Windows API (ETW) ----------

    private const uint ErrorAlreadyExists = 183;
    private const uint ControlStop = 1;
    private const uint EnableProvider = 1;
    private const byte TraceLevelInformation = 4;
    private const uint WnodeFlagTracedGuid = 0x00020000;
    private const uint EventTraceRealTimeMode = 0x00000100;
    private const uint ProcessTraceModeRealTime = 0x00000100;
    private const uint ProcessTraceModeEventRecord = 0x10000000;
    private const ulong InvalidHandle = ulong.MaxValue;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void EventRecordCallback(IntPtr eventRecord);

    [StructLayout(LayoutKind.Sequential)]
    private struct WnodeHeader
    {
        public uint BufferSize;
        public uint ProviderId;
        public ulong HistoricalContext;
        public long TimeStamp;
        public Guid Guid;
        public uint ClientContext;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTraceProperties
    {
        public WnodeHeader Wnode;
        public uint BufferSize;
        public uint MinimumBuffers;
        public uint MaximumBuffers;
        public uint MaximumFileSize;
        public uint LogFileMode;
        public uint FlushTimer;
        public uint EnableFlags;
        public int AgeLimit;
        public uint NumberOfBuffers;
        public uint FreeBuffers;
        public uint EventsLost;
        public uint BuffersWritten;
        public uint LogBuffersLost;
        public uint RealTimeBuffersLost;
        public IntPtr LoggerThreadId;
        public uint LogFileNameOffset;
        public uint LoggerNameOffset;
    }

    /// <summary>EVENT_TRACE_LOGFILEW (64-bit layout); only the fields SenStrap sets.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 448)]
    private struct EventTraceLogfile
    {
        [FieldOffset(0)] public IntPtr LogFileName;
        [FieldOffset(8)] public IntPtr LoggerName;
        [FieldOffset(28)] public uint ProcessTraceMode;
        [FieldOffset(424)] public IntPtr EventRecordCallback;
        [FieldOffset(440)] public IntPtr Context;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint StartTraceW(out ulong sessionHandle, string sessionName, IntPtr properties);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ControlTraceW(ulong sessionHandle, string sessionName, IntPtr properties, uint controlCode);

    [DllImport("advapi32.dll")]
    private static extern uint EnableTraceEx2(ulong traceHandle, ref Guid providerId, uint controlCode, byte level,
        ulong matchAnyKeyword, ulong matchAllKeyword, uint timeout, IntPtr enableParameters);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern ulong OpenTraceW(ref EventTraceLogfile logfile);

    [DllImport("advapi32.dll")]
    private static extern uint ProcessTrace(ulong[] handleArray, uint handleCount, IntPtr startTime, IntPtr endTime);

    [DllImport("advapi32.dll")]
    private static extern uint CloseTrace(ulong traceHandle);
}
