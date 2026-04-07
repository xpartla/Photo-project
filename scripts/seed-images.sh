#!/usr/bin/env bash
#
# scripts/seed-images.sh
#
# Seeds the local PartlPhoto stack with the 14 sample images in img/.
#
# Prerequisites:
#   - docker compose up   (db, api, frontend, azurite, mailpit running)
#   - curl, jq installed locally
#
# What it does:
#   1. Logs in as the seeded admin user (admin@dogphoto.sk / admin123)
#   2. Creates the `dog` and `film` tags
#   3. Uploads img/1.jpeg .. img/14.jpeg via /api/image-pipeline/upload
#        - Images 1–7  → tagged `dog`
#        - Images 8–14 → tagged `film`
#   4. Waits for background processing (variants generated)
#   5. Publishes each photo and assigns its tag
#   6. Creates two collections:
#        - "dog-portraits"   (Psie portréty / Dog Portraits)
#        - "film-collection" (Filmová kolekcia / Film Collection)
#
# Re-running the script is safe: existing photos / tags / collections are
# detected and reused (HTTP 409 from the API is treated as "already exists").

set -euo pipefail

API_URL="${API_URL:-http://localhost:5000}"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@dogphoto.sk}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-admin123}"
IMG_DIR="${IMG_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/img}"

# ── Pretty logging ────────────────────────────────────────────────────
log()  { printf '\033[1;36m[seed]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[seed]\033[0m %s\n' "$*" >&2; }
fail() { printf '\033[1;31m[seed]\033[0m %s\n' "$*" >&2; exit 1; }

# ── Dependency checks ─────────────────────────────────────────────────
command -v curl >/dev/null || fail "curl is required"
command -v jq   >/dev/null || fail "jq is required"
[[ -d "$IMG_DIR" ]] || fail "image directory not found: $IMG_DIR"

# ── Wait for API to be ready ──────────────────────────────────────────
log "Waiting for API at $API_URL..."
for i in {1..30}; do
  if curl -fsS "$API_URL/health/ready" >/dev/null 2>&1; then
    log "API is ready"
    break
  fi
  if [[ $i -eq 30 ]]; then
    fail "API at $API_URL did not become ready in 60s — is docker compose up?"
  fi
  sleep 2
done

# ── Login ─────────────────────────────────────────────────────────────
log "Logging in as $ADMIN_EMAIL..."
LOGIN_RESPONSE=$(curl -fsS -X POST "$API_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}") \
  || fail "admin login failed — is the database seeded?"

TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.accessToken')
[[ "$TOKEN" != "null" && -n "$TOKEN" ]] || fail "no access token in login response"
AUTH_HEADER="Authorization: Bearer $TOKEN"

# ── Helper: HTTP status from response ─────────────────────────────────
http_status() { tail -n1 <<<"$1"; }
http_body()   { sed '$d' <<<"$1"; }

# ── Helper: create tag (idempotent) ───────────────────────────────────
create_tag() {
  local slug="$1" name_sk="$2" name_en="$3"
  local resp status
  resp=$(curl -sS -o - -w '\n%{http_code}' -X POST "$API_URL/api/portfolio/tags" \
    -H "$AUTH_HEADER" \
    -H "Content-Type: application/json" \
    -d "{\"slug\":\"$slug\",\"nameSk\":\"$name_sk\",\"nameEn\":\"$name_en\"}")
  status=$(http_status "$resp")
  case "$status" in
    201) log "  + created tag '$slug'" ;;
    409) log "  · tag '$slug' already exists" ;;
    *)   fail "create tag '$slug' failed (HTTP $status): $(http_body "$resp")" ;;
  esac
}

log "Creating tags..."
create_tag "dog"  "Pes"  "Dog"
create_tag "film" "Film" "Film"

# ── Helper: upload one image ──────────────────────────────────────────
# Args: file_path slug title_sk title_en alt_sk alt_en desc_sk desc_en
# Echoes the photo id on success (whether newly uploaded or already existed).
upload_image() {
  local file="$1" slug="$2" title_sk="$3" title_en="$4"
  local alt_sk="$5" alt_en="$6" desc_sk="$7" desc_en="$8"

  local resp status body
  resp=$(curl -sS -o - -w '\n%{http_code}' -X POST "$API_URL/api/image-pipeline/upload" \
    -H "$AUTH_HEADER" \
    -F "image=@$file;type=image/jpeg" \
    -F "slug=$slug" \
    -F "titleSk=$title_sk" \
    -F "titleEn=$title_en" \
    -F "altTextSk=$alt_sk" \
    -F "altTextEn=$alt_en" \
    -F "descriptionSk=$desc_sk" \
    -F "descriptionEn=$desc_en")
  status=$(http_status "$resp")
  body=$(http_body "$resp")

  case "$status" in
    201)
      echo "$body" | jq -r '.id'
      ;;
    409)
      # Already uploaded — look up the existing id by slug.
      # The public detail endpoint requires the photo to be published, so we
      # fall back to listing tags and using the admin-only photo list via the
      # public photos endpoint with a generous page size, filtered by slug
      # client-side.
      local existing
      existing=$(curl -fsS "$API_URL/api/portfolio/photos?size=100" \
        | jq -r ".items[] | select(.slug==\"$slug\") | .id")
      if [[ -z "$existing" ]]; then
        # Photo exists but isn't published yet — admin status endpoint also
        # requires id, so as a last resort scan via the database isn't
        # possible from a shell script. Bail out with a helpful message.
        warn "  ! photo '$slug' exists but is unpublished and the script cannot recover its id; please reset the DB volume or publish it manually"
        echo ""
      else
        echo "$existing"
      fi
      ;;
    *)
      fail "upload of '$slug' failed (HTTP $status): $body"
      ;;
  esac
}

# ── Helper: poll until processing complete ───────────────────────────
wait_for_processing() {
  local id="$1" slug="$2"
  for _ in {1..60}; do
    local resp status
    resp=$(curl -sS -o - -w '\n%{http_code}' \
      -H "$AUTH_HEADER" \
      "$API_URL/api/image-pipeline/photos/$id/status")
    status=$(http_status "$resp")
    if [[ "$status" == "200" ]]; then
      local processed
      processed=$(http_body "$resp" | jq -r '.isProcessed')
      if [[ "$processed" == "true" ]]; then
        return 0
      fi
    fi
    sleep 1
  done
  fail "image '$slug' did not finish processing within 60s"
}

# ── Helper: publish + tag a photo ─────────────────────────────────────
publish_and_tag() {
  local id="$1" slug="$2" tag="$3" sort_order="$4"
  local resp status
  resp=$(curl -sS -o - -w '\n%{http_code}' -X PUT "$API_URL/api/portfolio/photos/$id" \
    -H "$AUTH_HEADER" \
    -H "Content-Type: application/json" \
    -d "{\"isPublished\":true,\"sortOrder\":$sort_order,\"tagSlugs\":[\"$tag\"]}")
  status=$(http_status "$resp")
  [[ "$status" == "200" ]] || fail "publish '$slug' failed (HTTP $status): $(http_body "$resp")"
}

# ── Bilingual metadata for the 14 images ──────────────────────────────
# Index → slug, sk title, en title, sk alt, en alt, sk desc, en desc
declare -a META=(
  "dog-portrait-1|Psí portrét I|Dog Portrait I|Detailný portrét psa|Close-up dog portrait|Detailný portrét psa v prirodzenom svetle.|Close-up portrait of a dog in natural light."
  "dog-portrait-2|Psí portrét II|Dog Portrait II|Pes pri pohľade do diaľky|Dog gazing into the distance|Pes pri pohľade do diaľky, vintage tóny.|Dog gazing into the distance with vintage tones."
  "dog-portrait-3|Psí portrét III|Dog Portrait III|Štúdiová fotografia psa|Studio photograph of a dog|Štúdiová fotografia psa, jemné svetlo.|Studio photograph of a dog with soft lighting."
  "dog-portrait-4|Psí portrét IV|Dog Portrait IV|Hravý moment psa|Playful moment of a dog|Hravý moment psa zachytený v exteriéri.|Playful moment of a dog captured outdoors."
  "dog-portrait-5|Psí portrét V|Dog Portrait V|Pes v zlatej hodine|Dog in golden hour|Portrét psa pri zapadajúcom slnku.|Portrait of a dog at sunset, golden hour light."
  "dog-portrait-6|Psí portrét VI|Dog Portrait VI|Pes na prechádzke|Dog on a walk|Pes počas prechádzky v Bratislave.|A dog on a walk in Bratislava."
  "dog-portrait-7|Psí portrét VII|Dog Portrait VII|Verný spoločník|A loyal companion|Portrét verného štvornohého spoločníka.|Portrait of a loyal four-legged companion."
  "film-frame-1|Filmový záber I|Film Frame I|Záber na 35mm film|35mm film frame|Krajinný záber zachytený na 35mm film.|Landscape shot captured on 35mm film."
  "film-frame-2|Filmový záber II|Film Frame II|Atmosféra mesta na filme|City atmosphere on film|Atmosféra mesta zachytená na film.|City atmosphere captured on film."
  "film-frame-3|Filmový záber III|Film Frame III|Analógová fotografia|Analog photograph|Analógová fotografia s jemným zrnom.|Analog photograph with soft grain."
  "film-frame-4|Filmový záber IV|Film Frame IV|Filmový portrét|Film portrait|Portrét vyfotografovaný na film.|Portrait photographed on film."
  "film-frame-5|Filmový záber V|Film Frame V|Pohľad cez objektív|Through the lens|Pohľad na svet cez analógový objektív.|A view of the world through an analog lens."
  "film-frame-6|Filmový záber VI|Film Frame VI|Filmová krajina|Film landscape|Krajinná fotografia na 35mm film.|Landscape photograph on 35mm film."
  "film-frame-7|Filmový záber VII|Film Frame VII|Vintage atmosféra|Vintage atmosphere|Záber s typickou vintage atmosférou filmu.|A frame with the signature vintage atmosphere of film."
)

declare -a DOG_IDS=()
declare -a FILM_IDS=()

log "Uploading and processing images..."
for i in $(seq 1 14); do
  file="$IMG_DIR/$i.jpeg"
  [[ -f "$file" ]] || fail "missing image: $file"

  IFS='|' read -r slug title_sk title_en alt_sk alt_en desc_sk desc_en <<<"${META[$((i-1))]}"
  log "  → [$i/14] $slug"

  id=$(upload_image "$file" "$slug" "$title_sk" "$title_en" "$alt_sk" "$alt_en" "$desc_sk" "$desc_en")
  if [[ -z "$id" ]]; then
    warn "    skipped (cannot recover id)"
    continue
  fi

  wait_for_processing "$id" "$slug"

  if [[ $i -le 7 ]]; then
    publish_and_tag "$id" "$slug" "dog" "$i"
    DOG_IDS+=("$id")
  else
    publish_and_tag "$id" "$slug" "film" "$i"
    FILM_IDS+=("$id")
  fi
done

# ── Helper: create collection (idempotent) ────────────────────────────
create_collection() {
  local slug="$1" name_sk="$2" name_en="$3" desc_sk="$4" desc_en="$5"
  shift 5
  local ids_json
  ids_json=$(printf '"%s",' "$@" | sed 's/,$//')

  local payload
  payload=$(jq -n \
    --arg slug "$slug" \
    --arg name_sk "$name_sk" \
    --arg name_en "$name_en" \
    --arg desc_sk "$desc_sk" \
    --arg desc_en "$desc_en" \
    --argjson photo_ids "[$ids_json]" \
    '{slug:$slug,nameSk:$name_sk,nameEn:$name_en,descriptionSk:$desc_sk,descriptionEn:$desc_en,photoIds:$photo_ids}')

  local resp status
  resp=$(curl -sS -o - -w '\n%{http_code}' -X POST "$API_URL/api/portfolio/collections" \
    -H "$AUTH_HEADER" \
    -H "Content-Type: application/json" \
    -d "$payload")
  status=$(http_status "$resp")
  case "$status" in
    201) log "  + created collection '$slug'" ;;
    409) log "  · collection '$slug' already exists" ;;
    *)   fail "create collection '$slug' failed (HTTP $status): $(http_body "$resp")" ;;
  esac
}

log "Creating collections..."
if (( ${#DOG_IDS[@]} > 0 )); then
  create_collection "dog-portraits" \
    "Psie portréty" "Dog Portraits" \
    "Kolekcia psích portrétov." "A collection of dog portraits." \
    "${DOG_IDS[@]}"
fi
if (( ${#FILM_IDS[@]} > 0 )); then
  create_collection "film-collection" \
    "Filmová kolekcia" "Film Collection" \
    "Analógové zábery na 35mm film." "Analog frames on 35mm film." \
    "${FILM_IDS[@]}"
fi

log "Done. Visit http://localhost:4321/sk/portfolio to see the seeded gallery."
