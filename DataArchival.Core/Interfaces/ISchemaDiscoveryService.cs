using DataArchival.Core.Models;

namespace DataArchival.Core.Interfaces;

public interface ISchemaDiscoveryService
{
    Task<TableSchema> GetTableSchemaAsync(string tableName);
    Task<bool> CreateArchiveTableAsync(TableSchema schema);
    Task<bool> TableExistsInArchiveAsync(string tableName);
}