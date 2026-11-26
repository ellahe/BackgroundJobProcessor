using BackgroundJobProcessor.Application;
using BackgroundJobProcessor.Domain;
using BackgroundJobProcessor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BackgroundJobProcessor.Infrastructure.Repositories;

public sealed class BackgroundJobRepository : IBackgroundJobRepository
{
    private readonly BackgroundJobDbContext _dbContext;

    public BackgroundJobRepository(BackgroundJobDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<BackgroundJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.BackgroundJobs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(BackgroundJob job, CancellationToken cancellationToken = default)
    {
        _dbContext.BackgroundJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(BackgroundJob job, CancellationToken cancellationToken = default)
    {
        _dbContext.BackgroundJobs.Update(job);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class JobExecutionRepository : IJobExecutionRepository
{
    private readonly BackgroundJobDbContext _dbContext;

    public JobExecutionRepository(BackgroundJobDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<JobExecution>> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        await _dbContext.JobExecutions
            .Where(x => x.BackgroundJobId == jobId)
            .OrderBy(x => x.AttemptNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(JobExecution execution, CancellationToken cancellationToken = default)
    {
        _dbContext.JobExecutions.Add(execution);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
