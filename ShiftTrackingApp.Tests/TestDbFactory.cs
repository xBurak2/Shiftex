using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Data;

namespace ShiftTrackingApp.Tests
{
    public static class TestDbFactory
    {
        public static AppDbContext Create(string dbName = "")
        {
            if (string.IsNullOrEmpty(dbName))
                dbName = Guid.NewGuid().ToString();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            var ctx = new AppDbContext(options);
            ctx.Database.EnsureCreated();

            // Seed data'dan gelen kullanıcıları temizle (ID çakışmasını önlemek için).
            // Departments ve Shifts seed verileri FK referansları için bırakılır.
            ctx.Users.RemoveRange(ctx.Users);
            ctx.SaveChanges();

            return ctx;
        }
    }
}
