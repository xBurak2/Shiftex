using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Helpers;
using ShiftTrackingApp.Services.Interfaces;

namespace ShiftTrackingApp.Services
{
    /// <summary>
    /// Vardiya kapasitesini hesaplar: bir gün için her departman/vardiyada
    /// kaç kişi gerekiyor (Required), kaç kişi atandı (Assigned),
    /// kaç kişi geldi (Present) ve eksik (Shortage) nedir.
    /// </summary>
    public class CoverageService : ICoverageService
    {
        private readonly AppDbContext _db;

        public CoverageService(AppDbContext db) => _db = db;

        public async Task<List<CoverageDto>> GetCoverageAsync(DateOnly date)
        {
            // System.DayOfWeek: Pazar=0..Cmt=6  →  bizde Pazartesi=0..Pazar=6
            int dow = ((int)date.DayOfWeek + 6) % 7;

            // 1) O güne ait ihtiyaç kayıtları (departman + vardiya bilgisiyle)
            var requirements = await _db.StaffingRequirements
                .Where(r => r.DayOfWeek == dow)
                .Include(r => r.Department)
                .Include(r => r.Shift)
                .ToListAsync();

            // 2) O güne atanmış vardiyalar (atanan personelin departmanıyla)
            var assignments = await _db.ShiftAssignments
                .Where(a => a.Date == date)
                .Select(a => new { a.ShiftId, a.UserId, DeptId = a.User.DepartmentId })
                .ToListAsync();

            // 3) O gün check-in yapan personeller (gelen sayısı için)
            // Gün sınırlarını Türkiye yerelinden UTC'ye çevir (CheckIn UTC saklanıyor).
            // Böylece gece vardiyaları da doğru günde sayılır.
            var dayStart = TimeZoneHelper.ConvertToUtc(date.ToDateTime(TimeOnly.MinValue));
            var dayEnd   = TimeZoneHelper.ConvertToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue));
            var checkedIn = (await _db.AttendanceLogs
                .Where(l => l.CheckIn >= dayStart && l.CheckIn < dayEnd)
                .Select(l => l.UserId)
                .Distinct()
                .ToListAsync())
                .ToHashSet();

            var result = new List<CoverageDto>();
            foreach (var req in requirements)
            {
                var matching = assignments
                    .Where(a => a.DeptId == req.DepartmentId && a.ShiftId == req.ShiftId)
                    .ToList();

                int assigned = matching.Count;
                int present  = matching.Count(a => checkedIn.Contains(a.UserId));

                result.Add(new CoverageDto
                {
                    DepartmentId   = req.DepartmentId,
                    DepartmentName = req.Department.Name,
                    ShiftId        = req.ShiftId,
                    ShiftName      = req.Shift.Name,
                    ShiftColor     = req.Shift.Color,
                    Required       = req.RequiredCount,
                    Assigned       = assigned,
                    Present        = present,
                    Shortage       = Math.Max(0, req.RequiredCount - present),
                });
            }

            return result
                .OrderBy(c => c.DepartmentName)
                .ThenBy(c => c.ShiftId)
                .ToList();
        }
    }
}
