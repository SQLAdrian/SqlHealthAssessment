<!-- In the name of God, the Merciful, the Compassionate -->
<!-- Bismillah ar-Rahman ar-Raheem -->

# D15 — RBAC: Admin Bootstrap + RoleGuard + Session Timeout

**Date:** 2026-04-18
**Decision:** First-run UX forces admin creation; page protection via `RoleGuard.razor` component; session timeout enforced server-side; local admin-only password reset (no email v1.0).

**Admin bootstrap (Onboarding Step 1):**
- On first launch, `RbacService.HasAnyUser()` returns false → route to `/onboarding?step=admin`
- Form: username (default: `admin`), password (min 12 chars), confirm, zxcvbn strength ≥3 (Zxcvbn.Core NuGet)
- Submit → `RbacService.CreateUser(role: "Admin", ...)` with Argon2id hash; Onboarding advances to Step 2 (server connection)
- **Block:** No other page accessible until admin exists (global `[Authorize]` gate bypassed only for `/onboarding`)

**RoleGuard component:**
- `Components/Shared/RoleGuard.razor` — wrapper for page content:
  ```razor
  [Parameter] public string RequiredRole { get; set; }
  [CascadingParameter] private Task<AuthenticationState> AuthTask { get; set; }
  protected override async Task OnInitializedAsync() {
      var auth = await AuthTask;
      if (!auth.User.IsInRole(RequiredRole)) Navigation.NavigateTo("/not-authorized");
  }
  @ruleContent
  ```
- Usage: wrap page body with `<RoleGuard RequiredRole="Admin">...</RoleGuard>`
- Auditor gets special `AuditorGuard` allowing only `/audit-log` page (audit-log-only role)

**Session timeout:**
- Implement sliding expiration via `AuthenticationStateProvider` + `ProtectedSessionStorage`
- Timeouts: Admin/DBA = 8 hours idle; Auditor = 4 hours (shorter window = reduced forensics exposure)
- On timeout: clear auth cookie/session, redirect to `/login` with "Session expired due to inactivity" message
- Inactivity tracked by last-seen timestamp updated on each navigation + heartbeat (every 30s)

**Password reset (local-only):**
- Page `/admin/reset-password` accessible only to Admin role
- Form: select user (dropdown of active users), enter new password (zxcvbn ≥3), confirm
- Call `RbacService.ChangePassword(userId, newPassword)` — rehashes with Argon2id
- No email verification, no external IdP; sufficient for v1.0 single-tenant desktop

**Gaps closed per Opus §A.4:** Bootstrap, RoleGuard, session timeout, local reset.

**Sources:** Opus §A.4 (bootstrap + guards + timeout + reset), COMMENT D04 extended; WORKFILE Task 25 expanded

