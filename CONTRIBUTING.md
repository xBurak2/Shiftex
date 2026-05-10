# Shiftex'e Katkı Rehberi

Önce ARCHITECTURE.md ve SECURITY.md dosyalarını oku — kararların arkasındaki rationale orada.

## Geliştirme Akışı

1. **Issue aç:** Sorunu/öneriyi tartış (3+ satırlık değişiklikler için)
2. **Branch:** `feat/...`, `fix/...`, `chore/...`, `docs/...`
3. **Lokalde test:** `dotnet build` ve `dotnet test` yeşil olmalı
4. **Commit:** Conventional Commits (`feat:`, `fix:`, `chore:`, vs.)
5. **PR aç:** Açıklamada **Ne değişti / Neden / Nasıl test edildi**

## Kod Standartları

- **.editorconfig** kuralları zorunlu — IDE otomatik uygular
- **Magic string yok** — `Constants/AppConstants.cs` kullan
- **`DateTime.UtcNow`** kullan — `DateTime.Now` timezone tuzağı
- **Async metodlar `Async` sonekli olmalı** — `LoginAsync`, `GetByIdAsync`
- **Service'lerde iş mantığı** — Controller'lar thin
- **DTO validation `[DataAnnotations]`** — model state otomatik doğrulanır
- **innerHTML'e kullanıcı verisi koyarken `esc()` kullan** (XSS)

## Test Yazma

- **Yeni endpoint** → minimum 1 happy-path + 1 error case
- **Bug fix** → regression test ekle
- **Auth değişikliği** → AuthServiceTests'e ekle

## Migration Ekleme

```bash
cd ShiftTrackingApp
dotnet ef migrations add YourMigrationName

# Production'a uygulamadan önce review et!
# Program.cs'deki Migrate() startup'ta otomatik çalışır.
```

## Commit Mesaj Örnekleri

```
feat(auth): account lockout after failed login attempts
fix(shifts): prevent duplicate assignment on same date
chore(deps): bump EF Core to 8.0.10
docs(readme): add Docker build instructions
refactor(controllers): extract magic strings to Roles constants
perf(dept): add output caching for GetAll
test(auth): cover refresh token rotation edge cases
```
