using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comven_VehicleBilling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowRefundCreditLedgerEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ledger_entries_exactly_one_origin",
                table: "ledger_entries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ledger_entries_exactly_one_origin",
                table: "ledger_entries",
                sql: "(\r\n    \"operation_id\" IS NOT NULL\r\n    AND \"balance_credit_id\" IS NULL\r\n    AND \"type\" IN (1, 2)\r\n)\r\nOR\r\n(\r\n    \"operation_id\" IS NULL\r\n    AND \"balance_credit_id\" IS NOT NULL\r\n    AND \"type\" = 2\r\n)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ledger_entries_exactly_one_origin",
                table: "ledger_entries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ledger_entries_exactly_one_origin",
                table: "ledger_entries",
                sql: "(\n    \"operation_id\" IS NOT NULL\n    AND \"balance_credit_id\" IS NULL\n    AND \"type\" = 1\n)\nOR\n(\n    \"operation_id\" IS NULL\n    AND \"balance_credit_id\" IS NOT NULL\n    AND \"type\" = 2\n)");
        }
    }
}
