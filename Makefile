# PartlPhoto local development & test runner.
#
# Required host tooling (not bundled in the docker stack):
#   - Docker + docker compose                        — for `make up`, the seed script
#   - .NET 10 SDK on the host                        — for `make build-backend`,
#                                                       `make test-backend-*` (avoids
#                                                       root-owned bin/obj that the
#                                                       api container would otherwise
#                                                       create in the bind mount)
#   - Node 22 on the host (use nvm) + `npm install`  — for `make test-e2e` and
#     in src/frontend/                                 `make playwright-install`
#                                                       (Playwright doesn't support the
#                                                       alpine frontend image)
#   - jq, curl on the host                           — for `make seed`

.PHONY: help up down logs seed \
        test test-backend test-backend-unit test-backend-integration \
        test-frontend test-frontend-unit test-e2e \
        playwright-install build-backend build-frontend fix-backend-perms

help:
	@echo "PartlPhoto — common tasks"
	@echo ""
	@echo "  make up                       Start full local stack (db, api, frontend, azurite, mailpit)"
	@echo "  make down                     Stop the local stack"
	@echo "  make logs                     Tail compose logs"
	@echo "  make seed                     Run scripts/seed-images.sh against the running stack"
	@echo ""
	@echo "  make test                     Run all tests (backend unit+arch+integration, frontend unit, E2E)"
	@echo "  make test-backend             Backend unit + arch + integration tests"
	@echo "  make test-backend-unit        Backend unit + arch tests only (no Testcontainers)"
	@echo "  make test-backend-integration Backend integration tests (requires Docker socket on host)"
	@echo "  make test-frontend            Frontend Vitest unit tests"
	@echo "  make test-e2e                 Playwright + axe end-to-end tests against the compose stack"
	@echo ""
	@echo "  make playwright-install       Install host node_modules + Playwright Chromium (one-time)"
	@echo "  make build-backend            dotnet build of the solution (host)"
	@echo "  make build-frontend           astro build (in the frontend container)"
	@echo "  make fix-backend-perms        chown src/backend back to the host user (after a container build)"

# ── Stack management ───────────────────────────────────────────────────

up:
	docker compose up -d

down:
	docker compose down

logs:
	docker compose logs -f

seed:
	./scripts/seed-images.sh

# ── Build ──────────────────────────────────────────────────────────────

# Backend builds run on the host so bin/obj are owned by the host user.
# Running `dotnet build` inside the api container would create root-owned
# files in the bind-mounted source directory.
build-backend:
	@$(call require_dotnet)
	cd src/backend && dotnet build DogPhoto.sln

build-frontend:
	docker compose exec -T -w /app frontend npm run build

# Recover from earlier in-container builds that left root-owned bin/obj.
fix-backend-perms:
	sudo chown -R "$$USER:$$USER" src/backend

# ── Backend tests ──────────────────────────────────────────────────────

# Unit + arch tests run on the host (same reason as build-backend).
test-backend-unit:
	@$(call require_dotnet)
	cd src/backend && dotnet test tests/DogPhoto.ArchTests/DogPhoto.ArchTests.csproj --nologo

# Integration tests use Testcontainers and need a real Docker socket, so they
# run on the host where docker is available.
test-backend-integration:
	@$(call require_dotnet)
	cd src/backend && dotnet test tests/DogPhoto.IntegrationTests/DogPhoto.IntegrationTests.csproj --nologo

test-backend: test-backend-unit test-backend-integration

# ── Frontend tests ─────────────────────────────────────────────────────

# Vitest can run either in the container (uses the volume's node_modules) or
# on the host (uses src/frontend/node_modules). Container is the default
# because it doesn't require any host install.
test-frontend-unit:
	docker compose exec -T -w /app frontend npm test

test-frontend: test-frontend-unit

# Playwright runs on the host because the frontend container is alpine
# (Playwright doesn't ship browser binaries for alpine). Requires `npm install`
# to have been run in src/frontend/ on the host first.
playwright-install:
	@$(call require_node)
	@if [ ! -d src/frontend/node_modules ]; then \
	  echo "Installing host node_modules in src/frontend..."; \
	  cd src/frontend && npm install; \
	fi
	cd src/frontend && npx playwright install --with-deps chromium

test-e2e:
	@$(call require_node)
	@if [ ! -x src/frontend/node_modules/.bin/playwright ]; then \
	  echo "ERROR: Playwright not installed on host. Run 'make playwright-install' first."; \
	  exit 1; \
	fi
	cd src/frontend && npm run test:e2e

# ── Aggregate ──────────────────────────────────────────────────────────

test: test-backend-unit test-frontend-unit
	@echo ""
	@echo "Unit + arch tests passed."
	@echo "Run 'make test-backend-integration' (needs host dotnet + docker) and"
	@echo "'make test-e2e' (needs host node + 'make playwright-install') for the full suite."

# ── Internal helpers ───────────────────────────────────────────────────

define require_dotnet
	if ! command -v dotnet >/dev/null 2>&1; then \
	  echo "ERROR: 'dotnet' not found on host. Install .NET 10 SDK first."; \
	  exit 1; \
	fi
endef

define require_node
	if ! command -v node >/dev/null 2>&1; then \
	  echo "ERROR: 'node' not found on host. Use nvm to install Node 22 first (nvm install 22 && nvm use 22)."; \
	  exit 1; \
	fi
endef
