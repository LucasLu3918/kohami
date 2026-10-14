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

參照[M3售後退款流程與驗收](../../planning/kohami/M3_AFTERCARE_REFUND_PLAN.md)及[對帳／報表／資料生命週期](../../planning/kohami/RECONCILIATION_DATA_PLAN.md)。本輪沿用既有原子退款額度／MFA 後端，並將受驗收退貨連接至既有退款操作；產品驗收NOT_EXECUTED，原依賴與工作完成狀態維持。

### 2026-10-14 實作紀錄

- 前端以近期 TOTP proof 呼叫既有單一 Owner `refund.execute`；後端從原付款 collection 與快照單位計算退款、以 SQL 交易保留額度及建立唯一操作。未知結果仍保留額度。
- 本輪新增人工唯讀 mock 對帳快照 API。專用 `demo_m0` SQL fixture 的 `Commerce.Tests` 新增 service 整合斷言：回應標示 `mock`／`readOnly`／`ManualOnly`，且呼叫前後 collection ledger 與 DEMO receipt／adjustment 資料未變；與既有 SQL runner 合計 35 項檢查通過。此測試使用只允許 `reconciliation.read` 的 fixture security adapter，不驗證真實 HTTP middleware、Owner／MFA 或瀏覽器旅程。
- API／storefront 未在本次工作階段執行，HTTP 路由與瀏覽器驗收仍未驗；每日排程及差異處置工作台依使用者選擇不納入本 mock 範圍。退款及財務核對驗收清單維持未勾選。

### 2026-10-15 SQL 驗收補充

- 同一專用 SQL runner 已串接退貨檢查建立的 refund case、依原付款快照保留 TWD 500 退款額度、目標 Job ID 的 worker dispatch、可信成功結果將 `Pending` 移至 `Refunded`，並確認 DEMO 收據調整完成。人工對帳回應為 `mock`／`readOnly`／`ManualOnly`，執行前後 collection 與 DEMO 收據／調整 SQL 快照完全相同；runner 共 45 項檢查全數通過。
- worker 測試精準指定合成 Job ID，不處理佇列中的其他工作；退款與收據資料皆清理合成案例。仍未驗真實 HTTP middleware、Owner／MFA、瀏覽器、退款失敗／未知結果旅程或差異處置 UI。每日排程維持停用；驗收清單維持未勾選。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：User Stories 24、74–76、79；N07；V04。
- 基準：KOHAMI 核心商店與模特換裝參考規格 v0.1（含 Comments）、產品規劃書、術語表、現行決策 D396–D446、驗證計畫及 ADR-0001；遵循 Round 18，有效決策不等於歷史編號總數。
- 先確認阻擋票均已完成；`ready-for-agent` 不解除依賴或重大待決條件。前置票須提供可審查證據並完成必要決策確認，不能把 UNKNOWN 或 PROVISIONAL 改寫成既定事實。
- 本票有實作時，按確認旅程補齊必要資料、介面、畫面、權限與錯誤行為，再驗證輸入、輸出、資料、事件及消費者影響；不拆成單獨的 schema／API／UI 層票。
- 沿用已確認測試接縫：瀏覽器端到端旅程為主；付款事件、庫存競爭與復原另用必要外部邊界驗證；素材品質及權利保留人工審核。執行前均屬 NOT_EXECUTED，不以圖片載入或工具成功當成所有驗收通過。
- 票單採用僅批准本機議題建立；後續工作依當次授權與必要決策執行。實作、付費／採購、素材製作、正式部署收款及換裝啟用不得僅由票單狀態推定授權；Phase 3／4 另案處理。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。


- 政策更新紀錄（2026-10-13）：使用者確認金流、退款、物流、發票先mock，運費／人員先規劃、管理者退款；更新目前範圍但不改產品完成狀態。
