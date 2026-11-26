using BackgroundJobProcessor.Domain;

namespace BackgroundJobProcessor.UnitTests;

public class JobExecutionTests
{
    [Fact]
    public void Constructor_Valid_StartsIncomplete()
    {
        var exec = new JobExecution(Guid.NewGuid(), Guid.NewGuid(), 1);
        Assert.False(exec.IsCompleted);
        Assert.False(exec.Succeeded);
        Assert.Null(exec.CompletedAt);
        Assert.True(exec.StartedAt.Offset == TimeSpan.Zero);
    }

    [Fact]
    public void Constructor_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new JobExecution(Guid.Empty, Guid.NewGuid(), 1));
    }

    [Fact]
    public void Constructor_EmptyJobId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new JobExecution(Guid.NewGuid(), Guid.Empty, 1));
    }

    [Fact]
    public void Constructor_InvalidAttempt_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JobExecution(Guid.NewGuid(), Guid.NewGuid(), 0));
    }

    [Fact]
    public void MarkSuccess_CompletesSuccessfully()
    {
        var exec = new JobExecution(Guid.NewGuid(), Guid.NewGuid(), 1);
        exec.MarkSuccess();
        Assert.True(exec.IsCompleted);
        Assert.True(exec.Succeeded);
        Assert.Null(exec.ErrorMessage);
        Assert.NotNull(exec.CompletedAt);
        Assert.True(exec.CompletedAt >= exec.StartedAt);
    }

    [Fact]
    public void MarkFailure_WithMessage_CompletesFailed()
    {
        var exec = new JobExecution(Guid.NewGuid(), Guid.NewGuid(), 1);
        exec.MarkFailure("error");
        Assert.True(exec.IsCompleted);
        Assert.False(exec.Succeeded);
        Assert.Equal("error", exec.ErrorMessage);
        Assert.True(exec.CompletedAt >= exec.StartedAt);
    }

    [Fact]
    public void MarkFailure_EmptyMessage_Throws()
    {
        var exec = new JobExecution(Guid.NewGuid(), Guid.NewGuid(), 1);
        Assert.Throws<ArgumentException>(() => exec.MarkFailure(" "));
    }

    [Fact]
    public void MarkSuccess_Twice_Throws()
    {
        var exec = new JobExecution(Guid.NewGuid(), Guid.NewGuid(), 1);
        exec.MarkSuccess();
        Assert.Throws<InvalidOperationException>(() => exec.MarkSuccess());
    }

    [Fact]
    public void MarkFailure_Twice_Throws()
    {
        var exec = new JobExecution(Guid.NewGuid(), Guid.NewGuid(), 1);
        exec.MarkFailure("error");
        Assert.Throws<InvalidOperationException>(() => exec.MarkFailure("error2"));
    }

    [Fact]
    public void MarkSuccess_ThenFailure_Throws()
    {
        var exec = new JobExecution(Guid.NewGuid(), Guid.NewGuid(), 1);
        exec.MarkSuccess();
        Assert.Throws<InvalidOperationException>(() => exec.MarkFailure("x"));
    }

    [Fact]
    public void MarkFailure_ThenSuccess_Throws()
    {
        var exec = new JobExecution(Guid.NewGuid(), Guid.NewGuid(), 1);
        exec.MarkFailure("error");
        Assert.Throws<InvalidOperationException>(() => exec.MarkSuccess());
    }

    [Fact]
    public void AttemptNumbering_Sequence()
    {
        var jobId = Guid.NewGuid();
        var list = new List<JobExecution>
        {
            new(Guid.NewGuid(), jobId, 1),
            new(Guid.NewGuid(), jobId, 2),
            new(Guid.NewGuid(), jobId, 3)
        };
        Assert.Equal(new[] { 1, 2, 3 }, list.Select(e => e.AttemptNumber).ToArray());
    }

    [Fact]
    public void ExecutionHistory_PreservedIndependently()
    {
        var jobId = Guid.NewGuid();
        var e1 = new JobExecution(Guid.NewGuid(), jobId, 1);
        e1.MarkFailure("first");
        var e2 = new JobExecution(Guid.NewGuid(), jobId, 2);
        e2.MarkSuccess();
        Assert.Equal("first", e1.ErrorMessage);
        Assert.False(e1.Succeeded);
        Assert.True(e2.Succeeded);
    }
}
