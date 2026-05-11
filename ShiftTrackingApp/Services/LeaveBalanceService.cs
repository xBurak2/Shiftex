using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.DTOs;

namespace ShiftTrackingApp.Services
{
    public interface ILeaveBalanceService
    {
        Task<LeaveBalanceDto> GetBalanceAsync(int userId, int? year = null);
    }

    /// <summary>
    /// Personelin yıllık izin bakiyesini hesaplar.
    /// Varsayılan: 14 gün/yıl. Sadece "Yıllık" tipindeki ONAYLANMIŞ izinler kullanılır.
    /// "Sağlık" ve "Mazeret" türleri bakiyeden düşmez.
    /// </summary>
    public class LeaveBalanceService : ILeaveBalanceService
    {
        private const int DEFAULT_ANNUAL_DAYS = 14;
        private readonly AppDbContext _db;

        public LeaveBalanceService(AppDbContext db) => _db = db;

        public async Task<LeaveBalanceDto> GetBalanceAsync(int userId, int? year = null)
        {
            var y          = year ?? DateTime.UtcNow.Year;
            var yearStart  = new DateOnly(y, 1, 1);
            var yearEnd    = new DateOnly(y, 12, 31);

            // Yıl içinde başlayan Yıllık izinler
            var leaves = await _db.LeaveRequests
                .Where(l => l.UserId == userId
                         && l.LeaveType == "Yıllık"
                         && l.StartDate >= yearStart && l.StartDate <= yearEnd)
                .ToListAsync();

            int used    = leaves.Where(l => l.Status == "Approved")
                                .Sum(l => l.EndDate.DayNumber - l.StartDate.DayNumber + 1);
            int pending = leaves.Where(l => l.Status == "Pending")
                                .Sum(l => l.EndDate.DayNumber - l.StartDate.DayNumber + 1);

            return new LeaveBalanceDto
            {
                UserId          = userId,
                Year            = y,
                AnnualAllowance = DEFAULT_ANNUAL_DAYS,
                UsedDays        = used,
                PendingDays     = pending
            };
        }
    }
}
