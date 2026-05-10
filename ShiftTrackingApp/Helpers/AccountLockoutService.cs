using System.Collections.Concurrent;

namespace ShiftTrackingApp.Helpers
{
    /// <summary>
    /// Brute-force koruması: in-memory başarısız giriş sayacı.
    /// Production'da distributed cache (Redis) önerilir; horizontal scale'de
    /// her instance kendi sayacını tutar, sınır gerçekte 5×N olabilir.
    /// </summary>
    public class AccountLockoutService
    {
        private record Attempt(int Count, DateTime FirstAttempt, DateTime? LockoutUntil);

        private readonly ConcurrentDictionary<string, Attempt> _attempts = new();
        private readonly ILogger<AccountLockoutService> _log;

        public int MaxAttempts             { get; set; } = 5;
        public TimeSpan LockoutDuration    { get; set; } = TimeSpan.FromMinutes(15);
        public TimeSpan AttemptWindow      { get; set; } = TimeSpan.FromMinutes(10);

        public AccountLockoutService(ILogger<AccountLockoutService> log) => _log = log;

        /// <summary>Hesap kilitli mi kontrol et. Kilitliyse kalan süreyi döner.</summary>
        public TimeSpan? GetLockoutRemaining(string key)
        {
            if (_attempts.TryGetValue(key.ToLowerInvariant(), out var a) && a.LockoutUntil.HasValue)
            {
                var remaining = a.LockoutUntil.Value - DateTime.UtcNow;
                if (remaining > TimeSpan.Zero) return remaining;
                _attempts.TryRemove(key.ToLowerInvariant(), out _);
            }
            return null;
        }

        /// <summary>Başarısız giriş kaydet. Eşiği aşıyorsa kilit uygula.</summary>
        public void RegisterFailure(string key)
        {
            var k = key.ToLowerInvariant();
            _attempts.AddOrUpdate(
                k,
                _ => new Attempt(1, DateTime.UtcNow, null),
                (_, old) =>
                {
                    // Pencerenin dışında ise sayacı sıfırla
                    if (DateTime.UtcNow - old.FirstAttempt > AttemptWindow)
                        return new Attempt(1, DateTime.UtcNow, null);

                    var count = old.Count + 1;
                    DateTime? until = count >= MaxAttempts
                        ? DateTime.UtcNow.Add(LockoutDuration)
                        : null;

                    if (until.HasValue)
                        _log.LogWarning("Hesap kilitlendi: {Key} — {Until:O}", k, until);

                    return new Attempt(count, old.FirstAttempt, until);
                });
        }

        /// <summary>Başarılı giriş — sayacı sıfırla.</summary>
        public void RegisterSuccess(string key) => _attempts.TryRemove(key.ToLowerInvariant(), out _);
    }
}
