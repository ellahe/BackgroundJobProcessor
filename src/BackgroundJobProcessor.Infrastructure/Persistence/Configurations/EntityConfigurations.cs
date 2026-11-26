using BackgroundJobProcessor.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BackgroundJobProcessor.Infrastructure.Persistence.Configurations;

public sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.ToTable("BackgroundJobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasField("<Id>k__BackingField");
        builder.Property(x => x.JobType).HasField("<JobType>k__BackingField").IsRequired().HasMaxLength(200);
        builder.Property(x => x.Payload).HasField("<Payload>k__BackingField").IsRequired();
        builder.Property(x => x.Status).HasField("<Status>k__BackingField").HasConversion<int>().IsRequired();
        builder.Property(x => x.RetryCount).HasField("<RetryCount>k__BackingField").IsRequired();
        builder.Property(x => x.MaxRetries).HasField("<MaxRetries>k__BackingField").IsRequired();
        builder.Property(x => x.CreatedAt).HasField("<CreatedAt>k__BackingField").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ScheduledAt).HasField("<ScheduledAt>k__BackingField").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.StartedAt).HasField("<StartedAt>k__BackingField").HasColumnType("timestamptz");
        builder.Property(x => x.CompletedAt).HasField("<CompletedAt>k__BackingField").HasColumnType("timestamptz");
        builder.Property(x => x.LastError).HasField("<LastError>k__BackingField").HasMaxLength(4000);

        builder.HasIndex(x => new { x.Status, x.ScheduledAt });
    }
}

public sealed class JobExecutionConfiguration : IEntityTypeConfiguration<JobExecution>
{
    public void Configure(EntityTypeBuilder<JobExecution> builder)
    {
        builder.ToTable("JobExecutions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasField("<Id>k__BackingField");
        builder.Property(x => x.BackgroundJobId).HasField("<BackgroundJobId>k__BackingField").IsRequired();
        builder.Property(x => x.AttemptNumber).HasField("<AttemptNumber>k__BackingField").IsRequired();
        builder.Property(x => x.StartedAt).HasField("<StartedAt>k__BackingField").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CompletedAt).HasField("<CompletedAt>k__BackingField").HasColumnType("timestamptz");
        builder.Property(x => x.Succeeded).HasField("<Succeeded>k__BackingField").IsRequired();
        builder.Property(x => x.ErrorMessage).HasField("<ErrorMessage>k__BackingField").HasMaxLength(4000);

        builder.Ignore(x => x.IsCompleted);

        builder.HasOne<BackgroundJob>()
            .WithMany()
            .HasForeignKey(x => x.BackgroundJobId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.BackgroundJobId, x.AttemptNumber }).IsUnique();
    }
}
