# Changelog

## Epic 0: Local Development Environment

### Task 0.1 — Repository Setup
- Initialized Git repo on `main` branch
- Created `.gitignore` (covers .NET, Node, Docker, IDE, OS files)
- Created `.editorconfig` (consistent formatting across C#, TS, YAML, Astro)
- Created `CLAUDE.md` with project conventions

### Task 0.2 — Backend Skeleton
- Created `DogPhoto.sln` with three projects:
  - `DogPhoto.Api` — ASP.NET 10 minimal API host with `/health` endpoint
  - `DogPhoto.SharedKernel` — empty class library, referenced by Api
  - `DogPhoto.Infrastructure` — empty class library, referenced by Api
- Added `appsettings.json` and `appsettings.Development.json` (with local connection strings)

### Task 0.3 — Frontend Skeleton
- Scaffolded Astro 6 project using `create-astro` (Node 22 via nvm)
- Configured `@astrojs/node` v10 adapter for SSR support
- Set up i18n routing in `astro.config.mjs` (Slovak default, English secondary, `/sk` and `/en` prefixes)
- Created `BaseLayout.astro` with nav, language switcher, and footer
- Created bilingual homepages (`/sk` and `/en`) with redirect from `/` → `/sk`
- Added i18n translation files (`sk.json`, `en.json`)

### Task 0.4 — Docker Compose
- Created `infra/docker/docker-compose.yml` with 5 services:
  - `db` — PostgreSQL 17 with health check
  - `api` — .NET backend with hot reload (dotnet watch)
  - `frontend` — Astro dev server with hot reload
  - `azurite` — Azure Blob Storage emulator
  - `mailpit` — Email catcher (UI at :8025, SMTP at :1025)
- Created root `compose.yml` that includes the infra compose file

### Task 0.5 — Dockerfiles
- Created `infra/docker/backend.Dockerfile` (multi-stage: dev/build/prod)
  - Dev target uses `dotnet watch run` for hot reload
  - Prod target uses non-root user
- Created `infra/docker/frontend.Dockerfile` (multi-stage: dev/build/prod)
  - Dev target uses `npm run dev` with `--host` for container access
  - Prod target serves via Node adapter

### Verification
- `docker compose up` starts all 5 services successfully
- API health check at `localhost:5000/health` returns 200
- Frontend at `localhost:4321` serves bilingual Slovak/English pages
- Mailpit UI accessible at `localhost:8025`
- Azurite blob storage emulator running on `localhost:10000`
- Hot reload configured for both backend and frontend via volume mounts
