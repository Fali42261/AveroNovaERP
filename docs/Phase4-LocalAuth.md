# AveroNova Phase 4 — MAUI Local Auth Session

## Auth Client
- `AuthenticationService` — online/offline login, register, refresh, logout, auto-login
- `IApiClient` / `ApiClient` — shared HTTP + JSON envelope
- `IAuthApiClient` / `AuthApiClient` — `/api/auth/*`
- `ISecureTokenStore` / `MauiSecureTokenStore` — AccessToken, RefreshToken, Expiry, SessionId
- `ILocalAuthSessionStore` — SQLite auth context persistence
- `IAppSessionContext` — CurrentUser / CurrentCompany / Roles / Permissions

## SQLite (stored)
LocalSession, LocalUser, LocalCompany, LocalUserCompany, LocalRole, LocalPermission

## Intentionally NOT in SQLite
Password, PasswordHash, AccessToken, RefreshToken, JWT signing secrets

## Online
Login → SecureStorage + SQLite → Dashboard  
Refresh when access token near expiry  
Logout → API revoke + clear tokens + deactivate local session (business data kept)

## Offline
Startup / Login uses LocalSession policy (InstallationId match, not expired, user/company/permissions present).  
No password verification offline.  
Expired / missing session → require online login with clear message.

## Installation
NotRegistered → Welcome (Login + Create Account)  
Registered → Login only  

## Development API BaseUrl (central JSON only)
- Debug → `Resources/Raw/appsettings.Development.json` → `ApiSettings`
  - `WindowsBaseUrl`
  - `AndroidDeviceBaseUrl` (Windows LAN IP)
  - `AndroidEmulatorBaseUrl` (`10.0.2.2` host mapping)
  - `IosSimulatorBaseUrl`
- Release → `Resources/Raw/appsettings.Production.json` → `ApiSettings.BaseUrl`
- Loader: `ApiSettingsLoader` → resolved `ApiSettings.BaseUrl` → `ApiClient` / `HttpClient.BaseAddress`
- Auth/Sync use relative paths only (`api/auth/...`) via `IAuthApiClient` / `IApiClient`

API Development profile binds `0.0.0.0:7243` so emulator and LAN devices can reach the host.  
DEBUG MAUI trusts the ASP.NET Core development HTTPS certificate (self-signed / hostname mismatch on LAN IP).  
Open only TCP 7243 inbound on Windows Firewall for private networks when using a physical device.
## Databases
- Server Dev: `AveroNovaDev.db` (API SQLite only)
- MAUI Local: `AveroNovaLocal.db` under `FileSystem.AppDataDirectory`
