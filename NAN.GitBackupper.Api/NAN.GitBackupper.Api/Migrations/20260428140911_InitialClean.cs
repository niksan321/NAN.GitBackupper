using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NAN.GitBackupper.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialClean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppRuntimeState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IsSchedulerRunning = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRuntimeState", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    ApiKey = table.Column<string>(type: "TEXT", nullable: true),
                    ProviderUserName = table.Column<string>(type: "TEXT", nullable: true),
                    GitLabBaseUrl = table.Column<string>(type: "TEXT", nullable: true),
                    BackupRootPath = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    BackupIntervalValue = table.Column<int>(type: "INTEGER", nullable: false),
                    BackupIntervalType = table.Column<int>(type: "INTEGER", nullable: false),
                    OperationTimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CleanBackupDirectory = table.Column<bool>(type: "INTEGER", nullable: false),
                    ZipAfterBackup = table.Column<bool>(type: "INTEGER", nullable: false),
                    ZipCompression = table.Column<int>(type: "INTEGER", nullable: false),
                    ZipArchivesToKeep = table.Column<int>(type: "INTEGER", nullable: false),
                    KeepArchivesUntilDiskLimit = table.Column<bool>(type: "INTEGER", nullable: false),
                    DiskUsageLimitPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    RepositoryBackupParallelism = table.Column<int>(type: "INTEGER", nullable: false),
                    RepositoryBranchesJson = table.Column<string>(type: "TEXT", nullable: false),
                    UseAllRepositories = table.Column<bool>(type: "INTEGER", nullable: true),
                    RepositoryBackupSelectionMode = table.Column<int>(type: "INTEGER", nullable: true),
                    BackupRepositoryKeysJson = table.Column<string>(type: "TEXT", nullable: false),
                    RepositoryBackupSizesBytesJson = table.Column<string>(type: "TEXT", nullable: false),
                    LastBackupSelectedTotalBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    LastBackupZipArchiveBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    LastBackupDurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    ShowOnlySelectedRepositories = table.Column<bool>(type: "INTEGER", nullable: false),
                    BackupProgressLogDismissed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CachedRepositoriesJson = table.Column<string>(type: "TEXT", nullable: true),
                    CachedRepositoriesFetchedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupProfiles", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppRuntimeState");

            migrationBuilder.DropTable(
                name: "BackupProfiles");
        }
    }
}
