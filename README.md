# MusicTransfer

Spotify → YouTube Music playlist migration app.

## Goal
Migrate user playlists from Spotify to YouTube Music with:
- OAuth account linking
- Async migration jobs
- Track matching (ISRC + metadata + fuzzy fallback)
- Manual review for low-confidence matches
- Migration report (matched/skipped/failed)

## Monorepo layout
- `apps/api` – HTTP API + auth + job orchestration
- `apps/worker` – background job processor (matching + writes)
- `apps/web` – frontend UI (playlist picker, progress, review)
- `packages/shared` – shared types/contracts
- `docs` – architecture, matching strategy, roadmap

## MVP plan
See:
- `docs/architecture.md`
- `docs/matching.md`
- `docs/roadmap.md`

## Current status
Repository initialized with project skeleton and implementation plan.
