namespace zfs_tool.Models;

public class ZfsSnapshot
{
    public string Name { get; set; } = "";
    public DateTime Creation { get; set; } = DateTime.MinValue;
    public long WrittenBytes { get; set; } = 0;

}