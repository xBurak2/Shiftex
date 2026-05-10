using Serilog.Context;

namespace ShiftTrackingApp.Middleware
{
    /// <summary>
    /// Her isteğe benzersiz bir correlation ID atar.
    /// İstemci "X-Correlation-Id" header'ı gönderirse onu kullanır,
    /// yoksa yeni bir GUID üretir. Logger context'ine push edilir; cevap
    /// header'ında geri döner. Bu sayede production'da bir hata raporu
    /// gelirse logları tek bir ID ile takip edebilirsiniz.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private const string HeaderName = "X-Correlation-Id";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext ctx)
        {
            var correlationId = ctx.Request.Headers.TryGetValue(HeaderName, out var v) && !string.IsNullOrWhiteSpace(v)
                ? v.ToString()
                : Guid.NewGuid().ToString("N");

            ctx.TraceIdentifier = correlationId;
            ctx.Response.Headers[HeaderName] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("UserId", ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous"))
            {
                await _next(ctx);
            }
        }
    }

    public static class CorrelationIdMiddlewareExtensions
    {
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
            => app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
