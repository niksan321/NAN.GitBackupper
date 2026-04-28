namespace NAN.GitBackupper.Api.Helpers;

public static class DirectorySizeHelper
{
    public static long ComputeDirectorySizeBytes(string rootPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return 0;

        long sum = 0;
        foreach (var filePath in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                sum += new FileInfo(filePath).Length;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return sum;
    }
}
