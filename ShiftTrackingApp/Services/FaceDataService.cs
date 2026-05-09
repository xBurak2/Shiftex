using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Helpers;
using ShiftTrackingApp.Models;
using ShiftTrackingApp.Services.Interfaces;

namespace ShiftTrackingApp.Services
{
    public class FaceDataService : IFaceDataService
    {
        private readonly AppDbContext _db;
        private readonly string _encKey;
        private readonly ILogger<FaceDataService> _logger;

        public FaceDataService(AppDbContext db, IConfiguration config, ILogger<FaceDataService> logger)
        {
            _db     = db;
            _logger = logger;

            // Önce environment variable, yoksa config
            _encKey = Environment.GetEnvironmentVariable("FACE_ENCRYPTION_KEY")
                      ?? config["FaceEncryption:Key"]
                      ?? throw new InvalidOperationException(
                          "FACE_ENCRYPTION_KEY ortam değişkeni veya FaceEncryption:Key config değeri tanımlı değil.");
        }

        public async Task<List<FaceDataDto>> GetAllAsync()
        {
            var records = await _db.FaceData
                .AsNoTracking()
                .Include(fd => fd.User)
                .Where(fd => fd.User.IsActive)
                .ToListAsync();

            var result = new List<FaceDataDto>();
            foreach (var r in records)
            {
                try
                {
                    var json       = EncryptionHelper.Decrypt(r.EncryptedDescriptor, _encKey);
                    var descriptor = JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>();
                    result.Add(new FaceDataDto
                    {
                        UserId       = r.UserId,
                        UserFullName = r.User.FullName,
                        UserPhoto    = r.User.PhotoBase64,
                        Descriptor   = descriptor,
                        EnrolledAt   = r.UpdatedAt
                    });
                }
                catch (Exception ex)
                {
                    // Bozuk kayıt varsa atla, uygulamayı durdurma
                    _logger.LogWarning(ex, "UserID={UserId} için yüz verisi çözülemedi, atlanıyor.", r.UserId);
                }
            }
            return result;
        }

        public async Task<FaceDataDto> SaveAsync(SaveFaceDataDto dto)
        {
            var user = await _db.Users.FindAsync(dto.UserId)
                       ?? throw new KeyNotFoundException($"Kullanıcı (ID: {dto.UserId}) bulunamadı.");

            if (!user.IsActive)
                throw new InvalidOperationException("Pasif kullanıcıya yüz verisi kaydedilemez.");

            var json      = JsonSerializer.Serialize(dto.Descriptor);
            var encrypted = EncryptionHelper.Encrypt(json, _encKey);

            var existing = await _db.FaceData.FirstOrDefaultAsync(fd => fd.UserId == dto.UserId);
            if (existing != null)
            {
                existing.EncryptedDescriptor = encrypted;
                existing.UpdatedAt           = DateTime.UtcNow;
            }
            else
            {
                _db.FaceData.Add(new FaceData
                {
                    UserId              = dto.UserId,
                    EncryptedDescriptor = encrypted,
                    CreatedAt           = DateTime.UtcNow,
                    UpdatedAt           = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Yüz verisi kaydedildi/güncellendi: UserID={UserId}", dto.UserId);

            return new FaceDataDto
            {
                UserId       = dto.UserId,
                UserFullName = user.FullName,
                UserPhoto    = user.PhotoBase64,
                Descriptor   = dto.Descriptor,
                EnrolledAt   = DateTime.UtcNow
            };
        }

        public async Task<bool> DeleteAsync(int userId)
        {
            var record = await _db.FaceData.FirstOrDefaultAsync(fd => fd.UserId == userId);
            if (record == null) return false;

            _db.FaceData.Remove(record);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Yüz verisi silindi: UserID={UserId}", userId);
            return true;
        }
    }
}
