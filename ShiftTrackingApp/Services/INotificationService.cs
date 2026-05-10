namespace ShiftTrackingApp.Services
{
    /// <summary>
    /// E-posta / push bildirim soyutlaması.
    /// MVP'de logger implementasyonu kullanılır; production'da SendGrid,
    /// SES, SMTP veya Azure Communication Services entegrasyonu eklenebilir.
    /// </summary>
    public interface INotificationService
    {
        Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
        Task NotifyLeaveReviewedAsync(int userId, string leaveType, string status, CancellationToken ct = default);
        Task NotifyShiftAssignedAsync(int userId, DateOnly date, string shiftName, CancellationToken ct = default);
    }

    /// <summary>
    /// Şimdilik sadece loglar. Production'da gerçek bir kanalla değiştirilmelidir.
    /// </summary>
    public class LoggerNotificationService : INotificationService
    {
        private readonly ILogger<LoggerNotificationService> _log;
        public LoggerNotificationService(ILogger<LoggerNotificationService> log) => _log = log;

        public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
        {
            _log.LogInformation("📧 [NOTIFICATION] To={To} Subject={Subject} Body={Body}", to, subject, body);
            return Task.CompletedTask;
        }

        public Task NotifyLeaveReviewedAsync(int userId, string leaveType, string status, CancellationToken ct = default)
        {
            _log.LogInformation("📧 [LEAVE] UserId={UserId} {Type} → {Status}", userId, leaveType, status);
            return Task.CompletedTask;
        }

        public Task NotifyShiftAssignedAsync(int userId, DateOnly date, string shiftName, CancellationToken ct = default)
        {
            _log.LogInformation("📧 [SHIFT] UserId={UserId} {Date} → {ShiftName}", userId, date, shiftName);
            return Task.CompletedTask;
        }
    }
}
