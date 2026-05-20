using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Models;
using ShiftTrackingApp.Services.Interfaces;

namespace ShiftTrackingApp.Services
{
    /// <summary>
    /// Yevmiyeci çağrı akışı: uygun yevmiyeci bulma, çağrı gönderme,
    /// kabul/red (kabulde otomatik vardiya ataması).
    /// </summary>
    public class CasualCalloutService : ICasualCalloutService
    {
        private readonly AppDbContext _db;
        private readonly INotificationService _notify;

        public CasualCalloutService(AppDbContext db, INotificationService notify)
        {
            _db = db;
            _notify = notify;
        }

        public async Task<List<EligibleCasualDto>> GetEligibleAsync(int departmentId, int shiftId, DateOnly date)
        {
            // O gün herhangi bir vardiyaya atanmış kullanıcılar (çift atama engeli)
            var assignedUserIds = await _db.ShiftAssignments
                .Where(a => a.Date == date)
                .Select(a => a.UserId)
                .ToListAsync();

            // O gün için zaten aktif çağrısı (Sent/Accepted) olan yevmiyeciler
            var calledUserIds = await _db.CasualCallouts
                .Where(c => c.Date == date && (c.Status == "Sent" || c.Status == "Accepted"))
                .Select(c => c.CalledUserId)
                .ToListAsync();

            var busy = assignedUserIds.Concat(calledUserIds).ToHashSet();

            return await _db.Users
                .Where(u => u.IsActive
                         && u.EmploymentType == "Casual"
                         && u.DepartmentId == departmentId
                         && !busy.Contains(u.Id))
                .OrderBy(u => u.FullName)
                .Select(u => new EligibleCasualDto
                {
                    UserId      = u.Id,
                    FullName    = u.FullName,
                    Position    = u.Position,
                    PhotoBase64 = u.PhotoBase64,
                })
                .ToListAsync();
        }

        public async Task<CasualCalloutDto> CreateAsync(CreateCasualCalloutDto dto, int adminId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == dto.CalledUserId && u.IsActive)
                ?? throw new KeyNotFoundException("Yevmiyeci bulunamadı.");

            if (user.EmploymentType != "Casual")
                throw new InvalidOperationException("Yalnızca yevmiyeci personel çağrılabilir.");

            // Aynı gün için bu yevmiyecide açık çağrı / atama var mı?
            bool alreadyCalled = await _db.CasualCallouts.AnyAsync(c =>
                c.CalledUserId == dto.CalledUserId && c.Date == dto.Date &&
                (c.Status == "Sent" || c.Status == "Accepted"));
            if (alreadyCalled)
                throw new InvalidOperationException("Bu yevmiyeciye o gün için zaten bir çağrı var.");

            bool alreadyAssigned = await _db.ShiftAssignments.AnyAsync(a =>
                a.UserId == dto.CalledUserId && a.Date == dto.Date);
            if (alreadyAssigned)
                throw new InvalidOperationException("Bu yevmiyecinin o gün zaten bir vardiyası var.");

            var callout = new CasualCallout
            {
                DepartmentId = dto.DepartmentId,
                ShiftId      = dto.ShiftId,
                Date         = dto.Date,
                CalledUserId = dto.CalledUserId,
                Note         = dto.Note,
                Status       = "Sent",
                CreatedBy    = adminId,
                CreatedAt    = DateTime.UtcNow,
            };
            _db.CasualCallouts.Add(callout);
            await _db.SaveChangesAsync();

            // Bilgilendirme (log tabanlı; ileride SMS/push ile değiştirilebilir)
            var shift = await _db.Shifts.FindAsync(dto.ShiftId);
            await _notify.NotifyShiftAssignedAsync(dto.CalledUserId, dto.Date, shift?.Name ?? "Vardiya");

            return await BuildDtoAsync(callout.Id);
        }

        public async Task<List<CasualCalloutDto>> GetMineAsync(int userId)
        {
            return await Query()
                .Where(c => c.CalledUserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => ToDto(c))
                .ToListAsync();
        }

        public async Task<List<CasualCalloutDto>> GetByDateAsync(DateOnly date)
        {
            return await Query()
                .Where(c => c.Date == date)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => ToDto(c))
                .ToListAsync();
        }

        public async Task<CasualCalloutDto> RespondAsync(int calloutId, int userId, bool accept)
        {
            var callout = await _db.CasualCallouts.FirstOrDefaultAsync(c => c.Id == calloutId)
                ?? throw new KeyNotFoundException("Çağrı bulunamadı.");

            if (callout.CalledUserId != userId)
                throw new UnauthorizedAccessException("Bu çağrı size ait değil.");

            if (callout.Status != "Sent")
                throw new InvalidOperationException("Bu çağrı zaten yanıtlanmış.");

            callout.Status      = accept ? "Accepted" : "Rejected";
            callout.RespondedAt = DateTime.UtcNow;

            if (accept)
            {
                // Çift atama emniyeti: aynı gün/vardiya kaydı yoksa oluştur
                bool exists = await _db.ShiftAssignments.AnyAsync(a =>
                    a.UserId == userId && a.Date == callout.Date && a.ShiftId == callout.ShiftId);
                if (!exists)
                {
                    _db.ShiftAssignments.Add(new ShiftAssignment
                    {
                        UserId    = userId,
                        ShiftId   = callout.ShiftId,
                        Date      = callout.Date,
                        Note      = "Yevmiyeci çağrısı ile atandı",
                        CreatedAt = DateTime.UtcNow,
                    });
                }
            }

            await _db.SaveChangesAsync();
            return await BuildDtoAsync(callout.Id);
        }

        // ── Yardımcılar ──────────────────────────────────────────────────
        private IQueryable<CasualCallout> Query() => _db.CasualCallouts
            .Include(c => c.Department)
            .Include(c => c.Shift)
            .Include(c => c.CalledUser);

        private async Task<CasualCalloutDto> BuildDtoAsync(int id)
        {
            var c = await Query().FirstAsync(x => x.Id == id);
            return ToDto(c);
        }

        private static CasualCalloutDto ToDto(CasualCallout c) => new()
        {
            Id             = c.Id,
            DepartmentId   = c.DepartmentId,
            DepartmentName = c.Department.Name,
            ShiftId        = c.ShiftId,
            ShiftName      = c.Shift.Name,
            ShiftColor     = c.Shift.Color,
            StartTime      = c.Shift.StartTime.ToString(@"hh\:mm"),
            EndTime        = c.Shift.EndTime.ToString(@"hh\:mm"),
            Date           = c.Date,
            CalledUserId   = c.CalledUserId,
            CalledUserName = c.CalledUser.FullName,
            Status         = c.Status,
            Note           = c.Note,
            CreatedAt      = c.CreatedAt,
            RespondedAt    = c.RespondedAt,
        };
    }
}
