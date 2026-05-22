using System.Windows.Media;

namespace DicomViewer.Wpf.ViewModels;

public sealed record MeasurementOverlayItem(
    PointCollection Points,
    double LabelX,
    double LabelY,
    string Label,
    Brush Stroke,
    double StrokeThickness);
