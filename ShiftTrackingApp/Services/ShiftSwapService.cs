using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Models;

namespace ShiftTrackingApp.Services
{
    public interface IShiftSwapService
    {
        Task<List<ShiftSwapRequestDto>> GetMyOutgoingAsync(int userId);
        Task<List<ShiftSwapRequestDto>> GetMyIncomingAsync(int userId);
        Task<List<ShiftSwapRequestDto>> GetAllAsync(string? status = null);
        Task<ShiftSwapRequestDto> CreateAsync(int requesterId, CreateShiftSwapDto dto);
        Task<ShiftSwapRequestDto?> RespondAsync(int id, int userId, RespondShiftSwapDto dto);
        Task<ShiftSwapRequestDto?> ApproveAsync(int id, int reviewerId);
        Task<ShiftSwapRequestDto?> RejectAsync(int id, int reviewerId);
        Task<bool> CancelAsync(int id, int requesterId);
    }

    public class ShiftSwapService : IShiftSwapService
    {
        private readonly AppDbContext _db;
        public ShiftSwapService(AppDbContext db) => _db = db;

        public async Task<List<ShiftSwapRequestDto>> GetMyOutgoingAsync(int userId)
            => await BaseQuery().Where(s => s.RequesterId == userId)
                                .OrderByDescending(s => s.CreatedAt)
                                .Select(s => ToDto(s)).ToListAsync();

        public async Task<List<ShiftSwapRequestDto>> GetMyIncomingAsync(int userId)
            => await BaseQuery().Where(s => s.TargetUserId == userId)
                                .OrderByDescending(s => s.CreatedAt)
                                .Select(s => ToDto(s)).ToListAsync();

        public async Task<List<ShiftSwapRequestDto>> GetAllAsync(string? status = null)
        {
            var q = BaseQuery();
            if (!string.IsNullOrEmpty(status)) q = q.Where(s => s.Status == status);
            return await q.OrderByDescending(s => s.CreatedAt)
                          .Select(s => ToDto(s)).ToListAsync();
        }

        public async Task<ShiftSwapRequestDto> CreateAsync(int requesterId, CreateShiftSwapDto dto)
        {
            // Talep edilen shift assignment gerçekten requester'a mı ait?
            var myAssignment = await _db.ShiftAssignments
                .FirstOrDefaultAsync(sa => sa.Id == dto.RequesterShiftAssignmentId && sa.UserId == requesterId)
                ?? throw new InvalidOperationException("Belirtilen vardiya size ait değil.");

            // Hedef shift assignment hedef kullanıcıya mı ait?
            if (dto.TargetShiftAssignmentId.HasValue)
            {
                var targetAssignment = await _db.ShiftAssignments
                    .FirstOrDefaultAsync(sa => sa.Id == dto.TargetShiftAssignmentId.Value
                                             && sa.UserId == dto.TargetUserId)
                    ?? throw new InvalidOperationException("Hedef vardiya hedef personele ait değil.");
            }

            // Aynı vardiya için bekleyen başka talep var mı?
            var dup = await _db.ShiftSwapRequests.AnyAsync(s =>
                s.RequesterShiftAssignmentId == dto.RequesterShiftAssignmentId
                && (s.Status == "Pending" || s.Status == "AcceptedByTarget"));
            if (dup) throw new InvalidOperationException("Bu vardiya için zaten bekleyen bir değişim talebi var.");

            var req = new ShiftSwapRequest
            {
                RequesterId               = requesterId,
                RequesterShiftAssignmentId= dto.RequesterShiftAssignmentId,
                TargetUserId              = dto.TargetUserId,
                TargetShiftAssignmentId   = dto.TargetShiftAssignmentId,
                Reason                    = dto.Reason,
                Status                    = "Pending",
                CreatedAt                 = DateTime.UtcNow
            };
            _db.ShiftSwapRequests.Add(req);
            await _db.SaveChangesAsync();
            return await LoadDto(req.Id);
        }

        public async Task<ShiftSwapRequestDto?> RespondAsync(int id, int userId, RespondShiftSwapDto dto)
        {
            var req = await _db.ShiftSwapRequests.FindAsync(id);
            if (req == null || req.TargetUserId != userId) return null;
            if (req.Status != "Pending") throw new InvalidOperationException("Bu talep zaten yanıtlanmış.");

            req.Status      = dto.Response == "Accept" ? "AcceptedByTarget" : "RejectedByTarget";
            req.RespondedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return await LoadDto(id);
        }

        public async Task<ShiftSwapRequestDto?> ApproveAsync(int id, int reviewerId)
        {
            var req = await _db.ShiftSwapRequests
                .Include(s => s.RequesterShiftAssignment)
                .Include(s => s.TargetShiftAssignment)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (req == null) return null;
            if (req.Status != "AcceptedByTarget")
                throw new InvalidOperationException("Onaylamak için önce hedef personelin kabul etmesi gerekir.");

            // Takasi uygula: assignment'ların userId'lerini değiştir
            var a = req.RequesterShiftAssignment;
            if (req.TargetShiftAssignment != null)
            {
                // Çift yönlü takas: A ↔ B
                var b = req.TargetShiftAssignment;
                (a.UserId, b.UserId) = (b.UserId, a.UserId);
            }
            else
            {
                // Tek yönlü "üstüme al": requester'ın vardiyası → target'a
                a.UserId = req.TargetUserId;
            }

            req.Status     = "ApprovedByAdmin";
            req.ReviewedAt = DateTime.UtcNow;
            req.ReviewedBy = reviewerId;
            await _db.SaveChangesAsync();
            return await LoadDto(id);
        }

        public async Task<ShiftSwapRequestDto?> RejectAsync(int id, int reviewerId)
        {
            var req = await _db.ShiftSwapRequests.FindAsync(id);
            if (req == null) return null;
            if (req.Status == "ApprovedByAdmin" || req.Status == "RejectedByAdmin")
                throw new InvalidOperationException("Bu talep zaten karara bağlanmış.");
            req.Status     = "RejectedByAdmin";
            req.ReviewedAt = DateTime.UtcNow;
            req.ReviewedBy = reviewerId;
            await _db.SaveChangesAsync();
            return await LoadDto(id);
        }

        public async Task<bool> CancelAsync(int id, int requesterId)
        {
            var req = await _db.ShiftSwapRequests.FindAsync(id);
            if (req == null || req.RequesterId != requesterId) return false;
            if (req.Status == "ApprovedByAdmin") return false;
            req.Status = "CancelledByRequester";
            await _db.SaveChangesAsync();
            return true;
        }

        // ── Helpers ────────────────────────────────────────────────────
        private IQueryable<ShiftSwapRequest> BaseQuery()
            => _db.ShiftSwapRequests
                  .Include(s => s.Requester)
                  .Include(s => s.TargetUser)
                  .Include(s => s.RequesterShiftAssignment).ThenInclude(sa => sa.Shift)
                  .Include(s => s.TargetShiftAssignment).ThenInclude(sa => sa!.Shift);

        private async Task<ShiftSwapRequestDto> LoadDto(int id)
            => ToDto(await BaseQuery().FirstAsync(s => s.Id == id));

        private static ShiftSwapRequestDto ToDto(ShiftSwapRequest s) => new()
        {
            Id                         = s.Id,
            RequesterId                = s.RequesterId,
            RequesterName              = s.Requester.FullName,
            RequesterShiftAssignmentId = s.RequesterShiftAssignmentId,
            RequesterDate              = s.RequesterShiftAssignment.Date,
            RequesterShiftName         = s.RequesterShiftAssignment.Shift.Name,
            RequesterShiftColor        = s.RequesterShiftAssignment.Shift.Color,
            TargetUserId               = s.TargetUserId,
            TargetUserName             = s.TargetUser.FullName,
            TargetShiftAssignmentId    = s.TargetShiftAssignmentId,
            TargetDate                 = s.TargetShiftAssignment?.Date,
            TargetShiftName            = s.TargetShiftAssignment?.Shift.Name,
            TargetShiftColor           = s.TargetShiftAssignment?.Shift.Color,
            Reason                     = s.Reason,
            Status                     = s.Status,
            CreatedAt                  = s.CreatedAt,
            RespondedAt                = s.RespondedAt,
            ReviewedAt                 = s.ReviewedAt
        };
    }
}
