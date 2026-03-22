using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using CleanScan.Services;

namespace CleanScan.Views;

/// <summary>
/// Handles mouse-drawn rectangle selection on the video player to set GamMac X/Y/W/H.
/// Activated when the GamMac parameter panel is expanded.
/// Uses native Win32 mouse messages from MpvHost (because the native HWND sits
/// above Avalonia content — "airspace" issue).
/// </summary>
public sealed class RegionDrawController
{
    private readonly Canvas _canvas;
    private readonly MpvHost _mpvHost;
    private readonly Func<MpvService?> _getMpv;
    private readonly Func<bool> _isPreview;
    private readonly Func<bool> _isHalfRes;
    private readonly Action<int, int, int, int> _commitRegion;

    private bool _drawModeActive;
    private bool _isDrawing;
    private Point _startPoint;
    private Rectangle? _selectionRect;

    public RegionDrawController(
        Canvas canvas,
        MpvHost mpvHost,
        Func<MpvService?> getMpv,
        Func<bool> isPreview,
        Func<bool> isHalfRes,
        Action<int, int, int, int> commitRegion)
    {
        _canvas = canvas;
        _mpvHost = mpvHost;
        _getMpv = getMpv;
        _isPreview = isPreview;
        _isHalfRes = isHalfRes;
        _commitRegion = commitRegion;

        // Subscribe to native mouse events from MpvHost (bypasses airspace issue)
        _mpvHost.NativeMouseDown += OnNativeMouseDown;
        _mpvHost.NativeMouseMoved += OnNativeMouseMoved;
        _mpvHost.NativeMouseUp += OnNativeMouseUp;
    }

    /// <summary>Enable or disable rectangle drawing mode.</summary>
    public void UpdateDrawMode(bool active)
    {
        _drawModeActive = active;
        _mpvHost.DrawModeActive = active;

        if (!active)
        {
            CancelDraw();
        }
    }

    private void OnNativeMouseDown(double x, double y)
    {
        if (!_drawModeActive) return;

        // Clamp to canvas bounds
        x = Math.Max(0, Math.Min(x, _canvas.Bounds.Width));
        y = Math.Max(0, Math.Min(y, _canvas.Bounds.Height));

        _startPoint = new Point(x, y);
        _isDrawing = true;

        // The selection rectangle on the Canvas is invisible behind the native HWND,
        // but we keep it for potential future use if airspace is resolved.
        _selectionRect = new Rectangle
        {
            Stroke = new SolidColorBrush(Colors.Red),
            StrokeThickness = 2,
            StrokeDashArray = [6, 3],
            Fill = new SolidColorBrush(Color.FromArgb(40, 255, 0, 0)),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(_selectionRect, x);
        Canvas.SetTop(_selectionRect, y);
        _selectionRect.Width = 0;
        _selectionRect.Height = 0;
        _canvas.Children.Add(_selectionRect);

        // Show a real-time drawbox via mpv during the drag
        UpdateMpvDrawbox(x, y, 0, 0);
    }

    private void OnNativeMouseMoved(double x, double y)
    {
        if (!_isDrawing || _selectionRect is null) return;

        // Clamp to canvas bounds
        x = Math.Max(0, Math.Min(x, _canvas.Bounds.Width));
        y = Math.Max(0, Math.Min(y, _canvas.Bounds.Height));

        var rx = Math.Min(x, _startPoint.X);
        var ry = Math.Min(y, _startPoint.Y);
        var rw = Math.Abs(x - _startPoint.X);
        var rh = Math.Abs(y - _startPoint.Y);

        Canvas.SetLeft(_selectionRect, rx);
        Canvas.SetTop(_selectionRect, ry);
        _selectionRect.Width = rw;
        _selectionRect.Height = rh;

        // Update the mpv drawbox in real time
        UpdateMpvDrawbox(rx, ry, rw, rh);
    }

    private void OnNativeMouseUp(double x, double y)
    {
        if (!_isDrawing || _selectionRect is null) return;

        _isDrawing = false;

        // Clamp to canvas bounds
        x = Math.Max(0, Math.Min(x, _canvas.Bounds.Width));
        y = Math.Max(0, Math.Min(y, _canvas.Bounds.Height));

        // Get the drawn rectangle
        var cx = Math.Min(x, _startPoint.X);
        var cy = Math.Min(y, _startPoint.Y);
        var cw = Math.Abs(x - _startPoint.X);
        var ch = Math.Abs(y - _startPoint.Y);

        // Remove the visual rectangle
        _canvas.Children.Remove(_selectionRect);
        _selectionRect = null;

        // Ignore tiny rectangles (accidental clicks)
        if (cw < 5 || ch < 5)
        {
            ClearMpvDrawbox();
            return;
        }

        // Convert canvas coords → source video coords
        var result = CanvasToSourceCoords(cx, cy, cw, ch);
        if (result is not var (sx, sy, sw, sh)) return;

        _commitRegion(sx, sy, sw, sh);
    }

    private void CancelDraw()
    {
        if (_selectionRect is not null)
        {
            _canvas.Children.Remove(_selectionRect);
            _selectionRect = null;
        }
        _isDrawing = false;
    }

    /// <summary>
    /// Updates the mpv drawbox overlay in real time during dragging.
    /// Converts canvas logical coordinates to video source coordinates for the drawbox.
    /// </summary>
    private void UpdateMpvDrawbox(double cx, double cy, double cw, double ch)
    {
        var mpv = _getMpv();
        if (mpv is null) return;

        var videoSize = mpv.GetVideoSize();
        if (videoSize.Width <= 0 || videoSize.Height <= 0) return;

        double vidW = videoSize.Width;
        double vidH = videoSize.Height;
        double canvasW = _canvas.Bounds.Width;
        double canvasH = _canvas.Bounds.Height;
        if (canvasW <= 0 || canvasH <= 0) return;

        // Compute the letterboxed video display rect
        double scale = Math.Min(canvasW / vidW, canvasH / vidH);
        double dispW = vidW * scale;
        double dispH = vidH * scale;
        double offX = (canvasW - dispW) / 2.0;
        double offY = (canvasH - dispH) / 2.0;

        // Map canvas coords to video pixel coords
        int vx = (int)Math.Max(0, (cx - offX) / scale);
        int vy = (int)Math.Max(0, (cy - offY) / scale);
        int vw = Math.Max(1, (int)(cw / scale));
        int vh = Math.Max(1, (int)(ch / scale));

        mpv.ShowRegionOverlay(vx, vy, vw, vh);
    }

    private void ClearMpvDrawbox()
    {
        _getMpv()?.ClearRegionOverlay();
    }

    /// <summary>
    /// Converts canvas pixel rectangle to source 1:1 coordinates,
    /// accounting for letterboxing and preview mode.
    /// Always returns coordinates in the original full-resolution source space.
    /// </summary>
    private (int X, int Y, int W, int H)? CanvasToSourceCoords(
        double cx, double cy, double cw, double ch)
    {
        var mpv = _getMpv();
        if (mpv is null) return null;

        var videoSize = mpv.GetVideoSize();
        if (videoSize.Width <= 0 || videoSize.Height <= 0) return null;

        double vidW = videoSize.Width;
        double vidH = videoSize.Height;
        double canvasW = _canvas.Bounds.Width;
        double canvasH = _canvas.Bounds.Height;
        if (canvasW <= 0 || canvasH <= 0) return null;

        // Compute the letterboxed video display rect within the canvas
        double scale = Math.Min(canvasW / vidW, canvasH / vidH);
        double dispW = vidW * scale;
        double dispH = vidH * scale;
        double offX = (canvasW - dispW) / 2.0;
        double offY = (canvasH - dispH) / 2.0;

        // Clip the drawn rect to the video display area
        double x1 = Math.Max(cx, offX);
        double y1 = Math.Max(cy, offY);
        double x2 = Math.Min(cx + cw, offX + dispW);
        double y2 = Math.Min(cy + ch, offY + dispH);
        if (x2 <= x1 || y2 <= y1) return null;

        // Map from display-pixel space to video-pixel space
        double vx = (x1 - offX) / scale;
        double vy = (y1 - offY) / scale;
        double vw = (x2 - x1) / scale;
        double vh = (y2 - y1) / scale;

        bool isPreview = _isPreview();
        if (isPreview)
        {
            double halfVidW = vidW / 2.0;

            // Must be on the right half (the "final" clip)
            if (vx + vw <= halfVidW) return null; // entirely on left half → ignore

            // Clamp to right half — coordinates within the half
            double rx = Math.Max(vx - halfVidW, 0);
            double rw = Math.Min(vx + vw, vidW) - Math.Max(vx, halfVidW);
            double ry = vy;
            double rh = vh;

            // Preview always shows each half at 0.5× source (both preview_half
            // and !preview_half end up with HalfSizeEven in _BuildPreview).
            // → multiply by 2 to get source 1:1 coordinates.
            rx *= 2; rw *= 2;
            ry *= 2; rh *= 2;

            return (
                Math.Max(0, (int)Math.Round(rx)),
                Math.Max(0, (int)Math.Round(ry)),
                Math.Max(1, (int)Math.Round(rw)),
                Math.Max(1, (int)Math.Round(rh))
            );
        }
        else
        {
            // No preview: mpv video = final clip
            // preview_half=true → video is at half res, multiply by 2 for source 1:1
            // preview_half=false → video is at full res, coordinates are already 1:1
            double upscale = _isHalfRes() ? 2.0 : 1.0;

            return (
                Math.Max(0, (int)Math.Round(vx * upscale)),
                Math.Max(0, (int)Math.Round(vy * upscale)),
                Math.Max(1, (int)Math.Round(vw * upscale)),
                Math.Max(1, (int)Math.Round(vh * upscale))
            );
        }
    }
}
