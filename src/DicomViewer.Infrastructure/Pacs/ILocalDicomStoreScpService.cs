namespace DicomViewer.Infrastructure.Pacs;

public interface ILocalDicomStoreScpService
{
    LocalDicomReceiveSession PrepareReceive(DicomViewer.Application.Models.PacsConfiguration configuration, string targetDirectory);
}