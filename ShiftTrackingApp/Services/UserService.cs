using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Helpers;
using ShiftTrackingApp.Models;
using ShiftTrackingApp.Services.Interfaces;

namespace ShiftTrackingApp.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _db;
        public UserService(AppDbContext db) => _db = db;

        public async Task<PagedResult<UserDto>> GetAllAsync(int page = 1, int pageSize = 50)
        {
            // Sınır koruma
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _db.Users
                .Include(u => u.Department)
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => ToDto(u))
                .ToListAsync();

            return new PagedResult<UserDto>
            {
                Items      = items,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize
            };
        }

        public async Task<UserDto?> GetByIdAsync(int id)
        {
            var user = await _db.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == id);
            return user == null ? null : ToDto(user);
        }

        public async Task<UserDto> CreateAsync(CreateUserDto dto)
        {
            // Fotoğraf boyutu ön kontrolü burada değil UpdateAsync'te, ama e-posta tekrar kontrolü burada
            var emailExists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
            if (emailExists)
                throw new InvalidOperationException("Bu e-posta adresi zaten kullanılıyor.");

            var user = new User
            {
                FullName     = dto.FullName,
                Email        = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role         = dto.Role,
                DepartmentId = dto.DepartmentId,
                Position     = dto.Position,
                HireDate     = dto.HireDate,
                PhoneNumber  = dto.PhoneNumber,
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            var saved = await _db.Users.Include(u => u.Department).FirstAsync(u => u.Id == user.Id);
            return ToDto(saved);
        }

        public async Task<UserDto?> UpdateAsync(int id, UpdateUserDto dto)
        {
            var user = await _db.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return null;

            // Fotoğraf boyut limiti: ~375 KB (Base64 yaklaşık %33 büyür → 500_000 char ≈ 375 KB)
            if (dto.PhotoBase64 != null && dto.PhotoBase64.Length > 500_000)
                throw new ArgumentException("Fotoğraf boyutu 375 KB sınırını aşıyor. Lütfen daha küçük bir görsel seçin.");

            if (dto.FullName    != null)         user.FullName     = dto.FullName;
            if (dto.Email       != null)         user.Email        = dto.Email;
            if (dto.Role        != null)         user.Role         = dto.Role;
            if (dto.DepartmentId.HasValue)       user.DepartmentId = dto.DepartmentId;
            if (dto.Position    != null)         user.Position     = dto.Position;
            if (dto.HireDate.HasValue)           user.HireDate     = dto.HireDate;
            if (dto.PhoneNumber != null)         user.PhoneNumber  = dto.PhoneNumber;
            if (dto.PhotoBase64 != null)         user.PhotoBase64  = dto.PhotoBase64;
            if (!string.IsNullOrEmpty(dto.NewPassword))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

            await _db.SaveChangesAsync();
            await _db.Entry(user).Reference(u => u.Department).LoadAsync();
            return ToDto(user);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return false;

            // Soft delete
            user.IsActive = false;

            // Gelecekteki vardiya atamalarını iptal et
            var today        = DateOnly.FromDateTime(DateTime.Today);
            var futureShifts = await _db.ShiftAssignments
                .Where(sa => sa.UserId == id && sa.Date >= today)
                .ToListAsync();
            if (futureShifts.Count > 0)
                _db.ShiftAssignments.RemoveRange(futureShifts);

            // Bekleyen izin taleplerini reddet
            var pendingLeaves = await _db.LeaveRequests
                .Where(l => l.UserId == id && l.Status == "Pending")
                .ToListAsync();
            foreach (var leave in pendingLeaves)
            {
                leave.Status     = "Rejected";
                leave.ReviewedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<MonthlyAttendanceSummaryDto> GetMonthlyAttendanceSummaryAsync(int userId, int year, int month)
        {
            var turkeyStart = new DateTime(year, month, 1);
            var turkeyEnd   = turkeyStart.AddMonths(1);
            var tz          = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            var utcStart    = TimeZoneInfo.ConvertTimeToUtc(turkeyStart, tz);
            var utcEnd      = TimeZoneInfo.ConvertTimeToUtc(turkeyEnd, tz);

            var logs = await _db.AttendanceLogs
                .Where(a => a.UserId == userId && a.CheckIn >= utcStart && a.CheckIn < utcEnd)
                .ToListAsync();

            var approvedLeaves = await _db.LeaveRequests
                .Where(l => l.UserId == userId && l.Status == "Approved"
                    && l.StartDate <= DateOnly.FromDateTime(turkeyEnd.AddDays(-1))
                    && l.EndDate   >= DateOnly.FromDateTime(turkeyStart))
                .ToListAsync();

            int leaveDays = approvedLeaves.Sum(l =>
            {
                var ls          = l.StartDate.ToDateTime(TimeOnly.MinValue);
                var le          = l.EndDate.ToDateTime(TimeOnly.MinValue).AddDays(1);
                long overlapTck = Math.Min(le.Ticks, turkeyEnd.Ticks) - Math.Max(ls.Ticks, turkeyStart.Ticks);
                return overlapTck > 0 ? (int)(overlapTck / TimeSpan.TicksPerDay) : 0;
            });

            int presentDays         = logs.Select(l => TimeZoneHelper.ConvertToTurkeyTime(l.CheckIn).Date).Distinct().Count();
            int workDays            = GetWorkingDaysInMonth(year, month);
            int absentWithReport    = approvedLeaves.Count(l => l.HasMedicalReport);
            int absentWithoutReport = Math.Max(0, workDays - presentDays - leaveDays);

            var overtimeShiftIds    = new[] { 7, 8, 9 };
            var overtimeAssignments = await _db.ShiftAssignments
                .Include(sa => sa.Shift)
                .Where(sa => sa.UserId == userId
                    && overtimeShiftIds.Contains(sa.ShiftId)
                    && sa.Date.Year  == year
                    && sa.Date.Month == month)
                .ToListAsync();

            double overtimeHours = overtimeAssignments.Sum(sa =>
            {
                var dur = sa.Shift.EndTime - sa.Shift.StartTime;
                return dur.TotalHours < 0 ? dur.TotalHours + 24 : dur.TotalHours;
            });

            return new MonthlyAttendanceSummaryDto
            {
                Month               = month,
                Year                = year,
                PresentDays         = presentDays,
                LeaveDays           = leaveDays,
                AbsentWithReport    = absentWithReport,
                AbsentWithoutReport = absentWithoutReport,
                AbsentDays          = Math.Max(0, workDays - presentDays - leaveDays),
                TotalWorkedHours    = Math.Round(logs
                    .Where(l => l.CheckOut.HasValue)
                    .Sum(l => (l.CheckOut!.Value - l.CheckIn).TotalHours), 1),
                TotalOvertimeHours  = Math.Round(overtimeHours, 1),
                OvertimeShiftCount  = overtimeAssignments.Count,
            };
        }

        private static int GetWorkingDaysInMonth(int year, int month)
        {
            return Enumerable.Range(1, DateTime.DaysInMonth(year, month))
                .Select(d => new DateTime(year, month, d))
                .Count(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday);
        }

        private static UserDto ToDto(User u) => new()
        {
            Id             = u.Id,
            FullName       = u.FullName,
            Email          = u.Email,
            Role           = u.Role,
            DepartmentName = u.Department?.Name,
            DepartmentId   = u.DepartmentId,
            Position       = u.Position,
            HireDate       = u.HireDate,
            PhotoBase64    = u.PhotoBase64,
            PhoneNumber    = u.PhoneNumber,
            IsActive       = u.IsActive,
        };
    }
}
