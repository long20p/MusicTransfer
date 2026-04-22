# Milestone 1 — Foundation Completion

This change closes remaining M1 foundation gaps:

## Added
- EF Core + PostgreSQL wiring in API (`AppDbContext` + entities)
- Connection-string based registration:
  - `ConnectionStrings:Postgres`
  - `ConnectionStrings:Redis`
- Redis-backed queue implementation (`RedisJobQueue`) implementing the same queue contract
- Dev utility endpoint to initialize schema when Postgres is configured:
  - `POST /v1/db/ensure`

## Behavior
- If Redis connection string exists, API uses `RedisJobQueue`.
- Otherwise it falls back to `InMemoryJobQueue`.
- If Postgres connection string exists, EF Core DbContext is enabled.

## Notes
- This keeps local dev friction low while enabling production-backed infra wiring.
