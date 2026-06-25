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
#   2. Creates the medium tags (film, digital) + thematic tags
#   3. Uploads img/1.jpeg .. img/14.jpeg via /api/image-pipeline/upload
#        - Each dog photo gets exactly ONE medium tag (film | digital) plus
#          thematic tags. The medium tag drives the Film/Digital split that
#          the home page and portfolio render.
#   4. Waits for background processing (variants generated)
#   5. Publishes each photo and assigns its tags
#   6. Creates three dog collections:
#        - "utulkaci" (Útulkáči / Shelter Dogs)
#        - "turisti"  (Turisti  / Hikers)
#        - "portrety" (Portréty / Portraits)
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
# Medium tags — drive the Film / Digital split on the home page and portfolio.
create_tag "film"       "Film"        "Film"
create_tag "digital"    "Digitál"     "Digital"
# Thematic tags — shared vocabulary for filtering and related photos.
create_tag "outdoor"    "Vonku"       "Outdoor"
create_tag "nature"     "Príroda"     "Nature"
create_tag "park"       "Park"        "Park"
create_tag "studio"     "Štúdio"      "Studio"
create_tag "training"   "Tréning"     "Training"
create_tag "action"     "Akcia"       "Action"
create_tag "bw"         "Čiernobiela" "Black & White"
create_tag "bratislava" "Bratislava"  "Bratislava"

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
# $3 is a comma-separated list of tag slugs, e.g. "film,street,analog".
publish_and_tag() {
  local id="$1" slug="$2" tags_csv="$3" sort_order="$4"
  local tags_json
  tags_json=$(jq -c -n --arg csv "$tags_csv" '$csv | split(",")')
  local resp status
  resp=$(curl -sS -o - -w '\n%{http_code}' -X PUT "$API_URL/api/portfolio/photos/$id" \
    -H "$AUTH_HEADER" \
    -H "Content-Type: application/json" \
    -d "{\"isPublished\":true,\"sortOrder\":$sort_order,\"tagSlugs\":$tags_json}")
  status=$(http_status "$resp")
  [[ "$status" == "200" ]] || fail "publish '$slug' failed (HTTP $status): $(http_body "$resp")"
}

# ── Bilingual metadata for the 14 dog images ──────────────────────────
# Index → slug, sk title, en title, sk alt, en alt, sk desc, en desc
declare -a META=(
  "utulkac-1|Útulkáč I|Shelter Dog I|Ateliérový portrét psa z útulku|Studio portrait of a shelter dog|Psík z bratislavského útulku v ateliéri.|A dog from a Bratislava shelter, photographed in the studio."
  "utulkac-2|Útulkáč II|Shelter Dog II|Pes z útulku na filmovej fotografii|Shelter dog on film|Záber psa z útulku zachytený na film.|A shelter dog captured on film."
  "utulkac-3|Útulkáč III|Shelter Dog III|Pes z útulku vonku|Shelter dog outdoors|Psík z útulku na prechádzke vonku.|A shelter dog out on a walk."
  "utulkac-4|Útulkáč IV|Shelter Dog IV|Čiernobiely portrét psa z útulku|Black-and-white shelter dog portrait|Čiernobiely filmový portrét psa z útulku.|A black-and-white film portrait of a shelter dog."
  "turista-1|Turista I|Hiker I|Pes na horskom chodníku|Dog on a mountain trail|Štvornohý turista na chodníku v prírode pri Bratislave.|A four-legged hiker on a trail in the nature around Bratislava."
  "turista-2|Turista II|Hiker II|Pes v prírode na filme|Dog in nature on film|Pes v prírode zachytený na film.|A dog in nature captured on film."
  "turista-3|Turista III|Hiker III|Bežiaci pes na lesnom chodníku|Dog running on a forest trail|Pes v plnom behu na lesnom chodníku.|A dog at full sprint on a forest trail."
  "turista-4|Turista IV|Hiker IV|Pes na prechádzke v parku|Dog on a walk in the park|Prechádzka so psom v bratislavskom parku, na film.|A walk with a dog in a Bratislava park, on film."
  "turista-5|Turista V|Hiker V|Tréning psa vonku|Dog training outdoors|Tréningové fotenie psa v prírode.|An outdoor dog training session."
  "portret-1|Portrét I|Portrait I|Ateliérový portrét psa na film|Studio dog portrait on film|Ateliérový portrét psa zachytený na film.|A studio dog portrait captured on film."
  "portret-2|Portrét II|Portrait II|Digitálny ateliérový portrét psa|Digital studio dog portrait|Digitálny ateliérový portrét psa.|A digital studio portrait of a dog."
  "portret-3|Portrét III|Portrait III|Čiernobiely portrét psa|Black-and-white dog portrait|Čiernobiely filmový portrét psa.|A black-and-white film portrait of a dog."
  "portret-4|Portrét IV|Portrait IV|Portrét psa v meste|Dog portrait in the city|Portrét psa v uliciach Bratislavy.|A dog portrait in the streets of Bratislava."
  "portret-5|Portrét V|Portrait V|Tréningový portrét psa na film|Training dog portrait on film|Portrét psa počas tréningu, na film.|A portrait of a dog during a training session, on film."
)

# Per-photo tags (index 1..14). Every photo carries exactly ONE medium tag
# (film | digital) — that is what the Film/Digital split on the home page and
# portfolio filters on — plus thematic tags from the shared vocabulary.
declare -a PHOTO_TAGS=(
  "digital,studio,bratislava"  # 1  utulkac-1
  "film,bratislava"            # 2  utulkac-2
  "digital,outdoor"            # 3  utulkac-3
  "film,bw"                    # 4  utulkac-4
  "digital,outdoor,nature"     # 5  turista-1
  "film,nature"                # 6  turista-2
  "digital,action,nature"      # 7  turista-3
  "film,park"                  # 8  turista-4
  "digital,outdoor,training"   # 9  turista-5
  "film,studio"                # 10 portret-1
  "digital,studio"             # 11 portret-2
  "film,bw"                    # 12 portret-3
  "digital,bratislava"         # 13 portret-4
  "film,training"              # 14 portret-5
)

# Collection membership, by 1-based photo index.
UTULKACI_INDICES=(1 2 3 4)
TURISTI_INDICES=(5 6 7 8 9)
PORTRETY_INDICES=(10 11 12 13 14)

declare -A PHOTO_ID_BY_INDEX=()

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

  publish_and_tag "$id" "$slug" "${PHOTO_TAGS[$((i-1))]}" "$i"
  PHOTO_ID_BY_INDEX[$i]="$id"
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

# Gather the recovered photo ids for a set of indices into the global IDS array.
collect_ids() {
  IDS=()
  local idx
  for idx in "$@"; do
    [[ -n "${PHOTO_ID_BY_INDEX[$idx]:-}" ]] && IDS+=("${PHOTO_ID_BY_INDEX[$idx]}")
  done
}

collect_ids "${UTULKACI_INDICES[@]}"
if (( ${#IDS[@]} > 0 )); then
  create_collection "utulkaci" \
    "Útulkáči" "Shelter Dogs" \
    "Psy z bratislavských útulkov, ktoré hľadajú domov." \
    "Dogs from Bratislava shelters looking for a home." \
    "${IDS[@]}"
fi

collect_ids "${TURISTI_INDICES[@]}"
if (( ${#IDS[@]} > 0 )); then
  create_collection "turisti" \
    "Turisti" "Hikers" \
    "Štvornohí parťáci na túrach v prírode okolo Bratislavy." \
    "Four-legged companions on hikes in the nature around Bratislava." \
    "${IDS[@]}"
fi

collect_ids "${PORTRETY_INDICES[@]}"
if (( ${#IDS[@]} > 0 )); then
  create_collection "portrety" \
    "Portréty" "Portraits" \
    "Charakterové portréty psov v ateliéri aj v exteriéri." \
    "Character portraits of dogs, in the studio and outdoors." \
    "${IDS[@]}"
fi

log "Done. Visit http://localhost:4321/sk/portfolio to see the seeded gallery."
