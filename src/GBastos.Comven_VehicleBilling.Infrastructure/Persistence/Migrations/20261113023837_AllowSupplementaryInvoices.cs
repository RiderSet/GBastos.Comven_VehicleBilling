using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comven_VehicleBilling.Infrastructure.Persistence.Migrations
{
  /// <inheritdoc />
  public partial class AllowSupplementaryInvoices : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropIndex(
          name: "ux_invoices_customer_period",
          table: "invoices");

      migrationBuilder.CreateIndex(
          name: "IX_invoices_customer_id",
          table: "invoices",
          column: "customer_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropIndex(
          name: "IX_invoices_customer_id",
          table: "invoices");

      migrationBuilder.CreateIndex(
          name: "ux_invoices_customer_period",
          table: "invoices",
          columns: new[] { "customer_id", "period_start", "period_end" },
          unique: true);
    }
  }
}
