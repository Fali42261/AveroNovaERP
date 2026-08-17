# AveroNova Phase 1 — Offline-First Foundation

## Databases (separate)

- **Server Development (API only)**: SQLite `AveroNova.API/Data/AveroNovaDev.db` via `AppDbContext`
- **MAUI Local**: `LocalAppDbContext` at `FileSystem.AppDataDirectory/AveroNovaLocal.db` for LocalSession + SyncQueue + local auth context
- Tokens use `SecureStorage`, not SQLite
- MAUI never opens `AveroNovaDev.db`; API never opens `AveroNovaLocal.db`

## Production (later)

- Azure App Service + Azure SQL (not configured in Development)

## Notes

- Development does **not** require SQL Server / LocalDB
