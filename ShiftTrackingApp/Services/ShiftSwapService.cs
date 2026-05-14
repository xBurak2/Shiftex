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

        // Açık ilan (open listing) işlemleri
        Task<List<ShiftSwapRequestDto>> GetOpenListingsAsync(int viewerId);
        Task<ShiftSwapRequestDto> CreateOpenAsync(int requesterId, CreateOpenSwapDto dto);
        Task<ShiftSwapRequestDto?> ClaimOpenAsync(int id, int claimerId);
    }

    public class ShiftSwapService : IShiftSwapService
    {
        private readonly AppDbContext _db;
        public ShiftSwapService(AppDbContext db) => _db = db;

        public async Task<List<ShiftSwapRequestDto>> GetMyOutgoingAsync(int userId)
        {
            var rows = await BaseQuery().Where(s => s.RequesterId == userId)
                                        .OrderByDescending(s => s.CreatedAt)
                                        .ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public async Task<List<ShiftSwapRequestDto>> GetMyIncomingAsync(int userId)
        {
            var rows = await BaseQuery().Where(s => s.TargetUserId == userId)
                                        .OrderByDescending(s => s.CreatedAt)
                                        .ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public async Task<List<ShiftSwapRequestDto>> GetAllAsync(string? status = null)
        {
            var q = BaseQuery();
            if (!string.IsNullOrEmpty(status)) q = q.Where(s => s.Status == status);
            var rows = await q.OrderByDescending(s => s.CreatedAt).ToListAsync();
            return rows.Select(ToDto).ToList();
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
            if (req.TargetUserId is null)
                throw new InvalidOperationException("Hedef personel belirlenmemiş bir talep onaylanamaz.");

            if (req.TargetShiftAssignment != null)
            {
                // Çift yönlü takas: A ↔ B
                var b = req.TargetShiftAssignment;
                (a.UserId, b.UserId) = (b.UserId, a.UserId);
            }
            else
            {
                // Tek yönlü "üstüme al": requester'ın vardiyası → target'a
                a.UserId = req.TargetUserId.Value;
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

        // ── Açık İlan (Open Listing) ─────────────────────────────────────

        public async Task<List<ShiftSwapRequestDto>> GetOpenListingsAsync(int viewerId)
        {
            // Viewer'ın departmanı
            var viewer = await _db.Users.FirstOrDefaultAsync(u => u.Id == viewerId);
            if (viewer == null) return new List<ShiftSwapRequestDto>();

            // Açık ilanlar = Status==Open && TargetUserId IS NULL && requester departmanı == viewer departmanı
            // Kendi ilanını da liste içinde tutmuyoruz (kendi ilanını "Gönderdiklerim" sekmesinden görür)
            var rows = await BaseQuery()
                .Where(s => s.Status == "Open"
                          && s.TargetUserId == null
                          && s.RequesterId != viewerId
                          && s.Requester.DepartmentId == viewer.DepartmentId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public async Task<ShiftSwapRequestDto> CreateOpenAsync(int requesterId, CreateOpenSwapDto dto)
        {
            // Vardiya requester'a ait mi?
            var myAssignment = await _db.ShiftAssignments
                .FirstOrDefaultAsync(sa => sa.Id == dto.RequesterShiftAssignmentId && sa.UserId == requesterId)
                ?? throw new InvalidOperationException("Belirtilen vardiya size ait değil.");

            // İstenilen shift varsa, requester'ın mevcut vardiyasından farklı olmalı
            if (dto.DesiredShiftId.HasValue && dto.DesiredShiftId.Value == myAssignment.ShiftId)
                throw new InvalidOperationException("İstediğiniz vardiya zaten mevcut vardiyanızla aynı.");

            // Aynı vardiya için açık/bekleyen başka talep var mı?
            var dup = await _db.ShiftSwapRequests.AnyAsync(s =>
                s.RequesterShiftAssignmentId == dto.RequesterShiftAssignmentId
                && (s.Status == "Open" || s.Status == "Pending" || s.Status == "AcceptedByTarget"));
            if (dup) throw new InvalidOperationException("Bu vardiya için zaten aktif bir değişim talebi var.");

            var req = new ShiftSwapRequest
            {
                RequesterId                = requesterId,
                RequesterShiftAssignmentId = dto.RequesterShiftAssignmentId,
                TargetUserId               = null,
                TargetShiftAssignmentId    = null,
                DesiredShiftId             = dto.DesiredShiftId,
                Reason                     = dto.Reason,
                Status                     = "Open",
                CreatedAt                  = DateTime.UtcNow
            };
            _db.ShiftSwapRequests.Add(req);
            await _db.SaveChangesAsync();
            return await LoadDto(req.Id);
        }

        public async Task<ShiftSwapRequestDto?> ClaimOpenAsync(int id, int claimerId)
        {
            var req = await _db.ShiftSwapRequests
                .Include(s => s.RequesterShiftAssignment)
                .Include(s => s.Requester)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (req == null) return null;
            if (req.Status != "Open" || req.TargetUserId != null)
                throw new InvalidOperationException("Bu ilan artık açık değil.");
            if (req.RequesterId == claimerId)
                throw new InvalidOperationException("Kendi ilanınızı kabul edemezsiniz.");

            var claimer = await _db.Users.FirstOrDefaultAsync(u => u.Id == claimerId)
                ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

            // Aynı departman kontrolü
            if (claimer.DepartmentId != req.Requester.DepartmentId)
                throw new InvalidOperationException("Sadece aynı departmandaki ilanları kabul edebilirsiniz.");

            // Claimer'ın o gün için kabul edilebilir bir vardiyası olmalı:
            // - Eğer DesiredShiftId belirtilmişse, claimer'ın o gün için tam o vardiyası olmalı
            // - Belirtilmemişse, claimer'ın o gün için herhangi bir vardiyası olmalı (basit takas)
            var dateToMatch = req.RequesterShiftAssignment.Date;
            var myAssignmentQuery = _db.ShiftAssignments
                .Where(sa => sa.UserId == claimerId && sa.Date == dateToMatch);
            if (req.DesiredShiftId.HasValue)
                myAssignmentQuery = myAssignmentQuery.Where(sa => sa.ShiftId == req.DesiredShiftId.Value);

            var myAssignment = await myAssignmentQuery.FirstOrDefaultAsync()
                ?? throw new InvalidOperationException(
                    req.DesiredShiftId.HasValue
                        ? "Bu ilan için aynı gün/uygun vardiyanız yok."
                        : "Bu ilanı kabul etmek için aynı gün vardiyanız yok.");

            // Claimer'ın vardiyası, requester'ın vardiyasıyla aynı olamaz (anlamsız takas)
            if (myAssignment.ShiftId == req.RequesterShiftAssignment.ShiftId)
                throw new InvalidOperationException("Vardiyalarınız zaten aynı; takasa gerek yok.");

            req.TargetUserId            = claimerId;
            req.TargetShiftAssignmentId = myAssignment.Id;
            req.Status                  = "AcceptedByTarget";
            req.RespondedAt             = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return await LoadDto(id);
        }

        // ── Helpers ────────────────────────────────────────────────────
        private IQueryable<ShiftSwapRequest> BaseQuery()
            => _db.ShiftSwapRequests
                  .Include(s => s.Requester)
                  .Include(s => s.TargetUser)
                  .Include(s => s.RequesterShiftAssignment).ThenInclude(sa => sa.Shift)
                  .Include(s => s.TargetShiftAssignment).ThenInclude(sa => sa!.Shift)
                  .Include(s => s.DesiredShift);

        private async Task<ShiftSwapRequestDto> LoadDto(int id)
        {
            var row = await BaseQuery().FirstAsync(s => s.Id == id);
            return ToDto(row);
        }

        private static ShiftSwapRequestDto ToDto(ShiftSwapRequest s) => new()
        {
            Id                         = s.Id,
            RequesterId                = s.RequesterId,
            RequesterName              = s.Requester.FullName,
            RequesterDepartmentId      = s.Requester.DepartmentId,
            RequesterShiftAssignmentId = s.RequesterShiftAssignmentId,
            RequesterDate              = s.RequesterShiftAssignment.Date,
            RequesterShiftId           = s.RequesterShiftAssignment.ShiftId,
            RequesterShiftName         = s.RequesterShiftAssignment.Shift.Name,
            RequesterShiftColor        = s.RequesterShiftAssignment.Shift.Color,
            TargetUserId               = s.TargetUserId,
            TargetUserName             = s.TargetUser?.FullName,
            TargetShiftAssignmentId    = s.TargetShiftAssignmentId,
            TargetDate                 = s.TargetShiftAssignment?.Date,
            TargetShiftId              = s.TargetShiftAssignment?.ShiftId,
            TargetShiftName            = s.TargetShiftAssignment?.Shift.Name,
            TargetShiftColor           = s.TargetShiftAssignment?.Shift.Color,
            DesiredShiftId             = s.DesiredShiftId,
            DesiredShiftName           = s.DesiredShift?.Name,
            DesiredShiftColor          = s.DesiredShift?.Color,
            Reason                     = s.Reason,
            Status                     = s.Status,
            CreatedAt                  = s.CreatedAt,
            RespondedAt                = s.RespondedAt,
            ReviewedAt                 = s.ReviewedAt
        };
    }
}
