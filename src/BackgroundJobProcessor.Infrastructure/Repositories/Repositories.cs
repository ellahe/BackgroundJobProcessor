
using BackgroundJobProcessor.Application;
using BackgroundJobProcessor.Domain;
using BackgroundJobProcessor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BackgroundJobProcessor.Infrastructure.Repositories;

public sealed class BackgroundJobRepository : IBackgroundJobRepository
{
    private readonly BackgroundJobDbContext _dbContext;

    public BackgroundJobRepository(BackgroundJobDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<BackgroundJob?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        _dbContext.BackgroundJobs.FirstOrDefaultAsync(
            x => x.Id == id,
            cancellationToken);

    public async Task AddAsync(
        BackgroundJob job,
        CancellationToken cancellationToken = default)
    {
        _dbContext.BackgroundJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(
        BackgroundJob job,
        CancellationToken cancellationToken = default)
    {
        _dbContext.BackgroundJobs.Update(job);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClaimedJob?> TryClaimNextAsync(
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            throw new ArgumentException("WorkerId is required.", nameof(workerId));

        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(leaseDuration),
                "Lease duration must be positive.");

        await using var tx = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var snapshots = new List<EntitySnapshot>();
        var addedEntities = new List<object>();

        try
        {
            var now = DateTimeOffset.UtcNow;

            var candidate = await _dbContext.BackgroundJobs
                .FromSqlRaw(
                    """
                    SELECT *
                    FROM "BackgroundJobs"
                    WHERE "Status" = {0}
                      AND "ScheduledAt" <= {1}
                    ORDER BY "ScheduledAt", "Id"
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                    """,
                    (int)JobStatus.Pending,
                    now)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (candidate is null)
            {
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }

            snapshots.Add(Capture(candidate));

            var attemptNumber = checked(candidate.RetryCount + 1);

            candidate.Claim(workerId, leaseDuration, now);

            var execution = new JobExecution(
                Guid.NewGuid(),
                candidate.Id,
                attemptNumber);

            _dbContext.JobExecutions.Add(execution);
            addedEntities.Add(execution);

            await _dbContext.SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new ClaimedJob(candidate, execution);
        }
        catch
        {
            await RollbackAndRestoreAsync(
                tx,
                snapshots,
                addedEntities,
                cancellationToken).ConfigureAwait(false);

            throw;
        }
    }

    public async Task<bool> RenewLeaseAsync(
        Guid jobId,
        string workerId,
        long claimToken,
        TimeSpan leaseExtension,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workerId) ||
            leaseExtension <= TimeSpan.Zero)
            return false;

        await using var tx = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var snapshots = new List<EntitySnapshot>();

        try
        {
            var job = await _dbContext.BackgroundJobs
                .FromSqlRaw(
                    """
                    SELECT *
                    FROM "BackgroundJobs"
                    WHERE "Id" = {0}
                    FOR UPDATE
                    """,
                    jobId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (job is null || job.LeaseExpiresAt is null)
            {
                await RollbackAndRestoreAsync(
                    tx, snapshots, [], cancellationToken).ConfigureAwait(false);

                return false;
            }

            snapshots.Add(Capture(job));

            var now = DateTimeOffset.UtcNow;
            var newExpiration = job.LeaseExpiresAt.Value.Add(leaseExtension);

            job.RenewLease(
                workerId,
                claimToken,
                newExpiration,
                now);

            await _dbContext.SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (InvalidOperationException)
        {
            await RollbackAndRestoreAsync(
                tx, snapshots, [], cancellationToken).ConfigureAwait(false);

            return false;
        }
        catch (ArgumentException)
        {
            await RollbackAndRestoreAsync(
                tx, snapshots, [], cancellationToken).ConfigureAwait(false);

            return false;
        }
        catch
        {
            await RollbackAndRestoreAsync(
                tx, snapshots, [], cancellationToken).ConfigureAwait(false);

            throw;
        }
    }

    public async Task<bool> CompleteAsync(
        Guid jobId,
        string workerId,
        long claimToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            return false;

        await using var tx = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var snapshots = new List<EntitySnapshot>();

        try
        {
            var job = await _dbContext.BackgroundJobs
                .FromSqlRaw(
                    """
                    SELECT *
                    FROM "BackgroundJobs"
                    WHERE "Id" = {0}
                    FOR UPDATE
                    """,
                    jobId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (job is null)
            {
                await RollbackAndRestoreAsync(
                    tx, snapshots, [], cancellationToken).ConfigureAwait(false);

                return false;
            }

            var attemptNumber = checked(job.RetryCount + 1);

            var execution = await _dbContext.JobExecutions
                .FirstOrDefaultAsync(
                    x => x.BackgroundJobId == jobId &&
                         x.AttemptNumber == attemptNumber &&
                         !x.Succeeded &&
                         x.CompletedAt == null,
                    cancellationToken)
                .ConfigureAwait(false);

            if (execution is null)
            {
                await RollbackAndRestoreAsync(
                    tx, snapshots, [], cancellationToken).ConfigureAwait(false);

                return false;
            }

            snapshots.Add(Capture(job));
            snapshots.Add(Capture(execution));

            var now = DateTimeOffset.UtcNow;

            job.Complete(workerId, claimToken, now);
            execution.MarkSuccess();

            await _dbContext.SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (InvalidOperationException)
        {
            await RollbackAndRestoreAsync(
                tx, snapshots, [], cancellationToken).ConfigureAwait(false);

            return false;
        }
        catch
        {
            await RollbackAndRestoreAsync(
                tx, snapshots, [], cancellationToken).ConfigureAwait(false);

            throw;
        }
    }

    public async Task<bool> FailAsync(
        Guid jobId,
        string workerId,
        long claimToken,
        string error,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workerId) ||
            string.IsNullOrWhiteSpace(error))
            return false;

        await using var tx = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var snapshots = new List<EntitySnapshot>();

        try
        {
            var job = await _dbContext.BackgroundJobs
                .FromSqlRaw(
                    """
                    SELECT *
                    FROM "BackgroundJobs"
                    WHERE "Id" = {0}
                    FOR UPDATE
                    """,
                    jobId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (job is null)
            {
                await RollbackAndRestoreAsync(
                    tx, snapshots, [], cancellationToken).ConfigureAwait(false);

                return false;
            }

            var attemptNumber = checked(job.RetryCount + 1);

            var execution = await _dbContext.JobExecutions
                .FirstOrDefaultAsync(
                    x => x.BackgroundJobId == jobId &&
                         x.AttemptNumber == attemptNumber &&
                         !x.Succeeded &&
                         x.CompletedAt == null,
                    cancellationToken)
                .ConfigureAwait(false);

            if (execution is null)
            {
                await RollbackAndRestoreAsync(
                    tx, snapshots, [], cancellationToken).ConfigureAwait(false);

                return false;
            }

            snapshots.Add(Capture(job));
            snapshots.Add(Capture(execution));

            var now = DateTimeOffset.UtcNow;

            // Validate and mutate the Job before modifying its Execution.
            job.FailAttempt(workerId, claimToken, error, now);
            execution.MarkFailure(error);

            await _dbContext.SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (InvalidOperationException)
        {
            await RollbackAndRestoreAsync(
                tx, snapshots, [], cancellationToken).ConfigureAwait(false);

            return false;
        }
        catch (ArgumentException)
        {
            await RollbackAndRestoreAsync(
                tx, snapshots, [], cancellationToken).ConfigureAwait(false);

            return false;
        }
        catch
        {
            await RollbackAndRestoreAsync(
                tx, snapshots, [], cancellationToken).ConfigureAwait(false);

            throw;
        }
    }

    public async Task RecoverExpiredLeasesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var snapshots = new List<EntitySnapshot>();

        try
        {
            var now = DateTimeOffset.UtcNow;

            var expiredJobs = await _dbContext.BackgroundJobs
                .FromSqlRaw(
                    """
                    SELECT *
                    FROM "BackgroundJobs"
                    WHERE "Status" = {0}
                      AND "LeaseExpiresAt" <= {1}
                    FOR UPDATE SKIP LOCKED
                    """,
                    (int)JobStatus.Processing,
                    now)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var job in expiredJobs)
            {
                var attemptNumber = checked(job.RetryCount + 1);

                var execution = await _dbContext.JobExecutions
                    .FirstOrDefaultAsync(
                        x => x.BackgroundJobId == job.Id &&
                             x.AttemptNumber == attemptNumber &&
                             !x.Succeeded &&
                             x.CompletedAt == null,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (execution is null)
                {
                    throw new InvalidOperationException(
                        $"Current execution not found for job {job.Id}.");
                }

                snapshots.Add(Capture(job));
                snapshots.Add(Capture(execution));

                // Recover the Job before modifying its Execution.
                job.RecoverExpiredLease(now);
                execution.MarkFailure("Worker lease expired");
            }

            await _dbContext.SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await RollbackAndRestoreAsync(
                tx,
                snapshots,
                [],
                cancellationToken).ConfigureAwait(false);

            throw;
        }
    }



    private EntitySnapshot Capture(object entity)
    {
        var entry = _dbContext.Entry(entity);

        var modifiedProperties = entry.Properties.ToDictionary(
            property => property.Metadata.Name,
            property => property.IsModified);

        return new EntitySnapshot(
            entry,
            entry.State,
            entry.CurrentValues.Clone(),
            entry.OriginalValues.Clone(),
            modifiedProperties);
    }

    private async Task RollbackAndRestoreAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx,
        IReadOnlyCollection<EntitySnapshot> snapshots,
        IReadOnlyCollection<object> addedEntities,
        CancellationToken cancellationToken)
    {
        try
        {
            await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            foreach (var entity in addedEntities)
            {
                var entry = _dbContext.Entry(entity);

                if (entry.State != EntityState.Detached)
                    entry.State = EntityState.Detached;
            }

            foreach (var snapshot in snapshots.Reverse())
            {
                if (snapshot.Entry.State == EntityState.Detached)
                    continue;

                snapshot.Entry.CurrentValues.SetValues(snapshot.CurrentValues);
                snapshot.Entry.OriginalValues.SetValues(snapshot.OriginalValues);
                snapshot.Entry.State = snapshot.State;

                if (snapshot.State == EntityState.Modified)
                {
                    foreach (var property in snapshot.ModifiedProperties)
                    {
                        snapshot.Entry.Property(property.Key).IsModified = property.Value;
                    }
                }
            }
        }
    }

    private sealed record EntitySnapshot(
      EntityEntry Entry,
      EntityState State,
      PropertyValues CurrentValues,
      PropertyValues OriginalValues,
      IReadOnlyDictionary<string, bool> ModifiedProperties);
}

public sealed class JobExecutionRepository : IJobExecutionRepository
{
    private readonly BackgroundJobDbContext _dbContext;

    public JobExecutionRepository(BackgroundJobDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<JobExecution>> GetByJobIdAsync(
        Guid jobId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.JobExecutions
            .Where(x => x.BackgroundJobId == jobId)
            .OrderBy(x => x.AttemptNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(
        JobExecution execution,
        CancellationToken cancellationToken = default)
    {
        _dbContext.JobExecutions.Add(execution);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
