using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Models;

namespace ShiftTrackingApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User>            Users            => Set<User>();
        public DbSet<Department>      Departments      => Set<Department>();
        public DbSet<Shift>           Shifts           => Set<Shift>();
        public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
        public DbSet<LeaveRequest>    LeaveRequests    => Set<LeaveRequest>();
        public DbSet<AttendanceLog>   AttendanceLogs   => Set<AttendanceLog>();
        public DbSet<RefreshToken>    RefreshTokens    => Set<RefreshToken>();
        public DbSet<FaceData>        FaceData         => Set<FaceData>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            // ── ŞİFT ATAMA — Unique index ─────────────────────────────────
            mb.Entity<ShiftAssignment>()
              .HasIndex(x => new { x.UserId, x.Date, x.ShiftId })
              .IsUnique();

            mb.Entity<User>()
              .HasOne(u => u.Department)
              .WithMany(d => d.Users)
              .HasForeignKey(u => u.DepartmentId)
              .OnDelete(DeleteBehavior.SetNull);

            mb.Entity<ShiftAssignment>()
              .HasOne(sa => sa.User)
              .WithMany(u => u.ShiftAssignments)
              .HasForeignKey(sa => sa.UserId)
              .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<LeaveRequest>()
              .HasOne(l => l.User)
              .WithMany(u => u.LeaveRequests)
              .HasForeignKey(l => l.UserId)
              .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<AttendanceLog>()
              .HasOne(a => a.User)
              .WithMany(u => u.AttendanceLogs)
              .HasForeignKey(a => a.UserId)
              .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<RefreshToken>()
              .HasOne(rt => rt.User)
              .WithMany()
              .HasForeignKey(rt => rt.UserId)
              .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<RefreshToken>()
              .HasIndex(rt => rt.Token)
              .IsUnique();

            // ── YÜZVERISI — Her personel için tek kayıt ──────────────────
            mb.Entity<FaceData>()
              .HasOne(fd => fd.User)
              .WithMany()
              .HasForeignKey(fd => fd.UserId)
              .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<FaceData>()
              .HasIndex(fd => fd.UserId)
              .IsUnique(); // Bir personele yalnızca bir yüz kaydı

            // ── SEED DATA ─────────────────────────────────────────────────
            mb.Entity<Department>().HasData(
                new Department { Id = 1, Name = "IT" },
                new Department { Id = 2, Name = "Muhasebe" },
                new Department { Id = 3, Name = "İnsan Kaynakları" },
                new Department { Id = 4, Name = "Operasyon" },
                new Department { Id = 5, Name = "Güvenlik" }
            );

            mb.Entity<Shift>().HasData(
                new Shift { Id = 1, Name = "Sabah",                       StartTime = new TimeSpan(8,  0, 0), EndTime = new TimeSpan(16, 0, 0), Color = "#f59e0b" },
                new Shift { Id = 2, Name = "Öğleden Sonra",               StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(22, 0, 0), Color = "#4f6ef7" },
                new Shift { Id = 3, Name = "Gece",                        StartTime = new TimeSpan(22, 0, 0), EndTime = new TimeSpan(6,  0, 0), Color = "#a78bfa" },
                new Shift { Id = 4, Name = "Tatil",                       StartTime = TimeSpan.Zero,          EndTime = TimeSpan.Zero,          Color = "#ef4444" },
                new Shift { Id = 5, Name = "İzinli",                      StartTime = TimeSpan.Zero,          EndTime = TimeSpan.Zero,          Color = "#22c55e" },
                new Shift { Id = 6, Name = "Part Time",                   StartTime = new TimeSpan(8,  0, 0), EndTime = new TimeSpan(12, 0, 0), Color = "#14b8a6" },
                new Shift { Id = 7, Name = "Sabah Fazla Mesai",           StartTime = new TimeSpan(16, 0, 0), EndTime = new TimeSpan(18, 0, 0), Color = "#f97316" },
                new Shift { Id = 8, Name = "Öğleden Sonra Fazla Mesai",  StartTime = new TimeSpan(22, 0, 0), EndTime = new TimeSpan(0,  0, 0), Color = "#6366f1" },
                new Shift { Id = 9, Name = "Gece Fazla Mesai",           StartTime = new TimeSpan(6,  0, 0), EndTime = new TimeSpan(8,  0, 0), Color = "#ec4899" }
            );

            mb.Entity<User>().HasData(
                new User
                {
                    Id           = 1,
                    FullName     = "Ayşe Yılmaz",
                    Email        = "admin@shifttrack.com",
                    PasswordHash = "$2a$11$F.sfeiJJml3fxcIVJaCAd..dCqvOj4lxyYkU5G/ntppmqcz/49LGG",
                    Role         = "Admin",
                    DepartmentId = 3,
                    Position     = "İK Müdürü",
                    IsActive     = true,
                    CreatedAt    = new DateTime(2020, 3, 1),
                    HireDate     = new DateTime(2020, 3, 1)
                },
                new User
                {
                    Id           = 2,
                    FullName     = "Mehmet Kaya",
                    Email        = "mehmet@shifttrack.com",
                    PasswordHash = "$2a$11$M7vuZGpSlgLJF7JVBddg6uk5RHNb12QOPrvybvFot8o4N8bKJ.deq",
                    Role         = "Employee",
                    DepartmentId = 1,
                    Position     = "Yazılım Geliştirici",
                    IsActive     = true,
                    CreatedAt    = new DateTime(2022, 6, 15),
                    HireDate     = new DateTime(2022, 6, 15)
                }
            );
        }
    }
}
