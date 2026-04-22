# Milestone 3 — Report Export + Retry Hardening

## Added report exports
- `GET /v1/jobs/{id}/report/export.json`
- `GET /v1/jobs/{id}/report/export.csv`

## Added retry/backoff
- Introduced shared `RetryPolicy` utility with exponential backoff.
- Applied retries to Spotify fetch, YouTube search, playlist creation, and track insert operations in `MigrationEngine`.

## UI improvements
- Frontend report section now includes one-click JSON/CSV download links.
