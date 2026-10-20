# Issue24 document-coordinate shift attribution

## Scope and reproduction

- Repeated the trilingual desktop home synthetic journey against a fresh production build from the current committed Storefront source. The app was copied to `/private/tmp/issue24-document-coordinate-shift-20261020/storefront`; the source checkout's existing Nuxt dev server and `.nuxt` lock were not touched.
- Preview used `127.0.0.1:3014`. Playwright supplied all `/api/v1/*` responses through route stubs. No SQL, live API, payment, LAN, RUM, or external service was used.
- Three locales × 30 fresh browser contexts = 90 journeys / 360 stages. Each journey exercised add failure/retry and quote failure/retry; all requests returned the expected 503/200 sequence.
- UTC measurement: 2026-10-19T17:26:51.613Z–2026-10-19T17:35:24.596Z. Darwin 25.6.0 arm64; Headless Chrome 155.0.8059.84; viewport 1440×900 CSS px; CPU 4× slowdown; 150 ms latency, 1.6 Mbps down / 0.75 Mbps up; API write stubs delayed 120 ms; fresh browser context per journey, cache not separately stratified. CPU model and memory were unavailable.
- Each stage stores before/after `scrollY`, viewport top, and `getBoundingClientRect().top + scrollY` for cart list, quantity and update controls, and checkout section. LayoutShift entries also include callback-time scrollY, `hadRecentInput`, source selector, and rects. CLS uses the maximum 1-second-gap / 5-second-cap session window excluding recent-input entries; P75 is nearest-rank `ceil(n × .75)`.

## Results

- All 12 locale/stage groups completed n=30. Every group had CLS P75=0 and max=0. All 90 add and 90 quote requests had expected 503/200 outcomes.
- Cart list and controls had document-coordinate delta 0 in all 30 journeys per locale and stage. During add failure, the browser scrolled to the action by 1,241–1,302 px in this runner, while document coordinates stayed fixed. No LayoutShift entries were observed during add failure or retry in any locale. This is consistent with the fixed cart feedback slot in the current build preventing the previously seen cart-list displacement.
- On successful quote retry, the checkout section's document top moved down exactly 497.6 px (zh-TW), 516.6 px (ja), and 476.6 px (en) in 30/30 journeys per locale; `scrollY` delta was 0. The quote panel is inserted before the checkout section in `apps/storefront/app/pages/index.vue`, so this is a real document-flow expansion, not focus scrolling. It was not a cart geometry shift.
- LayoutShift entries were observed only for ja: 30 on quote failure (source `section[aria-labelledby="care-heading"]`) and 60 on quote retry (30 for care section and 30 for checkout section). All 90 entries had `hadRecentInput=true` and were excluded from CLS. No entries were observed in zh-TW or en.
- Quote-panel insertion is the expected presentation of the successful quote result. No project criterion forbids downstream flow expansion or sets an interaction-shift threshold; this evidence is recorded as interaction geometry, not a confirmed product defect. No product code changed.

## Limits

- The previous 2026-10-19 raw LayoutShift records did not preserve scrollY, so those 210 historical entries cannot be retroactively separated into viewport scrolling versus document reflow. This new run is a fresh measurement after the cart-feedback fix, not a directly comparable reproduction of the old build's entry count.
- This is local synthetic desktop lab evidence only. It does not establish field CLS/INP, Core Web Vitals, a production SLA, real visitor impact, iPhone Safari, or VoiceOver acceptance. Issue24 remains `claimed`.

## Reproduction commands

Build from an isolated copy to avoid the active checkout Nuxt dev-server lock:

```sh
rsync -a --exclude=node_modules --exclude=.nuxt --exclude=.output --exclude=.data apps/storefront/ /private/tmp/issue24-document-coordinate-shift-20261020/storefront/
cd /private/tmp/issue24-document-coordinate-shift-20261020/storefront
NUXT_API_ORIGIN=http://127.0.0.1:59998 npm run build
NITRO_HOST=127.0.0.1 NITRO_PORT=3014 NUXT_SITE_ORIGIN=http://127.0.0.1:3014 NUXT_API_ORIGIN=http://127.0.0.1:59998 node .output/server/index.mjs
```

In a second shell from the repository root:

```sh
NO_PROXY='*' no_proxy='*' KOHAMI_STOREFRONT_ORIGIN=http://127.0.0.1:3014 ISSUE24_SAMPLES=30 ISSUE24_OUTPUT="$PWD/.ai/runs/issue24-document-coordinate-shift-20261020/metrics.json" node .ai/runs/issue24-document-coordinate-shift-20261020/measurement.mjs
```
