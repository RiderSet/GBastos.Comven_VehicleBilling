using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comven_VehicleBilling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginalInvoiceReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OriginalInvoiceId",
                table: "invoices",
                type: "uuid",
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invoices_invoices_OriginalInvoiceId",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_OriginalInvoiceId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "OriginalInvoiceId",
                table: "invoices");
        }
    }
}
