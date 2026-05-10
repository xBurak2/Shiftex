# Shiftex Güvenlik Politikası

## Güvenlik Açığı Bildirimi

Bir güvenlik açığı keşfederseniz **public bir issue açmayın**.
E-posta: `security@shiftexapp.com` (placeholder)

48 saat içinde alındı onayı verilir; düzeltme yayınlanana kadar gizli tutulur.

---

## Tehdit Modeli

| Tehdit | Karşı Önlem |
|--------|-------------|
| **Brute-force login** | Rate limit (10 req/dk) + Account lockout (5 deneme = 15 dk) |
| **Şifre sızıntısı** | BCrypt hash (cost 11) + güçlü şifre politikası (8+ karakter, karışık) |
| **JWT token theft** | 60 dk kısa ömür, refresh token rotasyonu, SHA-256 hash DB'de |
| **XSS** | Frontend `esc()` helper, CSP header, `X-Content-Type-Options: nosniff` |
| **Clickjacking** | `X-Frame-Options: DENY`, CSP `frame-ancestors 'none'` |
| **CSRF** | Bearer token (cookie değil) + same-origin policy |
| **SQL Injection** | EF Core parametrized queries (raw SQL'de bile) |
| **Biyometrik veri sızıntısı** | AES-256-CBC encryption (random IV), key Azure secret store'da |
| **Man-in-the-middle** | HTTPS zorunlu (HttpsRedirection), HSTS production'da |
| **DoS** | Request body 5 MB limit, rate limiting |
| **Information disclosure** | Production'da exception detayları gizlenir; sadece `traceId` döner |

---

## Yetkilendirme Modeli

### Roller
| Rol | Yetkiler |
|-----|----------|
| **Admin** | Tüm kullanıcılar üzerinde CRUD, vardiya planı oluşturma, izin onaylama, yüz kaydı, raporlar |
| **Employee** | Kendi profili, kendi vardiyaları, kendi izinleri, kendi devam kaydı, takım görüntüleme (readonly) |

### Yetki Kontrolleri
- Controller seviyesi: `[Authorize(Roles = "Admin")]`
- Method seviyesi: caller ID vs target ID karşılaştırması (`if (role != Admin && callerId != id) return Forbid();`)

---

## Şifrelerle İlgili Kurallar

- **Minimum:** 8 karakter
- **Zorunlu:** En az bir büyük harf, bir küçük harf, bir rakam
- **Saklama:** BCrypt (cost factor 11, salt rastgele)
- **Reset:** Şu an yok (gelecek özelliği)

---

## Production Konfigürasyon Checklist

- [x] `Jwt:Key` — en az 32 karakter, environment variable (Azure App Settings)
- [x] `FaceEncryption:Key` — Base64, 32-byte, environment variable
- [x] `ConnectionStrings:DefaultConnection` — Azure SQL connection string
- [x] `Cors:AllowedOrigins` — sadece kullanılan domain'ler
- [x] HTTPS zorunlu (HSTS production middleware'de)
- [x] Log retention 14 gün
- [x] Database backup (Azure SQL Basic — 7 gün otomatik PITR)

---

## Bağımlılık Güvenliği

```bash
# Vulnerability tarama
dotnet list package --vulnerable
dotnet list package --outdated
```

Aylık olarak bağımlılıkları güncelleyin (Dependabot önerilir).
