using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comven_VehicleBilling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueExternalOperationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_operations_external_operation_id",
                table: "operations",
                column: "external_operation_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_operations_external_operation_id",
                table: "operations");
        }
    }
}
