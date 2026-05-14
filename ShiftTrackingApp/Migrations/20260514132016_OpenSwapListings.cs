using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftTrackingApp.Migrations
{
    /// <inheritdoc />
    public partial class OpenSwapListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TargetUserId",
                table: "ShiftSwapRequests",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "DesiredShiftId",
                table: "ShiftSwapRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSwapRequests_DesiredShiftId",
                table: "ShiftSwapRequests",
                column: "DesiredShiftId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftSwapRequests_Shifts_DesiredShiftId",
                table: "ShiftSwapRequests",
                column: "DesiredShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftSwapRequests_Shifts_DesiredShiftId",
                table: "ShiftSwapRequests");

            migrationBuilder.DropIndex(
                name: "IX_ShiftSwapRequests_DesiredShiftId",
                table: "ShiftSwapRequests");

            migrationBuilder.DropColumn(
                name: "DesiredShiftId",
                table: "ShiftSwapRequests");

            migrationBuilder.AlterColumn<int>(
                name: "TargetUserId",
                table: "ShiftSwapRequests",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
