using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackgroundJobProcessor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261126095143_AddJobClaimLease : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "ClaimToken",
            table: "BackgroundJobs",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LeaseExpiresAt",
            table: "BackgroundJobs",
            type: "timestamptz",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "WorkerId",
            table: "BackgroundJobs",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ClaimToken",
            table: "BackgroundJobs");

        migrationBuilder.DropColumn(
            name: "LeaseExpiresAt",
            table: "BackgroundJobs");

        migrationBuilder.DropColumn(
            name: "WorkerId",
            table: "BackgroundJobs");
    }
}
