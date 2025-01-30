namespace zfs_tool.Enums;
[Flags]
public enum ExtraZfsProperties
{
    None = 1 << 0,
    Reclaim = 1 << 1,
    ReclaimSum = 1 << 2,
    All = int.MaxValue
}