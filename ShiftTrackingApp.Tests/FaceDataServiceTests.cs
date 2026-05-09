using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Models;
using ShiftTrackingApp.Services;
using Xunit;

namespace ShiftTrackingApp.Tests
{
    public class FaceDataServiceTests
    {
        // "ShiftexFaceEncryptionKey2026!Dev" = 32 bytes
        private const string TestKey = "U2hpZnRleEZhY2VFbmNyeXB0aW9uS2V5MjAyNiFEZXY=";

        private static IConfiguration BuildConfig() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FaceEncryption:Key"] = TestKey
                })
                .Build();

        private static float[] FakeDescriptor(float seed = 0.1f) =>
            Enumerable.Range(0, 128).Select(i => seed + i * 0.001f).ToArray();

        private static User MakeUser(int id) => new()
        {
            Id           = id,
            FullName     = $"Kullanıcı {id}",
            Email        = $"u{id}@test.com",
            PasswordHash = "x",
            Role         = "Employee",
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow
        };

        [Fact]
        public async Task Save_NewUser_CreatesFaceRecord()
        {
            using var db = TestDbFactory.Create();
            db.Users.Add(MakeUser(1));
            await db.SaveChangesAsync();

            var svc = new FaceDataService(db, BuildConfig(), NullLogger<FaceDataService>.Instance);
            var dto = new SaveFaceDataDto { UserId = 1, Descriptor = FakeDescriptor() };
            await svc.SaveAsync(dto);

            Assert.Single(db.FaceData);
            Assert.Equal(1, db.FaceData.First().UserId);
        }

        [Fact]
        public async Task Save_ExistingUser_UpdatesRecord()
        {
            using var db = TestDbFactory.Create();
            db.Users.Add(MakeUser(1));
            await db.SaveChangesAsync();

            var svc = new FaceDataService(db, BuildConfig(), NullLogger<FaceDataService>.Instance);
            await svc.SaveAsync(new SaveFaceDataDto { UserId = 1, Descriptor = FakeDescriptor(0.1f) });
            await svc.SaveAsync(new SaveFaceDataDto { UserId = 1, Descriptor = FakeDescriptor(0.9f) });

            // Upsert: tek kayıt olmalı
            Assert.Single(db.FaceData);
        }

        [Fact]
        public async Task GetAll_DecryptsDescriptorCorrectly()
        {
            using var db = TestDbFactory.Create();
            db.Users.Add(MakeUser(1));
            await db.SaveChangesAsync();

            var original = FakeDescriptor(0.5f);
            var svc = new FaceDataService(db, BuildConfig(), NullLogger<FaceDataService>.Instance);
            await svc.SaveAsync(new SaveFaceDataDto { UserId = 1, Descriptor = original });

            var results = await svc.GetAllAsync();
            Assert.Single(results);
            Assert.Equal(128, results[0].Descriptor.Length);
            Assert.Equal(original[0], results[0].Descriptor[0], precision: 5);
        }

        [Fact]
        public async Task Delete_ExistingRecord_ReturnsTrue()
        {
            using var db = TestDbFactory.Create();
            db.Users.Add(MakeUser(1));
            await db.SaveChangesAsync();

            var svc = new FaceDataService(db, BuildConfig(), NullLogger<FaceDataService>.Instance);
            await svc.SaveAsync(new SaveFaceDataDto { UserId = 1, Descriptor = FakeDescriptor() });
            var result = await svc.DeleteAsync(1);

            Assert.True(result);
            Assert.Empty(db.FaceData);
        }

        [Fact]
        public async Task Delete_NonExistentUser_ReturnsFalse()
        {
            using var db = TestDbFactory.Create();
            var svc      = new FaceDataService(db, BuildConfig(), NullLogger<FaceDataService>.Instance);
            var result   = await svc.DeleteAsync(999);
            Assert.False(result);
        }

        [Fact]
        public async Task Save_InactiveUser_ThrowsException()
        {
            using var db = TestDbFactory.Create();
            db.Users.Add(new User
            {
                Id = 1, FullName = "Pasif", Email = "p@t.com",
                PasswordHash = "x", Role = "Employee", IsActive = false,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var svc = new FaceDataService(db, BuildConfig(), NullLogger<FaceDataService>.Instance);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.SaveAsync(new SaveFaceDataDto { UserId = 1, Descriptor = FakeDescriptor() }));
        }

        [Fact]
        public async Task GetAll_ExcludesInactiveUsers()
        {
            using var db = TestDbFactory.Create();
            db.Users.Add(MakeUser(1));
            db.Users.Add(new User
            {
                Id = 2, FullName = "Pasif", Email = "p@t.com",
                PasswordHash = "x", Role = "Employee", IsActive = true, // Aktif olarak kaydet
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var svc = new FaceDataService(db, BuildConfig(), NullLogger<FaceDataService>.Instance);
            await svc.SaveAsync(new SaveFaceDataDto { UserId = 1, Descriptor = FakeDescriptor() });
            await svc.SaveAsync(new SaveFaceDataDto { UserId = 2, Descriptor = FakeDescriptor() });

            // Kullanıcı 2'yi pasife al
            var u2 = db.Users.Find(2)!;
            u2.IsActive = false;
            await db.SaveChangesAsync();

            var results = await svc.GetAllAsync();
            Assert.Single(results); // Yalnızca aktif kullanıcının verisi gelmeli
            Assert.Equal(1, results[0].UserId);
        }
    }
}
