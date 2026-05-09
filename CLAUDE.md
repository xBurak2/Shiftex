# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project Overview

**Shiftex** is a full-stack personnel shift and attendance tracking system with facial recognition support.
Built with ASP.NET Core 8 (backend) and Vanilla JS (frontend, served as SPA static files).

- Main app: `ShiftTrackingApp/`
- Tests: `ShiftTrackingApp.Tests/`
- Solution file: `ShiftTrackingApp.sln`

---

## Commands

Run from `ShiftTrackingApp/` unless noted otherwise.

```bash
# Run the application
dotnet run

# Apply EF Core migrations
dotnet ef database update

# Create a new migration
dotnet ef migrations add <MigrationName>

# Run all tests (from ShiftTrackingApp.Tests/)
dotnet test --verbosity normal

# Run a specific test class
dotnet test --filter "FullyQualifiedName~AuthServiceTests"
```

---

## Architecture

### Directory Map

```
ShiftTrackingApp/
├── Controllers/Controllers.cs      # All controllers in one file (7 controllers)
├── Services/
│   ├── Interfaces/IServices.cs     # All service interfaces
│   ├── AuthService.cs
│   ├── UserService.cs
│   ├── ShiftService.cs
│   ├── DepartmentService.cs
│   ├── LeaveAndAttendanceService.cs
│   └── FaceDataService.cs
├── Models/
│   ├── User.cs                     # User entity
│   └── Entities.cs                 # All other entities
├── DTOs/Dtos.cs                    # All request/response DTOs with validation
├── Data/AppDbContext.cs            # EF Core DbContext, seed data, relationships
├── Helpers/
│   ├── EncryptionHelper.cs         # AES-256-CBC (face data)
│   ├── JwtHelper.cs                # JWT token generation
│   └── TimeZoneHelper.cs
├── Middleware/GlobalExceptionMiddleware.cs
├── Program.cs                      # DI registration, middleware pipeline, startup
├── wwwroot/
│   ├── index.html                  # SPA shell
│   ├── css/app.css
│   └── js/app.js                   # ~957 lines, all frontend logic
└── Migrations/                     # Auto-generated EF migrations
```

### Backend

**Controllers** (`Controllers/Controllers.cs`) are thin wrappers — all logic lives in services. 7 controllers:
- `AuthController` — `/api/Auth/login`, `/refresh`, `/revoke`
- `UsersController` — User CRUD, pagination, monthly attendance summary
- `DepartmentsController` — Department CRUD
- `ShiftsController` — Weekly schedule, shift assignment CRUD
- `LeavesController` — Leave requests with approval workflow
- `AttendanceController` — Check-in/out (manual & face), dashboard stats
- `FaceDataController` — Face descriptor enrollment/deletion (admin-only)

**Services** implement interfaces from `IServices.cs`. All registered as **Scoped** in DI.

**DTOs** (`DTOs/Dtos.cs`) — all 14+ DTOs co-located in one file with `[Required]`, `[EmailAddress]`, `[MaxLength]`, `[Range]` validation attributes.

### Authentication Flow

1. `POST /api/Auth/login` → BCrypt verify → JWT (60 min) + RefreshToken (30 days)
2. `POST /api/Auth/refresh` → validates & rotates refresh token (old revoked, new issued)
3. Max **5 active refresh tokens per user** — oldest revoked automatically when exceeded
4. JWT validated via `[Authorize]` on controller actions

### Face Data Encryption

- Face descriptors = 128-element `float[]` arrays
- Stored encrypted: JSON → AES-256-CBC → Base64
- Key source: `appsettings` → `FaceEncryption:Key` (Base64-encoded 32 bytes)
- IV: randomly generated per encryption, prepended to ciphertext
- Format: `[16-byte IV | ciphertext]` → Base64 in DB
- One `FaceData` row per user (upsert in `FaceDataService.SaveAsync`)

### Database

EF Core 8 + SQL Server. Key constraints:

| Table | Constraint |
|---|---|
| `ShiftAssignments` | Unique index on `(UserId, Date, ShiftId)` |
| `FaceData` | Unique index on `UserId` |
| `RefreshTokens` | Revocation tracked via `RevokedAt` timestamp |
| `Users` | Soft-deleted via `IsActive = false` |

Seed data (in `AppDbContext.cs`):
- 5 departments
- 9 shift types (Morning, Afternoon, Night, Holiday, Leave, Part-Time, 3× Overtime)
- Default users:

| Email | Password | Role |
|---|---|---|
| admin@shifttrack.com | Admin123! | Yönetici (Admin) |
| mehmet@shifttrack.com | Mehmet123! | Personel (Employee) |

### Rate Limiting (built-in ASP.NET Core)

- Login endpoint: **10 req/min**
- All other API routes: **300 req/min**

### Logging

Serilog with daily rolling files, 14-day retention, thread ID enrichment. Configured in `Program.cs`.

### Frontend

Pure Vanilla JS SPA — **no build step**. Static files served directly by ASP.NET Core.

- `app.js` manages routing, API calls, modals, and face-api.js integration
- Face detection runs **client-side** via **face-api.js v0.22.2**
- Camera access requires **HTTPS**
- Global state: `currentUser`, `authToken`, `refreshToken`, `enrolledFaces[]`, `allUsers[]`
- Three camera streams: attendance check-in, co-registration, enrollment
- UI: dark theme, Plus Jakarta Sans + JetBrains Mono fonts

---

## Configuration

`appsettings.json` = production template. `appsettings.Development.json` is **git-ignored**, create locally with:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "<LocalDB connection string>"
  },
  "Jwt": {
    "Key": "<min 32 chars>",
    "Issuer": "<issuer>",
    "Audience": "<audience>",
    "ExpiresInMinutes": 60,
    "RefreshExpiresInDays": 30
  },
  "FaceEncryption": {
    "Key": "<Base64-encoded 32-byte value>"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173"]
  }
}
```

CORS dev allows: `localhost` + `192.168.1.101`. Production origins come from config.

---

## Testing

Framework: **xUnit + Moq + EF Core InMemory**

Test files in `ShiftTrackingApp.Tests/`:

| File | Coverage |
|---|---|
| `AuthServiceTests.cs` | Login, refresh token rotation, rate limiting (6 tests) |
| `UserServiceTests.cs` | CRUD, pagination, soft delete, cascading (8 tests) |
| `FaceDataServiceTests.cs` | Encryption round-trip, upsert, active-user filtering (6 tests) |
| `EncryptionHelperTests.cs` | AES round-trip, IV randomness, tamper detection (5 tests) |
| `TestDbFactory.cs` | Shared in-memory EF context setup |

---

## Key Patterns to Watch

### Soft Delete Cascade
Deleting a user sets `IsActive = false`. This cascades: shift assignments and leave requests for that user should be handled in `UserService.DeleteAsync`. Do not physically delete user rows.

### Refresh Token Rotation
Always revoke the old token before issuing a new one. `RefreshAsync` in `AuthService` does this atomically — do not break the rotation logic when modifying auth flows.

### AES Encryption IV
`EncryptionHelper.Encrypt()` generates a fresh random IV each call. The IV is prepended to the ciphertext. `Decrypt()` extracts the first 16 bytes as the IV. Never reuse IVs or change this format without migrating existing `FaceData` rows.

### Single-File Consolidation Pattern
Controllers, DTOs, and service interfaces are intentionally consolidated into single files (`Controllers.cs`, `Dtos.cs`, `IServices.cs`). Follow this pattern — do not split them.

### No-Build Frontend
There is no npm, webpack, or bundler. Any frontend changes go directly into `wwwroot/js/app.js` or `wwwroot/css/app.css`. The file is large (~957 lines) and handles all routing client-side.

### Startup Migration
`Program.cs` runs `context.Database.MigrateAsync()` on startup and applies a manual fix for the `ShiftAssignments` unique index. Be aware that startup modifies the schema — local DB must be reachable at startup time.

### Photo Size Limit
`UpdateUserDto.PhotoBase64` has a `[MaxLength(500000)]` attribute (~375 KB before Base64 overhead). Enforce this constraint at the client side too to avoid large payloads.
