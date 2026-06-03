using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Domain.ValueObjects;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Render;

namespace DicomViewer.Infrastructure.Imaging;

/// <summary>
/// 负责把 DICOM 像素数据转换为查看器可直接显示的灰度位图数据。
/// 当前实现有意只覆盖学习项目最核心的灰度图像路径，避免过早引入彩色图和调色板复杂度。
/// </summary>
public sealed class DicomViewportImageService : IViewportImageService
{
    /// <summary>
    /// 加载指定帧的像素数据，并依据窗宽窗位映射为 Gray8 图像。
    /// </summary>
    public ViewportLoadResult TryLoad(string filePath, int frameIndex, WindowLevel windowLevel)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            var message = filePath.StartsWith("Samples/", StringComparison.OrdinalIgnoreCase)
                ? "当前样例数据仅提供元数据，不包含可渲染的像素文件。"
                : $"影像文件不存在: {filePath}";
            return new ViewportLoadResult(null, message);
        }

        try
        {
            var dicomFile = DicomFile.Open(filePath);
            var dataset = dicomFile.Dataset;
            var pixelData = DicomPixelData.Create(dataset);

            if (pixelData.NumberOfFrames == 0)
            {
                return new ViewportLoadResult(null, "当前影像不包含可读取帧。");
            }

            var safeFrameIndex = Math.Clamp(frameIndex, 0, pixelData.NumberOfFrames - 1);
            var frame = PixelDataFactory.Create(pixelData, safeFrameIndex);
            var photometricInterpretation = dataset.GetSingleValueOrDefault(DicomTag.PhotometricInterpretation, "MONOCHROME2");
            var invert = string.Equals(photometricInterpretation, "MONOCHROME1", StringComparison.OrdinalIgnoreCase);
            var slope = dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1.0);
            var intercept = dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0.0);

            var image = frame switch
            {
                GrayscalePixelDataU8 image8 => BuildImage(image8.Data, image8.Width, image8.Height, windowLevel, invert, slope, intercept),
                GrayscalePixelDataU16 image16 => BuildImage(image16.Data, image16.Width, image16.Height, windowLevel, invert, slope, intercept),
                GrayscalePixelDataS16 image16Signed => BuildImage(image16Signed.Data, image16Signed.Width, image16Signed.Height, windowLevel, invert, slope, intercept),
                ColorPixelData24 colorImage => BuildColorImage(colorImage.Data, colorImage.Width, colorImage.Height),
                _ => null,
            };

            return image is null
                ? new ViewportLoadResult(null, "当前像素格式暂不支持显示。")
                : new ViewportLoadResult(image, $"已加载第 {safeFrameIndex + 1} 帧图像。");
        }
        catch
        {
            return new ViewportLoadResult(null, "影像加载失败，文件可能损坏或像素数据不受支持。");
        }
    }

    /// <summary>
    /// 把 8 位灰度原始像素映射为查看器使用的字节流。
    /// </summary>
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

    /// <summary>
    /// 把 16 位无符号灰度原始像素映射为查看器使用的字节流。
    /// </summary>
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

    /// <summary>
    /// 把 16 位有符号灰度原始像素映射为查看器使用的字节流。
    /// </summary>
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

    private static ViewportImageData BuildColorImage(
        byte[] source,
        int width,
        int height)
    {
        var pixels = new byte[source.Length];
        Buffer.BlockCopy(source, 0, pixels, 0, source.Length);
        return new ViewportImageData(pixels, width, height, width * 3, ViewportPixelFormat.Rgb24);
    }

    /// <summary>
    /// 先应用 Rescale Slope/Intercept，再按窗宽窗位压缩到 0-255 灰度区间。
    /// </summary>
    private static byte MapToByte(double sourceValue, WindowLevel windowLevel, bool invert, double slope, double intercept)
    {
        // 先应用 modality LUT 的线性部分，再做窗宽窗位压缩到 0-255，便于 WPF 直接显示 Gray8。
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