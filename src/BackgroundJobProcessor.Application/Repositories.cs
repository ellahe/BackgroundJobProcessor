using BackgroundJobProcessor.Domain;

namespace BackgroundJobProcessor.Application;

public interface IBackgroundJobRepository
{
    Task<BackgroundJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(BackgroundJob job, CancellationToken cancellationToken = default);
    Task UpdateAsync(BackgroundJob job, CancellationToken cancellationToken = default);
}

public interface IJobExecutionRepository
{
    Task<IReadOnlyList<JobExecution>> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task AddAsync(JobExecution execution, CancellationToken cancellationToken = default);
}
