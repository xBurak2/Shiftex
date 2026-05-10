using Serilog;
using Serilog.Events;
using ShiftTrackingApp.Middleware;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.Helpers;
using ShiftTrackingApp.Services;
using ShiftTrackingApp.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ── Veritabanı ────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── JWT ───────────────────────────────────────────────────────────────────
var jwtKey = Environment.GetEnvironmentVariable("JWT__Key")
             ?? builder.Configuration["Jwt:Key"]
             ?? throw new InvalidOperationException(
                 "JWT Key bulunamadı! 'JWT__Key' environment variable veya appsettings.json::Jwt:Key tanımlayın.");

if (jwtKey.Length < 32)
    throw new InvalidOperationException("JWT Key en az 32 karakter olmalıdır.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// ── Rate Limiting ─────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    // Login endpoint'i: 1 dakikada maksimum 10 istek
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit      = 10;
        opt.Window           = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit       = 0;
    });

    // Genel API limiti: 1 dakikada 300 istek
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.PermitLimit = 300;
        opt.Window      = TimeSpan.FromMinutes(1);
        opt.QueueLimit  = 0;
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { status = 429, message = "Çok fazla istek gönderildi. Lütfen bir süre bekleyin." },
            token);
    };
});

// ── Health Checks ─────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Uygulama çalışıyor."))
    .AddDbContextCheck<AppDbContext>("database");

// ── Servisler ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<JwtHelper>();
builder.Services.AddScoped<IAuthService,       AuthService>();
builder.Services.AddScoped<IUserService,       UserService>();
builder.Services.AddScoped<IShiftService,      ShiftService>();
builder.Services.AddScoped<ILeaveService,      LeaveService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IFaceDataService,   FaceDataService>();

// ── CORS ──────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(o => o.AddPolicy("AppPolicy", p =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Geliştirme ortamında localhost'a izin ver
        p.SetIsOriginAllowed(origin =>
            origin.StartsWith("https://localhost") || origin.StartsWith("http://localhost"))
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials();
    }
    else
    {
        p.WithOrigins(allowedOrigins)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials();
    }
}));

// ── Controller & JSON ─────────────────────────────────────────────────────
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .SelectMany(e => e.Value!.Errors.Select(x => x.ErrorMessage))
                .ToList();
            return new Microsoft.AspNetCore.Mvc.ObjectResult(new
            {
                status  = 400,
                message = string.Join(" | ", errors),
                errors
            })
            { StatusCode = 400 };
        };
    })
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Shiftex API",
        Version     = "v1",
        Description = "Personel Vardiya ve Devam Takip Sistemi API"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.Http,
        Scheme      = "Bearer",
        Description = "JWT Bearer token giriniz"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {{
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        },
        Array.Empty<string>()
    }});
});

// ── Serilog ───────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("Logs/shiftex-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ─────────────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseGlobalExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("AppPolicy");
app.UseRateLimiter();
app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// ── Health check endpoint'leri ────────────────────────────────────────────
app.MapHealthChecks("/health");

// Root path → index.html (redirect değil, direkt servis et — URL'de /index.html görünmesin)
app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(
        Path.Combine(app.Environment.WebRootPath, "index.html"));
});

app.MapControllers();

// ── Başlangıç migrasyonu & indeks düzeltmesi ─────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        db.Database.Migrate();

        db.Database.ExecuteSqlRaw(@"
            IF OBJECT_ID('ShiftAssignments') IS NOT NULL
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_ShiftAssignments_UserId_Date'
                      AND object_id = OBJECT_ID('ShiftAssignments')
                )
                BEGIN
                    DROP INDEX IX_ShiftAssignments_UserId_Date ON ShiftAssignments;
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_ShiftAssignments_UserId_Date_ShiftId'
                      AND object_id = OBJECT_ID('ShiftAssignments')
                )
                BEGIN
                    CREATE UNIQUE INDEX IX_ShiftAssignments_UserId_Date_ShiftId
                    ON ShiftAssignments (UserId, Date, ShiftId);
                END
            END
        ");

        log.LogInformation("Veritabanı başarıyla hazırlandı.");
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Başlangıç hatası: {Message}", ex.Message);
    }
}

app.Run();
