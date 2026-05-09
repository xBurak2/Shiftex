using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Models;
using ShiftTrackingApp.Services.Interfaces;

namespace ShiftTrackingApp.Services
{
    public class DepartmentService : IDepartmentService
    {
        private readonly AppDbContext _db;

        public DepartmentService(AppDbContext db) => _db = db;

        public async Task<List<DepartmentDto>> GetAllAsync()
        {
            return await _db.Departments
                .OrderBy(d => d.Name)
                .Select(d => new DepartmentDto { Id = d.Id, Name = d.Name })
                .ToListAsync();
        }

        public async Task<DepartmentDto?> GetByIdAsync(int id)
        {
            var dept = await _db.Departments.FindAsync(id);
            if (dept == null) return null;
            return new DepartmentDto { Id = dept.Id, Name = dept.Name };
        }

        public async Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto)
        {
            var dept = new Department
            {
                Name        = dto.Name.Trim(),
                Description = dto.Description?.Trim()
            };
            _db.Departments.Add(dept);
            await _db.SaveChangesAsync();
            return new DepartmentDto { Id = dept.Id, Name = dept.Name };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var dept = await _db.Departments.FindAsync(id);
            if (dept == null) return false;

            // Bağlı personelleri departmansız bırak (FK SetNull)
            _db.Departments.Remove(dept);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
