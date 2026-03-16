# PartlPhoto — Design Style Guide

## Brand Identity

**PartlPhoto** is a fine art 35mm film & dog photography brand based in Bratislava, Slovakia. The visual identity blends **Cuphead-era cartoon aesthetics** — thick ink outlines, bold pastel fills, hand-drawn energy — with the warmth and grain of analog film photography. The result should feel like a vintage animation cel come to life: playful, confident, and unmistakably handcrafted.

The brand serves two audiences: dog owners looking for portrait sessions, and art collectors drawn to fine art film prints. The design must feel approachable enough for pet owners yet carry enough visual weight to frame fine art credibly.

---

## Design Principles

1. **Outline everything** — Every interactive element, card, image frame, and container gets a visible dark stroke. This is the single most defining trait of the aesthetic. When in doubt, add an outline.
2. **Fill with warmth** — Backgrounds and fills use the vintage pastel palette. Never use pure white (`#FFFFFF`) or pure black (`#000000`) — always the tinted equivalents.
3. **Let photography breathe** — The UI is bold, but photographs are the product. Gallery views, lightboxes, and print previews use generous whitespace and minimal UI chrome so the images dominate.
4. **Animate with purpose** — Motion should feel like a cartoon: snappy entrances, playful hovers, satisfying micro-interactions. No gratuitous parallax or scroll-jacking.
5. **Grain over glass** — Prefer tactile, textured surfaces (paper, film grain, ink) over glossy/glassmorphic trends. Subtle noise textures and film-grain overlays are welcome.

---

## Color Palette

### Primary Colors
| Name     | Hex       | Usage                                       |
|----------|-----------|---------------------------------------------|
| Blue     | `#5A9CB5` | Primary action, links, CTA buttons, accents |
| Yellow   | `#FACE68` | Highlights, active states, hover fills      |
| Orange   | `#FAAC68` | Secondary accents, warnings, tags           |
| Red      | `#FA6868` | Destructive actions, errors, sale badges    |

### Neutral Colors
| Name     | Hex       | Usage                                       |
|----------|-----------|---------------------------------------------|
| White    | `#FFF8F0` | Card backgrounds, navbar, dropdowns         |
| Cream    | `#FFF3E0` | Page background, subtle fills               |
| Dark     | `#2A2A2A` | Text, outlines, borders — the "ink" color   |

### Extended Palette (derived)
- **Blue light:** `lighten($blue, 25%)` — info banners, selected tags
- **Yellow light:** `lighten($yellow, 15%)` — table row hover
- **Red light:** `lighten($red, 20%)` — error field backgrounds
- Use `darken()` by 8–12% for hover/pressed states on filled elements

### Color Rules
- **Outlines are always `$dark` (`#2A2A2A`)**. Never use colored outlines — the dark ink look is non-negotiable.
- **Text is always `$dark`** on light backgrounds. On `$blue` or `$dark` backgrounds, use `$white`.
- **Background alternation:** Alternate between `$cream` and `$white` for page sections to create visual rhythm without hard dividers.

---

## Typography

### Font Stack
| Role     | Family       | Weight(s)        | Fallback       |
|----------|-------------|------------------|----------------|
| Headings | **Titan One** | 400 (only weight) | `cursive`      |
| Body     | **Nunito**    | 400, 600, 700, 800 | `sans-serif` |

### Type Scale (desktop)
| Element       | Family     | Size     | Weight | Transform   |
|---------------|-----------|----------|--------|-------------|
| H1 (hero)     | Titan One | 3.5rem   | 400    | —           |
| H2 (section)  | Titan One | 2.25rem  | 400    | —           |
| H3 (card)     | Titan One | 1.5rem   | 400    | —           |
| H4 (sub)      | Titan One | 1.15rem  | 400    | —           |
| Nav link      | Nunito    | 0.95rem  | 700    | uppercase   |
| Body          | Nunito    | 1rem     | 400    | —           |
| Body bold     | Nunito    | 1rem     | 700    | —           |
| Caption/meta  | Nunito    | 0.85rem  | 600    | —           |
| Button        | Nunito    | 0.95rem  | 700    | uppercase   |
| Price         | Titan One | 1.25rem  | 400    | —           |

### Type Rules
- **Headings never use uppercase** — Titan One is already bold and chunky; uppercase makes it unreadable.
- **Nav links and buttons use uppercase Nunito** — this creates contrast between UI chrome and content.
- **Line height:** 1.2 for headings, 1.6 for body text.
- **Letter spacing:** 0.5px on uppercase elements, default elsewhere.

---

## The Cuphead Outline System

The thick outline is the core visual motif. Apply it consistently.

### Outline Tokens
```scss
$outline-width: 3px;        // Standard — buttons, cards, inputs, nav
$outline-width-thick: 4px;  // Emphasis — hero cards, image frames, modals
$outline: #2A2A2A;          // Always this color
```

### Where to Apply
| Element               | Outline | Border-radius | Box-shadow            |
|-----------------------|---------|---------------|-----------------------|
| Buttons (primary)     | 3px     | 8px           | `3px 3px 0 $outline`  |
| Buttons (hover)       | 3px     | 8px           | `4px 4px 0 $outline`  |
| Cards                 | 3px     | 12px          | `4px 4px 0 $outline`  |
| Image frames          | 4px     | 12px          | `5px 5px 0 $outline`  |
| Inputs / textareas    | 3px     | 8px           | none                  |
| Dropdowns             | 3px     | 12px          | `4px 4px 0 $outline`  |
| Modals                | 4px     | 16px          | `6px 6px 0 $outline`  |
| Tags / badges         | 2px     | 20px (pill)   | none                  |

### Box Shadow Pattern
The offset `box-shadow` creates the cartoon "lifted" look. Always use hard shadow (0 blur):
```scss
box-shadow: Xpx Xpx 0 $outline;
```
The offset should match or slightly exceed the border width. On hover, increase the offset by 1–2px and translate the element up by the same amount to simulate "lifting."

---

## Spacing System

Use an 8px grid. All spacing values should be multiples of 8.

| Token   | Value  | Usage                              |
|---------|--------|------------------------------------|
| `$sp-1` | 4px    | Tight gaps (icon + label)          |
| `$sp-2` | 8px    | Inline spacing, tag gaps           |
| `$sp-3` | 16px   | Component padding, form gaps       |
| `$sp-4` | 24px   | Section padding, card padding      |
| `$sp-5` | 32px   | Between nav links and brand        |
| `$sp-6` | 48px   | Section margins                    |
| `$sp-7` | 64px   | Hero vertical padding              |
| `$sp-8` | 96px   | Major section breaks               |

---

## Breakpoints (Desktop-First)

```scss
$breakpoint-lg: 1024px;  // Tablet landscape / small desktop
$breakpoint-md: 768px;   // Tablet portrait — nav collapses to hamburger
$breakpoint-sm: 480px;   // Mobile — single column, stacked layouts
```

Use `max-width` media queries (desktop-first):
```scss
@media (max-width: $breakpoint-md) { ... }
```

### Responsive Behavior
| Breakpoint        | Layout changes                                        |
|-------------------|-------------------------------------------------------|
| > 1024px          | Full desktop layout, all nav links visible            |
| 769px – 1024px    | Grid columns reduce, some padding tightens            |
| 481px – 768px     | Nav collapses to hamburger, 2-column grids            |
| ≤ 480px           | Single column, stacked cards, larger touch targets    |

---

## Component Patterns

### Buttons
```
┌─────────────────────┐
│   BUTTON LABEL      │  ← Nunito 700, uppercase, 0.95rem
└─────────────────────┘
  ████████████████████   ← box-shadow offset
```
- **Primary:** `$blue` fill, `$white` text, `$outline` border + shadow
- **Secondary:** `$cream` fill, `$dark` text, `$outline` border + shadow
- **Danger:** `$red` fill, `$white` text, `$outline` border + shadow
- **Ghost:** transparent fill, `$dark` text, `$outline` border, no shadow
- **Hover:** Darken fill 8%, increase shadow offset +1px, `translateY(-1px)`
- **Active/Pressed:** Remove shadow, `translateY(1px)` (button "pushes in")

### Cards (Product / Photo / Blog)
```
┌──────────────────────────┐
│  ┌────────────────────┐  │
│  │                    │  │  ← Image with 4px outline frame
│  │      PHOTO         │  │
│  │                    │  │
│  └────────────────────┘  │
│  Title                   │  ← Titan One
│  Description text...     │  ← Nunito 400
│  ┌──────────┐            │
│  │  ACTION  │            │  ← CTA button
│  └──────────┘            │
└──────────────────────────┘
  ██████████████████████████  ← card shadow
```
- 3px outline, 12px radius, 4px 4px shadow
- Padding: 16px (image bleeds to edges) or 24px (full padding)
- On hover: shadow grows to 6px 6px, card lifts 2px

### Image Frames
Photos are the product — frame them deliberately:
- **Gallery thumbnail:** 3px outline, 8px radius, no shadow
- **Detail / lightbox:** 4px outline, 12px radius, 5px 5px shadow
- **Print preview (shop):** 4px outline, 0 radius (sharp corners to mimic a real print)
- Always preserve aspect ratio. Never crop to forced dimensions in galleries.

### Form Inputs
- 3px `$outline` border, 8px radius
- `$white` background, `$cream` on focus
- Focus state: border becomes `$blue`, subtle `box-shadow: 0 0 0 3px lighten($blue, 30%)`
- Error state: border becomes `$red`, background `lighten($red, 25%)`

### Tags / Badges
- Pill shape (large border-radius)
- 2px `$outline` border
- Fill with palette colors: `$yellow` for categories, `$orange` for tags, `$red` for sale/new
- Font: Nunito 700, 0.8rem, uppercase

---

## Animation & Motion

### Approach
1. **CSS first** — Use native CSS transitions and `@keyframes` for all standard interactions (hover, focus, menu open/close, page transitions).
2. **GSAP only when needed** — Complex sequenced animations (hero entrance, scroll-triggered gallery reveals, cart fly-to animations). Import GSAP per-component, never globally.

### Standard Transitions
```scss
$transition-fast: 150ms ease;   // Hover color changes, small state shifts
$transition-base: 250ms ease;   // Menu open/close, dropdowns, card hover lifts
$transition-slow: 400ms ease;   // Page-level transitions, hero entrance
```

### Micro-interactions
| Interaction              | Animation                                        |
|--------------------------|--------------------------------------------------|
| Button hover             | Lift 1px, grow shadow — `$transition-base`       |
| Button press             | Drop 1px, remove shadow — `$transition-fast`     |
| Card hover               | Lift 2px, grow shadow — `$transition-base`       |
| Nav link hover           | Fill background slides in — `$transition-base`   |
| Dropdown open            | Fade + slide down 8px — `$transition-base`       |
| Mobile menu              | Slide down from navbar — `250ms ease`            |
| Image load (gallery)     | Fade in from 0 opacity — `300ms ease`            |
| Page transition (Astro)  | Crossfade — `200ms`                              |

### GSAP Use Cases (future)
- **Hero section:** Staggered entrance of heading, subtitle, CTA
- **Gallery scroll reveal:** Photos scale up from 0.9 with staggered timing as they enter viewport
- **Cart add animation:** Product thumbnail flies to cart icon in navbar
- **Lightbox open:** Photo scales from thumbnail position to full-screen center

---

## Photography-Specific Design

### Gallery Layouts
- **Grid:** CSS Grid with `auto-fill, minmax(280px, 1fr)` — adapts naturally to viewport
- **Masonry** (for mixed-aspect-ratio collections): CSS `columns` or future CSS masonry. Photos keep their natural aspect ratios — never force square crops on film photos.
- **Featured/Hero:** Single large image spanning full width with generous vertical padding

### Lightbox
- Dark overlay (`rgba($dark, 0.92)`) — photos pop against near-black
- 4px white/cream outline frame around the image
- EXIF metadata (camera, film stock, lens, aperture) displayed below in caption style
- Arrow navigation with thick-outline circular buttons
- Close button: top-right, circular, `$outline` border, `$cream` fill, `×`

### Print Shop
- Product cards show the photo in a **simulated frame/mat mockup** where possible
- Size selector uses large, tactile radio buttons with outlines
- Price displayed in Titan One — prices are a feature, not hidden
- "Add to cart" uses the primary `$blue` button pattern

### Film / Analog Touches
- **Grain overlay:** Optional subtle CSS noise texture on hero sections and page backgrounds (`background-image` with a tiny tiled noise PNG at low opacity)
- **Sprocket holes:** Decorative border element for "film strip" sections (blog posts, timeline)
- **Warm tone shift:** Photos may have a warm color grade — the cream/warm-white background complements this

---

## Iconography

- **Style:** Line icons with 2.5px stroke weight to match the outline aesthetic
- **Source:** Lucide icons (already used for account icon in Navbar) or custom SVGs
- **Size:** 20–24px for inline UI, 28–32px for navigation, 48px+ for empty states
- **Color:** Always `$dark` (`currentColor`), or `$white` on dark backgrounds
- Never use filled/solid icon variants — line-only maintains the Cuphead outline look

---

## Accessibility

- **Contrast:** All text/background combinations must meet WCAG AA (4.5:1 for normal text, 3:1 for large). The `$dark` on `$cream`/`$white` palette inherently passes.
- **Focus indicators:** Visible focus ring on all interactive elements — `3px solid $blue` with `2px offset` (don't rely on outline removal for aesthetics).
- **Touch targets:** Minimum 44x44px for all interactive elements on mobile.
- **Motion:** Respect `prefers-reduced-motion` — disable animations and transitions for users who prefer it.
- **Alt text:** Every `<ResponsiveImage>` requires meaningful alt text describing the photograph.

---

## SCSS Architecture

```
src/styles/
├── _variables.scss     ← Tokens: colors, fonts, breakpoints, spacing
├── _mixins.scss        ← Reusable mixins (outline-box, responsive, etc.)
├── global.scss         ← Reset, base element styles, font imports
```

Component styles live in scoped `<style lang="scss">` blocks within `.astro` files, importing `_variables` and `_mixins` as needed:
```scss
<style lang="scss">
  @use "../styles/variables" as *;
  @use "../styles/mixins" as *;
</style>
```

### Key Mixins (to create)
```scss
// Cuphead outline box — the core pattern
@mixin outline-box($radius: 12px, $shadow: true) {
  border: $outline-width solid $outline;
  border-radius: $radius;
  @if $shadow {
    box-shadow: 4px 4px 0 $outline;
  }
}

// Responsive shorthand
@mixin below($bp) {
  @media (max-width: $bp) { @content; }
}

// Hover lift effect
@mixin hover-lift($amount: 2px) {
  transition: transform $transition-base, box-shadow $transition-base;
  &:hover {
    transform: translateY(-#{$amount});
    box-shadow: (4px + $amount) (4px + $amount) 0 $outline;
  }
}
```

---

## BEM Naming Convention

All CSS classes follow **BEM** (Block Element Modifier):
```
.block {}
.block__element {}
.block__element--modifier {}
```

Examples from the navbar:
```scss
.navbar {}
.navbar__brand {}
.navbar__link {}
.navbar__link--active {}
.navbar__dropdown--open {}
```

Never use generic utility classes. Every class is scoped to its block.
