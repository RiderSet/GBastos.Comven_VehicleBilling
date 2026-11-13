using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comven_VehicleBilling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplementaryInvoiceIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invoices_invoices_OriginalInvoiceId",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_OriginalInvoiceId",
                table: "invoices");

            migrationBuilder.RenameColumn(
                name: "OriginalInvoiceId",
                table: "invoices",
                newName: "original_invoice_id");

            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_invoices_original_idempotency",
                table: "invoices",
                columns: new[] { "original_invoice_id", "idempotency_key" },
                unique: true,
                filter: "original_invoice_id IS NOT NULL AND idempotency_key IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_invoices_original_invoice",
                table: "invoices",
                column: "original_invoice_id",
                principalTable: "invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_invoices_original_invoice",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "ux_invoices_original_idempotency",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "invoices");

            migrationBuilder.RenameColumn(
                name: "original_invoice_id",
                table: "invoices",
                newName: "OriginalInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_OriginalInvoiceId",
                table: "invoices",
                column: "OriginalInvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_invoices_OriginalInvoiceId",
                table: "invoices",
                column: "OriginalInvoiceId",
                principalTable: "invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
