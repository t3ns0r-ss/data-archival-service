using DataArchival.Core.Models;

namespace DataArchival.Core.Interfaces;

public interface IDataArchivalService
{
    Task<ArchiveLog> ArchiveTableDataAsync(string tableName);
    Task<List<ArchiveLog>> ArchiveAllConfiguredTablesAsync();
    Task<List<ArchiveLog>> GetArchiveLogsAsync(string? tableName = null);
}