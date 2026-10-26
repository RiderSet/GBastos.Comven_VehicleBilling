using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comven_VehicleBilling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxMessageLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LockId",
                table: "outbox_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntil",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockId",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LockedUntil",
                table: "outbox_messages");
        }
    }
}
