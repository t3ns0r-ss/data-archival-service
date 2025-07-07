using System.Data;
using DataArchival.Core.Interfaces;
using DataArchival.Core.Models;
using Microsoft.Extensions.Logging;

namespace DataArchival.Core.Services;

public class DataArchivalService : IDataArchivalService
{
    private readonly IConnectionService _connectionService;
    private readonly IArchiveConfigService _configService;
    private readonly ISchemaDiscoveryService _schemaService;
    private readonly ILogger<DataArchivalService> _logger;

    public DataArchivalService(IConnectionService connectionService,
        IArchiveConfigService configService,
        ISchemaDiscoveryService schemaService,
        ILogger<DataArchivalService> logger)
    {
        _connectionService = connectionService;
        _configService = configService;
        _schemaService = schemaService;
        _logger = logger;
    }

    public async Task<ArchiveLog> ArchiveTableDataAsync(string tableName)
    {
        var log = new ArchiveLog
        {
            TableName = tableName,
            ArchiveDate = DateTime.UtcNow,
            Status = "Started"
        };

        try
        {
            // Get archive configuration
            var config = await _configService.GetConfigAsync(tableName);
            if (config == null || !config.IsEnabled)
            {
                log.Status = "Skipped";
                log.ErrorMessage = "No configuration found or disabled";
                await LogArchiveOperationAsync(log);
                return log;
            }

            // Get table schema
            var schema = await _schemaService.GetTableSchemaAsync(tableName);
            if (!schema.Columns.Any())
            {
                log.Status = "Failed";
                log.ErrorMessage = "Table not found or no columns";
                await LogArchiveOperationAsync(log);
                return log;
            }

            // Check if table has created_at column
            var createdAtColumn = schema.Columns.FirstOrDefault(c =>
                c.ColumnName.Equals("created_at", StringComparison.OrdinalIgnoreCase));
            if (createdAtColumn == null)
            {
                log.Status = "Failed";
                log.ErrorMessage = "Table does not have created_at column";
                await LogArchiveOperationAsync(log);
                return log;
            }

            // Ensure archive table exists
            if (!await _schemaService.TableExistsInArchiveAsync(tableName))
            {
                await _schemaService.CreateArchiveTableAsync(schema);
            }

            // Calculate archive and delete dates
            var archiveDate = DateTime.UtcNow.AddDays(-config.ArchiveAfterDays);
            var deleteDate = config.DeleteAfterDays.HasValue
                ? DateTime.UtcNow.AddDays(-config.DeleteAfterDays.Value)
                : (DateTime?)null;

            // Archive data
            var archivedCount = await ArchiveDataAsync(tableName, schema, archiveDate);
            log.RecordsArchived = archivedCount;

            // Delete old archived data if configured
            if (deleteDate.HasValue)
            {
                var deletedCount = await DeleteOldArchivedDataAsync(tableName, deleteDate.Value);
                log.RecordsDeleted = deletedCount;
            }

            log.Status = "Completed";
            _logger.LogInformation("Archived {Count} records from table {TableName}", archivedCount, tableName);
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            log.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to archive data from table {TableName}", tableName);
        }

        await LogArchiveOperationAsync(log);
        return log;
    }

    public async Task<List<ArchiveLog>> ArchiveAllConfiguredTablesAsync()
    {
        var logs = new List<ArchiveLog>();

        try
        {
            var configs = await _configService.GetAllConfigsAsync();
            var enabledConfigs = configs.Where(c => c.IsEnabled).ToList();

            _logger.LogInformation("Starting archival process for {Count} tables", enabledConfigs.Count);

            foreach (var config in enabledConfigs)
            {
                var log = await ArchiveTableDataAsync(config.TableName);
                logs.Add(log);
            }

            _logger.LogInformation("Completed archival process for all tables");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive all configured tables");
            throw;
        }

        return logs;
    }

    public async Task<List<ArchiveLog>> GetArchiveLogsAsync(string? tableName = null)
    {
        try
        {
            await using var connection = await _connectionService.GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();

            var whereClause = tableName != null ? "WHERE table_name = @tableName" : "";
            command.CommandText = $"""
                                   SELECT * FROM archive_log 
                                   {whereClause}
                                   ORDER BY archive_date DESC 
                                   LIMIT 100
                                   """;

            if (tableName != null)
            {
                command.Parameters.Add(new Npgsql.NpgsqlParameter("@tableName", tableName));
            }

            var logs = new List<ArchiveLog>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                logs.Add(new ArchiveLog
                {
                    Id = reader.GetInt32("id"),
                    TableName = reader.GetString("table_name"),
                    RecordsArchived = reader.GetInt32("records_archived"),
                    RecordsDeleted = reader.GetInt32("records_deleted"),
                    ArchiveDate = reader.GetDateTime("archive_date"),
                    Status = reader.GetString("status"),
                    ErrorMessage = await reader.IsDBNullAsync("error_message") ? null : reader.GetString("error_message")
                });
            }

            return logs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get archive logs");
            throw;
        }
    }

    private async Task<int> ArchiveDataAsync(string tableName, TableSchema schema, DateTime archiveDate)
    {
        const int batchSize = 1000;
        var totalArchived = 0;

        await using var sourceConnection = await _connectionService.GetSourceConnectionAsync();
        await using var archiveConnection = await _connectionService.GetArchiveConnectionAsync();

        // Get total count of records to archive
        await using var countCommand = sourceConnection.CreateCommand();
        countCommand.CommandText = $"SELECT COUNT(*) FROM {tableName} WHERE created_at < @archiveDate";
        countCommand.Parameters.Add(new MySql.Data.MySqlClient.MySqlParameter("@archiveDate", archiveDate));

        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
        if (totalCount == 0)
        {
            return 0;
        }

        _logger.LogInformation("Found {Count} records to archive from table {TableName}", totalCount, tableName);

        // Archive data in batches
        var offset = 0;
        while (offset < totalCount)
        {
            await using var transaction = await archiveConnection.BeginTransactionAsync();
            try
            {
                // Select batch of data to archive
                await using var selectCommand = sourceConnection.CreateCommand();
                selectCommand.CommandText = $"""
                                             SELECT * FROM {tableName} 
                                             WHERE created_at < @archiveDate 
                                             ORDER BY created_at 
                                             LIMIT {batchSize} OFFSET {offset}
                                             """;
                selectCommand.Parameters.Add(new MySql.Data.MySqlClient.MySqlParameter("@archiveDate", archiveDate));

                var dataTable = new DataTable();
                using var adapter = new MySql.Data.MySqlClient.MySqlDataAdapter((MySql.Data.MySqlClient.MySqlCommand)selectCommand);
                adapter.Fill(dataTable);

                if (dataTable.Rows.Count == 0)
                    break;

                // Insert into archive table
                var insertQuery = BuildInsertQuery(tableName, schema);
                await using var insertCommand = archiveConnection.CreateCommand();
                insertCommand.Transaction = transaction;
                insertCommand.CommandText = insertQuery;

                var batchArchived = 0;
                foreach (DataRow row in dataTable.Rows)
                {
                    insertCommand.Parameters.Clear();
                    foreach (var column in schema.Columns)
                    {
                        var value = row[column.ColumnName];
                        if (value is ulong)
                            insertCommand.Parameters.Add(new Npgsql.NpgsqlParameter($"@{column.ColumnName}", 
                                Convert.ToInt64(value)));
                        else
                            insertCommand.Parameters.Add(new Npgsql.NpgsqlParameter($"@{column.ColumnName}", 
                                value ?? DBNull.Value));
                    }

                    await insertCommand.ExecuteNonQueryAsync();
                    batchArchived++;
                }

                // Delete from source table
                var primaryKeyColumns = schema.Columns.Where(c => c.IsPrimaryKey).ToList();
                if (primaryKeyColumns.Any())
                {
                    var deleteQuery = BuildDeleteQuery(tableName, primaryKeyColumns);
                    await using var deleteCommand = sourceConnection.CreateCommand();
                    deleteCommand.CommandText = deleteQuery;

                    foreach (DataRow row in dataTable.Rows)
                    {
                        deleteCommand.Parameters.Clear();
                        foreach (var pkColumn in primaryKeyColumns)
                        {
                            deleteCommand.Parameters.Add(
                                new MySql.Data.MySqlClient.MySqlParameter($"@{pkColumn.ColumnName}",
                                    row[pkColumn.ColumnName]));
                        }

                        await deleteCommand.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();
                totalArchived += batchArchived;
                offset += batchSize;

                _logger.LogDebug("Archived batch of {Count} records from table {TableName}", batchArchived, tableName);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to archive batch starting at offset {Offset} for table {TableName}",
                    offset, tableName);
                throw;
            }
        }

        return totalArchived;
    }

    private async Task<int> DeleteOldArchivedDataAsync(string tableName, DateTime deleteDate)
    {
        try
        {
            await using var connection = await _connectionService.GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                                   DELETE FROM {tableName} 
                                   WHERE created_at < @deleteDate
                                   """;
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@deleteDate", deleteDate));

            var deletedCount = await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Deleted {Count} old archived records from table {TableName}", deletedCount,
                tableName);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete old archived data from table {TableName}", tableName);
            throw;
        }
    }

    private static string BuildInsertQuery(string tableName, TableSchema schema)
    {
        var columns = string.Join(", ", schema.Columns.Select(c => c.ColumnName));
        var parameters = string.Join(", ", schema.Columns.Select(c => $"@{c.ColumnName}"));
        return $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";
    }

    private static string BuildDeleteQuery(string tableName, List<ColumnInfo> primaryKeyColumns)
    {
        var whereClause = string.Join(" AND ", primaryKeyColumns.Select(c => $"{c.ColumnName} = @{c.ColumnName}"));
        return $"DELETE FROM {tableName} WHERE {whereClause}";
    }

    private async Task LogArchiveOperationAsync(ArchiveLog log)
    {
        try
        {
            await using var connection = await _connectionService.GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                                  INSERT INTO archive_log (table_name, records_archived, records_deleted, archive_date, status, error_message)
                                  VALUES (@tableName, @recordsArchived, @recordsDeleted, @archiveDate, @status, @errorMessage)
                                  """;

            command.Parameters.Add(new Npgsql.NpgsqlParameter("@tableName", log.TableName));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@recordsArchived", log.RecordsArchived));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@recordsDeleted", log.RecordsDeleted));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@archiveDate", log.ArchiveDate));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@status", log.Status));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@errorMessage", log.ErrorMessage ?? (object)DBNull.Value));

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log archive operation for table {TableName}", log.TableName);
        }
    }
}