using ShiftTrackingApp.DTOs;

namespace ShiftTrackingApp.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> LoginAsync(LoginDto dto);
        Task<AuthResponseDto?> RefreshAsync(string refreshToken);
        Task<bool> RevokeAsync(string refreshToken);
    }

    public interface IUserService
    {
        Task<PagedResult<UserDto>> GetAllAsync(int page = 1, int pageSize = 50);
        Task<UserDto?> GetByIdAsync(int id);
        Task<UserDto> CreateAsync(CreateUserDto dto);
        Task<UserDto?> UpdateAsync(int id, UpdateUserDto dto);
        Task<bool> DeleteAsync(int id);
        Task<MonthlyAttendanceSummaryDto> GetMonthlyAttendanceSummaryAsync(int userId, int year, int month);
    }

    public interface IShiftService
    {
        Task<List<ShiftAssignmentDto>> GetWeeklyAsync(DateOnly weekStart);
        Task<List<ShiftAssignmentDto>> GetByUserAsync(int userId, DateOnly from, DateOnly to);
        Task<ShiftAssignmentDto> AssignAsync(CreateShiftAssignmentDto dto);
        Task<ShiftAssignmentDto?> UpdateAsync(int id, CreateShiftAssignmentDto dto);
        Task<bool> DeleteAsync(int id);
    }

    public interface ILeaveService
    {
        Task<List<LeaveRequestDto>> GetAllAsync(string? status = null);
        Task<List<LeaveRequestDto>> GetByUserAsync(int userId);
        Task<LeaveRequestDto> CreateAsync(int userId, CreateLeaveRequestDto dto);
        Task<LeaveRequestDto?> ReviewAsync(int id, int reviewerId, ReviewLeaveDto dto);
    }

    public interface IAttendanceService
    {
        Task<List<AttendanceLogDto>> GetTodayAsync();
        Task<List<AttendanceLogDto>> GetByUserTodayAsync(int userId);
        Task<AttendanceLogDto> CheckInAsync(int userId, string source = "Manual");
        Task<AttendanceLogDto?> CheckOutAsync(int userId, string source = "Manual");
        Task<DashboardStatsDto> GetDashboardStatsAsync();
    }

    public interface IDepartmentService
    {
        Task<List<DepartmentDto>> GetAllAsync();
        Task<DepartmentDto?> GetByIdAsync(int id);
        Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto);
        Task<bool> DeleteAsync(int id);
    }

    /// <summary>
    /// Yüz tanıma verilerini şifreli olarak backend'de yönetir.
    /// </summary>
    public interface IFaceDataService
    {
        /// <summary>Tüm kayıtlı yüzleri şifresi çözülmüş olarak döner (yalnızca Admin).</summary>
        Task<List<FaceDataDto>> GetAllAsync();

        /// <summary>Personel için yüz kaydı oluşturur ya da günceller (upsert).</summary>
        Task<FaceDataDto> SaveAsync(SaveFaceDataDto dto);

        /// <summary>Personelin yüz kaydını siler.</summary>
        Task<bool> DeleteAsync(int userId);
    }
}
