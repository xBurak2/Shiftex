# Shiftex — Personel Vardiya & Devam Takip Sistemi

ASP.NET Core 8, Entity Framework Core ve Vanilla JS ile geliştirilmiş tam kapsamlı vardiya yönetim uygulaması.

---

## 🚀 Hızlı Başlangıç

### Gereksinimler
- .NET 8 SDK
- SQL Server (LocalDB veya tam sürüm)
- Modern tarayıcı (kamera özelliği için HTTPS gerekir)

### Kurulum

```bash
# 1. Bağlantı dizesini ayarlayın (appsettings.Development.json — .gitignore'da)
# 2. Migration uygulayın
dotnet ef database update

# 3. Çalıştırın
dotnet run
```

### Varsayılan Giriş
| E-posta | Şifre | Rol |
|---------|-------|-----|
| admin@shifttrack.com | Admin123! | Yönetici |
| mehmet@shifttrack.com | Mehmet123! | Personel |

---

## 📁 Proje Yapısı

```
ShiftTrackingApp/
├── Controllers/       API endpoint'leri
├── Data/              EF Core DbContext
├── DTOs/              Request/Response modelleri
├── Helpers/           JWT, Şifreleme, Timezone yardımcıları
├── Middleware/        Global hata yakalayıcı
├── Migrations/        EF Core migration'ları
├── Models/            Entity sınıfları
├── Services/          İş mantığı katmanı
└── wwwroot/           Statik dosyalar (SPA frontend)

ShiftTrackingApp.Tests/
├── AuthServiceTests.cs
├── UserServiceTests.cs
├── FaceDataServiceTests.cs
└── EncryptionHelperTests.cs
```

---

## 🔐 Güvenlik

### Ortam Değişkenleri (production'da MUTLAKA ayarlayın)

| Değişken | Açıklama |
|----------|----------|
| `JWT__Key` | JWT imzalama anahtarı (min. 32 karakter) |
| `FACE_ENCRYPTION_KEY` | Yüz verisi AES-256 anahtarı (32-byte Base64) |

```bash
# AES anahtarı üretmek için:
openssl rand -base64 32
```

### Yüz Verisi Güvenliği
- Yüz tanıma vektörleri (128 float) **AES-256-CBC** ile şifreli saklanır
- Her şifreleme işleminde rastgele IV kullanılır
- Anahtar uygulama belleğine yüklenir, açık metin asla veritabanına yazılmaz
- Veri localStorage'da tutulmaz — tamamen backend'de yönetilir

### Diğer Güvenlik Önlemleri
- Login rate limiting: 1 dakikada max 10 istek
- Genel API rate limiting: 1 dakikada max 300 istek
- Refresh token rotasyonu (kullanıldıktan sonra iptal)
- Kullanıcı başına max 5 aktif refresh token
- Fotoğraf upload boyut limiti: 375 KB
- Soft delete — kullanıcı silindiğinde gelecek vardiyalar iptal, bekleyen izinler reddedilir

---

## 📋 API Endpoint'leri

### Auth
| Method | Path | Açıklama |
|--------|------|----------|
| POST | `/api/Auth/login` | Giriş |
| POST | `/api/Auth/refresh` | Token yenile |
| POST | `/api/Auth/revoke` | Token iptal |

### Kullanıcılar
| Method | Path | Rol |
|--------|------|-----|
| GET | `/api/Users?page=1&pageSize=50` | Admin |
| GET | `/api/Users/{id}` | Admin / Kendisi |
| GET | `/api/Users/me` | Tümü |
| POST | `/api/Users` | Admin |
| PUT | `/api/Users/{id}` | Admin / Kendisi |
| DELETE | `/api/Users/{id}` | Admin |

### Yüz Verisi
| Method | Path | Rol |
|--------|------|-----|
| GET | `/api/FaceData` | Admin |
| POST | `/api/FaceData` | Admin |
| DELETE | `/api/FaceData/{userId}` | Admin |

### Departmanlar
| Method | Path | Rol |
|--------|------|-----|
| GET | `/api/Departments` | Tümü |
| POST | `/api/Departments` | Admin |
| DELETE | `/api/Departments/{id}` | Admin |

### Sağlık Kontrolü
| Method | Path |
|--------|------|
| GET | `/health` |

---

## 🧪 Testleri Çalıştırma

```bash
cd ShiftTrackingApp.Tests
dotnet test --verbosity normal
```

**Test kapsamı:**
- `AuthServiceTests` — Giriş, refresh token, rate limiting (6 test)
- `UserServiceTests` — CRUD, sayfalama, soft delete, cascade (8 test)
- `FaceDataServiceTests` — Şifreleme, upsert, aktif kullanıcı filtresi (6 test)
- `EncryptionHelperTests` — AES round-trip, IV rastgeleliği, bozulma tespiti (5 test)

---

## ⚠️ Production Kontrol Listesi

- [ ] `appsettings.Development.json` repo'da YOK (`.gitignore`'da)
- [ ] `JWT__Key` environment variable ayarlandı (min 32 karakter)
- [ ] `FACE_ENCRYPTION_KEY` environment variable ayarlandı (32-byte Base64)
- [ ] `Cors:AllowedOrigins` production domain'e güncellendi
- [ ] HTTPS sertifikası yapılandırıldı
- [ ] `dotnet ef database update` çalıştırıldı

---

## 🗄️ Veritabanı Migrasyonu

```bash
# Yeni migration eklemek için:
dotnet ef migrations add MigrationAdi

# Uygulamak için:
dotnet ef database update

# Geri almak için:
dotnet ef database update OncekiMigrationAdi
```
