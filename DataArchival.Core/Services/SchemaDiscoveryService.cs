using DataArchival.Core.Interfaces;
using DataArchival.Core.Models;
using Microsoft.Extensions.Logging;

namespace DataArchival.Core.Services;

public class SchemaDiscoveryService : ISchemaDiscoveryService
{
    private readonly IConnectionService _connectionService;
    private readonly ILogger<SchemaDiscoveryService> _logger;

    public SchemaDiscoveryService(IConnectionService connectionService, 
        ILogger<SchemaDiscoveryService> logger)
    {
        _connectionService = connectionService;
        _logger = logger;
    }

    public async Task<TableSchema> GetTableSchemaAsync(string tableName)
    {
        try
        {
            await using var connection = await _connectionService.GetSourceConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                    SELECT 
                        COLUMN_NAME,
                        DATA_TYPE,
                        IS_NULLABLE,
                        COLUMN_DEFAULT,
                        CHARACTER_MAXIMUM_LENGTH,
                        COLUMN_KEY
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName
                    ORDER BY ORDINAL_POSITION";

            command.Parameters.Add(new MySql.Data.MySqlClient.MySqlParameter("@tableName", tableName));

            var schema = new TableSchema { TableName = tableName };
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                schema.Columns.Add(new ColumnInfo
                {
                    ColumnName = reader.GetString(reader.GetOrdinal("COLUMN_NAME")),
                    DataType = reader.GetString(reader.GetOrdinal("DATA_TYPE")),
                    IsNullable = reader.GetString(reader.GetOrdinal("IS_NULLABLE")) == "YES",
                    IsPrimaryKey = reader.GetString(reader.GetOrdinal("COLUMN_KEY")) == "PRI",
                    DefaultValue = reader.IsDBNull(reader.GetOrdinal("COLUMN_DEFAULT"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("COLUMN_DEFAULT")),
                    MaxLength = reader.IsDBNull(reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH"))
                        ? null
                        : reader.GetInt32(reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH"))
                });
            }

            return schema;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schema for table {TableName}", tableName);
            throw;
        }
    }

    public async Task<bool> CreateArchiveTableAsync(TableSchema schema)
    {
        try
        {
            await using var connection = await _connectionService.GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();

            var columnDefinitions = schema.Columns.Select(col =>
            {
                var pgType = ConvertMySqlTypeToPostgreSQL(col.DataType, col.MaxLength);
                var nullable = col.IsNullable ? "" : "NOT NULL";
                var defaultValue = col.DefaultValue != null ? $"DEFAULT {col.DefaultValue}" : "";

                return $"{col.ColumnName} {pgType} {nullable} {defaultValue}".Trim();
            });

            var primaryKeys = schema.Columns.Where(c => c.IsPrimaryKey).Select(c => c.ColumnName);
            var primaryKeyConstraint = primaryKeys.Any() ? $", PRIMARY KEY ({string.Join(", ", primaryKeys)})" : "";

            command.CommandText = $@"
                    CREATE TABLE IF NOT EXISTS {schema.TableName} (
                        {string.Join(",\n                        ", columnDefinitions)}
                        {primaryKeyConstraint}
                    )";

            await command.ExecuteNonQueryAsync();

            // Create index on created_at if it exists
            if (schema.Columns.Any(c => c.ColumnName.Equals("created_at", StringComparison.OrdinalIgnoreCase)))
            {
                command.CommandText = $@"
                        CREATE INDEX IF NOT EXISTS idx_{schema.TableName}_created_at 
                        ON {schema.TableName} (created_at)";
                await command.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Created archive table for {TableName}", schema.TableName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create archive table for {TableName}", schema.TableName);
            throw;
        }
    }

    public async Task<bool> TableExistsInArchiveAsync(string tableName)
    {
        try
        {
            await using var connection = await _connectionService.GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                    SELECT COUNT(*) FROM information_schema.tables 
                    WHERE table_schema = 'public' AND table_name = @tableName";
            command.Parameters.Add(new Npgsql.NpgsqlParameter("@tableName", $"{tableName}"));

            var count = await command.ExecuteScalarAsync();
            return Convert.ToInt32(count) > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if archive table exists for {TableName}", tableName);
            throw;
        }
    }

    private static string ConvertMySqlTypeToPostgreSQL(string mysqlType, int? maxLength)
    {
        return mysqlType.ToLower() switch
        {
            "varchar" => maxLength.HasValue ? $"VARCHAR({maxLength})" : "VARCHAR(255)",
            "char" => maxLength.HasValue ? $"CHAR({maxLength})" : "CHAR(255)",
            "text" => "TEXT",
            "longtext" => "TEXT",
            "mediumtext" => "TEXT",
            "tinytext" => "TEXT",
            "int" => "INTEGER",
            "tinyint" => "SMALLINT",
            "smallint" => "SMALLINT",
            "mediumint" => "INTEGER",
            "bigint" => "BIGINT",
            "decimal" => "DECIMAL",
            "numeric" => "NUMERIC",
            "float" => "REAL",
            "double" => "DOUBLE PRECISION",
            "datetime" => "TIMESTAMP",
            "timestamp" => "TIMESTAMP",
            "date" => "DATE",
            "time" => "TIME",
            "year" => "INTEGER",
            "boolean" => "BOOLEAN",
            "bool" => "BOOLEAN",
            "json" => "JSON",
            "blob" => "BYTEA",
            "longblob" => "BYTEA",
            "mediumblob" => "BYTEA",
            "tinyblob" => "BYTEA",
            _ => "TEXT"
        };
    }
}