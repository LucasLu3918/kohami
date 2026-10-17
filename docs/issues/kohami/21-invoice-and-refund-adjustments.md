# 21: 交易憑證與退款發票處理

**What to build:** 顧客取得符合已確認規則的憑證；取消、退貨及退款對應正確發票處理，失敗可恢復。

**Blocked by:** 20：受控退款與財務核對

**Status:** resolved

- [x] 依現行 mock 契約，在隔離測試資料集由受控交易結果建立無稅務效力 DEMO 憑證，顧客可查閱自己的資料。
- [x] 取消、退貨、部分／全部退款建立模擬憑證調整，不宣稱有效稅務作廢或折讓，金額與原交易／退款一致。
- [x] 重送、服務拒絕與結果未知可查詢、重試或進入例外，不重複建立有效憑證處理。
- [x] 核對資料存取、營運稽核及財務對帳；正式開票另依實際營運授權。

## Resolution（2026-10-17）

本機 mock 範圍已 resolved。Runtime 原本已有受控付款後建立 DEMO 憑證、退款成功後獨立建立 DEMO 調整、顧客本人訂單查詢，以及調整失敗／未知後重試且不重送退款的處理。本次補上專屬 Storefront browser acceptance，驗證憑證與退款調整以伺服器回傳的同額 TWD 顯示，並標示 DEMO 無稅務效力；未需修改 runtime 行為、API、資料表、migration、事件或授權。

- `apps/storefront/tests/issue21-invoice-adjustments.spec.ts`：以合成 API 驗證本人訂單頁查詢 `/receipts`，顯示 DEMO 憑證／退款調整狀態、同額及非正式發票聲明；Playwright 1/1 PASS。
- `npm run typecheck`（`apps/storefront`）：PASS。
- Commerce 專用 `demo_m0` SQL harness 的 2026-10-15 紀錄已涵蓋退款成功建立唯一調整工作、Unknown 不回滾已成功退款、重試同一調整最終只留一筆 `Adjusted`、不重建或重送退款，共 49 項檢查 PASS。此 SQL harness 本輪未重跑；另參閱 [Issue 20 驗證紀錄](../../architecture/VERIFICATION.md#issue-20-受控退款與財務核對2026-10-17)。
- 正式稅務效力、真實開票服務、外部 provider／銀行及正式營運權限未驗，不屬本機 mock 結案範圍。

## 現行 mock 階段範圍（2026-10-13）

本階段交付無稅務效力的DEMO憑證／調整紀錄，不是正式發票；獨立失敗／未知與去重，不能阻擋退款狀態查閱。

詳細以 [全流程mock政策](../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為準；本節優先於以下完整產品／歷史來源中的真實服務或雙人退款要求。既有阻擋編號保留，按本階段契約是否完成判斷。所有新增產品案例仍 NOT_EXECUTED。

## M3詳規追蹤（2026-10-13）

參照[M3售後退款流程與驗收](../../planning/kohami/commerce/AFTERCARE_REFUND_PLAN.md)及[對帳／報表／資料生命週期](../../planning/kohami/commerce/RECONCILIATION_DATA_PLAN.md)。本輪新增付款成功 DEMO 憑證、退款成功後的 DEMO 調整紀錄，以及本人訂單查閱 API／畫面；產品驗收NOT_EXECUTED，原依賴與工作完成狀態維持。

### 2026-10-14 實作紀錄

- 憑證識別以 `DEMO-` 開頭，明示無稅務效力；付款成功與退款成功才產生相應紀錄，退貨／退款狀態分開呈現。
- 退款成功交易會另建 DEMO 調整工作；憑證調整由後續 worker 工作獨立處理，可呈現成功／失敗／未知，並以受權限保護的 mock 情境重試，不回寫退款狀態。
- SQL／瀏覽器旅程未執行，故尚未用故障注入證明調整失敗不影響退款；驗收清單維持未勾選。

### 2026-10-15 SQL 驗收補充

- `Commerce.Tests` 專用 `demo_m0` SQL runner 已驗證退款成功產生唯一 DEMO 調整工作、worker 依目標 Job ID 完成調整，顧客收據查詢呈現 `Adjusted` 且 `taxEffect=none`。與配送／退貨／退款流程合計 45 項檢查全數通過。
- 此為 service＋SQL 成功路徑；HTTP middleware、瀏覽器、調整失敗／未知結果、去重重試及正式稅務效力仍未驗。原驗收清單維持未勾選。

### 2026-10-15 未知結果與重試補驗

- 專用 `demo_m0` SQL runner 增至 49 項檢查全數通過。DEMO 調整未知時不回滾已成功退款；受控 mock 情境可重試同一調整，最終只有一筆無稅務效力的 `Adjusted` 紀錄，也不會重建或重送退款。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：User Stories 29、76–77；V05。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。


- 政策更新紀錄（2026-10-13）：使用者確認金流、退款、物流、發票先mock，運費／人員先規劃、管理者退款；更新目前範圍但不改產品完成狀態。
