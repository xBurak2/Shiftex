using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftTrackingApp.Migrations
{
    /// <inheritdoc />
    public partial class ResetAdminPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // admin@shiftex.com şifresini bilinen değere ("Admin123!") geri sıfırla.
            // (Kullanıcı uygulama üzerinden değiştirip unuttu; BCrypt hash geri çevrilemez,
            //  bu yüzden bilinen bir hash ile üzerine yazıyoruz.)
            migrationBuilder.Sql(
                "UPDATE [Users] SET [PasswordHash] = " +
                "'$2a$11$E.YPlfQB/vm9Ef/cni.wROw1JGbmwwGVCWe7WI4LWCxMY0fjZYNnu' " +
                "WHERE [Email] = 'admin@shiftex.com';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
