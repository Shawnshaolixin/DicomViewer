using System.Windows;
using System.Windows.Input;
using DicomViewer.Domain.Enums;
using DicomViewer.Wpf.ViewModels;

namespace DicomViewer.Wpf;

public partial class MainWindow : Window
{
    private DragMode _dragMode;
    private Point _lastViewportPoint;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void ViewportSurface_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || !viewModel.HasSeriesItems)
        {
            return;
        }

        if (viewModel.CurrentToolMode is ViewerToolMode.Pan or ViewerToolMode.WindowLevel)
        {
            _dragMode = viewModel.CurrentToolMode == ViewerToolMode.Pan ? DragMode.Pan : DragMode.WindowLevel;
            _lastViewportPoint = e.GetPosition(ViewportSurface);
            ViewportSurface.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (viewModel.CurrentToolMode is ViewerToolMode.MeasureLength or ViewerToolMode.MeasureAngle)
        {
            var imagePoint = TryGetImagePoint(e);
            if (imagePoint is not null)
            {
                viewModel.AddMeasurementPoint(imagePoint.Value);
                e.Handled = true;
            }
        }
    }

    private void ViewportSurface_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || !viewModel.HasSeriesItems)
        {
            return;
        }

        if (_dragMode != DragMode.None && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPoint = e.GetPosition(ViewportSurface);
            var delta = currentPoint - _lastViewportPoint;
            _lastViewportPoint = currentPoint;

            if (_dragMode == DragMode.Pan)
            {
                viewModel.PanViewport(delta.X, delta.Y);
            }
            else if (_dragMode == DragMode.WindowLevel)
            {
                viewModel.AdjustWindowLevelFromDrag(delta.X, delta.Y);
            }

            e.Handled = true;
        }

        if (viewModel.CurrentToolMode is ViewerToolMode.MeasureLength or ViewerToolMode.MeasureAngle)
        {
            var imagePoint = TryGetImagePoint(e);
            if (imagePoint is not null)
            {
                viewModel.UpdateMeasurementPreview(imagePoint.Value);
            }
        }
    }

    private void ViewportSurface_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragMode == DragMode.None)
        {
            return;
        }

        _dragMode = DragMode.None;
        ViewportSurface.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void ViewportSurface_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || !viewModel.HasSeriesItems)
        {
            return;
        }

        viewModel.ZoomFromWheel(e.Delta);
        e.Handled = true;
    }

    private Point? TryGetImagePoint(MouseEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.ViewportImageSource is null || viewModel.ImagePixelWidth <= 0 || viewModel.ImagePixelHeight <= 0)
        {
            return null;
        }

        var point = e.GetPosition(ViewportImageCanvas);
        return new Point(
            Math.Clamp(point.X, 0, Math.Max(0, viewModel.ImagePixelWidth - 1)),
            Math.Clamp(point.Y, 0, Math.Max(0, viewModel.ImagePixelHeight - 1)));
    }

    private enum DragMode
    {
        None,
        Pan,
        WindowLevel,
    }
}
