using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Models;

namespace ShiftTrackingApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User>              Users              => Set<User>();
        public DbSet<Department>        Departments        => Set<Department>();
        public DbSet<Shift>             Shifts             => Set<Shift>();
        public DbSet<ShiftAssignment>   ShiftAssignments   => Set<ShiftAssignment>();
        public DbSet<LeaveRequest>      LeaveRequests      => Set<LeaveRequest>();
        public DbSet<AttendanceLog>     AttendanceLogs     => Set<AttendanceLog>();
        public DbSet<RefreshToken>      RefreshTokens      => Set<RefreshToken>();
        public DbSet<FaceData>          FaceData           => Set<FaceData>();
        public DbSet<ShiftSwapRequest>  ShiftSwapRequests  => Set<ShiftSwapRequest>();
        public DbSet<OvertimeRequest>   OvertimeRequests   => Set<OvertimeRequest>();

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

            // Yevmiyeci günlük ücreti: TL bazlı, 2 ondalık (HasPrecision 10,2)
            mb.Entity<User>()
              .Property(u => u.DailyWage)
              .HasPrecision(10, 2);

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

            // ── VARDIYA DEĞİŞİM TALEPLERİ ────────────────────────────────
            mb.Entity<ShiftSwapRequest>()
              .HasOne(s => s.Requester)
              .WithMany()
              .HasForeignKey(s => s.RequesterId)
              .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<ShiftSwapRequest>()
              .HasOne(s => s.TargetUser)
              .WithMany()
              .HasForeignKey(s => s.TargetUserId)
              .OnDelete(DeleteBehavior.Restrict)
              .IsRequired(false);

            mb.Entity<ShiftSwapRequest>()
              .HasOne(s => s.RequesterShiftAssignment)
              .WithMany()
              .HasForeignKey(s => s.RequesterShiftAssignmentId)
              .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<ShiftSwapRequest>()
              .HasOne(s => s.TargetShiftAssignment)
              .WithMany()
              .HasForeignKey(s => s.TargetShiftAssignmentId)
              .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<ShiftSwapRequest>()
              .HasOne(s => s.DesiredShift)
              .WithMany()
              .HasForeignKey(s => s.DesiredShiftId)
              .OnDelete(DeleteBehavior.Restrict);

            // ── MESAİ TALEPLERİ ───────────────────────────────────────────
            mb.Entity<OvertimeRequest>()
              .HasOne(o => o.User)
              .WithMany()
              .HasForeignKey(o => o.UserId)
              .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<OvertimeRequest>()
              .HasOne(o => o.Shift)
              .WithMany()
              .HasForeignKey(o => o.ShiftId)
              .OnDelete(DeleteBehavior.Restrict);

            // ── SEED DATA ─────────────────────────────────────────────────
            // ── DEPARTMANLAR ──────────────────────────────────────────────
            // ShiftEx Konfeksiyon — orta ölçekli tekstil atölyesi
            mb.Entity<Department>().HasData(
                new Department { Id = 1, Name = "Kesim",            Description = "Kumaş kesim hattı" },
                new Department { Id = 2, Name = "Dikiş",            Description = "Dikiş atölyesi (en kalabalık bölüm)" },
                new Department { Id = 3, Name = "Ütü & Paketleme",  Description = "Ütüleme ve paketleme bandı" },
                new Department { Id = 4, Name = "Kalite Kontrol",   Description = "Ürün kalite muayene" },
                new Department { Id = 5, Name = "Sevkiyat",         Description = "Depo / sevkiyat hattı" }
            );

            mb.Entity<Shift>().HasData(
                new Shift { Id = 1, Name = "Sabah",                       StartTime = new TimeSpan(8,  0, 0), EndTime = new TimeSpan(16, 0, 0), Color = "#f59e0b" },
                new Shift { Id = 2, Name = "Öğleden Sonra",               StartTime = new TimeSpan(16, 0, 0), EndTime = new TimeSpan(0,  0, 0), Color = "#4f6ef7" },
                new Shift { Id = 3, Name = "Gece",                        StartTime = new TimeSpan(0,  0, 0), EndTime = new TimeSpan(8,  0, 0), Color = "#a78bfa" },
                new Shift { Id = 4, Name = "Tatil",                       StartTime = TimeSpan.Zero,          EndTime = TimeSpan.Zero,          Color = "#ef4444" },
                new Shift { Id = 5, Name = "İzinli",                      StartTime = TimeSpan.Zero,          EndTime = TimeSpan.Zero,          Color = "#22c55e" },
                new Shift { Id = 6, Name = "Part Time",                   StartTime = new TimeSpan(8,  0, 0), EndTime = new TimeSpan(12, 0, 0), Color = "#14b8a6" },
                new Shift { Id = 7, Name = "Sabah Fazla Mesai",           StartTime = new TimeSpan(16, 0, 0), EndTime = new TimeSpan(18, 0, 0), Color = "#f97316" },
                new Shift { Id = 8, Name = "Öğleden Sonra Fazla Mesai",  StartTime = new TimeSpan(22, 0, 0), EndTime = new TimeSpan(0,  0, 0), Color = "#6366f1" },
                new Shift { Id = 9, Name = "Gece Fazla Mesai",           StartTime = new TimeSpan(6,  0, 0), EndTime = new TimeSpan(8,  0, 0), Color = "#ec4899" }
            );

            // ── KULLANICILAR ──────────────────────────────────────────────
            // 1 admin (Üretim Müdürü) + 1 kadrolu personel (dikiş operatörü)
            // Yevmiyeci havuzu (Id 100-107): Kesim×2, Dikiş×3, Ütü&Pkt×1, Kalite×1, Sevkiyat×1
            // Tüm yevmiyeci password'ü demo amaçlı aynı: "Yevmiye123!"
            const string YEVMIYE_HASH  = "$2a$11$OtjPXY5sSdMc2Sagr6O8r.5j0a8vAlJROTuKr03GvMsmhL/G2P3Li";
            const string PERSONEL_HASH = "$2a$11$TVb1Y7cHcpUJlrAQLLs.ueVrdsfZuPRbUOSYI7o2KTYtTgOqzDfhS";
            var yevmiyeHireDate = new DateTime(2024, 1, 15);

            mb.Entity<User>().HasData(
                new User
                {
                    Id             = 1,
                    FullName       = "Ayşe Yılmaz",
                    Email          = "admin@shifttrack.com",
                    PasswordHash   = "$2a$11$E.YPlfQB/vm9Ef/cni.wROw1JGbmwwGVCWe7WI4LWCxMY0fjZYNnu",
                    Role           = "Admin",
                    EmploymentType = "Permanent",
                    DepartmentId   = null,
                    Position       = "Üretim Müdürü",
                    IsActive       = true,
                    CreatedAt      = new DateTime(2020, 3, 1),
                    HireDate       = new DateTime(2020, 3, 1)
                },
                new User
                {
                    Id             = 2,
                    FullName       = "Mehmet Kaya",
                    Email          = "mehmet@shifttrack.com",
                    PasswordHash   = "$2a$11$wfZHxZ2X.NMhZdirV1rJDO6FR6svu2Ll9MnlWG5Nbl.T4.1AYtmLG",
                    Role           = "Employee",
                    EmploymentType = "Permanent",
                    DepartmentId   = 2, // Dikiş
                    Position       = "Dikiş Operatörü",
                    IsActive       = true,
                    CreatedAt      = new DateTime(2022, 6, 15),
                    HireDate       = new DateTime(2022, 6, 15)
                },
                // ── YEVMİYECİ HAVUZU ────────────────────────────────────────
                // Kesim — 2 kişi
                new User { Id = 100, FullName = "Hasan Demir",     Email = "hasan.demir@shifttrack.com",     PasswordHash = YEVMIYE_HASH, Role = "Employee", EmploymentType = "Casual", DepartmentId = 1, Position = "Yevmiyeci · Kesim",            DailyWage = 850m, IsActive = true, CreatedAt = yevmiyeHireDate, HireDate = yevmiyeHireDate },
                new User { Id = 101, FullName = "Murat Aksoy",     Email = "murat.aksoy@shifttrack.com",     PasswordHash = YEVMIYE_HASH, Role = "Employee", EmploymentType = "Casual", DepartmentId = 1, Position = "Yevmiyeci · Kesim",            DailyWage = 850m, IsActive = true, CreatedAt = yevmiyeHireDate, HireDate = yevmiyeHireDate },
                // Dikiş — 3 kişi (en kalabalık)
                new User { Id = 102, FullName = "Fatma Şahin",     Email = "fatma.sahin@shifttrack.com",     PasswordHash = YEVMIYE_HASH, Role = "Employee", EmploymentType = "Casual", DepartmentId = 2, Position = "Yevmiyeci · Dikiş",            DailyWage = 800m, IsActive = true, CreatedAt = yevmiyeHireDate, HireDate = yevmiyeHireDate },
                new User { Id = 103, FullName = "Zeynep Aydın",    Email = "zeynep.aydin@shifttrack.com",    PasswordHash = YEVMIYE_HASH, Role = "Employee", EmploymentType = "Casual", DepartmentId = 2, Position = "Yevmiyeci · Dikiş",            DailyWage = 800m, IsActive = true, CreatedAt = yevmiyeHireDate, HireDate = yevmiyeHireDate },
                new User { Id = 104, FullName = "Emine Çelik",     Email = "emine.celik@shifttrack.com",     PasswordHash = YEVMIYE_HASH, Role = "Employee", EmploymentType = "Casual", DepartmentId = 2, Position = "Yevmiyeci · Dikiş",            DailyWage = 800m, IsActive = true, CreatedAt = yevmiyeHireDate, HireDate = yevmiyeHireDate },
                // Ütü & Paketleme — 1 kişi
                new User { Id = 105, FullName = "Sibel Polat",     Email = "sibel.polat@shifttrack.com",     PasswordHash = YEVMIYE_HASH, Role = "Employee", EmploymentType = "Casual", DepartmentId = 3, Position = "Yevmiyeci · Ütü & Paketleme",  DailyWage = 750m, IsActive = true, CreatedAt = yevmiyeHireDate, HireDate = yevmiyeHireDate },
                // Kalite Kontrol — 1 kişi
                new User { Id = 106, FullName = "Burak Öztürk",    Email = "burak.ozturk@shifttrack.com",    PasswordHash = YEVMIYE_HASH, Role = "Employee", EmploymentType = "Casual", DepartmentId = 4, Position = "Yevmiyeci · Kalite Kontrol",   DailyWage = 900m, IsActive = true, CreatedAt = yevmiyeHireDate, HireDate = yevmiyeHireDate },
                // Sevkiyat — 1 kişi
                new User { Id = 107, FullName = "Selim Kurt",      Email = "selim.kurt@shifttrack.com",      PasswordHash = YEVMIYE_HASH, Role = "Employee", EmploymentType = "Casual", DepartmentId = 5, Position = "Yevmiyeci · Sevkiyat",         DailyWage = 850m, IsActive = true, CreatedAt = yevmiyeHireDate, HireDate = yevmiyeHireDate },

                // ── KADROLU PERSONEL (20 kişi) ──────────────────────────────
                // Tüm kadrolu personel password'ü demo amaçlı aynı: "Personel123!"
                // Kesim (4) · Dikiş (6) · Ütü & Paketleme (4) · Kalite Kontrol (3) · Sevkiyat (3)
                // Kesim — DepartmentId 1
                new User { Id = 3,  FullName = "Ali Vural",        Email = "ali.vural@shifttrack.com",       PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 1, Position = "Kesim Ustası",       IsActive = true, CreatedAt = new DateTime(2021, 3, 10), HireDate = new DateTime(2021, 3, 10) },
                new User { Id = 4,  FullName = "Cemal Doğan",      Email = "cemal.dogan@shifttrack.com",     PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 1, Position = "Kesim Operatörü",    IsActive = true, CreatedAt = new DateTime(2022, 7, 1),  HireDate = new DateTime(2022, 7, 1)  },
                new User { Id = 5,  FullName = "Hakan Arslan",     Email = "hakan.arslan@shifttrack.com",    PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 1, Position = "Kesim Operatörü",    IsActive = true, CreatedAt = new DateTime(2023, 1, 16), HireDate = new DateTime(2023, 1, 16) },
                new User { Id = 6,  FullName = "Osman Yıldırım",   Email = "osman.yildirim@shifttrack.com",  PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 1, Position = "Kesim Operatörü",    IsActive = true, CreatedAt = new DateTime(2023, 9, 4),  HireDate = new DateTime(2023, 9, 4)  },
                // Dikiş — DepartmentId 2 (en kalabalık bölüm)
                new User { Id = 7,  FullName = "Elif Şimşek",      Email = "elif.simsek@shifttrack.com",     PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 2, Position = "Dikiş Ustası",      IsActive = true, CreatedAt = new DateTime(2020, 11, 2), HireDate = new DateTime(2020, 11, 2) },
                new User { Id = 8,  FullName = "Derya Koç",        Email = "derya.koc@shifttrack.com",       PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 2, Position = "Dikiş Operatörü",    IsActive = true, CreatedAt = new DateTime(2021, 6, 14), HireDate = new DateTime(2021, 6, 14) },
                new User { Id = 9,  FullName = "Gül Erdoğan",      Email = "gul.erdogan@shifttrack.com",     PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 2, Position = "Overlok Operatörü",  IsActive = true, CreatedAt = new DateTime(2022, 2, 21), HireDate = new DateTime(2022, 2, 21) },
                new User { Id = 10, FullName = "Hatice Aslan",     Email = "hatice.aslan@shifttrack.com",    PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 2, Position = "Dikiş Operatörü",    IsActive = true, CreatedAt = new DateTime(2022, 10, 3),HireDate = new DateTime(2022, 10, 3) },
                new User { Id = 11, FullName = "Meryem Taş",       Email = "meryem.tas@shifttrack.com",      PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 2, Position = "Dikiş Operatörü",    IsActive = true, CreatedAt = new DateTime(2023, 4, 17), HireDate = new DateTime(2023, 4, 17) },
                new User { Id = 12, FullName = "Nurten Acar",      Email = "nurten.acar@shifttrack.com",     PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 2, Position = "Düğme/İlik Operatörü",IsActive = true, CreatedAt = new DateTime(2023, 11, 6),HireDate = new DateTime(2023, 11, 6) },
                // Ütü & Paketleme — DepartmentId 3
                new User { Id = 13, FullName = "Kemal Şen",        Email = "kemal.sen@shifttrack.com",       PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 3, Position = "Ütü Ustası",        IsActive = true, CreatedAt = new DateTime(2021, 8, 9),  HireDate = new DateTime(2021, 8, 9)  },
                new User { Id = 14, FullName = "Ramazan Bulut",    Email = "ramazan.bulut@shifttrack.com",   PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 3, Position = "Ütücü",            IsActive = true, CreatedAt = new DateTime(2022, 5, 23), HireDate = new DateTime(2022, 5, 23) },
                new User { Id = 15, FullName = "Yasemin Toprak",   Email = "yasemin.toprak@shifttrack.com",  PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 3, Position = "Paketleme Elemanı",  IsActive = true, CreatedAt = new DateTime(2023, 2, 13), HireDate = new DateTime(2023, 2, 13) },
                new User { Id = 16, FullName = "Hülya Çetin",      Email = "hulya.cetin@shifttrack.com",     PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 3, Position = "Paketleme Elemanı",  IsActive = true, CreatedAt = new DateTime(2024, 1, 8),  HireDate = new DateTime(2024, 1, 8)  },
                // Kalite Kontrol — DepartmentId 4
                new User { Id = 17, FullName = "Serkan Korkmaz",   Email = "serkan.korkmaz@shifttrack.com",  PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 4, Position = "Kalite Şefi",       IsActive = true, CreatedAt = new DateTime(2020, 9, 28), HireDate = new DateTime(2020, 9, 28) },
                new User { Id = 18, FullName = "Aylin Güneş",      Email = "aylin.gunes@shifttrack.com",     PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 4, Position = "Kalite Kontrolör",  IsActive = true, CreatedAt = new DateTime(2022, 3, 7),  HireDate = new DateTime(2022, 3, 7)  },
                new User { Id = 19, FullName = "Pınar Yalçın",     Email = "pinar.yalcin@shifttrack.com",    PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 4, Position = "Kalite Kontrolör",  IsActive = true, CreatedAt = new DateTime(2023, 7, 19), HireDate = new DateTime(2023, 7, 19) },
                // Sevkiyat — DepartmentId 5
                new User { Id = 20, FullName = "Tolga Eren",       Email = "tolga.eren@shifttrack.com",      PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 5, Position = "Sevkiyat Sorumlusu", IsActive = true, CreatedAt = new DateTime(2021, 5, 11), HireDate = new DateTime(2021, 5, 11) },
                new User { Id = 21, FullName = "Erhan Avcı",       Email = "erhan.avci@shifttrack.com",      PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 5, Position = "Depo Görevlisi",     IsActive = true, CreatedAt = new DateTime(2022, 12, 5),HireDate = new DateTime(2022, 12, 5) },
                new User { Id = 22, FullName = "Volkan Kaplan",    Email = "volkan.kaplan@shifttrack.com",   PasswordHash = PERSONEL_HASH, Role = "Employee", EmploymentType = "Permanent", DepartmentId = 5, Position = "Depo Görevlisi",     IsActive = true, CreatedAt = new DateTime(2023, 6, 26), HireDate = new DateTime(2023, 6, 26) }
            );
        }
    }
}
