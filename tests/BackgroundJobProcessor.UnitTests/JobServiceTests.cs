using BackgroundJobProcessor.Application;
using BackgroundJobProcessor.Domain;

namespace BackgroundJobProcessor.UnitTests;

public class JobServiceTests
{
    private sealed class FakeJobRepo : IBackgroundJobRepository
    {
        public Dictionary<Guid, BackgroundJob> Store = new();
        public Exception? AddException;
        public Exception? GetException { get; set; }
        public CancellationToken LastToken;

        public Task<BackgroundJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            LastToken = ct;
            ct.ThrowIfCancellationRequested();
            if (GetException is not null) throw GetException;
            Store.TryGetValue(id, out var job);
            return Task.FromResult(job);
        }

        public Task AddAsync(BackgroundJob job, CancellationToken ct = default)
        {
            LastToken = ct;
            ct.ThrowIfCancellationRequested();
            if (AddException is not null) throw AddException;
            Store[job.Id] = job;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(BackgroundJob job, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeExecRepo : IJobExecutionRepository
    {
        public Dictionary<Guid, List<JobExecution>> Store = new();
        public Exception? GetException { get; set; }
        public CancellationToken LastToken;

        public Task<IReadOnlyList<JobExecution>> GetByJobIdAsync(Guid jobId, CancellationToken ct = default)
        {
            LastToken = ct;
            ct.ThrowIfCancellationRequested();
            if (GetException is not null) throw GetException;
            if (Store.TryGetValue(jobId, out var list))
                return Task.FromResult<IReadOnlyList<JobExecution>>(list);
            return Task.FromResult<IReadOnlyList<JobExecution>>(Array.Empty<JobExecution>());
        }

        public Task AddAsync(JobExecution execution, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task EnqueueAsync_Valid_CreatesAndStoresJob()
    {
        var jr = new FakeJobRepo();
        var er = new FakeExecRepo();
        var svc = new JobService(jr, er);
        var job = await svc.EnqueueAsync("Email", "{}", 2, DateTimeOffset.UtcNow);
        Assert.Equal("Email", job.JobType);
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.True(jr.Store.ContainsKey(job.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EnqueueAsync_InvalidJobType_Throws(string jobType)
    {
        var svc = new JobService(new FakeJobRepo(), new FakeExecRepo());
        await Assert.ThrowsAsync<ArgumentException>(() => svc.EnqueueAsync(jobType, "{}", 2, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task EnqueueAsync_NullPayload_Throws()
    {
        var svc = new JobService(new FakeJobRepo(), new FakeExecRepo());
        await Assert.ThrowsAsync<ArgumentNullException>(() => svc.EnqueueAsync("t", null!, 2, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task EnqueueAsync_NegativeMaxRetries_Throws()
    {
        var svc = new JobService(new FakeJobRepo(), new FakeExecRepo());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => svc.EnqueueAsync("t", "{}", -1, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task EnqueueAsync_RepositoryFailure_Propagates()
    {
        var jr = new FakeJobRepo { AddException = new InvalidOperationException("db") };
        var svc = new JobService(jr, new FakeExecRepo());
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.EnqueueAsync("t", "{}", 1, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task EnqueueAsync_Cancellation_Throws()
    {
        var jr = new FakeJobRepo();
        var svc = new JobService(jr, new FakeExecRepo());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => svc.EnqueueAsync("t", "{}", 1, DateTimeOffset.UtcNow, cts.Token));
    }

    [Fact]
    public async Task GetAsync_Existing_ReturnsJob()
    {
        var jr = new FakeJobRepo();
        var job = new BackgroundJob(Guid.NewGuid(), "t", "{}", 1, DateTimeOffset.UtcNow);
        jr.Store[job.Id] = job;
        var svc = new JobService(jr, new FakeExecRepo());
        var found = await svc.GetAsync(job.Id);
        Assert.Equal(job, found);
    }

    [Fact]
    public async Task GetAsync_Missing_ReturnsNull()
    {
        var svc = new JobService(new FakeJobRepo(), new FakeExecRepo());
        var found = await svc.GetAsync(Guid.NewGuid());
        Assert.Null(found);
    }

    [Fact]
    public async Task GetAsync_EmptyId_Throws()
    {
        var svc = new JobService(new FakeJobRepo(), new FakeExecRepo());
        await Assert.ThrowsAsync<ArgumentException>(() => svc.GetAsync(Guid.Empty));
    }

    [Fact]
    public async Task GetAsync_Cancellation_Throws()
    {
        var jr = new FakeJobRepo();
        var svc = new JobService(jr, new FakeExecRepo());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => svc.GetAsync(Guid.NewGuid(), cts.Token));
    }

    [Fact]
    public async Task GetExecutionHistoryAsync_Existing_ReturnsList()
    {
        var jobId = Guid.NewGuid();
        var jr = new FakeJobRepo();
        jr.Store[jobId] = new BackgroundJob(jobId, "t", "{}", 1, DateTimeOffset.UtcNow);
        var er = new FakeExecRepo();
        var e1 = new JobExecution(Guid.NewGuid(), jobId, 1);
        er.Store[jobId] = new List<JobExecution> { e1 };
        var svc = new JobService(jr, er);
        var history = await svc.GetExecutionHistoryAsync(jobId);
        Assert.Single(history);
    }

    [Fact]
    public async Task GetExecutionHistoryAsync_ExistingJobNoExecutions_ReturnsEmpty()
    {
        var jobId = Guid.NewGuid();
        var jr = new FakeJobRepo();
        jr.Store[jobId] = new BackgroundJob(jobId, "t", "{}", 1, DateTimeOffset.UtcNow);
        var svc = new JobService(jr, new FakeExecRepo());
        var history = await svc.GetExecutionHistoryAsync(jobId);
        Assert.Empty(history);
    }

    [Fact]
    public async Task GetExecutionHistoryAsync_NonexistentJob_ThrowsKeyNotFound()
    {
        var svc = new JobService(new FakeJobRepo(), new FakeExecRepo());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.GetExecutionHistoryAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetExecutionHistoryAsync_EmptyId_Throws()
    {
        var svc = new JobService(new FakeJobRepo(), new FakeExecRepo());
        await Assert.ThrowsAsync<ArgumentException>(() => svc.GetExecutionHistoryAsync(Guid.Empty));
    }

    [Fact]
    public async Task GetExecutionHistoryAsync_RepositoryFailure_Propagates()
    {
        var jobId = Guid.NewGuid();
        var jr = new FakeJobRepo();
        jr.Store[jobId] = new BackgroundJob(jobId, "t", "{}", 1, DateTimeOffset.UtcNow);
        var er = new FakeExecRepo { GetException = new InvalidOperationException("db") };
        var svc = new JobService(jr, er);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GetExecutionHistoryAsync(jobId));
    }

    [Fact]
    public async Task EnqueueAsync_PropagatesCancellationToken()
    {
        var jr = new FakeJobRepo();
        var svc = new JobService(jr, new FakeExecRepo());
        using var cts = new CancellationTokenSource();
        await svc.EnqueueAsync("t", "{}", 1, DateTimeOffset.UtcNow, cts.Token);
        Assert.Equal(cts.Token, jr.LastToken);
    }

    [Fact]
    public async Task GetAsync_PropagatesCancellationToken()
    {
        var jr = new FakeJobRepo();
        var svc = new JobService(jr, new FakeExecRepo());
        using var cts = new CancellationTokenSource();
        await svc.GetAsync(Guid.NewGuid(), cts.Token);
        Assert.Equal(cts.Token, jr.LastToken);
    }

    [Fact]
    public async Task GetExecutionHistoryAsync_PropagatesCancellationToken()
    {
        var jobId = Guid.NewGuid();
        var jr = new FakeJobRepo();
        jr.Store[jobId] = new BackgroundJob(jobId, "t", "{}", 1, DateTimeOffset.UtcNow);
        var er = new FakeExecRepo();
        var svc = new JobService(jr, er);
        using var cts = new CancellationTokenSource();
        await svc.GetExecutionHistoryAsync(jobId, cts.Token);
        Assert.Equal(cts.Token, jr.LastToken);
        Assert.Equal(cts.Token, er.LastToken);
    }
}
