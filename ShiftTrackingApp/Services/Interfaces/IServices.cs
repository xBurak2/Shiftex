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

        /// <summary>Bir haftadaki tüm vardiyaları başka bir haftaya kopyalar. Hedef hafta üzerine yazılır.</summary>
        Task<int> CopyWeekAsync(DateOnly sourceWeekStart, DateOnly targetWeekStart);
    }

    public interface ILeaveService
    {
        Task<List<LeaveRequestDto>> GetAllAsync(string? status = null);
        Task<List<LeaveRequestDto>> GetByUserAsync(int userId);
        Task<LeaveRequestDto> CreateAsync(int userId, CreateLeaveRequestDto dto);
        Task<LeaveRequestDto?> ReviewAsync(int id, int reviewerId, ReviewLeaveDto dto);
        Task<(byte[] Bytes, string FileName, string ContentType)?> GetDocumentAsync(int id, int viewerId, bool isAdmin);
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
    /// Personel ihtiyaç matrisini (departman × vardiya × gün → gereken sayı) yönetir.
    /// Talep-güdümlü vardiya planlamasının temelidir.
    /// </summary>
    public interface IStaffingRequirementService
    {
        /// <summary>Bir departmanın tüm ihtiyaç kayıtlarını döner.</summary>
        Task<List<StaffingRequirementDto>> GetByDepartmentAsync(int departmentId);

        /// <summary>Tüm departmanların ihtiyaç kayıtlarını döner (dashboard için).</summary>
        Task<List<StaffingRequirementDto>> GetAllAsync();

        /// <summary>Bir departmanın haftalık matrisini topluca günceller (upsert + temizlik).</summary>
        Task UpsertForDepartmentAsync(int departmentId, List<UpsertStaffingRequirementDto> items);
    }

    /// <summary>
    /// Belirli bir gün için departman×vardiya kapasitesini hesaplar:
    /// Gereken (ihtiyaç) vs Atanan (roster) vs Gelen (check-in) → Eksik.
    /// </summary>
    public interface ICoverageService
    {
        Task<List<CoverageDto>> GetCoverageAsync(DateOnly date);
    }

    /// <summary>
    /// Yevmiyeci çağrı akışını yönetir: uygun yevmiyeci listesi, çağrı oluşturma,
    /// yevmiyecinin kabul/reddi (kabulde otomatik ShiftAssignment).
    /// </summary>
    public interface ICasualCalloutService
    {
        /// <summary>Belirli gün/vardiya/departman için çağrılabilecek müsait yevmiyeciler.</summary>
        Task<List<EligibleCasualDto>> GetEligibleAsync(int departmentId, int shiftId, DateOnly date);

        /// <summary>Admin bir yevmiyeciye çağrı gönderir.</summary>
        Task<CasualCalloutDto> CreateAsync(CreateCasualCalloutDto dto, int adminId);

        /// <summary>Bir yevmiyecinin kendi çağrıları (en yeni önce).</summary>
        Task<List<CasualCalloutDto>> GetMineAsync(int userId);

        /// <summary>Belirli bir günün tüm çağrıları (admin görünümü).</summary>
        Task<List<CasualCalloutDto>> GetByDateAsync(DateOnly date);

        /// <summary>Yevmiyeci çağrıyı kabul/red eder. Kabulde ShiftAssignment oluşturulur.</summary>
        Task<CasualCalloutDto> RespondAsync(int calloutId, int userId, bool accept);
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
