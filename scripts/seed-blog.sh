#!/usr/bin/env bash
#
# scripts/seed-blog.sh
#
# Seeds three bilingual Markdown blog posts plus a handful of tags so you can
# manually exercise the blog listing, filters, category/tag archives, and
# admin CMS flows.
#
# Prerequisites:
#   - docker compose up   (db, api running; admin + customer users auto-seeded on startup)
#   - curl, jq installed locally
#
# Usage:
#   ./scripts/seed-blog.sh
#
# Idempotent — existing tags/posts are detected via HTTP 409 and skipped.

set -euo pipefail

API_URL="${API_URL:-http://localhost:5000}"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@dogphoto.sk}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-admin123}"

log()  { printf '\033[1;36m[seed-blog]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[seed-blog]\033[0m %s\n' "$*" >&2; }
fail() { printf '\033[1;31m[seed-blog]\033[0m %s\n' "$*" >&2; exit 1; }

command -v curl >/dev/null || fail "curl is required"
command -v jq   >/dev/null || fail "jq is required"

log "Waiting for API at $API_URL..."
for i in {1..30}; do
  if curl -fsS "$API_URL/health/ready" >/dev/null 2>&1; then
    log "API is ready"
    break
  fi
  if [[ $i -eq 30 ]]; then fail "API did not become ready within 30 seconds"; fi
  sleep 1
done

log "Logging in as $ADMIN_EMAIL..."
LOGIN_RESPONSE=$(curl -sS -X POST "$API_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}")
TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.accessToken // empty')
[[ -n "$TOKEN" ]] || fail "Login failed: $LOGIN_RESPONSE"
AUTH="Authorization: Bearer $TOKEN"
log "Authenticated"

# ── Helpers ────────────────────────────────────────────────────────────

create_tag() {
  local slug=$1 name_sk=$2 name_en=$3
  local code
  code=$(curl -sS -o /tmp/seed-blog-response.json -w "%{http_code}" \
    -X POST "$API_URL/api/blog/tags" \
    -H "Content-Type: application/json" -H "$AUTH" \
    -d "{\"nameSk\":\"$name_sk\",\"nameEn\":\"$name_en\",\"slug\":\"$slug\"}")
  case "$code" in
    201) log "  Tag created: $slug" ;;
    409) log "  Tag exists:  $slug (skipped)" ;;
    *)   warn "  Tag failed ($code) for $slug: $(cat /tmp/seed-blog-response.json)" ;;
  esac
}

# create_post <slug> <json-payload-file>
create_post() {
  local slug=$1 payload_file=$2
  local code
  code=$(curl -sS -o /tmp/seed-blog-response.json -w "%{http_code}" \
    -X POST "$API_URL/api/blog/posts" \
    -H "Content-Type: application/json" -H "$AUTH" \
    --data @"$payload_file")
  case "$code" in
    201) log "  Post created: $slug" ;;
    409) log "  Post exists:  $slug (skipped)" ;;
    *)   warn "  Post failed ($code) for $slug: $(cat /tmp/seed-blog-response.json)" ;;
  esac
}

# ── Tags ──────────────────────────────────────────────────────────────
log ""
log "Creating blog tags..."
create_tag "film"        "film"          "film"
create_tag "35mm"        "35mm"          "35mm"
create_tag "dogs"        "psy"           "dogs"
create_tag "portrait"    "portrét"       "portrait"
create_tag "bratislava"  "bratislava"    "bratislava"
create_tag "tips"        "tipy"          "tips"

# ── Posts ─────────────────────────────────────────────────────────────
log ""
log "Creating blog posts..."

# Post 1 — Behind the scenes
cat > /tmp/seed-blog-post-1.json <<'JSON'
{
  "slug": "why-i-shoot-on-35mm-film",
  "titleSk": "Prečo fotografujem na 35mm film",
  "titleEn": "Why I Shoot on 35mm Film",
  "excerptSk": "Príbeh o tom, prečo som sa vrátil k analógu po desaťročí v digitále — a čo mi to dalo.",
  "excerptEn": "A short story about why I returned to analog after a decade in digital — and what it gave me.",
  "contentMarkdownSk": "# Návrat k filmu\n\nPamätám si prvýkrát, keď som vložil rolku **Portra 400** do starého Pentaxu. Bol to pocit, ktorý sa v digitále nedá nájsť: každá snímka musí niečo znamenať. Tridsaťšesť záberov. Potom koniec.\n\n## Čo mi film dal\n\n- **Trpezlivosť.** Počkáš si, kým niečo naozaj cítiš.\n- **Teplo.** Grain, tónovanie, farby — to nie je nostalgia, je to textúra.\n- **Pokoru.** Polovica snímok nie je dobrá. A to je v poriadku.\n\n## Čo používam\n\nPentax K1000, Zeiss 50mm f/1.4, Kodak Portra 400 a občas Ilford HP5+ pre čiernobiele.\n\n> „Dobrá fotografia vie, kde stáť.\" — Ansel Adams\n\nKeď mi niekto povie „ale veď to je pomalé\", odpoviem: presne o to ide.",
  "contentMarkdownEn": "# Back to Film\n\nI still remember the first time I loaded a roll of **Portra 400** into an old Pentax. It's a feeling digital can't replicate: every frame has to mean something. Thirty-six shots. Then done.\n\n## What film gave me\n\n- **Patience.** You wait until you actually feel something.\n- **Warmth.** Grain, tone, colour — it isn't nostalgia, it's texture.\n- **Humility.** Half the frames aren't good. That's fine.\n\n## What I use\n\nPentax K1000, Zeiss 50mm f/1.4, Kodak Portra 400, and sometimes Ilford HP5+ for black & white.\n\n> \"A good photograph is knowing where to stand.\" — Ansel Adams\n\nWhen people say \"but it's slow\", I say: that is exactly the point.",
  "author": "Adam Partl",
  "status": "Published",
  "categorySlugs": ["behind-the-scenes"],
  "tagSlugs": ["film", "35mm"],
  "metaTitleSk": "Prečo fotografujem na 35mm film — PartlPhoto",
  "metaTitleEn": "Why I Shoot on 35mm Film — PartlPhoto",
  "metaDescriptionSk": "Osobná úvaha o analógovej fotografii, trpezlivosti a textúre filmu.",
  "metaDescriptionEn": "A personal reflection on analog photography, patience, and the texture of film."
}
JSON
create_post "why-i-shoot-on-35mm-film" /tmp/seed-blog-post-1.json

# Post 2 — Dog portrait tips
cat > /tmp/seed-blog-post-2.json <<'JSON'
{
  "slug": "dog-portrait-tips-first-timers",
  "titleSk": "Psie portréty: päť tipov pre začiatočníkov",
  "titleEn": "Dog Portraits: Five Tips for First-Timers",
  "excerptSk": "Praktické rady ako pripraviť psa (a seba) na fotenie, aby to nebol stres — ale radosť.",
  "excerptEn": "Practical advice on how to prep your dog (and yourself) for a session so it's joy, not stress.",
  "contentMarkdownSk": "# Päť vecí, ktoré sa naučíš pri prvom fotení\n\nPsie portréty sú o trpezlivosti a svetle. Nie o vybavení.\n\n## 1. Unav ho — ale nie úplne\n\nKrátka prechádzka pred fotením zníži úzkosť. Ale nič, čo by z neho urobilo mokrý handričku.\n\n## 2. Nájdi svetlo, nie pozíciu\n\nZlatá hodina je klišé z nejakého dôvodu. Postav sa chrbtom k slnku a nechaj psa otočiť sa k tebe.\n\n## 3. Pamlsky v nenápadnom vrecku\n\nPes nesmie vidieť pamlsok, iba ho cítiť — inak ti fotky skončia s nosom pri objektíve.\n\n## 4. Fotografuj z jeho úrovne\n\nKľakni si. Všetko vyzerá lepšie, keď sa kamera stretne s očami.\n\n## 5. Nestriehať — reagovať\n\nPes ti povie, kedy je čas skončiť. Respektuj to.\n\n## Bonus\n\nAk máš čierneho psa, expozíciu prelej o pol stopu do plus. Inak sa stratí v tieňoch.",
  "contentMarkdownEn": "# Five Things You'll Learn on Your First Shoot\n\nDog portraits are about patience and light. Not gear.\n\n## 1. Tire them out — a little\n\nA short walk before the shoot cuts anxiety. But not so long they turn into a wet rag.\n\n## 2. Find light, not a pose\n\nGolden hour is a cliché for a reason. Put your back to the sun and let the dog turn toward you.\n\n## 3. Treats in a hidden pocket\n\nThey should smell the treat, not see it — otherwise you'll shoot nothing but a snout pressed into the lens.\n\n## 4. Shoot at their level\n\nKneel. Everything looks better when the camera meets the eyes.\n\n## 5. Don't hunt — respond\n\nThe dog will tell you when they're done. Respect that.\n\n## Bonus\n\nBlack dogs: over-expose by about half a stop, or they'll disappear into the shadows.",
  "author": "Adam Partl",
  "status": "Published",
  "categorySlugs": ["photography-tips"],
  "tagSlugs": ["dogs", "portrait", "tips"],
  "metaTitleSk": "Psie portréty — päť tipov pre začiatočníkov",
  "metaTitleEn": "Dog Portraits — Five Tips for First-Timers",
  "metaDescriptionSk": "Praktické tipy ako odfotiť psa tak, aby to nebol stres.",
  "metaDescriptionEn": "Practical dog portrait tips that keep the session calm and the photos good."
}
JSON
create_post "dog-portrait-tips-first-timers" /tmp/seed-blog-post-2.json

# Post 3 — Bratislava locations
cat > /tmp/seed-blog-post-3.json <<'JSON'
{
  "slug": "best-photo-spots-bratislava",
  "titleSk": "Najlepšie miesta na fotenie v Bratislave",
  "titleEn": "The Best Photo Spots in Bratislava",
  "excerptSk": "Sprievodca lokáciami, kde sa dá fotografovať celoročne — od Devína po Staré Mesto.",
  "excerptEn": "A location guide you can shoot year-round — from Devín to the Old Town.",
  "contentMarkdownSk": "# Šesť miest, ktoré stoja za batožinu\n\nBratislava nie je veľká. To je jej výhoda.\n\n## 1. Devín\n\nRuiny hradu a ústie Moravy do Dunaja. Ráno je tam prázdno a vie to dodať dramatické svetlo.\n\n## 2. Staré Mesto — uličky za Michalskou bránou\n\nTeplé steny, úzka hĺbka. Skvelé pre portréty.\n\n## 3. Železná studnička\n\nLes, lavičky, hmla v zime. Zober psa.\n\n## 4. Kamzík\n\nVežu ignoruj, les ešte neignoruj. Dobré pre environmentálne portréty.\n\n## 5. Nábrežie — Eurovea\n\nModerné, čisté línie, Dunaj ako zrkadlo.\n\n## 6. Slavín\n\nPanoráma celého mesta. Najkrajšia pred búrkou.\n\n## Praktické\n\nVeľa miest má bezplatné parkovanie. Nechcem ti ich prezradiť všetky — nechaj si aspoň jeden vlastný objav.",
  "contentMarkdownEn": "# Six Spots Worth the Walk\n\nBratislava isn't large. That's its advantage.\n\n## 1. Devín\n\nCastle ruins where the Morava meets the Danube. Empty in the early morning, dramatic light.\n\n## 2. Old Town — the lanes behind Michael's Gate\n\nWarm walls, narrow depth. Great for portraits.\n\n## 3. Železná studnička\n\nForest, benches, fog in winter. Bring the dog.\n\n## 4. Kamzík\n\nIgnore the tower, don't ignore the forest. Good for environmental portraits.\n\n## 5. The Danube riverfront — Eurovea\n\nModern, clean lines, the river as a mirror.\n\n## 6. Slavín\n\nPanorama over the whole city. Most beautiful right before a storm.\n\n## Practical\n\nMost of these have free parking. I won't list them all — leave yourself at least one personal discovery.",
  "author": "Adam Partl",
  "status": "Published",
  "categorySlugs": ["locations"],
  "tagSlugs": ["bratislava", "tips"],
  "metaTitleSk": "Najlepšie miesta na fotenie v Bratislave — PartlPhoto",
  "metaTitleEn": "Best Photo Spots in Bratislava — PartlPhoto",
  "metaDescriptionSk": "Sprievodca lokáciami v Bratislave, kde sa dobre fotografuje.",
  "metaDescriptionEn": "A location guide for photographers shooting in and around Bratislava."
}
JSON
create_post "best-photo-spots-bratislava" /tmp/seed-blog-post-3.json

log ""
log "Done! Open http://localhost:4321/sk/blog (SK) or /en/blog (EN)."
log "Admin CMS: log in as $ADMIN_EMAIL and visit /sk/admin/blog."
log "Regular user for testing: customer@dogphoto.sk / customer123 (auto-seeded on startup)."
