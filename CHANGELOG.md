# Changelog

## Epic 5: Booking Module (2026-04-14)

### Added
- **Backend API** — 10 booking endpoints: session types (list, detail, admin update), availability (query, create, bulk-generate, update/block), bookings (create, view, cancel, my-bookings)
- **Booking flow** — Customers browse session types, pick a date/slot from a calendar, fill a validated form, and receive a pending booking with confirmation email
- **Email notifications** — Branded HTML confirmation email to customer + notification to photographer on every booking (Mailpit locally, production SMTP planned for Epic 9)
- **Session types** — 3 seeded types: Dog Portrait (60 min, 120 EUR), Action Session (90 min, 150 EUR), Outdoor Session (90 min, 140 EUR)
- **Availability management** — Admin API for creating individual slots, bulk-generating slots with configurable duration/breaks, blocking/updating slots
- **Frontend pages** (bilingual SK/EN):
  - `/sk/rezervacia`, `/en/booking` — Session type cards with pricing, detail modals with FAQ accordion, testimonials, trust signals
  - `/sk/rezervacia/kalendar`, `/en/booking/schedule` — Interactive calendar with month navigation, day state indicators, slot picker, validated booking form
  - `/sk/rezervacia/potvrdenie/[id]`, `/en/booking/confirmation/[id]` — Booking confirmation page with summary
- **BookingCalendar component** — Server-rendered with client-side interactivity: month navigation, available/unavailable/past day states, slot time picker, inline form validation
- **SCSS** — `_booking.scss` (847 lines), `_booking-calendar.scss` (602 lines)
- **i18n** — 82 booking + footer keys in both `sk.json` and `en.json`
- **JSON-LD SEO** — `Service`, `Offer`, and `LocalBusiness` schemas on booking pages
- **Footer NAP** — Full Name, Address (Bratislava), Phone, Email in site footer across all pages with bilingual support
- **Booking seed script** — `scripts/seed-booking.sh` for seeding availability slots (Saturdays + weekday afternoons)
- **Booking utility module** — `src/lib/booking.ts` with pure validation and calendar state functions
- **Database** — `booking` schema with 3 tables: `session_types`, `availability_slots`, `bookings`

### Testing
- **Integration tests** — 26 xUnit tests: all booking endpoints (happy path, auth guards, validation failures), email capture via FakeEmailService, slot conflicts, bulk generation with breaks, dog count limits, localization
- **Architecture tests** — Booking module isolation from EShop/Blog verified
- **Frontend unit tests** — 22 Vitest tests: form validation (name, email), calendar day state logic, slot filtering
- **Playwright E2E** — 9 tests: full booking journey (calendar → slot → form → confirmation), form validation errors, session type listing, modal interactions, calendar page load
- **Accessibility** — axe WCAG 2A/2AA scans on `/sk/rezervacia` and `/sk/rezervacia/kalendar`

### Deferred
- Recurring availability rules (e.g., "every Saturday 9-16") → Epic 8 (Admin Interface)
- Session detail pages (`/[slug]`) → replaced by modal-based approach on booking index

### Design Decisions
- Session detail information shown via modal overlay rather than separate `/[slug]` pages — keeps the booking flow compact
- Availability managed via individual/bulk slot creation through API; recurring rule engine deferred to admin panel (Epic 8)
- BookingCalendar uses inline `<script>` with client-side fetch rather than Astro island — sufficient for the calendar's interaction model

---

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

---

## Epic 4: Sample Data & Test Infrastructure

### Task 4.1 — Sample Image Seeding Script
- Created `scripts/seed-images.sh` (bash + curl + jq)
- Authenticates as the seeded admin user (`admin@dogphoto.sk` / `admin123`) and waits for `/health/ready`
- Creates `dog` and `film` tags via `POST /api/portfolio/tags` (idempotent — treats HTTP 409 as success)
- Uploads all 14 images from `img/` (1–7 as `dog-portrait-N`, 8–14 as `film-frame-N`) via `POST /api/image-pipeline/upload` with bilingual SK/EN titles, alt text and descriptions
- Polls `GET /api/image-pipeline/photos/{id}/status` until each photo finishes background processing
- Publishes each photo and assigns its tag via `PUT /api/portfolio/photos/{id}` with `isPublished:true`, `sortOrder` and `tagSlugs`
- Creates two collections via `POST /api/portfolio/collections`:
  - `dog-portraits` (Psie portréty / Dog Portraits) with the 7 dog photos
  - `film-collection` (Filmová kolekcia / Film Collection) with the 7 film photos
- Idempotent end-to-end: re-running the script detects existing tags, photos and collections (HTTP 409) and skips them
- Configurable via `API_URL`, `ADMIN_EMAIL`, `ADMIN_PASSWORD`, `IMG_DIR` env vars

### Task 4.2 — Backend Integration Test Harness
- New project `src/backend/tests/DogPhoto.IntegrationTests` added to `DogPhoto.sln`
- Packages: `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`, `Testcontainers.Azurite`, `xunit`
- `Program.cs` made `partial` so `WebApplicationFactory<Program>` can resolve the entry point
- `ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime` boots the API in-memory against ephemeral Postgres 17 + Azurite Testcontainers and overrides `ConnectionStrings:Default`, `Azure:BlobStorage:ConnectionString`, and `Jwt:*` via in-memory configuration
- `ApiCollection` xUnit collection fixture shares the container set across the whole suite (containers spin up once per test run)
- `ApiTestBase` provides `CreateClient()`, `CreateAdminClientAsync()` (logs in as the seeded admin) and `CreateCustomerClientAsync()` (registers a unique customer per call)
- `SmokeTests` covers `/health/live`, `/health/ready`, admin login + `/api/auth/me`, public portfolio listing, and that `/api/image-pipeline/upload` rejects unauthenticated requests
- Backend Dockerfile updated to copy the new test project for restore in the dev/build stages
- Solution builds clean: 0 warnings, 0 errors

### Task 4.3 — Frontend Test Infrastructure
- Added devDependencies in `src/frontend/package.json`: `vitest@^2.1.0`, `@playwright/test@^1.48.0`, `@axe-core/playwright@^4.10.0`
- New scripts: `test`, `test:unit`, `test:unit:watch`, `test:e2e`, `test:e2e:install`
- `vitest.config.ts` — node environment, picks up `tests/unit/**/*.test.ts`
- `playwright.config.ts` — targets `http://localhost:4321` (the docker compose stack), Chromium project, screenshot/trace on failure
- Unit tests:
  - `tests/unit/i18n.test.ts` — `t()` lookup + missing-key fallback, `getAlternateLang()`, bilingual portfolio collection route rewriting
  - `tests/unit/imageUrl.test.ts` — `buildSrcset()` rewrites `azurite:10000` → `localhost:10000` and sorts widths ascending, `getFallbackUrl()` prefers 1200w jpeg, `getVariantUrl()` builds the expected path
- E2E scaffolding:
  - `tests/e2e/pages/BasePage.ts` — Page Object base with navbar/brand/lang-switch/login locators
  - `tests/e2e/smoke.spec.ts` — verifies `/` redirects to `/sk`, `/sk` and `/en` render the navbar with the correct language switch label, and `/sk` has zero `critical` axe violations against `wcag2a` + `wcag2aa`

### Task 4.4 — Local Test Runner (Makefile)
- New root `Makefile` with targets:
  - `up` / `down` / `logs` — wrap `docker compose`
  - `seed` — runs `scripts/seed-images.sh`
  - `build-backend` / `build-frontend` — build inside the compose containers
  - `test-backend-unit` — runs `dotnet test` for `DogPhoto.ArchTests` inside the api container
  - `test-backend-integration` — runs the new integration test project on the host (Testcontainers needs Docker socket access, which the api dev container does not mount)
  - `test-frontend-unit` — runs Vitest inside the frontend container
  - `test-e2e` — runs Playwright on the host against the running compose stack
  - `playwright-install` — installs Playwright browser binaries on demand
  - `test` — aggregates unit + arch + frontend unit (the host-only targets are documented for the user to run separately)
- `make help` documents every target; targets that need a host toolchain (`dotnet`, `node`) print a clear error if the binary is missing

### Task 4.5 — Test runner hardening (post-first-run fixes)
After bringing the test runner up against the host toolchain, three issues had to be fixed before all four targets passed cleanly:

- **Backend builds were creating root-owned `bin/`/`obj/`.** The original Makefile ran `dotnet build`/`test` inside the api container (which runs as root), so the bind-mounted `src/backend/` ended up with `root:root` files that subsequent host-side builds couldn't overwrite. Reworked the Makefile so `build-backend`, `test-backend-unit`, and `test-backend-integration` all run on the host (`dotnet` is required there anyway for Testcontainers). Added a `make fix-backend-perms` recovery target.
- **Playwright install ran inside the alpine frontend container, which Playwright doesn't support.** Reworked `playwright-install` and `test-e2e` to run on the host instead. The new `playwright-install` target auto-runs `npm install` in `src/frontend/` if the host's `node_modules/` is missing, then runs `npx playwright install --with-deps chromium`. `test-e2e` checks for the host Playwright binary and tells the user to run `playwright-install` first if it's missing.
- **`ApiFactory` config overrides were silently ignored.** `Program.cs` calls `services.AddInfrastructure(builder.Configuration)` at the top level, which reads `ConnectionStrings:Default` *eagerly* during service registration — before `WebApplicationFactory<T>.ConfigureWebHost` callbacks are applied. The result: tests against Testcontainers Postgres were actually trying to connect to `Host=db` from `appsettings.Development.json` (the docker-compose hostname, unresolvable from the host network), surfacing as a misleading `EAI_AGAIN` from `getaddrinfo`. Fixed by setting `ConnectionStrings__Default`, `Azure__BlobStorage__ConnectionString` and `Jwt__*` as **process environment variables** in `IAsyncLifetime.InitializeAsync` (immediately after the containers report their mapped ports). Env vars are read by `WebApplication.CreateBuilder()` before any user code runs, so they win over appsettings.
- The Testcontainers connection strings are also rebuilt explicitly with `Host=127.0.0.1` and the mapped port (rather than relying on `_postgres.GetConnectionString()`/`_azurite.GetConnectionString()`), to bypass any IPv6/`localhost` ambiguity in the WSL2 + .NET 10 RC2 DNS path.
- Caught a real WCAG 2.0 Level A bug as a bonus: `PhotoStrip.astro` rendered `alt={photo.altText || photo.title}` which produced an `<img>` with no `alt` attribute at all when both fields were null on legacy photo records. Every other portfolio component already used `… || ""` as a defensive empty-string fallback — PhotoStrip was the only inconsistency. Fixed in one line. The Playwright + axe smoke test caught this immediately on its first real run, exactly as designed.
- Added `test-results/`, `playwright-report/`, `playwright/.cache/` to `src/frontend/.gitignore`.

### Verification
- `make test-backend-unit` — `Passed!  - Failed: 0, Passed: 4` in 67 ms
- `make test-backend-integration` — `Passed!  - Failed: 0, Passed: 5` in 3 s (Testcontainers Postgres + Azurite spinning up per run)
- `make test-frontend-unit` — `Test Files  2 passed (2)`, `Tests  12 passed (12)` in 507 ms (Vitest)
- `make test-e2e` — `4 passed` in 3.4 s (Playwright + `@axe-core/playwright`, all four smoke specs including the WCAG scan)
- Backend solution build: 0 warnings, 0 errors (including the new integration test project)
- `scripts/seed-images.sh` end-to-end against the live compose stack:
  - Uploaded all 14 images, generated 8 variants per image, applied bilingual metadata
  - Created `dog-portraits` (7 photos) and `film-collection` (7 photos)
  - Second invocation correctly detected and skipped existing tags / photos / collections
- Verified via API: `GET /api/portfolio/photos?tag=dog` and `?tag=film&lang=en` each return all 7 seeded entries with bilingual titles
