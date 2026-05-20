using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftTrackingApp.Migrations
{
    /// <inheritdoc />
    public partial class ResetSeedUserPasswords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$E.YPlfQB/vm9Ef/cni.wROw1JGbmwwGVCWe7WI4LWCxMY0fjZYNnu");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                column: "PasswordHash",
                value: "$2a$11$wfZHxZ2X.NMhZdirV1rJDO6FR6svu2Ll9MnlWG5Nbl.T4.1AYtmLG");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$F.sfeiJJml3fxcIVJaCAd..dCqvOj4lxyYkU5G/ntppmqcz/49LGG");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                column: "PasswordHash",
                value: "$2a$11$M7vuZGpSlgLJF7JVBddg6uk5RHNb12QOPrvybvFot8o4N8bKJ.deq");
        }
    }
}
