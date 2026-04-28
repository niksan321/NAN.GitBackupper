using Microsoft.EntityFrameworkCore;

namespace NAN.GitBackupper.Api.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<BackupProfileEntity> BackupProfiles => Set<BackupProfileEntity>();

    public DbSet<AppRuntimeStateEntity> AppRuntimeState => Set<AppRuntimeStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppRuntimeStateEntity>(e =>
        {
            e.ToTable("AppRuntimeState");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<BackupProfileEntity>(e =>
        {
            e.ToTable("BackupProfiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.RepositoryBranchesJson).IsRequired();
            e.Property(x => x.BackupRepositoryKeysJson).IsRequired();
            e.Property(x => x.RepositoryBackupSizesBytesJson).IsRequired();
        });
    }
}
