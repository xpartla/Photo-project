# Changelog

## Epic 7: Blog Module (2026-04-17)

### Added
- **Backend API** — `BlogEndpoints.cs` registers under `/api/blog`:
  - Public: `GET /posts` (paginated, filters by `category`, `tag`, `q` full-text search; bilingual SK/EN), `GET /posts/{slug}` (post detail with related posts + reading-time), `GET /categories`, `GET /tags`
  - Admin: `POST /posts`, `PUT /posts/{id}`, `DELETE /posts/{id}` (soft delete), `GET /posts/by-id/{id}` (returns raw Markdown + draft/scheduled posts for the editor), `POST /categories`, `POST /tags`
  - RSS: `GET /rss.xml` (backend-generated, proxied by an Astro route)
- **Markdown rendering** — `Markdig 0.37.*` pipeline with `UseAdvancedExtensions` + `UseSoftlineBreakAsHardlineBreak`. Markdown is stored as-is in `content_markdown_sk/en` and HTML is rendered once at save time into `content_html_sk/en`, so the public read path serves static HTML with no per-request parsing.
- **Full-text search (`?q=`)** — PostgreSQL `ILIKE '%…%'` across `title_sk/en`, `excerpt_sk/en`, and `content_markdown_sk/en`. Same ~15-line approach taken in Epic 6.2 for shop search. `tsvector`/GIN index is documented as the upgrade path if the post catalog grows past ~500 entries.
- **Draft/scheduled/published workflow** — `status` is one of `Draft`/`Scheduled`/`Published`. Public listing and detail endpoints hide everything except `status == Published && publishedAt <= now`. Admins can see drafts + scheduled posts by passing `?includeDrafts=true` (listing) or by visiting the post — `RequireAuthorization` + `ICurrentUser.IsAdmin` on the admin-by-id helper.
- **Admin vs Customer differentiation (frontend)** — the backend already enforced admin via JWT role claims and `ICurrentUser.IsAdmin` on endpoint handlers. Epic 7 wires this into the UI:
  - Navbar reads `localStorage.partlphoto_user.role`. Admins see an "Admin" dropdown link + a small "Admin" chip on the account button. Non-admins and anonymous users see neither.
  - All admin pages use a `requireAdmin()` client-side guard (`src/lib/admin.ts`) that redirects unauthorized visitors to `?return=`-aware login. The backend check remains authoritative — the client-side redirect is just UX.
- **Regular user seed on startup** — `DependencyInjection.SeedDataAsync` now seeds a `customer@dogphoto.sk` / `customer123` user alongside the admin, so manual testing of authenticated-but-non-admin flows works out of the box.
- **Admin CMS (blog only — portfolio/shop/booking wiring lands in Epic 8)**:
  - `/sk/admin`, `/en/admin` — dashboard with cards for each future admin section (the other three are marked "Coming in Epic 8")
  - `/sk/admin/blog`, `/en/admin/blog` — post list including drafts + scheduled, with status badges, View/Edit/Delete actions
  - `/sk/admin/blog/novy`, `/en/admin/blog/new` — create post
  - `/sk/admin/blog/upravit/[id]`, `/en/admin/blog/edit/[id]` — edit post
  - **Full Markdown editor** via `EasyMDE` (toolbar, live preview, side-by-side, fullscreen, table/link/image/guide) with bilingual SK + EN panes, category + tag checkbox lists, status dropdown, scheduled-publish datetime, SEO meta fields in a collapsed `<details>`, and a featured-image URL field
- **Public blog pages (bilingual SK/EN)**:
  - `/sk/blog`, `/en/blog` — listing with `<BlogFilters>` (URL-backed `<form method="get">` exposing search, category `<select>`, tag `<select>`), empty state, pagination, and an RSS badge
  - `/sk/blog/[slug]`, `/en/blog/[slug]` — detail with rendered HTML, author + published date + reading-time, category pills, tag pills, related posts, `BlogPosting` + `BreadcrumbList` JSON-LD
  - `/sk/blog/kategoria/[slug]`, `/en/blog/category/[slug]` — category archive
  - `/sk/blog/tag/[slug]`, `/en/blog/tag/[slug]` — tag archive
- **SCSS** — `_blog.scss` (listing, cards, filters, pagination, empty state), `_blog-detail.scss` (article rendering with typographic defaults for headings/lists/blockquote/code/images), `_admin.scss` (dashboard cards, post list, form layout + EasyMDE outline-styled toolbar)
- **i18n** — 60+ blog/admin keys added to both `sk.json` and `en.json`. Route mapping: `kategoria ↔ category`, `novy ↔ new`, `upravit ↔ edit` in `lib/i18n.ts` so the language switcher lands on the right URL.
- **Seeding** — `scripts/seed-blog.sh` (idempotent, via `curl` + `jq`) creates 6 tags (`film`, `35mm`, `dogs`, `portrait`, `bratislava`, `tips`) and 3 bilingual SK/EN Markdown posts: "Why I Shoot on 35mm Film" (behind-the-scenes), "Dog Portraits: Five Tips for First-Timers" (photography-tips), "Best Photo Spots in Bratislava" (locations). `make seed-blog` target added.
- **EasyMDE dependency** — `"easymde": "^2.20.0"` added to `src/frontend/package.json`

### Fixed
- `src/lib/admin.ts` initial JSDoc comment contained `*/*` (an API path), which esbuild's parser took as end-of-comment — broke Vitest collection. Rewrote as `//` line comments.

### Testing
- **Backend integration tests (15 new)** — `BlogTests.cs` covers listing + empty state, draft/published visibility (public vs admin with `includeDrafts=true`), public 404 on drafts, Markdown rendering, reading-time from word count, filters by category and tag, full-text search matching title / excerpt / content, auth rejection (401 + 403), duplicate-slug 409, update re-renders HTML, soft-delete hides from public, RSS XML shape + content-type, and `customer@dogphoto.sk` seeded-can-login. Total integration: 64 tests, all passing.
- **Arch tests (1 new)** — `BlogModule_ShouldNotDependOn_EShopOrBooking` added to `ModuleBoundaryTests.cs`; total 8, all passing.
- **Frontend unit tests (4 new)** — `admin.test.ts` covers `getStoredUser`/`isAdmin`/`isAuthenticated` with anonymous, Customer, Admin, and malformed-JSON states. Total unit: 56 tests.
- **Playwright E2E (9 new)** — `blog.spec.ts` covers listing render, URL-backed search filter narrowing results, post detail page render, EN listing render, RSS XML response, non-admin redirect off `/sk/admin`, admin `localStorage`-primed visit to `/sk/admin/blog` seeing seeded posts, and axe scans on listing + detail. `BlogPage` page object added. Total E2E: 32 tests.

### Design Decisions
- **Markdown rendered at save time, not per request** — keeps the hot read path trivial (serve pre-rendered HTML) and lets content editors preview exactly what the site will render. The few cases that need the raw Markdown (editor) read `content_markdown_*` directly from the admin endpoint.
- **`ILIKE` again for search** — consistent with the Epic 6.2 shop search decision. With ~10 blog posts it's sub-millisecond; GIN + `tsvector` is documented as the future upgrade path.
- **Client-side admin guard is UX only** — every admin endpoint still enforces `ICurrentUser.IsAdmin` on the backend. The client-side `requireAdmin()` redirect prevents an unauthorized visitor from seeing a blank page, but cannot be relied on for security.
- **Admin dashboard stubs the other modules intentionally** — Epic 7 is scoped to *blog* CMS. The dashboard shows disabled "coming in Epic 8" cards for portfolio/shop/booking so the shape of the admin area is visible without implying those flows work yet.
- **EasyMDE over a custom textarea+preview** — user asked for the full editor (it will be reused in Epic 8 for blog management inside the admin panel). EasyMDE ships toolbar, preview, side-by-side, fullscreen, table/link/image/guide actions in ~300KB; replacing it with something smaller later is straightforward since the editor is isolated behind a single Astro component.
- **Regular user seeded on startup rather than in the blog seed script** — keeps it idempotent and available even without running any seed scripts. Symmetric with the existing admin seed.

---

## Epic 6.2: Shop Collections, Filtering & Search (2026-04-17)

### Added
- **Shop collections (via portfolio reuse)** — `GET /api/shop/collections` surfaces `portfolio.collections` that have ≥1 linked product (derived from `portfolio.collection_photos` → `products.photo_id`). `GET /api/shop/collections/{slug}` returns the full landing data (name, description, cover image, products). No new tables; coupling is deliberate so a visitor navigating portfolio → shop follows a continuous thread.
- **Product filters** — `GET /api/shop/products` accepts `?collection=`, `?tag=`, `?format=`, `?paperType=`, `?q=`. Collection/tag resolve via cross-module reads into portfolio; `q` does `ILIKE '%…%'` across product title/description and photo title/location.
- **Tag inheritance** — `MapProduct` now emits a `tags[]` array inherited from the product's photo. Visible on the product detail and available for filtering.
- **`GET /api/shop/tags`** — lists tags that appear on at least one product's photo, with per-tag product counts for the filter UI.
- **Shop filters component** — `<ShopFilters>` renders search + collection/tag/format/paper `<select>`s as a URL-backed `<form method="get">`. Filters are shareable, bookmarkable, SEO-friendly, and hydrate state from the URL on every page load. Responsive grid: 1 column on mobile, expands on wider viewports.
- **Collection landing pages** — `/sk/obchod/kolekcie/[slug]` + `/en/shop/collections/[slug]` with a hero image (from the collection's `cover_photo_id`), description, and product grid. Back-link to the shop.
- **Seed script overhaul**
  - `seed-images.sh` now creates five additional tags (`street`, `urban`, `landscape`, `portrait`, `analog`), re-tags each film photo with `film + 1–2 themes`, and creates three thematic collections (`bratislava-2026`, `the-alps`, `wildlife-in-motion`). `publish_and_tag` now accepts a CSV of tag slugs.
  - `seed-shop.sh` now creates four products from film photos only (`bratislava-film-1`, `bratislava-film-2`, `alps-landscape-1`, `wildlife-motion-1`) so collection/tag filters have meaningful data out of the box.

### Changed
- **Film-category constraint** — treated as admin discipline (seed script + admin workflow), not a hard API check. Products can reference any photo; the expectation is that shop products use `film`-tagged photos. Flagged in PLAN for a later "enforce on POST /products" upgrade if needed.
- **Search strategy** — `ILIKE` instead of PostgreSQL `tsvector`/`tsquery`. With <100 items it runs in ~1 ms and keeps the implementation to ~15 lines; GIN/tsvector is documented as the upgrade path when the catalog grows.

### Testing
- **Backend integration tests (6 new)** — `GetProducts_FilterByFormat`, `GetProducts_FilterByCollectionAndTag`, `GetProducts_SearchMatchesTitleAndPhotoLocation`, `GetCollections_IncludesOnlyCollectionsWithProducts`, `GetCollectionBySlug_ReturnsProducts`, `GetTags_ReturnsTagsWithProductCounts`. Helper `SeedPhotoWithTagsAndCollectionAsync` seeds portfolio photos/tags/collections directly via `PortfolioDbContext` so tests don't need the image-upload pipeline. 49 tests total, all passing.
- **Arch tests** — unchanged (cross-module read into `Portfolio` was already permitted for `PhotoVariants`).

### Design Decisions
- **Reused `portfolio.collections` instead of a new `eshop.collections` table** — follows the user's portfolio → shop discovery flow (visitor spots a print in a portfolio collection and expects the shop to surface the same collection as a theme). Admin maintains one list of collections, not two. Cover image and description already live on the portfolio collection; nothing to duplicate.
- **Cross-module filters via pre-fetched ID lists** — resolve matching photo IDs in `PortfolioDbContext`, then narrow the `db.Products` query with `Contains(ids)`. Simpler than a raw join across schemas and keeps the read pattern consistent with the existing `PhotoVariants` fan-out.
- **URL-backed filters (`<form method="get">`)** — no client-side state machine; every filter change is a full-page navigation to a bookmarkable URL. Works with Astro's SSR, accessible without JavaScript, and trivially shareable. Fine at ~100 items; if interactive filtering becomes critical, a future client-side layer can hydrate on top without breaking the URL contract.

---

## Epic 6.1: E-Shop Variants + Cart Fix (2026-04-17)

### Added
- **Product variants** — Multi-format, multi-paper support via a new `product_variants` table. Each variant = (product × format × paper × price). Open-edition products can have many variants; limited editions are constrained to exactly one variant to preserve the "one limited edition, one configuration" feel.
- **Format and paper type lookups** — `formats` and `paper_types` tables with bilingual names (`name_sk`, `name_en`), short `code` identifiers, and `display_order`. Seeded with A4/A3/30×40/40×60/50×70 and Fine Art 310g / Baryta / Matte 200g / Premium Glossy.
- **Public endpoints** — `GET /api/shop/formats`, `GET /api/shop/paper-types` for populating the variant dropdowns.
- **Admin endpoints** — `POST /api/shop/formats`, `POST /api/shop/paper-types`, `POST /api/shop/products/{id}/variants`, `PUT/DELETE /api/shop/variants/{id}` for managing the lookup tables and product variants.
- **Order-item snapshots** — `order_items` now stores `format_name_sk/en`, `paper_type_name_sk/en`, and `product_title_sk/en` at purchase time so order history survives later renames or deletes of the referenced product/variant.
- **Cart keyed by variant** — `cart_items.variant_id` replaces `product_id`. Variants of the same product (A4 Matte vs A3 Fine Art) are now distinct cart lines. `POST /api/shop/cart/sync` resolves anonymous-cart items by `(productSlug, formatCode, paperTypeCode)` triples.
- **Variant selection UI** — Product detail page renders two `<select>` dropdowns (format, paper) for open editions that re-price on change; limited editions render a static spec strip ("A4 · Fine Art 310g"). Listing cards show "from X €" when variant prices span a range.

### Fixed
- **Cart disappears after login-on-checkout** — previously, add-to-cart and cart-page reads always hit `localStorage` regardless of auth state, while the checkout submit used the server cart. After login, localStorage was cleared (synced to server) but subsequent adds wrote to localStorage again — which the checkout submit ignored. Rewritten as a single `src/lib/cart.ts` service that switches source-of-truth on `isAuthenticated()`: anonymous → localStorage, authenticated → server API. All pages (product detail, cart, checkout, navbar badge) go through the service.

### Changed
- **Product schema** — removed `format`, `paper_type`, `price` columns from `products`. Price now lives per-variant; product metadata (`is_limited_edition` flag, `edition_size`, `edition_sold`) stays at product level since edition tracking is per-product.
- **Shop listing** — `product.format` removed from cards; price shows `from X EUR` for products with variable variant pricing.
- **Seed script** — `seed-shop.sh` now POSTs a `variants` array. Three limited-edition products each have one variant; one open-edition product (`dog-portrait-3-print`) has five variants spanning A4/A3/30×40 across glossy and fine art papers (45–89 EUR range).
- **Dev schema bootstrap** — `DependencyInjection.ApplyMigrationsAsync` detects the absence of `eshop.product_variants` and drops the `eshop` schema so `EnsureCreated` can rebuild it. Dev-only; production uses versioned migrations.

### Testing
- **Backend integration tests** — added: `CreateProduct_LimitedEditionWithMultipleVariants_Returns400`, `Cart_DifferentVariantsAreDistinctLines`, `Formats_PaperTypes_ArePubliclyReadable`. Existing tests updated for the new `variants` payload and `variantId` cart shape. 43 tests, all passing.
- **Frontend unit tests** — cart tests updated to use `(slug, formatCode, paperTypeCode)` identity instead of slug alone. Adds a test verifying variants of the same product stay as distinct lines.
- **Playwright** — `ensureProduct` helper updated for variants payload.

### Design Decisions
- **`ProductVariant` table over two pivot tables** — the user's initial suggestion of `product_formats` + `product_paper_types` pivots can't express per-combination price, availability, or SKU, and would force cart/order items to compose a three-way composite key. A single `product_variants` row per offered combo is simpler for both storage and the UI (one select wired directly to variant records).
- **Limited editions stay single-variant** — `is_limited_edition` flag enforces exactly one variant at admin creation; the UI renders a static spec strip instead of dropdowns. Keeps the "one piece, one configuration" feel the user asked for without special-casing the data model.
- **Snapshot format/paper names on `order_items`** — variant deletion or rename after purchase must not corrupt historical orders. Snapshots cost ~4 extra varchar columns and remove a whole class of historical-integrity bugs.
- **Price moves entirely off `products`** — avoids the ambiguity of "which price wins, product or variant?". Listings show `minPrice`/`maxPrice` (computed) so the UX still has a clear price headline.

---

## Epic 6: E-Shop Module (2026-04-14)

### Added
- **Payment gateway abstraction** — `IPaymentGateway` interface with `CreatePayment`, `GetPaymentStatus`, `RefundPayment` + `MockPaymentGateway` that simulates the full GoPay flow (in-memory state, fake redirect URLs to `/mock-pay/[id]`). Real `GoPayClient` drops in behind the same interface in Epic 9.
- **Backend API** — 14 shop endpoints: products (list, detail, admin create/update), cart (get, add, update qty, remove, sync from localStorage), orders (create, detail, my-orders, admin status update), payment webhook, payment info for mock page
- **Order workflow** — Full status state machine: `pending_payment` → `paid` → `processing` → `shipped` → `completed`, plus `cancelled` and `refunded`. Validated transitions prevent invalid state changes. Admin status update endpoint with email notification on each transition.
- **Limited edition logic** — `edition_size` + `edition_sold` tracking per product with PostgreSQL `xmin` concurrency token. Edition numbers assigned sequentially on order creation. Auto-unavailable when sold out. Concurrency conflicts caught and reported.
- **Hybrid cart** — localStorage cart for anonymous users, server-side cart for authenticated. On login, localStorage cart syncs to server via `POST /api/shop/cart/sync` (merges items, increments quantity for duplicates). Checkout checks server cart before syncing to prevent double-counting.
- **Email notifications** — 3 branded HTML email templates: customer order confirmation (with edition numbers), photographer order notification (with fulfillment details), order status update. Same Cuphead styling as booking emails.
- **Frontend pages** (bilingual SK/EN):
  - `/sk/obchod`, `/en/shop` — Product grid with Cuphead-styled cards, edition badges ("3 remaining" / "Sold out" / "Open edition"), price in Titan One, availability filter pills
  - `/sk/obchod/[slug]`, `/en/shop/[slug]` — Product detail with framed print effect (mat/passepartout, thick borders, hover-lift shadow), animated floating background blobs, staggered reveal animations, edition stamp overlay, pulsing availability dot, quality trust strip (Fine Art Print, Archival Paper, Certificate of Authenticity, Safe Delivery)
  - `/sk/obchod/kosik`, `/en/shop/cart` — Cart page with quantity +/- controls, item removal, running total, checkout button. Renders from localStorage via client-side JS.
  - `/sk/obchod/pokladna`, `/en/shop/checkout` — Checkout with shipping/billing address forms (same-as-shipping toggle), order summary sidebar, inline validation. Redirects to login if not authenticated.
  - `/sk/obchod/objednavky`, `/en/shop/orders` — My orders listing with status badges, dates, totals. Each links to order detail.
  - `/sk/obchod/objednavky/[id]`, `/en/shop/orders/[id]` — Order confirmation/detail with status badge, line items with edition numbers, total. "Pay now" button for orders stuck in `pending_payment`.
  - `/mock-pay/[id]` — Mock payment page with Confirm/Cancel buttons, redirects to webhook
- **Login pages** — `/sk/prihlasenie`, `/en/login` with email/password form, JWT stored in localStorage, automatic cart sync on login, `?return=` URL parameter for redirect after auth
- **Navbar enhancements** — Cart icon (SVG) with badge counter (reads from localStorage, updates on add), auth state management (checks JWT in localStorage, toggles login button vs account dropdown), logout handler, my-orders link in account dropdown
- **SCSS** — `_shop.scss` (product grid, product detail with animations and decorative blobs), `_cart.scss` (cart items, footer, empty state), `_checkout.scss` (address forms, order summary, mock payment page), `_login.scss` (centered card layout)
- **i18n** — 70+ shop/cart/checkout/order/login keys in both `sk.json` and `en.json`. Route mapping for `obchod` ↔ `shop` (including sub-routes: `kosik` ↔ `cart`, `pokladna` ↔ `checkout`, `objednavky` ↔ `orders`, `prihlasenie` ↔ `login`)
- **JSON-LD SEO** — `Product` + `Offer` schemas on product detail pages (price, availability, seller)
- **Shop seed script** — `scripts/seed-shop.sh` creates 4 products from existing seeded portfolio photos (3 limited editions, 1 open edition), idempotent
- **Database** — `eshop` schema enhanced: `customer_email` column on `orders`, `xmin` concurrency token on `products`, `pending_payment` default status

### Testing
- **Integration tests** — 10 xUnit tests: product CRUD (admin create, duplicate slug conflict, public list/detail, bilingual content, auth guard), cart flow (add + get, sync merge), full checkout flow (cart → order → webhook → paid + email), edition number assignment (sequential across two customers), order status invalid transition, auth guards on cart/orders
- **Architecture tests** — EShop module isolation from Booking/Blog verified; `MockPaymentGateway` implements `IPaymentGateway` verified
- **Frontend unit tests** — 14 Vitest tests: localStorage cart operations (add, increment, remove, update quantity, clear), cart total calculation, edition badge formatting ("3 remaining", "Sold out", "Open edition")
- **Playwright E2E** — 10 tests: product listing (SK + EN), filter buttons, product detail page (title, price, edition, quality strip), add to cart + badge update, empty cart state, cart with items, accessibility scans on `/sk/obchod`, product detail, and cart pages
- **Accessibility** — axe WCAG 2A/2AA scans on shop listing, product detail, and cart

### Design Decisions
- Hybrid cart (localStorage + server sync) chosen over server-only to reduce auth friction — anonymous users can browse and add to cart freely, items merge on login
- Full order status pipeline (7 states) built upfront rather than minimal — avoids refactoring when admin panel (Epic 8) adds status management
- Product detail uses framed "print preview" aesthetic (mat/passepartout + thick borders) rather than standard card — reinforces that these are physical art prints
- Mock payment page lives in frontend (`/mock-pay/[id]`) rather than backend — consistent with Astro routing, GoPay will handle its own redirect page in production
- Login pages created as part of this epic (not originally planned) — checkout flow requires authentication, no login UI existed

---

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
