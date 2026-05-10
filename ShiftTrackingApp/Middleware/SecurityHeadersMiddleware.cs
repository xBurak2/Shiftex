namespace ShiftTrackingApp.Middleware
{
    /// <summary>
    /// Tarayıcı tabanlı saldırılara karşı güvenlik başlıkları ekler.
    /// OWASP önerilerine uygun olarak yapılandırılmıştır.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly bool _isDevelopment;

        public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
        {
            _next          = next;
            _isDevelopment = env.IsDevelopment();
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            var headers = ctx.Response.Headers;

            // Tarayıcının MIME-sniffing yapmasını engelle (X-Content-Type-Options: nosniff)
            headers["X-Content-Type-Options"] = "nosniff";

            // Iframe içine sokulmayı engelle (clickjacking koruması)
            headers["X-Frame-Options"] = "DENY";

            // Çapraz origin'lere referer sızıntısını sınırla
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Sensör/hardware erişimini sınırla (kamera yüz tanıma için aynı origin'de izin)
            headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=(), payment=(), usb=()";

            // Cross-origin pencere açılışlarında koruma
            headers["Cross-Origin-Opener-Policy"]   = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";

            // CSP — face-api.js CDN ve Google Fonts'a izin ver
            // Production'da nonce/hash kullanılarak inline scriptler tamamen kaldırılmalı.
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                "style-src  'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                "font-src   'self' https://fonts.gstatic.com; " +
                "img-src    'self' data: blob:; " +
                "media-src  'self' blob:; " +
                "connect-src 'self' https://cdn.jsdelivr.net; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'";

            // HSTS — sadece HTTPS production'da
            if (!_isDevelopment)
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

            // Server header'ı gizle (versiyon bilgisi sızdırma)
            headers.Remove("Server");

            await _next(ctx);
        }
    }

    public static class SecurityHeadersMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
            => app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
