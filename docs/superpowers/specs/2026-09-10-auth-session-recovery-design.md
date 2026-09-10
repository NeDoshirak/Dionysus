# Auth Session and Password Recovery Design

## Goal

Complete the existing email/password authentication flow with refresh-token rotation, logout, authenticated identity lookup, and email-code password recovery.

## Scope

- Add `POST /api/auth/refresh`, `POST /api/auth/logout`, `GET /api/auth/me`, `POST /api/auth/password-reset/request`, and `POST /api/auth/password-reset/confirm`.
- Keep users and password hashes in ASP.NET Core Identity/PostgreSQL.
- Keep refresh sessions and one-time codes in Valkey through `IDistributedCache`.
- Do not add roles, external providers, device management, or frontend UI.

## Session Design

`TokenService` issues a 15-minute access JWT and a 14-day refresh JWT. Each refresh JWT has a unique `jti`; Valkey stores `auth:session:{jti}` with the user id and matching 14-day TTL.

`POST /api/auth/refresh` reads the refresh cookie, validates JWT issuer, audience, signature, expiry, and token type, verifies its `jti` in Valkey, removes the old session, and issues a replacement pair. Reuse of a consumed or revoked refresh token fails.

`POST /api/auth/logout` removes only the session named by the refresh cookie and expires that cookie. Password reset removes every `auth:session:*` belonging to the user, so all devices are signed out.

The refresh cookie is `HttpOnly`, `SameSite=Strict`, scoped to `/api/auth/refresh`, and lasts 14 days. It is `Secure` when the runtime environment is Production; it is non-secure in local development so Swagger over `http://localhost` can exercise refresh.

## Password Recovery

The request endpoint always responds successfully. For a confirmed user it produces a six-digit `reset-password` code and sends it by SMTP. Codes are hashed with `AUTH_CODE_PEPPER`, expire after 10 minutes, accept at most five verification attempts, and can be resent once per minute.

The confirm endpoint validates the code, resets the password using `UserManager`, revokes every refresh session for that user, and returns success without issuing a session.

## Error Handling

Controllers return `ProblemDetails` with an appropriate HTTP status. Invalid, expired, reused, or missing refresh tokens return `401`. Invalid reset codes return `400`; invalid new passwords return `400` with Identity validation details. A missing SMTP configuration returns a descriptive service-unavailable response rather than leaking an unhandled exception.

## Verification

Integration tests cover refresh rotation, rejected refresh reuse, logout, password-reset privacy, reset confirmation revoking sessions, `/api/auth/me`, and refresh-cookie flags in development and production. Backend tests and build must pass, followed by Docker build/run verification using the no-FFmpeg local image.
