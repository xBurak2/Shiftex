# Shiftex — Personel Vardiya ve Devam Takip Sistemi

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core 8](https://img.shields.io/badge/EF%20Core-8.0-blueviolet)](https://learn.microsoft.com/ef/core/)
[![Azure](https://img.shields.io/badge/Azure-App%20Service-0078D4?logo=microsoftazure)](https://azure.microsoft.com/)
[![Tests](https://img.shields.io/badge/tests-32%20passing-brightgreen)](#testler)

**Shiftex**, KOBİ'ler için tasarlanmış bir personel vardiya yönetimi ve yüz tanıma ile devam takip sistemidir.
ASP.NET Core 8 Web API + Azure SQL + Vanilla JS SPA mimarisi üzerine kuruludur ve `https://www.shiftexapp.com`
adresinde production'da çalışır.

---

## ✨ Özellikler

### 🔐 Güvenlik
- JWT (60 dk) + Refresh Token (30 gün) — SHA-256 hash'li DB saklama, rotasyonlu, max 5 aktif/user
- BCrypt şifre hash'leme
- **Account lockout** (5 başarısız denemede 15 dk kilit)
- **Strong password policy** (min 8 karakter, büyük + küçük + rakam)
- OWASP **security headers** (CSP, HSTS, X-Frame-Options, Permissions-Policy)
- **Rate limiting** (login 10/dk, API 300/dk)
- **AES-256-CBC** ile yüz tanıma verisi şifreleme (rastgele IV, ciphertext'e prepend)

### 🎯 İş Mantığı
- Rol bazlı erişim (Admin / Employee)
- Soft-delete + bağımlı kayıtların güvenli temizlenmesi (FK SetNull / cascade)
- Vardiya kategorileri: **Vardiyalar / Tatil-İzin / Fazla Mesai** (aynı güne çoklu atama)
- Yüz tanıma client-side (face-api.js v0.22.2) → encrypted descriptor backend'de
- Aylık devam özeti + **CSV export** (Excel uyumlu, UTF-8 BOM)

### 🏗️ Mimari Kalite
- RFC 7807 **ProblemDetails** standart hata cevapları
- **Correlation ID** middleware (X-Correlation-Id)
- **Output caching** + **Response compression** (Brotli/Gzip)
- Detaylı **health checks** (`/health`, `/health/live`)
- Yapılandırılmış Serilog (correlation ID, machine name, environment)

---

## 🚀 Hızlı Başlangıç

### Önkoşullar
- .NET 8 SDK
- SQL Server LocalDB / SQL Server / Azure SQL
- (İsteğe bağlı) Docker

### Yerel çalıştırma

```bash
cd ShiftTrackingApp

# Konfigürasyonu hazırla (git-ignored)
cp appsettings.json appsettings.Development.json
# appsettings.Development.json içinde:
#   - ConnectionStrings:DefaultConnection (LocalDB)
#   - Jwt:Key (en az 32 karakter)
#   - FaceEncryption:Key (Base64, 32-byte)

# Veritabanını oluştur ve migrate et (otomatik startup'ta yapılır)
dotnet run

# Tarayıcı: https://localhost:5001
```

### Varsayılan Kullanıcılar (seed)

| E-posta | Şifre | Rol |
|---------|-------|-----|
| `admin@shifttrack.com` | `Admin123!` | Yönetici |
| `mehmet@shifttrack.com` | `Mehmet123!` | Personel |

---

## 🧪 Testler

```bash
cd ShiftTrackingApp.Tests
dotnet test --verbosity minimal
```

**32 birim testi** — Auth (login, RT rotation, hashing, account lockout), User CRUD,
encryption (round-trip, IV randomness, tamper detection), FaceData (upsert, encryption).

---

## 📂 Mimari

Detaylı mimari için → [ARCHITECTURE.md](./ARCHITECTURE.md)
Güvenlik prensipleri için → [SECURITY.md](./SECURITY.md)

```
ShiftTrackingApp/
├── Controllers/Controllers.cs       # 7 thin controllers (delegasyon)
├── Services/                        # İş mantığı (interfaces in IServices.cs)
├── Models/                          # User + Entities (RefreshToken, Shift, Leave, etc.)
├── DTOs/Dtos.cs                     # Validation attributes
├── Data/AppDbContext.cs             # EF Core + seed
├── Helpers/                         # JWT, AES, password policy, lockout, CSV
├── Constants/AppConstants.cs        # Magic-string yerine sabitler
├── Middleware/
│   ├── CorrelationIdMiddleware.cs
│   ├── SecurityHeadersMiddleware.cs
│   └── GlobalExceptionMiddleware.cs (ProblemDetails)
├── Program.cs                       # DI, middleware pipeline
└── wwwroot/                         # SPA (index.html + app.js + app.css)
```

---

## 🚢 Deployment

GitHub Actions otomatik deploy: `main` push → build → test → publish → Azure App Service.

**Pipeline:** [.github/workflows/azure-deploy.yml](./.github/workflows/azure-deploy.yml)

**Production URL:** https://www.shiftexapp.com
**Health:** https://www.shiftexapp.com/health

---

## 🤝 Katkı

```bash
# Branch aç, değişikliği yap
git checkout -b feat/yeni-ozellik

# Test + build (her ikisi de yeşil olmalı)
dotnet build ShiftTrackingApp -c Release
dotnet test ShiftTrackingApp.Tests -c Release

# Commit (Conventional Commits)
git commit -m "feat(scope): kısa açıklama"
git push -u origin feat/yeni-ozellik
```

Conventional commit prefixleri: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`, `perf:`.

---

## 📝 Lisans

Proprietary — © 2026 Shiftex. Tüm hakları saklıdır.
