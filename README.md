# MusicTransfer

Spotify → YouTube Music playlist migration app.

## Stack
- **Frontend**: React + TypeScript (Vite)
- **Backend API**: .NET 8 (ASP.NET Core Web API)
- **Background Worker**: .NET 8 Worker Service
- **Data**: PostgreSQL + Redis
- **Deploy**: Azure (App Service / Container Apps) or run locally with Docker Compose

## Monorepo Layout
- `src/frontend` — React TypeScript app
- `src/backend/MusicTransfer.Api` — .NET 8 API
- `src/backend/MusicTransfer.Worker` — .NET 8 worker
- `infra/azure` — Azure deployment templates (starter)
- `docs` — architecture/design/roadmap

## Local Run (after installing prerequisites)
Prerequisites:
- .NET SDK 8+
- Node.js 20+
- Docker

1. Start infrastructure:
   ```bash
   docker compose up -d
   ```
2. Run API:
   ```bash
   cd src/backend/MusicTransfer.Api
   dotnet run
   ```
3. Run worker:
   ```bash
   cd src/backend/MusicTransfer.Worker
   dotnet run
   ```
4. Run frontend:
   ```bash
   cd src/frontend
   npm install
   npm run dev
   ```

## Notes
This repo was refactored to .NET 8 + React TypeScript architecture. If .NET SDK is missing on host, install it before running.
