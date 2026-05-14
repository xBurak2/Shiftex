using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Helpers;
using ShiftTrackingApp.Models;
using ShiftTrackingApp.Services.Interfaces;

namespace ShiftTrackingApp.Services
{
    public class LeaveService : ILeaveService
    {
        private readonly AppDbContext _db;
        public LeaveService(AppDbContext db) => _db = db;

        public async Task<List<LeaveRequestDto>> GetAllAsync(string? status = null)
        {
            // Performans: DocumentBytes'ı list sorgularında çekme — sadece HasDocument'i türet
            var q = _db.LeaveRequests.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(status)) q = q.Where(l => l.Status == status);
            return await q.OrderByDescending(l => l.CreatedAt).Select(l => new LeaveRequestDto
            {
                Id                  = l.Id,
                UserId              = l.UserId,
                UserFullName        = l.User.FullName,
                LeaveType           = l.LeaveType,
                StartDate           = l.StartDate,
                EndDate             = l.EndDate,
                Description         = l.Description,
                HasMedicalReport    = l.HasMedicalReport,
                Status              = l.Status,
                CreatedAt           = l.CreatedAt,
                HasDocument         = l.DocumentBytes != null && l.DocumentBytes.Length > 0,
                DocumentFileName    = l.DocumentFileName,
                DocumentContentType = l.DocumentContentType
            }).ToListAsync();
        }

        public async Task<List<LeaveRequestDto>> GetByUserAsync(int userId)
            => await _db.LeaveRequests
                .AsNoTracking()
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new LeaveRequestDto
                {
                    Id                  = l.Id,
                    UserId              = l.UserId,
                    UserFullName        = l.User.FullName,
                    LeaveType           = l.LeaveType,
                    StartDate           = l.StartDate,
                    EndDate             = l.EndDate,
                    Description         = l.Description,
                    HasMedicalReport    = l.HasMedicalReport,
                    Status              = l.Status,
                    CreatedAt           = l.CreatedAt,
                    HasDocument         = l.DocumentBytes != null && l.DocumentBytes.Length > 0,
                    DocumentFileName    = l.DocumentFileName,
                    DocumentContentType = l.DocumentContentType
                })
                .ToListAsync();

        public async Task<LeaveRequestDto> CreateAsync(int userId, CreateLeaveRequestDto dto)
        {
            if (dto.EndDate < dto.StartDate)
                throw new ArgumentException("Bitiş tarihi başlangıç tarihinden önce olamaz.");

            byte[]? docBytes = null;
            string? docName  = null;
            string? docType  = null;
            if (!string.IsNullOrWhiteSpace(dto.DocumentBase64))
            {
                // Frontend "data:application/pdf;base64,..." veya saf base64 gönderebilir
                var raw = dto.DocumentBase64;
                var commaIdx = raw.IndexOf(',');
                if (raw.StartsWith("data:") && commaIdx > 0) raw = raw[(commaIdx + 1)..];
                try
                {
                    docBytes = Convert.FromBase64String(raw);
                }
                catch (FormatException)
                {
                    throw new ArgumentException("Geçersiz belge formatı. Lütfen geçerli bir dosya yükleyin.");
                }
                if (docBytes.Length > 5 * 1024 * 1024)
                    throw new ArgumentException("Belge boyutu en fazla 5 MB olabilir.");

                // İzin verilen content-type'lar
                docType = dto.DocumentContentType?.ToLowerInvariant();
                var allowed = new[] { "application/pdf", "image/jpeg", "image/jpg", "image/png" };
                if (string.IsNullOrEmpty(docType) || Array.IndexOf(allowed, docType) < 0)
                    throw new ArgumentException("Sadece PDF, JPG veya PNG dosyaları kabul edilir.");

                docName = string.IsNullOrWhiteSpace(dto.DocumentFileName) ? "belge" : dto.DocumentFileName;
            }

            var leave = new LeaveRequest
            {
                UserId              = userId,
                LeaveType           = dto.LeaveType,
                StartDate           = dto.StartDate,
                EndDate             = dto.EndDate,
                Description         = dto.Description,
                HasMedicalReport    = dto.HasMedicalReport,
                Status              = "Pending",
                CreatedAt           = DateTime.UtcNow,
                DocumentBytes       = docBytes,
                DocumentFileName    = docName,
                DocumentContentType = docType
            };
            _db.LeaveRequests.Add(leave);
            await _db.SaveChangesAsync();
            await _db.Entry(leave).Reference(l => l.User).LoadAsync();
            return ToDto(leave);
        }

        public async Task<(byte[] Bytes, string FileName, string ContentType)?> GetDocumentAsync(int id, int viewerId, bool isAdmin)
        {
            var leave = await _db.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
            if (leave == null || leave.DocumentBytes == null) return null;
            // Sadece admin veya talep sahibi indirebilir
            if (!isAdmin && leave.UserId != viewerId) return null;
            return (leave.DocumentBytes, leave.DocumentFileName ?? "belge", leave.DocumentContentType ?? "application/octet-stream");
        }

        public async Task<LeaveRequestDto?> ReviewAsync(int id, int reviewerId, ReviewLeaveDto dto)
        {
            var leave = await _db.LeaveRequests.Include(l => l.User).FirstOrDefaultAsync(l => l.Id == id);
            if (leave == null) return null;

            leave.Status     = dto.Status;
            leave.ReviewedBy = reviewerId;
            leave.ReviewedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return ToDto(leave);
        }

        private static LeaveRequestDto ToDto(LeaveRequest l) => new()
        {
            Id                  = l.Id,
            UserId              = l.UserId,
            UserFullName        = l.User.FullName,
            LeaveType           = l.LeaveType,
            StartDate           = l.StartDate,
            EndDate             = l.EndDate,
            Description         = l.Description,
            HasMedicalReport    = l.HasMedicalReport,
            Status              = l.Status,
            CreatedAt           = l.CreatedAt,
            HasDocument         = l.DocumentBytes != null && l.DocumentBytes.Length > 0,
            DocumentFileName    = l.DocumentFileName,
            DocumentContentType = l.DocumentContentType
        };
    }

    public class AttendanceService : IAttendanceService
    {
        private readonly AppDbContext _db;
        public AttendanceService(AppDbContext db) => _db = db;

        public async Task<List<AttendanceLogDto>> GetTodayAsync()
        {
            var (utcStart, utcEnd) = TodayUtcRange();
            var logs = await _db.AttendanceLogs
                .Include(a => a.User)
                .Where(a => a.CheckIn >= utcStart && a.CheckIn < utcEnd && a.User.IsActive)
                .OrderByDescending(a => a.CheckIn)
                .ToListAsync();
            return await EnrichWithShift(logs, DateOnly.FromDateTime(DateTime.Today));
        }

        public async Task<List<AttendanceLogDto>> GetByUserTodayAsync(int userId)
        {
            var (utcStart, utcEnd) = TodayUtcRange();
            var logs = await _db.AttendanceLogs
                .Include(a => a.User)
                .Where(a => a.UserId == userId && a.CheckIn >= utcStart && a.CheckIn < utcEnd)
                .OrderByDescending(a => a.CheckIn)
                .ToListAsync();
            return await EnrichWithShift(logs, DateOnly.FromDateTime(DateTime.Today));
        }

        public async Task<AttendanceLogDto> CheckInAsync(int userId, string source = "Manual")
        {
            var (utcStart, utcEnd) = TodayUtcRange();
            var existing = await _db.AttendanceLogs
                .Where(a => a.UserId == userId && a.CheckIn >= utcStart && a.CheckIn < utcEnd && a.CheckOut == null)
                .FirstOrDefaultAsync();
            if (existing != null)
                throw new InvalidOperationException("Zaten aktif bir giriş kaydınız bulunuyor.");

            var log = new AttendanceLog
            {
                UserId  = userId,
                CheckIn = DateTime.UtcNow,
                Source  = source
            };
            _db.AttendanceLogs.Add(log);
            await _db.SaveChangesAsync();
            await _db.Entry(log).Reference(a => a.User).LoadAsync();
            var today = DateOnly.FromDateTime(DateTime.Today);
            var list  = await EnrichWithShift(new List<AttendanceLog> { log }, today);
            return list.First();
        }

        public async Task<AttendanceLogDto?> CheckOutAsync(int userId, string source = "Manual")
        {
            var (utcStart, utcEnd) = TodayUtcRange();
            var log = await _db.AttendanceLogs
                .Include(a => a.User)
                .Where(a => a.UserId == userId && a.CheckIn >= utcStart && a.CheckIn < utcEnd && a.CheckOut == null)
                .FirstOrDefaultAsync();
            if (log == null) return null;

            log.CheckOut = DateTime.UtcNow;
            if (source != "Manual") log.Source = source;
            await _db.SaveChangesAsync();
            var today = DateOnly.FromDateTime(DateTime.Today);
            var list  = await EnrichWithShift(new List<AttendanceLog> { log }, today);
            return list.First();
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            var (utcStart, utcEnd) = TodayUtcRange();
            var today              = DateOnly.FromDateTime(DateTime.Today);

            var totalActive  = await _db.Users.CountAsync(u => u.IsActive);
            var presentToday = await _db.AttendanceLogs
                .Where(a => a.CheckIn >= utcStart && a.CheckIn < utcEnd && a.User.IsActive)
                .Select(a => a.UserId).Distinct().CountAsync();
            var onLeave = await _db.LeaveRequests
                .CountAsync(l => l.Status == "Approved" && l.StartDate <= today && l.EndDate >= today
                    && l.User.IsActive);
            var pending = await _db.LeaveRequests.CountAsync(l => l.Status == "Pending");

            return new DashboardStatsDto
            {
                TotalActiveEmployees = totalActive,
                PresentToday         = presentToday,
                OnLeaveToday         = onLeave,
                AbsentToday          = Math.Max(0, totalActive - presentToday - onLeave),
                PendingLeaveRequests = pending
            };
        }

        // ── Yardımcılar ──────────────────────────────────────────────────
        private static (DateTime utcStart, DateTime utcEnd) TodayUtcRange()
        {
            var tz         = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            var turkeyNow  = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var todayTr    = turkeyNow.Date;
            var utcStart   = TimeZoneInfo.ConvertTimeToUtc(todayTr, tz);
            var utcEnd     = utcStart.AddDays(1);
            return (utcStart, utcEnd);
        }

        private async Task<List<AttendanceLogDto>> EnrichWithShift(
            List<AttendanceLog> logs, DateOnly date)
        {
            if (!logs.Any()) return new List<AttendanceLogDto>();

            var userIds    = logs.Select(l => l.UserId).Distinct().ToList();
            var assignments = await _db.ShiftAssignments
                .Include(sa => sa.Shift)
                .Where(sa => userIds.Contains(sa.UserId) && sa.Date == date)
                .ToListAsync();

            return logs.Select(a =>
            {
                var assignment    = assignments.FirstOrDefault(sa => sa.UserId == a.UserId);
                var shift         = assignment?.Shift;
                var checkInTr     = TimeZoneHelper.ConvertToTurkeyTime(a.CheckIn);
                int lateMin       = 0;
                int earlyMin      = 0;
                bool isLate       = false;
                bool isEarly      = false;
                bool isInvalid    = false;

                if (shift != null)
                {
                    var shiftStart = checkInTr.Date + shift.StartTime;
                    var diff       = (checkInTr - shiftStart).TotalMinutes;
                    if (diff > 5)  { isLate = true;  lateMin = (int)diff; }

                    if (a.CheckOut.HasValue && shift.EndTime != TimeSpan.Zero)
                    {
                        var checkOutTr = TimeZoneHelper.ConvertToTurkeyTime(a.CheckOut.Value);
                        var shiftEnd   = checkInTr.Date + shift.EndTime;
                        var earlyDiff  = (shiftEnd - checkOutTr).TotalMinutes;
                        if (earlyDiff > 5) { isEarly = true; earlyMin = (int)earlyDiff; }
                    }

                    // Vardiya saatleri dışında giriş (±2 saat tolerans)
                    isInvalid = Math.Abs((checkInTr - (checkInTr.Date + shift.StartTime)).TotalHours) > 2;
                }

                double? workedHours = a.CheckOut.HasValue
                    ? Math.Round((a.CheckOut.Value - a.CheckIn).TotalHours, 2)
                    : null;

                // Kısa çalışma: 1 saatten az (checkout varsa)
                bool isShort = a.CheckOut.HasValue && workedHours < 1;

                return new AttendanceLogDto
                {
                    Id              = a.Id,
                    UserId          = a.UserId,
                    UserFullName    = a.User.FullName,
                    UserPhoto       = a.User.PhotoBase64,
                    CheckIn         = checkInTr,
                    CheckOut        = a.CheckOut.HasValue
                                          ? TimeZoneHelper.ConvertToTurkeyTime(a.CheckOut.Value)
                                          : null,
                    Source          = a.Source,
                    Note            = a.Note,
                    IsLateArrival   = isLate,
                    LateMinutes     = lateMin,
                    IsEarlyDeparture = isEarly,
                    EarlyMinutes    = earlyMin,
                    IsInvalidTime   = isInvalid,
                    IsShortDuration = isShort,
                    WorkedHours     = workedHours
                };
            }).ToList();
        }
    }
}
