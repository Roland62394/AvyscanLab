using System;
using System.Collections.Generic;
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

    [DllImport("libmpv-2", CharSet = CharSet.Ansi)]
    private static extern int mpv_request_log_messages(nint ctx, string min_level);

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

    [StructLayout(LayoutKind.Sequential)]
    private struct MpvEventLogMessage
    {
        public nint Prefix;   // const char*
        public nint Level;    // const char*
        public nint Text;     // const char*
        public int  LogLevel; // mpv_log_level
    }

    private const int EventShutdown       = 1;
    private const int EventLogMessage     = 9;
    private const int EventEndFile        = 7;
    private const int EventFileLoaded      = 8;
    private const int EventPlaybackRestart = 21;
    private const int EventPropertyChange  = 22;
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
    public event Action?         PlaybackRestart;

    #endregion

    private nint                     _ctx;
    private nint                     _hwnd;
    private CancellationTokenSource? _cts;
    private Task?                    _eventTask;
    private double                   _pendingSeekPos;
    private bool                     _disposed;
    private bool                     _expectingShutdown;
    private volatile bool            _paused = true;
    private double                   _duration;
    private readonly List<string>    _errorLogs = [];
    private readonly object          _errorLogLock = new();

    public bool IsReady => _ctx != 0;
    public double Duration => _duration;

    /// <summary>Returns a snapshot of recent error/warning log messages from mpv.</summary>
    public string GetLastErrorLogs()
    {
        lock (_errorLogLock) { return string.Join("\n", _errorLogs); }
    }

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
        mpv_set_option_string(_ctx, "pause",                   "yes");
        mpv_set_option_string(_ctx, "sid",                    "no");
        mpv_set_option_string(_ctx, "framedrop",             "vo");
        mpv_set_option_string(_ctx, "video-sync",            "audio");

        if (mpv_initialize(_ctx) != 0)
        {
            mpv_terminate_destroy(_ctx);
            _ctx = 0;
            return;
        }

        mpv_request_log_messages(_ctx, "v");

        mpv_observe_property(_ctx, 0, "time-pos", FormatDouble);
        mpv_observe_property(_ctx, 0, "duration",  FormatDouble);
        mpv_observe_property(_ctx, 0, "pause",     FormatFlag);

        _cts       = new CancellationTokenSource();
        _eventTask = Task.Run(() => EventLoop(_ctx, _cts.Token));
    }

    public void LoadFile(string path, double startPos = 0)
    {
        if (_ctx == 0) return;

        // Stop any in-progress load (AviSynth script parsing) before loading
        // a new file.  Without this, rapid clip switches can overwrite
        // ScriptUser.avs while AviSynth is still reading it → segfault.
        mpv_command(_ctx, ["stop", null]);
        _paused = true;
        mpv_set_property_string(_ctx, "pause", "yes");
        lock (_errorLogLock) { _errorLogs.Clear(); }
        _pendingSeekPos = startPos > 0.5 ? startPos : 0;
        mpv_command(_ctx, ["loadfile", path, null]);
    }

    public void Play()
    {
        if (_ctx == 0) return;
        _paused = false;
        mpv_set_property_string(_ctx, "pause", "no");
    }

    public void Pause()
    {
        if (_ctx == 0) return;
        _paused = true;
        mpv_set_property_string(_ctx, "pause", "yes");
    }

    public void Stop()
    {
        if (_ctx == 0) return;
        _paused = true;
        mpv_command(_ctx, ["seek", "0", "absolute", null]);
        mpv_set_property_string(_ctx, "pause", "yes");
    }

    /// <summary>Unloads the current file, leaving mpv idle (black screen).</summary>
    public void Unload()
    {
        if (_ctx == 0) return;
        mpv_command(_ctx, ["stop", null]);
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

    public void SetSpeed(double speed)
    {
        if (_ctx == 0) return;
        mpv_set_property_string(_ctx, "speed", speed.ToString("F2", CultureInfo.InvariantCulture));
    }

    public bool IsPaused() => _paused;

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

        // Wait for EventLoop to exit BEFORE destroying the mpv context,
        // otherwise mpv_wait_event may be called on a destroyed handle
        // → ExecutionEngineException / access violation.
        try { _eventTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _eventTask = null;

        var ctx = _ctx;
        _ctx = 0;
        if (ctx != 0)
            mpv_terminate_destroy(ctx);

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

                case EventPlaybackRestart:
                    PlaybackRestart?.Invoke();
                    break;

                case EventLogMessage:
                    if (ev.Data != 0)
                    {
                        try
                        {
                            var logMsg = Marshal.PtrToStructure<MpvEventLogMessage>(ev.Data);
                            var prefix = Marshal.PtrToStringAnsi(logMsg.Prefix) ?? "";
                            var level  = Marshal.PtrToStringAnsi(logMsg.Level)  ?? "";
                            var text   = Marshal.PtrToStringAnsi(logMsg.Text)?.Trim() ?? "";
                            if (!string.IsNullOrEmpty(text))
                            {
                                // Keep error/warn messages from any module,
                                // plus verbose messages that mention script/avisynth keywords
                                var isError = level is "error" or "fatal" or "warn";
                                var isRelevant = text.Contains("avisynth", StringComparison.OrdinalIgnoreCase)
                                              || text.Contains("script", StringComparison.OrdinalIgnoreCase)
                                              || text.Contains("error", StringComparison.OrdinalIgnoreCase)
                                              || text.Contains("failed", StringComparison.OrdinalIgnoreCase);
                                if (isError || isRelevant)
                                    lock (_errorLogLock) { _errorLogs.Add($"[{prefix}] {text}"); }
                            }
                        }
                        catch { }
                    }
                    break;

                case EventEndFile:
                    if (ev.Data != 0)
                    {
                        try
                        {
                            var endFile = Marshal.PtrToStructure<MpvEventEndFile>(ev.Data);
                            if (endFile.Reason == EndFileReasonError)
                            {
                                string msg;
                                lock (_errorLogLock)
                                {
                                    msg = _errorLogs.Count > 0
                                        ? string.Join("\n", _errorLogs)
                                        : Marshal.PtrToStringAnsi(mpv_error_string(endFile.Error))
                                          ?? $"error {endFile.Error}";
                                    _errorLogs.Clear();
                                }
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
            else if (name == "duration") { _duration = val; DurationChanged?.Invoke(val); }
        }
        else if (prop.Format == FormatFlag)
        {
            var val = Marshal.PtrToStructure<int>(prop.Data);
            if (name == "pause") { _paused = val != 0; PauseChanged?.Invoke(val != 0); }
        }
    }
}
