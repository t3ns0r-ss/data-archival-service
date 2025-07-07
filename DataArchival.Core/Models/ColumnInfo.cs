namespace DataArchival.Core.Models;

public class ColumnInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public string? DefaultValue { get; set; }
    public int? MaxLength { get; set; }
}