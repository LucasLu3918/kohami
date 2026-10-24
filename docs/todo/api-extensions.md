# KOHAMI API：模組額外路由（來源碼盤點）

> `main@0d54903e`：由 `MapFoundationCommerce`、`MapFoundationCatalog`、`MapSecurity` 與 `Core.openapi.json` 差集生成。下列路由**程式已掛載**，但不等於已納入 runtime OpenAPI 或所有端點通過最新全流程測試。若 Handler 未明確提供欄位則保留待驗，不虛構 req/res。核心 27 項見 [api-spec.md](api-spec.md)。

## 共同 request/response 及授權

- API Prefix：`/api/v1`；對修改操作送同源 `Origin`、`X-CSRF-TOKEN` 及 `Cookie`。視 Handler `Write`、`Expected` 額外送 `Idempotency-Key`、`If-Match`、`X-Change-Reason`；請由來源驗證必填性，**不可一律省略**。
- Body：JSON `Content-Type: application/json`；GET 無 body；大於 64 KiB 由代理層拒絕。回應以 JSON、`no-store`；例外為 `application/problem+json`，包含 `code/status/traceId`。
- `/admin/**` 皆以受保護管理員權限／資源 Scope 控制；`/wallet/current` 與 `/wallet/top-ups` 僅登入本人會員，匿名不可讀取他人餘額。
- mock 模式完全由前端 `demo-api.ts` 模擬，不保證與 SQL API 全欄一致；切到 backend 模式時須經 `server/api/v1/[...path].ts`。資金結果必須查狀態，不可用操作已送出代表入帳成功。

## 差集路由（26 項）

### GET /api/v1/admin/stock/{skuId}

- **Handler**：`Stock`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs)）
- **Path / Query**：`skuId`
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：以 handler 與測試驗證 2xx/4xx/5xx（尚未納入 runtime 契約）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/stock-adjustments

- **Handler**：`AdjustStock`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：無明確 JSON body 或由 handler 內部產生
- **Res**：以 handler 與測試驗證 2xx/4xx/5xx（尚未納入 runtime 契約）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/admin/reports

- **Handler**：`AggregateReport`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/admin/reconciliation-report

- **Handler**：`ReconciliationReport`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/admin/reconciliation-notifications

- **Handler**：`ReconciliationNotifications`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/reconciliation-cases/{id}/resolutions

- **Handler**：`ResolveReconciliationCase`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：action: string；expectedVersion: string；reason: string；evidenceRef: string
- **Res**：以 handler 與測試驗證 2xx/4xx/5xx（尚未納入 runtime 契約）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/reconciliation-cases/{id}/assignments

- **Handler**：`AssignReconciliationCase`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：assigneeEmail: string；expectedVersion: string；reason: string
- **Res**：以 handler 與測試驗證 2xx/4xx/5xx（尚未納入 runtime 契約）；失敗以 problem `code` 處理，服務異常不能視為成功。

### DELETE /api/v1/carts/current/items/{skuId}

- **Handler**：`RemoveCartItem`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs)）
- **Path / Query**：`skuId`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：無明確 JSON body 或由 handler 內部產生
- **Res**：HTTP 200（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### PUT /api/v1/carts/current/promotion

- **Handler**：`SetCartPromotion`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：JSON body 欄位未能靜態抽取，必須對照 handler 與測試
- **Res**：HTTP 200（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/wallet/current

- **Handler**：`CurrentWallet`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/wallet/top-ups

- **Handler**：`TopUpWallet`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：amountTwd: integer；cardNumber: string
- **Res**：HTTP 201（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/wallet-credits

- **Handler**：`AdminWalletCredit`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：memberUserId: string；amountTwd: integer；reason: string
- **Res**：HTTP 201（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/orders

- **Handler**：`Orders`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/orders/{id}/receipts

- **Handler**：`Receipts`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/orders/{id}/returns

- **Handler**：`RequestReturn`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：orderLineId: string；quantity: integer；reason: string
- **Res**：HTTP 201（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/orders/{id}/shipments

- **Handler**：`CreateShipment`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：expectedVersion: 需核對 handler 型別
- **Res**：HTTP 201（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/shipments/{id}/transitions

- **Handler**：`TransitionShipment`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：state: string；eventId: string；expectedVersion: 需核對 handler 型別
- **Res**：HTTP 200（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/returns/{id}/transitions

- **Handler**：`TransitionReturn`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：state: string；expectedVersion: 需核對 handler 型別
- **Res**：HTTP 200（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/returns/{id}/inspections

- **Handler**：`InspectReturn`（[`commerce endpoint`](../../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：resellableQuantity: integer；reason: string；expectedVersion: 需核對 handler 型別
- **Res**：HTTP 200（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/products

- **Handler**：`Products`（[`catalog endpoint`](../../src/Modules/Catalog/Infrastructure/CatalogEndpoints.cs)；[`implementation`](../../src/Modules/Catalog/Infrastructure/Legacy/CatalogService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/products/{slug}

- **Handler**：`Product`（[`catalog endpoint`](../../src/Modules/Catalog/Infrastructure/CatalogEndpoints.cs)；[`implementation`](../../src/Modules/Catalog/Infrastructure/Legacy/CatalogService.cs)）
- **Path / Query**：`slug`
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/admin/products

- **Handler**：`AdminProducts`（[`catalog endpoint`](../../src/Modules/Catalog/Infrastructure/CatalogEndpoints.cs)；[`implementation`](../../src/Modules/Catalog/Infrastructure/Legacy/CatalogService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/products

- **Handler**：`Create`（[`catalog endpoint`](../../src/Modules/Catalog/Infrastructure/CatalogEndpoints.cs)；[`implementation`](../../src/Modules/Catalog/Infrastructure/Legacy/CatalogService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：JSON body 欄位未能靜態抽取，必須對照 handler 與測試
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/admin/products/{id}

- **Handler**：`GetAdminProductOrDraft`（[`catalog endpoint`](../../src/Modules/Catalog/Infrastructure/CatalogEndpoints.cs)；[`implementation`](../../src/Modules/Catalog/Infrastructure/Legacy/CatalogService.cs)）
- **Path / Query**：`id`
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### PUT /api/v1/admin/products/{id}

- **Handler**：`Update`（[`catalog endpoint`](../../src/Modules/Catalog/Infrastructure/CatalogEndpoints.cs)；[`implementation`](../../src/Modules/Catalog/Infrastructure/Legacy/CatalogService.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：JSON body 欄位未能靜態抽取，必須對照 handler 與測試
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/products/{id}/publication-commands

- **Handler**：`Publish`（[`catalog endpoint`](../../src/Modules/Catalog/Infrastructure/CatalogEndpoints.cs)；[`implementation`](../../src/Modules/Catalog/Infrastructure/Legacy/CatalogService.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：JSON body 欄位未能靜態抽取，必須對照 handler 與測試
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

## 已在程式確認的具體範例

### 會員合成卡儲值（mock，非真實收款）

```http
POST /api/v1/wallet/top-ups
Content-Type: application/json
Origin: https://localhost:3000
X-CSRF-TOKEN: <先由 GET /auth/csrf 取得>
Idempotency-Key: <唯一請求鍵>
Cookie: <登入會員 Cookie>

{"amountTwd":1000,"cardNumber":"<測試卡號>"}
```

- `amountTwd` 範圍 **1–50000**；`cardNumber` 僅接受 Handler 的合成測試卡檢查，不得記錄卡號。成功時 `201`：`entryId`、`mode:"mock"`、`amountTwd`、`availableTwd`。卡號驗證失敗 `CARD_INVALID`；無會員 session `AUTHENTICATION_REQUIRED`。
- `POST /admin/wallet-credits`：Body `memberUserId`、`amountTwd` (1–50000)、`reason` (1–500 字)；需要 `user.manage` 對應 member Scope；`201` 含 `entryId,userId,mode,amountTwd,availableTwd`。
- `GET /wallet/current?limit=...&cursor=...`：本人帳號餘額與 ledger 分頁，`200`：`mode,currency,availableTwd,version,entries[],nextCursor`，entry 有 `id,type,amountTwd,currency,sourceId,createdAt`。
- `POST /admin/orders/{id}/shipments`：`{ "expectedVersion": "..." }`；`201` 回傳模擬配送單（`id,orderId,mode,state,version,createdAt,history,displayNotice`）；只有訂單 Confirmed 與付款 Succeeded、受授權 scope 才可建立。
- `POST /orders/{id}/returns`：`{ "orderLineId": "...", "quantity": 1, "reason": "..." }`；數量限 1–100、理由 1–500 字，必須是本人會員或既有具有效能力的訪客歷史訂單。
- `POST /admin/shipments/{id}/transitions`：`{ "state":"Shipped", "eventId":"...", "expectedVersion":"...", "reason":"..." }`；狀態僅允許 `Pending→Shipped→InTransit→Delivered/Exception`、`Exception→InTransit/Returned` 指定轉移，重播相同 eventId 需相同 payload。

## 必補契約缺口與驗收

1. 將本頁的額外路由各自補成正式 OpenAPI `requestBody`、Path/Query/Header、`responses`（包含 error `Problem`）、cookie security；不要把未批准候選規格直接覆蓋 runtime。
2. 對照每個 Handler 的欄位 allowlist、nullable、大小限制、版本與 idempotency 行為；加入本機 API/SQL 整合與 Playwright 正反案例。比對 demo mock 與 backend response 差異。
3. 優先確認 Wallet 儲值、管理員合成信用入帳、商品發布、報表、退貨狀態、庫存和對帳這些 runtime 子集尚未收錄的操作。無實際 schema 的端點不可宣稱契約完備。
