using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Helpers;
using ShiftTrackingApp.Models;
using ShiftTrackingApp.Services.Interfaces;

namespace ShiftTrackingApp.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly JwtHelper _jwt;
        private readonly IConfiguration _config;

        public AuthService(AppDbContext db, JwtHelper jwt, IConfiguration config)
        {
            _db     = db;
            _jwt    = jwt;
            _config = config;
        }

        public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
        {
            var user = await _db.Users
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive);

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return null;

            var token        = _jwt.GenerateToken(user);
            var refreshToken = await CreateRefreshTokenAsync(user.Id);

            return new AuthResponseDto
            {
                Token        = token,
                RefreshToken = refreshToken,
                FullName     = user.FullName,
                Email        = user.Email,
                Role         = user.Role,
                UserId       = user.Id,
                PhotoBase64  = user.PhotoBase64
            };
        }

        public async Task<AuthResponseDto?> RefreshAsync(string refreshToken)
        {
            var rt = await _db.RefreshTokens
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Token == refreshToken);

            if (rt == null || rt.IsRevoked || rt.ExpiresAt < DateTime.UtcNow || !rt.User.IsActive)
                return null;

            rt.IsRevoked = true;
            var newToken        = _jwt.GenerateToken(rt.User);
            var newRefreshToken = await CreateRefreshTokenAsync(rt.User.Id);
            await _db.SaveChangesAsync();

            return new AuthResponseDto
            {
                Token        = newToken,
                RefreshToken = newRefreshToken,
                FullName     = rt.User.FullName,
                Email        = rt.User.Email,
                Role         = rt.User.Role,
                UserId       = rt.User.Id,
                PhotoBase64  = rt.User.PhotoBase64
            };
        }

        public async Task<bool> RevokeAsync(string refreshToken)
        {
            var rt = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken);
            if (rt == null) return false;
            rt.IsRevoked = true;
            await _db.SaveChangesAsync();
            return true;
        }

        private async Task<string> CreateRefreshTokenAsync(int userId)
        {
            const int maxTokens = 5;

            // Kullanıcının aktif token sayısı sınırı aşıyorsa en eskisini iptal et
            var activeTokens = await _db.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow)
                .OrderBy(rt => rt.CreatedAt)
                .ToListAsync();

            if (activeTokens.Count >= maxTokens)
                activeTokens.First().IsRevoked = true;

            var days    = int.TryParse(_config["Jwt:RefreshExpiresInDays"], out var d) ? d : 30;
            var token   = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var newRt   = new RefreshToken
            {
                UserId    = userId,
                Token     = token,
                ExpiresAt = DateTime.UtcNow.AddDays(days),
                CreatedAt = DateTime.UtcNow
            };
            _db.RefreshTokens.Add(newRt);
            await _db.SaveChangesAsync();
            return token;
        }
    }
}
