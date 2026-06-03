using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DicomViewer.Application.Models;

namespace DicomViewer.Wpf.Converters;

public sealed class ViewportImageToBitmapSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ViewportImageData image || image.Width <= 0 || image.Height <= 0 || image.Pixels.Length == 0)
        {
            return null;
        }

        var pixelFormat = image.PixelFormat == ViewportPixelFormat.Rgb24
            ? PixelFormats.Rgb24
            : PixelFormats.Gray8;

        var bitmap = BitmapSource.Create(
            image.Width,
            image.Height,
            96,
            96,
            pixelFormat,
            null,
            image.Pixels,
            image.Stride);

        bitmap.Freeze();
        return bitmap;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
