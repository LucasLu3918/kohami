# 20: 受控退款與財務核對

**What to build:** 依核准及金額上限執行退款，顧客看見可信狀態；重送、未知結果與差異可對帳和追溯。

**Blocked by:** 15：每日對帳與付款異常處理；19：退貨申請、客服與驗收

**Status:** ready-for-agent

- [ ] 從已符合條件的售後／補償案件、具退款權限單一 Owner 操作、受控後端 mock 退款至顧客狀態與財務核對的完整旅程可驗證。
- [ ] 依確認金額精度、分攤與退款上限執行，不超退；保留操作識別與稽核，超出角色／可退額度不得放行。
- [ ] 重送、重複通知、未知退款與服務失敗不造成重複有效退款，結果依可信查詢／通知確認。
- [ ] 每日對帳涵蓋付款、退款、訂單及補償，差異與處置可稽核；不能存取他人退款或繞過核准。

## 現行 mock 階段範圍（2026-10-13）

Owner具退款權限即可單人操作mock，不要求另一人；後端驗證MFA、範圍、版本、原快照金額上限及未知額度保留。處理中／完成／失敗／待確認皆明示模擬。

詳細以 [全流程mock政策](../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為準；本節優先於以下完整產品／歷史來源中的真實服務或雙人退款要求。既有阻擋編號保留，按本階段契約是否完成判斷。所有新增產品案例仍 NOT_EXECUTED。

## M3詳規追蹤（2026-10-13）

參照[M3售後退款流程與驗收](../../planning/kohami/commerce/AFTERCARE_REFUND_PLAN.md)及[對帳／報表／資料生命週期](../../planning/kohami/commerce/RECONCILIATION_DATA_PLAN.md)。本輪沿用既有原子退款額度／MFA 後端，並將受驗收退貨連接至既有退款操作；產品驗收NOT_EXECUTED，原依賴與工作完成狀態維持。

### 2026-10-14 實作紀錄

- 前端以近期 TOTP proof 呼叫既有單一 Owner `refund.execute`；後端從原付款 collection 與快照單位計算退款、以 SQL 交易保留額度及建立唯一操作。未知結果仍保留額度。
- 本輪新增人工唯讀 mock 對帳快照 API。專用 `demo_m0` SQL fixture 的 `Commerce.Tests` 新增 service 整合斷言：回應標示 `mock`／`readOnly`／`ManualOnly`，且呼叫前後 collection ledger 與 DEMO receipt／adjustment 資料未變；與既有 SQL runner 合計 35 項檢查通過。此測試使用只允許 `reconciliation.read` 的 fixture security adapter，不驗證真實 HTTP middleware、Owner／MFA 或瀏覽器旅程。
- API／storefront 未在本次工作階段執行，HTTP 路由與瀏覽器驗收仍未驗；每日排程及差異處置工作台依使用者選擇不納入本 mock 範圍。退款及財務核對驗收清單維持未勾選。

### 2026-10-15 SQL 驗收補充

- 同一專用 SQL runner 已串接退貨檢查建立的 refund case、依原付款快照保留 TWD 500 退款額度、目標 Job ID 的 worker dispatch、可信成功結果將 `Pending` 移至 `Refunded`，並確認 DEMO 收據調整完成。人工對帳回應為 `mock`／`readOnly`／`ManualOnly`，執行前後 collection 與 DEMO 收據／調整 SQL 快照完全相同；runner 共 45 項檢查全數通過。
- worker 測試精準指定合成 Job ID，不處理佇列中的其他工作；退款與收據資料皆清理合成案例。仍未驗真實 HTTP middleware、Owner／MFA、瀏覽器、退款失敗／未知結果旅程或差異處置 UI。每日排程維持停用；驗收清單維持未勾選。

### 2026-10-15 未知結果與重試補驗

- `Commerce.Tests` 專用 `demo_m0` SQL runner 增至 49 項檢查全數通過：退款 worker 回報未知後，TWD 500 仍留在 `Pending`、`Refunded` 為 0；同一原操作的可信成功結果才將額度結算一次。
- DEMO 調整先進入 `Unknown` 時，原退款仍維持 `Succeeded` 且台帳仍只記 TWD 500；授權的 mock 情境重試既有調整後變為 `Adjusted`，只保留一筆調整且未再建立退款。
- 這是合成 SQL／service／worker 驗證，使用 fixture security adapter；HTTP middleware、真實 Owner／MFA、瀏覽器及每日排程仍未驗。驗收清單與 Work status 不變。
- 本機 API 唯讀 smoke：`GET /health` 回 200、`database-connected`／`mock`／`demo_m0`；匿名 `GET /api/v1/admin/reconciliation-report` 回 401 `AUTHENTICATION_REQUIRED`。只證明匿名請求遭 HTTP 授權拒絕；未測合法 Owner／MFA、前端旅程或報表資料寫入行為。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：User Stories 24、74–76、79；N07；V04。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。


- 政策更新紀錄（2026-10-13）：使用者確認金流、退款、物流、發票先mock，運費／人員先規劃、管理者退款；更新目前範圍但不改產品完成狀態。
