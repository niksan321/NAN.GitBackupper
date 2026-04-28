namespace NAN.GitBackupper.Core.Services;

public sealed class BackupDirectoryPruneService
{
    private const int DeleteAttempts = 3;

    public Task PruneAsync(string backupRoot, IReadOnlySet<string> allowedFolderNames,
        IProgress<(int Value, int Maximum)> countProgress, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(allowedFolderNames);
        return Task.Run(() =>
        {
            var root = Path.GetFullPath(backupRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!Directory.Exists(root))
                return;

            List<string> toRemove = [];
            foreach (var dir in Directory.GetDirectories(root))
            {
                ct.ThrowIfCancellationRequested();
                var name = Path.GetFileName(dir);
                if (!allowedFolderNames.Contains(name))
                    toRemove.Add(dir);
            }

            if (toRemove.Count == 0)
                return;

            List<string> allFiles = [];
            foreach (var top in toRemove)
                allFiles.AddRange(Directory.EnumerateFiles(top, "*", SearchOption.AllDirectories));

            var totalFiles = allFiles.Count;
            var maximum = Math.Max(1, totalFiles);
            var done = 0;

            foreach (var file in allFiles.OrderByDescending(PathSegmentDepth))
            {
                ct.ThrowIfCancellationRequested();
                TryDeleteFileBestEffort(file, ct);
                done++;
                countProgress?.Report((done, maximum));
            }

            foreach (var top in toRemove.OrderByDescending(t => t.Length))
            {
                ct.ThrowIfCancellationRequested();
                if (!Directory.Exists(top))
                    continue;
                TryDeleteDirectoryTreeBestEffort(top, ct);
            }
        }, ct);
    }

    /// <summary>Больше сегментов пути — глубже вложенность; удаляем сначала глубокие файлы.</summary>
    private static int PathSegmentDepth(string path)
    {
        var n = 0;
        foreach (var c in path)
        {
            if (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar)
                n++;
        }

        return n;
    }

    private static void TryDeleteFileBestEffort(string path, CancellationToken ct)
    {
        for (var attempt = 0; attempt < DeleteAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!File.Exists(path))
                    return;
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                Thread.Sleep(50 * (attempt + 1));
            }
        }
    }

    private static void TryDeleteDirectoryTreeBestEffort(string top, CancellationToken ct)
    {
        try
        {
            if (!Directory.Exists(top))
                return;

            foreach (var file in Directory.EnumerateFiles(top, "*", SearchOption.AllDirectories)
                         .OrderByDescending(PathSegmentDepth))
            {
                ct.ThrowIfCancellationRequested();
                TryDeleteFileBestEffort(file, ct);
            }

            ClearDirectoryAttributesRecursive(top);
            TryClearDirectoryReadOnly(top);

            for (var attempt = 0; attempt < DeleteAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (!Directory.Exists(top))
                        return;
                    Directory.Delete(top, true);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    foreach (var file in Directory.EnumerateFiles(top, "*", SearchOption.AllDirectories)
                                 .OrderByDescending(PathSegmentDepth))
                    {
                        ct.ThrowIfCancellationRequested();
                        TryDeleteFileBestEffort(file, ct);
                    }

                    ClearDirectoryAttributesRecursive(top);
                    TryClearDirectoryReadOnly(top);
                    Thread.Sleep(50 * (attempt + 1));
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // ignore — backup continues
        }
    }

    private static void ClearDirectoryAttributesRecursive(string root)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                catch
                {
                    // ignore
                }
            }

            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                         .OrderByDescending(d => d.Length))
            {
                try
                {
                    var di = new DirectoryInfo(dir);
                    if (di.Exists)
                        di.Attributes &= ~FileAttributes.ReadOnly;
                }
                catch
                {
                    // ignore
                }
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void TryClearDirectoryReadOnly(string directoryPath)
    {
        try
        {
            var di = new DirectoryInfo(directoryPath);
            if (di.Exists)
                di.Attributes &= ~FileAttributes.ReadOnly;
        }
        catch
        {
            // ignore
        }
    }
}
