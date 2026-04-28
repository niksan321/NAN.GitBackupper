using System.Text.Json;

namespace NAN.GitBackupper.Api.Services.Backup;

/// <summary>Метаданные длительности полного бэкапа, рядом с файлом {name}.zip в виде {name}.zip.gbmeta.json.</summary>
public static class BackupArchiveDurationMeta
{
    public const string MetaFileSuffix = ".gbmeta.json";

    public static string GetMetaFileNameForZip(string zipFileName) => zipFileName + MetaFileSuffix;

    public static void TryWrite(string archivesDirectory, string zipFileName, long durationMs)
    {
        if (string.IsNullOrEmpty(archivesDirectory) || string.IsNullOrEmpty(zipFileName))
            return;
        try
        {
            var path = Path.Combine(archivesDirectory, GetMetaFileNameForZip(zipFileName));
            var json = JsonSerializer.Serialize(new MetaDto { DurationMs = durationMs }, SerializerOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // best effort
        }
    }

    public static long? TryRead(string archivesDirectory, string zipFileName)
    {
        if (string.IsNullOrEmpty(archivesDirectory) || string.IsNullOrEmpty(zipFileName))
            return null;
        var path = Path.Combine(archivesDirectory, GetMetaFileNameForZip(zipFileName));
        if (!File.Exists(path))
            return null;
        try
        {
            var text = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<MetaDto>(text, SerializerOptions);
            if (dto is not { DurationMs: >= 0 })
                return null;
            return dto.DurationMs;
        }
        catch
        {
            return null;
        }
    }

    public static void TryDeleteForZip(string archivesDirectory, string zipFileName)
    {
        if (string.IsNullOrEmpty(archivesDirectory) || string.IsNullOrEmpty(zipFileName))
            return;
        try
        {
            var path = Path.Combine(archivesDirectory, GetMetaFileNameForZip(zipFileName));
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private sealed class MetaDto
    {
        public long DurationMs { get; set; }
    }
}
