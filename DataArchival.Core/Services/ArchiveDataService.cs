using DataArchival.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DataArchival.Core.Services;

public class ArchiveDataService : IArchiveDataService
{
    private readonly IConnectionService _connectionService;
    private readonly ILogger<ArchiveDataService> _logger;

    public ArchiveDataService(IConnectionService connectionService, 
        ILogger<ArchiveDataService> logger)
    {
        _connectionService = connectionService;
        _logger = logger;
    }

    public async Task<List<Dictionary<string, object>>> GetArchivedDataAsync(string tableName, int page = 1, int pageSize = 50)
    {
        try
        {
            await using var connection = await _connectionService.GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();

            var offset = (page - 1) * pageSize;
            command.CommandText = $@"
                    SELECT * FROM {tableName} 
                    ORDER BY created_at DESC 
                    LIMIT @pageSize OFFSET @offset";

            command.Parameters.Add(new Npgsql.NpgsqlParameter("@pageSize", pageSize));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@offset", offset));

            var results = new List<Dictionary<string, object>>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                results.Add(row);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get archived data for table {TableName}", tableName);
            throw;
        }
    }

    public async Task<int> GetArchivedDataCountAsync(string tableName)
    {
        try
        {
            await using var connection = await _connectionService.GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {tableName}";

            var count = await command.ExecuteScalarAsync();
            return Convert.ToInt32(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get archived data count for table {TableName}", tableName);
            throw;
        }
    }
}