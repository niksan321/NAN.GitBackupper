using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NAN.GitBackupper.Api.Database;

/// <summary>Для dotnet ef migrations (design-time).</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite("Data Source=gitbackupper.design.db");
        return new AppDbContext(optionsBuilder.Options);
    }
}