using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace CleanScan.Views;

/// <summary>
/// NativeControlHost wrapping the mpv render window.
/// Also intercepts WM_DROPFILES on the native HWND so files dragged onto the
/// player area are forwarded to the UI layer via <see cref="FileDropped"/>.
/// </summary>
public sealed class MpvHost : NativeControlHost
{
    public event Action<nint>?   HandleReady;
    public event Action<string>? FileDropped;

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

    private const int  GWLP_WNDPROC  = -4;
    private const int  GCLP_HCURSOR  = -12;
    private const int  IDC_ARROW     = 32512;
    private const uint WM_DROPFILES  = 0x0233;

    // Keep the delegate alive — GC must not collect it while the window exists.
    private WndProcDelegate? _wndProcDelegate;
    private nint             _origWndProc;

    // ── NativeControlHost ────────────────────────────────────────────────────

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle = base.CreateNativeControlCore(parent);
        var hwnd   = handle.Handle;

        // Enable shell drag-and-drop on the native HWND and subclass its WndProc.
        DragAcceptFiles(hwnd, true);
        _wndProcDelegate = WndProc;
        _origWndProc     = SetWindowLongPtr(hwnd, GWLP_WNDPROC,
                               Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

        // Set the native window cursor to a standard arrow (default is hourglass).
        SetClassLongPtr(hwnd, GCLP_HCURSOR, LoadCursor(0, IDC_ARROW));

        HandleReady?.Invoke(hwnd);
        return handle;
    }

    // ── WndProc subclass ─────────────────────────────────────────────────────

    private nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_DROPFILES)
        {
            HandleDrop(wParam);
            return 0;
        }
        return CallWindowProc(_origWndProc, hWnd, msg, wParam, lParam);
    }

    private void HandleDrop(nint hDrop)
    {
        try
        {
            var count = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
            if (count == 0) return;

            // Only process the first dropped item.
            var len = DragQueryFile(hDrop, 0, null, 0);
            if (len == 0) return;
            var buf = new char[len + 1];
            DragQueryFile(hDrop, 0, buf, len + 1);
            var path = new string(buf, 0, (int)len);

            if (!string.IsNullOrEmpty(path))
                Dispatcher.UIThread.Post(() => FileDropped?.Invoke(path));
        }
        finally
        {
            DragFinish(hDrop);
        }
    }
}
