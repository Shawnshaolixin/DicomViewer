using DicomViewer.Application.Abstractions;
using DicomViewer.Domain.Entities;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;
using FellowOakDicom;

namespace DicomViewer.Infrastructure.Data;

public sealed class FileSystemStudyCatalogService : IStudyCatalogService
{
    public async Task<IReadOnlyList<Patient>> LoadAsync(string? sourcePath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return BuildSamplePatients();
        }

        if (!Directory.Exists(sourcePath))
        {
            return Array.Empty<Patient>();
        }

        var records = new List<DicomRecord>();
        foreach (var filePath in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = await TryReadRecordAsync(filePath, cancellationToken);
            if (record is not null)
            {
                records.Add(record);
            }
        }

        return records.Count == 0 ? Array.Empty<Patient>() : BuildPatients(records);
    }

    private static async Task<DicomRecord?> TryReadRecordAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var dicomFile = await DicomFile.OpenAsync(filePath, FileReadOption.Default, 0);
            var dataset = dicomFile.Dataset;

            var patientId = GetString(dataset, DicomTag.PatientID, "UNKNOWN");
            var patientName = GetString(dataset, DicomTag.PatientName, "Unknown Patient");
            var studyUid = GetString(dataset, DicomTag.StudyInstanceUID, $"STUDY-{Guid.NewGuid():N}");
            var studyDescription = GetString(dataset, DicomTag.StudyDescription, "Unnamed Study");
            var seriesUid = GetString(dataset, DicomTag.SeriesInstanceUID, $"SERIES-{Guid.NewGuid():N}");
            var seriesDescription = GetString(dataset, DicomTag.SeriesDescription, "Unnamed Series");
            var sopUid = GetString(dataset, DicomTag.SOPInstanceUID, $"INSTANCE-{Guid.NewGuid():N}");
            var modality = MapModality(GetString(dataset, DicomTag.Modality, "OT"));
            var instanceNumber = GetInt(dataset, DicomTag.InstanceNumber, 0);
            var width = GetInt(dataset, DicomTag.Columns, 0);
            var height = GetInt(dataset, DicomTag.Rows, 0);
            var frameCount = GetInt(dataset, DicomTag.NumberOfFrames, 1);
            var pixelSpacing = GetPixelSpacing(dataset);
            var windowLevel = new WindowLevel(
                GetDouble(dataset, DicomTag.WindowWidth, GetDefaultWindowWidth(modality)),
                GetDouble(dataset, DicomTag.WindowCenter, GetDefaultWindowCenter(modality)));

            return new DicomRecord(
                patientId,
                patientName,
                studyUid,
                studyDescription,
                ParseStudyDate(GetString(dataset, DicomTag.StudyDate, string.Empty)),
                seriesUid,
                seriesDescription,
                modality,
                sopUid,
                filePath,
                instanceNumber,
                width,
                height,
                frameCount,
                pixelSpacing,
                windowLevel);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<Patient> BuildPatients(IReadOnlyList<DicomRecord> records)
    {
        // 这里把扁平文件记录重新组装成 Patient -> Study -> Series -> Instance 的层级结构。
        return records
            .GroupBy(record => new { record.PatientId, record.PatientName })
            .OrderBy(group => group.Key.PatientName)
            .Select(patientGroup => new Patient
            {
                PatientId = patientGroup.Key.PatientId,
                PatientName = patientGroup.Key.PatientName,
                Studies = patientGroup
                    .GroupBy(record => new { record.StudyInstanceUid, record.StudyDescription, record.StudyDate })
                    .OrderBy(group => group.Key.StudyDate)
                    .ThenBy(group => group.Key.StudyDescription)
                    .Select(studyGroup => new Study
                    {
                        StudyInstanceUid = studyGroup.Key.StudyInstanceUid,
                        StudyDescription = studyGroup.Key.StudyDescription,
                        StudyDate = studyGroup.Key.StudyDate,
                        SeriesList = studyGroup
                            .GroupBy(record => new { record.SeriesInstanceUid, record.SeriesDescription, record.Modality })
                            .OrderBy(group => group.Key.SeriesDescription)
                            .Select(seriesGroup => new Series
                            {
                                SeriesInstanceUid = seriesGroup.Key.SeriesInstanceUid,
                                SeriesDescription = seriesGroup.Key.SeriesDescription,
                                Modality = seriesGroup.Key.Modality,
                                Instances = seriesGroup
                                    .OrderBy(record => record.InstanceNumber)
                                    .ThenBy(record => record.FilePath)
                                    .Select(record => new ImageInstance
                                    {
                                        SopInstanceUid = record.SopInstanceUid,
                                        FilePath = record.FilePath,
                                        InstanceNumber = record.InstanceNumber,
                                        Width = record.Width,
                                        Height = record.Height,
                                        FrameCount = record.FrameCount,
                                        PixelSpacing = record.PixelSpacing,
                                        DefaultWindowLevel = record.WindowLevel,
                                    })
                                    .ToList(),
                            })
                            .ToList(),
                    })
                    .ToList(),
            })
            .ToList();
    }

    private static IReadOnlyList<Patient> BuildSamplePatients()
    {
        return new[]
        {
            new Patient
            {
                PatientId = "P-0001",
                PatientName = "Demo Patient",
                Studies = new[]
                {
                    new Study
                    {
                        StudyInstanceUid = "1.2.840.10008.1",
                        StudyDescription = "Head trauma follow-up",
                        StudyDate = new DateTime(2026, 5, 17),
                        SeriesList = new[]
                        {
                            BuildSampleSeries("1.2.840.10008.1.1", "Head CT Axial", ModalityType.CT, 24, 512, 512, new WindowLevel(80, 40)),
                            BuildSampleSeries("1.2.840.10008.1.2", "Brain MR T1", ModalityType.MR, 18, 384, 384, new WindowLevel(400, 40)),
                            BuildSampleSeries("1.2.840.10008.1.3", "Chest CT Lung Window", ModalityType.CT, 30, 512, 512, new WindowLevel(1500, -600)),
                        }
                    }
                }
            }
        };
    }

    private static Series BuildSampleSeries(
        string seriesInstanceUid,
        string description,
        ModalityType modality,
        int imageCount,
        int width,
        int height,
        WindowLevel windowLevel)
    {
        var instances = Enumerable.Range(1, imageCount)
            .Select(index => new ImageInstance
            {
                SopInstanceUid = $"{seriesInstanceUid}.{index}",
                FilePath = $"Samples/{seriesInstanceUid}/{index:000}.dcm",
                InstanceNumber = index,
                Width = width,
                Height = height,
                FrameCount = 1,
                PixelSpacing = new PixelSpacing(0.65, 0.65),
                DefaultWindowLevel = windowLevel,
            })
            .ToList();

        return new Series
        {
            SeriesInstanceUid = seriesInstanceUid,
            SeriesDescription = description,
            Modality = modality,
            Instances = instances,
        };
    }

    private static string GetString(DicomDataset dataset, DicomTag tag, string fallback)
    {
        return dataset.TryGetSingleValue(tag, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static int GetInt(DicomDataset dataset, DicomTag tag, int fallback)
    {
        return dataset.TryGetSingleValue(tag, out int value) ? value : fallback;
    }

    private static double GetDouble(DicomDataset dataset, DicomTag tag, double fallback)
    {
        if (dataset.TryGetSingleValue(tag, out double value))
        {
            return value;
        }

        if (dataset.TryGetValues(tag, out double[]? values) && values.Length > 0)
        {
            return values[0];
        }

        return fallback;
    }

    private static PixelSpacing GetPixelSpacing(DicomDataset dataset)
    {
        if (dataset.TryGetValues(DicomTag.PixelSpacing, out double[]? values) && values.Length >= 2)
        {
            return new PixelSpacing(values[0], values[1]);
        }

        return new PixelSpacing(1.0, 1.0);
    }

    private static DateTime? ParseStudyDate(string value)
    {
        return DateTime.TryParseExact(value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static ModalityType MapModality(string modality)
    {
        // 先覆盖当前项目会遇到的常见模态，未知值统一回退为 Unknown。
        return modality.ToUpperInvariant() switch
        {
            "CT" => ModalityType.CT,
            "MR" => ModalityType.MR,
            "CR" => ModalityType.CR,
            "DX" => ModalityType.DR,
            "DR" => ModalityType.DR,
            "US" => ModalityType.US,
            _ => ModalityType.Unknown,
        };
    }

    private static double GetDefaultWindowWidth(ModalityType modality)
    {
        return modality switch
        {
            ModalityType.CT => 350,
            ModalityType.MR => 400,
            _ => 255,
        };
    }

    private static double GetDefaultWindowCenter(ModalityType modality)
    {
        return modality switch
        {
            ModalityType.CT => 40,
            ModalityType.MR => 40,
            _ => 128,
        };
    }

    private sealed record DicomRecord(
        string PatientId,
        string PatientName,
        string StudyInstanceUid,
        string StudyDescription,
        DateTime? StudyDate,
        string SeriesInstanceUid,
        string SeriesDescription,
        ModalityType Modality,
        string SopInstanceUid,
        string FilePath,
        int InstanceNumber,
        int Width,
        int Height,
        int FrameCount,
        PixelSpacing PixelSpacing,
        WindowLevel WindowLevel);
}