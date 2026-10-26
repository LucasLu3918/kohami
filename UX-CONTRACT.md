# KOHAMI Storefront UX Contract

## Product context

- **Audience:** apparel shoppers and authorized staff.
- **Primary jobs:** discover products, confirm a cart, complete local mock checkout, inspect one's orders, and follow aftercare; staff manage catalog and inspect aggregate reports.
- **Target market(s):** current commerce is local mock mode in TWD. Japanese locale exists, but a Japan-market business boundary is not established by the maintained product sources.
- **Active locales:** `zh-TW`, `ja`, `en`.
- **Language/content register:** direct, calm retail language; use complete localized messages. Critical Japanese payment and commerce copy needs a native Japanese reviewer with domain context before release; high-traffic Japanese acquisition copy also needs native review.
- **Timezone/calendar:** display ISO instants using the active locale and browser locale timezone; do not change server UTC timestamps or transaction deadlines.
- **Accessibility target:** WCAG 2.2 AA baseline; existing project-specific mobile VoiceOver work remains separately tracked.

## Business-context sources

| Domain / scope | Authoritative source | Source type | Reviewed date |
|---|---|---|---|
| Commerce and payment lifecycle | `docs/planning/kohami/commerce/CHECKOUT_ORDER_PLAN.md` | Domain plan | 2026-10-23 |
| Trusted payment results | `docs/issues/kohami/13-trusted-payment-order-results.md` | Accepted mock scope and evidence | 2026-10-23 |
| Timeout, cancellation, and late payment | `docs/issues/kohami/14-payment-races-and-compensation.md` | Accepted mock scope and evidence | 2026-10-23 |
| Member order ownership | `docs/issues/kohami/16-member-checkout-private-orders.md` | Permission and order policy | 2026-10-23 |
| Mock commerce limits | `docs/proposals/kohami/04-demo-commerce-policy.md` | Adopted mock policy | 2026-10-23 |
| Storefront architecture and data ownership | `docs/architecture/ARCHITECTURE.md`, `docs/architecture/MODULES.md` | Architecture / domain boundaries | 2026-10-23 |
| Page journey and Japanese UI content | `docs/planning/kohami/experience/EXPERIENCE_DESIGN.md` | UX plan / language evidence | 2026-10-23 |

## Visual contract

- **Project design source:** `DESIGN.md`.
- **Token ownership:** documented in `DESIGN.md`; runtime custom properties are owned by `apps/storefront/app/app.vue`.
- **Canonical UI map:** `StorefrontHeader` owns shop navigation/search; `MemberOrderList` owns the member order list; `MemberOrderAftercare` owns expanded status details; `useMemberOrders` owns member order-list retrieval; `app.vue` owns global typography/focus/scrollbar baseline.
- **Native controls:** existing native form controls remain platform-owned; search clear is an explicit app button. Existing select/date usage is not restyled to impersonate an authored popup.
- **Scrollbar:** global root styles in `app.vue`; visible and keyboard/trackpad operable, with forced-colors fallback.
- **Shared visual values:** global typography, focus, scrollbar, and common palette tokens live in `app.vue`; page-specific colors remain where they express a distinct state.
- **Themes:** light theme only.
- **Consumer storefront art direction:** Japanese-inspired women's fashion editorial with rose-paper surfaces, an inset wardrobe-note hero frame, left-aligned KOHAMI wordmark, fine dividers, berry primary actions, and garment photography. Product discovery remains directly below the hero; use four columns on desktop, three on tablets, and two on narrow phones. No category or ranking entry is shown without corresponding catalog data.

## Component behavior

| Component | Contract |
|---|---|
| Buttons and links | Native semantics; pointer cursor for enabled actions; visible focus; disabled state is not actionable; busy labels preserve dimensions. |
| Search | Query is reflected in the route; non-empty search has a localized clear control; clearing updates the route immediately and returns focus to the input. |
| Order list | Server state is the source of truth. Order state and payment state are shown separately when they differ. Details are lazy-loaded; order cards retain full identifiers and snapshot price. |
| Payment link | Show only when the order is `AwaitingPayment`, server payment state is `Created` or `Failed`, confirmed collection is zero, compensation is not required, and a checkout identifier exists. Missing, processing, unknown, succeeded, contradictory, or stale payment data has no payment affordance. Checkout remains the final server revalidation boundary. |
| Refresh | Initial account entry loads orders; route activation and foreground restoration revalidate the list. Manual account refresh remains available. Older overlapping responses cannot replace a newer order list. |
| Disclosure | Order details open on activation, expose `aria-expanded`, announce loading/error states, and retain focus on the disclosure control. |
| Forms | Keep visible labels, localized errors, password manager semantics, and non-native app error recovery. Avoid duplicate submit. |
| Product media | Keep color-specific product-only images separate from model outfit concepts. Selecting a color updates both sections to that color; the main product gallery never includes a person. If that color has no product image, show a localized missing-image state instead of another color's image or an outfit photo. Label concept-color product artwork as non-photographic, and label model imagery as outfit inspiration rather than verified product photography or fit evidence. |

## Dataset navigation

- Member orders are a server-returned list. No client-side filtering or pagination is currently applied; preserve server ordering.
- Admin reports use the range filtering contract owned by their existing page/API. Do not add unbounded tables.
- Empty order list, initial loading, and service failure remain distinct. A refresh failure clears private order data through the current error path rather than keeping an unverified payment action.
- Back navigation preserves the route; returning to the account triggers a fresh server query.

## Flow ledger

| Operation | Trigger | Pending | Success destination | Success feedback | Failure recovery | Focus outcome | Source ref |
|---|---|---|---|---|---|---|---|
| Search | Header form submit | Route navigation | Catalog anchor | Updated results | Keep query; clear or retry | Focus remains in normal route flow | `apps/storefront/app/components/storefront/StorefrontHeader.vue` |
| Add to cart | Product card or detail action | Existing API pending state | Current page/cart link | Existing localized success status | Inline error, preserve chosen SKU | Keep initiating control available | `docs/planning/kohami/experience/EXPERIENCE_DESIGN.md` |
| Place order | Member confirms cart | Existing server operation | Checkout result route | Existing order result state | Refresh current server quote; no duplicate order | Keep route content reachable | `docs/planning/kohami/commerce/CHECKOUT_ORDER_PLAN.md` |
| Pay mock checkout | Member submits checkout payment | Processing and status refresh | Same checkout result | Show server-confirmed status | Unknown/processing stays pending; no re-pay; refresh same checkout | Keep refresh/action context | Issues 13 and 14 |
| Refresh member orders | Initial route entry, route activation, page foreground, or explicit refresh | Inline loading | Same account page | Updated server order/payment status | Recoverable service error; refresh again | Do not steal focus | `apps/storefront/app/composables/useMemberOrders.ts` |
| Cancel order | Expanded order detail action allowed by API | Existing pending state | Same account page | Refresh order detail and list | On 409/412 refresh; do not blindly resubmit | Return to initiating control | Issue 26 and order plan |
| Request return | Expanded order detail action allowed by API | Existing pending state | Same account page | Refresh order detail and list | On 409/412 refresh; preserve actionable error | Return to form/control | Issue 26 and order plan |

## Navigation and responsive behavior

- Each route uses `useLocalizedPageHead` with localized titles; private routes remain no-store/noindex under current policy.
- Shop shell navigation is shared; desktop aligns the brand at the left and quick links at the right, while mobile places brand and locale together above search and a stable link row. Do not obscure the focused control with sticky UI.
- Homepage product variant query updates preserve the current route component and scroll position; product cards reserve the same geometry before and after a SKU selection.
- Product detail media follows the selected SKU color. Product-only images and outfit inspiration have separate headings and media roles; color changes reset the product gallery to that color's first image and filter outfit concepts to the same color. Concept-color art must say it is not a photograph of the physical item.
- Order identifiers, amounts, and status text remain readable at narrow widths; product rows stack without hiding the price or image.
- No table or page applies a fixed viewport height to the shared shell.
- The current app has no maintained custom modal or toast provider; keep feedback inline and preserve existing page-owned patterns until a recurring canonical owner is introduced.

## Async and resilience

- Checkout/payment writes remain pessimistic and server-authoritative. The client never marks an order paid from navigation, a browser event, or a payment-return URL.
- Existing idempotency keys, CSRF protection, order versions, ownership checks, and server transitions remain unchanged.
- Account order GET requests are read-only and may be reissued on route entry and visibility restoration. Ignore stale responses that finish after a newer read.
- `Processing` and `Unknown` are not failures and do not permit another payment attempt. `Succeeded` never permits another payment attempt. If payment succeeds while the order projection has not advanced, show the payment result and omit the pay link until refreshed.
- API or detail-load errors remain visible with a retry path; do not present stale payment actions after an error.
- Session expiry clears private order data using the existing session handling.

## Validation and permissions

- The API authorizes reads for the current member; the UI receives only the current member's orders.
- Client action visibility is explanatory only; API authorization and transition validation remain authoritative.
- No payment card or wallet action is performed during order-list refresh.
- Forms continue to own text feedback and preserve entered values on recoverable failures.

## Verification

- Static evidence: frontend-design-premium strict audit, design-token review, localized source inspection, and route/style inventory.
- Required runtime review for consequential release: `zh-TW`, `ja`, and `en`; desktop and narrow viewport; success, processing, unknown, failure, missing-summary, empty, and service-error states; keyboard focus and reduced motion.
- Browser review and project tests are reported only when run. The existing support docs identify Japanese native review and some mobile accessibility evidence as release follow-ups.
