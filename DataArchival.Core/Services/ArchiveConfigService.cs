using DataArchival.Core.Interfaces;
using DataArchival.Core.Models;
using Microsoft.Extensions.Logging;

namespace DataArchival.Core.Services;

public class ArchiveConfigService : IArchiveConfigService
{
    private readonly IConnectionService _connectionService;
    private readonly ILogger<ArchiveConfigService> _logger;

    public ArchiveConfigService(IConnectionService connectionService,
        ILogger<ArchiveConfigService> logger)
    {
        _connectionService = connectionService;
        _logger = logger;
    }

    public async Task<List<ArchiveConfig>> GetAllConfigsAsync()
    {
        try
        {
            await using var connection = await _connectionService.GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM archive_config ORDER BY table_name";

            var configs = new List<ArchiveConfig>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                configs.Add(new ArchiveConfig
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    TableName = reader.GetString(reader.GetOrdinal("table_name")),
                    ArchiveAfterDays = reader.GetInt32(reader.GetOrdinal("archive_after_days")),
                    DeleteAfterDays = reader.IsDBNull(reader.GetOrdinal("delete_after_days"))
                        ? null
                        : reader.GetInt32(reader.GetOrdinal("delete_after_days")),
                    IsEnabled = reader.GetBoolean(reader.GetOrdinal("is_enabled")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
                });
            }

            return configs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get archive configurations");
            throw;
        }
    }

    public async Task<ArchiveConfig?> GetConfigAsync(string tableName)
    {
        try
        {
            await using var connection = await _connectionService.GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM archive_config WHERE table_name = @tableName";
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@tableName", tableName));

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new ArchiveConfig
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    TableName = reader.GetString(reader.GetOrdinal("table_name")),
                    ArchiveAfterDays = reader.GetInt32(reader.GetOrdinal("archive_after_days")),
                    DeleteAfterDays = reader.IsDBNull(reader.GetOrdinal("delete_after_days"))
                        ? null
                        : reader.GetInt32(reader.GetOrdinal("delete_after_days")),
                    IsEnabled = reader.GetBoolean(reader.GetOrdinal("is_enabled")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get archive configuration for table {TableName}", tableName);
            throw;
        }
    }

    public async Task<ArchiveConfig> CreateConfigAsync(ArchiveConfig config)
    {
        try
        {
            await using var connection = await _connectionService.GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                    INSERT INTO archive_config (table_name, archive_after_days, delete_after_days, is_enabled)
                    VALUES (@tableName, @archiveAfterDays, @deleteAfterDays, @isEnabled)
                    RETURNING id, created_at, updated_at";

            command.Parameters.Add(new Npgsql.NpgsqlParameter("@tableName", config.TableName));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@archiveAfterDays", config.ArchiveAfterDays));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@deleteAfterDays",
                config.DeleteAfterDays ?? (object)DBNull.Value));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@isEnabled", config.IsEnabled));

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                config.Id = reader.GetInt32(reader.GetOrdinal("id"));
                config.CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"));
                config.UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"));
            }

            _logger.LogInformation("Created archive configuration for table {TableName}", config.TableName);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create archive configuration for table {TableName}", config.TableName);
            throw;
        }
    }

    public async Task<ArchiveConfig> UpdateConfigAsync(ArchiveConfig config)
    {
        try
        {
            await using var connection = await _connectionService.GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                    UPDATE archive_config 
                    SET archive_after_days = @archiveAfterDays, 
                        delete_after_days = @deleteAfterDays, 
                        is_enabled = @isEnabled,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE table_name = @tableName
                    RETURNING updated_at";

            command.Parameters.Add(new Npgsql.NpgsqlParameter("@tableName", config.TableName));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@archiveAfterDays", config.ArchiveAfterDays));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@deleteAfterDays",
                config.DeleteAfterDays ?? (object)DBNull.Value));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@isEnabled", config.IsEnabled));

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                config.UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"));
            }

            _logger.LogInformation("Updated archive configuration for table {TableName}", config.TableName);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update archive configuration for table {TableName}", config.TableName);
            throw;
        }
    }

    public async Task<bool> DeleteConfigAsync(string tableName)
    {
        try
        {
            await using var connection = await _connectionService.GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM archive_config WHERE table_name = @tableName";
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@tableName", tableName));

            var rowsAffected = await command.ExecuteNonQueryAsync();

            _logger.LogInformation("Deleted archive configuration for table {TableName}", tableName);
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete archive configuration for table {TableName}", tableName);
            throw;
        }
    }
}