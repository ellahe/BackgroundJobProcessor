using BackgroundJobProcessor.Domain;

namespace BackgroundJobProcessor.UnitTests;

public class BackgroundJobTests
{
    [Fact]
    public void Constructor_Valid_CreatesPendingWithZeroRetries()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "Email", "{}", 3, DateTimeOffset.UtcNow);
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(0, job.RetryCount);
        Assert.True(job.CreatedAt.Offset == TimeSpan.Zero);
        Assert.True(job.ScheduledAt.Offset == TimeSpan.Zero);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidJobType_Throws(string jobType)
    {
        Assert.Throws<ArgumentException>(() => new BackgroundJob(Guid.NewGuid(), jobType, "{}", 3, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_NullPayload_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BackgroundJob(Guid.NewGuid(), "t", null!, 3, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_NegativeMaxRetries_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BackgroundJob(Guid.NewGuid(), "t", "{}", -1, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new BackgroundJob(Guid.Empty, "t", "{}", 3, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ScheduledAt_PreservesInstant_InUtc()
    {
        var local = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 3, local);
        Assert.Equal(local.ToUniversalTime(), job.ScheduledAt);
        Assert.Equal(TimeSpan.Zero, job.ScheduledAt.Offset);
    }

    [Fact]
    public void Start_FromPending_TransitionsToProcessing()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 3, DateTimeOffset.UtcNow);
        job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        Assert.Equal(JobStatus.Processing, job.Status);
        Assert.NotNull(job.StartedAt);
        Assert.True(job.StartedAt!.Value.Offset == TimeSpan.Zero);
    }

    [Fact]
    public void Start_FromNonPending_Throws()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 3, DateTimeOffset.UtcNow);
        job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        job.Complete("worker", job.ClaimToken);
        var before = job.Status;
        Assert.Throws<InvalidOperationException>(() => job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow));
        Assert.Equal(before, job.Status);
    }

    [Fact]
    public void Start_FromCompleted_Throws()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 3, DateTimeOffset.UtcNow);
        job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        job.Complete("worker", job.ClaimToken);
        Assert.Throws<InvalidOperationException>(() => job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Start_FromFailed_Throws()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 0, DateTimeOffset.UtcNow);
        job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        job.FailAttempt("worker", job.ClaimToken, "boom");
        Assert.Throws<InvalidOperationException>(() => job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Start_AfterRetryAvailable_Succeeds()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 1, DateTimeOffset.UtcNow);
        job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        job.FailAttempt("worker", job.ClaimToken, "boom");
        Assert.Equal(JobStatus.Pending, job.Status);
        job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        Assert.Equal(JobStatus.Processing, job.Status);
    }

    [Fact]
    public void Complete_FromProcessing_TransitionsToCompleted()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 3, DateTimeOffset.UtcNow);
        job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        job.Complete("worker", job.ClaimToken);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAt);
        Assert.True(job.CompletedAt >= job.StartedAt);
    }

    [Fact]
    public void Complete_AfterPreviousFailedAttempt_Succeeds()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 1, DateTimeOffset.UtcNow);
        job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        job.FailAttempt("worker", job.ClaimToken, "boom");
        job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        job.Complete("worker", job.ClaimToken);
        Assert.Equal(JobStatus.Completed, job.Status);
    }

    [Fact]
    public void Complete_FromNonProcessing_Throws()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 3, DateTimeOffset.UtcNow);
        var before = job.Status;
        Assert.Throws<InvalidOperationException>(() => job.Complete("worker", job.ClaimToken));
        Assert.Equal(before, job.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void FailAttempt_Boundaries(int maxRetries)
    {
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", maxRetries, DateTimeOffset.UtcNow);
        for (int i = 0; i <= maxRetries; i++)
        {
            job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
            job.FailAttempt("worker", job.ClaimToken, "boom");
            if (i < maxRetries)
                Assert.Equal(JobStatus.Pending, job.Status);
            else
                Assert.Equal(JobStatus.Failed, job.Status);
        }
        Assert.Equal(maxRetries + 1, job.RetryCount);
    }

    [Fact]
    public void FailAttempt_FirstWithZeroMaxRetries_FailsImmediately()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 0, DateTimeOffset.UtcNow);
        job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        job.FailAttempt("worker", job.ClaimToken, "boom");
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(1, job.RetryCount);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void FailAttempt_FirstWithOneMaxRetries_ReturnsPending()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 1, DateTimeOffset.UtcNow);
        job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        job.FailAttempt("worker", job.ClaimToken, "boom");
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(1, job.RetryCount);
    }

    [Fact]
    public void FailAttempt_FromNonProcessing_Throws()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 3, DateTimeOffset.UtcNow);
        var before = job.RetryCount;
        Assert.Throws<InvalidOperationException>(() => job.FailAttempt("worker", job.ClaimToken, "boom"));
        Assert.Equal(before, job.RetryCount);
    }

    [Fact]
    public void FailAttempt_EmptyError_Throws()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 3, DateTimeOffset.UtcNow);
        job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => job.FailAttempt("worker", job.ClaimToken, "  "));
    }

    [Fact]
    public void FailAttempt_DoesNotClearStartedAt()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 3, DateTimeOffset.UtcNow);
        job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        var started = job.StartedAt;
        job.FailAttempt("worker", job.ClaimToken, "boom");
        Assert.Equal(started, job.StartedAt);
    }

    [Fact]
    public void FailAttempt_UpdatesStartedAt_OnNextStart()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 3, DateTimeOffset.UtcNow);
        job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        var first = job.StartedAt;
        job.FailAttempt("worker", job.ClaimToken, "boom");
        job.Claim("worker", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        Assert.NotEqual(first, job.StartedAt);
    }
}




