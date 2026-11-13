# PartlPhoto

Website and admin CMS for a dog photographer in Bratislava: portfolio, session
booking, blog, customer accounts, and a print store that is built but hidden
behind a feature flag. Slovak and English.

- **Backend** — .NET 10 minimal API, EF Core + PostgreSQL, Azure Blob Storage (Azurite locally)
- **Frontend** — Astro 6 (SSR), TypeScript, SCSS
- **Local stack** — Docker Compose: database, API, frontend, Azurite, Mailpit

## Quick start

You need Docker with Compose, `make`, `curl` and `jq`.

```bash
make up            # build and start everything; the first start takes a few minutes
make seed          # 14 sample photos, tags, collections (needs img/1.jpeg … img/14.jpeg)
make seed-blog     # three sample posts
./scripts/seed-booking.sh   # availability slots; run once, it is not idempotent
```

| | |
|---|---|
| Site | http://localhost:4321 |
| API | http://localhost:5000 (`/health/ready`) |
| Outgoing mail (Mailpit) | http://localhost:8025 |
| Admin login | `admin@dogphoto.sk` / `admin123` |
| Customer login | `customer@dogphoto.sk` / `customer123` |

The two accounts and the starter booking packages are created when the API
starts. `img/` is not in the repository; any 14 JPEGs named `1.jpeg` … `14.jpeg`
will do.

`make down` stops the stack and keeps its data. `docker compose down -v` wipes
the database and stored images.

If a port is already in use, remap it in a `compose.override.yml` next to
`compose.yml` (gitignored, merged automatically):

```yaml
services:
  db:
    ports: !override
      - "5433:5432"
```

## Working on it

The API and the frontend reload on file changes. `make help` lists every task.

```bash
make test-backend      # arch + integration tests (host .NET 10 SDK, Docker for Testcontainers)
make test-frontend     # Vitest unit tests, inside the frontend container
make test-e2e          # Playwright against the running stack (host Node 22; `make playwright-install` once)
make migration CONTEXT=Booking NAME=AddSomething   # after changing an entity
```

Database migrations are applied when the API starts.

The store is off by default. To work on it, set `PUBLIC_STORE_ENABLED: "true"`
on the `frontend` service, restart it, and run `make seed-shop`. Details are in
[docs/store-reenable.md](docs/store-reenable.md).

## More

- [DESIGN.md](DESIGN.md) — visual style guide
- [CHANGELOG.md](CHANGELOG.md) — what was built, when, and why
- [docs/store-reenable.md](docs/store-reenable.md) — the hidden store
