# 25: 商務監測、安全與災難復原

**What to build:** 驗證權限與秘密保護、去識別化日誌、交易警示，完成備份還原及金流對帳演練，提供 RPO／RTO 證據。

**Blocked by:** 21：交易憑證與退款發票處理（已 resolved）；23：最小化報表與資料生命週期（本機 mock 範圍已 resolved）；24：核心購物可及性與效能（本機桌面範圍已 resolved）

**Status:** ready-for-agent

**Work status:** claimed

- [ ] 商務健康、關鍵交易及重大付款差異有監測、分級警示、負責人與處理程序，可由受控異常觸發驗證。
- [ ] 在授權且不破壞正式資料的環境完成災難備份還原及付款／退款／訂單對帳，量測 RPO ≤ 1 小時、RTO ≤ 8 小時並附條件與結果。
- [ ] 復原後核對庫存、歷史快照、履約及發票一致性；每日對帳與季度演練的排程／責任明確。
- [ ] 定義月可用性口徑但不將單次演練當成每月 99.9% 證據，保留恢復與回退操作指引。

## 現行 mock 階段範圍（2026-10-13）

安全與復原驗證涵蓋本階段mock資料集、真實應用權限及SQL一致性；日誌不含秘密或完整PII；不以mock驗證宣稱真實供應商可信性通過。

詳細以 [全流程mock政策](../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為準；本節優先於以下完整產品／歷史來源中的真實服務或雙人退款要求。既有阻擋編號保留，按本階段契約是否完成判斷。所有新增產品案例仍 NOT_EXECUTED。

## M4規劃追蹤（2026-10-13）

參照[M4驗收／故障復原](../../planning/kohami/delivery/VALIDATION_PLAN.md)與[本機操作](../../planning/kohami/delivery/LOCAL_DEMO_RUNBOOK.md)。本輪補齊規劃與證據模板，產品NOT_EXECUTED；原驗收清單、依賴及工作狀態維持。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：N03–N04、N07；V12；User Stories 79–84。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。


- 政策更新紀錄（2026-10-13）：使用者確認金流、退款、物流、發票先mock，運費／人員先規劃、管理者退款；更新目前範圍但不改產品完成狀態。

## Resolution（2026-10-19；本機健康檢查與可觀測性來源盤點）

- 靜態檢視 `apps/api/Program.cs`、`apps/worker/Program.cs` 及現有一般診斷日誌來源紀錄：API／worker Host 均清除預設 logging providers，沒有 Console、檔案、資料庫或外部診斷 provider；未發現交易監測、告警路由、分級、負責人或事件處理程序。此結論限於檢視的本機 repository 程式與文件，不代表外部主機／服務的完整清冊。
- API `GET /health` 只開啟資料庫連線並回報 `database-connected`、mock mode 與 `requires-native-reconciliation`；`GET /ready` 只核對支援的 schema migration ledger，未就緒時回 `503 not-ready`。兩者都不是交易健康、維護狀態解除或正式啟用證據；本機操作手冊已說明這項限制。
- 初次唯讀探測被 sandbox 以 `not_allowed_local` 拒絕；其後核准對本機 loopback 做一次唯讀 GET，2026-10-19 11:07:35 UTC 的 `/health` 回 200 `database-connected`、`activation=requires-native-reconciliation`、`mode=mock`、`datasetId=kohami_local`；`/ready` 回 503 `not-ready`。因此當時 API 可連資料庫，但 readiness 未通過。探測沒有啟動／停止程序，也沒有變更 SQL、資料、權限、維護閘門或設定。
- 依使用者授權，2026-10-19 11:27:52 UTC 重查 loopback `/ready` 仍回 HTTP 503；11:28:06 UTC `/health` 回 HTTP 200 `database-connected`。隨後在 14336 專屬 SQL Server container 內使用既有 `kohami_api` runtime principal、strict TLS 與 server certificate pin，限定連線至 `Kohami`，唯讀查詢 `OBJECT_ID(N'ops.SchemaMigrations',N'U')` 回 `LEDGER_MISSING`。這確認當前 `/ready` 失敗的根因：主 checkout API 指向的舊 14336 fixture 缺少 migration ledger；migration contract 無法成立。查詢未讀業務資料、連線字串或密碼，沒有執行 migration／seed／寫入，也沒有改動權限、maintenance、服務或 fixture。
- 此子範圍只記錄程式現況；未新增日誌收集、RUM、訪客遙測、外部通知、監控或備份機制。交易警示、責任與處理程序仍未完成；備份／還原、RPO／RTO、復原後一致性及演練也未驗證。Issue 25 維持未結案，所有原驗收項目維持未勾選。

## 依賴重評（2026-10-20）

Issue 21 已 resolved；Issue 23 依使用者確認的本機 mock 範圍已 resolved；Issue 24 依使用者確認的本機桌面交付範圍已 resolved，因此依賴均已滿足，Issue 25 是 S4 下一張可開始的票。原四項驗收仍均未勾選；本票需獨立取得本機適用的監測／告警及復原證據。Issue 23 已延後的備份來源／目標與生命週期未因此解除，不能用空 fixture 推定備份、還原、RPO／RTO 通過；正式環境的監控與每月可用性亦不由本機結果推定。

## 本機安全診斷檢查（2026-10-20）

- 執行既有 Native 專項：`dotnet tests/Native/bin/Release/net10.0/NativeTests.dll --safe-diagnostic`，結果 `PASS native diagnostics expose only bounded reason codes`。此項只驗原生診斷理由碼白名單會拒絕任意錯誤文字及含秘密的字串，不連 SQL、不啟動服務，也不讀取私密設定。
- 嘗試以 `dotnet run --project tests/Native/NativeTests.csproj -c Release -- --safe-diagnostic` 執行時，建置長時間無輸出；已中止該次建置，改以既有 Release DLL 完成上述專項。此輪未取得新建置結果。
- 此項是秘密輸出邊界的單一回歸檢查，不涵蓋 API／worker 日誌、完整授權矩陣、商務告警、備份／還原、對帳、RPO／RTO 或 SLA；Issue 25 維持未結案，原驗收清單不勾選。

## 本機日誌與告警治理決定（2026-10-20）

- 範圍沿用使用者決定：本機執行、合成資料；正式網域不納入目前規劃，RUM、訪客遙測與外部通知不啟用。
- 程式檢視 `apps/api/Program.cs`、`apps/worker/Program.cs`、Commerce worker 與 account inactivity worker：兩個 Host 均清除預設 logging providers，沒有 Console／檔案／資料庫／外部接收端；現有少數 worker 錯誤呼叫只記錄例外型別，沒有例外訊息。Native bounded-reason-code 測試不覆蓋這些 logger 呼叫或 sink。
- **採用的本機預設：不註冊任何日誌 provider、不新增持久化或定期收集、不建立告警路由。**每次本機合成驗收由啟動該次驗收的操作者即時查看 `/health`、`/ready` 與該次測試結果；只在該次操作需要時記下安全的 HTTP status／固定 reason code，不保存原始輸出。終端或 IDE 若自行保存 session，仍須由操作者依本機工具設定處理；產品不負責保存、保留或清除這類內容。
- 非預期失敗時停止該次驗收，保留 fixture 與 maintenance 狀態，由啟動者作為本機處理責任角色檢查；不得自動重試業務寫入、解除 maintenance、執行 migration／reset 或輸出原始例外。這是本機開發／驗收流程，不是正式值班、事故通知或 SLA 流程；未指定具名營運負責人。
- 若未來要加入持久化日誌、定期收集、告警或正式營運監測，須先另行決定欄位與資料分類、保存期限與清除方式、讀取權限、具名負責人、異常條件／分級／處理時限及輸出 sink，並驗證終端／IDE 等接收端的保存行為。允許欄位候選限於固定 component／event code／severity、UTC 時間及不可回指真人或交易的合成 run correlation；拒絕原始 exception/message、request／response、headers、query、秘密、完整 PII、IP、session、訂單／付款識別碼。
- 備份來源／目標、還原授權、對帳範圍與演練責任仍依 Issue 23 延後決議，不能以本機預設推定 RPO／RTO。Issue25 四項原驗收維持未勾選；本決定沒有產品、API、SQL、權限、maintenance 或資料變更，也不代表交易監測／分級告警已驗收。

## 本機存取控制與秘密處理來源盤點（2026-10-20）

- 靜態檢視 `apps/api/Program.cs`、`src/Modules/IdentityAccess/Presentation/SecurityEndpoints.cs`、`src/Modules/IdentityAccess/Infrastructure/Legacy/SecurityService.cs`、`.gitignore`、本機 fixture runner 與秘密生命週期／SQL 最小權限提案。API 綁定 `127.0.0.1`，檢查 loopback、Host 與 port；`/api/v1` 非 GET／HEAD 請求要求核准 Origin 與 antiforgery token。安全端點明確呼叫 `SecurityService`，目前 session、有效 grant、角色／permission、resource scope、期限與 maintenance 狀態在應用層重新檢查；不是由全域 ASP.NET authorization middleware 自動保護所有路由。
- 程式與操作腳本將本機私密設定放在 Git 忽略的 `.local/`，部分 fixture runner 另檢查私密檔案非 symlink 及 mode `0600`，並把 runtime／worker／native 連線用途分開。這只證明指定程式路徑的本機檔案處理；未確認所有秘密的實際 ACL、shell／IDE session 保存、OS credential broker／程序隔離、Data Protection key ring at-rest 保護與復原能力。SQL 最小權限及 key／cert 生命週期文件仍是 `PROPOSED／NOT_EXECUTED`，不能視為已完成部署或有效權限報告。
- 本次未讀取或輸出 `.local/` 私密值，未掃描未納入本輪範圍的私密材料；未連線 SQL、啟停服務、執行 Owner／native authority、migration、grant 或任何資料寫入。沿用既有 `dotnet tests/Native/bin/Release/net10.0/NativeTests.dll --safe-diagnostic` 結果只覆蓋 Native 診斷理由碼白名單，不能代替 API 授權矩陣或秘密保管驗收。
- 結論：目前有可由程式碼與既有回歸證據確認的 loopback／Origin／CSRF／應用權限檢查，以及部分私密檔案慣例；尚無涵蓋所有 API 路由與角色／scope 負向案例的本輪驗收，也無 OS broker、at-rest key protection、有效 SQL grant／secret ACL 證據。未將任何 Issue25 驗收項目標為完成；監測／告警、復原與對帳仍未驗證。

## 本機 API 路由授權矩陣來源盤點（2026-10-20）

- 直接檢視 `apps/api/Program.cs`、IdentityAccess／Commerce／Catalog route map 與 handlers、`tests/Identity/SecurityRulesTests.cs`。矩陣按目前 route map 與呼叫鏈分類，屬靜態來源盤點，不是逐路由 HTTP 負向驗收。
- 共通入口：API listener 固定 loopback；middleware 對 remote IP、Host 與 port 做拒絕檢查；`/api/v1` 非 GET／HEAD 要求設定 Origin 並通過 antiforgery。成功回應 `no-store`。沒有全域 `RequireAuthorization` middleware；業務授權須由各 handler／service 自行落實。

| 路由群 | 目前可由來源確認的控制 | 現有驗證證據與缺口 |
| --- | --- | --- |
| `/auth/csrf`、session／email 驗證端點 | CSRF token 發行；session 由簽章 cookie 對應資料庫 active session；email challenge 綁 browser proof／nonce 並限流。session 查詢／登出可匿名呼叫。 | `tests/Identity/SecurityRulesTests.cs` 未覆蓋完整 HTTP／cookie／Origin 負向矩陣；email sink 與資料庫實際權限未在本輪驗證。 |
| `/admin/access-grants/{id}` | 讀取 grant 需 `access.read`；撤銷需 `access.manage`，兩者都按 grant 對應 User 做 scope 比對；撤銷另要求變更原因、ETag 版本條件與冪等鍵。 | 有 `ScopeMatches`、角色 allowlist 等 Identity 規則測試；未逐 endpoint 驗證匿名、錯誤角色、過期／撤銷 grant、錯誤 scope 與 maintenance 回應。 |
| `/products`、`/products/{slug}` | 公開讀取已發布商品；草稿寫入／更新／發布由 `catalog.write`／Owner 建立檢查，並在交易中重驗 actor／grant。 | Catalog 驗收來源存在，但本輪未執行；未建立 route-by-route 403／401 matrix。 |
| `/admin/stock/{skuId}`、`/admin/stock-adjustments` | `stock.adjust`＋SKU scope；寫入交易內再檢查目前 grant。 | 一般 commerce SQL／stock 驗收並非本輪授權矩陣執行；未驗證各錯誤角色及資源 scope 組合。 |
| `/admin/reports`、`/admin/reconciliation-*`、退款／出貨／退貨／情境操作 | 依 handler 明確要求 `reports.read`、`reconciliation.read/resolve`、`orders.read`、`refund.execute/check`、`shipment.manage`、`support.respond`、`return.inspect` 或 `demo.control`；部分寫入在同一 SQL transaction 重驗有效 grant／security version。 | Identity 單元測試涵蓋角色不能取得 `refund.execute`、Merchandiser 不能取得 dataset-wide `stock.adjust`、未知資源 resolver fail closed；端到端授權拒絕／grant 變更競爭未在本輪執行。 |
| 購物車、quote、checkout、付款、訂單／收據、訪客驗證與退貨申請 | 顧客資源由 command/browser owner、member 身分或短效、綁定 browser／order／intent 的 guest capability 限制；訂單讀取會按本人／訪客能力隱藏未授權資源。 | 有各 Issue 的部分 SQL／browser journey 證據，但不等於全 API route 負向矩陣；本輪未連 API／SQL 執行驗證。 |

- `SecurityRulesTests` 覆蓋 scope 精確匹配／dataset 隔離、核心 permission allowlist、role deny、未知 resource resolver fail closed 與部分密碼規則；它不能證明所有 endpoint 都呼叫了正確 permission，也不能證明 SQL 中有效 grant／session 狀態及 HTTP middleware 行為。
- 範圍限制：未讀取 `.local/` 私密值，未連 API／SQL、未啟動服務，未改 grant／maintenance／資料。未建立新的授權保證或勾選 Issue25 驗收；完整 HTTP endpoint × actor × scope × session 狀態矩陣仍未驗證。全域 Impact Graph／Retrieval Index 不完整或 stale，只人工檢視上述直接 route／handler／測試消費者。

## 本機 API 負向授權驗收計畫（2026-10-20）

狀態：規劃完成；下列執行案例均 `NOT_EXECUTED`。本節只定義後續可重現的驗收面，不代表授權已通過。

### 路由覆蓋與預期拒絕面

| 案例群 | 必須納入的路由 | 負向 actor／狀態與預期觀察 |
| --- | --- | --- |
| 管理權限與資源 scope | `GET /admin/stock/{skuId}`、`POST /admin/stock-adjustments`、`GET /admin/reports`、`GET /admin/reconciliation-report`、`GET /admin/reconciliation-notifications`、`POST /admin/reconciliation-cases/{id}/resolutions`、`POST /admin/reconciliation-cases/{id}/assignments`、`POST /admin/orders/{id}/shipments`、`POST /admin/shipments/{id}/transitions`、`POST /admin/returns/{id}/transitions`、`POST /admin/returns/{id}/inspections`、`POST /admin/refund-cases/{id}/operations`、`POST /admin/order-shipping-refund-cases/{id}/operations`、`POST /admin/payment-compensation-cases/{id}/operations`、`GET /admin/refund-operations/{id}`、`POST /admin/refund-operations/{id}/checks`、`POST /admin/demo/scenarios`、`GET/DELETE /admin/access-grants/{id}`、`POST /admin/products`、`GET/PUT /admin/products/{id}`、`POST /admin/products/{id}/publication-commands` | 對每條路由分別使用匿名、無有效 session、錯誤角色／缺少該 permission、正確 permission 但 scope 指向另一 SKU／訂單／案件／商品／使用者的 actor；確認依該資源的既有隱藏策略回應拒絕，且不得回傳跨 scope 資料。寫入路由另比對操作前後資料／版本／audit／idempotency 狀態，拒絕時必須無業務副作用。 |
| Grant／session 時效 | `GET/DELETE /admin/access-grants/{id}` 及上述全部管理路由 | 同一請求分別帶過期 session、已撤銷 session、security version 過期 session；有效 session 搭配過期 grant、已撤銷 grant。確認失效憑證不能繼續授權；撤銷 grant 的拒絕不能再次改變狀態。 |
| Maintenance gate | 上述所有管理寫入路由；另以有權限的管理讀取路由作對照 | maintenance 開啟時確認受 gate 保護的管理變更拒絕且資料不變；讀取路由依實際政策記錄允許或拒絕，不從寫入 gate 推定讀取行為。此案例需專用隔離 fixture，執行前不得改動現有 fixture 狀態。 |
| 顧客資源所有權 | `GET /carts/current`、`PUT/DELETE /carts/current/items/{skuId}`、`PUT /carts/current/promotion`、`POST /checkout-quotes`、`POST /checkouts`、`GET /checkouts/{id}/status`、`POST /checkouts/{id}/payment-attempts`、`GET /orders/{id}`、`GET /orders`、`GET /orders/{id}/receipts`、`POST /orders/{id}/returns`、`POST /orders/{id}/cancellations` | 無 session 讀取會員專屬訂單清單應拒絕；會員 A 存取會員 B 的 cart／checkout／order／receipt／退貨／取消資源應拒絕且不得洩漏內容。guest capability 分別使用錯誤 browser、錯誤 order／intent、到期、已消耗值，確認拒絕及不消耗其他有效 capability。 |
| 匿名／短效驗證端點 | `GET /auth/csrf`、`POST /auth/sessions`、`GET/DELETE /auth/session`、`GET /auth/account-context`、`POST /auth/email-verification-requests`、`POST /auth/email-verifications`、`POST /guest-order-verifications`、`POST /guest-order-verifications/confirm` | 依各端點匿名契約區分「預期可匿名」與「必須驗證 proof/session」；驗證缺漏、錯誤、過期、重放及 rate limit 邊界。匿名可用不視為越權；不得在拒絕時洩漏帳號／訂單是否存在或輸出秘密。 |
| 公開目錄與入口 middleware | `GET /products`、`GET /products/{slug}`；所有 `/api/v1` 非 GET／HEAD 路由 | 公開讀取只能暴露已發布資料，草稿不可由公開路由取得。每個狀態變更路由分別缺 Origin、錯誤 Origin、缺 antiforgery token、錯誤 token，確認 middleware 拒絕且 handler 未產生副作用；另記錄 loopback／Host／port 拒絕面。 |

### 共通執行設計與限制

- 每個管理 permission 至少有一個正向基線 actor（僅作案例配對）及匿名、錯誤角色／缺 permission、錯誤 scope、過期／撤銷授權拒絕案例；涵蓋路由表列出的每一條 endpoint，不以 handler 群組結果替代路由覆蓋。
- 顧客所有權與 guest capability 使用彼此隔離的合成 actor／資源。狀態變更拒絕案例須在新 dedicated loopback fixture 中比較前後資料、版本及相關稽核／冪等紀錄；不得對現有展示 fixture 執行 reset、migration 或 seed。
- 應先由執行契約／handler 核定各錯誤碼與 HTTP status；同一保護可能按資源存在性使用隱藏式 404。驗收 oracle 必須同時包含「符合既有拒絕契約」及「未洩漏／未改變狀態」，不能只斷言某個通用 401／403。
- middleware 的 Origin／antiforgery 與 handler 的業務授權須分開記錄，避免 middleware 先拒絕後誤稱已驗證業務 permission。避免為執行測試建立或啟用 Owner/native authority；需要該信任根或使用者授權時維持 `BLOCKED`，先回報差異。
- 本輪未連 API／SQL、未啟動服務、未執行案例，未變更 role／grant／maintenance／資料。全域 Impact Graph／Retrieval Index 仍 stale／覆蓋不完整；此計畫依人工檢視的 endpoint map、service handlers、Identity 規則測試與本票矩陣建立，不宣稱全域 consumer 完整。

## kohami-sql 本機健康與 readiness spot check（2026-10-20）

- 2026-10-20 05:31:15 UTC 對現有 loopback API `127.0.0.1:5083` 執行唯讀 `curl --noproxy '*' -sS http://127.0.0.1:5083/health` 與 `/ready`：兩者 HTTP 200。`/health` 回報 `database-connected`、`activation=requires-native-reconciliation`、`mode=mock`、`datasetId=kohami_local`；`/ready` 回報 `ready`、`mode=mock`、同一 dataset。
- 此結果只證明當時 API 可連 `kohami-sql` 且現有 migration ledger 符合目前程式的 readiness contract；不代表 maintenance gate 已解除、native Owner 已建立、錢包遷移已套用、checkout／付款／worker 或復原演練通過。此 probe 沒有寫入 SQL、變更權限／gate 或啟停服務。
- Issue25 原四項驗收全部維持未勾選。商務告警／負責流程、完整 API 負向授權矩陣、備份還原／對帳、RPO／RTO 仍未驗證或依本機範圍延期。
- 2026-10-20 follow-up：新建置 loopback API 5084 對 `/health`、`/ready` 均 HTTP 200；`/health` 為 `database-connected`、`mode=mock`、`datasetId=kohami_local`。這只證明 API／schema readiness，不是 Issue25 原四項驗收或 native activation 證據。`ops.Inspect` 顯示 `Maintenance=True`、`Epoch=1`、activation `requires-native-reconciliation`。0013 已於 dedicated marker-verified `localhost:14349/Kohami/kohami_local` 套用；缺會員的合成入帳拒絕測試無資料寫入。Secure Enclave issuer 因 `OSSTATUS_-34018` 未建立，未改 gate。Issue25 原驗收仍全未勾選。

## 本機開發模式啟用決策與實作狀態（2026-10-20）

- 本機顧客購物 API 不再要求 Apple Developer／Secure Enclave／Owner native authority。新增的 `local-development-bootstrap` 僅接受 `localhost:14349/Kohami/kohami_local` marker、0013 schema、空白身份／grant 及 maintenance=1；建立 synthetic member 與 NT$50,000 測試入帳，在同一 SQL transaction 寫 audit 並開啟本機 gate。Native Operations 與 API Release build 通過；bootstrap 未連資料庫執行，故其實際執行與 live checkout 均為 `NOT_EXECUTED`。
- 簡化只限開發啟動與 synthetic member 建立；loopback、會員 session、Origin／CSRF、資源權限、冪等、交易版本與 SQL 原子性仍是本機 API 契約。Issue25 的完整 route × actor 授權矩陣、監測告警、備份還原與 RPO／RTO 仍未驗證；本決策不將 Issue25 標為完成。
- 同日 `scripts/foundation/run-local.py` 啟動的持續本機預覽使用 `https://localhost:3004`／`http://127.0.0.1:5085`；API readiness 與三語首頁 SSR smoke check PASS。此結果不增加 Issue25 原驗收完成項。

## Comments（續）

- 2026-10-21：使用者決定維持專用 fixture 的 `Maintenance` 開啟，並記錄限制；本工作不暫時關閉或改寫 maintenance 狀態。
- Issue25 authenticated matrix setup helper 在 actor 建立前因 `ISSUE25_FIXTURE_MAINTENANCE_ACTIVE` 中止；因此會員／訪客與 Owner、Merchandiser、CustomerSupport、Analyst 的 live role／scope 案例為 `BLOCKED`，沒有建立 synthetic actor。該次 disposable Compose 專案 `kohami-issue06-41bfda42f9e7` 及專屬 fixture 資源已移除；未連到 14349 demo fixture。
- `issue25-s4-local-security-20261020` 的 CHANGE_IMPACT artifact 記錄較早一版匿名／CSRF 子範圍 11/11 PASS。此歷史結果不覆蓋目前 candidate，也不含 authenticated ownership、role／scope、過期／撤銷 session／grant 或 full route matrix。最新 candidate 的 API listener 未在期限內就緒，該次 HTTP cases 為 `NOT_EXECUTED`；不得沿用舊結果標成目前通過。
- Issue25 四項原驗收均維持未勾選，S4 必要 live 授權／交易證據仍未完成；本決定只記錄 maintenance 限制，不解除 Issue25 或其下游依賴。
