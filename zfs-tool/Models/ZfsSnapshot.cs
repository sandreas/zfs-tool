using NeoSmart.PrettySize;

namespace zfs_tool.Models;

public class ZfsSnapshot
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public DateTime Creation { get; set; } = DateTime.MinValue;

    public string Written => PrettySize.Bytes(WrittenBytes).Format(UnitBase.Base10, UnitStyle.Abbreviated);
    public string Reclaim => PrettySize.Bytes(ReclaimBytes).Format(UnitBase.Base10, UnitStyle.Abbreviated);
    public string ReclaimSum => PrettySize.Bytes(ReclaimSumBytes).Format(UnitBase.Base10, UnitStyle.Abbreviated);
    public long WrittenBytes { get; set; } = 0;
    public long ReclaimBytes { get; set; } = 0;
    public long ReclaimSumBytes { get; set; } = 0;

    
    

}