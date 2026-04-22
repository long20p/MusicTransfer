# Architecture (MVP)

## Components
1. Web UI
2. API service
3. Worker service
4. Postgres (state)
5. Redis (queue/cache)

## Flow
1. User links Spotify + Google accounts (OAuth)
2. User starts migration job
3. API enqueues job
4. Worker fetches Spotify playlist tracks
5. Worker matches tracks to YouTube Music candidates
6. Worker creates target playlists + inserts tracks
7. API exposes status + summary

## Key entities
- users
- oauth_tokens
- migration_jobs
- migration_playlists
- source_tracks
- track_matches
- job_events

## Reliability
- Idempotent writes
- Retry with exponential backoff
- Dead-letter handling for repeated failures
