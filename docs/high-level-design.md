# MusicTransfer — High-Level Design (Spotify → YouTube Music)

## 1) Objective
Build a reliable app that migrates a user’s playlists from Spotify to YouTube Music with minimal manual effort and clear progress/error reporting.

## 2) Scope
### MVP (v1)
- One-way migration: Spotify → YouTube Music
- OAuth account linking for both providers
- Playlist selection and migration job start
- Asynchronous processing via worker queue
- Track matching with confidence scoring
- Manual review for low-confidence matches
- Final migration report (matched, skipped, failed)

### Out of scope (v1)
- Continuous sync / scheduled sync
- Two-way sync
- Collaborative playlist reconciliation
- Rich recommendation engine

## 3) High-Level Architecture

```text
[Web UI]
   |
   v
[API Service] <----> [Postgres]
   |
   v
[Queue/Redis] <----> [Worker Service]
                      |         |
                      v         v
                [Spotify API] [YouTube API]
```

### Components
- **Web UI**: connect accounts, choose playlists, monitor progress, resolve ambiguous tracks.
- **API Service**:
  - OAuth flow + token refresh orchestration
  - Migration job creation and status endpoints
  - Manual-review decisions endpoint
- **Worker Service**:
  - Imports source tracks from Spotify
  - Runs matching pipeline
  - Creates/updates destination playlists in YouTube
  - Retries failed operations with backoff
- **Postgres**: users, tokens, jobs, playlist snapshots, track metadata, match decisions, event logs.
- **Redis/Queue**: async job processing, retries, dead-letter handling.

## 4) Data Flow (MVP)
1. User connects Spotify and Google accounts.
2. User selects playlists and starts migration.
3. API creates `migration_job` and enqueues work.
4. Worker pulls source playlists/tracks from Spotify.
5. Worker maps each source track to best YouTube candidate.
6. Worker auto-accepts high-confidence matches.
7. Worker stores low-confidence items for manual review.
8. After review, worker creates target playlists and inserts tracks in order.
9. API surfaces final report and downloadable summary.

## 5) Matching Strategy
Priority order:
1. **ISRC exact match** (highest confidence)
2. **Normalized title + artist + duration tolerance**
3. **Fuzzy ranking** across title/artist tokens
4. **Manual review** bucket for uncertain matches

### Confidence bands
- **High (>= 0.90)**: auto-accept
- **Medium (0.70–0.89)**: auto-accept only if no near-tie candidate
- **Low (< 0.70)**: requires user review

## 6) API Surface (MVP)
- `POST /v1/auth/spotify/start`
- `GET /v1/auth/spotify/callback`
- `POST /v1/auth/google/start`
- `GET /v1/auth/google/callback`
- `GET /v1/playlists/source` (Spotify playlists)
- `POST /v1/jobs` (start migration)
- `GET /v1/jobs/:id` (status/progress)
- `GET /v1/jobs/:id/review-items`
- `POST /v1/jobs/:id/review-decisions`
- `GET /v1/jobs/:id/report`

## 7) Reliability & Failure Handling
- Idempotency keys for write operations (playlist creation/add-track)
- Exponential backoff + jitter for rate limits / transient provider errors
- Dead-letter queue for repeated failures
- Checkpointing so jobs can resume mid-migration
- Structured job event log for diagnostics

## 8) Security & Privacy
- Encrypt provider tokens at rest
- Use least-privilege OAuth scopes
- Separate encryption keys from DB secrets
- User data deletion endpoint (hard delete tokens + metadata)
- Avoid storing unnecessary listening history

## 9) Observability
- Metrics:
  - job success rate
  - average migration duration
  - track match rate
  - manual review rate
  - provider error rate
- Logs: structured per job + per playlist
- Alerts: repeated provider failures, queue backlog growth, token refresh failures

## 10) Deployment (MVP)
- Containerized API + Worker
- Managed Postgres + Redis
- Environment-based config (`.env` + secret store)
- CI/CD pipeline with staging then production

## 11) Milestones
### Milestone 1 — Foundation
- Repo scaffold, API health, worker scaffold
- DB schema and migrations
- OAuth wiring for Spotify + Google

### Milestone 2 — Migration Core
- Playlist import from Spotify
- Matching pipeline v1
- Playlist creation + track insertion on YouTube

### Milestone 3 — Review & Reporting
- Manual-review UI/API
- Job report endpoint and export
- Error handling hardening

### Milestone 4 — Production hardening
- Observability dashboards and alerts
- Retry tuning and edge-case handling
- Documentation + runbook

## 12) Risks & Mitigations
- **Provider API limits** → queue + retry + batching
- **Ambiguous track mapping** → confidence scoring + manual review
- **Token expiry issues** → robust refresh + proactive renewal
- **Partial failures** → checkpointing + idempotent writes

## 13) Success Criteria (MVP)
- User can migrate at least one playlist end-to-end
- >= 90% automatic match rate on typical mainstream playlists
- Clear manual flow for unresolved tracks
- Job report gives actionable outcomes and errors
