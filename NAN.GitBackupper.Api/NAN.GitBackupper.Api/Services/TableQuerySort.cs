using NAN.Git.Abstractions;
using NAN.GitBackupper.Api.Models;

namespace NAN.GitBackupper.Api.Services;

/// <summary>Разбор order=asc|desc и сортировка списков для query-параметров sort/order.</summary>
public static class TableQuerySort
{
    public static bool IsAscending(string? order) =>
        string.IsNullOrWhiteSpace(order) ||
        string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase);

    /// <summary>Сортирует профили in-place. Неизвестный sort — без изменений.</summary>
    public static void SortTargetItems(List<BackupProfileModel>? items, string? sort, string? order)
    {
        if (items is not { Count: > 1 } || string.IsNullOrWhiteSpace(sort))
            return;

        var asc = IsAscending(order);
        var key = sort.Trim();
        IOrderedEnumerable<BackupProfileModel>? ordered = key switch
        {
            "name" => asc
                ? items.OrderBy(t => t.Name ?? "", StringComparer.CurrentCulture)
                : items.OrderByDescending(t => t.Name ?? "", StringComparer.CurrentCulture),
            "provider" => asc
                ? items.OrderBy(t => t.Provider)
                : items.OrderByDescending(t => t.Provider),
            "backupInterval" => asc
                ? items.OrderBy(t => t.BackupInterval?.ToTimeSpan() ?? TimeSpan.MaxValue)
                : items.OrderByDescending(t => t.BackupInterval?.ToTimeSpan() ?? TimeSpan.MinValue),
            "parallelism" => asc
                ? items.OrderBy(t => t.RepositoryBackupParallelism)
                : items.OrderByDescending(t => t.RepositoryBackupParallelism),
            "lastBackupSizes" => asc
                ? items.OrderBy(t => t.LastBackupSelectedTotalBytes ?? long.MaxValue)
                    .ThenBy(t => t.LastBackupZipArchiveBytes ?? long.MaxValue)
                : items.OrderByDescending(t => t.LastBackupSelectedTotalBytes ?? long.MinValue)
                    .ThenByDescending(t => t.LastBackupZipArchiveBytes ?? long.MinValue),
            "lastBackupDuration" => asc
                ? items.OrderBy(t => t.LastBackupDurationMs ?? long.MaxValue)
                : items.OrderByDescending(t => t.LastBackupDurationMs ?? long.MinValue),
            _ => null
        };
        if (ordered == null)
            return;
        var sorted = ordered.ThenBy(t => t.Id).ToList();
        items.Clear();
        items.AddRange(sorted);
    }

    public static void SortBackupArchives(List<BackupArchiveFileInfo> list, string? sort, string? order)
    {
        if (list.Count < 2)
            return;
        var asc = IsAscending(order);
        var key = string.IsNullOrWhiteSpace(sort) ? "modifiedUtc" : sort.Trim();
        if (key == "durationMs")
        {
            list.Sort((a, b) => CompareArchiveDuration(a, b, asc));
            return;
        }

        Comparison<BackupArchiveFileInfo> cmp = key switch
        {
            "fileName" => (a, b) => string.Compare(a.FileName, b.FileName, StringComparison.CurrentCulture),
            "sizeBytes" => (a, b) => a.SizeBytes.CompareTo(b.SizeBytes),
            "modifiedUtc" => (a, b) => a.ModifiedUtc.CompareTo(b.ModifiedUtc),
            _ => (a, b) => a.ModifiedUtc.CompareTo(b.ModifiedUtc)
        };
        if (!asc)
        {
            var inner = cmp;
            cmp = (a, b) => inner(b, a);
        }
        list.Sort(cmp);
    }

    /// <summary>Null duration всегда в конце, затем сравнение длительности по sort order.</summary>
    private static int CompareArchiveDuration(BackupArchiveFileInfo a, BackupArchiveFileInfo b, bool ascending)
    {
        var da = a.DurationMs;
        var db = b.DurationMs;
        if (da == null && db == null) return 0;
        if (da == null) return 1;
        if (db == null) return -1;
        var c = da.Value.CompareTo(db.Value);
        return ascending ? c : -c;
    }

    public static void SortRepositoryDescriptors(
        List<GitRepositoryDescriptor> list,
        Dictionary<string, long> sizesByKey,
        Dictionary<string, string> branches,
        string? sort,
        string? order)
    {
        if (list.Count < 2 || string.IsNullOrWhiteSpace(sort))
            return;
        var asc = IsAscending(order);
        var key = sort.Trim();
        long SizeKey(string displayKey) =>
            sizesByKey.TryGetValue(displayKey, out var b) ? b : (asc ? long.MaxValue : long.MinValue);
        string BranchKey(GitRepositoryDescriptor r)
        {
            if (branches.TryGetValue(r.DisplayKey, out var br) && !string.IsNullOrWhiteSpace(br))
                return br.Trim();
            return (r.DefaultBranch ?? "").Trim();
        }
        Comparison<GitRepositoryDescriptor> cmp = key switch
        {
            "displayKey" => (a, b) => string.Compare(a.DisplayKey, b.DisplayKey, StringComparison.CurrentCulture),
            "httpsCloneUrl" => (a, b) => string.Compare(a.HttpsCloneUrl, b.HttpsCloneUrl, StringComparison.CurrentCulture),
            "size" => (a, b) => SizeKey(a.DisplayKey).CompareTo(SizeKey(b.DisplayKey)),
            "branch" => (a, b) => string.Compare(BranchKey(a), BranchKey(b), StringComparison.CurrentCulture),
            _ => (a, b) => string.Compare(a.DisplayKey, b.DisplayKey, StringComparison.CurrentCulture)
        };
        if (!asc)
        {
            var inner = cmp;
            cmp = (a, b) => inner(b, a);
        }
        list.Sort(cmp);
    }
}
