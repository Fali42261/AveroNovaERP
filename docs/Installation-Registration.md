# First Installation Registration Rule

## Local state (SQLite)

Table `LocalInstallations` (single row per install):

| Field | Meaning |
|-------|---------|
| `InstallationId` | Stable GUID created on first launch (offline-safe) |
| `DeviceId` | Separate stable device identifier |
| `Status` | `NotRegistered` / `Registered` |
| `UserId` / `CompanyId` | Set only after successful server registration |
| `RegisteredAtUtc` | Set only after successful registration |

`InstallationId ≠ DeviceId ≠ UserId ≠ CompanyId`

## Startup

```
Splash
  → Ensure local DB + Installation row
  → Valid offline session? → Main
  → Registered? → Login (Create Account hidden)
  → Else → Welcome (Login + Create Account)
```

## Registration

- Online required (no fake offline registration).
- Mark `Registered` only after successful register response.
- Failure / offline → remain `NotRegistered`, Create Account stays available.
- After success → navigate to Login (not auto-login as a new account).

## Guardrails

- Login / Welcome hide Create Account when registered.
- Register route + page blocked when registered → redirect Login.
- Server stores `ClientInstallations`; same `InstallationId` cannot register twice (409).

## Secure vs SQLite

Unchanged: tokens in Secure Storage; installation/session context in SQLite.
