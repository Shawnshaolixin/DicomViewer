using System.Globalization;
using System.Text.Json;
using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;

namespace DicomViewer.Infrastructure.Persistence;

public sealed class SqliteConsoleConfigurationStore : IConsoleConfigurationStore
{
    private const string ConsoleConfigurationKey = "ConsoleConfiguration";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IAppDbConnectionFactory _connectionFactory;

    public SqliteConsoleConfigurationStore(IAppDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public ConsoleConfiguration Load()
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM AppSettings WHERE Key = $key LIMIT 1;";

        var keyParameter = command.CreateParameter();
        keyParameter.ParameterName = "$key";
        keyParameter.Value = ConsoleConfigurationKey;
        command.Parameters.Add(keyParameter);

        var result = command.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(result))
        {
            return ConsoleConfiguration.Default;
        }

        try
        {
            return JsonSerializer.Deserialize<ConsoleConfiguration>(result, SerializerOptions) ?? ConsoleConfiguration.Default;
        }
        catch
        {
            return ConsoleConfiguration.Default;
        }
    }

    public void Save(ConsoleConfiguration configuration)
    {
        var serializedConfiguration = JsonSerializer.Serialize(configuration, SerializerOptions);

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AppSettings (Key, Value, UpdatedAtUtc)
            VALUES ($key, $value, $updatedAtUtc)
            ON CONFLICT(Key) DO UPDATE SET
                Value = excluded.Value,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;

        var keyParameter = command.CreateParameter();
        keyParameter.ParameterName = "$key";
        keyParameter.Value = ConsoleConfigurationKey;
        command.Parameters.Add(keyParameter);

        var valueParameter = command.CreateParameter();
        valueParameter.ParameterName = "$value";
        valueParameter.Value = serializedConfiguration;
        command.Parameters.Add(valueParameter);

        var updatedAtUtcParameter = command.CreateParameter();
        updatedAtUtcParameter.ParameterName = "$updatedAtUtc";
        updatedAtUtcParameter.Value = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        command.Parameters.Add(updatedAtUtcParameter);

        command.ExecuteNonQuery();
    }
}