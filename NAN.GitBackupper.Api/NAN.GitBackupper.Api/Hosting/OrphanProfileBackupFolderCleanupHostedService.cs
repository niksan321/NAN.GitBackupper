using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NAN.GitBackupper.Api.Database;
using NAN.GitBackupper.Api.Options;
using NAN.GitBackupper.Api.Services;
using NAN.GitBackupper.Api.Services.Backup;

namespace NAN.GitBackupper.Api.Hosting;

public sealed class OrphanProfileBackupFolderCleanupHostedService(
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<GitBackupperOptions> optionsAccessor,
    ILogger<OrphanProfileBackupFolderCleanupHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!optionsAccessor.Value.OrphanProfileBackupFolderCleanupEnabled) return;
        if (!ProfileBackupRootHelper.IsRootConfigured(optionsAccessor.Value)) return;

        var intervalHrs = Math.Max(1, optionsAccessor.Value.OrphanProfileBackupCleanupIntervalHours);
        var root = ProfileBackupRootHelper.GetConfiguredRoot(optionsAccessor.Value);
        logger.LogInformation("Запуск стартовой очистки осиротевших каталогов бэкапа.");
        try
        {
            await OrphanProfileBackupDirectoryCleanup.RunAsync(
                root, dbFactory, logger, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Стартовая очистка осиротевших каталогов бэкапа завершилась с ошибкой.");
        }

        logger.LogInformation("Периодическая очистка осиротевших каталогов бэкапа запущена, интервал: {Hours} ч.", intervalHrs);
        using var period = new PeriodicTimer(TimeSpan.FromHours(intervalHrs));
        while (await period.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await OrphanProfileBackupDirectoryCleanup.RunAsync(
                    root, dbFactory, logger, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Периодическая очистка осиротевших каталогов бэкапа завершилась с ошибкой.");
            }
        }
    }
}
