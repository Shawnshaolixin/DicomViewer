using System.Data.Common;

namespace DicomViewer.Application.Abstractions;

public interface IAppDbConnectionFactory
{
    string DatabasePath { get; }

    DbConnection CreateConnection();
}