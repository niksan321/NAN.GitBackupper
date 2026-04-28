using System.Text.Json;
using NAN.GitBackupper.Core.Enums;
using NAN.GitBackupper.Core.Models;

namespace NAN.GitBackupper.Core.Tests;

public sealed class TargetItemModelSerializationTests
{
    [Fact]
    public void RoundTrip_target_item_preserves_core_fields()
    {
        var original = new TargetItemModel
        {
            Id = Guid.Parse("a1b2c3d4-e5f6-4789-a012-3456789abcde"),
            Provider = GitProviderType.GitHub,
            Name = "test-profile",
            BackupRootPath = @"C:\backup\root",
            ApiKey = "secret",
            BackupInterval = new TimePeriod(TimePeriodType.Hours, 12),
            RepositoryBranches = new Dictionary<string, string> { ["o/r"] = "main" },
        };

        var json = JsonSerializer.Serialize(original);
        var copy = JsonSerializer.Deserialize<TargetItemModel>(json);

        Assert.NotNull(copy);
        Assert.Equal(original.Id, copy.Id);
        Assert.Equal(original.Provider, copy.Provider);
        Assert.Equal(original.Name, copy.Name);
        Assert.Equal(original.BackupRootPath, copy.BackupRootPath);
        Assert.Equal(original.ApiKey, copy.ApiKey);
        Assert.Equal(original.BackupInterval.Type, copy.BackupInterval.Type);
        Assert.Equal(original.BackupInterval.Value, copy.BackupInterval.Value);
        Assert.Equal("main", copy.RepositoryBranches["o/r"]);
    }
}
