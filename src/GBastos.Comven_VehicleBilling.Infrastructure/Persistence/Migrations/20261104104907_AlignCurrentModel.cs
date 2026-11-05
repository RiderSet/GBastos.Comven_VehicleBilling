using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comven_VehicleBilling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignCurrentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_ledger_entries_operation_type",
                table: "ledger_entries");

            migrationBuilder.AlterColumn<Guid>(
                name: "operation_id",
                table: "ledger_entries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "balance_credit_id",
                table: "ledger_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_ledger_entries_balance_credit_type",
                table: "ledger_entries",
                columns: new[] { "balance_credit_id", "type" },
                unique: true,
                filter: "\"balance_credit_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_ledger_entries_operation_type",
                table: "ledger_entries",
                columns: new[] { "operation_id", "type" },
                unique: true,
                filter: "\"operation_id\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ledger_entries_exactly_one_origin",
                table: "ledger_entries",
                sql: "(\r\n    \"operation_id\" IS NOT NULL\r\n    AND \"balance_credit_id\" IS NULL\r\n    AND \"type\" = 1\r\n)\r\nOR\r\n(\r\n    \"operation_id\" IS NULL\r\n    AND \"balance_credit_id\" IS NOT NULL\r\n    AND \"type\" = 2\r\n)");

            migrationBuilder.AddForeignKey(
                name: "FK_ledger_entries_balance_credits_balance_credit_id",
                table: "ledger_entries",
                column: "balance_credit_id",
                principalTable: "balance_credits",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ledger_entries_balance_credits_balance_credit_id",
                table: "ledger_entries");

            migrationBuilder.DropIndex(
                name: "ux_ledger_entries_balance_credit_type",
                table: "ledger_entries");

            migrationBuilder.DropIndex(
                name: "ux_ledger_entries_operation_type",
                table: "ledger_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ledger_entries_exactly_one_origin",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "balance_credit_id",
                table: "ledger_entries");

            migrationBuilder.AlterColumn<Guid>(
                name: "operation_id",
                table: "ledger_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_ledger_entries_operation_type",
                table: "ledger_entries",
                columns: new[] { "operation_id", "type" },
                unique: true);
        }
    }
}
