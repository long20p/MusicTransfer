# Milestone 1 Implementation Notes

## Delivered

### API (.NET 8)
- OAuth start endpoints:
  - `GET /v1/auth/spotify/start`
  - `GET /v1/auth/google/start`
- OAuth callback skeleton endpoints:
  - `GET /v1/auth/spotify/callback`
  - `GET /v1/auth/google/callback`
- Provider-link status endpoint:
  - `GET /v1/providers/links`
- Migration job endpoints:
  - `POST /v1/jobs`
  - `GET /v1/jobs/{id}`
- Queue inspection endpoint:
  - `GET /v1/queue/pending`

### Queue contracts
- Introduced `MigrationRequested` contract in API and worker skeleton.
- In-memory queue implementation for local dev.

### DB schema baseline
- Added SQL schema file:
  - `src/backend/MusicTransfer.Api/db/001_init.sql`
- Includes tables for users, OAuth links, jobs, and job playlists.

### Frontend (React + TypeScript)
- Added interactive Milestone-1 UI:
  - Connect Spotify button (fetches auth URL)
  - Connect Google button (fetches auth URL)
  - Start migration job form with playlist IDs

## Not yet delivered (next milestones)
- Real token exchange and refresh flow
- Persistent DB integration in API
- Redis-backed queue implementation
- Worker consumption of queued jobs
- Manual review workflow and reporting
