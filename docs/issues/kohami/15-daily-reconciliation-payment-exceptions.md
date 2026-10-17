# 15: 每日對帳與付款異常處理

**What to build:** 營運人員核對付款與訂單，追蹤差異、補償及重大警示；重複處理不造成重複效果。

**Blocked by:** 14：處理付款逾時、取消與晚到成功

**Status:** ready-for-agent
**Work status:** claimed

- [ ] 按確認排程至少每日核對付款、訂單與補償紀錄，授權營運人員可追蹤一致、差異與未知案件。
- [ ] 重大異常依已定分級通知負責角色，保留去識別化診斷及敏感操作 Audit Record。
- [ ] 重跑對帳或重送處置不重複產生有效交易／補償，差異關閉有可信依據。
- [ ] 測試對帳成功、差異、供應商資料缺漏與排程／查詢失敗；退款納入後續退款票，不因本票先完成而遺漏。

## 現行 mock 階段範圍（2026-10-13）

本階段對帳核對模擬交易台帳與訂單／庫存／退款／補償，不宣稱已和銀行對帳；差異可處置與追溯。

詳細以 [全流程mock政策](../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為準；本節優先於以下完整產品／歷史來源中的真實服務或雙人退款要求。既有阻擋編號保留，按本階段契約是否完成判斷。所有新增產品案例仍 NOT_EXECUTED。

## M2本機mock規劃補充（2026-10-13）

現行補充見[M2結帳／訂單詳規](../../planning/kohami/commerce/CHECKOUT_ORDER_PLAN.md)與[訪客存取契約](../../planning/kohami/identity/GUEST_ORDER_ACCESS.md)：保留15分鐘、原操作重送、Unknown不重付、取消／到期晚到成功補償與自己的訂單範圍。14狀態／5存取與靜態線框只為規劃，真正SQL／授權／API／瀏覽器證據仍NOT_EXECUTED。


### 2026-10-14 實作紀錄

- 新增 `GET /api/v1/admin/reconciliation-report` 人工唯讀 mock 快照，限具 `reconciliation.read` 權限的資料集 Owner；回傳目前 demo_m0 收款、退款保留／成功金額、訂單彙總、憑證例外與來源／金額差異識別碼，不回傳顧客資料、不寫交易狀態。
- 依使用者選擇不啟用每日排程。報表目前沒有營業日區間／固定 cutoff 重跑、差異處置工作台、通知與責任人流程；不可視為每日營運對帳或正式財務報表。
- 當時 SQL／瀏覽器旅程尚未執行；後續驗收狀態見 2026-10-17 更新。

### 2026-10-17 人工唯讀報表驗收

- 依目前核准範圍，完成 `GET /api/v1/admin/reconciliation-report` 的專用 SQL、API 與 Storefront 驗收。報表仍為 `mock`／`readOnly`／`ManualOnly`，能標示 Late／Extra 收款待查、缺少來源訂單、需補償訂單與退款例外；只回傳來源識別與彙總，不回傳顧客聯絡資料，也不改寫收款、訂單、憑證或退款狀態。
- Commerce SQL harness 驗證合成 Late collection 會出現在 `NON_PRIMARY_COLLECTION_REQUIRES_REVIEW`、補償待處理數量大於零、讀取前後 collections／DEMO receipts SQL snapshot 相同。Storefront Playwright 1/1 PASS；重複 API 讀取回傳相同狀態、摘要與 findings。
- 專用 fixture 的 runner 清除本次合成資料與 session；Commerce harness 通過後依精確 marker 移除本次容器、network、volumes 與臨時 workspace。本輪沒有使用主 checkout `.local/m0`。不涉及 schema／migration、真實銀行／provider 對帳或真資金。
- 依使用者決定不啟用每日排程。差異處置／關閉工作台、重大通知與責任人流程，以及完整成功／來源缺漏／查詢失敗矩陣仍未完成；不得宣稱每日營運對帳或本票整體完成。
- 命令與限制見[Issue 15 驗證紀錄](../../architecture/VERIFICATION.md#issue-15-人工唯讀對帳報表驗收2026-10-17)。

### 2026-10-17 來源缺漏分支 SQL 驗收補強

- 專用 Commerce SQL harness 新增合成 `RefundOperation` 缺少來源 collection，以及 `compensationRequired` 訂單缺少 Late／Extra collection 兩例，分別斷言 `REFUND_SOURCE_MISSING` 與 `COMPENSATION_COLLECTION_MISSING`。報表前後快照涵蓋 Collections、Orders、RefundOperations 與 DEMO 收據，驗證查詢未改寫資料；`finally` 以 Issue15 專用 ID 前綴清理合成列。
- 新建隔離 checkout 與 localhost:14339 專用 SQL Server 2025 fixture，Commerce SQL harness 全部通過後，按精確 Compose marker 移除容器、network 與 volumes。主 checkout 的 `.local/m0` 未使用。
- 這只補足既有唯讀 finding 的 SQL 覆蓋，沒有新增異常處置、關閉、重大通知、責任人或查詢失敗流程；Issue 15 維持 `claimed`，每日排程仍不啟用。命令及限制見驗證紀錄。

### 2026-10-17 SQL 查詢失敗回應驗收補強

- 新增專用 runner 模式，於 marker-verified dedicated SQL fixture 暫時拒絕 `m0_api` 讀取 `m0commerce.Collections`，呼叫人工唯讀報表 API，驗證回應為 `503 SERVICE_UNAVAILABLE` 且只有泛化 Problem 欄位；撤銷故障注入後，確認原有物件 `SELECT` 權限與報表讀取恢復。
- 專用 `--issue15-query-failure-only` API／SQL 路徑 PASS，合成資料已清理。首次完整 `--issue15-manual-reconciliation-only` 重跑未找到訂單面板中的 reconciliation 按鈕；根因與後續修復見下方「Storefront 完整驗收修復」。
- 不變更產品 API、錯誤契約、schema 或 migration。Issue 15 仍為 `claimed`，差異處置／關閉、責任人與通知、每日排程及真實金融來源仍未完成。命令、fixture 清理與範圍見[驗證紀錄](../../architecture/VERIFICATION.md#issue-15-sql-查詢失敗回應驗收補強2026-10-17)。

### 2026-10-17 Storefront 完整驗收修復

- 根因：Issue 15 重用 Issue 14 建立的晚到付款訂單，但驗收沒有替該訂單簽發綁定同一瀏覽器的 guest read capability；Playwright 也未載入 `kohami.guest.read`。既有 API 正確以 404 隱藏未授權訂單，故訂單面板及其對帳按鈕不會出現。
- 修正只在驗收 fixture 呼叫既有 `issue06-owner-capabilities` 命令簽發該訂單能力，並由 Playwright 載入該 cookie；沒有改 API、授權規則、schema 或產品行為。
- 修正後在全新 localhost:14339 dedicated fixture 重跑 `--issue15-manual-reconciliation-only`：API／SQL／worker 報表重讀與查詢失敗恢復 PASS，Storefront Playwright 1/1 PASS。runner 清除本次 synthetic rows 與 session；驗收結束後依本次精確 Compose marker 移除 fixture。
- `python3 -m unittest tests.m0.test_issue06_acceptance_runner -v`：19/19 PASS；Python `py_compile`、Nuxt `npm run typecheck` 與 `git diff --check` PASS。Issue 15 整體仍為 `claimed`；排程、差異處置／關閉、責任人／通知及正式 provider／銀行對帳仍未完成。詳見[最新驗證紀錄](../../architecture/VERIFICATION.md#issue-15-storefront-完整驗收修復2026-10-17)。

### 2026-10-17 OP-45 人工差異處置與結案證據

- 依使用者授權新增 `POST /api/v1/admin/reconciliation-cases/{id}/resolutions`。唯讀報表為每個 finding 提供穩定案件 ID、處置狀態、ETag 版本及最近責任操作人；處置以既有 Commerce `Documents` 與 `Idempotency` SQL 儲存，不新增 schema／migration。
- 支援 `QueueOriginalCheck`、`LinkCompensationCase`、`RecordReviewedNotApplicable`、`CloseReconciled`；每次需 `reconciliation.resolve` 權限與目前 dataset/case scope、`If-Match`、`Idempotency-Key`、原因及證據參照。補償連結只接受既有 CollectionCompensation `RefundCase`。結案須先有查核／補償連結紀錄，並確認原 finding 已不再存在；不手動改寫金融狀態或餘額。
- 已結案來源再次出現相同差異時會以 `Reopened` 顯示並允許新版本處置；原歷程仍保留。
- 每筆處置歷程記錄 actor、時間、原因、證據參照與動作，並回報當前 `responsibleActor`。目前提供處置者追溯，尚未提供預先指派角色、重大通知或 Storefront 操作工作台。
- 每日排程仍依使用者決定不啟用；報表仍為 `mock`／`readOnly`／`ManualOnly`。本次只完成手動 SQL/API 服務路徑，不代表 provider／銀行對帳或 Issue 15 整體結案。
- SQL 驗收與限制見[OP-45 驗證紀錄](../../architecture/VERIFICATION.md#issue-15-op-45-人工差異處置sql驗收2026-10-17)。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：N07；User Stories 75–76、79、82–83。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。


- 政策更新紀錄（2026-10-13）：使用者確認金流、退款、物流、發票先mock，運費／人員先規劃、管理者退款；更新目前範圍但不改產品完成狀態。
