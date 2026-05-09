using System.ComponentModel.DataAnnotations;

namespace ShiftTrackingApp.DTOs
{
    // ─── AUTH ─────────────────────────────────────────────
    public class LoginDto
    {
        [Required(ErrorMessage = "E-posta zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string? PhotoBase64 { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
    }

    // ─── PAGINATION ───────────────────────────────────────
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPrev => Page > 1;
        public bool HasNext => Page < TotalPages;
    }

    // ─── USER ─────────────────────────────────────────────
    public class UserDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public int? DepartmentId { get; set; }
        public string? Position { get; set; }
        public DateTime? HireDate { get; set; }
        public string? PhotoBase64 { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateUserDto
    {
        [Required(ErrorMessage = "Ad soyad zorunludur.")]
        [MaxLength(100, ErrorMessage = "Ad soyad en fazla 100 karakter olabilir.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-posta zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
        [MaxLength(150, ErrorMessage = "E-posta en fazla 150 karakter olabilir.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
        [MaxLength(100, ErrorMessage = "Şifre en fazla 100 karakter olabilir.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Rol zorunludur.")]
        [RegularExpression("^(Admin|Employee)$", ErrorMessage = "Rol 'Admin' veya 'Employee' olmalıdır.")]
        public string Role { get; set; } = "Employee";

        public int? DepartmentId { get; set; }

        [MaxLength(100, ErrorMessage = "Pozisyon en fazla 100 karakter olabilir.")]
        public string? Position { get; set; }

        public DateTime? HireDate { get; set; }

        [Phone(ErrorMessage = "Geçerli bir telefon numarası girin.")]
        [MaxLength(20, ErrorMessage = "Telefon numarası en fazla 20 karakter olabilir.")]
        public string? PhoneNumber { get; set; }
    }

    public class UpdateUserDto
    {
        [MaxLength(100, ErrorMessage = "Ad soyad en fazla 100 karakter olabilir.")]
        public string? FullName { get; set; }

        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
        [MaxLength(150, ErrorMessage = "E-posta en fazla 150 karakter olabilir.")]
        public string? Email { get; set; }

        [RegularExpression("^(Admin|Employee)$", ErrorMessage = "Rol 'Admin' veya 'Employee' olmalıdır.")]
        public string? Role { get; set; }

        public int? DepartmentId { get; set; }

        [MaxLength(100, ErrorMessage = "Pozisyon en fazla 100 karakter olabilir.")]
        public string? Position { get; set; }

        public DateTime? HireDate { get; set; }

        [Phone(ErrorMessage = "Geçerli bir telefon numarası girin.")]
        [MaxLength(20, ErrorMessage = "Telefon numarası en fazla 20 karakter olabilir.")]
        public string? PhoneNumber { get; set; }

        public string? PhotoBase64 { get; set; }

        [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
        [MaxLength(100, ErrorMessage = "Şifre en fazla 100 karakter olabilir.")]
        public string? NewPassword { get; set; }
    }

    // ─── SHIFT ────────────────────────────────────────────
    public class ShiftAssignmentDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string? UserPhoto { get; set; }
        public int ShiftId { get; set; }
        public string ShiftName { get; set; } = string.Empty;
        public string ShiftColor { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public string? Position { get; set; }
        public string? Note { get; set; }
    }

    public class CreateShiftAssignmentDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir personel seçin.")]
        public int UserId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir vardiya türü seçin.")]
        public int ShiftId { get; set; }

        [Required(ErrorMessage = "Tarih zorunludur.")]
        public DateOnly Date { get; set; }

        [MaxLength(100, ErrorMessage = "Pozisyon en fazla 100 karakter olabilir.")]
        public string? Position { get; set; }

        [MaxLength(500, ErrorMessage = "Not en fazla 500 karakter olabilir.")]
        public string? Note { get; set; }
    }

    // ─── LEAVE ────────────────────────────────────────────
    public class LeaveRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string LeaveType { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public int TotalDays => (EndDate.ToDateTime(TimeOnly.MinValue) - StartDate.ToDateTime(TimeOnly.MinValue)).Days + 1;
        public string? Description { get; set; }
        public bool HasMedicalReport { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CreateLeaveRequestDto
    {
        [Required(ErrorMessage = "İzin türü zorunludur.")]
        [RegularExpression("^(Yıllık|Sağlık|Mazeret)$", ErrorMessage = "İzin türü 'Yıllık', 'Sağlık' veya 'Mazeret' olmalıdır.")]
        public string LeaveType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Başlangıç tarihi zorunludur.")]
        public DateOnly StartDate { get; set; }

        [Required(ErrorMessage = "Bitiş tarihi zorunludur.")]
        public DateOnly EndDate { get; set; }

        [MaxLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
        public string? Description { get; set; }

        public bool HasMedicalReport { get; set; } = false;
    }

    public class ReviewLeaveDto
    {
        [Required(ErrorMessage = "Durum zorunludur.")]
        [RegularExpression("^(Approved|Rejected)$", ErrorMessage = "Durum 'Approved' veya 'Rejected' olmalıdır.")]
        public string Status { get; set; } = string.Empty;
    }

    // ─── ATTENDANCE ───────────────────────────────────────
    public class AttendanceLogDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string? UserPhoto { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public string Source { get; set; } = string.Empty;
        public string? Note { get; set; }
        public bool IsEarlyDeparture { get; set; }
        public int EarlyMinutes { get; set; }
        public bool IsLateArrival { get; set; }
        public int LateMinutes { get; set; }
        public bool IsInvalidTime { get; set; }
        public bool IsShortDuration { get; set; }
        public double? WorkedHours { get; set; }
    }

    public class MonthlyAttendanceSummaryDto
    {
        public int PresentDays { get; set; }
        public int AbsentDays { get; set; }
        public int AbsentWithReport { get; set; }
        public int AbsentWithoutReport { get; set; }
        public int LeaveDays { get; set; }
        public double TotalWorkedHours { get; set; }
        public double TotalOvertimeHours { get; set; }
        public int OvertimeShiftCount { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
    }

    // ─── DASHBOARD ────────────────────────────────────────
    public class DashboardStatsDto
    {
        public int TotalActiveEmployees { get; set; }
        public int PresentToday { get; set; }
        public int OnLeaveToday { get; set; }
        public int AbsentToday { get; set; }
        public int PendingLeaveRequests { get; set; }
        public double AttendanceRate => TotalActiveEmployees > 0
            ? Math.Round((double)PresentToday / TotalActiveEmployees * 100, 1) : 0;
    }

    // ─── DEPARTMENT ───────────────────────────────────────
    public class DepartmentDto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Departman adı zorunludur.")]
        [MaxLength(100, ErrorMessage = "Departman adı en fazla 100 karakter olabilir.")]
        public string Name { get; set; } = string.Empty;
    }

    public class CreateDepartmentDto
    {
        [Required(ErrorMessage = "Departman adı zorunludur.")]
        [MaxLength(100, ErrorMessage = "Departman adı en fazla 100 karakter olabilir.")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }
    }

    // ─── REFRESH TOKEN ─────────────────────────────────────
    public class RefreshRequestDto
    {
        [Required(ErrorMessage = "Refresh token zorunludur.")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    // ─── FACE DATA ─────────────────────────────────────────
    /// <summary>
    /// Yüz kaydı oluşturma / güncelleme isteği.
    /// Descriptor: face-api.js'den gelen 128 float'lık vektör.
    /// </summary>
    public class SaveFaceDataDto
    {
        [Required(ErrorMessage = "Kullanıcı ID zorunludur.")]
        [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir kullanıcı seçin.")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Yüz vektörü zorunludur.")]
        [MinLength(128, ErrorMessage = "Descriptor en az 128 elemandan oluşmalıdır.")]
        [MaxLength(512, ErrorMessage = "Descriptor en fazla 512 elemandan oluşabilir.")]
        public float[] Descriptor { get; set; } = Array.Empty<float>();
    }

    /// <summary>
    /// Yüz tanıma için frontend'e dönen DTO.
    /// Descriptor şifresi çözülmüş float[] olarak gelir.
    /// </summary>
    public class FaceDataDto
    {
        public int UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string? UserPhoto { get; set; }
        public float[] Descriptor { get; set; } = Array.Empty<float>();
        public DateTime EnrolledAt { get; set; }
    }
}
