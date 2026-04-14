#!/usr/bin/env bash
#
# scripts/seed-shop.sh
#
# Seeds shop products from existing portfolio photos so you can test
# the full shop flow: browse products → add to cart → checkout →
# mock payment → order confirmation + email in Mailpit.
#
# Prerequisites:
#   - docker compose up   (db, api running)
#   - scripts/seed-images.sh has been run first (photos must exist)
#   - curl, jq installed locally
#
# Usage:
#   ./scripts/seed-shop.sh
#
# Idempotent — skips products that already exist (HTTP 409).

set -euo pipefail

API_URL="${API_URL:-http://localhost:5000}"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@dogphoto.sk}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-admin123}"

# ── Pretty logging ────────────────────────────────────────────────────
log()  { printf '\033[1;36m[seed-shop]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[seed-shop]\033[0m %s\n' "$*" >&2; }
fail() { printf '\033[1;31m[seed-shop]\033[0m %s\n' "$*" >&2; exit 1; }

# ── Dependency checks ─────────────────────────────────────────────────
command -v curl >/dev/null || fail "curl is required"
command -v jq   >/dev/null || fail "jq is required"

# ── Wait for API to be ready ──────────────────────────────────────────
log "Waiting for API at $API_URL..."
for i in {1..30}; do
  if curl -fsS "$API_URL/health/ready" >/dev/null 2>&1; then
    log "API is ready"
    break
  fi
  if [[ $i -eq 30 ]]; then
    fail "API did not become ready within 30 seconds"
  fi
  sleep 1
done

# ── Authenticate as admin ────────────────────────────────────────────
log "Logging in as $ADMIN_EMAIL..."
LOGIN_RESPONSE=$(curl -sS -X POST "$API_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}")

TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.accessToken // empty')
[[ -n "$TOKEN" ]] || fail "Login failed: $LOGIN_RESPONSE"
log "Authenticated"

AUTH="Authorization: Bearer $TOKEN"

# ── Helper: get photo ID by slug ──────────────────────────────────────
get_photo_id() {
  local slug=$1
  local response
  response=$(curl -sS "$API_URL/api/portfolio/photos/$slug")
  echo "$response" | jq -r '.id // empty'
}

# ── Helper: create product ────────────────────────────────────────────
create_product() {
  local slug=$1
  local title_sk=$2
  local title_en=$3
  local desc_sk=$4
  local desc_en=$5
  local format=$6
  local paper=$7
  local price=$8
  local edition=$9
  local photo_slug=${10}

  log "Creating product: $slug"

  # Get photo ID
  local photo_id
  photo_id=$(get_photo_id "$photo_slug")
  if [[ -z "$photo_id" ]]; then
    warn "  Photo '$photo_slug' not found — creating product without photo link"
    photo_id="null"
  else
    photo_id="\"$photo_id\""
  fi

  local edition_json
  if [[ "$edition" == "null" ]]; then
    edition_json="null"
  else
    edition_json="$edition"
  fi

  local response
  local http_code
  http_code=$(curl -sS -o /tmp/seed-shop-response.json -w "%{http_code}" \
    -X POST "$API_URL/api/shop/products" \
    -H "Content-Type: application/json" \
    -H "$AUTH" \
    -d "{
      \"titleSk\": \"$title_sk\",
      \"titleEn\": \"$title_en\",
      \"slug\": \"$slug\",
      \"descriptionSk\": \"$desc_sk\",
      \"descriptionEn\": \"$desc_en\",
      \"format\": \"$format\",
      \"paperType\": \"$paper\",
      \"price\": $price,
      \"editionSize\": $edition_json,
      \"photoId\": $photo_id,
      \"isAvailable\": true
    }")

  if [[ "$http_code" == "201" ]]; then
    log "  Created: $slug"
  elif [[ "$http_code" == "409" ]]; then
    log "  Already exists: $slug (skipped)"
  else
    warn "  Failed ($http_code): $(cat /tmp/seed-shop-response.json)"
  fi
}

# ── Seed products ────────────────────────────────────────────────────
log ""
log "Creating shop products from seeded portfolio photos..."

create_product \
  "dog-portrait-1-print" \
  "Psí portrét — Limitovaná edícia" \
  "Dog Portrait — Limited Edition" \
  "Profesionálny portrét psa na fine art papieri. Limitovaná edícia 10 kusov." \
  "Professional dog portrait on fine art paper. Limited edition of 10." \
  "30x40 cm" \
  "Hahnemühle Photo Rag" \
  89 \
  10 \
  "dog-portrait-1"

create_product \
  "dog-portrait-2-print" \
  "Akčný záber — Limitovaná edícia" \
  "Action Shot — Limited Edition" \
  "Dynamický záber psa v pohybe. Limitovaná edícia 15 kusov." \
  "Dynamic shot of a dog in motion. Limited edition of 15." \
  "40x60 cm" \
  "Hahnemühle Photo Rag" \
  120 \
  15 \
  "dog-portrait-2"

create_product \
  "film-frame-1-print" \
  "Filmový záber — Limitovaná edícia" \
  "Film Frame — Limited Edition" \
  "Analógový filmový záber na archívnom papieri. Limitovaná edícia 25 kusov." \
  "Analog film frame on archival paper. Limited edition of 25." \
  "20x30 cm" \
  "Canson Baryta Photographique" \
  65 \
  25 \
  "film-frame-1"

create_product \
  "dog-portrait-3-print" \
  "Psí portrét III — Otvorená edícia" \
  "Dog Portrait III — Open Edition" \
  "Psí portrét v prírode. Otvorená edícia bez limitu." \
  "Dog portrait in nature. Open edition, unlimited." \
  "A4" \
  "Epson Premium Glossy" \
  45 \
  null \
  "dog-portrait-3"

# ── Summary ──────────────────────────────────────────────────────────
log ""
log "Done! You can now test the shop flow:"
log "  1. Browse http://localhost:4321/sk/obchod"
log "  2. Click a product → Add to cart"
log "  3. Go to cart → Proceed to checkout"
log "  4. Fill address form → Pay now → Confirm mock payment"
log "  5. Check Mailpit at http://localhost:8025 for order emails"
