# Visual acceptance: storefront and admin login

Date: 2026-10-21. Candidate branch: `codex/demo-visual-polish`, base `547e521`.

## Scope and result

Visual comparison against [GRL](https://www.grail.bz) at 1440px desktop and 390px mobile. The shared KOHAMI header now centers the brand independently of side actions; the catalog grid is 5 columns on desktop and 3 on mobile; the admin login uses the shared storefront header and a centered form with a clear administrator label. All six final routes have no horizontal overflow. Product details were intentionally left unchanged because P5 edits overlap that page; the current approved product asset is a single flat-lay image, so no alternate-view thumbnails were fabricated.

GRL presents a centered brand/navigation header and a denser 5-column desktop / 3-column mobile catalog. The baseline had a left-biased header, a 4/2-column product grid, and an editorial split-image admin login. The final KOHAMI home layout matches the header centering and grid density; admin now uses a conventional centered credential form. GRL's first-visit promotional overlay obscures part of its catalog during capture, limiting direct card-detail comparison. KOHAMI retains its own colors, copy, and demo safeguards.

## Measurements

| Route | Viewport | Before | After | Final overflow | Notes |
|---|---:|---|---|---|---|
| Home | 1440 | 4 columns; brand not centered | 5 columns; center delta 0px | none (1440px document) | 15 cards and 15 images |
| Home | 390 | 2 columns; brand left aligned | 3 columns; center delta 0.01px | none (390px document) | 15 cards and 15 images |
| Product detail | 1440 | unchanged baseline | unchanged | none | one real approved product image |
| Product detail | 390 | unchanged baseline | unchanged | none | gallery/sticky purchase bar deferred with P5 |
| Admin login | 1440 | split image/form | centered form; brand and panel center delta 0px | none | admin role hint retained |
| Admin login | 390 | stacked editorial image/form | centered form; brand and panel center delta 0px | none | no horizontal overflow |

## Functional checks

- P3 storefront: 15 products/images, localized routes, search, SKU selection updates URL, add-to-cart request and cart route all pass.
- P4 admin: login form route and invalid-credential safe error pass; role label and shop-return link render; protected access behavior remains covered by the existing spec.
- The only console error during the invalid-login smoke was the expected `401 Unauthorized` response.
- Product grid and brand centering assertions were added to P3; admin center/role/header assertions were added to P4.

## Commands and results

Run from `apps/storefront` with the synthetic API fixture and local HTTPS preview configured as in the existing P3/P4 specs:

```sh
KOHAMI_STOREFRONT_ORIGIN=https://localhost:3114 npm exec playwright -- test tests/issue-demo-p3-storefront.spec.ts tests/issue-demo-p4-admin.spec.ts --config=playwright.config.ts --reporter=line --output=/private/tmp/kohami-visual-review/final-browser-playwright
npm run typecheck
npm run build
```

Results: Playwright 7/7 passed (~8.5s); typecheck passed; production build passed (`Build complete in 2.37s`). Final screenshots were taken with Chrome strict TLS (`ignoreHTTPSErrors: false`) at 1440x1000 and 390x844. The temporary localhost CA used for that run was removed afterward; Keychain verification showed only the pre-existing localhost certificate remained. Nuxt and synthetic API processes started for acceptance were stopped.

## Evidence

Before and after PNGs are included under `evidence/before/` and `evidence/after/`. GRL reference captures and the complete run output remain in `/private/tmp/kohami-visual-review/` (`ref-home-*`, `ref-product-*`, `ref-login-*`, and `final-browser-playwright/`).

## Gate decision

Home/header/grid and admin-login visual gate: PASS at both target viewport widths, with the measured differences above. Full PDP visual polish: DEFERRED because P5 overlaps that page; no claim is made for gallery thumbnails or a fixed mobile purchase bar. This candidate does not assert overall release or Integration Gate readiness.
