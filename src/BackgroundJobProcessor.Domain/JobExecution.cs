namespace BackgroundJobProcessor.Domain;

public sealed class JobExecution
{
    public Guid Id { get; }
    public Guid BackgroundJobId { get; }
    public int AttemptNumber { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public bool Succeeded { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool IsCompleted => CompletedAt.HasValue;

    public JobExecution(Guid id, Guid backgroundJobId, int attemptNumber)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (backgroundJobId == Guid.Empty)
            throw new ArgumentException("BackgroundJobId cannot be empty.", nameof(backgroundJobId));
        if (attemptNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(attemptNumber), "AttemptNumber must be at least 1.");

        Id = id;
        BackgroundJobId = backgroundJobId;
        AttemptNumber = attemptNumber;
        StartedAt = DateTimeOffset.UtcNow;
        Succeeded = false;
    }

    public void MarkSuccess()
    {
        if (IsCompleted)
            throw new InvalidOperationException("Execution already completed.");

        Succeeded = true;
        ErrorMessage = null;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailure(string errorMessage)
    {
        if (IsCompleted)
            throw new InvalidOperationException("Execution already completed.");
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message is required.", nameof(errorMessage));

        Succeeded = false;
        ErrorMessage = errorMessage;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
