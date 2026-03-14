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

---

## Epic 2: Image Pipeline (Local)

### Task 2.1 — Upload Flow
- `POST /api/image-pipeline/upload` endpoint (Admin-only, multipart form)
  - Accepts image file (JPEG, PNG, WebP, TIFF) + metadata (slug, titles, alt text, location)
  - Validates content type and slug uniqueness
  - Stores original in Azurite `originals/` container (private access)
  - Creates `Photo` record in portfolio schema
  - Queues photo for background processing
- `GET /api/image-pipeline/photos/{id}/status` endpoint (Admin-only)
  - Returns processing status, variant count, extracted metadata
- `IBlobStorageService` with upload, download, and container management
  - `originals/` container: private access
  - `processed/` container: public blob-level access
  - Blob containers auto-created on API startup

### Task 2.2 — Image Processing (SixLabors.ImageSharp)
- Background processing via `Channel<Guid>` queue + `BackgroundService` worker
- EXIF metadata extraction: camera model, f-number, exposure time, ISO, focal length, shot date
- Blurhash placeholder generation (4×3 components, inline DCT encoder)
- Dominant color extraction (resize to 1×1 pixel, hex output)
- Responsive variant generation:
  - Widths: 400px (thumbnail), 800px, 1200px (gallery), 2000px (full preview)
  - Formats: WebP (quality 80), JPEG (quality 85)
  - Only generates variants ≤ original width
  - 8 variants per image (4 widths × 2 formats)
- Variants stored in `processed/{photoId}/{width}w.{format}` blob paths
- Each variant recorded in `photo_variants` table with dimensions, format, quality, URL, size

### Task 2.3 — Frontend Image Component
- `<ResponsiveImage>` Astro component (`src/frontend/src/components/ResponsiveImage.astro`)
  - `<picture>` element with WebP `<source>` srcset and JPEG `<img>` fallback
  - Responsive `sizes` attribute for viewport-based selection
  - `loading="lazy"` / `loading="eager"` + `fetchpriority` support
  - Dominant color background placeholder via inline style
  - `data-blurhash` attribute for progressive loading (client-side decode)
- Image URL abstraction layer (`src/frontend/src/lib/imageUrl.ts`)
  - Returns Azurite URLs locally, CDN URLs in production (via `PUBLIC_BLOB_BASE_URL`)
  - `buildSrcset()`, `getFallbackUrl()`, `getVariantUrl()` utilities

### Infrastructure Changes
- Added `Azure.Storage.Blobs` and `SixLabors.ImageSharp` NuGet packages
- Azurite now runs with `--skipApiVersionCheck` for SDK compatibility
- API depends on Azurite service in Docker Compose
- Backend Dockerfile copies test project for restore

### Verification
- Admin can upload an image via `POST /api/image-pipeline/upload` (returns 201)
- Background worker generates all 8 variants and stores in Azurite `processed/` container
- All variants accessible via HTTP (200 response from Azurite)
- Photo metadata extracted: width, height, dominant color, blurhash
- Status endpoint confirms processing complete with variant count
- Frontend `<ResponsiveImage>` component renders `<picture>` with srcset
- Full pipeline works end-to-end in Docker Compose
- Architecture tests: 4/4 passing
- Health checks: all Healthy

---

## Epic 3: Portfolio Module (Local)

### Task 3.1 — Backend API
- Created `PortfolioEndpoints.cs` with full CRUD API:
  - `GET /api/portfolio/photos` — paginated, filterable by tag/collection, bilingual
  - `GET /api/portfolio/photos/{slug}` — photo detail with EXIF, variants, related photos
  - `GET /api/portfolio/collections` — list collections with cover photos
  - `GET /api/portfolio/collections/{slug}` — collection detail with paginated photos
  - `POST /api/portfolio/photos` — update photo metadata (Admin)
  - `PUT /api/portfolio/photos/{id}` — update photo fields and tags (Admin)
  - `DELETE /api/portfolio/photos/{id}` — soft delete (Admin)
  - `POST /api/portfolio/tags` — create tags (Admin)
  - `GET /api/portfolio/tags` — list tags with photo counts
  - `POST /api/portfolio/collections` — create collection with photos (Admin)
  - `PUT /api/portfolio/collections/{id}` — update collection (Admin)
  - `DELETE /api/portfolio/collections/{id}` — soft delete (Admin)
- All public endpoints support `?lang=sk|en` parameter for bilingual content
- Admin endpoints require JWT authentication with Admin role

### Task 3.2 — Frontend Pages
- Switched Astro output to `server` mode for dynamic API fetching
- Created API client (`src/lib/api.ts`) with typed interfaces for all portfolio data
- Created i18n helper (`src/lib/i18n.ts`) with alternate URL generation for hreflang
- Portfolio gallery pages:
  - `/sk/portfolio` and `/en/portfolio` — masonry grid with lazy loading, collections strip, tag filtering, pagination
  - `/sk/portfolio/[slug]` and `/en/portfolio/[slug]` — single photo with EXIF data, tags, related photos
  - `/sk/portfolio/kolekcie/[slug]` and `/en/portfolio/collections/[slug]` — collection view with paginated photos
- Added `<slot name="head">` to `BaseLayout` for per-page SEO injection
- "Buy as print" placeholder links on photo detail pages (ready for Epic 6)

### Task 3.3 — SEO Implementation
- Created `SeoMeta.astro` component providing:
  - `<link rel="canonical">` on every page
  - `hreflang` tags linking SK/EN versions (including `x-default`)
  - OpenGraph metadata (`og:title`, `og:description`, `og:type`, `og:image`, `og:locale`)
  - Twitter Card (`summary_large_image`) on photo pages
  - JSON-LD structured data per page type
- JSON-LD `ImageGallery` on portfolio index and collection pages
- JSON-LD `ImageObject` on photo detail pages with:
  - `contentUrl`, `author`, `datePublished`, `exifData`, `contentLocation`, `keywords`
- Bilingual route mapping: SK `/kolekcie/` ↔ EN `/collections/`

### Task 3.4 — Portfolio ↔ Shop Links
- "Buy as print" placeholder on photo detail pages (dashed border, ready for shop module)

### Infrastructure Changes
- Added portfolio-specific i18n strings to `sk.json` and `en.json` (13 new keys each)
- Astro output mode changed from `static` to `server` for SSR

### Verification
- Backend builds: 0 errors, 0 warnings
- Frontend type check: 0 errors, 0 warnings
- Architecture tests: 4/4 passing
- All portfolio API endpoints registered and accessible
- Bilingual routing works (`/sk/portfolio`, `/en/portfolio`)
- JSON-LD, OpenGraph, hreflang metadata present on all portfolio pages
- Admin can manage photos, tags, and collections via API
