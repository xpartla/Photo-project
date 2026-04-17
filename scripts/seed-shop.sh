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

# ── Helper: create product with variants ──────────────────────────────
# Usage: create_product <slug> <title_sk> <title_en> <desc_sk> <desc_en>
#                      <is_limited:true|false> <edition_size|null>
#                      <photo_slug> <variants_json>
# where variants_json is a JSON array of
#   [{"formatCode":"a4","paperTypeCode":"fine-art-310","price":45}, ...]
create_product() {
  local slug=$1
  local title_sk=$2
  local title_en=$3
  local desc_sk=$4
  local desc_en=$5
  local is_limited=$6
  local edition=$7
  local photo_slug=$8
  local variants_json=$9

  log "Creating product: $slug"

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
      \"isLimitedEdition\": $is_limited,
      \"editionSize\": $edition_json,
      \"photoId\": $photo_id,
      \"isAvailable\": true,
      \"variants\": $variants_json
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

# Bratislava 2026 — limited-edition street frames
create_product \
  "bratislava-film-1" \
  "Bratislava I — Limitovaná edícia" \
  "Bratislava I — Limited Edition" \
  "Uličný záber Bratislavy na 35mm film. Limitovaná edícia 10 kusov." \
  "A Bratislava street frame on 35mm film. Limited edition of 10." \
  true \
  10 \
  "film-frame-1" \
  '[{"formatCode":"30x40","paperTypeCode":"fine-art-310","price":89}]'

create_product \
  "bratislava-film-2" \
  "Bratislava II — Limitovaná edícia" \
  "Bratislava II — Limited Edition" \
  "Mestská atmosféra Bratislavy zachytená na film. Limitovaná edícia 15 kusov." \
  "Bratislava city atmosphere captured on film. Limited edition of 15." \
  true \
  15 \
  "film-frame-2" \
  '[{"formatCode":"40x60","paperTypeCode":"fine-art-310","price":120}]'

# The Alps — baryta paper, medium edition
create_product \
  "alps-landscape-1" \
  "Alpy I — Limitovaná edícia" \
  "The Alps I — Limited Edition" \
  "Alpská krajina na 35mm filme a bryte. Limitovaná edícia 20 kusov." \
  "Alpine landscape on 35mm film and baryta. Limited edition of 20." \
  true \
  20 \
  "film-frame-6" \
  '[{"formatCode":"30x40","paperTypeCode":"baryta","price":75}]'

# Wildlife in motion — open edition with multiple variants (drives dropdowns)
create_product \
  "wildlife-motion-1" \
  "Divočina v pohybe — Otvorená edícia" \
  "Wildlife in Motion — Open Edition" \
  "Filmový portrét v pohybe. Vyberte si formát a papier podľa potreby." \
  "A film portrait in motion. Pick the format and paper you prefer." \
  false \
  null \
  "film-frame-3" \
  '[
    {"formatCode":"a4","paperTypeCode":"glossy-premium","price":45},
    {"formatCode":"a4","paperTypeCode":"fine-art-310","price":55},
    {"formatCode":"a3","paperTypeCode":"glossy-premium","price":65},
    {"formatCode":"a3","paperTypeCode":"fine-art-310","price":79},
    {"formatCode":"30x40","paperTypeCode":"fine-art-310","price":89}
  ]'

# ── Summary ──────────────────────────────────────────────────────────
log ""
log "Done! You can now test the shop flow:"
log "  1. Browse http://localhost:4321/sk/obchod"
log "  2. Click a product → Select variant (open editions) → Add to cart"
log "  3. Go to cart → Proceed to checkout"
log "  4. Fill address form → Pay now → Confirm mock payment"
log "  5. Check Mailpit at http://localhost:8025 for order emails"
