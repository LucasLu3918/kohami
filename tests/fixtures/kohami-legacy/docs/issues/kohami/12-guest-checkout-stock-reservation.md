# 12: 訪客結帳與可靠庫存承諾

**What to build:** 結帳重新驗證價格及庫存，建立可追蹤的待付款訂單；並行與重送不超賣、不重複建立有效交易，保留交易條件。

**Blocked by:** 11：確認 SKU、購物車與完整費用

**Status:** complete — local mock storefront and dedicated public API/SQL acceptance passed (2026-10-17)

- [x] 訪客無須註冊即可確認聯絡／配送資料與交易條件；公開 API 對報價、費用、庫存與目前購物車版本重新驗證。
- [x] 成功結帳建立可追蹤的待付款訂單與 Reservation，保存 mock Price Snapshot，不宣稱已收款。
- [x] 公開 HTTP／SQL 並行購買、同 key 重送與失敗回滾證明不超賣、不重複有效交易；庫存驗證使用真 SQL。
- [x] 過期購物車版本、缺貨、無效配送與多 SKU 建立失敗均有契約錯誤結果；庫存扣減與 Reservation 維持原子一致。
- [x] 訪客 checkout 狀態只可由建立該訂單的瀏覽器讀取；runner 留下可重現流程並清理本次合成資料。

## M2本機mock規劃補充（2026-10-13）

現行補充見[M2結帳／訂單詳規](../../planning/kohami/commerce/CHECKOUT_ORDER_PLAN.md)與[訪客存取契約](../../planning/kohami/identity/GUEST_ORDER_ACCESS.md)：保留15分鐘、原操作重送、Unknown不重付、取消／到期晚到成功補償與自己的訂單範圍。14狀態／5存取與靜態線框只為規劃，真正SQL／授權／API／瀏覽器證據仍NOT_EXECUTED。

本票原依賴保留；完成範圍是本機 mock checkout 與本票列出的驗收，不包含沙盒／正式收款或 M2 整體結案。

## 2026-10-17 實作紀錄

- 已在購物車報價後加入訪客 Email／交易條件確認，透過既有 `POST /checkouts` 傳入同一 Quote、Cart ETag、配送方式及冪等請求；成功導向既有待付款結果頁。畫面註明只有結帳成功才保留 15 分鐘，且此 mock 不代表已付款或配送。
- 報價失效、價格／庫存／費用衝突時清除舊報價並重新載入購物車；更換配送方式或重新報價會清除舊條款確認。API 與 SQL 實作未更動。
- Playwright `Issue12` 1/1 使用瀏覽器 API route mocks 通過；這只驗證 Storefront 互動和送出的契約，不是 API／SQL 產品驗收。型別檢查和 Nuxt build 通過。
- 本票仍未完成：新購物車入口對真實專用 SQL fixture 的結帳旅程、公開結帳邊界的並行／冪等／失敗回滾與訂單 scope 驗收需另取得實證；Issue 01 歷史證據不直接替代本票完整驗收。付款維持 mock，M2-AC 仍不可整體宣稱通過。詳見[驗證紀錄](../../architecture/VERIFICATION.md)。

## 2026-10-17 公開 API／SQL 驗收 runner

- 新增 `scripts/foundation/run-issue12-checkout-acceptance.py` 與 Commerce SQL fixture helper，涵蓋無效配送、版本衝突、多 SKU 交易回滾、冪等重播、最後一件庫存競爭及訪客狀態 scope；runner 只建立唯一合成 SKU，完成或失敗時按本次記錄的 ID 清理。
- 專用 Issue 02 SQL 2025 fixture 套用 0001–0008 遷移及 Commerce SQL 驗證通過；真實公開 HTTP／SQL runner 全部通過，證明 412 版本拒絕、409 無效配送／缺貨、回滾、同 key 回放、最後一件競爭僅一方成功，以及另一瀏覽器讀取 checkout 被拒。
- `python3 -m unittest tests.foundation.test_fixture_provisioning tests.foundation.test_issue12_checkout_acceptance -v`：8/8 PASS；Native、Commerce、API Release build 均為 0 warnings／0 errors。驗收後已移除專用容器、volumes、合成帳號私密設定與暫存 workspace。
- 本票更新為 `complete`。付款維持 mock；此結果不代表 M2-AC 或整體 Foundation 驗收完成。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：R03–R04、R06／AC03–AC04、AC06；User Stories 17、19–21、25。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。
- 2026-10-17：使用者決定將本機 fixture 作業延後列為選配；既有驗收證據與未通過狀態不變。

