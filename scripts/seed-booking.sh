#!/usr/bin/env bash
#
# scripts/seed-booking.sh
#
# Seeds availability slots for the next 4 Saturdays so you can manually test
# the full booking flow: browse sessions → pick a date → select a slot →
# fill the form → submit → see confirmation + email in Mailpit.
#
# Prerequisites:
#   - docker compose up   (db, api, mailpit running)
#   - curl, jq installed locally
#
# Usage:
#   ./scripts/seed-booking.sh
#
# Re-running is safe — duplicate slots will simply be added (the API does
# not enforce slot uniqueness), but this is fine for local dev/testing.

set -euo pipefail

API_URL="${API_URL:-http://localhost:5000}"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@dogphoto.sk}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-admin123}"

# ── Pretty logging ────────────────────────────────────────────────────
log()  { printf '\033[1;36m[seed-booking]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[seed-booking]\033[0m %s\n' "$*" >&2; }
fail() { printf '\033[1;31m[seed-booking]\033[0m %s\n' "$*" >&2; exit 1; }

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

# ── Calculate next 4 Saturdays from today ─────────────────────────────
get_next_saturdays() {
  local today
  today=$(date +%u)  # 1=Monday, 7=Sunday
  local days_until_sat=$(( (6 - today + 7) % 7 ))
  # If today is Saturday, start with next Saturday
  [[ $days_until_sat -eq 0 ]] && days_until_sat=7

  for i in 0 1 2 3; do
    local offset=$(( days_until_sat + i * 7 ))
    date -d "+${offset} days" +%Y-%m-%d 2>/dev/null || \
      date -v "+${offset}d" +%Y-%m-%d 2>/dev/null
  done
}

SATURDAYS=$(get_next_saturdays)
[[ -n "$SATURDAYS" ]] || fail "Could not calculate upcoming Saturdays"

# ── Seed availability slots ───────────────────────────────────────────
log "Creating availability slots for the next 4 Saturdays..."

for DATE in $SATURDAYS; do
  log "  $DATE — generating 90-min slots from 09:00 to 17:00..."

  RESPONSE=$(curl -sS -X POST "$API_URL/api/booking/availability" \
    -H "Content-Type: application/json" \
    -H "$AUTH" \
    -d "{
      \"date\": \"$DATE\",
      \"startTime\": \"09:00\",
      \"endTime\": \"17:00\",
      \"slotDurationMinutes\": 90,
      \"breakMinutes\": 30
    }")

  COUNT=$(echo "$RESPONSE" | jq -r '.count // "error"')
  if [[ "$COUNT" == "error" ]]; then
    warn "  Failed for $DATE: $RESPONSE"
  else
    log "  Created $COUNT slots"
  fi
done

# ── Also add a few weekday slots for variety ──────────────────────────
TOMORROW=$(date -d "+1 day" +%Y-%m-%d 2>/dev/null || date -v "+1d" +%Y-%m-%d 2>/dev/null)
DAY_AFTER=$(date -d "+2 days" +%Y-%m-%d 2>/dev/null || date -v "+2d" +%Y-%m-%d 2>/dev/null)

for DATE in $TOMORROW $DAY_AFTER; do
  log "  $DATE — adding afternoon slots (14:00-18:00)..."

  RESPONSE=$(curl -sS -X POST "$API_URL/api/booking/availability" \
    -H "Content-Type: application/json" \
    -H "$AUTH" \
    -d "{
      \"date\": \"$DATE\",
      \"startTime\": \"14:00\",
      \"endTime\": \"18:00\",
      \"slotDurationMinutes\": 90,
      \"breakMinutes\": 30
    }")

  COUNT=$(echo "$RESPONSE" | jq -r '.count // "error"')
  if [[ "$COUNT" == "error" ]]; then
    warn "  Failed for $DATE: $RESPONSE"
  else
    log "  Created $COUNT slots"
  fi
done

# ── Summary ───────────────────────────────────────────────────────────
log ""
log "Done! You can now test the booking flow:"
log "  1. Browse http://localhost:4321/sk/rezervacia"
log "  2. Click a session → Book this session"
log "  3. Pick a highlighted date → select a time slot"
log "  4. Fill the form and submit"
log "  5. Check Mailpit at http://localhost:8025 for the confirmation email"
