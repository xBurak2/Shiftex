using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ShiftTrackingApp.Migrations
{
    /// <inheritdoc />
    public partial class Seed20PermanentEmployees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "DailyWage", "DepartmentId", "Email", "EmploymentType", "FullName", "HireDate", "IsActive", "PasswordHash", "PhoneNumber", "PhotoBase64", "Position", "Role" },
                values: new object[,]
                {
                    { 3, new DateTime(2021, 3, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1, "ali.vural@shifttrack.com", "Permanent", "Ali Vural", new DateTime(2021, 3, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Kesim Ustası", "Employee" },
                    { 4, new DateTime(2022, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1, "cemal.dogan@shifttrack.com", "Permanent", "Cemal Doğan", new DateTime(2022, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Kesim Operatörü", "Employee" },
                    { 5, new DateTime(2023, 1, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1, "hakan.arslan@shifttrack.com", "Permanent", "Hakan Arslan", new DateTime(2023, 1, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Kesim Operatörü", "Employee" },
                    { 6, new DateTime(2023, 9, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1, "osman.yildirim@shifttrack.com", "Permanent", "Osman Yıldırım", new DateTime(2023, 9, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Kesim Operatörü", "Employee" },
                    { 7, new DateTime(2020, 11, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2, "elif.simsek@shifttrack.com", "Permanent", "Elif Şimşek", new DateTime(2020, 11, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Dikiş Ustası", "Employee" },
                    { 8, new DateTime(2021, 6, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2, "derya.koc@shifttrack.com", "Permanent", "Derya Koç", new DateTime(2021, 6, 14, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Dikiş Operatörü", "Employee" },
                    { 9, new DateTime(2022, 2, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2, "gul.erdogan@shifttrack.com", "Permanent", "Gül Erdoğan", new DateTime(2022, 2, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Overlok Operatörü", "Employee" },
                    { 10, new DateTime(2022, 10, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2, "hatice.aslan@shifttrack.com", "Permanent", "Hatice Aslan", new DateTime(2022, 10, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Dikiş Operatörü", "Employee" },
                    { 11, new DateTime(2023, 4, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2, "meryem.tas@shifttrack.com", "Permanent", "Meryem Taş", new DateTime(2023, 4, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Dikiş Operatörü", "Employee" },
                    { 12, new DateTime(2023, 11, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2, "nurten.acar@shifttrack.com", "Permanent", "Nurten Acar", new DateTime(2023, 11, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Düğme/İlik Operatörü", "Employee" },
                    { 13, new DateTime(2021, 8, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 3, "kemal.sen@shifttrack.com", "Permanent", "Kemal Şen", new DateTime(2021, 8, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Ütü Ustası", "Employee" },
                    { 14, new DateTime(2022, 5, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 3, "ramazan.bulut@shifttrack.com", "Permanent", "Ramazan Bulut", new DateTime(2022, 5, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Ütücü", "Employee" },
                    { 15, new DateTime(2023, 2, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 3, "yasemin.toprak@shifttrack.com", "Permanent", "Yasemin Toprak", new DateTime(2023, 2, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Paketleme Elemanı", "Employee" },
                    { 16, new DateTime(2024, 1, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 3, "hulya.cetin@shifttrack.com", "Permanent", "Hülya Çetin", new DateTime(2024, 1, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Paketleme Elemanı", "Employee" },
                    { 17, new DateTime(2020, 9, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 4, "serkan.korkmaz@shifttrack.com", "Permanent", "Serkan Korkmaz", new DateTime(2020, 9, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Kalite Şefi", "Employee" },
                    { 18, new DateTime(2022, 3, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 4, "aylin.gunes@shifttrack.com", "Permanent", "Aylin Güneş", new DateTime(2022, 3, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Kalite Kontrolör", "Employee" },
                    { 19, new DateTime(2023, 7, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 4, "pinar.yalcin@shifttrack.com", "Permanent", "Pınar Yalçın", new DateTime(2023, 7, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Kalite Kontrolör", "Employee" },
                    { 20, new DateTime(2021, 5, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 5, "tolga.eren@shifttrack.com", "Permanent", "Tolga Eren", new DateTime(2021, 5, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Sevkiyat Sorumlusu", "Employee" },
                    { 21, new DateTime(2022, 12, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 5, "erhan.avci@shifttrack.com", "Permanent", "Erhan Avcı", new DateTime(2022, 12, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Depo Görevlisi", "Employee" },
                    { 22, new DateTime(2023, 6, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 5, "volkan.kaplan@shifttrack.com", "Permanent", "Volkan Kaplan", new DateTime(2023, 6, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS", null, null, "Depo Görevlisi", "Employee" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 22);
        }
    }
}
