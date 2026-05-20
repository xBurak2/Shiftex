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

    /// <summary>
    /// Bir departmanın belirli bir vardiyada, haftanın belirli bir gününde
    /// kaç kişiye ihtiyaç duyduğunu tanımlar (talep-güdümlü planlamanın temeli).
    /// Haftalık şablon: (DepartmentId, ShiftId, DayOfWeek) benzersiz.
    /// </summary>
    public class StaffingRequirement
    {
        public int Id { get; set; }
        public int DepartmentId { get; set; }
        public int ShiftId { get; set; }

        /// <summary>0 = Pazartesi ... 6 = Pazar</summary>
        public int DayOfWeek { get; set; }

        /// <summary>O gün/vardiya için gereken minimum personel sayısı.</summary>
        public int RequiredCount { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Department Department { get; set; } = null!;
        public Shift Shift { get; set; } = null!;
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

        // Belge eki (sağlık raporu, cenaze belgesi, sınav belgesi vb.)
        public byte[]? DocumentBytes      { get; set; }
        public string? DocumentFileName   { get; set; }
        public string? DocumentContentType{ get; set; }

        public User User { get; set; } = null!;
    }

    /// <summary>
    /// Personeller arası vardiya değişim talebi.
    /// İki personel rıza ile gün/vardiya takası ister, admin onayıyla işleme alınır.
    /// </summary>
    public class ShiftSwapRequest
    {
        public int Id { get; set; }

        // Talebi başlatan
        public int RequesterId { get; set; }
        public int RequesterShiftAssignmentId { get; set; }

        // Hedef personel — açık ilan (Open) durumunda NULL
        public int? TargetUserId { get; set; }
        public int? TargetShiftAssignmentId { get; set; } // null ise tek yönlü "üstüme al"

        // Açık ilan için: ilan veren hangi shift türüne geçmek istiyor
        // (NULL = "vardiyamı isteyen alsın", dolu = "şu vardiya türüyle takas isterim")
        public int? DesiredShiftId { get; set; }

        public string? Reason { get; set; }

        // State:
        //   Open                — açık ilan, kimse henüz kabul etmedi (TargetUserId NULL)
        //   Pending             — belirli hedefe yapılan talep, hedef cevap bekliyor
        //   AcceptedByTarget    — hedef kabul etti, admin onayı bekliyor
        //   ApprovedByAdmin     — admin onayladı, vardiyalar takas edildi
        //   RejectedByTarget / RejectedByAdmin / CancelledByRequester — sonlanmış
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
        public DateTime? RespondedAt{ get; set; }   // hedef cevap verdiğinde
        public DateTime? ReviewedAt { get; set; }   // admin onayında
        public int? ReviewedBy      { get; set; }

        public User Requester  { get; set; } = null!;
        public User? TargetUser { get; set; }
        public ShiftAssignment RequesterShiftAssignment { get; set; } = null!;
        public ShiftAssignment? TargetShiftAssignment   { get; set; }
        public Shift? DesiredShift { get; set; }
    }

    /// <summary>
    /// Personelin fazla mesai için yaptığı talep.
    /// Admin onaylarsa ilgili güne FM vardiyası atanır.
    /// </summary>
    public class OvertimeRequest
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateOnly Date { get; set; }
        public int ShiftId { get; set; }   // FM shift ID'si (7, 8, 9)
        public string? Reason { get; set; }

        // Pending → Approved (ShiftAssignment otomatik oluşturulur) | Rejected
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt{ get; set; }
        public int? ReviewedBy     { get; set; }

        public User User   { get; set; } = null!;
        public Shift Shift { get; set; } = null!;
    }

    /// <summary>
    /// Yönetici tarafından bir yevmiyeciye gönderilen vardiya çağrısı.
    /// Vardiya açığı oluştuğunda departmana uygun yevmiyeci çağrılır;
    /// yevmiyeci kabul ederse ilgili güne otomatik ShiftAssignment oluşur.
    /// </summary>
    public class CasualCallout
    {
        public int Id { get; set; }
        public int DepartmentId { get; set; }
        public int ShiftId { get; set; }
        public DateOnly Date { get; set; }

        /// <summary>Çağrılan yevmiyeci.</summary>
        public int CalledUserId { get; set; }

        // Sent → Accepted (ShiftAssignment oluşur) | Rejected | Cancelled
        public string Status { get; set; } = "Sent";
        public string? Note { get; set; }

        public int CreatedBy { get; set; }           // çağrıyı yapan admin
        public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
        public DateTime? RespondedAt { get; set; }    // yevmiyeci cevap verince

        public Department Department { get; set; } = null!;
        public Shift Shift           { get; set; } = null!;
        public User CalledUser       { get; set; } = null!;
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
