using BackgroundJobProcessor.Domain;

namespace BackgroundJobProcessor.Application;

public sealed class JobService
{
    private readonly IBackgroundJobRepository _jobs;
    private readonly IJobExecutionRepository _executions;

    public JobService(IBackgroundJobRepository jobs, IJobExecutionRepository executions)
    {
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _executions = executions ?? throw new ArgumentNullException(nameof(executions));
    }

    public async Task<BackgroundJob> EnqueueAsync(
        string jobType,
        string payload,
        int maxRetries,
        DateTimeOffset scheduledAt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobType))
            throw new ArgumentException("JobType is required.", nameof(jobType));
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));
        if (maxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries));

        var job = new BackgroundJob(Guid.NewGuid(), jobType, payload, maxRetries, scheduledAt);
        await _jobs.AddAsync(job, cancellationToken).ConfigureAwait(false);
        return job;
    }

    public Task<BackgroundJob?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        return _jobs.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<JobExecution>> GetExecutionHistoryAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (jobId == Guid.Empty)
            throw new ArgumentException("JobId cannot be empty.", nameof(jobId));

        var job = await _jobs.GetByIdAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
            throw new KeyNotFoundException($"Background job '{jobId}' was not found.");

        return await _executions.GetByJobIdAsync(jobId, cancellationToken).ConfigureAwait(false);
    }
}
