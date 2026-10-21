# 20: 受控退款與財務核對

**What to build:** 依核准及金額上限執行退款，顧客看見可信狀態；重送、未知結果與差異可對帳和追溯。

**Blocked by:** 15：每日對帳與付款異常處理；19：退貨申請、客服與驗收

**Status:** ready-for-agent

**Work status:** resolved

- [x] 從已符合條件的售後／補償案件、具退款權限單一 Owner 操作、受控後端 mock 退款至顧客狀態與財務核對的完整旅程可驗證。
- [x] 依確認金額精度、分攤與退款上限執行，不超退；保留操作識別與稽核，超出角色／可退額度不得放行。
- [x] 重送、重複通知、未知退款與服務失敗不造成重複有效退款，結果依可信查詢／通知確認。
- [x] 人工觸發的日常對帳涵蓋付款、退款、訂單及補償，差異與處置可稽核；不能存取他人退款或繞過核准。自動排程維持選配。

## 現行 mock 階段範圍（2026-10-13）

Owner具退款權限即可單人操作mock，不要求另一人；後端驗證授權範圍、版本、原快照金額上限及未知額度保留。處理中／完成／失敗／待確認皆明示模擬。

詳細以 [全流程mock政策](../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為準；本節優先於以下完整產品／歷史來源中的真實服務或雙人退款要求。既有阻擋編號保留，按本階段契約是否完成判斷。實測狀態與限制見下方 Resolution。

## M3詳規追蹤（2026-10-13）


### 2026-10-14 實作紀錄

- API／storefront 未在本次工作階段執行，HTTP 路由與瀏覽器驗收仍未驗；每日排程及差異處置工作台依使用者選擇不納入本 mock 範圍。退款及財務核對驗收清單維持未勾選。

### 2026-10-15 SQL 驗收補充

- 同一專用 SQL runner 已串接退貨檢查建立的 refund case、依原付款快照保留 TWD 500 退款額度、目標 Job ID 的 worker dispatch、可信成功結果將 `Pending` 移至 `Refunded`，並確認 DEMO 收據調整完成。人工對帳回應為 `mock`／`readOnly`／`ManualOnly`，執行前後 collection 與 DEMO 收據／調整 SQL 快照完全相同；runner 共 45 項檢查全數通過。

### 2026-10-15 未知結果與重試補驗

- `Commerce.Tests` 專用 `kohami_local` SQL runner 增至 49 項檢查全數通過：退款 worker 回報未知後，TWD 500 仍留在 `Pending`、`Refunded` 為 0；同一原操作的可信成功結果才將額度結算一次。
- DEMO 調整先進入 `Unknown` 時，原退款仍維持 `Succeeded` 且台帳仍只記 TWD 500；授權的 mock 情境重試既有調整後變為 `Adjusted`，只保留一筆調整且未再建立退款。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：User Stories 24、74–76、79；N07；V04。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。


- 政策更新紀錄（2026-10-13）：使用者確認金流、退款、物流、發票先mock，運費／人員先規劃、管理者退款；更新目前範圍但不改產品完成狀態。

## Resolution

2026-10-17 完成目前核准的本機 mock 範圍。顧客查詢本人訂單時可看到伺服器投影的退款申請、處理中、待確認、成功、失敗筆數，以及已退款與尚保留金額；顯示繁中／日文／英文文案與台幣格式，操作 ID／Actor 不進入顧客摘要。前端只呈現後端結果，不自行決定資格、金額或終態。

- 退款授權、原付款快照金額與商品單位保留、shipping slot 上限、併發競爭、未知結果及可信查詢沿用既有 Commerce 交易；新 dedicated SQL Server 2025 fixture 的完整 Commerce harness PASS。Unknown 保留全額，可信結果只結算原 operation 一次。
- live API／worker／SQL／Storefront 售後旅程 PASS 1/1：合成已付款訂單完成配送、退貨及驗收後發出 mock 退款；Storefront 從本人訂單投影顯示回傳狀態、相同 TWD 金額及模擬聲明，不顯示 operation ID。SQL 回讀確認訂單、配送、退貨與退款 operation 持久化。
- 人工唯讀對帳報表涵蓋 collection、退款 pending／succeeded、訂單與補償差異；SQL harness 確認缺來源／補償差異、排除顧客聯絡資料，且報表不改變 collection、退款、訂單或 DEMO 收據資料。回應維持 `mock`／`readOnly`／`ManualOnly`。
- 本次沒有 schema／migration 或 OpenAPI 欄位變更；使用既有 `CommerceRefundSummary` 合約。

自動對帳排程、provider／銀行核對、真實資金移動、正式發票／稅務判定及正式營運均不在此本機 mock 驗收範圍；排程候選見[對帳排程選配](../../planning/kohami/options/RECONCILIATION_SCHEDULING_OPTION.md)。下一張未完成且依賴已解除的票為 Issue 21「交易憑證與退款發票處理」。完整命令、candidate 與環境限制見[驗證紀錄](../../architecture/VERIFICATION.md#issue-20-受控退款與財務核對2026-10-17)。
