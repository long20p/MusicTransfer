# MusicTransfer — High-Level Design (Updated Stack)

## Objective
Migrate Spotify playlists to YouTube Music using a React TypeScript frontend and .NET 8 backend services, runnable locally and deployable on Azure.

## Stack
- Frontend: React + TypeScript + Vite
- API: ASP.NET Core (.NET 8)
- Worker: .NET 8 Worker Service
- Data: PostgreSQL + Redis
- Deploy: Azure App Service/Container Apps (+ Azure Database for PostgreSQL + Azure Cache for Redis)

## System Architecture
```text
[React Web]
   |
   v
[.NET 8 API] <----> [PostgreSQL]
   |
   v
[Redis Queue] <----> [.NET 8 Worker]
                      |           |
                      v           v
                 [Spotify API] [YouTube API]
```

## Core Flows
1. OAuth connect (Spotify + Google)
2. Select source playlists
3. Create migration job
4. Worker imports and matches tracks
5. Manual review for low-confidence matches
6. Worker creates destination playlists + writes tracks
7. Report generation

## Local Development
- `docker compose up -d` for Postgres + Redis
- Run API (`dotnet run`)
- Run Worker (`dotnet run`)
- Run frontend (`npm run dev`)

## Azure Deployment (target)
- Frontend: Azure Static Web Apps (or App Service)
- API: Azure App Service / Container Apps
- Worker: Container Apps Jobs or App Service background process
- Data: Azure Database for PostgreSQL + Azure Cache for Redis
- Secrets: Key Vault + Managed Identity

## MVP Milestones
1. Foundation + auth wiring
2. Migration pipeline + matching engine v1
3. Manual review and report
4. Hardening (retry, observability, runbooks)
