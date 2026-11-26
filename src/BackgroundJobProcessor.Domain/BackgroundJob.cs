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
    public string? WorkerId { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public long ClaimToken { get; private set; }

    
public void Claim(string workerId, TimeSpan leaseDuration, DateTimeOffset now)
{
    if (Status != JobStatus.Pending)
        throw new InvalidOperationException($"Cannot claim a job in status {Status}.");

    if (string.IsNullOrWhiteSpace(workerId))
        throw new ArgumentException("WorkerId is required.", nameof(workerId));

    if (leaseDuration <= TimeSpan.Zero)
        throw new ArgumentOutOfRangeException(nameof(leaseDuration));

    var nextToken = checked(ClaimToken + 1);
    var utcNow = now.ToUniversalTime();
    var leaseExpiresAt = utcNow.Add(leaseDuration);

    Status = JobStatus.Processing;
    StartedAt = utcNow;
    CompletedAt = null;
    LastError = null;
    WorkerId = workerId;
    LeaseExpiresAt = leaseExpiresAt;
    ClaimToken = nextToken;
}

public void FailAttempt(
    string workerId,
    long claimToken,
    string error,
    DateTimeOffset? now = null)
{
    var utcNow = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();

    ValidateOwnership(workerId, claimToken, utcNow);

    if (string.IsNullOrWhiteSpace(error))
        throw new ArgumentException("Error message is required.", nameof(error));

    var nextRetryCount = checked(RetryCount + 1);

    LastError = error;
    RetryCount = nextRetryCount;

    if (nextRetryCount <= MaxRetries)
    {
        Status = JobStatus.Pending;
    }
    else
    {
        Status = JobStatus.Failed;
        CompletedAt = utcNow;
    }

    WorkerId = null;
    LeaseExpiresAt = null;
}

public void RecoverExpiredLease(DateTimeOffset? now = null)
{
    var utcNow = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();

    if (Status != JobStatus.Processing)
        throw new InvalidOperationException($"Cannot recover a job in status {Status}.");

    if (LeaseExpiresAt is null || LeaseExpiresAt > utcNow)
        throw new InvalidOperationException("Job lease has not expired.");

    var nextRetryCount = checked(RetryCount + 1);

    LastError = "Worker lease expired";
    RetryCount = nextRetryCount;

    if (nextRetryCount <= MaxRetries)
    {
        Status = JobStatus.Pending;
    }
    else
    {
        Status = JobStatus.Failed;
        CompletedAt = utcNow;
    }

    WorkerId = null;
    LeaseExpiresAt = null;
}

    public void RenewLease(string workerId, long claimToken, DateTimeOffset newLeaseExpiresAt, DateTimeOffset? now = null)
    {
        var utcNow = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        ValidateOwnership(workerId, claimToken, utcNow);
        if (LeaseExpiresAt is null || newLeaseExpiresAt.ToUniversalTime() <= LeaseExpiresAt)
            throw new ArgumentOutOfRangeException(nameof(newLeaseExpiresAt), "New lease must be later than current lease.");
        LeaseExpiresAt = newLeaseExpiresAt.ToUniversalTime();
    }

    public void Complete(string workerId, long claimToken, DateTimeOffset? now = null)
    {
        var utcNow = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        ValidateOwnership(workerId, claimToken, utcNow);
        if (Status != JobStatus.Processing)
            throw new InvalidOperationException($"Cannot complete a job in status {Status}.");
        Status = JobStatus.Completed;
        CompletedAt = utcNow;
        WorkerId = null;
        LeaseExpiresAt = null;
    }




    private void ValidateOwnership(string workerId, long claimToken, DateTimeOffset now)
    {
        if (Status != JobStatus.Processing)
            throw new InvalidOperationException($"Cannot operate on a job in status {Status}.");
        if (WorkerId != workerId || ClaimToken != claimToken)
            throw new InvalidOperationException("Job ownership validation failed.");
        if (LeaseExpiresAt is null || LeaseExpiresAt <= now)
            throw new InvalidOperationException("Job lease has expired.");
    }

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
}
