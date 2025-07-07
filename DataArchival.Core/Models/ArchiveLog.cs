namespace DataArchival.Core.Models;

public class ArchiveLog
{
    public int Id { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int RecordsArchived { get; set; }
    public int RecordsDeleted { get; set; }
    public DateTime ArchiveDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}