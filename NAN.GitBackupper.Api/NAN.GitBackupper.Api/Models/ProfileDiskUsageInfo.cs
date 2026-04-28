namespace NAN.GitBackupper.Api.Models;

public sealed class ProfileDiskUsageInfo
{
    public long TotalBytes { get; set; }

    public long FreeBytes { get; set; }

    public long UsedBytes { get; set; }

    public int DiskUsagePercent { get; set; }
}
