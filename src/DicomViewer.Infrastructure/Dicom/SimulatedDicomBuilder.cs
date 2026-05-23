using DicomViewer.Domain.Entities;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;

namespace DicomViewer.Infrastructure.Dicom;

public sealed class SimulatedDicomBuilder
{
    private const int ImageWidth = 256;
    private const int ImageHeight = 256;
    private const double DefaultWindowWidth = 255.0;
    private const double DefaultWindowCenter = 127.5;

    private readonly string _defaultOutputDirectory;

    public SimulatedDicomBuilder(string? outputDirectory = null)
    {
        _defaultOutputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "simulated-output")
            : outputDirectory;
    }

    public async Task<string> BuildAsync(
        ExamSession session,
        string imageId,
        DateTime acquiredAtUtc,
        string? outputDirectory = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targetOutputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? _defaultOutputDirectory
            : outputDirectory;

        Directory.CreateDirectory(targetOutputDirectory);
        var filePath = Path.Combine(targetOutputDirectory, $"{imageId}.dcm");
        var localAcquiredTime = acquiredAtUtc.ToLocalTime();
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage },
            { DicomTag.SOPInstanceUID, DicomUID.Generate() },
            { DicomTag.PatientName, session.Order.PatientName },
            { DicomTag.PatientID, session.Order.PatientId },
            { DicomTag.StudyInstanceUID, DicomUID.Generate() },
            { DicomTag.SeriesInstanceUID, DicomUID.Generate() },
            { DicomTag.StudyDescription, $"{session.Order.ProcedureDescription} Simulated Study" },
            { DicomTag.SeriesDescription, $"{session.Order.BodyPart} {session.Order.Projection} Simulated Series" },
            { DicomTag.Modality, "DX" },
            { DicomTag.BodyPartExamined, session.ExposureParameters.BodyPart },
            { DicomTag.ViewPosition, session.ExposureParameters.Projection },
            { DicomTag.StudyDate, localAcquiredTime.ToString("yyyyMMdd") },
            { DicomTag.StudyTime, localAcquiredTime.ToString("HHmmss") },
            { DicomTag.ContentDate, localAcquiredTime.ToString("yyyyMMdd") },
            { DicomTag.ContentTime, localAcquiredTime.ToString("HHmmss") },
            { DicomTag.AcquisitionDateTime, acquiredAtUtc.ToString("yyyyMMddHHmmss") },
            { DicomTag.InstanceNumber, 1 },
            { DicomTag.Rows, (ushort)ImageHeight },
            { DicomTag.Columns, (ushort)ImageWidth },
            { DicomTag.SamplesPerPixel, (ushort)1 },
            { DicomTag.PhotometricInterpretation, "MONOCHROME2" },
            { DicomTag.BitsAllocated, (ushort)8 },
            { DicomTag.BitsStored, (ushort)8 },
            { DicomTag.HighBit, (ushort)7 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.WindowWidth, DefaultWindowWidth },
            { DicomTag.WindowCenter, DefaultWindowCenter },
            { DicomTag.PixelSpacing, new[] { 0.25, 0.25 } },
            { DicomTag.KVP, session.ExposureParameters.KilovoltagePeak },
            { DicomTag.XRayTubeCurrent, (int)Math.Round(session.ExposureParameters.TubeCurrentMilliampere) },
            { DicomTag.ExposureTime, (int)Math.Round(session.ExposureParameters.ExposureTimeMilliseconds) },
            { DicomTag.Exposure, (int)Math.Round(session.ExposureParameters.MilliampereSeconds * 1000.0) },
            { DicomTag.DistanceSourceToDetector, session.ExposureParameters.SourceToImageDistanceMillimeter },
        };

        var pixelData = DicomPixelData.Create(dataset, true);
        var pixels = GeneratePixels(session, imageId);
        pixelData.AddFrame(new MemoryByteBuffer(pixels));

        await new DicomFile(dataset).SaveAsync(filePath);
        return filePath;
    }

    private static byte[] GeneratePixels(ExamSession session, string imageId)
    {
        var pixels = new byte[ImageWidth * ImageHeight];
        var brightness = Math.Clamp(45.0 + ((session.ExposureParameters.KilovoltagePeak - 40.0) / 110.0 * 120.0) + (session.ExposureParameters.MilliampereSeconds * 4.0), 35.0, 220.0);
        var contrast = Math.Clamp(25.0 + (session.ExposureParameters.KilovoltagePeak / 3.0), 25.0, 90.0);
        var seed = HashCode.Combine(imageId, session.Order.OrderId, session.ExposureParameters.KilovoltagePeak, session.ExposureParameters.MilliampereSeconds);
        var random = new Random(seed);

        for (var y = 0; y < ImageHeight; y++)
        {
            for (var x = 0; x < ImageWidth; x++)
            {
                var horizontal = (x - (ImageWidth / 2.0)) / (ImageWidth / 2.0);
                var vertical = (y - (ImageHeight / 2.0)) / (ImageHeight / 2.0);
                var radial = Math.Sqrt((horizontal * horizontal) + (vertical * vertical));
                var anatomyMask = Math.Max(0.0, 1.0 - radial);
                var ribPattern = Math.Sin((x / 14.0) + (y / 40.0)) * 14.0;
                var centerBoost = Math.Exp(-(horizontal * horizontal * 4.0 + vertical * vertical * 5.0)) * contrast;
                var noise = random.NextDouble() * 10.0 - 5.0;
                var value = brightness + (anatomyMask * 45.0) + ribPattern + centerBoost + noise;

                if (Math.Abs(horizontal) < 0.015 || Math.Abs(vertical) < 0.015)
                {
                    value -= 20.0;
                }

                pixels[(y * ImageWidth) + x] = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
            }
        }

        return pixels;
    }
}