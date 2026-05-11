using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Models;

namespace ShiftTrackingApp.Services
{
    public interface IOvertimeRequestService
    {
        Task<List<OvertimeRequestDto>> GetMyAsync(int userId);
        Task<List<OvertimeRequestDto>> GetAllAsync(string? status = null);
        Task<OvertimeRequestDto> CreateAsync(int userId, CreateOvertimeRequestDto dto);
        Task<OvertimeRequestDto?> ApproveAsync(int id, int reviewerId);
        Task<OvertimeRequestDto?> RejectAsync(int id, int reviewerId);
    }

    /// <summary>
    /// Personelin fazla mesai taleplerini yönetir.
    /// Onaylanan talepler otomatik olarak ShiftAssignment'a dönüşür.
    /// </summary>
    public class OvertimeRequestService : IOvertimeRequestService
    {
        private readonly AppDbContext _db;
        public OvertimeRequestService(AppDbContext db) => _db = db;

        public async Task<List<OvertimeRequestDto>> GetMyAsync(int userId)
            => await BaseQuery().Where(o => o.UserId == userId)
                                .OrderByDescending(o => o.CreatedAt)
                                .Select(o => ToDto(o)).ToListAsync();

        public async Task<List<OvertimeRequestDto>> GetAllAsync(string? status = null)
        {
            var q = BaseQuery();
            if (!string.IsNullOrEmpty(status)) q = q.Where(o => o.Status == status);
            return await q.OrderByDescending(o => o.CreatedAt)
                          .Select(o => ToDto(o)).ToListAsync();
        }

        public async Task<OvertimeRequestDto> CreateAsync(int userId, CreateOvertimeRequestDto dto)
        {
            // Aynı gün için bekleyen talep var mı?
            var dup = await _db.OvertimeRequests.AnyAsync(o =>
                o.UserId == userId && o.Date == dto.Date && o.Status == "Pending");
            if (dup) throw new InvalidOperationException("Bu güne zaten bekleyen bir mesai talebiniz var.");

            // FM shift mı?
            if (dto.ShiftId < 7 || dto.ShiftId > 9)
                throw new InvalidOperationException("Geçerli bir fazla mesai vardiyası seçin.");

            var req = new OvertimeRequest
            {
                UserId    = userId,
                Date      = dto.Date,
                ShiftId   = dto.ShiftId,
                Reason    = dto.Reason,
                Status    = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            _db.OvertimeRequests.Add(req);
            await _db.SaveChangesAsync();
            return await LoadDto(req.Id);
        }

        public async Task<OvertimeRequestDto?> ApproveAsync(int id, int reviewerId)
        {
            var req = await _db.OvertimeRequests.FindAsync(id);
            if (req == null) return null;
            if (req.Status != "Pending")
                throw new InvalidOperationException("Bu talep zaten karara bağlanmış.");

            // Aynı kullanıcı için aynı tarih ve aynı vardiya zaten atanmış mı?
            var exists = await _db.ShiftAssignments.AnyAsync(sa =>
                sa.UserId == req.UserId && sa.Date == req.Date && sa.ShiftId == req.ShiftId);

            if (!exists)
            {
                _db.ShiftAssignments.Add(new ShiftAssignment
                {
                    UserId    = req.UserId,
                    ShiftId   = req.ShiftId,
                    Date      = req.Date,
                    Note      = req.Reason ?? "Mesai talebi onayı",
                    CreatedAt = DateTime.UtcNow
                });
            }

            req.Status     = "Approved";
            req.ReviewedAt = DateTime.UtcNow;
            req.ReviewedBy = reviewerId;
            await _db.SaveChangesAsync();
            return await LoadDto(id);
        }

        public async Task<OvertimeRequestDto?> RejectAsync(int id, int reviewerId)
        {
            var req = await _db.OvertimeRequests.FindAsync(id);
            if (req == null) return null;
            if (req.Status != "Pending")
                throw new InvalidOperationException("Bu talep zaten karara bağlanmış.");
            req.Status     = "Rejected";
            req.ReviewedAt = DateTime.UtcNow;
            req.ReviewedBy = reviewerId;
            await _db.SaveChangesAsync();
            return await LoadDto(id);
        }

        // ── Helpers ────────────────────────────────────────────────────
        private IQueryable<OvertimeRequest> BaseQuery()
            => _db.OvertimeRequests
                  .Include(o => o.User)
                  .Include(o => o.Shift);

        private async Task<OvertimeRequestDto> LoadDto(int id)
            => ToDto(await BaseQuery().FirstAsync(o => o.Id == id));

        private static OvertimeRequestDto ToDto(OvertimeRequest o) => new()
        {
            Id             = o.Id,
            UserId         = o.UserId,
            UserName       = o.User.FullName,
            Date           = o.Date,
            ShiftId        = o.ShiftId,
            ShiftName      = o.Shift.Name,
            ShiftColor     = o.Shift.Color,
            ShiftStartTime = o.Shift.StartTime.ToString(@"hh\:mm"),
            ShiftEndTime   = o.Shift.EndTime.ToString(@"hh\:mm"),
            Reason         = o.Reason,
            Status         = o.Status,
            CreatedAt      = o.CreatedAt,
            ReviewedAt     = o.ReviewedAt
        };
    }
}
