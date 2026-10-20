# 25: 商務監測、安全與災難復原

**What to build:** 驗證權限與秘密保護、去識別化日誌、交易警示，完成備份還原及金流對帳演練，提供 RPO／RTO 證據。

**Blocked by:** 21：交易憑證與退款發票處理（已 resolved）；23：最小化報表與資料生命週期（本機 mock 範圍已 resolved）；24：核心購物可及性與效能（本機桌面範圍已 resolved）

**Status:** ready-for-agent

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
- 初次唯讀探測被 sandbox 以 `not_allowed_local` 拒絕；其後核准對本機 loopback 做一次唯讀 GET，2026-10-19 11:07:35 UTC 的 `/health` 回 200 `database-connected`、`activation=requires-native-reconciliation`、`mode=mock`、`datasetId=demo_m0`；`/ready` 回 503 `not-ready`。因此當時 API 可連資料庫，但 readiness 未通過。探測沒有啟動／停止程序，也沒有變更 SQL、資料、權限、維護閘門或設定。
- 依使用者授權，2026-10-19 11:27:52 UTC 重查 loopback `/ready` 仍回 HTTP 503；11:28:06 UTC `/health` 回 HTTP 200 `database-connected`。隨後在 14336 專屬 SQL Server container 內使用既有 `m0_api` runtime principal、strict TLS 與 server certificate pin，限定連線至 `KohamiM0`，唯讀查詢 `OBJECT_ID(N'ops.SchemaMigrations',N'U')` 回 `LEDGER_MISSING`。這確認當前 `/ready` 失敗的根因：主 checkout API 指向的舊 14336 fixture 缺少 migration ledger；migration contract 無法成立。查詢未讀業務資料、連線字串或密碼，沒有執行 migration／seed／寫入，也沒有改動權限、maintenance、服務或 fixture。
- 此子範圍只記錄程式現況；未新增日誌收集、RUM、訪客遙測、外部通知、監控或備份機制。交易警示、責任與處理程序仍未完成；備份／還原、RPO／RTO、復原後一致性及演練也未驗證。Issue 25 維持未結案，所有原驗收項目維持未勾選。

## 依賴重評（2026-10-20）

Issue 21 已 resolved；Issue 23 依使用者確認的本機 mock 範圍已 resolved；Issue 24 依使用者確認的本機桌面交付範圍已 resolved，因此依賴均已滿足，Issue 25 是 S4 下一張可開始的票。原四項驗收仍均未勾選；本票需獨立取得本機適用的監測／告警及復原證據。Issue 23 已延後的備份來源／目標與生命週期未因此解除，不能用空 fixture 推定備份、還原、RPO／RTO 通過；正式環境的監控與每月可用性亦不由本機結果推定。

## 本機安全診斷檢查（2026-10-20）

- 執行既有 Native 專項：`dotnet tests/Native/bin/Release/net10.0/NativeTests.dll --safe-diagnostic`，結果 `PASS native diagnostics expose only bounded reason codes`。此項只驗原生診斷理由碼白名單會拒絕任意錯誤文字及含秘密的字串，不連 SQL、不啟動服務，也不讀取私密設定。
- 嘗試以 `dotnet run --project tests/Native/NativeTests.csproj -c Release -- --safe-diagnostic` 執行時，建置長時間無輸出；已中止該次建置，改以既有 Release DLL 完成上述專項。此輪未取得新建置結果。
- 此項是秘密輸出邊界的單一回歸檢查，不涵蓋 API／worker 日誌、完整授權矩陣、商務告警、備份／還原、對帳、RPO／RTO 或 SLA；Issue 25 維持未結案，原驗收清單不勾選。
