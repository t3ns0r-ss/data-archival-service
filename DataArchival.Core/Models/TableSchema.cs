namespace DataArchival.Core.Models;

public class TableSchema
{
    public string TableName { get; set; } = string.Empty;
    public List<ColumnInfo> Columns { get; set; } = new();
}