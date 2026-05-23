namespace DicomViewer.Application.Models;

public sealed record WorklistItem(
    string OrderId,
    string PatientId,
    string PatientName,
    string ProcedureDescription,
    string BodyPart,
    string Projection,
    DateTime ScheduledTime,
    string Status);