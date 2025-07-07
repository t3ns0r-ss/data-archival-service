namespace DataArchival.Core.Interfaces;

public interface IArchiveDataService
{
    Task<List<Dictionary<string, object>>> GetArchivedDataAsync(string tableName, int page = 1, int pageSize = 50);
    Task<int> GetArchivedDataCountAsync(string tableName);
}