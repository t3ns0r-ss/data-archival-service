namespace DataArchival.Core.Models;

public class ArchiveConfig
{
    public int Id { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int ArchiveAfterDays { get; set; }
    public int? DeleteAfterDays { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}