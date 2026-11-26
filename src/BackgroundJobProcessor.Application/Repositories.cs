using BackgroundJobProcessor.Domain;

using BackgroundJobProcessor.Domain;

namespace BackgroundJobProcessor.Application;

public sealed record ClaimedJob(BackgroundJob Job, JobExecution Execution);

public interface IBackgroundJobRepository
{
    Task<BackgroundJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(BackgroundJob job, CancellationToken cancellationToken = default);
    Task UpdateAsync(BackgroundJob job, CancellationToken cancellationToken = default);
    Task<ClaimedJob?> TryClaimNextAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default);
    Task<bool> RenewLeaseAsync(Guid jobId, string workerId, long claimToken, TimeSpan leaseExtension, CancellationToken cancellationToken = default);
    Task<bool> CompleteAsync(Guid jobId, string workerId, long claimToken, CancellationToken cancellationToken = default);
    Task<bool> FailAsync(Guid jobId, string workerId, long claimToken, string error, CancellationToken cancellationToken = default);
    Task RecoverExpiredLeasesAsync(CancellationToken cancellationToken = default);
}

public interface IJobExecutionRepository
{
    Task<IReadOnlyList<JobExecution>> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task AddAsync(JobExecution execution, CancellationToken cancellationToken = default);
}
