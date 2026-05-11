using ShiftTrackingApp.Models;
using ShiftTrackingApp.Services;
using Xunit;

namespace ShiftTrackingApp.Tests
{
    public class ShiftServiceTests
    {
        private static User MakeUser(int id) => new()
        {
            Id           = id,
            FullName     = $"Test User {id}",
            Email        = $"u{id}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test1234"),
            Role         = "Employee",
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow
        };

        [Fact]
        public async Task CopyWeek_CopiesAllAssignmentsToNewWeek()
        {
            using var db = TestDbFactory.Create();
            db.Users.Add(MakeUser(1));
            db.Users.Add(MakeUser(2));

            // Kaynak hafta: 2026-05-04 (Pzt) → 2026-05-10 (Pz)
            var srcWeek = new DateOnly(2026, 5, 4);
            db.ShiftAssignments.Add(new ShiftAssignment
            {
                UserId = 1, ShiftId = 1, Date = srcWeek,             CreatedAt = DateTime.UtcNow
            });
            db.ShiftAssignments.Add(new ShiftAssignment
            {
                UserId = 2, ShiftId = 2, Date = srcWeek.AddDays(2),  CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            // Hedef hafta: 2026-05-11
            var dstWeek = new DateOnly(2026, 5, 11);
            var svc = new ShiftService(db);
            var copied = await svc.CopyWeekAsync(srcWeek, dstWeek);

            Assert.Equal(2, copied);
            var dstAssignments = db.ShiftAssignments
                .Where(sa => sa.Date >= dstWeek && sa.Date <= dstWeek.AddDays(6))
                .ToList();
            Assert.Equal(2, dstAssignments.Count);
            Assert.Contains(dstAssignments, a => a.UserId == 1 && a.Date == dstWeek);
            Assert.Contains(dstAssignments, a => a.UserId == 2 && a.Date == dstWeek.AddDays(2));
        }

        [Fact]
        public async Task CopyWeek_OverwritesExistingTargetWeek()
        {
            using var db = TestDbFactory.Create();
            db.Users.Add(MakeUser(1));

            var srcWeek = new DateOnly(2026, 5, 4);
            var dstWeek = new DateOnly(2026, 5, 11);

            // Hedef haftada zaten bir atama var — kopyalama bunu silmeli
            db.ShiftAssignments.Add(new ShiftAssignment
            {
                UserId = 1, ShiftId = 3, Date = dstWeek, CreatedAt = DateTime.UtcNow
            });
            // Kaynak haftada atama
            db.ShiftAssignments.Add(new ShiftAssignment
            {
                UserId = 1, ShiftId = 1, Date = srcWeek, CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var svc = new ShiftService(db);
            await svc.CopyWeekAsync(srcWeek, dstWeek);

            var dstAssignments = db.ShiftAssignments
                .Where(sa => sa.Date >= dstWeek && sa.Date <= dstWeek.AddDays(6))
                .ToList();
            Assert.Single(dstAssignments);
            Assert.Equal(1, dstAssignments[0].ShiftId); // Kaynak vardiyası (Sabah)
        }

        [Fact]
        public async Task CopyWeek_ThrowsWhenSourceEqualsTarget()
        {
            using var db = TestDbFactory.Create();
            var svc = new ShiftService(db);
            var week = new DateOnly(2026, 5, 4);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.CopyWeekAsync(week, week));
        }
    }
}
