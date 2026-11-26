using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackgroundJobProcessor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261126082426_InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BackgroundJobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                JobType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Payload = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                RetryCount = table.Column<int>(type: "integer", nullable: false),
                MaxRetries = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                ScheduledAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BackgroundJobs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "JobExecutions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BackgroundJobId = table.Column<Guid>(type: "uuid", nullable: false),
                AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JobExecutions", x => x.Id);
                table.ForeignKey(
                    name: "FK_JobExecutions_BackgroundJobs_BackgroundJobId",
                    column: x => x.BackgroundJobId,
                    principalTable: "BackgroundJobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BackgroundJobs_Status_ScheduledAt",
            table: "BackgroundJobs",
            columns: new[] { "Status", "ScheduledAt" });

        migrationBuilder.CreateIndex(
            name: "IX_JobExecutions_BackgroundJobId_AttemptNumber",
            table: "JobExecutions",
            columns: new[] { "BackgroundJobId", "AttemptNumber" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "JobExecutions");

        migrationBuilder.DropTable(
            name: "BackgroundJobs");
    }
}
