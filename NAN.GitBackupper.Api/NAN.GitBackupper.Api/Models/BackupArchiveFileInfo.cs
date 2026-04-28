namespace NAN.GitBackupper.Api.Models;

public sealed class BackupArchiveFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset ModifiedUtc { get; set; }

    /// <summary>Длительность полного запуска бэкапа, создавшего этот ZIP (мс), если рядом есть .gbmeta.json.</summary>
    public long? DurationMs { get; set; }
}
