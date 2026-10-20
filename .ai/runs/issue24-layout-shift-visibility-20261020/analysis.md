# Issue24 interaction layout-shift visibility diagnostic

## Scope

This run followed up the recommendation to retain stable source selectors and viewport visibility for recent-input layout shifts. The runner is a copy of the prior Issue24 cart-add / quote-retry synthetic scenario. It adds selector uniqueness, source rectangles, and previous/current viewport intersection and visible-area ratio. It does not change CLS calculation or create a pass threshold.

## Method and environment

- Three locales, 30 fresh browser contexts per locale, four interaction stages per journey: 90 journeys and 360 stages.
- Existing Storefront production preview build, served on loopback `127.0.0.1:3005`; Playwright route stubs supplied all browser API responses. No SQL, live API, payment service, LAN, RUM or external service.
- UTC measurement period: `2026-10-19T16:37:13.024Z`–`2026-10-19T16:45:48.259Z`.
- Darwin 25.6.0 arm64; Headless Chrome 155.0.8059.84; 1440×900 CSS px; 4× CPU slowdown; 150 ms network latency, 1.6 Mbps down / 0.75 Mbps up; synthetic write-response delay 120 ms; new browser context per sample, cache not separately stratified. CPU model and memory were unavailable.
- P75 uses nearest-rank `ceil(n × 0.75)`. CLS is the maximum 1-second-gap / 5-second-cap session window excluding `hadRecentInput=true`. The separate source record includes semantic selector, selector uniqueness, previous/current source rects, current DOM rect, and viewport intersection/visible-area ratio.
- Reproduction command: `NITRO_HOST=127.0.0.1 NITRO_PORT=3005 NUXT_SITE_ORIGIN=http://127.0.0.1:3005 node apps/storefront/.output/server/index.mjs`; then `NO_PROXY='*' no_proxy='*' KOHAMI_STOREFRONT_ORIGIN=http://127.0.0.1:3005 ISSUE24_SAMPLES=30 ISSUE24_OUTPUT="$PWD/.ai/runs/issue24-layout-shift-visibility-20261020/metrics.json" node .ai/runs/issue24-layout-shift-visibility-20261020/measurement.mjs`.

## Results

- All 12 locale/stage groups completed n=30; 90 add and 90 quote operations returned their expected 503/200 sequences. All 12 CLS groups have P75 0 and max 0.
- There are 210 layout-shift entries, all `hadRecentInput=true`. In each locale, add failure and retry have 30/30 entries and semantic unique selectors for `ul.cart-list`, `section[aria-labelledby="cart-heading"] > label`, and `section[aria-labelledby="cart-heading"] > button`. Their source nodes intersect the viewport 30/30. Cart elements move down/up by 62 px (zh-TW), 64 px (ja), and 58 px (en).
- English add failure also moves `section[aria-labelledby="checkout-heading"]` out of the viewport in 30/30 samples; the retry brings it back into view. The source rect after callback is used for viewport attribution when LayoutShift API's current rect is zero for an offscreen source.
- Quote failure has no layout-shift entries in any locale. zh-TW and ja quote retry have none. English quote retry has 30/30 recent-input entries from `section[aria-labelledby="checkout-heading"]`: its previous rect starts at y=890.6 with 9.4 px visible, then the current DOM rect is at y≈1,367.1 and fully outside the 900 px viewport.
- Manual DOM review confirms the discovery alert appears before the cart section; this is consistent with the observed downstream movement. Source attribution confidence is high. The movement is visible in this synthetic desktop scenario, but current acceptance documents do not adopt a threshold for input-triggered layout shifts. Treat it as a UX improvement candidate, not a confirmed defect. No product test or code fix was added.

## Limits and status

Recent-input shifts are excluded from CLS by definition, so CLS P75 zero does not mean the interactions caused no movement. This is local synthetic lab evidence, not INP, field CLS/Core Web Vitals, production SLA, iPhone Safari, or VoiceOver evidence. Issue24 remains `claimed`; its overall performance item remains incomplete.

The run directory contains the measurement script, full raw metrics and Change Impact record. Project Intelligence targeted refresh was blocked by the dirty working tree; existing `.ai/` changes and evidence were preserved. Global consumer graph coverage remains partial.
