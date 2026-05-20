using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Models;
using ShiftTrackingApp.Services.Interfaces;

namespace ShiftTrackingApp.Services
{
    /// <summary>
    /// Personel ihtiyaç matrisini yönetir: departman × vardiya × gün → gereken sayı.
    /// </summary>
    public class StaffingRequirementService : IStaffingRequirementService
    {
        private readonly AppDbContext _db;

        public StaffingRequirementService(AppDbContext db) => _db = db;

        public async Task<List<StaffingRequirementDto>> GetByDepartmentAsync(int departmentId)
        {
            return await _db.StaffingRequirements
                .Where(s => s.DepartmentId == departmentId)
                .OrderBy(s => s.ShiftId).ThenBy(s => s.DayOfWeek)
                .Select(s => ToDto(s))
                .ToListAsync();
        }

        public async Task<List<StaffingRequirementDto>> GetAllAsync()
        {
            return await _db.StaffingRequirements
                .OrderBy(s => s.DepartmentId).ThenBy(s => s.ShiftId).ThenBy(s => s.DayOfWeek)
                .Select(s => ToDto(s))
                .ToListAsync();
        }

        public async Task UpsertForDepartmentAsync(int departmentId, List<UpsertStaffingRequirementDto> items)
        {
            // Departman var mı?
            var deptExists = await _db.Departments.AnyAsync(d => d.Id == departmentId);
            if (!deptExists)
                throw new KeyNotFoundException("Departman bulunamadı.");

            var existing = await _db.StaffingRequirements
                .Where(s => s.DepartmentId == departmentId)
                .ToListAsync();

            foreach (var item in items)
            {
                var row = existing.FirstOrDefault(
                    e => e.ShiftId == item.ShiftId && e.DayOfWeek == item.DayOfWeek);

                if (item.RequiredCount <= 0)
                {
                    // 0 ihtiyaç = kayıt tutma (varsa sil)
                    if (row != null) _db.StaffingRequirements.Remove(row);
                    continue;
                }

                if (row == null)
                {
                    _db.StaffingRequirements.Add(new StaffingRequirement
                    {
                        DepartmentId  = departmentId,
                        ShiftId       = item.ShiftId,
                        DayOfWeek     = item.DayOfWeek,
                        RequiredCount = item.RequiredCount,
                        UpdatedAt     = DateTime.UtcNow
                    });
                }
                else
                {
                    row.RequiredCount = item.RequiredCount;
                    row.UpdatedAt     = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync();
        }

        private static StaffingRequirementDto ToDto(StaffingRequirement s) => new()
        {
            Id            = s.Id,
            DepartmentId  = s.DepartmentId,
            ShiftId       = s.ShiftId,
            DayOfWeek     = s.DayOfWeek,
            RequiredCount = s.RequiredCount,
        };
    }
}
