# Re-enabling the Store / E-commerce module

**Status:** Store **hidden** for the initial dog-photography launch (since 2026-06-25).
**Owner action to re-enable:** flip one env var + rebuild + reseed. Details below.

The e-commerce store (shop, cart, checkout, customer orders, and the admin shop
CMS) is fully built and **left intact in the codebase**. It is hidden from
visitors behind a single feature flag so the site can launch focused purely on
dog photography (portfolio + booking + blog). When there is enough demand to sell
prints, the store can be brought back without rebuilding any of it.

This document is the single source of truth for: what was hidden, what was kept,
the before/after state, and the exact steps to turn the store back on.

---

## 1. The feature flag

Everything hangs off one boolean.

| | |
|---|---|
| **Env var** | `PUBLIC_STORE_ENABLED` |
| **Read in** | `src/frontend/src/lib/features.ts` → `export const STORE_ENABLED` |
| **Default** | `false` (any value other than the string `"true"` keeps the store hidden) |
| **Consumed by** | the Astro middleware (route guard) + every store UI surface |

```ts
// src/frontend/src/lib/features.ts
export const STORE_ENABLED = import.meta.env.PUBLIC_STORE_ENABLED === "true";
```

> ⚠️ **Astro inlines `import.meta.env.PUBLIC_*` at _build_ time.** The dev server
> (`astro dev`, which is what the docker `frontend` service runs) picks the value
> up at runtime on restart, but a **production build must have the var set before
> `npm run build`**. See step-by-step in §6.

---

## 2. Before vs. after hiding

| Surface | Store ENABLED (`PUBLIC_STORE_ENABLED=true`) | Store HIDDEN (current default) |
|---|---|---|
| Navbar "Shop" link | shown | removed |
| Navbar cart icon + badge (desktop & mobile) | shown | removed |
| "My Orders" in account dropdown | shown | removed |
| Home photo strips "Shop" link per photo | shown | removed |
| Portfolio detail "Buy as print" button | shown | removed (only "Book a session" remains) |
| Portfolio lightbox "Buy as print" button | shown | removed |
| Account dashboard "Orders" card | shown | removed |
| Admin dashboard "Shop" card + "Pending orders" count | shown | removed |
| `/{sk,en}/shop` · `/{sk,en}/obchod` (and sub-routes) | served | **302 → `/{lang}` home** |
| `/{sk,en}/admin/shop` · `/admin/obchod` (and sub-routes) | served | **302 → `/{lang}` home** |
| Backend `/api/shop/**` endpoints | live | **live** (unchanged — see §4) |

So while hidden: nothing store-related is linked anywhere, and the routes cannot
be reached even by typing the URL — they redirect home.

---

## 3. What was CHANGED to hide the store

All changes are additive/guarding — none delete store functionality.

### New files
- `src/frontend/src/lib/features.ts` — the `STORE_ENABLED` flag.
- `src/frontend/src/middleware.ts` — Astro middleware. When `STORE_ENABLED` is
  false it redirects these patterns to the language home page:
  - `^/(sk|en)/(obchod|shop)(/|$)`
  - `^/(sk|en)/admin/(obchod|shop)(/|$)`

### UI surfaces wrapped in `{STORE_ENABLED && …}`
| File | What is guarded |
|---|---|
| `src/components/Navbar.astro` | shop nav link (built conditionally into `leftLinks`), desktop cart `<li>`, mobile cart `<a>`, "My Orders" dropdown link |
| `src/components/PhotoStrip.astro` | per-photo "Shop" link |
| `src/components/PortfolioSplit.astro` | lightbox "Buy as print" `<a>` + the JS that sets its href is now null-safe (`if (lightboxShop) …`) |
| `src/pages/en/portfolio/[slug].astro` | "Buy as print" action button |
| `src/pages/sk/portfolio/[slug].astro` | "Buy as print" action button |
| `src/pages/en/account/index.astro` | "Orders" card |
| `src/pages/sk/ucet/index.astro` | "Orders" card |
| `src/pages/en/admin/index.astro` | "Shop" card + "Pending orders" count card |
| `src/pages/sk/admin/index.astro` | "Shop" card + "Pending orders" count card |

### Tests
- `src/frontend/tests/e2e/shop.spec.ts` — the whole `shop` describe is skipped
  unless `PUBLIC_STORE_ENABLED=true` (the routes redirect while hidden, so the
  specs would otherwise fail). Re-enabling the flag re-enables the specs.

### Seed data
- `scripts/seed-shop.sh` — the sample products were repointed from the removed
  fine-art photos (`film-frame-*`) to **dog photos** seeded by `seed-images.sh`
  (`portret-1`, `portret-3`, `turista-2`, `portret-2`) and renamed to dog-print
  themes. A note was added that the store must be enabled to view them. This
  script is **not run** by default while the store is hidden, but stays valid.

---

## 4. What was KEPT (intact, do NOT need re-creating)

These were intentionally left untouched and remain fully functional:

- **All backend store endpoints are live** — `app.MapEShopEndpoints()` in
  `src/backend/DogPhoto.Api/Program.cs` was **not** disabled. The `/api/shop/**`
  routes (products, cart, checkout, collections, formats, paper-types, payment
  webhook, admin orders) still respond. Rationale: the API is not customer-facing
  once the UI is hidden, and keeping it live avoids breaking the backend
  integration tests (`tests/DogPhoto.IntegrationTests/EShopTests.cs`).
- **All store page files** — `src/pages/{en/shop,sk/obchod}/**` (public) and
  `src/pages/{en/admin/shop,sk/admin/obchod}/**` (admin CMS) are unchanged; they
  are only made unreachable by the middleware.
- **All store components** — `AdminProductList`, `AdminProductEditor`,
  `AdminOrderList`, `AdminOrderDetail`, `AdminLookupManager`, `ShopFilters`.
- **Cart logic** — `src/lib/cart.ts` and the cart-sync calls in the login/register
  pages are unchanged (harmless while no cart UI is rendered).
- **All store i18n keys** — `shop.*`, `cart.*`, `checkout.*`, `order.*`,
  `admin.shop.*`, `nav.shop`, `nav.cart`, `home.viewInShop`,
  `portfolio.buyAsPrint` remain in `en.json`/`sk.json`. Unused keys are harmless.
- **Database schema** — the `eshop` schema and all its tables are unchanged.

---

## 5. Revisit when re-enabling (deferred copy/decisions)

These were deliberately **left as-is** while hidden and should be reviewed before
the store goes live, because they still carry the old fine-art-prints framing:

- `shop.subtitle` — "Limited edition fine art prints from analog film" /
  "Limitované edície autorských tlačí z analógového filmu"
- `shop.description` — mentions "dog and film photography" / "psej a filmovej
  fotografie"
- `shop.filters.searchPlaceholder` — "Search prints…" / "Hľadať tlače…"
  (fine if kept, just review)
- Sample products in `scripts/seed-shop.sh` are now dog-print themed, but pricing,
  editions, formats and paper types are placeholders — review for real catalogue.
- **Payments**: checkout still uses `MockPaymentGateway`. Real selling requires
  the planned **GoPay** integration (Epic 9). Do not enable the store for real
  customers until a real payment gateway replaces the mock.

---

## 6. How to re-enable — step by step

### A. Local dev (docker compose)
1. Add the env var to the `frontend` service in
   `infra/docker/docker-compose.yml` (next to `PUBLIC_API_URL`):
   ```yaml
   environment:
     PUBLIC_API_URL: http://api:8080
     PUBLIC_STORE_ENABLED: "true"
   ```
2. Restart the frontend container so `astro dev` re-reads it:
   ```bash
   docker compose restart frontend     # or: docker compose up -d frontend
   ```
3. Seed shop products (photos must already be seeded):
   ```bash
   make seed        # dog photos + tags + collections (if not already done)
   make seed-shop   # sample dog-print products
   ```
4. Verify (see §7). The shop specs now run:
   ```bash
   PUBLIC_STORE_ENABLED=true npm --prefix src/frontend run test:e2e
   ```

### B. Production build
1. Set `PUBLIC_STORE_ENABLED=true` in the build environment **before**
   `npm run build` (it is inlined at build time). In CI/CD set it as a build env
   var; in the prod Docker target pass it as a build arg/env consumed during the
   build stage.
2. Rebuild and redeploy the frontend.
3. Ensure the backend `/api/shop/**` is reachable (it already is) and a real
   payment gateway is configured (see §5).

### C. Reverting to hidden
Remove the env var (or set it to anything other than `"true"`) and rebuild /
restart. No code changes required.

---

## 7. Verification checklist (store enabled)

- [ ] "Shop" appears in the navbar; cart icon shows (desktop + mobile).
- [ ] `/sk/obchod` and `/en/shop` load the product grid (no redirect).
- [ ] Product detail → add to cart → cart → checkout → mock payment → order
      confirmation; confirmation email visible in Mailpit (`:8025`).
- [ ] "My Orders" link in the account dropdown works; account "Orders" card shows.
- [ ] Admin dashboard shows the "Shop" card + "Pending orders" count;
      `/admin/shop` (EN) / `/admin/obchod` (SK) product & order CMS load.
- [ ] Portfolio detail + lightbox show the "Buy as print" button again.
- [ ] `PUBLIC_STORE_ENABLED=true npm run test:e2e` runs the shop specs (not skipped).

---

## 8. Not affected by store hiding

Portfolio, Booking, Blog, Auth, and Account (profile + addresses) are independent
of the store flag and remain fully visible and functional throughout.
