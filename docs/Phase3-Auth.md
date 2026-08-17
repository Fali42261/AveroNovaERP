# AveroNova Auth Phase 3 — Login / JWT / Refresh / Offline Contract

## APIs
- `POST /api/auth/register` (Phase 2, required for login verification)
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`
- `GET  /api/auth/me`

## Secure Storage (MAUI — Phase 4)
Store only in platform Secure Storage (never SQLite):
- `AccessToken`
- `RefreshToken`
- (optional) `AccessTokenExpiresAtUtc`

Keys defined in `OfflineSessionDefaults`.

## SQLite Local Session (MAUI — Phase 4)
Initialize from `LoginResponse` (no tokens):
- `LocalSession` ← `Session` (+ `OfflineSessionExpiresAtUtc`)
- `LocalUser` ← `User`
- `LocalCompany` / `LocalUserCompany` ← `CurrentCompany` + `Companies`
- `LocalRoles` ← `Roles`
- `LocalPermissions` ← `Permissions`

## Multi-company
`CompanyId` on login is optional. When provided, membership is validated against `UserCompany`. Unauthorized company → 403. Default company used when omitted.

## JWT
Short-lived access token (`Jwt:AccessTokenMinutes`, default 15). Claims: `sub`/user id, `company_id`, `session_id`, `jti`, roles. Signing key from config/env — never hardcoded in source for production.

## Refresh
Hashed refresh tokens on `DeviceSession`. Rotation on refresh. Reuse of revoked token revokes the token family.

## Offline
Do not extend JWT lifetime for offline. Local offline session max age = `OfflineSessionDefaults.OfflineSessionMaxAge` (14 days). Expired offline session requires online login.
