# Shiftex Mimari Dokümanı

## Yüksek Seviyeli Bakış

```
┌──────────────────────────────────────────────────────────────┐
│ İSTEMCİ (Vanilla JS SPA — face-api.js client-side)          │
└──────────────────────────────────────────────────────────────┘
                        ↓ HTTPS (JWT + Refresh Token)
┌──────────────────────────────────────────────────────────────┐
│ AZURE APP SERVICE (Linux B1, West Europe)                    │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ ASP.NET Core 8 Web API                                │   │
│  │                                                       │   │
│  │ Middleware Pipeline (sırasıyla):                      │   │
│  │   1. Swagger UI                                       │   │
│  │   2. CorrelationId        → X-Correlation-Id          │   │
│  │   3. SecurityHeaders      → CSP, HSTS, X-Frame, ...   │   │
│  │   4. GlobalException      → RFC 7807 ProblemDetails   │   │
│  │   5. HttpsRedirection                                 │   │
│  │   6. ResponseCompression  → Brotli + Gzip             │   │
│  │   7. CORS                                             │   │
│  │   8. RateLimiter          → login 10/dk, api 300/dk   │   │
│  │   9. SerilogRequestLogging                            │   │
│  │  10. StaticFiles          → /wwwroot                  │   │
│  │  11. Authentication       → JWT Bearer                │   │
│  │  12. Authorization        → Role-based                │   │
│  │  13. OutputCache          → /api/Departments (60s)    │   │
│  │  14. Controllers          → 7 thin controllers        │   │
│  └──────────────────────────────────────────────────────┘   │
│                        ↓                                     │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ Services (Scoped DI)                                 │   │
│  │ AuthService, UserService, ShiftService,              │   │
│  │ DepartmentService, LeaveService, AttendanceService,  │   │
│  │ FaceDataService, NotificationService                 │   │
│  └──────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────┘
                        ↓ Entity Framework Core 8
┌──────────────────────────────────────────────────────────────┐
│ AZURE SQL DATABASE (Basic 5 DTU, 2 GB)                       │
│  Users · Departments · Shifts · ShiftAssignments ·          │
│  LeaveRequests · AttendanceLogs · RefreshTokens · FaceData  │
└──────────────────────────────────────────────────────────────┘
```

---

## Domain Modeli

### Anahtar Entity'ler

| Entity | Sorumluluğu | Unique Constraint |
|--------|-------------|-------------------|
| `User` | Personel + admin | `Email` unique |
| `Department` | Şirket departmanı | `Name` unique |
| `Shift` | Vardiya türü (Sabah, Gece, FM, vs.) | — |
| `ShiftAssignment` | Personel × Tarih × Vardiya | `(UserId, Date, ShiftId)` unique |
| `LeaveRequest` | İzin talebi (state machine: Pending→Approved/Rejected) | — |
| `AttendanceLog` | Giriş/çıkış kaydı | — |
| `RefreshToken` | Hashlenmiş refresh token | `Token` unique |
| `FaceData` | AES-256 encrypted face descriptor | `UserId` unique |

### State Makineleri

#### Leave Request
```
Pending ──(admin onaylar)──→ Approved
   ├──(admin reddeder)────→ Rejected
   └──(personel siler)────→ deleted
```

#### Refresh Token
```
Active ──(yeni token kullanılırsa)──→ Revoked (rotation)
   ├──(login limit aşılırsa)────────→ Revoked (oldest first, max 5)
   ├──(süresi dolarsa)──────────────→ Expired
   └──(kullanıcı logout)────────────→ Revoked
```

---

## Anahtar Tasarım Kararları

### 1. Vanilla JS Frontend (No build step)
**Karar:** React/Vue yerine vanilla JS.
**Neden:** Ekip küçük, build pipeline yokluğu = düşük operasyon yükü. 1000 satır JS yönetilebilir.
**Trade-off:** Kompleks state olmadığı sürece çalışır; ileride React'a taşıma yapılabilir.

### 2. Face Recognition Client-Side
**Karar:** face-api.js tarayıcıda çalışır, backend sadece 128-elemanlı descriptor saklar.
**Neden:**
- Sunucu işlemcisinden tasarruf
- GDPR uyumlu (ham yüz görüntüsü asla sunucuya gitmez)
- Düşük gecikme

### 3. Face Descriptor Şifreleme (AES-256-CBC)
**Karar:** Descriptor'lar DB'de şifreli (`Helpers/EncryptionHelper.cs`).
**Neden:** DB sızıntısında biyometrik veri tek başına faydasız.
**Format:** `[16-byte rastgele IV | ciphertext]` → Base64
**Anahtar:** `FaceEncryption:Key` (Base64-encoded 32-byte) — Azure App Service Configuration'dan gelir.

### 4. Refresh Token Hashing (SHA-256)
**Karar:** DB'de plain text değil, hash saklanır.
**Neden:** DB dump'ında token'lar kullanılamaz. Tokenlar uzun (64 random byte, base64-url encoded).
**Migration:** Geçişli — eski plain-text token'lar da hala çalışır (legacy fallback).

### 5. Single-File Consolidation
**Karar:** `Controllers.cs`, `IServices.cs`, `Dtos.cs` tek dosyada.
**Neden:** Proje boyutu küçük (~3000 satır toplam); küçük projelerde navigation kolaylaşır.
**Trade-off:** Büyürse split edilmesi gerekir.

### 6. Soft Delete with Cascading Cleanup
**Karar:** `User.IsActive = false`; gelecekteki shift atamaları silinir, bekleyen izinler reddedilir.
**Neden:** Geçmiş verileri kaybetmemek (audit trail), aktif planlamayı temizlemek.

### 7. Account Lockout In-Memory
**Karar:** `AccountLockoutService` singleton + `ConcurrentDictionary`.
**Neden:** Tek instance için yeterli; production scale-out'ta Redis kullanılmalı.
**Limit:** 5 yanlış deneme = 15 dk kilit (10 dk pencerede).

---

## Veri Akışı: Login + Refresh

```
[İstemci]                  [Backend]                    [DB]
   │                          │                          │
   │ POST /api/Auth/login     │                          │
   ├─────────────────────────→│                          │
   │                          │ Lockout check            │
   │                          ├──── kilitliyse 401 ←─────│
   │                          │                          │
   │                          │ Email + Password         │
   │                          ├─────────────────────────→│
   │                          │  ←── User row + hash ────│
   │                          │                          │
   │                          │ BCrypt.Verify            │
   │                          │ → ✓ generate JWT         │
   │                          │ → 64 random bytes        │
   │                          │ → SHA-256 hash           │
   │                          │ → Store hash             │
   │                          ├─────────────────────────→│
   │ 200 { token, refresh }   │                          │
   │←─────────────────────────│                          │
   │                          │                          │
   │ Token expires (60 dk)    │                          │
   │ POST /api/Auth/refresh   │                          │
   │ { refreshToken: raw }    │                          │
   ├─────────────────────────→│                          │
   │                          │ Hash raw token           │
   │                          │ Find by hash             │
   │                          ├─────────────────────────→│
   │                          │ Check: not revoked       │
   │                          │ Revoke old, create new   │
   │                          ├─────────────────────────→│
   │ 200 { token, refresh }   │                          │
   │←─────────────────────────│                          │
```

---

## Performans Düşünceleri

| Endpoint | Strateji |
|----------|----------|
| `GET /api/Departments` | Output cache, 60 saniye, tag-based invalidation |
| Tüm JSON cevapları | Brotli/Gzip response compression (1ms ek + %70-80 boyut tasarrufu) |
| `GET /api/Users?page=N&pageSize=N` | Pagination — TotalCount, HasNext, HasPrev wrapper |
| `GET /api/Shifts/weekly` | Tek query, EF Include ile N+1'i önler |

---

## Observability

| Sinyal | Nerede |
|--------|--------|
| **Logs** | Serilog → Console + dosya (`Logs/shiftex-{date}.log`, 14 gün retention). Her log satırında `CorrelationId`, `UserId`, `MachineName`. |
| **Health** | `/health` (DB dahil), `/health/live` (sadece self) |
| **Tracing** | `X-Correlation-Id` header — istemci gönderirse kullanır, yoksa GUID üretir. Loglarda görünür, hata cevaplarında `traceId`. |
| **Slow requests** | `>2000ms` → Warning level otomatik |

---

## Bilinen Sınırlamalar

1. **Account lockout** sadece tek instance'da çalışır (horizontal scale'de N×5 deneme).
2. **Output cache** in-memory; multi-instance'da Redis backend gerekir.
3. **Logs/** klasörü Azure'da geçici (App Service restart silebilir); production'da Application Insights önerilir.
4. **Email/Push notification** abstraction var (`INotificationService`) ama implementasyon sadece logger; gerçek kanal eklemek için `LoggerNotificationService`'i değiştirin.
