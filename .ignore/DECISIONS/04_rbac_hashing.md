<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# D04 — RBAC: Argon2id Hashing (Revised per Opus Pass 1)

**Date:** 2026-04-18
**Updated:** 2026-04-18
**Decision:** Use `Konscious.Security.Cryptography.Argon2` NuGet package for password hashing. OWASP 2024 parameters: `memorySize=19456KB`, `iterations=2`, `parallelism=1`, `saltLength=16`, `hashLength=32`. NOT `Microsoft.AspNetCore.Cryptography.KeyDerivation.Pbkdf2`.

**Rationale:** Argon2id is memory-hard, GPU-resistant, OWASP-recommended for 2024. PBKDF2 is deprecated for new designs; too fast on GPUs.

**Implementation:**
```csharp
var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
{
    Salt = RandomNumberGenerator.GetBytes(16),
    MemorySize = 19456,  // KB ≈ 19 MB
    Iterations = 2,
    Parallelism = 1
};
byte[] hash = argon2.GetBytes(32);  // 256-bit subkey
// Store: base64(salt), base64(hash), params JSON
```

**SQLite schema (Data/Rbac/rbac.db):**
```sql
CREATE TABLE Users (
    UserId INTEGER PRIMARY KEY,
    Username TEXT UNIQUE NOT NULL,
    PasswordHash TEXT NOT NULL,        -- Argon2id base64 hash
    PasswordSalt TEXT NOT NULL,        -- base64 salt
    Argon2Params TEXT NOT NULL,        -- JSON { "memorySize":19456,"iterations":2,"parallelism":1,"saltLength":16,"hashLength":32 }
    RoleId INTEGER REFERENCES Roles(RoleId),
    Active INTEGER DEFAULT 1,
    LastLogin DATETIME,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

**Migration path:** If any existing `Users.PasswordHash` was created with `CredentialProtector`, set `Argon2Params` to `"legacy": true` and force password reset on next login (admin-only override page for recovery).

**RBAC Roles (v1.0):** Admin, DBA, ReadOnly, Auditor, Operator — sufficient for SOC2 Access Control + audit separation.

**Gaps filled per Opus:**
- Admin bootstrap: Onboarding Step 1 enforces admin creation with password strength ≥12 chars + zxcvbn ≥3 (Zxcvbn.Core NuGet)
- RoleGuard: `Components/Shared/RoleGuard.razor` component wrapping protected pages (`[Parameter] RequiredRole string`)
- Session timeout: 8h idle for Admin/DBA, 4h for Auditor (shorter = less forensics exposure)
- Password reset: local admin-only "Reset User Password" page (no email infra v1.0)

**Sources:** COMMENT D04 originally; Opus §A.4 (Konscious package + OWASP params + bootstrap + guards + session + reset); WORKFILE Task 25 updated

