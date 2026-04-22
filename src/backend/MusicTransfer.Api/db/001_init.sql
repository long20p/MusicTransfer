-- Milestone 1: initial schema baseline (PostgreSQL)

create table if not exists users (
  id uuid primary key,
  external_id text not null unique,
  created_at_utc timestamptz not null default now()
);

create table if not exists oauth_links (
  id uuid primary key,
  user_id uuid not null references users(id) on delete cascade,
  provider text not null,
  access_token text,
  refresh_token text,
  scope text,
  token_expires_at_utc timestamptz,
  created_at_utc timestamptz not null default now(),
  updated_at_utc timestamptz not null default now(),
  unique(user_id, provider)
);

create table if not exists migration_jobs (
  id uuid primary key,
  user_id uuid not null references users(id) on delete cascade,
  source_provider text not null,
  target_provider text not null,
  status text not null,
  created_at_utc timestamptz not null default now(),
  updated_at_utc timestamptz not null default now()
);

create table if not exists migration_job_playlists (
  id uuid primary key,
  job_id uuid not null references migration_jobs(id) on delete cascade,
  source_playlist_id text not null,
  target_playlist_id text,
  status text not null,
  created_at_utc timestamptz not null default now()
);

create index if not exists idx_oauth_links_user_provider on oauth_links(user_id, provider);
create index if not exists idx_migration_jobs_user_created on migration_jobs(user_id, created_at_utc desc);
create index if not exists idx_job_playlists_job on migration_job_playlists(job_id);
