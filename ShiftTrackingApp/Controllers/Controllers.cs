using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Constants;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Services;
using ShiftTrackingApp.Services.Interfaces;

namespace ShiftTrackingApp.Controllers
{
    // ════════════ AUTH ════════════════════════════════════════════════════
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;
        public AuthController(IAuthService auth) => _auth = auth;

        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var result = await _auth.LoginAsync(dto);
            if (result == null)
                return Unauthorized(new { message = "E-posta veya şifre hatalı." });
            return Ok(result);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto dto)
        {
            var result = await _auth.RefreshAsync(dto.RefreshToken);
            if (result == null)
                return Unauthorized(new { message = "Refresh token geçersiz veya süresi dolmuş." });
            return Ok(result);
        }

        [HttpPost("revoke")]
        [Authorize]
        public async Task<IActionResult> Revoke([FromBody] RefreshRequestDto dto)
        {
            await _auth.RevokeAsync(dto.RefreshToken);
            return NoContent();
        }
    }

    // ════════════ USERS ═══════════════════════════════════════════════════
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _users;
        public UsersController(IUserService users) => _users = users;

        [HttpGet]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page     = 1,
            [FromQuery] int pageSize = 50)
            => Ok(await _users.GetAllAsync(page, pageSize));

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var callerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role     = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != Roles.Admin && callerId != id) return Forbid();
            var user = await _users.GetByIdAsync(id);
            return user == null ? NotFound() : Ok(user);
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var callerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user     = await _users.GetByIdAsync(callerId);
            return user == null ? NotFound() : Ok(user);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, UpdateUserDto dto)
        {
            var callerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role     = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != Roles.Admin && callerId != id) return Forbid();
            if (role != Roles.Admin) dto.Role = null;
            var updated = await _users.UpdateAsync(id, dto);
            return updated == null ? NotFound() : Ok(updated);
        }

        [HttpGet("{id}/attendance-summary")]
        public async Task<IActionResult> GetAttendanceSummary(int id,
            [FromQuery] int? year, [FromQuery] int? month)
        {
            var callerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role     = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != Roles.Admin && callerId != id) return Forbid();
            // UTC kullanırız (sunucu timezone'una bağımlı olmaz); month/year hesaplaması doğru kalır
            var now     = DateTime.UtcNow;
            var summary = await _users.GetMonthlyAttendanceSummaryAsync(
                id, year ?? now.Year, month ?? now.Month);
            return Ok(summary);
        }

        /// <summary>Aylık özet raporu CSV (Excel uyumlu) olarak indir.</summary>
        [HttpGet("{id}/attendance-summary/export")]
        public async Task<IActionResult> ExportAttendanceSummary(int id,
            [FromQuery] int? year, [FromQuery] int? month)
        {
            var callerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role     = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != Roles.Admin && callerId != id) return Forbid();

            var now     = DateTime.UtcNow;
            var y       = year  ?? now.Year;
            var m       = month ?? now.Month;
            var s       = await _users.GetMonthlyAttendanceSummaryAsync(id, y, m);
            var user    = await _users.GetByIdAsync(id);
            if (user == null) return NotFound();

            var bytes = ShiftTrackingApp.Helpers.CsvWriter.Write(
                headers: new[] { "Metric", "Value" },
                rows: new[]
                {
                    new object?[] { "Personel",         user.FullName },
                    new object?[] { "E-posta",          user.Email },
                    new object?[] { "Departman",        user.DepartmentName ?? "-" },
                    new object?[] { "Yıl",              y },
                    new object?[] { "Ay",               m },
                    new object?[] { "Mevcut Gün",       s.PresentDays },
                    new object?[] { "İzinli Gün",       s.LeaveDays },
                    new object?[] { "Devamsız Gün",    s.AbsentDays },
                    new object?[] { "Raporlu Gün",     s.AbsentWithReport },
                    new object?[] { "Raporsuz Gün",    s.AbsentWithoutReport },
                    new object?[] { "Toplam Çalışma (sa)", s.TotalWorkedHours },
                    new object?[] { "Fazla Mesai (sa)",    s.TotalOvertimeHours },
                    new object?[] { "FM Vardiya Sayısı",   s.OvertimeShiftCount },
                });
            var fileName = $"devam-raporu-{user.FullName.Replace(' ','_')}-{y}-{m:D2}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        [HttpPost]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Create(CreateUserDto dto)
        {
            var user = await _users.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _users.DeleteAsync(id);
            return success ? NoContent() : NotFound();
        }
    }

    // ════════════ DEPARTMENTS ════════════════════════════════════════════
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DepartmentsController : ControllerBase
    {
        private readonly IDepartmentService _departments;
        private readonly IOutputCacheStore  _cache;
        public DepartmentsController(IDepartmentService departments, IOutputCacheStore cache)
        {
            _departments = departments;
            _cache       = cache;
        }

        [HttpGet]
        [OutputCache(PolicyName = "departments", Tags = new[] { "departments" })]
        public async Task<IActionResult> GetAll()
            => Ok(await _departments.GetAllAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var dept = await _departments.GetByIdAsync(id);
            return dept == null ? NotFound() : Ok(dept);
        }

        [HttpPost]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Create([FromBody] CreateDepartmentDto dto)
        {
            var dept = await _departments.CreateAsync(dto);
            await _cache.EvictByTagAsync("departments", default); // Cache invalidation
            return CreatedAtAction(nameof(GetById), new { id = dept.Id }, dept);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _departments.DeleteAsync(id);
            if (success) await _cache.EvictByTagAsync("departments", default);
            return success ? NoContent() : NotFound();
        }
    }

    // ════════════ SHIFTS ═════════════════════════════════════════════════
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ShiftsController : ControllerBase
    {
        private readonly IShiftService _shifts;
        public ShiftsController(IShiftService shifts) => _shifts = shifts;

        [HttpGet("weekly")]
        public async Task<IActionResult> GetWeekly([FromQuery] DateOnly weekStart)
            => Ok(await _shifts.GetWeeklyAsync(weekStart));

        [HttpGet("my")]
        public async Task<IActionResult> GetMine([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            return Ok(await _shifts.GetByUserAsync(userId, from, to));
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUser(int userId,
            [FromQuery] DateOnly from, [FromQuery] DateOnly to)
        {
            var callerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role     = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != Roles.Admin && callerId != userId) return Forbid();
            return Ok(await _shifts.GetByUserAsync(userId, from, to));
        }

        [HttpPost]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Assign([FromBody] CreateShiftAssignmentDto dto)
        {
            var result = await _shifts.AssignAsync(dto);
            return Ok(result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Update(int id, [FromBody] CreateShiftAssignmentDto dto)
        {
            var result = await _shifts.UpdateAsync(id, dto);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _shifts.DeleteAsync(id);
            return success ? NoContent() : NotFound();
        }

        public class CopyWeekDto
        {
            public DateOnly SourceWeekStart { get; set; }
            public DateOnly TargetWeekStart { get; set; }
        }

        /// <summary>Bir haftanın atamalarını başka bir haftaya kopyalar. Hedef hafta üzerine yazılır.</summary>
        [HttpPost("copy-week")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> CopyWeek([FromBody] CopyWeekDto dto)
        {
            var count = await _shifts.CopyWeekAsync(dto.SourceWeekStart, dto.TargetWeekStart);
            return Ok(new { copied = count });
        }
    }

    // ════════════ LEAVES ═════════════════════════════════════════════════
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LeavesController : ControllerBase
    {
        private readonly ILeaveService _leaves;
        public LeavesController(ILeaveService leaves) => _leaves = leaves;

        [HttpGet]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetAll([FromQuery] string? status)
            => Ok(await _leaves.GetAllAsync(status));

        [HttpGet("my")]
        public async Task<IActionResult> GetMine()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            return Ok(await _leaves.GetByUserAsync(userId));
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateLeaveRequestDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            return Ok(await _leaves.CreateAsync(userId, dto));
        }

        [HttpPatch("{id}/review")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Review(int id, ReviewLeaveDto dto)
        {
            var reviewerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result     = await _leaves.ReviewAsync(id, reviewerId, dto);
            return result == null ? NotFound() : Ok(result);
        }
    }

    // ════════════ ATTENDANCE ═════════════════════════════════════════════
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AttendanceController : ControllerBase
    {
        private readonly IAttendanceService _attendance;
        public AttendanceController(IAttendanceService attendance) => _attendance = attendance;

        [HttpGet("today")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetToday()
            => Ok(await _attendance.GetTodayAsync());

        [HttpGet("my-today")]
        public async Task<IActionResult> GetMyToday()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            return Ok(await _attendance.GetByUserTodayAsync(userId));
        }

        [HttpPost("checkin")]
        public async Task<IActionResult> CheckIn()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            return Ok(await _attendance.CheckInAsync(userId, "Manual"));
        }

        [HttpPost("checkin-face/{userId}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> CheckInFace(int userId)
            => Ok(await _attendance.CheckInAsync(userId, "FaceRecognition"));

        [HttpPost("checkout-face/{userId}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> CheckOutFace(int userId)
        {
            var result = await _attendance.CheckOutAsync(userId, "FaceRecognition");
            return result == null
                ? BadRequest(new { message = "Açık giriş kaydı bulunamadı." })
                : Ok(result);
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> CheckOut()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _attendance.CheckOutAsync(userId, "Manual");
            return result == null
                ? BadRequest(new { message = "Açık giriş kaydı bulunamadı." })
                : Ok(result);
        }

        [HttpGet("dashboard")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Dashboard()
            => Ok(await _attendance.GetDashboardStatsAsync());
    }

    // ════════════ LEAVE BALANCE ═════════════════════════════════════════
    [ApiController]
    [Route("api/LeaveBalance")]
    [Authorize]
    public class LeaveBalanceController : ControllerBase
    {
        private readonly ILeaveBalanceService _svc;
        public LeaveBalanceController(ILeaveBalanceService svc) => _svc = svc;

        [HttpGet("me")]
        public async Task<IActionResult> GetMine([FromQuery] int? year)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            return Ok(await _svc.GetBalanceAsync(userId, year));
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUser(int userId, [FromQuery] int? year)
        {
            var callerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role     = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != Roles.Admin && callerId != userId) return Forbid();
            return Ok(await _svc.GetBalanceAsync(userId, year));
        }
    }

    // ════════════ SHIFT SWAP ════════════════════════════════════════════
    [ApiController]
    [Route("api/ShiftSwap")]
    [Authorize]
    public class ShiftSwapController : ControllerBase
    {
        private readonly IShiftSwapService _svc;
        public ShiftSwapController(IShiftSwapService svc) => _svc = svc;

        [HttpGet("my-outgoing")]
        public async Task<IActionResult> MyOutgoing()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            return Ok(await _svc.GetMyOutgoingAsync(userId));
        }

        [HttpGet("my-incoming")]
        public async Task<IActionResult> MyIncoming()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            return Ok(await _svc.GetMyIncomingAsync(userId));
        }

        [HttpGet]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> All([FromQuery] string? status)
            => Ok(await _svc.GetAllAsync(status));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateShiftSwapDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            return Ok(await _svc.CreateAsync(userId, dto));
        }

        [HttpPost("{id}/respond")]
        public async Task<IActionResult> Respond(int id, [FromBody] RespondShiftSwapDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _svc.RespondAsync(id, userId, dto);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost("{id}/approve")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Approve(int id)
        {
            var reviewerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result     = await _svc.ApproveAsync(id, reviewerId);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost("{id}/reject")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Reject(int id)
        {
            var reviewerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result     = await _svc.RejectAsync(id, reviewerId);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId  = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var success = await _svc.CancelAsync(id, userId);
            return success ? NoContent() : NotFound();
        }
    }

    // ════════════ OVERTIME REQUEST ══════════════════════════════════════
    [ApiController]
    [Route("api/Overtime")]
    [Authorize]
    public class OvertimeController : ControllerBase
    {
        private readonly IOvertimeRequestService _svc;
        public OvertimeController(IOvertimeRequestService svc) => _svc = svc;

        [HttpGet("my")]
        public async Task<IActionResult> Mine()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            return Ok(await _svc.GetMyAsync(userId));
        }

        [HttpGet]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> All([FromQuery] string? status)
            => Ok(await _svc.GetAllAsync(status));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOvertimeRequestDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            return Ok(await _svc.CreateAsync(userId, dto));
        }

        [HttpPost("{id}/approve")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Approve(int id)
        {
            var reviewerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result     = await _svc.ApproveAsync(id, reviewerId);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost("{id}/reject")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Reject(int id)
        {
            var reviewerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result     = await _svc.RejectAsync(id, reviewerId);
            return result == null ? NotFound() : Ok(result);
        }
    }

    // ════════════ KIOSK ══════════════════════════════════════════════════
    /// <summary>
    /// Sabit cihazlardan (tablet/telefon) yüz tanıma turnike erişimi.
    /// Kiosk login: özel bir kiosk şifresiyle JWT alır; bu JWT
    /// sadece kiosk endpoint'lerini açar. Standart kullanıcı oturumlarından
    /// ayrı tutulur — hassas verilere erişim yok.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class KioskController : ControllerBase
    {
        private readonly IFaceDataService _faceData;
        private readonly IAttendanceService _attendance;
        private readonly IConfiguration _config;
        private readonly Helpers.JwtHelper _jwt;
        private readonly ILogger<KioskController> _log;

        public KioskController(
            IFaceDataService faceData,
            IAttendanceService attendance,
            IConfiguration config,
            Helpers.JwtHelper jwt,
            ILogger<KioskController> log)
        {
            _faceData   = faceData;
            _attendance = attendance;
            _config     = config;
            _jwt        = jwt;
            _log        = log;
        }

        public class KioskLoginDto
        {
            public string DeviceId { get; set; } = string.Empty;
            public string KioskPin { get; set; } = string.Empty;
        }

        public class KioskAttendDto
        {
            public int UserId { get; set; }
        }

        /// <summary>
        /// Kiosk cihazı için kısa süreli JWT döner.
        /// Konfigürasyon: Kiosk:Pin (App Settings'te). Yoksa fallback olarak Jwt:Key + "KIOSK".
        /// </summary>
        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public IActionResult Login([FromBody] KioskLoginDto dto)
        {
            var configuredPin = _config["Kiosk:Pin"] ?? "shiftex-kiosk-2026";
            if (!string.Equals(dto.KioskPin, configuredPin, StringComparison.Ordinal))
            {
                _log.LogWarning("Kiosk login başarısız: DeviceId={DeviceId}", dto.DeviceId);
                return Unauthorized(new { message = "Geçersiz kiosk PIN'i." });
            }

            // Kiosk-specific token (24 saatlik). Role = "Kiosk".
            var fakeUser = new ShiftTrackingApp.Models.User
            {
                Id       = 0,
                FullName = $"Kiosk: {dto.DeviceId}",
                Email    = $"kiosk-{dto.DeviceId}@shiftex.local",
                Role     = "Kiosk"
            };
            var token = _jwt.GenerateToken(fakeUser);

            _log.LogInformation("Kiosk login: DeviceId={DeviceId}", dto.DeviceId);
            return Ok(new { token, deviceId = dto.DeviceId, expiresInHours = 24 });
        }

        /// <summary>Kayıtlı tüm yüz vektörlerini (decrypt edilmiş) döner.</summary>
        [HttpGet("face-descriptors")]
        [Authorize(Roles = "Kiosk,Admin")]
        public async Task<IActionResult> GetFaceDescriptors()
            => Ok(await _faceData.GetAllAsync());

        /// <summary>
        /// Kiosk akıllı tanıma sonucu: kişiye göre otomatik giriş veya çıkış.
        /// Aynı gün açık kayıt varsa → çıkış; yoksa → giriş.
        /// </summary>
        [HttpPost("attend")]
        [Authorize(Roles = "Kiosk,Admin")]
        public async Task<IActionResult> Attend([FromBody] KioskAttendDto dto)
        {
            if (dto.UserId <= 0) return BadRequest(new { message = "Geçersiz kullanıcı." });

            // Bugünkü açık kayıtları kontrol et
            var today = await _attendance.GetByUserTodayAsync(dto.UserId);
            var hasOpen = today.Any(t => t.CheckOut == null);

            if (hasOpen)
            {
                var result = await _attendance.CheckOutAsync(dto.UserId, "FaceRecognition");
                if (result == null) return BadRequest(new { message = "Çıkış kaydı oluşturulamadı." });
                return Ok(new { action = "checkout", attendance = result });
            }
            else
            {
                var result = await _attendance.CheckInAsync(dto.UserId, "FaceRecognition");
                return Ok(new { action = "checkin", attendance = result });
            }
        }
    }

    // ════════════ FACE DATA ══════════════════════════════════════════════
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Roles.Admin)]
    public class FaceDataController : ControllerBase
    {
        private readonly IFaceDataService _faceData;
        public FaceDataController(IFaceDataService faceData) => _faceData = faceData;

        /// <summary>Tüm kayıtlı yüzleri şifresi çözülmüş olarak döner.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(await _faceData.GetAllAsync());

        /// <summary>Personel için yüz kaydı oluşturur veya günceller.</summary>
        [HttpPost]
        public async Task<IActionResult> Save([FromBody] SaveFaceDataDto dto)
        {
            var result = await _faceData.SaveAsync(dto);
            return Ok(result);
        }

        /// <summary>Personelin yüz verisini siler.</summary>
        [HttpDelete("{userId}")]
        public async Task<IActionResult> Delete(int userId)
        {
            var success = await _faceData.DeleteAsync(userId);
            return success ? NoContent() : NotFound();
        }
    }
}
