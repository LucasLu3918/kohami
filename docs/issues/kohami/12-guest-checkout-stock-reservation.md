# 12: 訪客結帳與可靠庫存承諾

**What to build:** 結帳重新驗證價格及庫存，建立可追蹤的待付款訂單；並行與重送不超賣、不重複建立有效交易，保留交易條件。

**Blocked by:** 11：確認 SKU、購物車與完整費用

**Status:** in-progress — local mock storefront path delivered; API/SQL acceptance remains open

- [ ] 訪客無須註冊即可確認聯絡／配送資料與交易條件，服務端重新核對權威 SKU、價格、費用及可承諾庫存。
- [ ] 成功結帳建立可追蹤的待付款訂單與 Reservation，依契約保存交易條件／Price Snapshot 所需資訊，不宣稱已收款。
- [ ] 使用公開交易邊界的並行購買及重送案例證明不超賣、不重複有效交易；外部替身不偽造內部庫存。
- [ ] 過期價格、缺貨、無效配送與建立失敗有可理解結果；保留到期及釋放按已確認契約維持一致性。
- [ ] 訂單的查詢輸出與資料存取遵守身份政策，旅程測試與交易邊界測試保留可重現證據。

## M2本機mock規劃補充（2026-10-13）

現行補充見[M2結帳／訂單詳規](../../planning/kohami/commerce/CHECKOUT_ORDER_PLAN.md)與[訪客存取契約](../../planning/kohami/identity/GUEST_ORDER_ACCESS.md)：保留15分鐘、原操作重送、Unknown不重付、取消／到期晚到成功補償與自己的訂單範圍。14狀態／5存取與靜態線框只為規劃，真正SQL／授權／API／瀏覽器證據仍NOT_EXECUTED。

本票原依賴／checkbox／Work status不改，MFA保持待決；本輪不實作或將本票標完成。

## 2026-10-17 實作紀錄

- 已在購物車報價後加入訪客 Email／交易條件確認，透過既有 `POST /checkouts` 傳入同一 Quote、Cart ETag、配送方式及冪等請求；成功導向既有待付款結果頁。畫面註明只有結帳成功才保留 15 分鐘，且此 mock 不代表已付款或配送。
- 報價失效、價格／庫存／費用衝突時清除舊報價並重新載入購物車；更換配送方式或重新報價會清除舊條款確認。API 與 SQL 實作未更動。
- Playwright `Issue12` 1/1 使用瀏覽器 API route mocks 通過；這只驗證 Storefront 互動和送出的契約，不是 API／SQL 產品驗收。型別檢查和 Nuxt build 通過。
- 本票仍未完成：新購物車入口對真實專用 SQL fixture 的結帳旅程、公開結帳邊界的並行／冪等／失敗回滾與訂單 scope 驗收需另取得實證；Issue 01 歷史證據不直接替代本票完整驗收。付款維持 mock，M2-AC 仍不可整體宣稱通過。詳見[驗證紀錄](../../architecture/VERIFICATION.md)。

## 2026-10-17 公開 API／SQL 驗收 runner

- 新增 `scripts/m0/run-issue12-checkout-acceptance.py` 與 Commerce SQL fixture helper，涵蓋版本衝突、多 SKU 交易回滾、冪等重播、最後一件庫存競爭及訪客狀態 scope；runner 只建立唯一合成 SKU，完成或失敗時按本次記錄的 ID 清理。
- Release build 與 runner Python 單元測試通過。真實 HTTP journey 尚未通過：現有 fixture 的唯讀 `development-fixture-bootstrap-verify` 回報 `DEV_FIXTURE_BOOTSTRAP_MARKER_INVALID`，API 寫入回 `SERVICE_UNAVAILABLE`。本輪建立的合成 SKU／請求資料已由 SQL cleanup 移除；後續 runner 已加上 bootstrap 唯讀前置檢查，未完成 bootstrap 時會在寫入前停止。
- 使用者於 2026-10-17 決定將本票專用 fixture 啟動及 development bootstrap 的後續作業移出目前執行規劃，改列選配；驗收仍未執行，不得宣稱 API／SQL acceptance、M2-AC 或付款通過。本票維持 in-progress。候選作業只保留於[Issue 12 fixture bootstrap 選配](../../planning/kohami/options/ISSUE12_FIXTURE_BOOTSTRAP_OPTION.md)。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：R03–R04、R06／AC03–AC04、AC06；User Stories 17、19–21、25。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。
- 2026-10-17：使用者決定將本票專用 fixture 啟動與 development bootstrap/TOTP 人工確認流程移出目前執行規劃，僅保留為選配改善；既有驗收證據與未通過狀態不變，程式中的安全檢查不變。

