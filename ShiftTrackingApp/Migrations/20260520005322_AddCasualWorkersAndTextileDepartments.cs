using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ShiftTrackingApp.Migrations
{
    /// <inheritdoc />
    public partial class AddCasualWorkersAndTextileDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DailyWage",
                table: "Users",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmploymentType",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Kumaş kesim hattı", "Kesim" });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Dikiş atölyesi (en kalabalık bölüm)", "Dikiş" });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Ütüleme ve paketleme bandı", "Ütü & Paketleme" });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Ürün kalite muayene", "Kalite Kontrol" });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Depo / sevkiyat hattı", "Sevkiyat" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DailyWage", "DepartmentId", "EmploymentType", "Position" },
                values: new object[] { null, null, "Permanent", "Üretim Müdürü" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DailyWage", "DepartmentId", "EmploymentType", "Position" },
                values: new object[] { null, 2, "Permanent", "Dikiş Operatörü" });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "DailyWage", "DepartmentId", "Email", "EmploymentType", "FullName", "HireDate", "IsActive", "PasswordHash", "PhoneNumber", "PhotoBase64", "Position", "Role" },
                values: new object[,]
                {
                    { 100, new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), 850m, 1, "hasan.demir@shifttrack.com", "Casual", "Hasan Demir", new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$OtjPXY5sSdMc2Sagr6O8r.5j0a8vAlJROTuKr03GvMsmhL/G2P3Li", null, null, "Yevmiyeci · Kesim", "Employee" },
                    { 101, new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), 850m, 1, "murat.aksoy@shifttrack.com", "Casual", "Murat Aksoy", new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$OtjPXY5sSdMc2Sagr6O8r.5j0a8vAlJROTuKr03GvMsmhL/G2P3Li", null, null, "Yevmiyeci · Kesim", "Employee" },
                    { 102, new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), 800m, 2, "fatma.sahin@shifttrack.com", "Casual", "Fatma Şahin", new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$OtjPXY5sSdMc2Sagr6O8r.5j0a8vAlJROTuKr03GvMsmhL/G2P3Li", null, null, "Yevmiyeci · Dikiş", "Employee" },
                    { 103, new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), 800m, 2, "zeynep.aydin@shifttrack.com", "Casual", "Zeynep Aydın", new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$OtjPXY5sSdMc2Sagr6O8r.5j0a8vAlJROTuKr03GvMsmhL/G2P3Li", null, null, "Yevmiyeci · Dikiş", "Employee" },
                    { 104, new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), 800m, 2, "emine.celik@shifttrack.com", "Casual", "Emine Çelik", new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$OtjPXY5sSdMc2Sagr6O8r.5j0a8vAlJROTuKr03GvMsmhL/G2P3Li", null, null, "Yevmiyeci · Dikiş", "Employee" },
                    { 105, new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), 750m, 3, "sibel.polat@shifttrack.com", "Casual", "Sibel Polat", new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$OtjPXY5sSdMc2Sagr6O8r.5j0a8vAlJROTuKr03GvMsmhL/G2P3Li", null, null, "Yevmiyeci · Ütü & Paketleme", "Employee" },
                    { 106, new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), 900m, 4, "burak.ozturk@shifttrack.com", "Casual", "Burak Öztürk", new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$OtjPXY5sSdMc2Sagr6O8r.5j0a8vAlJROTuKr03GvMsmhL/G2P3Li", null, null, "Yevmiyeci · Kalite Kontrol", "Employee" },
                    { 107, new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), 850m, 5, "selim.kurt@shifttrack.com", "Casual", "Selim Kurt", new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$OtjPXY5sSdMc2Sagr6O8r.5j0a8vAlJROTuKr03GvMsmhL/G2P3Li", null, null, "Yevmiyeci · Sevkiyat", "Employee" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 100);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 103);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 104);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 105);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 106);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 107);

            migrationBuilder.DropColumn(
                name: "DailyWage",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmploymentType",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "Name" },
                values: new object[] { null, "IT" });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "Name" },
                values: new object[] { null, "Muhasebe" });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "Name" },
                values: new object[] { null, "İnsan Kaynakları" });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "Name" },
                values: new object[] { null, "Operasyon" });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Description", "Name" },
                values: new object[] { null, "Güvenlik" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DepartmentId", "Position" },
                values: new object[] { 3, "İK Müdürü" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DepartmentId", "Position" },
                values: new object[] { 1, "Yazılım Geliştirici" });
        }
    }
}
