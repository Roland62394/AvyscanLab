using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CleanScan.Services;

public sealed class MpvService : IDisposable
{
    #region P/Invoke

    static MpvService()
    {
        NativeLibrary.SetDllImportResolver(typeof(MpvService).Assembly, (name, _, _) =>
        {
            if (name != "libmpv-2") return IntPtr.Zero;
            try
            {
                var dir  = System.IO.Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;
                var path = System.IO.Path.Combine(dir, "mpv", "libmpv-2.dll");
                return NativeLibrary.Load(path);
            }
            catch { return IntPtr.Zero; }
        });
    }

    [DllImport("libmpv-2")] private static extern nint mpv_create();
    [DllImport("libmpv-2")] private static extern int  mpv_initialize(nint ctx);
    [DllImport("libmpv-2")] private static extern void mpv_terminate_destroy(nint ctx);

    [DllImport("libmpv-2", CharSet = CharSet.Ansi)]
    private static extern int mpv_set_option_string(nint ctx, string name, string data);

    [DllImport("libmpv-2", CharSet = CharSet.Ansi)]
    private static extern nint mpv_error_string(int error);

    [DllImport("libmpv-2", CharSet = CharSet.Ansi)]
    private static extern int mpv_command(nint ctx, string?[] args);

    [DllImport("libmpv-2", CharSet = CharSet.Ansi)]
    private static extern int mpv_observe_property(nint ctx, ulong replyUserdata, string name, int format);

    [DllImport("libmpv-2")]
    private static extern nint mpv_wait_event(nint ctx, double timeout);

    [DllImport("libmpv-2", CharSet = CharSet.Ansi)]
    private static extern int mpv_get_property(nint ctx, string name, int format, out double val);

    [DllImport("libmpv-2", CharSet = CharSet.Ansi)]
    private static extern int mpv_set_property_string(nint ctx, string name, string data);

    #endregion

    #region Event structures

    [StructLayout(LayoutKind.Sequential)]
    private struct MpvEvent
    {
        public int   EventId;
        public int   Error;
        public ulong ReplyUserData;
        public nint  Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MpvEventProperty
    {
        public nint Name;
        public int  Format;
        public nint Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MpvEventEndFile
    {
        public int Reason;
        public int Error;
    }

    private const int EventShutdown       = 1;
    private const int EventEndFile        = 7;
    private const int EventFileLoaded     = 8;
    private const int EventPropertyChange = 22;
    private const int FormatDouble        = 5;
    private const int FormatFlag          = 3;
    private const int EndFileReasonError  = 4;

    #endregion

    #region Public events

    public event Action<double>? PositionChanged;
    public event Action<double>? DurationChanged;
    public event Action<bool>?   PauseChanged;
    public event Action?         FileLoaded;
    public event Action?         EndReached;
    public event Action<string>? LoadFailed;

    #endregion

    private nint                     _ctx;
    private nint                     _hwnd;
    private CancellationTokenSource? _cts;
    private Task?                    _eventTask;
    private double                   _pendingSeekPos;
    private bool                     _disposed;
    private bool                     _expectingShutdown;

    public bool IsReady => _ctx != 0;

    /// <summary>Fired (on background thread) when mpv shuts down unexpectedly during playback.</summary>
    public event Action? UnexpectedShutdown;

    public void Initialize(nint hwnd)
    {
        _hwnd = hwnd;

        try { _ctx = mpv_create(); }
        catch { return; }

        if (_ctx == 0) return;

        mpv_set_option_string(_ctx, "wid",                    hwnd.ToString());
        mpv_set_option_string(_ctx, "vo",                     "gpu");
        mpv_set_option_string(_ctx, "osc",                    "no");
        mpv_set_option_string(_ctx, "input-default-bindings", "no");
        mpv_set_option_string(_ctx, "input-vo-keyboard",      "no");
        mpv_set_option_string(_ctx, "keep-open",              "always");
        mpv_set_option_string(_ctx, "sid",                    "no");

        if (mpv_initialize(_ctx) != 0)
        {
            mpv_terminate_destroy(_ctx);
            _ctx = 0;
            return;
        }

        mpv_observe_property(_ctx, 0, "time-pos", FormatDouble);
        mpv_observe_property(_ctx, 0, "duration",  FormatDouble);
        mpv_observe_property(_ctx, 0, "pause",     FormatFlag);

        _cts       = new CancellationTokenSource();
        _eventTask = Task.Run(() => EventLoop(_ctx, _cts.Token));
    }

    public void LoadFile(string path, double startPos = 0)
    {
        if (_ctx == 0) return;
        _pendingSeekPos = startPos > 0.5 ? startPos : 0;
        mpv_command(_ctx, ["loadfile", path, null]);
    }

    public void Play()
    {
        if (_ctx == 0) return;
        mpv_set_property_string(_ctx, "pause", "no");
    }

    public void Pause()
    {
        if (_ctx == 0) return;
        mpv_set_property_string(_ctx, "pause", "yes");
    }

    public void Stop()
    {
        if (_ctx == 0) return;
        mpv_command(_ctx, ["seek", "0", "absolute", null]);
        mpv_set_property_string(_ctx, "pause", "yes");
    }

    public void TogglePlayPause()
    {
        if (_ctx == 0) return;
        mpv_command(_ctx, ["cycle", "pause", null]);
    }

    public void FrameStep()
    {
        if (_ctx == 0) return;
        mpv_command(_ctx, ["frame-step", null]);
    }

    public void FrameBackStep()
    {
        if (_ctx == 0) return;
        mpv_command(_ctx, ["frame-back-step", null]);
    }

    public void Seek(double seconds)
    {
        if (_ctx == 0) return;
        mpv_command(_ctx, ["seek", seconds.ToString("F3", CultureInfo.InvariantCulture), "absolute+exact", null]);
    }

    public double GetPosition()
    {
        if (_ctx == 0) return 0;
        mpv_get_property(_ctx, "time-pos", FormatDouble, out var val);
        return val;
    }

    public int GetTotalFrames()
    {
        if (_ctx == 0) return 0;
        if (mpv_get_property(_ctx, "estimated-frame-count", FormatDouble, out var val) != 0) return 0;
        return (int)Math.Round(val);
    }

    public double GetFps()
    {
        if (_ctx == 0) return 0;
        if (mpv_get_property(_ctx, "container-fps", FormatDouble, out var fps) == 0 && fps > 0) return fps;
        if (mpv_get_property(_ctx, "fps",           FormatDouble, out var fps2) == 0 && fps2 > 0) return fps2;
        return 0;
    }

    /// <summary>
    /// Reinitialize mpv after an unexpected shutdown (uses the same HWND as the last Initialize call).
    /// Must be called from the UI thread.
    /// </summary>
    public void Reinitialize()
    {
        if (_hwnd == 0 || _disposed) return;

        // EventLoop has already exited; clean up its .NET resources.
        _eventTask?.Wait(TimeSpan.FromMilliseconds(500));
        _eventTask = null;
        _cts?.Dispose();
        _cts = null;

        Initialize(_hwnd);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _expectingShutdown = true;
        _cts?.Cancel();

        var ctx = _ctx;
        _ctx = 0;
        if (ctx != 0)
            mpv_terminate_destroy(ctx);

        _eventTask?.Wait(TimeSpan.FromSeconds(2));
        _eventTask = null;
        _cts?.Dispose();
        _cts = null;
    }

    private void EventLoop(nint ctx, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            nint evPtr;
            try { evPtr = mpv_wait_event(ctx, 0.5); }
            catch { return; }

            if (evPtr == 0 || ct.IsCancellationRequested) continue;

            MpvEvent ev;
            try { ev = Marshal.PtrToStructure<MpvEvent>(evPtr); }
            catch { return; }

            switch (ev.EventId)
            {
                case EventShutdown:
                    if (!_expectingShutdown)
                    {
                        _ctx = 0; // invalidate before notifying UI
                        UnexpectedShutdown?.Invoke();
                    }
                    return;

                case EventFileLoaded:
                {
                    var seekPos = _pendingSeekPos;
                    _pendingSeekPos = 0;
                    FileLoaded?.Invoke();
                    if (seekPos > 0.5 && !ct.IsCancellationRequested)
                        try { mpv_command(ctx, ["seek", seekPos.ToString("F3", CultureInfo.InvariantCulture), "absolute", null]); }
                        catch { }
                    break;
                }

                case EventEndFile:
                    if (ev.Data != 0)
                    {
                        try
                        {
                            var endFile = Marshal.PtrToStructure<MpvEventEndFile>(ev.Data);
                            if (endFile.Reason == EndFileReasonError)
                            {
                                var msg = Marshal.PtrToStringAnsi(mpv_error_string(endFile.Error))
                                          ?? $"error {endFile.Error}";
                                LoadFailed?.Invoke(msg);
                                break;
                            }
                        }
                        catch { }
                    }
                    EndReached?.Invoke();
                    break;

                case EventPropertyChange:
                    if (ev.Data != 0)
                        try { HandlePropertyChange(ev.Data); }
                        catch { }
                    break;
            }
        }
    }

    private void HandlePropertyChange(nint data)
    {
        var prop = Marshal.PtrToStructure<MpvEventProperty>(data);
        if (prop.Data == 0) return;

        var name = Marshal.PtrToStringAnsi(prop.Name) ?? string.Empty;

        if (prop.Format == FormatDouble)
        {
            var val = Marshal.PtrToStructure<double>(prop.Data);
            if (name == "time-pos") PositionChanged?.Invoke(val);
            else if (name == "duration") DurationChanged?.Invoke(val);
        }
        else if (prop.Format == FormatFlag)
        {
            var val = Marshal.PtrToStructure<int>(prop.Data);
            if (name == "pause") PauseChanged?.Invoke(val != 0);
        }
    }
}
