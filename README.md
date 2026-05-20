# Shiftex — Talep-Güdümlü Personel Vardiya ve Devam Takip Sistemi

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core 8](https://img.shields.io/badge/EF%20Core-8.0-blueviolet)](https://learn.microsoft.com/ef/core/)
[![Azure](https://img.shields.io/badge/Azure-App%20Service-0078D4?logo=microsoftazure)](https://azure.microsoft.com/)
[![Tests](https://img.shields.io/badge/tests-35%20passing-brightgreen)](#-testler)

**Shiftex**, üretim/konfeksiyon işletmeleri için tasarlanmış **talep-güdümlü** bir personel
vardiya yönetimi ve yüz tanıma ile devam takip sistemidir. Klasik "eldeki personele iş bul"
yaklaşımının tersine, **önce işin ihtiyacını tanımlar**, sonra bu ihtiyacı planlama → gerçek
katılım → eksik kapatma zinciriyle yönetir.

ASP.NET Core 8 Web API + Azure SQL + Vanilla JS SPA mimarisi üzerine kuruludur ve
[`https://www.shiftexapp.com`](https://www.shiftexapp.com) adresinde production'da çalışır.

---

## 🎯 Çekirdek Fikir: Yukarıdan Aşağı (Top-Down) Vardiya Yönetimi

Çoğu vardiya uygulaması personeli vardiyalara dağıtmakla yetinir; "bu vardiyada kaç kişi
olmalıydı, kaç kişi geldi, açık ne kadar?" sorusunu cevaplayamaz. Shiftex bu zinciri kurar:

```
1) İHTİYAÇ        Yönetici her departman × vardiya için gereken kişi sayısını tanımlar
      │           (Personel İhtiyaç Matrisi)
      ▼
2) PLAN           Kadrolu personel haftalık roster'a yerleştirilir
      │
      ▼
3) GERÇEKLEŞEN    Personel giriş (check-in) yapar → "gelen" sayısı ölçülür
      │           Vardiya Kapasitesi: Gereken / Atanan / Gelen / EKSİK
      ▼
4) AKSİYON        Eksik tespit edilince yönetici "Yevmiyeci Çağrısı" başlatır →
                  departmana uygun yevmiyeci kabul edince vardiyaya OTOMATİK atanır
```

Bu sayede sistem şu soruların hepsini net cevaplar:
*Bu vardiyada kaç kişi gerekli? · Kaç kişi atandı? · Kaç kişi geldi? · Eksik ne kadar? ·
Açığı kim kapatacak?*

---

## 👥 İki Personel Tipi

| | **Kadrolu (Permanent)** | **Yevmiyeci (Casual)** |
|---|---|---|
| Çalışma | Sabit haftalık roster | İhtiyaç oldukça çağrılır |
| Atama | Vardiya planlamada elle | Yalnızca **çağrı kabulü** ile (otomatik) |
| Departman | Bir departmana bağlı | Bir departman uzmanlığına bağlı (örn. *Dikiş yevmiyecisi*) |
| Uygulama menüsü | Tam panel | Kısıtlı: Vardiyalarım · Devam Durumum · Gelen Çağrılar |

Yevmiyeci çağrı algoritması yalnızca **uygun** kişileri listeler: doğru departman + o gün
boşta + aktif çağrısı yok.

---

## ✨ Özellikler

### 📊 Talep-Güdümlü Planlama (çekirdek)
- **Personel İhtiyaç Matrisi** — departman × vardiya × gün → gereken kişi sayısı (haftalık şablon, hafta sonu otomatik düşer)
- **Vardiya Kapasitesi Dashboard** — seçilen gün için Gereken / Atanan / Gelen / Eksik; renk kodlu durum (Tam · Gelmeyen var · Eksik atama)
- **Yevmiyeci Çağrısı** — eksik açığında tek tıkla uygun yevmiyeci listesi → çağrı → kabul/red → kabulde otomatik vardiya ataması
- **Demo Simülasyonu** — tek tıkla gerçekçi aylık veri üretimi (roster, devam, çağrılar) — idempotent, sunum için hazır senaryo

### 🔐 Güvenlik
- JWT (60 dk) + Refresh Token (30 gün) — SHA-256 hash'li DB saklama, rotasyonlu, max 5 aktif/user
- BCrypt şifre hash'leme + **account lockout** (5 başarısız → 15 dk kilit)
- **Strong password policy** (min 8, büyük + küçük + rakam)
- OWASP **security headers** (CSP, HSTS, X-Frame-Options, Permissions-Policy)
- **Rate limiting** (login 10/dk, API 300/dk)
- **AES-256-CBC** ile yüz tanıma verisi şifreleme (rastgele IV, ciphertext'e prepend)
- JWT'de `emp_type` claim'i ile yevmiyeci/kadrolu yetki ayrımı

### 🎯 İş Mantığı
- Rol + istihdam tipi bazlı erişim (Admin / Employee · Permanent / Casual)
- Soft-delete + bağımlı kayıtların güvenli temizlenmesi (FK SetNull / cascade)
- 3 vardiya: **Sabah 08:00–16:00 · Öğle 16:00–00:00 · Gece 00:00–08:00** (+ Part-Time, Tatil/İzin, Fazla Mesai)
- İzin talepleri (belge ekli) + onay akışı, vardiya değişim (swap) sistemi
- Yüz tanıma client-side (face-api.js v0.22.2) → şifreli descriptor backend'de
- Aylık devam özeti + **CSV export** (Excel uyumlu, UTF-8 BOM)
- Çok dilli arayüz (TR / EN)

### 🏗️ Mimari Kalite
- RFC 7807 **ProblemDetails** standart hata cevapları + **Correlation ID** middleware
- **Output caching** + **Response compression** (Brotli/Gzip)
- Yapılandırılmış **Serilog** (correlation ID, machine name, environment)
- Startup'ta otomatik migration + idempotent varsayılan veri seed'i

---

## 🏭 Demo Senaryosu: "ShiftEx Konfeksiyon"

Orta ölçekli, 7/24 çalışan bir tekstil atölyesi:

- **5 departman:** ✂️ Kesim · 🧵 Dikiş · 📦 Ütü & Paketleme · 🔍 Kalite Kontrol · 🚚 Sevkiyat
- **22 kadrolu personel** + **8 yevmiyeci** (departman uzmanlıklarına dağıtılmış) + yönetici
- **Mayıs simülasyonu:** ~260 roster ataması, ~250 check-in, gerçekçi devamsızlık (~%4) ve tetiklenmiş yevmiyeci çağrıları

> Yönetici panelinde **Vardiya Kapasitesi → "Simülasyon Yükle"** ile bu veri tek tıkla üretilir/yenilenir.

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
# appsettings.Development.json içinde doldur:
#   - ConnectionStrings:DefaultConnection (LocalDB)
#   - Jwt:Key (en az 32 karakter), Jwt:Issuer, Jwt:Audience
#   - FaceEncryption:Key (Base64, 32-byte)

# Veritabanı startup'ta otomatik migrate olur + varsayılan veri seed'lenir
dotnet run

# Tarayıcı: https://localhost:5001
```

### Varsayılan Kullanıcılar (seed)

| E-posta | Şifre | Tip |
|---------|-------|-----|
| `admin@shiftex.com` | `Admin123!` | Yönetici |
| `mehmet@shiftex.com` | `Mehmet123!` | Kadrolu Personel |
| `elif.simsek@shiftex.com` | `Personel123!` | Kadrolu Personel |
| `hasan.demir@shiftex.com` | `Yevmiye123!` | Yevmiyeci |

> Tüm kadrolu mailler `ad.soyad@shiftex.com` (örn. `ali.vural@shiftex.com`), tüm yevmiyeci
> şifreleri `Yevmiye123!`, tüm kadrolu şifreler `Personel123!` formatındadır.

---

## 🧪 Testler

```bash
cd ShiftTrackingApp.Tests
dotnet test --verbosity minimal
```

**35 birim testi** — Auth (login, RT rotation, hashing, account lockout), User CRUD,
şifreleme (round-trip, IV randomness, tamper detection), FaceData (upsert, encryption).

---

## 📂 Mimari

Detaylı mimari için → [ARCHITECTURE.md](./ARCHITECTURE.md) · Güvenlik için → [SECURITY.md](./SECURITY.md)

```
ShiftTrackingApp/
├── Controllers/Controllers.cs       # Thin controllers (delegasyon)
│     Auth · Users · Departments · StaffingRequirements · Coverage
│     CasualCallouts · Simulation · Shifts · Leaves · Attendance · FaceData
├── Services/                        # İş mantığı (interfaces → IServices.cs)
│     StaffingRequirementService · CoverageService · CasualCalloutService
│     SimulationService · Auth/User/Shift/Leave/Attendance/FaceData servisleri
├── Models/                          # User + Entities
│     StaffingRequirement · CasualCallout · ShiftAssignment · AttendanceLog ...
├── DTOs/Dtos.cs                     # Validation attributes
├── Data/AppDbContext.cs             # EF Core + seed (departmanlar, kadro, yevmiyeci)
├── Helpers/                         # JWT, AES, TimeZone, password policy, CSV
├── Middleware/                      # CorrelationId · SecurityHeaders · GlobalException
├── Program.cs                       # DI, pipeline, startup migration + seed
└── wwwroot/                         # SPA (index.html + js/app.js + js/i18n.js + css/app.css)
```

### Talep-güdümlü akışın API uç noktaları
| Uç nokta | Açıklama |
|---|---|
| `GET/PUT /api/StaffingRequirements` | İhtiyaç matrisini oku / departman bazında topluca güncelle |
| `GET /api/Coverage?date=` | Bir gün için Gereken/Atanan/Gelen/Eksik özeti |
| `GET /api/CasualCallouts/eligible` | Çağrılabilecek uygun yevmiyeciler |
| `POST /api/CasualCallouts` · `POST /api/CasualCallouts/{id}/respond` | Çağrı gönder · kabul/red |
| `POST /api/Simulation/generate` | Demo verisi üret (admin) |

---

## 🚢 Deployment

GitHub Actions otomatik deploy: `main` push → build → test → publish → Azure App Service.

**Pipeline:** [.github/workflows/azure-deploy.yml](./.github/workflows/azure-deploy.yml)
**Production:** https://www.shiftexapp.com

---

## 🤝 Katkı

```bash
git checkout -b feat/yeni-ozellik
dotnet build ShiftTrackingApp -c Release
dotnet test ShiftTrackingApp.Tests -c Release
git commit -m "feat(scope): kısa açıklama"   # Conventional Commits
git push -u origin feat/yeni-ozellik
```

Prefixler: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`, `perf:`.

---

## 📝 Lisans

Proprietary — © 2026 Shiftex. Tüm hakları saklıdır.
