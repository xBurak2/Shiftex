namespace ShiftTrackingApp.Models
{
    public class Department
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ICollection<User> Users { get; set; } = new List<User>();
    }

    public class Shift
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Color { get; set; } = "#4f7eff";
        public ICollection<ShiftAssignment> Assignments { get; set; } = new List<ShiftAssignment>();
    }

    public class ShiftAssignment
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ShiftId { get; set; }
        public DateOnly Date { get; set; }
        public string? Position { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }

        public User User { get; set; } = null!;
        public Shift Shift { get; set; } = null!;
    }

    public class LeaveRequest
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string LeaveType { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public string? Description { get; set; }
        public bool HasMedicalReport { get; set; } = false;
        public string Status { get; set; } = "Pending";
        public int? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
    }

    public class AttendanceLog
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public string Source { get; set; } = "Manual";
        public string? Note { get; set; }

        public User User { get; set; } = null!;
    }

    public class RefreshToken
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
    }

    /// <summary>
    /// Personelin yüz tanıma vektörünü (descriptor) şifreli olarak saklar.
    /// Her personele ait yalnızca tek bir kayıt bulunur (UserId unique).
    /// </summary>
    public class FaceData
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        /// <summary>
        /// AES-256-CBC ile şifrelenmiş, JSON-serileştirilmiş float[] vektörü.
        /// </summary>
        public string EncryptedDescriptor { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
    }
}
