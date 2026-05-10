namespace ShiftTrackingApp.Constants
{
    /// <summary>Sistem rolleri — magic string yerine compile-time sabit.</summary>
    public static class Roles
    {
        public const string Admin    = "Admin";
        public const string Employee = "Employee";
    }

    /// <summary>İzin durumları — leave request state machine.</summary>
    public static class LeaveStatus
    {
        public const string Pending  = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }

    /// <summary>Devam kaydı kaynağı — manuel mi yüz tanıma mı.</summary>
    public static class AttendanceSource
    {
        public const string Manual          = "Manual";
        public const string FaceRecognition = "FaceRecognition";
    }

    /// <summary>İzin türleri.</summary>
    public static class LeaveType
    {
        public const string Annual = "Yıllık";
        public const string Health = "Sağlık";
        public const string Excuse = "Mazeret";
    }

    /// <summary>Rate limiter politika isimleri.</summary>
    public static class RateLimitPolicies
    {
        public const string Login = "login";
        public const string Api   = "api";
    }

    /// <summary>Vardiya kategorileri (Shift ID'ye göre).</summary>
    public static class ShiftCategory
    {
        public const string Regular  = "Regular";   // 1, 2, 3, 6
        public const string Leave    = "Leave";     // 4, 5
        public const string Overtime = "Overtime";  // 7, 8, 9

        public static string FromShiftId(int shiftId) => shiftId switch
        {
            >= 1 and <= 3 => Regular,
            6             => Regular,
            4 or 5        => Leave,
            >= 7 and <= 9 => Overtime,
            _             => Regular
        };
    }
}
