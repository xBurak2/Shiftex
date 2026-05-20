using Serilog;
using Serilog.Events;
using ShiftTrackingApp.Middleware;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.Helpers;
using ShiftTrackingApp.Services;
using ShiftTrackingApp.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ── Kestrel — istek body boyutu sınırı (DoS koruması) ────────────────────
builder.WebHost.ConfigureKestrel(opts =>
{
    // Default 30 MB → 5 MB (yüz/fotoğraf yüklemeleri için yeterli)
    opts.Limits.MaxRequestBodySize = 5 * 1024 * 1024;
});

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

// ── Response Compression (Brotli + Gzip) ─────────────────────────────────
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json", "application/problem+json",
        "text/css", "application/javascript", "text/html",
        "image/svg+xml"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o   => o.Level = CompressionLevel.Fastest);

// ── Output Caching ───────────────────────────────────────────────────────
builder.Services.AddOutputCache(opts =>
{
    // Departments listesi: 60 sn cache (yönetici/personel bağımsız aynı)
    opts.AddPolicy("departments", b => b.Expire(TimeSpan.FromSeconds(60)));
});

// ── ProblemDetails (RFC 7807) — ASP.NET'in built-in middleware'i ──────────
builder.Services.AddProblemDetails(opts =>
{
    opts.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
    };
});

// ── Servisler ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<AccountLockoutService>(); // brute-force koruma — singleton state
builder.Services.AddScoped<JwtHelper>();
builder.Services.AddScoped<IAuthService,       AuthService>();
builder.Services.AddScoped<IUserService,       UserService>();
builder.Services.AddScoped<IShiftService,      ShiftService>();
builder.Services.AddScoped<ILeaveService,      LeaveService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IStaffingRequirementService, StaffingRequirementService>();
builder.Services.AddScoped<ICoverageService, CoverageService>();
builder.Services.AddScoped<ICasualCalloutService, CasualCalloutService>();
builder.Services.AddScoped<ISimulationService, SimulationService>();
builder.Services.AddScoped<IFaceDataService,   FaceDataService>();
builder.Services.AddScoped<ILeaveBalanceService, LeaveBalanceService>();
builder.Services.AddScoped<IShiftSwapService,    ShiftSwapService>();
builder.Services.AddScoped<IOvertimeRequestService, OvertimeRequestService>();
builder.Services.AddScoped<INotificationService, LoggerNotificationService>(); // Production'da SMTP/SendGrid impl ile değiştirin

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
        Description = "Personel Vardiya ve Devam Takip Sistemi API. " +
                      "JWT ile authentication, AES-256 ile şifrelenmiş yüz verisi, refresh token rotasyonu.",
        Contact     = new OpenApiContact
        {
            Name = "Shiftex Team",
            Url  = new Uri("https://github.com/xBurak2/Shiftex")
        },
        License     = new OpenApiLicense { Name = "Proprietary" }
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        Description  = "JWT Bearer token giriniz (login endpoint'inden alabilirsiniz)"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {{
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        },
        Array.Empty<string>()
    }});

    // XML doc'ları varsa Swagger'a dahil et (servislerin <summary> taglerini gösterir)
    var xmlPath = Path.Combine(AppContext.BaseDirectory, "ShiftTrackingApp.xml");
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});

// ── Serilog (correlation ID, environment, machine, user ile zenginleştirilmiş) ──
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("Application", "Shiftex")
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("Logs/shiftex-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{MachineName}/{ThreadId}] [{CorrelationId}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ─────────────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCorrelationId();            // En başta — her log/exception ID ile zenginleştirilsin
app.UseSecurityHeaders();          // OWASP başlıkları (CSP, X-Frame-Options, vb.)
app.UseGlobalExceptionHandler();
app.UseHttpsRedirection();
app.UseResponseCompression();      // Brotli/Gzip — JSON ve statik dosyalar için
app.UseCors("AppPolicy");
app.UseRateLimiter();
app.UseSerilogRequestLogging(opts =>
{
    opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} ({Elapsed:0} ms)";
    opts.GetLevel = (httpCtx, elapsed, ex) =>
        ex != null                            ? LogEventLevel.Error
      : httpCtx.Response.StatusCode >= 500    ? LogEventLevel.Error
      : httpCtx.Response.StatusCode >= 400    ? LogEventLevel.Warning
      : elapsed > 2000                        ? LogEventLevel.Warning   // yavaş istekler
      :                                          LogEventLevel.Information;
});
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();              // Departments gibi statik-ish endpoint'ler için

// ── Health check endpoint'leri (detaylı JSON cevap) ───────────────────────
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var payload = new
        {
            status   = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            checks   = report.Entries.Select(e => new
            {
                name        = e.Key,
                status      = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration    = e.Value.Duration.TotalMilliseconds,
                error       = e.Value.Exception?.Message
            })
        };
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
});

// Liveness — sadece self check (Azure App Service liveness probe için ideal)
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = r => r.Name == "self"
});

// Kiosk sayfası (clean URL: /kiosk)
app.MapGet("/kiosk", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(
        Path.Combine(app.Environment.WebRootPath, "kiosk.html"));
});

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
        // Migration durumunu logla (production'da debug için kritik)
        var pending = db.Database.GetPendingMigrations().ToList();
        if (pending.Any())
        {
            log.LogWarning("⚠ {Count} bekleyen migration var: {Migrations}",
                pending.Count, string.Join(", ", pending));
        }

        db.Database.Migrate();

        var applied = db.Database.GetAppliedMigrations().ToList();
        log.LogInformation("✓ DB migration tamamlandı. Toplam uygulanan: {Count}. Son: {Last}",
            applied.Count, applied.LastOrDefault() ?? "(yok)");

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

        // ── Personel ihtiyaç matrisi: tablo boşsa makul varsayılanlarla doldur ──
        // İdempotent: yalnızca hiç kayıt yoksa ekler (üzerine yazmaz).
        SeedDefaultStaffingRequirements(db, log);

        log.LogInformation("Veritabanı başarıyla hazırlandı.");
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Başlangıç hatası: {Message}", ex.Message);
    }
}

app.Run();

// ── Personel ihtiyaç matrisi varsayılan seed (idempotent) ───────────────
// Konfeksiyon atölyesi için makul başlangıç değerleri. Tablo boşsa eklenir.
static void SeedDefaultStaffingRequirements(AppDbContext db, Microsoft.Extensions.Logging.ILogger log)
{
    if (db.StaffingRequirements.Any()) return; // zaten tanımlı → dokunma

    // Departman var mı kontrolü (temiz seed'de 1-5 bekleniyor)
    var deptIds = db.Departments.Select(d => d.Id).ToHashSet();
    if (deptIds.Count == 0) return;

    // (DepartmentId, ShiftId) → hafta içi gereken sayı. Shift: 1=Sabah,2=Öğle,3=Gece
    // Hafta sonu (Cmt/Paz) yarıya iner (aşağıda hesaplanır).
    var weekday = new (int dept, int shift, int count)[]
    {
        (1,1,4), (1,2,3), (1,3,1),   // Kesim
        (2,1,6), (2,2,4), (2,3,2),   // Dikiş (en kalabalık)
        (3,1,3), (3,2,2), (3,3,0),   // Ütü & Paketleme
        (4,1,2), (4,2,1), (4,3,0),   // Kalite Kontrol
        (5,1,2), (5,2,2), (5,3,1),   // Sevkiyat
    };

    var rows = new List<ShiftTrackingApp.Models.StaffingRequirement>();
    foreach (var (dept, shift, count) in weekday)
    {
        if (!deptIds.Contains(dept) || count <= 0) continue;
        for (int dow = 0; dow < 7; dow++)
        {
            // Hafta sonu (5=Cmt, 6=Paz) ihtiyacı yarıya iner (yukarı yuvarlanır)
            int required = dow >= 5 ? (count + 1) / 2 : count;
            if (required <= 0) continue;
            rows.Add(new ShiftTrackingApp.Models.StaffingRequirement
            {
                DepartmentId  = dept,
                ShiftId       = shift,
                DayOfWeek     = dow,
                RequiredCount = required,
                UpdatedAt     = DateTime.UtcNow
            });
        }
    }

    if (rows.Count > 0)
    {
        db.StaffingRequirements.AddRange(rows);
        db.SaveChanges();
        log.LogInformation("✓ {Count} varsayılan personel ihtiyaç kaydı eklendi.", rows.Count);
    }
}
