using DataArchival.Core.Models;

namespace DataArchival.Core.Interfaces;

public interface IArchiveConfigService
{
    Task<List<ArchiveConfig>> GetAllConfigsAsync();
    Task<ArchiveConfig?> GetConfigAsync(string tableName);
    Task<ArchiveConfig> CreateConfigAsync(ArchiveConfig config);
    Task<ArchiveConfig> UpdateConfigAsync(ArchiveConfig config);
    Task<bool> DeleteConfigAsync(string tableName);
}