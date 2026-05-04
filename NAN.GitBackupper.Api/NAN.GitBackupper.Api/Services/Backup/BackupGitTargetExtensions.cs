using NAN.Git.Abstractions;
using NAN.GitBackupper.Api.Models;

namespace NAN.GitBackupper.Api.Services.Backup;

public static class BackupGitTargetExtensions
{
    public static IGitBackupTarget AsGitTarget(this BackupProfileModel model) => new BackupProfileGitTarget(model);
}