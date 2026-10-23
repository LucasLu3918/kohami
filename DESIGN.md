---
version: 1
name: KOHAMI Storefront
description: Editorial Japanese-fashion storefront with calm product browsing and clear, trustworthy transaction states.
colors:
  ink: "#332d2b"
  ink-strong: "#272322"
  muted: "#756c67"
  muted-strong: "#625c58"
  accent: "#975e65"
  accent-strong: "#6d2942"
  accent-hover: "#4e1d30"
  paper: "#f8f6f3"
  linen: "#f3ede6"
  surface: "#ffffff"
  border: "#ded9d5"
  border-subtle: "#eee9e4"
  border-strong: "#8a817b"
  focus: "#235b9d"
  danger: "#a11919"
  danger-ink: "#711010"
  danger-surface: "#fff3f3"
  success: "#277547"
  success-surface: "#f0faf4"
  scrollbar-thumb: "#b8aaa4"
  scrollbar-track: "#f8f6f3"
  scrollbar-hover: "#957d78"
typography:
  sans:
    fontFamily: "'Noto Sans TC', 'Hiragino Kaku Gothic ProN', 'Yu Gothic', Meiryo, system-ui, sans-serif"
  display:
    fontFamily: "Georgia, 'Times New Roman', serif"
  mono:
    fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace"
rounded:
  DEFAULT: "0.5rem"
  sm: "0.25rem"
  md: "0.5rem"
  lg: "0.75rem"
spacing:
  section-gap: "3rem"
  page-max: "88rem"
components:
  button: { "minimum-height": "2.75rem", "focus-ring": "3px solid var(--store-focus)" }
  card: { "border": "1px solid var(--store-border)", "radius": "0.5rem" }
  search: { "clear-control": "explicit, localized, shown when non-empty" }
  status: { "state": "text plus semantic color", "payment-source": "server projection" }
---

# KOHAMI Storefront Design System

## Overview

### Creative North Star

Use the feeling of a carefully edited independent clothing boutique: warm paper, garment photography, a centered wordmark, and a restrained berry accent. The product itself supplies the expressive color and texture; interface decoration stays quiet so size, color, price, and order state remain easy to compare.

### Product context and register

- **Audience and primary job:** shoppers browse Japanese-inspired apparel, choose variants, and manage their own orders; staff use separate admin routes for catalog and reports.
- **Target market(s) and evidence:** the product supports Traditional Chinese, Japanese, and English and currently uses a local TWD mock-commerce contract. The Japanese locale does not establish a Japan-market operating or regulatory scope.
- **Locale(s) and language policy:** `zh-TW`, `ja`, and `en`; localize content, dates, currency, status labels, and accessible names. Japanese order/payment copy still needs review by a native Japanese reviewer with commerce context.
- **Usage scene:** responsive consumer storefront, most often phone or laptop; order/payment status is consequential and must stay explicit.
- **Register:** hybrid. Storefront routes use editorial retail expression; account, checkout, and admin routes use familiar task-oriented product UI under the same visual tokens.
- **Memorable signature:** the quiet center-aligned KOHAMI wordmark paired with natural garment photography and a dark berry action color.
- **Restraint:** keep transaction screens structured, readable, and low-motion; reserve serif display type for brand and select headings.
- **Anti-references:** generic gradient dashboards, promotional neon, ornate Japanese motifs without product evidence, and dense layouts that hide status or action.
- **Token ownership/runtime mapping:** this file documents the shared token values. `apps/storefront/app/app.vue` owns runtime CSS custom properties (`--store-*`); shared palette and typography choices in scoped Vue styles should consume them. Keep shared values synchronized in the same change and run the premium audit after token edits. Page-specific colors remain only when they carry a distinct meaning that the shared palette does not express.

## Colors

The palette comes from the existing storefront: dark warm ink on white, paper and linen surfaces, muted rose for accents, and dark berry for primary actions. `focus` stays a distinct blue so keyboard location is not confused with brand emphasis. Danger and success use text plus semantic color. Borders use subtle, regular, and strong contrast levels. There is one light theme; forced-colors mode returns scrollbar rendering to the operating system.

## Typography

Use a Japanese-capable sans stack first for body copy, controls, and multilingual data. The installed system fonts remain the fallback; no remote font is required. Use Georgia for the wordmark and restrained display moments. Keep body copy near 16px with 1.6 line height; small metadata may be smaller when it remains readable at zoom. Identifiers wrap instead of truncating. Japanese prose uses natural wrapping and receives a rendered-context review for long labels, mixed scripts, and punctuation. Do not use italics as the default emphasis.

## Layout

Use a centered shell with an 88rem maximum width for the shop header and narrower reading widths for forms and support content. Consumer pages use generous section spacing; order cards group state, amount, action, and line items. At narrow widths, product rows stack while keeping each action and full value available. Avoid viewport-height constraints on shared page shells. Images reserve their aspect ratio; loading and error copy occupy stable regions.

## Elevation & Depth

Use surface changes and fine borders before shadows. Cards are white on white/paper with a visible border. Reserve shadows for anchored menus. Avoid blurred glass, floating panels, and decorative elevation on transaction details.

## Shapes

Cards use a 0.5rem radius; controls use 0.25rem to 0.5rem. Dividers are one pixel and use the subtle border token. Product photography may have a soft corner; controls remain familiar and plainly clickable.

## Components

### Foundational visual states

Hover may deepen an existing accent. Focus-visible uses a 3px `--store-focus` outline with offset. Disabled controls retain legible contrast and stop behaving as links/actions. Busy controls retain their dimensions. Payment state is written in text and reflects the server projection; color alone never communicates state. Loading uses inline status text already present in the workflow.

### Buttons and actions

Primary actions use dark berry; secondary actions use outline or neutral treatment. Destructive actions stay separated from routine actions. Pointer actions show a pointer cursor; keyboard focus remains visible. Do not widen buttons when changing a label to a busy label.

### Navigation and data display

The shop shell has a narrow announcement, centered wordmark, search, quick links, cart count, and locale switcher. Lists preserve full identifiers and use responsive product rows. Status and amount are grouped before the detail disclosure. Admin pages use the same tokens with denser data presentation.

### Forms and overlays

Inputs inherit the Japanese-capable body family and minimum control height. Search has an explicit localized clear button when populated. Use visible labels and inline correction messages. Keep select and date popups platform-owned only where the existing flow accepts that behavior. No browser-native alert/confirm/prompt dialogs.

### Iconography

Use Phosphor icons already installed in the storefront. Use regular weight for navigation and controls, align optically with text, and keep a text label or localized accessible name.

### Motion

Motion is brief and state-driven. Do not animate routine order refreshes. Honor `prefers-reduced-motion`; no motion may be required to discover or understand an action.

### Content and data visualization

Use plain, specific verbs and localized complete messages. Keep currency in TWD for the current mock commerce flow. Distinguish order state from payment state and keep timestamps localized while preserving their ISO source value.

## Do's and Don'ts

- **Do:** let garment photography and product details carry visual interest.
- **Do:** show order and payment state as separate server-owned facts.
- **Don't:** infer payment success or retry eligibility from an old order-state label.
- **Don't:** add cultural motifs based only on the `ja` locale.
