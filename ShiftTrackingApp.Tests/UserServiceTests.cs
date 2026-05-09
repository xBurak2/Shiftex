using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Models;
using ShiftTrackingApp.Services;
using Xunit;

namespace ShiftTrackingApp.Tests
{
    public class UserServiceTests
    {
        private static User MakeUser(int id, string name, bool active = true) => new()
        {
            Id           = id,
            FullName     = name,
            Email        = $"user{id}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test1234"),
            Role         = "Employee",
            IsActive     = active,
            CreatedAt    = DateTime.UtcNow
        };

        [Fact]
        public async Task GetAll_ReturnsOnlyActiveUsers()
        {
            using var db = TestDbFactory.Create();
            db.Users.AddRange(
                MakeUser(1, "Aktif Bir"),
                MakeUser(2, "Aktif İki"),
                MakeUser(3, "Pasif Üç", active: false)
            );
            await db.SaveChangesAsync();

            var svc    = new UserService(db);
            var result = await svc.GetAllAsync();

            Assert.Equal(2, result.TotalCount);
            Assert.All(result.Items, u => Assert.True(u.IsActive));
        }

        [Fact]
        public async Task GetAll_PaginationWorks()
        {
            using var db = TestDbFactory.Create();
            for (int i = 1; i <= 10; i++)
                db.Users.Add(MakeUser(i, $"Kullanıcı {i}"));
            await db.SaveChangesAsync();

            var svc  = new UserService(db);
            var page = await svc.GetAllAsync(page: 1, pageSize: 3);

            Assert.Equal(10, page.TotalCount);
            Assert.Equal(3,  page.Items.Count);
            Assert.Equal(4,  page.TotalPages);
            Assert.True(page.HasNext);
            Assert.False(page.HasPrev);
        }

        [Fact]
        public async Task GetById_NotFound_ReturnsNull()
        {
            using var db = TestDbFactory.Create();
            var svc    = new UserService(db);
            var result = await svc.GetByIdAsync(999);
            Assert.Null(result);
        }

        [Fact]
        public async Task GetById_ExistingUser_ReturnsDto()
        {
            using var db = TestDbFactory.Create();
            db.Users.Add(MakeUser(1, "Test Kişi"));
            await db.SaveChangesAsync();

            var svc    = new UserService(db);
            var result = await svc.GetByIdAsync(1);

            Assert.NotNull(result);
            Assert.Equal("Test Kişi",   result!.FullName);
            Assert.Equal("user1@test.com", result.Email);
        }

        [Fact]
        public async Task Create_DuplicateEmail_ThrowsException()
        {
            using var db = TestDbFactory.Create();
            db.Users.Add(MakeUser(1, "Mevcut Kullanıcı"));
            await db.SaveChangesAsync();

            var svc = new UserService(db);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateAsync(new CreateUserDto
                {
                    FullName = "Yeni Kullanıcı",
                    Email    = "user1@test.com",  // duplicate
                    Password = "Test1234",
                    Role     = "Employee"
                }));
        }

        [Fact]
        public async Task Delete_SoftDeletesUser()
        {
            using var db = TestDbFactory.Create();
            db.Users.Add(MakeUser(1, "Silinecek"));
            await db.SaveChangesAsync();

            var svc     = new UserService(db);
            var success = await svc.DeleteAsync(1);

            Assert.True(success);
            var user = db.Users.Find(1);
            Assert.NotNull(user);
            Assert.False(user!.IsActive); // Soft delete — veritabanından silinmez
        }

        [Fact]
        public async Task Delete_CancelsFutureShifts()
        {
            using var db = TestDbFactory.Create();
            db.Users.Add(MakeUser(1, "Personel"));
            db.Shifts.Add(new Shift { Id = 1, Name = "Sabah",
                StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(16), Color="#fff" });
            db.ShiftAssignments.Add(new ShiftAssignment
            {
                UserId    = 1,
                ShiftId   = 1,
                Date      = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var svc = new UserService(db);
            await svc.DeleteAsync(1);

            Assert.Empty(db.ShiftAssignments.Where(sa => sa.UserId == 1));
        }

        [Fact]
        public async Task Delete_RejectsPendingLeaves()
        {
            using var db = TestDbFactory.Create();
            db.Users.Add(MakeUser(1, "Personel"));
            db.LeaveRequests.Add(new LeaveRequest
            {
                UserId    = 1,
                LeaveType = "Yıllık",
                StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
                EndDate   = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
                Status    = "Pending",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var svc = new UserService(db);
            await svc.DeleteAsync(1);

            var leave = db.LeaveRequests.First();
            Assert.Equal("Rejected", leave.Status);
        }

        [Fact]
        public async Task Update_PhotoTooLarge_ThrowsException()
        {
            using var db = TestDbFactory.Create();
            db.Users.Add(MakeUser(1, "Foto Test"));
            await db.SaveChangesAsync();

            var svc = new UserService(db);
            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.UpdateAsync(1, new UpdateUserDto
                {
                    PhotoBase64 = new string('A', 600_000) // 600 KB — sınırı aşıyor
                }));
        }

        [Fact]
        public async Task Delete_NonExistentUser_ReturnsFalse()
        {
            using var db = TestDbFactory.Create();
            var svc     = new UserService(db);
            var result  = await svc.DeleteAsync(999);
            Assert.False(result);
        }
    }
}
