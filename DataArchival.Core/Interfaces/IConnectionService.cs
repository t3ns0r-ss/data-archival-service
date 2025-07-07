using System.Data.Common;

namespace DataArchival.Core.Interfaces;

public interface IConnectionService
{
    Task<DbConnection> GetSourceConnectionAsync();
    Task<DbConnection> GetArchiveConnectionAsync();
    Task InitializeArchiveDatabaseAsync();
}