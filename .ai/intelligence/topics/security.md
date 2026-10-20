# security

目前登入以個別 KOHAMI 使用者帳密驗證，密碼雜湊存於資料庫；API 的 SQL 連線憑證僅供服務連線使用。登入方式改善只保留於選配規劃；敏感操作的目的／資源綁定近期驗證維持獨立。實際驗證紀錄以 `docs/architecture/VERIFICATION.md` 為準。Owner 仍受有效角色、permission、scope 與逐資源檢查。M0-AC12 為選配且仍 PARTIAL；Issue 02 專屬完整售後驗收及正式 provider／部署另行追蹤，M0 總狀態仍 PARTIAL。
Catalog Owner 建立僅接受有效 Dataset `catalog.write` grant；Owner／Merchandiser 的 Product 變更在既有 security lock 下由 `m0.security_catalog_actor_allowed` 重驗，API 不直接讀取 `m0.Grants`。Issue 08 證據限合成 Owner 與本機 mock；未驗 Merchandiser 專屬 browser journey 或正式素材權利。

2026-10-17 public `GET /products` search accepts only bounded allowlisted query values, parameterizes SQL, stays inside `demo_m0`, and restricts results to Published products plus active/included SKUs. Color/size/availability share one SKU/Inventory EXISTS predicate; search does not mutate stock or create reservations. Current data has no category field, so category is rejected as an unsupported parameter. See `CatalogService.cs`, Issue 10 and `docs/architecture/VERIFICATION.md`.

M0-AC11 source-bound acceptance 亦已 PASS：CheckoutStatus 僅對 checkout owner browser 顯示摘要；不同 API 簽發的 browser 收到 404，Nuxt SSR 僅顯示 generic unavailable，不洩漏交易 ID、品項或金額。使用 synthetic identity／session 與 dedicated SQL fixture；外部 provider 與結算不在證據範圍。

2026-10-17 Issue12 public checkout acceptance also passed on a fresh dedicated SQL fixture: a second guest browser received 404 for another browser's checkout, and only the recorded synthetic SKU/order rows were used and cleaned. Payment remains mock. See `docs/issues/kohami/12-guest-checkout-stock-reservation.md` and `docs/architecture/VERIFICATION.md`.

2026-10-16 M0-AC01 is PASS after the cross-Dataset follow-up: 11 Dataset constraints reject foreign values; all three composite FK shapes include Dataset and reject cross-Dataset references; eight same-ID Commerce reads remain isolated; existing Identity foreign-Dataset cookie projection and schema readiness/non-destructive upgrade evidence also pass. The new Commerce cases used synthetic data in a fresh temporary SQL2025 fixture and preserved the Issue06 fixture. See the latest M0-AC01 follow-up in `docs/architecture/VERIFICATION.md` and the current-candidate entry in `IMPLEMENTATION_STATUS.yaml`.

來源：`docs/planning/kohami/delivery/technical-baseline/APPROVED_SCOPE.yaml`、`docs/planning/kohami/delivery/technical-baseline/IMPLEMENTATION_STATUS.yaml`、`docs/architecture/VERIFICATION.md`、`docs/planning/kohami/identity/AUTH_SECURITY_PROFILE.md`、`docs/planning/kohami/options/ADMIN_MFA_HARDENING_OPTION.md`、`docs/planning/kohami/product/DECISIONS_ASSUMPTIONS.md`、`docs/planning/kohami/options/OWNER_AUTHENTICATOR_NATIVE_PRESENCE_OPTION.md`。本檔為衍生索引；以原文件為準，未知 runtime 證據不推定通過。

M0-AC10 mock acceptance also verifies that a newly queued demo scenario does not claim worker start, and that `m0.security_dispatch_allowed` blocks an unstarted job when its scoped `demo.control` grant is revoked before dispatch. The test restores its synthetic grant for later cases and removes the fixture user/grants during teardown; this is dedicated mock SQL evidence, not proof of external provider authorization behavior.

M0-AC12 clean setup uses a new loopback-only SQL Server fixture, strict TLS, dedicated credentials, pinned migrations and synthetic `CommerceSeed.sql`; the recorded run removed only its exact Compose project and left the existing demonstration fixture untouched. Provisioning is not a permission to reset an existing fixture. A later tracked-only clean snapshot stalled during locked restore graph evaluation before any SQL connection; no cause is established. Source-to-binary clean restore/build and a separate human handoff remain distinct, unverified requirements.

2026-10-17 Issue 13 adds a manual refresh to the existing checkout owner's result page. It rereads the current server status and retains the last rendered response if refresh fails; it does not change identity, authorization, payment processing, persistence, or snapshot ownership. Browser evidence uses API route mocks, so SQL-backed result-page behavior remains unverified. See Issue 13 and `docs/architecture/VERIFICATION.md`.

Issue 13 follow-up extends only mocked result-state coverage. The Failed/Unknown/503 case was not executed because no Storefront service was listening; no SQL fixture migration was applied. It adds no authentication, authorization, or payment-processing behavior. The separate Commerce refund-worker harness failure remains unresolved.

Issue 14's live acceptance used the existing checkout read capability and authorized `orders.read` support scope. API, SQL and Storefront showed the same expired/succeeded/compensation-required state; the fresh dedicated fixture and synthetic session were removed afterward. This is local mock evidence only. See Issue 14 and the verification record.

2026-10-17 Issue 15 manual reconciliation report is restricted to the dataset-scoped `reconciliation.read` permission and returns mock aggregates/source identifiers without customer contact data or state changes. Its evidence is limited to the dedicated local mock journey recorded in the Issue 15 ticket and verification record; no schedule or external financial reconciliation is enabled.


2026-10-17 Issue 18 acceptance confirmed shipment creation and transitions require shipment.manage; unpaid orders are rejected. Customer tracking uses the bound guest read capability and omits internal actor/event/reason fields. Evidence is local mock only; external carrier security and complete global consumer coverage remain unknown.

2026-10-17 Issue 19/20 mock aftercare evidence: return inspection derives refund units from the original order snapshot; scoped Owner operations carry the refund case into Issue 20's mock refund. The Issue 20 manual reconciliation is read-only and dataset-scoped. No real tax authority, carrier, bank or provider is involved. See the two issue records and `docs/architecture/VERIFICATION.md`.


2026-10-17 Issue 21 adds only a Storefront mock acceptance for DEMO receipt/adjustment display. No runtime authorization or financial behavior changed; orders remain owner-scoped, and no real tax effect or external invoice authority is involved. See Issue 21 and the verification record.

2026-10-18 Issue 23 adopts temporary engineering retention ceilings, not legal classifications: known-account inactivity notification and login suspension, bounded security/report-read audit cleanup, and evidence-backed aftercare de-identification preserve the minimum order summary and financial/inventory evidence. External mail, backup lifecycle, unimplemented diagnostic sources, and final accounting/tax classification remain outside verified coverage. See the Issue 23 ticket, identity/data proposal, and verification record.

Issue 23 verification further bounds account activity to UTC timestamps updated on successful authentication or valid sessions at most once every five minutes; it stores no route, content, IP, or per-event activity. Inactivity notices use worker-only private mock evidence for verified email and known activity; login suspension follows the 30-day notice window, preserves account data, and protects the last active Owner. Report-read audit records actor when resolvable, request time, authorization outcome, and date range only—no report content, IP, session, or email; cleanup retains failed-login records for 90 days and security/report audit for one year. External mail remains unconnected.

2026-10-18 Issue 24 desktop zoom-equivalent acceptance changed only checkout summary wrapping and synthetic browser coverage; it adds no authentication, authorization, ownership, payment, API, or data behavior. Route stubs use synthetic values. See the Issue 24 verification record.

Review of the Issue24 native 400% checkout/member-order observation found no change to authentication, authorization, personal data, API, or payment boundaries; it used only the local synthetic preview. See the Issue24 ticket.

2026-10-18 Issue24 shared SSR fixture follow-up changes only Playwright configuration, synthetic in-memory fixture, and verification documentation. It does not change authentication, authorization, customer data, API contract or persistence; the fixture listens on loopback only. See `docs/issues/kohami/24-core-mobile-accessibility-performance.md` and `docs/architecture/VERIFICATION.md`.

2026-10-18 Issue24 focus-stop 與 zoom 驗收僅使用本機頁面及合成 Playwright 路由；沒有 API／SQL、帳號、付款或授權行為變更，安全邊界未改。

2026-10-19 Issue24 cache-attribution diagnostic used only a synthetic authenticated member/order fixture over loopback; no real account data, authentication policy, or security boundary changed. See `docs/architecture/VERIFICATION.md`.

2026-10-19 Issue24 desktop performance and maintenance-gate follow-up used synthetic local fixtures. The checkout API continues to enforce `MAINTENANCE_REQUIRED`; no grant, permit, SQL state, or security boundary was bypassed. `/health` and `/ready` expose only their documented connection/schema status and are not activation authority. No RUM or visitor data collection is enabled. See Issue 24 and `docs/architecture/VERIFICATION.md`.

2026-10-20 Issue25 local observability review found API/worker logging providers are cleared and no sink is configured. A governance proposal keeps collection disabled; if later approved, candidate fields are bounded event codes, severity, UTC time and synthetic run correlation only, excluding raw exceptions, request data, secrets, PII and transaction identifiers. No RUM, visitor telemetry, external alerts or persistent logging is enabled. See Issue25 and the verification record.

2026-10-19 Issue 25 readiness root-cause review used the existing loopback SQL fixture and the runtime principal for metadata-only `OBJECT_ID(N'ops.SchemaMigrations',N'U')`; the ledger was absent. No business rows, credentials, permissions, maintenance state, or fixture data were changed, and no migration was run. A healthy SQL connection does not establish schema readiness or authorize checkout; the existing migration flow requires an explicit baseline for this legacy schema. See Issue 25 and `docs/architecture/VERIFICATION.md`.

2026-10-20 本機會員儲值扣抵屬 SAL 4 經濟狀態：只以 dedicated 合成帳戶／測試入帳建立帳本餘額；結帳需由伺服器確認本人、金額、冪等與並行餘額，並與訂單／庫存原子提交。不提供第三方收款或任意餘額更新；原生 Owner／maintenance 流程須先核准目標。參見 Issue26、Wallet Settlement Plan 與本機 SQL migration／API consumers。

2026-10-20 Issue26 follow-up: migration 0013 is applied only on marker-verified `kohami-sql:14349`; the synthetic credit procedure is migrator-only, bounded, source-unique and fixture-guarded, with no API credit route. Native Secure Enclave issuer provisioning failed with OSSTATUS -34018, so no Owner, permit or maintenance change occurred. Keep checkout/payment acceptance blocked and distinguish API readiness from authority activation.

2026-10-21 member-wallet/03 real HTTP acceptance passed on a fresh, isolated synthetic-only SQL fixture: login session cookie, CSRF/Origin checks, wallet checkout, insufficient-balance rollback, successful debit/ledger/order/stock SQL readback, logout and scoped cleanup. The protected fixture's maintenance state was preserved. This does not establish real-money, formal refund, Outbox-consumer, or Storefront browser-to-live-API acceptance. See the dated Issue 03 and architecture verification record.
