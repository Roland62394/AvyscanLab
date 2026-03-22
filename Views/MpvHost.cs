using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace AvyscanLab.Views;

/// <summary>
/// NativeControlHost wrapping the mpv render window.
/// Also intercepts WM_DROPFILES on the native HWND so files dragged onto the
/// player area are forwarded to the UI layer via <see cref="FileDropped"/>.
/// When <see cref="DrawModeActive"/> is true, mouse messages are intercepted
/// to support region drawing over the native surface.
/// </summary>
public sealed class MpvHost : NativeControlHost
{
    public event Action<nint>?   HandleReady;
    public event Action<List<string>>? FilesDropped;

    /// <summary>Fired on WM_LBUTTONDOWN when draw mode is active (x, y in physical pixels).</summary>
    public event Action<double, double>? NativeMouseDown;
    /// <summary>Fired on WM_MOUSEMOVE when draw mode is active (x, y in physical pixels).</summary>
    public event Action<double, double>? NativeMouseMoved;
    /// <summary>Fired on WM_LBUTTONUP when draw mode is active (x, y in physical pixels).</summary>
    public event Action<double, double>? NativeMouseUp;

    /// <summary>When true, mouse messages are intercepted for region drawing.</summary>
    public bool DrawModeActive { get; set; }

    // ── Win32 P/Invoke ───────────────────────────────────────────────────────

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")] private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint newProc);
    [DllImport("user32.dll")] private static extern nint CallWindowProc(nint lpPrev, nint hWnd, uint msg, nint w, nint l);

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(nint hWnd, bool fAccept);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(nint hDrop, uint iFile, [Out] char[]? buf, uint cch);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(nint hDrop);

    [DllImport("user32.dll")]
    private static extern nint LoadCursor(nint hInstance, int lpCursorName);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
    private static extern nint SetClassLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool ChangeWindowMessageFilterEx(nint hwnd, uint message, uint action, nint pChangeFilterStruct);

    [DllImport("user32.dll")]
    private static extern nint SetCapture(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern nint SetCursor(nint hCursor);

    private const int  GWLP_WNDPROC       = -4;
    private const int  GCLP_HCURSOR       = -12;
    private const int  IDC_ARROW          = 32512;
    private const int  IDC_CROSS          = 32515;
    private const uint WM_DROPFILES       = 0x0233;
    private const uint WM_COPYGLOBALDATA  = 0x0049;
    private const uint WM_SETCURSOR       = 0x0020;
    private const uint WM_LBUTTONDOWN     = 0x0201;
    private const uint WM_MOUSEMOVE       = 0x0200;
    private const uint WM_LBUTTONUP       = 0x0202;
    private const uint MSGFLT_ALLOW       = 1;

    // Keep the delegate alive — GC must not collect it while the window exists.
    private WndProcDelegate? _wndProcDelegate;
    private nint             _origWndProc;
    private nint             _crossCursor;
    private nint             _hwnd;

    // ── NativeControlHost ────────────────────────────────────────────────────

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle = base.CreateNativeControlCore(parent);
        _hwnd = handle.Handle;

        // Enable shell drag-and-drop on the native HWND and subclass its WndProc.
        // Allow WM_DROPFILES through UIPI when the process runs elevated
        // (e.g. launched from the NSIS installer finish page).
        ChangeWindowMessageFilterEx(_hwnd, WM_DROPFILES, MSGFLT_ALLOW, 0);
        ChangeWindowMessageFilterEx(_hwnd, WM_COPYGLOBALDATA, MSGFLT_ALLOW, 0);
        DragAcceptFiles(_hwnd, true);
        _wndProcDelegate = WndProc;
        _origWndProc     = SetWindowLongPtr(_hwnd, GWLP_WNDPROC,
                               Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

        // Set the native window cursor to a standard arrow (default is hourglass).
        SetClassLongPtr(_hwnd, GCLP_HCURSOR, LoadCursor(0, IDC_ARROW));

        HandleReady?.Invoke(_hwnd);
        return handle;
    }

    // ── WndProc subclass ─────────────────────────────────────────────────────

    private nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        try
        {
            if (msg == WM_DROPFILES)
            {
                HandleDrop(wParam);
                return 0;
            }

            if (DrawModeActive)
            {
                if (msg == WM_SETCURSOR)
                {
                    if (_crossCursor == 0) _crossCursor = LoadCursor(0, IDC_CROSS);
                    SetCursor(_crossCursor);
                    return 1; // handled — prevents default arrow
                }

                if (msg == WM_LBUTTONDOWN)
                {
                    SetCapture(hWnd);
                    PostMouseEvent(NativeMouseDown, lParam);
                    return 0;
                }

                if (msg == WM_MOUSEMOVE)
                {
                    PostMouseEvent(NativeMouseMoved, lParam);
                    return 0;
                }

                if (msg == WM_LBUTTONUP)
                {
                    ReleaseCapture();
                    PostMouseEvent(NativeMouseUp, lParam);
                    return 0;
                }
            }
        }
        catch
        {
            // Never let an exception escape the WndProc subclass — it would
            // unhook the procedure and permanently break drag-and-drop.
        }
        return CallWindowProc(_origWndProc, hWnd, msg, wParam, lParam);
    }

    private void PostMouseEvent(Action<double, double>? handler, nint lParam)
    {
        if (handler is null) return;
        // lParam: LOWORD = x, HIWORD = y (signed for captured out-of-bounds moves)
        int px = (short)(lParam & 0xFFFF);
        int py = (short)((lParam >> 16) & 0xFFFF);
        // Convert physical pixels → Avalonia logical pixels
        double scale = VisualRoot?.RenderScaling ?? 1.0;
        double x = px / scale;
        double y = py / scale;
        Dispatcher.UIThread.Post(() => handler(x, y));
    }

    private void HandleDrop(nint hDrop)
    {
        try
        {
            var count = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
            if (count == 0) return;

            var paths = new List<string>();
            for (uint i = 0; i < count; i++)
            {
                var len = DragQueryFile(hDrop, i, null, 0);
                if (len == 0) continue;
                var buf = new char[len + 1];
                DragQueryFile(hDrop, i, buf, len + 1);
                var path = new string(buf, 0, (int)len);
                if (!string.IsNullOrEmpty(path))
                    paths.Add(path);
            }

            if (paths.Count > 0)
                Dispatcher.UIThread.Post(() => FilesDropped?.Invoke(paths));
        }
        finally
        {
            DragFinish(hDrop);
        }
    }
}
