using System.Net;
using System.Text.Json;

namespace ShiftTrackingApp.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger)
        {
            _next   = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            try
            {
                await _next(ctx);
            }
            catch (KeyNotFoundException ex)
            {
                await WriteError(ctx, HttpStatusCode.NotFound, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                await WriteError(ctx, HttpStatusCode.BadRequest, ex.Message);
            }
            catch (ArgumentException ex)
            {
                await WriteError(ctx, HttpStatusCode.BadRequest, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                await WriteError(ctx, HttpStatusCode.Unauthorized, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Beklenmeyen hata: {Message}", ex.Message);
                await WriteError(ctx, HttpStatusCode.InternalServerError,
                    "Beklenmeyen bir sunucu hatası oluştu.");
            }
        }

        private static async Task WriteError(HttpContext ctx, HttpStatusCode code, string message)
        {
            ctx.Response.StatusCode  = (int)code;
            ctx.Response.ContentType = "application/json";
            var body = JsonSerializer.Serialize(new
            {
                status  = (int)code,
                message
            });
            await ctx.Response.WriteAsync(body);
        }
    }

    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(
            this IApplicationBuilder app)
            => app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
