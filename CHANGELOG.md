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

---

## Epic 1: Shared Kernel, Database & Auth

### Task 1.1 — SharedKernel Project
- `BaseEntity<TId>` with Id, CreatedAt, UpdatedAt, soft-delete, domain events collection
- `IDomainEvent` interface and `DomainEventDispatcher` (reflection-based handler dispatch)
- `Result<T>` pattern for operation results (Success/Failure with implicit conversion)
- `GlobalExceptionHandler` implementing `IExceptionHandler` (returns JSON error responses)
- `ICurrentUser` interface and `Roles` constants (Customer, Admin)
- `IModule` interface for module registration pattern
- Health check endpoints: `/health/live` (liveness), `/health/ready` (DB connectivity), `/health` (all)
- CORS configured via `appsettings` with origins whitelist
- Rate limiting with fixed window on auth endpoints (10 req/min)

### Task 1.2 — Database Schema Design
- All 5 PostgreSQL schemas created: `identity`, `portfolio`, `eshop`, `booking`, `blog`
- Identity: `users`, `refresh_tokens` (2 tables)
- Portfolio: `photos`, `tags`, `photo_tags`, `collections`, `collection_photos`, `photo_variants` (6 tables)
- EShop: `products`, `orders`, `order_items`, `shopping_carts`, `cart_items` (5 tables)
- Booking: `session_types`, `availability_slots`, `bookings` (3 tables)
- Blog: `posts`, `categories`, `post_categories`, `tags`, `post_tags` (5 tables)
- Total: 21 tables across 5 schemas

### Task 1.3 — EF Core Setup
- One `DbContext` per module schema (IdentityDbContext, PortfolioDbContext, EShopDbContext, BookingDbContext, BlogDbContext)
- All contexts configured with Npgsql provider and schema-specific migration history tables
- Development mode uses `EnsureCreated` + `CreateTables` for auto-schema creation
- Production mode configured for proper EF Core migrations
- Seed data: 4 booking session types, 4 blog categories, 1 admin user
- Soft-delete query filters on entities with `DeletedAt`

### Task 1.4 — Authentication & Authorization
- Local email/password registration with BCrypt password hashing
- Local email/password login with JWT access token (15 min) + refresh token (7 days)
- Refresh token rotation (old token revoked on refresh)
- Google OAuth login endpoint (creates/links user accounts)
- `/api/auth/me` endpoint returning current user profile (requires auth)
- `CurrentUser` service extracting claims from JWT via `IHttpContextAccessor`
- JWT configuration via `appsettings` (issuer, audience, secret, expiration)
- Admin user seeded: `admin@dogphoto.sk` / `admin123`

### Task 1.5 — Module Registration Pattern & Architecture Tests
- `IModule` interface with static abstract `AddServices()` and `MapEndpoints()`
- `DependencyInjection.AddInfrastructure()` extension method registers all DbContexts, auth, DI
- `AuthEndpoints.MapAuthEndpoints()` extension method for auth route group
- Architecture tests (xUnit + NetArchTest):
  - SharedKernel does not depend on Infrastructure
  - SharedKernel does not depend on Api
  - Infrastructure does not depend on Api
  - Infrastructure.Auth implements SharedKernel interfaces
- All 4 architecture tests passing

### Verification
- All health checks return Healthy (live, ready, default)
- User registration returns JWT tokens and user data
- User login validates credentials and returns tokens
- Authenticated `/api/auth/me` returns user profile
- Admin user can log in with seeded credentials (Role: Admin)
- All 5 database schemas with 21 tables verified via psql
- Seed data present (4 session types, 4 categories, 1 admin)
- Architecture tests: 4/4 passing
