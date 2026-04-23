# Runtime Implementation Status

## App Configuration
- API now has `appsettings.json` + `appsettings.Development.json`.
- Worker now has `appsettings.json` + `appsettings.Development.json`.
- Config supports Redis, Postgres, OAuth client settings, and worker polling options.

## OAuth + Provider Clients
- OAuth callbacks now perform real token exchange for Spotify and Google.
- Access/refresh token payloads are stored in migration store.
- API supports provider mode switch via `Providers:UseMockProviders`:
  - `true` => mock providers
  - `false` => real Spotify + YouTube API clients

## Worker
- Worker now consumes queue messages from Redis list `musictransfer:migration:requested`.
- For each dequeued job, worker calls API endpoint `/v1/jobs/{id}/run`.
- Loop includes basic error handling and retry delay via polling interval.
