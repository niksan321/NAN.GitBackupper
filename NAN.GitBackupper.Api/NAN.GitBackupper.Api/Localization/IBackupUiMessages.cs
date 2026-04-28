namespace NAN.GitBackupper.Api.Localization;

public interface IBackupUiMessages
{
    string BackupStepPreparing { get; }
    string BackupStepListingRepositories { get; }
    string BackupNoRepositories { get; }
    string BackupNoRepositoriesSelected { get; }
    string BackupStepCleaningDirectory { get; }
    string BackupCleaningProgress(int value, int maximum);
    string BackupProgress(int done, int total);
    string BackupStepCalculatingRepositorySizes { get; }
    string BackupCalculatingSizesProgress(int done, int total);
    string BackupFinishedOk(int ok, int total);
    string BackupFinishedWithErrors(int ok, int errorCount, string errorsPreview);
    string BackupStepCreatingZip { get; }
    string BackupZipCompressingProgress(int done, int total);
    string BackupZipBundling { get; }
    string BackupZipFailed(string message);
    string BackupCancelled { get; }
}
