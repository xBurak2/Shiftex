using ShiftTrackingApp.Models;

namespace ShiftTrackingApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "Employee";

        /// <summary>
        /// "Permanent" = kadrolu personel (sabit roster, aylık maaş)
        /// "Casual"    = yevmiyeci (ihtiyaç bazlı çağrılır, günlük ücret)
        /// </summary>
        public string EmploymentType { get; set; } = "Permanent";

        /// <summary>
        /// Yevmiyeci için günlük ücret (TL). Kadrolu personelde null.
        /// </summary>
        public decimal? DailyWage { get; set; }

        public int? DepartmentId { get; set; }
        public string? Position { get; set; }
        public DateTime? HireDate { get; set; }
        public string? PhotoBase64 { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Department? Department { get; set; }
        public ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
        public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
        public ICollection<AttendanceLog> AttendanceLogs { get; set; } = new List<AttendanceLog>();
    }
}
