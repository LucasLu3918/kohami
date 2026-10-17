# 18: 出貨與顧客配送追蹤

**What to build:** 授權人員依可信付款結果出貨；訪客／會員查看配送進度，重複或異常物流事件不破壞狀態。

**Blocked by:** 05：驗證物流與電子發票契約；16：會員購物與受保護的訂單紀錄

**Status:** ready-for-agent

**Work status:** claimed（Issue 18 本機 mock UTC 配送追蹤）

- [ ] 授權人員僅依可信交易條件建立模擬 Shipment，從模擬出貨操作、示意配送資訊至顧客訂單追蹤可驗證。
- [ ] 訪客／會員只查閱自己的配送資料，非履約角色無法越權出貨或修改狀態。
- [ ] 物流通知重送、亂序、不合法轉移與服務失敗可恢復，不把未付款或待確認結果當成可履約證據。
- [x] 記錄可信 Primary 模擬付款確認、建立出貨、已出貨及已送達 UTC 節點與耗時；不計算營業日或宣稱達成 SLA。

## 現行 mock 階段範圍（2026-10-13）

依05 mock契約做模擬出貨與追蹤；Owner或授權Support推進狀態；未付／未知／取消不出貨，無真實寄送與承運連結。

詳細以 [全流程mock政策](../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為準；本節優先於以下完整產品／歷史來源中的真實服務或雙人退款要求。既有阻擋編號保留，按本階段契約是否完成判斷。所有新增產品案例仍 NOT_EXECUTED。

## M3詳規追蹤（2026-10-13）

參照[M3售後退款流程與驗收](../../planning/kohami/commerce/AFTERCARE_REFUND_PLAN.md)及[對帳／報表／資料生命週期](../../planning/kohami/commerce/RECONCILIATION_DATA_PLAN.md)。本輪新增本機 API／畫面實作；產品驗收NOT_EXECUTED，原依賴與工作完成狀態維持。

### 2026-10-14 實作紀錄

- 新增受權限與訂單版本保護的模擬 Shipment 建立、狀態轉移及事件識別去重；只允許已確認付款的訂單建立，無真實承運商資料。
- 訂單讀取回傳配送狀態與歷程；前端新增本人訂單檢視及管理端操作表單。
- 編譯未取得結果，SQL／瀏覽器旅程未執行；營業日計時、外部物流通知重送／亂序與恢復驗收尚未完成。驗收清單維持未勾選。

### 2026-10-15 SQL 驗收補充

- `Commerce.Tests` 專用 `demo_m0` SQL runner 已通過配送建立、Pending→Shipped→InTransit→Delivered、相同事件重送不重複寫入、本人訂單查閱及配送歷程隱去員工身分。共用流程 runner 45 項檢查全數通過。
- 回歸測試發現並修正無原因配送事件重送時，null `reason` 被當必填文字讀取而拒絕的問題；衝突事件仍由既有狀態／原因比對拒絕。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：User Stories 26、77、79；N05。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。


- 政策更新紀錄（2026-10-13）：使用者確認金流、退款、物流、發票先mock，運費／人員先規劃、管理者退款；更新目前範圍但不改產品完成狀態。


## 2026-10-17 本機 mock 實作進度

- 顧客本人訂單摘要新增安全配送歷程、可信 Primary 付款確認 UTC 時點及付款確認→出貨建立、出貨建立→已出貨、已出貨→已送達的耗時秒數；對外歷程不含員工、事件識別碼或內部原因。Storefront 顯示三語 mock 配送時間軸，明示沒有真實包裹／承運商追蹤；時間以 UTC 顯示。日曆與 SLA 不計算。
- `dotnet build tests/Commerce/Commerce.Tests.csproj --no-restore --configuration Release`：PASS，0 warnings／0 errors。`npm run typecheck`、`npm run build`：PASS。`npx playwright test tests/issue18-shipment-tracking.spec.ts --reporter=list`：PASS，1/1，使用合成 API 回應。`python3 -m json.tool docs/contracts/runtime/Core.openapi.json` 與 `git diff --check`：PASS。
- SQL 行為斷言已新增，但未取得 Issue 18 SQL PASS：整體 Commerce runner 先於 Issue 18 在既有 Issue 16 驗收遇到 `PRODUCT_UNAVAILABLE`；暫略過該前置後又在既有退款驗收「refund operation enters the mock worker once」中止，均未到配送斷言。未重設或修改展示資料。Work status 保持 claimed，驗收核取方框不勾選。

### 2026-10-17 專用 SQL fixture 驗收補充

- Issue 16 的會員 checkout fixture 現在在 dedicated SQL 測試中暫備一件 `sku_demo_01` 庫存，並於 `finally` 還原原始庫存，避免測試結果依賴展示 SKU 的先前庫存狀態。
- 修正 Issue 18 SQL assertion 將 `shippedToDeliveredSeconds` 誤讀為 `shipmentToDeliveredSeconds` 的欄位拼字；公開 API、Storefront 與契約使用的欄位名稱一致。
- Native／Commerce Release build 均 PASS，0 warnings／0 errors。`python3 scripts/m0/provision-issue06-fixture.py --port 14339` 建立全新 loopback-only SQL Server 2025 fixture，strict TLS 與 migrations 0001–0008（含 password-only auth）通過；完整 Commerce SQL harness PASS，Issue 18 的 UTC 與耗時斷言也 PASS。
- 驗收使用合成資料；專用 Compose containers、volumes、network 及私有 fixture 設定已依 exact project marker 移除。既有 14336 fixture 未作 schema migration 或 reset。
- 本機服務／SQL integration browser journey 尚未執行，因此 Work status 仍為 claimed；其他未驗收項目維持未勾選。
