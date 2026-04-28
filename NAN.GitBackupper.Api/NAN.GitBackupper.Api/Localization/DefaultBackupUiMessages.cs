namespace NAN.GitBackupper.Api.Localization;

public sealed class DefaultBackupUiMessages : IBackupUiMessages
{
    public string BackupStepPreparing => "Preparing backup…";
    public string BackupStepListingRepositories => "Listing repositories…";
    public string BackupNoRepositories => "No repositories found.";
    public string BackupNoRepositoriesSelected => "No repositories selected for backup.";
    public string BackupStepCleaningDirectory => "Cleaning backup directory…";
    public string BackupCleaningProgress(int value, int maximum) => $"Cleaning: {value} / {maximum}";
    public string BackupProgress(int done, int total) => $"Progress: {done} / {total}";
    public string BackupStepCalculatingRepositorySizes => "Calculating repository sizes…";
    public string BackupCalculatingSizesProgress(int done, int total) => $"Sizes: {done} / {total}";
    public string BackupFinishedOk(int ok, int total) => $"Backup finished: {ok} / {total} repositories OK.";
    public string BackupFinishedWithErrors(int ok, int errorCount, string errorsPreview) =>
        $"Backup finished with errors. OK: {ok}, errors: {errorCount}. {errorsPreview}";
    public string BackupStepCreatingZip => "Creating ZIP archive…";
    public string BackupZipCompressingProgress(int done, int total) => $"Compressing repositories: {done} / {total}";
    public string BackupZipBundling => "Bundling repository archives (no compression)…";
    public string BackupZipFailed(string message) => $"ZIP failed: {message}";
    public string BackupCancelled => "Backup cancelled.";
}
