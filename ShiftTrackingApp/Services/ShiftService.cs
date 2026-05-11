using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Models;
using ShiftTrackingApp.Services.Interfaces;

namespace ShiftTrackingApp.Services
{
    public class ShiftService : IShiftService
    {
        private readonly AppDbContext _db;
        public ShiftService(AppDbContext db) => _db = db;

        public async Task<List<ShiftAssignmentDto>> GetWeeklyAsync(DateOnly weekStart)
        {
            var weekEnd = weekStart.AddDays(6);
            return await _db.ShiftAssignments
                .Include(sa => sa.User).ThenInclude(u => u.Department)
                .Include(sa => sa.Shift)
                // Soft-delete tutarlılığı: pasif kullanıcıların vardiyalarını gösterme
                .Where(sa => sa.Date >= weekStart && sa.Date <= weekEnd && sa.User.IsActive)
                .OrderBy(sa => sa.Date).ThenBy(sa => sa.Shift.StartTime)
                .Select(sa => ToDto(sa))
                .ToListAsync();
        }

        public async Task<List<ShiftAssignmentDto>> GetByUserAsync(int userId, DateOnly from, DateOnly to)
        {
            return await _db.ShiftAssignments
                .Include(sa => sa.User).ThenInclude(u => u.Department)
                .Include(sa => sa.Shift)
                .Where(sa => sa.UserId == userId && sa.Date >= from && sa.Date <= to)
                .OrderBy(sa => sa.Date)
                .Select(sa => ToDto(sa))
                .ToListAsync();
        }

        public async Task<ShiftAssignmentDto> AssignAsync(CreateShiftAssignmentDto dto)
        {
            var sa = new ShiftAssignment
            {
                UserId    = dto.UserId,
                ShiftId   = dto.ShiftId,
                Date      = dto.Date,
                Position  = dto.Position,
                Note      = dto.Note,
                CreatedAt = DateTime.UtcNow
            };
            _db.ShiftAssignments.Add(sa);
            await _db.SaveChangesAsync();
            return await LoadDto(sa.Id);
        }

        public async Task<ShiftAssignmentDto?> UpdateAsync(int id, CreateShiftAssignmentDto dto)
        {
            var sa = await _db.ShiftAssignments.FindAsync(id);
            if (sa == null) return null;

            sa.UserId   = dto.UserId;
            sa.ShiftId  = dto.ShiftId;
            sa.Date     = dto.Date;
            sa.Position = dto.Position;
            sa.Note     = dto.Note;
            await _db.SaveChangesAsync();
            return await LoadDto(id);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var sa = await _db.ShiftAssignments.FindAsync(id);
            if (sa == null) return false;
            _db.ShiftAssignments.Remove(sa);
            await _db.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Bir haftanın tüm atamalarını başka bir haftaya kopyalar.
        /// Hedef haftadaki mevcut atamalar SİLİNİR (overwrite).
        /// Geçmişe kopyalanmaz (sourceWeekStart geçmişte olabilir).
        /// </summary>
        public async Task<int> CopyWeekAsync(DateOnly sourceWeekStart, DateOnly targetWeekStart)
        {
            if (sourceWeekStart == targetWeekStart)
                throw new InvalidOperationException("Kaynak ve hedef hafta aynı olamaz.");

            var sourceEnd = sourceWeekStart.AddDays(6);
            var targetEnd = targetWeekStart.AddDays(6);

            // Hedef haftayı temizle
            var existing = await _db.ShiftAssignments
                .Where(sa => sa.Date >= targetWeekStart && sa.Date <= targetEnd)
                .ToListAsync();
            _db.ShiftAssignments.RemoveRange(existing);

            // Kaynak haftayı al
            var source = await _db.ShiftAssignments
                .Where(sa => sa.Date >= sourceWeekStart && sa.Date <= sourceEnd && sa.User.IsActive)
                .ToListAsync();

            var dayDiff = targetWeekStart.DayNumber - sourceWeekStart.DayNumber;
            var copies = source.Select(sa => new ShiftAssignment
            {
                UserId    = sa.UserId,
                ShiftId   = sa.ShiftId,
                Date      = sa.Date.AddDays(dayDiff),
                Position  = sa.Position,
                Note      = sa.Note,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            await _db.ShiftAssignments.AddRangeAsync(copies);
            await _db.SaveChangesAsync();
            return copies.Count;
        }

        private async Task<ShiftAssignmentDto> LoadDto(int id)
        {
            var sa = await _db.ShiftAssignments
                .Include(x => x.User).ThenInclude(u => u.Department)
                .Include(x => x.Shift)
                .FirstAsync(x => x.Id == id);
            return ToDto(sa);
        }

        private static ShiftAssignmentDto ToDto(ShiftAssignment sa) => new()
        {
            Id             = sa.Id,
            UserId         = sa.UserId,
            UserFullName   = sa.User.FullName,
            DepartmentName = sa.User.Department?.Name ?? "—",
            UserPhoto      = sa.User.PhotoBase64,
            ShiftId        = sa.ShiftId,
            ShiftName      = sa.Shift.Name,
            ShiftColor     = sa.Shift.Color,
            StartTime      = sa.Shift.StartTime.ToString(@"hh\:mm"),
            EndTime        = sa.Shift.EndTime.ToString(@"hh\:mm"),
            Date           = sa.Date,
            Position       = sa.Position,
            Note           = sa.Note
        };
    }
}
