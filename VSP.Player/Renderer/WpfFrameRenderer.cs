using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VSP.Player.Entities;
using VSP.Player.Interfaces;

namespace VSP.Player.Renderer;

/// <summary>
/// Converts CPU-resident <see cref="DecodedFrame"/>s into a WPF <see cref="WriteableBitmap"/>.
/// <see cref="WriteableBitmap.WritePixels(Int32Rect, byte[], int, int)"/> updates the existing
/// bitmap's pixels in place, so a bound Image control repaints automatically without requiring
/// a property-changed notification on every frame.
/// </summary>
public sealed class WpfFrameRenderer : IFrameRenderer
{
    private readonly Dispatcher _dispatcher;
    private WriteableBitmap? _bitmap;
    private volatile bool _updatePending;
    private volatile bool _isActive;
    private volatile bool _disposed;

    public WpfFrameRenderer(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public bool IsActive => _isActive;

    public ImageSource? CurrentFrameSource { get; private set; }

    public event EventHandler? FrameRendered;

    public void Start() => _isActive = true;

    public void Stop() => _isActive = false;

    public void OnFrame(DecodedFrame frame)
    {
        if (_disposed || !_isActive || frame.Storage != FrameStorage.Cpu || frame.PixelBuffer is null)
        {
            frame.Dispose();
            return;
        }

        if (_updatePending)
        {
            // A render is already queued on the UI thread; drop this frame rather than let the
            // dispatcher queue grow unbounded under UI lag.
            frame.Dispose();
            return;
        }

        _updatePending = true;
        _dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => RenderOnUiThread(frame)));
    }

    private void RenderOnUiThread(DecodedFrame frame)
    {
        try
        {
            if (_bitmap is null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height)
            {
                _bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null);
                CurrentFrameSource = _bitmap;
            }

            _bitmap.WritePixels(
                new Int32Rect(0, 0, frame.Width, frame.Height),
                frame.PixelBuffer!,
                frame.Stride,
                0);

            FrameRendered?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            frame.Dispose();
            _updatePending = false;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _isActive = false;
    }
}
