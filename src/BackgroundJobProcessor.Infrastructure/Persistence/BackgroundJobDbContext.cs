using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BackgroundJobProcessor.Infrastructure.Persistence;

public sealed class BackgroundJobDbContext : DbContext
{
    public BackgroundJobDbContext(DbContextOptions<BackgroundJobDbContext> options) : base(options) { }

    public DbSet<Domain.BackgroundJob> BackgroundJobs => Set<Domain.BackgroundJob>();
    public DbSet<Domain.JobExecution> JobExecutions => Set<Domain.JobExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BackgroundJobDbContext).Assembly);
    }
}

public sealed class BackgroundJobDbContextFactory : IDesignTimeDbContextFactory<BackgroundJobDbContext>
{
    public BackgroundJobDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings__DefaultConnection environment variable is required to create the design-time DbContext.");

        var options = new DbContextOptionsBuilder<BackgroundJobDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(BackgroundJobDbContext).Assembly.FullName))
            .Options;

        return new BackgroundJobDbContext(options);
    }
}
