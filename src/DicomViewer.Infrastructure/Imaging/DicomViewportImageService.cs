using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Domain.ValueObjects;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Render;

namespace DicomViewer.Infrastructure.Imaging;

public sealed class DicomViewportImageService : IViewportImageService
{
    public ViewportImageData? TryLoad(string filePath, int frameIndex, WindowLevel windowLevel)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var dicomFile = DicomFile.Open(filePath);
            var dataset = dicomFile.Dataset;
            var pixelData = DicomPixelData.Create(dataset);
            if (pixelData.NumberOfFrames == 0 || pixelData.SamplesPerPixel != 1)
            {
                return null;
            }

            var safeFrameIndex = Math.Clamp(frameIndex, 0, pixelData.NumberOfFrames - 1);
            var frame = PixelDataFactory.Create(pixelData, safeFrameIndex);
            var photometricInterpretation = dataset.GetSingleValueOrDefault(DicomTag.PhotometricInterpretation, "MONOCHROME2");
            var invert = string.Equals(photometricInterpretation, "MONOCHROME1", StringComparison.OrdinalIgnoreCase);
            var slope = dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1.0);
            var intercept = dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0.0);

            return frame switch
            {
                GrayscalePixelDataU8 image8 => BuildImage(image8.Data, image8.Width, image8.Height, windowLevel, invert, slope, intercept),
                GrayscalePixelDataU16 image16 => BuildImage(image16.Data, image16.Width, image16.Height, windowLevel, invert, slope, intercept),
                GrayscalePixelDataS16 image16Signed => BuildImage(image16Signed.Data, image16Signed.Width, image16Signed.Height, windowLevel, invert, slope, intercept),
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static ViewportImageData BuildImage(
        byte[] source,
        int width,
        int height,
        WindowLevel windowLevel,
        bool invert,
        double slope,
        double intercept)
    {
        var pixels = new byte[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            pixels[index] = MapToByte(source[index], windowLevel, invert, slope, intercept);
        }

        return new ViewportImageData(pixels, width, height, width);
    }

    private static ViewportImageData BuildImage(
        ushort[] source,
        int width,
        int height,
        WindowLevel windowLevel,
        bool invert,
        double slope,
        double intercept)
    {
        var pixels = new byte[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            pixels[index] = MapToByte(source[index], windowLevel, invert, slope, intercept);
        }

        return new ViewportImageData(pixels, width, height, width);
    }

    private static ViewportImageData BuildImage(
        short[] source,
        int width,
        int height,
        WindowLevel windowLevel,
        bool invert,
        double slope,
        double intercept)
    {
        var pixels = new byte[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            pixels[index] = MapToByte(source[index], windowLevel, invert, slope, intercept);
        }

        return new ViewportImageData(pixels, width, height, width);
    }

    private static byte MapToByte(double sourceValue, WindowLevel windowLevel, bool invert, double slope, double intercept)
    {
        var modalityValue = sourceValue * slope + intercept;
        var width = Math.Max(windowLevel.Width, 1.0);
        var center = windowLevel.Center;
        var lower = center - 0.5 - ((width - 1.0) / 2.0);
        var upper = center - 0.5 + ((width - 1.0) / 2.0);

        double mappedValue;
        if (modalityValue <= lower)
        {
            mappedValue = 0;
        }
        else if (modalityValue > upper)
        {
            mappedValue = 255;
        }
        else
        {
            mappedValue = ((modalityValue - (center - 0.5)) / (width - 1.0) + 0.5) * 255.0;
        }

        var output = (byte)Math.Clamp((int)Math.Round(mappedValue), 0, 255);
        return invert ? (byte)(255 - output) : output;
    }
}