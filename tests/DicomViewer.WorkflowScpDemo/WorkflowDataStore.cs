using System.Text.Json;

namespace DicomViewer.WorkflowScpDemo;

internal sealed class WorkflowDataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly Lock _syncRoot = new();
    private readonly string _filePath;

    private WorkflowDataStore(string filePath)
    {
        _filePath = filePath;
    }

    public static WorkflowDataStore Create(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var store = new WorkflowDataStore(fullPath);
        store.EnsureInitialized();
        return store;
    }

    public IReadOnlyList<WorklistItemRecord> QueryWorklist(WorklistQueryCriteria criteria)
    {
        lock (_syncRoot)
        {
            return LoadInternal().WorklistItems
                .Where(item => !string.Equals(item.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                .Where(item => !string.Equals(item.Status, "DISCONTINUED", StringComparison.OrdinalIgnoreCase))
                .Where(item => Matches(item.PatientId, criteria.PatientId))
                .Where(item => Matches(item.PatientName, criteria.PatientName))
                .Where(item => Matches(item.AccessionNumber, criteria.AccessionNumber))
                .Where(item => Matches(item.Modality, criteria.Modality))
                .Where(item => Matches(item.ScheduledStationAeTitle, criteria.ScheduledStationAeTitle))
                .Where(item => MatchesDateRange(item.ScheduledStart, criteria.ScheduledDateRange))
                .OrderBy(item => item.ScheduledStart)
                .ToArray();
        }
    }

    public MppsUpsertResult CreateMpps(
        string sopInstanceUid,
        string performedProcedureStepId,
        string scheduledProcedureStepId,
        string accessionNumber,
        string studyInstanceUid,
        string patientId,
        string patientName)
    {
        lock (_syncRoot)
        {
            var data = LoadInternal();
            if (data.MppsRecords.Any(record => string.Equals(record.SopInstanceUid, sopInstanceUid, StringComparison.OrdinalIgnoreCase)))
            {
                return new MppsUpsertResult(false, null, null, "SOP Instance UID 已存在。");
            }

            var worklistItem = FindWorklistItem(data, scheduledProcedureStepId, accessionNumber, studyInstanceUid);
            if (worklistItem is null)
            {
                return new MppsUpsertResult(false, null, null, "未找到匹配的本地 worklist。");
            }

            worklistItem.Status = "IN PROGRESS";
            var record = new MppsRecord
            {
                SopInstanceUid = sopInstanceUid,
                PerformedProcedureStepId = performedProcedureStepId,
                ScheduledProcedureStepId = worklistItem.ScheduledProcedureStepId,
                AccessionNumber = worklistItem.AccessionNumber,
                StudyInstanceUid = worklistItem.StudyInstanceUid,
                PatientId = string.IsNullOrWhiteSpace(patientId) ? worklistItem.PatientId : patientId,
                PatientName = string.IsNullOrWhiteSpace(patientName) ? worklistItem.PatientName : patientName,
                Status = "IN PROGRESS",
                UpdatedAtUtc = DateTime.UtcNow,
            };

            data.MppsRecords.Add(record);
            SaveInternal(data);
            return new MppsUpsertResult(true, worklistItem, record, "MPPS N-CREATE 已记录。");
        }
    }

    public MppsUpsertResult UpdateMpps(string sopInstanceUid, string status, string? comments)
    {
        lock (_syncRoot)
        {
            var data = LoadInternal();
            var record = data.MppsRecords.FirstOrDefault(item => string.Equals(item.SopInstanceUid, sopInstanceUid, StringComparison.OrdinalIgnoreCase));
            if (record is null)
            {
                return new MppsUpsertResult(false, null, null, "未找到对应的 MPPS 实例。");
            }

            record.Status = status;
            record.Comments = string.IsNullOrWhiteSpace(comments) ? null : comments;
            record.UpdatedAtUtc = DateTime.UtcNow;

            var worklistItem = FindWorklistItem(data, record.ScheduledProcedureStepId, record.AccessionNumber, record.StudyInstanceUid);
            if (worklistItem is not null)
            {
                worklistItem.Status = status;
            }

            SaveInternal(data);
            return new MppsUpsertResult(true, worklistItem, record, "MPPS N-SET 已记录。");
        }
    }

    private void EnsureInitialized()
    {
        lock (_syncRoot)
        {
            if (File.Exists(_filePath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            SaveInternal(CreateSampleData());
        }
    }

    private WorkflowDataFile LoadInternal()
    {
        using var stream = File.OpenRead(_filePath);
        return JsonSerializer.Deserialize<WorkflowDataFile>(stream, JsonOptions) ?? new WorkflowDataFile();
    }

    private void SaveInternal(WorkflowDataFile data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        using var stream = File.Create(_filePath);
        JsonSerializer.Serialize(stream, data, JsonOptions);
    }

    private static WorklistItemRecord? FindWorklistItem(WorkflowDataFile data, string scheduledProcedureStepId, string accessionNumber, string studyInstanceUid)
    {
        if (!string.IsNullOrWhiteSpace(scheduledProcedureStepId))
        {
            var byStepId = data.WorklistItems.FirstOrDefault(item => string.Equals(item.ScheduledProcedureStepId, scheduledProcedureStepId, StringComparison.OrdinalIgnoreCase));
            if (byStepId is not null)
            {
                return byStepId;
            }
        }

        if (!string.IsNullOrWhiteSpace(accessionNumber))
        {
            var byAccession = data.WorklistItems.FirstOrDefault(item => string.Equals(item.AccessionNumber, accessionNumber, StringComparison.OrdinalIgnoreCase));
            if (byAccession is not null)
            {
                return byAccession;
            }
        }

        if (!string.IsNullOrWhiteSpace(studyInstanceUid))
        {
            return data.WorklistItems.FirstOrDefault(item => string.Equals(item.StudyInstanceUid, studyInstanceUid, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static bool Matches(string source, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return source.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesDateRange(DateTime scheduledStart, string dateRange)
    {
        if (string.IsNullOrWhiteSpace(dateRange))
        {
            return true;
        }

        var parts = dateRange.Split('-', 2, StringSplitOptions.TrimEntries);
        var from = ParseDicomDate(parts[0]);
        var to = parts.Length == 2 ? ParseDicomDate(parts[1]) : from;
        if (from is null && to is null)
        {
            return true;
        }

        var scheduledDate = DateOnly.FromDateTime(scheduledStart);
        return (from is null || scheduledDate >= from.Value)
            && (to is null || scheduledDate <= to.Value);
    }

    private static DateOnly? ParseDicomDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParseExact(value, "yyyyMMdd", out var parsed) ? parsed : null;
    }

    private static WorkflowDataFile CreateSampleData()
    {
        return new WorkflowDataFile
        {
            WorklistItems =
            [
                new WorklistItemRecord
                {
                    OrderId = "ORDER-001",
                    PatientId = "P10001",
                    PatientName = "ZHANG^SAN",
                    AccessionNumber = "ACC-20260605-001",
                    RequestedProcedureId = "RP-001",
                    ScheduledProcedureStepId = "SPS-001",
                    StudyInstanceUid = "1.2.826.0.1.3680043.10.543.1.1.1",
                    Modality = "DX",
                    ScheduledStationAeTitle = "DICOMVIEWER",
                    ScheduledProcedureStepDescription = "Chest PA",
                    RequestedProcedureDescription = "Chest X-Ray",
                    ReferringPhysicianName = "LI^DOCTOR",
                    PatientSex = "M",
                    PatientBirthDate = "19881001",
                    Status = "SCHEDULED",
                    ScheduledStart = new DateTime(2026, 6, 5, 9, 0, 0, DateTimeKind.Local),
                },
                new WorklistItemRecord
                {
                    OrderId = "ORDER-002",
                    PatientId = "P10002",
                    PatientName = "LI^SI",
                    AccessionNumber = "ACC-20260605-002",
                    RequestedProcedureId = "RP-002",
                    ScheduledProcedureStepId = "SPS-002",
                    StudyInstanceUid = "1.2.826.0.1.3680043.10.543.1.1.2",
                    Modality = "CT",
                    ScheduledStationAeTitle = "DICOMVIEWER",
                    ScheduledProcedureStepDescription = "Head Plain",
                    RequestedProcedureDescription = "Head CT",
                    ReferringPhysicianName = "WANG^DOCTOR",
                    PatientSex = "F",
                    PatientBirthDate = "19920512",
                    Status = "SCHEDULED",
                    ScheduledStart = new DateTime(2026, 6, 5, 10, 0, 0, DateTimeKind.Local),
                },
            ],
        };
    }
}
