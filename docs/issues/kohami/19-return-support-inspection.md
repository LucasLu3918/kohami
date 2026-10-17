# 19: 退貨申請、客服與驗收

**What to build:** 顧客提出售後需求，客服依權限處理，履約人員記錄退貨驗收；拒絕及例外有可理解原因。

**Blocked by:** 18：出貨與顧客配送追蹤

**Status:** ready-for-agent
**Work status:** resolved

- [x] 顧客由自己的訂單提出退貨需求，授權客服處理 Return，履約人員完成 Inspection，顧客能追蹤結果。
- [x] 本機 mock 資格限已送達訂單及未占用付款單位；拒絕要求可理解原因，核准、收件及驗收依角色與狀態控制。法定期限、日曆與 SLA 不在本票決定或推定。
- [x] SQL 驗證未授權客服不能核准、活動中的退貨單位不能重複申請，拒絕後以新 capability／操作重申請成功且不重複改動庫存。
- [x] 記錄 Return 分類、首次回應及人工升級 UTC 節點／耗時；客戶可追蹤拒絕原因與驗收數量，退款執行交由後續退款票。

## 現行 mock 階段範圍（2026-10-13）

退貨、Inspection與入庫採合成案例驗證；退回不自動回補庫存，退款不等於驗收；未採用直接換貨或額外延伸退貨承諾。

詳細以 [全流程mock政策](../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為準；本節優先於以下完整產品／歷史來源中的真實服務或雙人退款要求。既有阻擋編號保留，按本階段契約是否完成判斷。所有新增產品案例仍 NOT_EXECUTED。

## M3詳規追蹤（2026-10-13）

參照[M3售後退款流程與驗收](../../planning/kohami/commerce/AFTERCARE_REFUND_PLAN.md)及[對帳／報表／資料生命週期](../../planning/kohami/commerce/RECONCILIATION_DATA_PLAN.md)。本輪新增本機 API／畫面實作；產品驗收NOT_EXECUTED，原依賴與工作完成狀態維持。

### 2026-10-14 實作紀錄

- 已送達訂單可由本人／有效 guest action 提出退貨；Customer Support 可受理／拒絕並記錄原因，履約角色可記錄驗收結果。
- 只對驗收可再售數量增加合成庫存，產生唯一 movement 記錄；驗收建立待處理退款案件，退款需另行操作。
- SQL／瀏覽器旅程未執行；guest 驗證投遞、案件分流／首次回應、部分收件與重複申請邊界尚未完成驗收。驗收清單維持未勾選。

### 2026-10-15 SQL 驗收補充

- `Commerce.Tests` 專用 `demo_m0` SQL runner 已通過配送完成後的本人退貨申請、Support 狀態推進、履約驗收及庫存變動驗證；只有可再售數量產生一筆 movement，退款單位仍來自原付款快照。共用流程 runner 45 項檢查全數通過。

## Resolution

（2026-10-17；僅 Issue 19 本機 mock 範圍）

- Return 公開投影加入分類、UTC 時間軸、首次客服回應／人工升級耗時、客戶可讀拒絕理由與驗收數量；升級不啟動自動 SLA、通知或日曆計算。
- 退回單位仍以付款快照和交易內 Processing 狀態防重；拒絕釋放單位後可用新操作重申請，已進行中案件仍不能重複占用。Unauthorized Support、有效退款單位、Inspection 庫存 movement 由 SQL 驗收。
- `dotnet build tests/Commerce/Commerce.Tests.csproj --no-restore --configuration Release --verbosity minimal --maxcpucount:1 --nodeReuse:false -p:UseSharedCompilation=false`：PASS，0 warnings／0 errors；專用 loopback-only SQL Server 2025 fixture 上完整 Commerce harness PASS，含 Issue 19 新增 SQL 斷言及 runtime OpenAPI response validation。
- `npm run typecheck`（`apps/storefront`）：PASS；`python3 -m unittest tests.m0.test_issue06_acceptance_runner -v`：21/21 PASS；live `python3 scripts/m0/run-issue06-aftercare-acceptance.py`：Storefront Playwright 1/1 PASS，SQL 回讀確認已送達、已驗收 Return 與待處理 mock refund。
- 未採納法定退貨期限、日曆／營業日或 SLA；時間只記 UTC 節點與經過秒數。沒有真實 carrier、通知、退款執行、稅務憑證或正式營運驗收。下一票是 Issue 20「受控退款與財務核對」。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：User Stories 27、74、77；N06。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。
- 2026-10-17：依使用者授權完成 Issue 19 本機 mock Return／客服／Inspection 旅程；依使用者選擇只記 UTC 節點及耗時，日曆規則保持待決。


- 政策更新紀錄（2026-10-13）：使用者確認金流、退款、物流、發票先mock，運費／人員先規劃、管理者退款；更新目前範圍但不改產品完成狀態。
