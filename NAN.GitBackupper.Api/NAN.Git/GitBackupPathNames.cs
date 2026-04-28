namespace NAN.Git;

public static class GitBackupPathNames
{
    /// <summary>
    /// Имя подкаталога в корне бекапа для репозитория с данным DisplayKey.
    /// </summary>
    public static string FolderNameFromDisplayKey(string displayKey) =>
        SanitizePathSegment(displayKey.Replace('/', '_'));

    private static string SanitizePathSegment(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}