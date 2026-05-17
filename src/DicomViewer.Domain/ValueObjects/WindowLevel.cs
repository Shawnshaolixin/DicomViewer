namespace DicomViewer.Domain.ValueObjects;

public sealed record WindowLevel(double Width, double Center)
{
    public override string ToString()
    {
        return $"WW {Width:0} / WL {Center:0}";
    }
}