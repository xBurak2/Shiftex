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
            return ctx;
        }
    }
}
