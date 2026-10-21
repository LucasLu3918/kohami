# 14: 處理付款逾時、取消與晚到成功

**What to build:** 通知重送、亂序與保留到期競爭產生一致結果；晚到成功進入補償，不直接恢復訂單造成超賣。

**Blocked by:** 13：完成付款並顯示可信結果

**Status:** ready-for-agent

**Work status:** resolved

- [x] 以受控 mock 事件驗證重送、亂序、前台逾時、取消與 Reservation 到期前後收到成功付款的情境。
- [x] 同一事件不重複扣減、釋放或補償；成功付款與已取消／到期訂單依確認規則對帳，不直接復活訂單。
- [x] 結帳持有人與具 `orders.read` 的客服 API／瀏覽器看見一致的付款及補償狀態；Issue 13 已驗證 Unknown 與 API 暫時中斷不被假定為付款失敗。
- [x] 金額不符／重播事件由 SQL acceptance 驗證隔離；本票 live API 旅程驗證晚到成功的補償失敗、訂單終態、付款與庫存／帳務不變條件。

## 現行 mock 階段範圍（2026-10-13）

受控mock驗證重送、亂序、未知、取消／到期後成功與補償；保留真正應用及SQL一致性，不用mock偽造庫存結果。

詳細以 [全流程mock政策](../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為準；本節優先於以下完整產品／歷史來源中的真實服務或雙人退款要求。既有阻擋編號保留，按本階段契約是否完成判斷；本票目前 mock 驗收結果見下方 Resolution。

## M2本機mock規劃補充（2026-10-13）

需求來源見[M2結帳／訂單詳規](../../planning/kohami/commerce/CHECKOUT_ORDER_PLAN.md)與[訪客存取契約](../../planning/kohami/identity/GUEST_ORDER_ACCESS.md)：保留15分鐘、原操作重送、Unknown不重付、取消／到期晚到成功補償與自己的訂單範圍。歷史狀態矩陣與靜態線框屬規劃資料，當前 SQL／授權／API／瀏覽器結果以本票 Resolution 為準。

本票僅以受控 DEMO mock adapter 驗收；不代表正式 provider sandbox、真實金流或銀行對帳通過。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：R03、R05／AC03、AC05；User Stories 20–24、75。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。


- 政策更新紀錄（2026-10-13）：使用者確認金流、退款、物流、發票先mock，運費／人員先規劃、管理者退款；更新目前範圍但不改產品完成狀態。

- 2026-10-17 初次進度紀錄：付款截止／取消 SQL acceptance 與三語 SSR copy 已通過；當時 live API／客服／補償失敗旅程尚未執行，Commerce harness 在既有退款 worker assertion 中止。後續 fresh-fixture 驗收結果如下。
- 2026-10-17：live dedicated fixture 驗收完成；API／worker 驗證到期後可信成功保持 `Expired` 並建立補償，客服 `orders.read` 讀取一致；補償操作被受控 mock 拒絕後，SQL 驗證 RefundOperation=`Failed`、pending／refunded 均為 0、reservation 已釋放且補償仍可追蹤。Storefront live Playwright 1/1 通過，synthetic rows／session 清理通過。完整證據見[架構驗證紀錄](../../architecture/VERIFICATION.md)。

## Resolution

Issue 14 的本機 mock 交付與驗收已完成，`Work status` 設為 resolved；Issue 15 的 14 依賴因此解除。

- Fresh localhost:14337 SQL Server 2025 dedicated fixture，strict TLS、migrations 0001–0008；fixture 建置時完整 Commerce SQL harness 通過，包含付款期限、取消／到期競爭、晚到成功、重播／亂序、金額不符及退款 worker cases。
- `KOHAMI_LOCAL_DIR=/private/tmp/kohami-issue14-fixture/.local/foundation KOHAMI_FIXTURE_ROOT=/private/tmp/kohami-issue14-fixture python3 scripts/foundation/run-issue06-aftercare-acceptance.py --issue14-late-payment-only`：PASS。API／worker 建立到期後晚到成功；`orders.read` 支援讀取顯示 `Expired`／`Succeeded`／補償必要；refund mock 失敗後 SQL 確認 failed operation、pending=0、refunded=0、reservation released；live Storefront Playwright 1/1。
- runner 清除合成 rows 與私密 session artifact；原有 14336 fixture 未重設或修改。暫存 14337 fixture 及其容器／volume 於驗收後清除。
- 範圍限制：僅證明本機 DEMO/mock adapter，不代表正式付款供應商、銀行對帳或部署驗收。
