using DataArchival.Core.Interfaces;
using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Npgsql;

namespace DataArchival.Core.Services;

public class ConnectionService : IConnectionService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConnectionService> _logger;

    public ConnectionService(IConfiguration configuration, 
        ILogger<ConnectionService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }
    
    public async Task<DbConnection> GetSourceConnectionAsync()
    {
        var connectionString = _configuration.GetConnectionString("SourceDatabase");
        var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<DbConnection> GetArchiveConnectionAsync()
    {
        var connectionString = _configuration.GetConnectionString("ArchiveDatabase");
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task InitializeArchiveDatabaseAsync()
    {
        try
        {
            await using var connection = await GetArchiveConnectionAsync();
            await using var command = connection.CreateCommand();

            // Create archive_config table
            command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS archive_config (
                        id SERIAL PRIMARY KEY,
                        table_name VARCHAR(255) NOT NULL UNIQUE,
                        archive_after_days INTEGER NOT NULL,
                        delete_after_days INTEGER,
                        is_enabled BOOLEAN DEFAULT true,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );";
            await command.ExecuteNonQueryAsync();

            // Create archive_log table
            command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS archive_log (
                        id SERIAL PRIMARY KEY,
                        table_name VARCHAR(255) NOT NULL,
                        records_archived INTEGER NOT NULL,
                        records_deleted INTEGER DEFAULT 0,
                        archive_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        status VARCHAR(50) NOT NULL,
                        error_message TEXT
                    );";
            await command.ExecuteNonQueryAsync();

            // Create user_roles table
            command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS user_roles (
                        id SERIAL PRIMARY KEY,
                        username VARCHAR(255) NOT NULL,
                        role VARCHAR(255) NOT NULL,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        UNIQUE(username, role)
                    );";
            await command.ExecuteNonQueryAsync();

            // Insert default admin user
            command.CommandText = @"
                    INSERT INTO user_roles (username, role) 
                    VALUES ('admin', 'admin') 
                    ON CONFLICT (username, role) DO NOTHING;";
            await command.ExecuteNonQueryAsync();

            _logger.LogInformation("Archive database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize archive database");
            throw;
        }
    }
}