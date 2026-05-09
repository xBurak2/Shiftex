using Microsoft.Extensions.Configuration;
using Moq;
using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Helpers;
using ShiftTrackingApp.Models;
using ShiftTrackingApp.Services;
using Xunit;

namespace ShiftTrackingApp.Tests
{
    public class AuthServiceTests
    {
        private static IConfiguration BuildConfig() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"]                 = "TestJwtKeyForUnitTests_MustBe32Chars!",
                    ["Jwt:Issuer"]              = "TestIssuer",
                    ["Jwt:Audience"]            = "TestAudience",
                    ["Jwt:ExpiresInMinutes"]    = "60",
                    ["Jwt:RefreshExpiresInDays"]= "7",
                })
                .Build();

        private static User CreateTestUser(string password = "Test1234") => new()
        {
            Id           = 1,
            FullName     = "Test Kullanıcı",
            Email        = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role         = "Employee",
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow
        };

        [Fact]
        public async Task Login_WithCorrectCredentials_ReturnsToken()
        {
            using var db   = TestDbFactory.Create();
            var config     = BuildConfig();
            var jwt        = new JwtHelper(config);
            db.Users.Add(CreateTestUser());
            await db.SaveChangesAsync();

            var svc    = new AuthService(db, jwt, config);
            var result = await svc.LoginAsync(new LoginDto
                { Email = "test@example.com", Password = "Test1234" });

            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result!.Token));
            Assert.False(string.IsNullOrEmpty(result.RefreshToken));
            Assert.Equal("test@example.com", result.Email);
        }

        [Fact]
        public async Task Login_WithWrongPassword_ReturnsNull()
        {
            using var db = TestDbFactory.Create();
            var config   = BuildConfig();
            var jwt      = new JwtHelper(config);
            db.Users.Add(CreateTestUser());
            await db.SaveChangesAsync();

            var svc    = new AuthService(db, jwt, config);
            var result = await svc.LoginAsync(new LoginDto
                { Email = "test@example.com", Password = "YanlisŞifre" });

            Assert.Null(result);
        }

        [Fact]
        public async Task Login_WithUnknownEmail_ReturnsNull()
        {
            using var db = TestDbFactory.Create();
            var config   = BuildConfig();
            var jwt      = new JwtHelper(config);

            var svc    = new AuthService(db, jwt, config);
            var result = await svc.LoginAsync(new LoginDto
                { Email = "yok@example.com", Password = "herhangi" });

            Assert.Null(result);
        }

        [Fact]
        public async Task Login_InactiveUser_ReturnsNull()
        {
            using var db = TestDbFactory.Create();
            var config   = BuildConfig();
            var jwt      = new JwtHelper(config);
            var u        = CreateTestUser();
            u.IsActive   = false;
            db.Users.Add(u);
            await db.SaveChangesAsync();

            var svc    = new AuthService(db, jwt, config);
            var result = await svc.LoginAsync(new LoginDto
                { Email = "test@example.com", Password = "Test1234" });

            Assert.Null(result);
        }

        [Fact]
        public async Task Refresh_WithRevokedToken_ReturnsNull()
        {
            using var db = TestDbFactory.Create();
            var config   = BuildConfig();
            var jwt      = new JwtHelper(config);
            var u        = CreateTestUser();
            db.Users.Add(u);
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId    = 1,
                Token     = "revoked-token",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsRevoked = true
            });
            await db.SaveChangesAsync();

            var svc    = new AuthService(db, jwt, config);
            var result = await svc.RefreshAsync("revoked-token");

            Assert.Null(result);
        }

        [Fact]
        public async Task Refresh_WithExpiredToken_ReturnsNull()
        {
            using var db = TestDbFactory.Create();
            var config   = BuildConfig();
            var jwt      = new JwtHelper(config);
            var u        = CreateTestUser();
            db.Users.Add(u);
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId    = 1,
                Token     = "expired-token",
                ExpiresAt = DateTime.UtcNow.AddDays(-1), // Süresi geçmiş
                IsRevoked = false
            });
            await db.SaveChangesAsync();

            var svc    = new AuthService(db, jwt, config);
            var result = await svc.RefreshAsync("expired-token");

            Assert.Null(result);
        }

        [Fact]
        public async Task Login_MaxRefreshTokens_OldestRevoked()
        {
            using var db = TestDbFactory.Create();
            var config   = BuildConfig();
            var jwt      = new JwtHelper(config);
            var u        = CreateTestUser();
            db.Users.Add(u);

            // 5 aktif token ekle (maksimum)
            for (int i = 0; i < 5; i++)
            {
                db.RefreshTokens.Add(new RefreshToken
                {
                    UserId    = 1,
                    Token     = $"old-token-{i}",
                    ExpiresAt = DateTime.UtcNow.AddDays(7),
                    CreatedAt = DateTime.UtcNow.AddMinutes(-i - 1),
                    IsRevoked = false
                });
            }
            await db.SaveChangesAsync();

            var svc    = new AuthService(db, jwt, config);
            await svc.LoginAsync(new LoginDto
                { Email = "test@example.com", Password = "Test1234" });

            var activeCount = db.RefreshTokens.Count(rt => !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow);
            Assert.True(activeCount <= 5, $"Aktif token sayısı 5'i geçmemeli. Şu an: {activeCount}");
        }
    }
}
