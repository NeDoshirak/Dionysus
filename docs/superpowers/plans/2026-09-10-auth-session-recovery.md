# Auth Session Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement refresh-token rotation, logout, current-user lookup, and code-based password recovery for the ASP.NET Core API.

**Architecture:** `TokenService` owns creation, validation, rotation, and revocation of JWT-backed refresh sessions held in `IDistributedCache`. `CodeService` owns throttled, hashed, attempt-limited email codes. `AuthController` remains a thin HTTP adapter over these services and ASP.NET Identity.

**Tech Stack:** ASP.NET Core 8, ASP.NET Core Identity, JWT bearer authentication, `IDistributedCache` backed by Valkey, xUnit, `WebApplicationFactory`.

**Spec:** `docs/superpowers/specs/2026-09-10-auth-session-recovery-design.md`

## Global Constraints

- Access JWT lifetime is 15 minutes; refresh JWT and its Valkey session TTL are 14 days.
- Refresh cookies are `HttpOnly`, `SameSite=Strict`, and scoped to `/api/auth/refresh`.
- Refresh cookies are `Secure` in Production and non-secure only in Development.
- Password-recovery codes are six digits, SHA-256 hashed with `AUTH_CODE_PEPPER`, expire in 10 minutes, allow five attempts, and resend no faster than once per minute.
- Do not add secrets to source control; SMTP configuration comes only from environment variables.

---

### Task 1: Harden one-time-code storage

**Files:**
- Modify: `backend/Dionysus.Api/Infrastructure/Services/AuthServices.cs`
- Modify: `backend/Dionysus.Api.Tests/AuthApiTests.cs`

**Interfaces:**
- Produces: `CodeService.VerifyAsync(string purpose, string email, string code)` returning `false` after five bad attempts and deleting a consumed code.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Reset_code_is_rejected_after_five_invalid_attempts()
{
    var codes = CreateCodeService();
    var code = await codes.CreateAsync("reset-password", "user@example.com");
    for (var i = 0; i < 5; i++)
        Assert.False(await codes.VerifyAsync("reset-password", "user@example.com", "000000"));
    Assert.False(await codes.VerifyAsync("reset-password", "user@example.com", code!));
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter "FullyQualifiedName~Reset_code_is_rejected_after_five_invalid_attempts" --no-restore`

Expected: the real generated code is still accepted after five invalid attempts.

- [ ] **Step 3: Implement the minimal cache-state change**

Increment `auth:attempts:{purpose}:{email}` for every mismatch, preserve the ten-minute TTL, and remove code plus attempts after the fifth mismatch or a successful verification.

- [ ] **Step 4: Verify and commit**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter "FullyQualifiedName~AuthApiTests" --no-restore`

```powershell
git add backend/Dionysus.Api/Infrastructure/Services/AuthServices.cs backend/Dionysus.Api.Tests/AuthApiTests.cs
git commit -m "feat: limit authentication code attempts"
```

### Task 2: Add refresh-session lifecycle

**Files:**
- Modify: `backend/Dionysus.Api/Infrastructure/Services/AuthServices.cs`
- Modify: `backend/Dionysus.Api.Tests/AuthApiTests.cs`

**Interfaces:**
- Produces: `Task<TokenResponse?> RefreshAsync(string? refreshToken, HttpResponse response)`.
- Produces: `Task RevokeAsync(string? refreshToken)` and `Task RevokeAllAsync(string userId)`.

- [ ] **Step 1: Write failing refresh and logout tests**

```csharp
[Fact]
public async Task Refresh_rotates_cookie_and_rejects_the_old_token()
{
    var client = CreateConfirmedAuthenticatedClient(out _);
    Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
}
```

- [ ] **Step 2: Run the test and verify it fails with a missing route**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter "FullyQualifiedName~Refresh_" --no-restore`

- [ ] **Step 3: Implement rotation and revocation**

Validate signature, issuer, audience, expiry, `typ=refresh`, and `jti`. Check `auth:session:{jti}` maps to the token user id, remove it before issuing the replacement, and keep a per-user JSON list at `auth:user-sessions:{userId}` for global revocation.

- [ ] **Step 4: Implement environment-aware cookies**

Inject `IHostEnvironment` into `TokenService`; use `Secure = environment.IsProduction()`, while retaining `HttpOnly`, strict same-site policy, refresh-only path, and 14-day lifetime.

- [ ] **Step 5: Verify and commit**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter "FullyQualifiedName~Refresh_|FullyQualifiedName~Logout_" --no-restore`

```powershell
git add backend/Dionysus.Api/Infrastructure/Services/AuthServices.cs backend/Dionysus.Api.Tests/AuthApiTests.cs
git commit -m "feat: add refresh session lifecycle"
```

### Task 3: Expose password recovery and identity routes

**Files:**
- Modify: `backend/Dionysus.Api/Api/Controllers/AuthController.cs`
- Modify: `backend/Dionysus.Api/Application/Contracts.cs`
- Modify: `backend/Dionysus.Api/Infrastructure/Services/AuthServices.cs`
- Modify: `backend/Dionysus.Api.Tests/AuthApiTests.cs`

**Interfaces:**
- Consumes: `TokenService.RefreshAsync`, `RevokeAsync`, `RevokeAllAsync`, and `CodeService`.
- Produces: `/api/auth/refresh`, `/api/auth/logout`, `/api/auth/password-reset/request`, `/api/auth/password-reset/confirm`, and `/api/auth/me`.

- [ ] **Step 1: Write failing endpoint tests**

```csharp
[Fact]
public async Task Password_reset_request_does_not_disclose_unknown_email()
{
    var response = await factory.CreateClient().PostAsJsonAsync(
        "/api/auth/password-reset/request", new { email = "missing@example.com" });
    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
}
```

- [ ] **Step 2: Run endpoint tests and verify they fail with 404**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter "FullyQualifiedName~Password_reset|FullyQualifiedName~Me_returns" --no-restore`

- [ ] **Step 3: Implement the five controller actions**

Refresh returns `401` for an absent, invalid, or revoked cookie. Logout revokes its session, expires the cookie, and returns `204`. Reset request always returns `202`; reset confirm validates the email code, invokes `UserManager.ResetPasswordAsync`, revokes all sessions, and returns `204`. `Me` requires JWT and returns id, email, and confirmation state.

- [ ] **Step 4: Convert configuration and validation failures to HTTP responses**

Validate SMTP sender configuration before constructing `MailMessage`; return a non-secret `503 ProblemDetails` for missing SMTP settings. Map Identity password validation errors to `ValidationProblem`.

- [ ] **Step 5: Verify and commit**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter "FullyQualifiedName~AuthApiTests" --no-restore`

```powershell
git add backend/Dionysus.Api/Api/Controllers/AuthController.cs backend/Dionysus.Api/Application/Contracts.cs backend/Dionysus.Api/Infrastructure/Services/AuthServices.cs backend/Dionysus.Api.Tests/AuthApiTests.cs
git commit -m "feat: add password recovery and auth session endpoints"
```

### Task 4: Document and verify the API

**Files:**
- Modify: `README.md`
- Modify: `.env.example`
- Modify: `backend/Dionysus.Api.Tests/AuthApiTests.cs`

**Interfaces:**
- Consumes: final Task 3 routes.
- Produces: Swagger-visible routes, local environment instructions, and reproducible verification.

- [ ] **Step 1: Write a failing Swagger contract assertion**

```csharp
var document = await factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json");
Assert.Contains("/api/auth/password-reset/confirm", document);
Assert.Contains("/api/auth/refresh", document);
```

- [ ] **Step 2: Run it and verify route names appear only after Task 3**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter "FullyQualifiedName~Swagger" --no-restore`

- [ ] **Step 3: Document environment variables and request examples**

Document `JWT_KEY`, `AUTH_CODE_PEPPER`, SMTP, Valkey, refresh cookie behavior for Development/Production, and examples for reset, refresh, logout, and me.

- [ ] **Step 4: Run final verification**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --no-restore`

Run: `dotnet build backend/Dionysus.Api/Dionysus.Api.csproj --no-restore -c Release`

Run: `docker compose build --build-arg INSTALL_FFMPEG=false backend`

Start `dionysus-backend-no-ffmpeg` with `--env-file .env`; verify `/health`, `/swagger/v1/swagger.json`, and a Development refresh cookie.

- [ ] **Step 5: Commit docs and final tests**

```powershell
git add README.md .env.example backend/Dionysus.Api.Tests/AuthApiTests.cs
git commit -m "docs: document auth session recovery"
```

