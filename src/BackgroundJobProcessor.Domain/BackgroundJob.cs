namespace BackgroundJobProcessor.Domain;

public sealed class BackgroundJob
{
    public Guid Id { get; }
    public string JobType { get; }
    public string Payload { get; }
    public JobStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ScheduledAt { get; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? LastError { get; private set; }

    public BackgroundJob(
        Guid id,
        string jobType,
        string payload,
        int maxRetries,
        DateTimeOffset scheduledAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(jobType))
            throw new ArgumentException("JobType is required.", nameof(jobType));
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));
        if (maxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "MaxRetries cannot be negative.");

        Id = id;
        JobType = jobType;
        Payload = payload;
        MaxRetries = maxRetries;
        ScheduledAt = scheduledAt.ToUniversalTime();
        CreatedAt = DateTimeOffset.UtcNow;
        Status = JobStatus.Pending;
        RetryCount = 0;
    }

    public void Start()
    {
        if (Status != JobStatus.Pending)
            throw new InvalidOperationException($"Cannot start a job in status {Status}.");

        Status = JobStatus.Processing;
        StartedAt = DateTimeOffset.UtcNow;
        CompletedAt = null;
        LastError = null;
    }

    public void Complete()
    {
        if (Status != JobStatus.Processing)
            throw new InvalidOperationException($"Cannot complete a job in status {Status}.");

        Status = JobStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void FailAttempt(string error)
    {
        if (Status != JobStatus.Processing)
            throw new InvalidOperationException($"Cannot record failure for a job in status {Status}.");
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("Error message is required.", nameof(error));

        LastError = error;
        RetryCount++;

        if (RetryCount <= MaxRetries)
        {
            Status = JobStatus.Pending;
        }
        else
        {
            Status = JobStatus.Failed;
            CompletedAt = DateTimeOffset.UtcNow;
        }
    }
}
