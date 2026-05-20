using System.Net;
using System.Text.Json;

namespace ShiftTrackingApp.Middleware
{
    /// <summary>
    /// Tüm beklenmeyen exception'ları RFC 7807 ProblemDetails formatında dönen middleware.
    /// Production'da iç hata mesajları sızdırılmaz; correlation ID ile loglara bağlanır.
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger,
            IWebHostEnvironment env)
        {
            _next   = next;
            _logger = logger;
            _env    = env;
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            try
            {
                await _next(ctx);
            }
            catch (KeyNotFoundException ex)
            {
                await WriteProblem(ctx, HttpStatusCode.NotFound, "Not Found", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                await WriteProblem(ctx, HttpStatusCode.BadRequest, "Bad Request", ex.Message);
            }
            catch (ArgumentException ex)
            {
                await WriteProblem(ctx, HttpStatusCode.BadRequest, "Bad Request", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                await WriteProblem(ctx, HttpStatusCode.Unauthorized, "Unauthorized", ex.Message);
            }
            catch (BadHttpRequestException ex) when (ex.StatusCode == 413)
            {
                await WriteProblem(ctx, HttpStatusCode.RequestEntityTooLarge,
                    "Payload Too Large",
                    "Yüklediğiniz veri çok büyük (en fazla 5 MB).");
            }
            catch (Exception ex)
            {
                var correlationId = ctx.TraceIdentifier;
                _logger.LogError(ex,
                    "Beklenmeyen hata. CorrelationId={CorrelationId} Path={Path}",
                    correlationId, ctx.Request.Path);

                // GEÇİCİ DEBUG: production'da da exception mesajını dön (diagnoz sonrası geri alınacak)
                var detail = _env.IsDevelopment()
                    ? ex.ToString()
                    : $"[DEBUG] {ex.GetType().Name}: {ex.Message} || INNER: {ex.InnerException?.Message ?? "(none)"}";

                await WriteProblem(ctx, HttpStatusCode.InternalServerError,
                    "Internal Server Error", detail, correlationId);
            }
        }

        /// <summary>RFC 7807 application/problem+json formatında hata cevabı yazar.</summary>
        private static async Task WriteProblem(
            HttpContext ctx, HttpStatusCode code, string title, string detail, string? correlationId = null)
        {
            ctx.Response.StatusCode  = (int)code;
            ctx.Response.ContentType = "application/problem+json";

            var problem = new
            {
                type     = $"https://httpstatuses.com/{(int)code}",
                title,
                status   = (int)code,
                detail,
                instance = ctx.Request.Path.ToString(),
                traceId  = correlationId ?? ctx.TraceIdentifier,
                // Backward-compat: eski client'lar "message" alanını okuyor
                message  = detail
            };

            await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }

    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(
            this IApplicationBuilder app)
            => app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
