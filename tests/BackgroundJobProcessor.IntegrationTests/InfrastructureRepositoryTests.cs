using BackgroundJobProcessor.Application;
using BackgroundJobProcessor.Domain;
using BackgroundJobProcessor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BackgroundJobProcessor.IntegrationTests;

public class InfrastructureRepositoryTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is required.");

    private static BackgroundJobDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<BackgroundJobDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(BackgroundJobDbContext).Assembly.FullName))
            .Options);

    private static IBackgroundJobRepository CreateJobRepo(BackgroundJobDbContext db) =>
        new BackgroundJobProcessor.Infrastructure.Repositories.BackgroundJobRepository(db);

    private static IJobExecutionRepository CreateExecRepo(BackgroundJobDbContext db) =>
        new BackgroundJobProcessor.Infrastructure.Repositories.JobExecutionRepository(db);

    [Fact]
    public async Task BackgroundJobRepository_AddAndGet_Persists()
    {
        using var db = CreateContext();
        await db.Database.MigrateAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var repo = CreateJobRepo(db);
        var job = new BackgroundJob(Guid.NewGuid(), "Email", "{}", 2, DateTimeOffset.UtcNow);
        await repo.AddAsync(job);

        db.ChangeTracker.Clear();
        var loaded = await repo.GetByIdAsync(job.Id);
        Assert.NotNull(loaded);
        Assert.Equal(job.Id, loaded!.Id);
        Assert.Equal(JobStatus.Pending, loaded.Status);
        Assert.Equal(0, loaded.RetryCount);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task BackgroundJobRepository_Update_PersistsState()
    {
        using var db = CreateContext();
        await db.Database.MigrateAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var repo = CreateJobRepo(db);
        var job = new BackgroundJob(Guid.NewGuid(), "Email", "{}", 2, DateTimeOffset.UtcNow);
        await repo.AddAsync(job);

        job.Start();
        job.FailAttempt("boom");
        await repo.UpdateAsync(job);

        db.ChangeTracker.Clear();
        var loaded = await repo.GetByIdAsync(job.Id);
        Assert.Equal(JobStatus.Pending, loaded!.Status);
        Assert.Equal(1, loaded.RetryCount);
        Assert.Equal("boom", loaded.LastError);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task JobExecutionRepository_AddAndGet_OrderedByAttempt()
    {
        using var db = CreateContext();
        await db.Database.MigrateAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var jobRepo = CreateJobRepo(db);
        var execRepo = CreateExecRepo(db);

        var job = new BackgroundJob(Guid.NewGuid(), "Email", "{}", 2, DateTimeOffset.UtcNow);
        await jobRepo.AddAsync(job);

        await execRepo.AddAsync(new JobExecution(Guid.NewGuid(), job.Id, 2));
        await execRepo.AddAsync(new JobExecution(Guid.NewGuid(), job.Id, 1));

        db.ChangeTracker.Clear();
        var history = await execRepo.GetByJobIdAsync(job.Id);
        Assert.Equal(new[] { 1, 2 }, history.Select(x => x.AttemptNumber).ToArray());

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task JobExecutionRepository_UniqueConstraint_Throws()
    {
        using var db = CreateContext();
        await db.Database.MigrateAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var jobRepo = CreateJobRepo(db);
        var execRepo = CreateExecRepo(db);

        var job = new BackgroundJob(Guid.NewGuid(), "Email", "{}", 2, DateTimeOffset.UtcNow);
        await jobRepo.AddAsync(job);

        await execRepo.AddAsync(new JobExecution(Guid.NewGuid(), job.Id, 1));
        await Assert.ThrowsAsync<DbUpdateException>(() => execRepo.AddAsync(new JobExecution(Guid.NewGuid(), job.Id, 1)));

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task JobExecutionRepository_ForeignKey_Throws()
    {
        using var db = CreateContext();
        await db.Database.MigrateAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var execRepo = CreateExecRepo(db);
        await Assert.ThrowsAsync<DbUpdateException>(() => execRepo.AddAsync(new JobExecution(Guid.NewGuid(), Guid.NewGuid(), 1)));

        await tx.RollbackAsync();
    }
}
