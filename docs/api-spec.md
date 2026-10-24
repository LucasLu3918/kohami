> 2026-10-24 · 靜態來源基準 `main@7427c94`。現行程式／SQL DDL 為權威；不是此次執行的本機 SQL、瀏覽器或正式金流驗收。

# 現行 HTTP API 規格

此處僅列已掛載 endpoint。27 項在 [runtime OpenAPI](contracts/runtime/Core.openapi.json) 定義，另 26 項以程式入口/handler 靜態對照，尚未全部補入 runtime OpenAPI。Headers、path、query、req/res、錯誤語意須以程式為準，**缺失 schema 不得臆造**。

## A. Runtime OpenAPI（27 條）

## 已存在的正式運行契約

> 稽核：2026-10-24；基準 `main@7427c94`。**來源**：[`Core.openapi.json`](contracts/runtime/Core.openapi.json)、[`apps/api/Program.cs`](../apps/api/Program.cs)。本檔為來源規格的索引／展開快照，詳細 constraints、headers、組合型別、全量狀態碼與 schema 仍以 JSON 為準；契約宣告 ≠ 目前服務正常啟動或正式上線。模組額外 API 見 [本檔 B 節](#additional-routes)。

## 共同 HTTP 行為

- 本機前端呼叫 `/api/v1/*`，Nuxt server proxy 將請求送至 loopback API；**API 本體預設僅 `http://127.0.0.1:5080`**，port 支援 5080–5099。使用 `https://localhost:3000/api/v1` 是 OpenAPI 範例 base URL，不代表已公開部署。
- session/瀏覽器識別以 HttpOnly Cookie 維護；修改資料時必須先 `GET /api/v1/auth/csrf` 取得 `requestToken` 並帶 `X-CSRF-TOKEN`，以及與設定相符之 `Origin`。API Host 不允許非 loopback IP、錯 Host。
- proxy 僅轉送 `cookie`、`origin`、`content-type`、`x-csrf-token`、`idempotency-key`、`if-match`、`x-change-reason` 等 allowlist headers；請求 body 最大 65,536 bytes；錯誤不得洩漏 SQL、憑證或 proof。
- 寫入操作視宣告要求提供 `Idempotency-Key`、`If-Match`/版本、`X-Change-Reason`，不得省略後推定交易成功；保持 `Cache-Control: no-store`。
- `200/201/202/204` 依個別端點；`202` 代表接受處理不等於已成功付款、退款或配送。查詢狀態端點才能取得確認後結果。
- 錯誤 Envelope：`{ "type":"about:blank", "title":"...", "status":403, "code":"CSRF_INVALID", "traceId":"..." }` 為格式示意；以實際 `Problem` schema 為準。

## 已收錄操作（27 項）

### GET /auth/csrf

- **operationId**：`getCsrf`；PROPOSED Foundation subset；NOT_EXECUTED。 建立／取得穩定匿名BrowserId與dataset binding，回request token；bootstrap與token不可快取。
- **授權與 headers/path/query**：無額外宣告參數；同樣受 API Host 全域 Cookie、Origin 與 CSRF 規則約束
- **Req Content-Type**：無 JSON body；**body**：無
- **Res**：`200` `requestToken` **必填**: `string`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /auth/sessions

- **operationId**：`createSession`；PROPOSED Foundation subset；NOT_EXECUTED。
- **授權與 headers/path/query**：`X-CSRF-TOKEN` (header,required) `string`
- **Req Content-Type**：application/json；**body**：`email` **必填**: `string/email`; `password` **必填**: `string`
- **Res**：`200` (無可展開欄位；請參照來源 schema)
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `413`, `415`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### GET /auth/session

- **operationId**：`getSession`；PROPOSED Foundation subset；NOT_EXECUTED。
- **授權與 headers/path/query**：無額外宣告參數；同樣受 API Host 全域 Cookie、Origin 與 CSRF 規則約束
- **Req Content-Type**：無 JSON body；**body**：無
- **Res**：`200` (無可展開欄位；請參照來源 schema)
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### DELETE /auth/session

- **operationId**：`deleteSession`；當前管理/member或受限stage登出；已登出204；撤server session/stage/近期驗證並清所有auth cookie，不取消訂單。
- **授權與 headers/path/query**：`X-CSRF-TOKEN` (header,required) `string`；`Origin` (header,required) `string/uri`
- **Req Content-Type**：無 JSON body；**body**：無
- **Res**：`204` (無回應 body)
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /carts

- **operationId**：`createCart`；PROPOSED Foundation subset；NOT_EXECUTED。 初次cart去重以bootstrap匿名BrowserId為scope，不用尚未存在CartId；同browser同key重送不重建。
- **授權與 headers/path/query**：`X-CSRF-TOKEN` (header,required) `string`；`Idempotency-Key` (header,required) `string`
- **Req Content-Type**：無 JSON body；**body**：無
- **Res**：`201` `id` **必填**: `string`; `mode` **必填**: `any`; `version` **必填**: `string`; `items` **必填**: `array<object [CommerceCartItem]>`; `promotionCode` **必填**: `string|null`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### GET /carts/current

- **operationId**：`getCurrentCart`；PROPOSED Foundation subset；NOT_EXECUTED。
- **授權與 headers/path/query**：無額外宣告參數；同樣受 API Host 全域 Cookie、Origin 與 CSRF 規則約束
- **Req Content-Type**：無 JSON body；**body**：無
- **Res**：`200` `id` **必填**: `string`; `mode` **必填**: `any`; `version` **必填**: `string`; `items` **必填**: `array<object [CommerceCartItem]>`; `promotionCode` **必填**: `string|null`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### PUT /carts/current/items/{skuId}

- **operationId**：`setCartItem`；PROPOSED Foundation subset；NOT_EXECUTED。
- **授權與 headers/path/query**：`skuId` (path,required) `string`；`X-CSRF-TOKEN` (header,required) `string`；`Idempotency-Key` (header,required) `string`；`If-Match` (header,required) `string`
- **Req Content-Type**：application/json；**body**：`quantity` **必填**: `integer`
- **Res**：`200` `id` **必填**: `string`; `mode` **必填**: `any`; `version` **必填**: `string`; `items` **必填**: `array<object [CommerceCartItem]>`; `promotionCode` **必填**: `string|null`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `413`, `415`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /checkout-quotes

- **operationId**：`createQuote`；PROPOSED Foundation subset；NOT_EXECUTED。
- **授權與 headers/path/query**：`X-CSRF-TOKEN` (header,required) `string`；`Idempotency-Key` (header,required) `string`
- **Req Content-Type**：application/json；**body**：`cartVersion` **必填**: `string`; `deliveryMethod` **必填**: `any {mock_home,mock_pickup}`; `region` **必填**: `any {demo_supported,demo_unsupported}`
- **Res**：`201` `id` **必填**: `string`; `mode` **必填**: `any`; `expiresAt` **必填**: `string/date-time`; `snapshot` **必填**: `object`; `version` **必填**: `string`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `413`, `415`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /checkouts

- **operationId**：`createCheckout`；PROPOSED Foundation subset；NOT_EXECUTED。建立新 checkout／order 必須為已登入會員；聯絡 Email 必須等於該會員帳號 Email。訪客仍可建立購物車與報價，但此操作回覆 AUTHENTICATION_REQUIRED。paymentMethod 可選 wallet、debit_card 或 credit_card；卡片共用模擬驗證與付款流程，wallet 僅會員本人可使用，伺服器在同一交易扣抵並確認訂單。省略欄位視為 card；餘額不足不建立 checkout／order／reservation。
- **授權與 headers/path/query**：`X-CSRF-TOKEN` (header,required) `string`；`Idempotency-Key` (header,required) `string`
- **Req Content-Type**：application/json；**body**：`quoteId` **必填**: `string`; `cartVersion` **必填**: `string`; `contact` **必填**: `object`; `delivery` **必填**: `object`; `confirmed` **必填**: `any`; `paymentMethod`: `any {card,wallet,debit_card,credit_card}`
- **Res**：`201` `checkoutId` **必填**: `string`; `orderId` **必填**: `string`; `mode` **必填**: `any`; `version` **必填**: `string`; `expiresAt` **必填**: `string/date-time`; `snapshot` **必填**: `object`; `statusUrl` **必填**: `string`; `capabilities` **必填**: `array<any {checkout.read,checkout.pay}>`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `413`, `415`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### GET /checkouts/{id}/status

- **operationId**：`getCheckoutStatus`；PROPOSED Foundation subset；NOT_EXECUTED。
- **授權與 headers/path/query**：`id` (path,required) `string`
- **Req Content-Type**：無 JSON body；**body**：無
- **Res**：`200` `checkoutId` **必填**: `string`; `mode` **必填**: `any`; `version` **必填**: `string`; `orderState` **必填**: `any {AwaitingPayment,Confirmed,Cancelled,Expired}`; `paymentState` **必填**: `any {Created,Processing,Succeeded,Failed,Unknown}`; `paymentMethod` **必填**: `any {wallet,debit_card,credit_card,card}`; `compensationRequired` **必填**: `boolean`; `expiresAt` **必填**: `string/date-time`; `snapshot` **必填**: `object`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /checkouts/{id}/payment-attempts

- **operationId**：`createPaymentAttempt`；本機 mock 專用付款。信用卡付款接受有效測試卡輸入，伺服器不保存卡號或安全碼，也不連接外部金流；會員可在訂單待付款期間切換使用本人儲值金，並在同一交易中扣款及確認訂單。儲值金不足時不建立付款或扣款。
- **授權與 headers/path/query**：`id` (path,required) `string`；`X-CSRF-TOKEN` (header,required) `string`；`Idempotency-Key` (header,required) `string`；`If-Match` (header,required) `string`
- **Req Content-Type**：application/json；**body**：`expectedVersion` **必填**: `string`; `cardNumber`: `string` — 本機 mock 卡號；需為支援卡別且通過 Luhn 檢核。請勿輸入真實卡號。; `expiryDate`: `string` — 本機 mock 卡有效期限，格式 MM/YY，且不得早於目前月份。; `securityCode`: `string` — 本機 mock 卡安全碼；American Express 需 4 碼，其餘支援卡別需 3 碼。; `paymentMethod` **必填**: `string {wallet,debit_card,credit_card}` — 選擇信用卡或本人會員儲值金；錢包付款無須傳送卡片欄位，卡片付款仍需提供 mock 測試卡資料。
- **Res**：`202` `id` **必填**: `string`; `checkoutId` **必填**: `string`; `mode` **必填**: `any`; `state` **必填**: `any {Processing,Succeeded,Failed,Unknown}`; `amountTwd` **必填**: `integer/int64`; `version` **必填**: `string`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `413`, `415`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /admin/refund-cases/{id}/operations

- **operationId**：`executeProductRefund`；executeProductRefund
- **授權與 headers/path/query**：`id` (path,required) `string`；`X-CSRF-TOKEN` (header,required) `string`；`Idempotency-Key` (header,required) `string`；`If-Match` (header,required) `string`
- **Req Content-Type**：application/json；**body**：`lines` **必填**: `array<object [CommerceRefundLineRequest]>`; `refundShipping` **必填**: `boolean`; `amountTwd` **必填**: `integer/int64`; `reason` **必填**: `string`; `expectedVersion` **必填**: `string`
- **Res**：`202` `id` **必填**: `string`; `caseId` **必填**: `string`; `mode` **必填**: `any`; `state` **必填**: `any {Processing,Succeeded,Failed,Unknown}`; `amountTwd` **必填**: `integer/int64`; `version` **必填**: `string`; `displayNotice` **必填**: `any`; `sourceCollectionId` **必填**: `string`; `kind` **必填**: `string {OrderRefund,PaymentCompensation}`; `units` **必填**: `array<object [CommerceRefundUnit]>`; `shippingTwd` **必填**: `integer/int64`; `dispatchState` **必填**: `string {NotStarted,Started}`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `413`, `415`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### GET /admin/refund-operations/{id}

- **operationId**：`getRefundOperation`；getRefundOperation
- **授權與 headers/path/query**：`id` (path,required) `string`
- **Req Content-Type**：無 JSON body；**body**：無
- **Res**：`200` `id` **必填**: `string`; `caseId` **必填**: `string`; `mode` **必填**: `any`; `state` **必填**: `any {Processing,Succeeded,Failed,Unknown}`; `amountTwd` **必填**: `integer/int64`; `version` **必填**: `string`; `displayNotice` **必填**: `any`; `sourceCollectionId` **必填**: `string`; `kind` **必填**: `string {OrderRefund,PaymentCompensation}`; `units` **必填**: `array<object [CommerceRefundUnit]>`; `shippingTwd` **必填**: `integer/int64`; `dispatchState` **必填**: `string {NotStarted,Started}`
- **非成功回應碼**：`401`, `403`, `404`, `422`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /admin/demo/scenarios

- **operationId**：`enqueueDemoScenario`；PROPOSED Foundation subset；NOT_EXECUTED。後端逐次Owner需有效permission/scope驗證；不能依前端boolean。 If-Match與expectedVersion驗目標原操作。
- **授權與 headers/path/query**：`X-CSRF-TOKEN` (header,required) `string`；`Idempotency-Key` (header,required) `string`；`If-Match` (header,required) `string`
- **Req Content-Type**：application/json；**body**：`targetKind` **必填**: `any {PaymentAttempt,RefundOperation}`; `targetId` **必填**: `string`; `scenario` **必填**: `any {success,declined,unknown,duplicate,late_success,amount_mismatch}`; `expectedVersion` **必填**: `string`
- **Res**：`202` `id` **必填**: `string`; `mode` **必填**: `any`; `state` **必填**: `any`; `resourceUrl` **必填**: `string`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `413`, `415`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### GET /orders/{id}

- **operationId**：`getOwnOrder`；getOwnOrder
- **授權與 headers/path/query**：`id` (path,required) `string`
- **Req Content-Type**：無 JSON body；**body**：無
- **Res**：`200` `id` **必填**: `string`; `checkoutId`: `string` — Included only for an authenticated member's own order so the storefront can continue to the existing checkout payment fl; `mode` **必填**: `string`; `version` **必填**: `string`; `placedAt` **必填**: `string/date-time`; `orderState` **必填**: `string {AwaitingPayment,Confirmed,Cancelled,Expired}`; `snapshot` **必填**: `object`; `paymentSummary` **必填**: `object`; `shipmentSummary` **必填**: `object`; `refundSummary` **必填**: `object`; `receiptSummary` **必填**: `object`; `maskedContact` **必填**: `string`; `returns` **必填**: `array<object [CommerceOrderReturnSummary]>`; `actions` **必填**: `array<object [CommerceActionAvailability]>`
- **非成功回應碼**：`401`, `403`, `404`, `422`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /orders/{id}/cancellations

- **operationId**：`cancelOwnOrder`；cancelOwnOrder
- **授權與 headers/path/query**：`id` (path,required) `string`；`X-CSRF-TOKEN` (header,required) `string`；`Idempotency-Key` (header,required) `string`；`If-Match` (header,required) `string`
- **Req Content-Type**：application/json；**body**：`expectedVersion` **必填**: `string`; `reason`: `string`
- **Res**：`202` `id` **必填**: `string`; `operationId` **必填**: `string`; `mode` **必填**: `string`; `state` **必填**: `string`; `version` **必填**: `string`; `reasonCodes` **必填**: `array<string>`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `413`, `415`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /guest-order-verifications

- **operationId**：`requestGuestVerification`；requestGuestVerification
- **授權與 headers/path/query**：`X-CSRF-TOKEN` (header,required) `string`
- **Req Content-Type**：application/json；**body**：`orderId` **必填**: `string`; `email` **必填**: `string/email`; `intent` **必填**: `string {read,cancel,return}`
- **Res**：`202` `state` **必填**: `any`; `requestId` **必填**: `string`; `requestNonce` **必填**: `string`
- **非成功回應碼**：`401`, `403`, `404`, `413`, `415`, `422`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /guest-order-verifications/confirm

- **operationId**：`confirmGuestVerification`；confirmGuestVerification
- **授權與 headers/path/query**：`X-CSRF-TOKEN` (header,required) `string`
- **Req Content-Type**：application/json；**body**：`requestId` **必填**: `string`; `requestNonce` **必填**: `string`; `proof` **必填**: `string`; `intent` **必填**: `string {read,cancel,return}`
- **Res**：`200` `state` **必填**: `any`; `scope` **必填**: `string {guest.read,guest.action.cancel,guest.action.return,case.read,case.action}`; `resourceId` **必填**: `string`; `expiresAt` **必填**: `string/date-time`
- **非成功回應碼**：`401`, `403`, `404`, `413`, `415`, `422`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /admin/payment-compensation-cases/{id}/operations

- **operationId**：`executePaymentCompensation`；executePaymentCompensation
- **授權與 headers/path/query**：`id` (path,required) `string`；`X-CSRF-TOKEN` (header,required) `string`；`Idempotency-Key` (header,required) `string`；`If-Match` (header,required) `string`
- **Req Content-Type**：application/json；**body**：`expectedVersion` **必填**: `string`; `reason` **必填**: `string`; `confirmed` **必填**: `any`
- **Res**：`202` `id` **必填**: `string`; `caseId` **必填**: `string`; `mode` **必填**: `any`; `state` **必填**: `any {Processing,Succeeded,Failed,Unknown}`; `amountTwd` **必填**: `integer/int64`; `version` **必填**: `string`; `displayNotice` **必填**: `any`; `sourceCollectionId` **必填**: `string`; `kind` **必填**: `string {OrderRefund,PaymentCompensation}`; `units` **必填**: `array<object [CommerceRefundUnit]>`; `shippingTwd` **必填**: `integer/int64`; `dispatchState` **必填**: `string {NotStarted,Started}`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `413`, `415`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /admin/order-shipping-refund-cases/{id}/operations

- **operationId**：`executeShippingOnlyRefund`；executeShippingOnlyRefund
- **授權與 headers/path/query**：`id` (path,required) `string`；`X-CSRF-TOKEN` (header,required) `string`；`Idempotency-Key` (header,required) `string`；`If-Match` (header,required) `string`
- **Req Content-Type**：application/json；**body**：`expectedVersion` **必填**: `string`; `reason` **必填**: `string`; `confirmed` **必填**: `any`
- **Res**：`202` `id` **必填**: `string`; `caseId` **必填**: `string`; `mode` **必填**: `any`; `state` **必填**: `any {Processing,Succeeded,Failed,Unknown}`; `amountTwd` **必填**: `integer/int64`; `version` **必填**: `string`; `displayNotice` **必填**: `any`; `sourceCollectionId` **必填**: `string`; `kind` **必填**: `string {OrderRefund,PaymentCompensation}`; `units` **必填**: `array<object [CommerceRefundUnit]>`; `shippingTwd` **必填**: `integer/int64`; `dispatchState` **必填**: `string {NotStarted,Started}`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `413`, `415`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /admin/refund-operations/{id}/checks

- **operationId**：`checkRefundOperation`；checkRefundOperation
- **授權與 headers/path/query**：`id` (path,required) `string`；`X-CSRF-TOKEN` (header,required) `string`；`Idempotency-Key` (header,required) `string`；`If-Match` (header,required) `string`
- **Req Content-Type**：application/json；**body**：`expectedVersion` **必填**: `string`; `reason` **必填**: `string`
- **Res**：`202` `id` **必填**: `string`; `mode` **必填**: `any`; `state` **必填**: `any`; `resourceUrl` **必填**: `string`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `413`, `415`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### GET /auth/account-context

- **operationId**：`getAccountBrowserContext`；建立/讀匿名Browser-bound nonce；不含Email/User/challenge existence，不能取得會員權。
- **授權與 headers/path/query**：無額外宣告參數；同樣受 API Host 全域 Cookie、Origin 與 CSRF 規則約束
- **Req Content-Type**：無 JSON body；**body**：無
- **Res**：`200` `requestNonce` **必填**: `string`; `expiresAt` **必填**: `string/date-time`; `mode` **必填**: `any`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /auth/email-verification-requests

- **operationId**：`requestEmailVerification`；既有/未知/停用同形受理；重送撤舊該intent proof，不披露狀態。
- **授權與 headers/path/query**：`X-CSRF-TOKEN` (header,required) `string`；`Origin` (header,required) `string/uri`
- **Req Content-Type**：application/json；**body**：`email` **必填**: `string/email`; `requestNonce` **必填**: `string`
- **Res**：`202` `accepted` **必填**: `any`; `messageCode` **必填**: `any`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `413`, `415`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /auth/email-verifications

- **operationId**：`confirmAccountEmail`；單次註冊Email proof＋原browser nonce；成功不發session。
- **授權與 headers/path/query**：`X-CSRF-TOKEN` (header,required) `string`；`Origin` (header,required) `string/uri`
- **Req Content-Type**：application/json；**body**：`challengeId` **必填**: `string`; `proof` **必填**: `string`; `requestNonce` **必填**: `string`
- **Res**：`204` (無回應 body)
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `413`, `415`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### DELETE /admin/access-grants/{id}

- **operationId**：`revokeAccessGrant`；Owner＋access.manage(User/grant.target)；If-Match grant etag；X-Change-Reason；tombstone不刪稽核。
- **授權與 headers/path/query**：`X-CSRF-TOKEN` (header,required) `string`；`Origin` (header,required) `string/uri`；`If-Match` (header,required) `string`；`Idempotency-Key` (header,required) `string`；`X-Change-Reason` (header,required) `string`；`id` (path,required) `string`
- **Req Content-Type**：無 JSON body；**body**：無
- **Res**：`204` (無回應 body)
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### GET /admin/access-grants/{id}

- **operationId**：`getAccessGrant`；Owner＋access.read現行scope；可閱授權包括已撤tombstone，未知／不可閱404。Grant ETag供delete；不提供秘密或會員收藏。
- **授權與 headers/path/query**：`id` (path,required) `string`
- **Req Content-Type**：無 JSON body；**body**：無
- **Res**：`200` `id` **必填**: `string`; `targetUserId` **必填**: `string`; `role` **必填**: `string {Owner,Merchandiser,CustomerSupport,Analyst}`; `permission` **必填**: `string {access.manage,access.read,audit.read,catalog.write,data.handle,demo.control,demo.reset,orders.read,reconciliation.check,reconciliation.read,reconciliation.resolve,reconciliation.run,refund.check,refund.execute,refund.request,reports.read,return.inspect,shipment.manage,shipping.configure,stock.adjust,support.respond,user.manage,user.read}`; `scope` **必填**: `object`; `validFrom` **必填**: `string/date-time`; `validUntil` **必填**: `string/date-time`; `reason` **必填**: `string`; `grantedBy` **必填**: `string`; `revokedAt` **必填**: `string|null/date-time`; `version` **必填**: `string`; `etag` **必填**: `string`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。

### POST /auth/registrations

- **operationId**：`registerAccount`；Email/password 必填、姓名與電話選填；只驗證信箱格式，不驗證持有人；建立會員、不授予管理角色，由 Storefront 隨後使用相同憑證登入。
- **授權與 headers/path/query**：`X-CSRF-TOKEN` (header,required) `string`；`Origin` (header,required) `string/uri`
- **Req Content-Type**：application/json；**body**：`email` **必填**: `string/email`; `password` **必填**: `string`; `displayName`: `string`; `phone`: `string`
- **Res**：`202` `accepted` **必填**: `any`; `messageCode` **必填**: `any`
- **非成功回應碼**：`401`, `403`, `404`, `409`, `412`, `413`, `415`, `422`, `428`, `429`, `500`, `503`，統一檢查 `application/problem+json` 與 `code`，不得僅看 HTTP 2xx。


## 結構與約束的唯一詳細來源

- 所有 nested fields、`pattern/minLength/maxLength`、`oneOf`、`additionalProperties` 及錯誤 `Problem`，請以 [runtime OpenAPI](contracts/runtime/Core.openapi.json) 的 `components.schemas` 與 `components.parameters` 核對；本頁僅展開首層 body/schema 以免重複維護巨量內容。
- 候選的 `API_CANONICAL.openapi.yaml`（已退出 docs 的歷史候選驗證來源） 含尚未採用的大範圍操作（包括換裝）；**不得當作現行公開 API**。
- `Core.openapi.json` 不涵蓋所有已 Map 的模組路由。新增 endpoint 前先更新真實執行契約與負向驗收，再更新本索引。


<a id="additional-routes"></a>

## B. 程式已掛載但未全數納入契約（26 條）

## 額外路由實作索引

> `main@7427c94`：由 `MapFoundationCommerce`、`MapFoundationCatalog`、`MapSecurity` 與 `Core.openapi.json` 差集生成。下列路由**程式已掛載**，但不等於已納入 runtime OpenAPI 或所有端點通過最新全流程測試。若 Handler 未明確提供欄位則保留待驗，不虛構 req/res。核心 27 項見 [api-spec.md](api-spec.md)。

## 共同 request/response 及授權

- API Prefix：`/api/v1`；對修改操作送同源 `Origin`、`X-CSRF-TOKEN` 及 `Cookie`。視 Handler `Write`、`Expected` 額外送 `Idempotency-Key`、`If-Match`、`X-Change-Reason`；請由來源驗證必填性，**不可一律省略**。
- Body：JSON `Content-Type: application/json`；GET 無 body；大於 64 KiB 由代理層拒絕。回應以 JSON、`no-store`；例外為 `application/problem+json`，包含 `code/status/traceId`。
- `/admin/**` 皆以受保護管理員權限／資源 Scope 控制；`/wallet/current` 與 `/wallet/top-ups` 僅登入本人會員，匿名不可讀取他人餘額。
- mock 模式完全由前端 `demo-api.ts` 模擬，不保證與 SQL API 全欄一致；切到 backend 模式時須經 `server/api/v1/[...path].ts`。資金結果必須查狀態，不可用操作已送出代表入帳成功。

## 差集路由（26 項）

### GET /api/v1/admin/stock/{skuId}

- **Handler**：`Stock`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs)）
- **Path / Query**：`skuId`
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：以 handler 與測試驗證 2xx/4xx/5xx（尚未納入 runtime 契約）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/stock-adjustments

- **Handler**：`AdjustStock`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：無明確 JSON body 或由 handler 內部產生
- **Res**：以 handler 與測試驗證 2xx/4xx/5xx（尚未納入 runtime 契約）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/admin/reports

- **Handler**：`AggregateReport`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/admin/reconciliation-report

- **Handler**：`ReconciliationReport`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/admin/reconciliation-notifications

- **Handler**：`ReconciliationNotifications`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/reconciliation-cases/{id}/resolutions

- **Handler**：`ResolveReconciliationCase`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：action: string；expectedVersion: string；reason: string；evidenceRef: string
- **Res**：以 handler 與測試驗證 2xx/4xx/5xx（尚未納入 runtime 契約）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/reconciliation-cases/{id}/assignments

- **Handler**：`AssignReconciliationCase`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：assigneeEmail: string；expectedVersion: string；reason: string
- **Res**：以 handler 與測試驗證 2xx/4xx/5xx（尚未納入 runtime 契約）；失敗以 problem `code` 處理，服務異常不能視為成功。

### DELETE /api/v1/carts/current/items/{skuId}

- **Handler**：`RemoveCartItem`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs)）
- **Path / Query**：`skuId`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：無明確 JSON body 或由 handler 內部產生
- **Res**：HTTP 200（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### PUT /api/v1/carts/current/promotion

- **Handler**：`SetCartPromotion`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：JSON body 欄位未能靜態抽取，必須對照 handler 與測試
- **Res**：HTTP 200（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/wallet/current

- **Handler**：`CurrentWallet`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/wallet/top-ups

- **Handler**：`TopUpWallet`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：amountTwd: integer；cardNumber: string
- **Res**：HTTP 201（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/wallet-credits

- **Handler**：`AdminWalletCredit`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：memberUserId: string；amountTwd: integer；reason: string
- **Res**：HTTP 201（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/orders

- **Handler**：`Orders`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/orders/{id}/receipts

- **Handler**：`Receipts`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/orders/{id}/returns

- **Handler**：`RequestReturn`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：orderLineId: string；quantity: integer；reason: string
- **Res**：HTTP 201（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/orders/{id}/shipments

- **Handler**：`CreateShipment`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：expectedVersion: 需核對 handler 型別
- **Res**：HTTP 201（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/shipments/{id}/transitions

- **Handler**：`TransitionShipment`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：state: string；eventId: string；expectedVersion: 需核對 handler 型別
- **Res**：HTTP 200（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/returns/{id}/transitions

- **Handler**：`TransitionReturn`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：state: string；expectedVersion: 需核對 handler 型別
- **Res**：HTTP 200（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/returns/{id}/inspections

- **Handler**：`InspectReturn`（[`commerce endpoint`](../src/Modules/Commerce/Presentation/CommerceEndpoints.cs)；[`implementation`](../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：resellableQuantity: integer；reason: string；expectedVersion: 需核對 handler 型別
- **Res**：HTTP 200（內容由 handler 返回的 JSON/DTO；實際結構以來源及測試為準）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/products

- **Handler**：`Products`（[`catalog endpoint`](../src/Modules/Catalog/Infrastructure/CatalogEndpoints.cs)；[`implementation`](../src/Modules/Catalog/Infrastructure/Legacy/CatalogService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/products/{slug}

- **Handler**：`Product`（[`catalog endpoint`](../src/Modules/Catalog/Infrastructure/CatalogEndpoints.cs)；[`implementation`](../src/Modules/Catalog/Infrastructure/Legacy/CatalogService.cs)）
- **Path / Query**：`slug`
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/admin/products

- **Handler**：`AdminProducts`（[`catalog endpoint`](../src/Modules/Catalog/Infrastructure/CatalogEndpoints.cs)；[`implementation`](../src/Modules/Catalog/Infrastructure/Legacy/CatalogService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/products

- **Handler**：`Create`（[`catalog endpoint`](../src/Modules/Catalog/Infrastructure/CatalogEndpoints.cs)；[`implementation`](../src/Modules/Catalog/Infrastructure/Legacy/CatalogService.cs)）
- **Path / Query**：無 path 變數；其他查詢條件依 handler 檢查
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：JSON body 欄位未能靜態抽取，必須對照 handler 與測試
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### GET /api/v1/admin/products/{id}

- **Handler**：`GetAdminProductOrDraft`（[`catalog endpoint`](../src/Modules/Catalog/Infrastructure/CatalogEndpoints.cs)；[`implementation`](../src/Modules/Catalog/Infrastructure/Legacy/CatalogService.cs)）
- **Path / Query**：`id`
- **Headers**：Cookie（權限路由）
- **Req body**：無
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### PUT /api/v1/admin/products/{id}

- **Handler**：`Update`（[`catalog endpoint`](../src/Modules/Catalog/Infrastructure/CatalogEndpoints.cs)；[`implementation`](../src/Modules/Catalog/Infrastructure/Legacy/CatalogService.cs)）
- **Path / Query**：`id`
- **Headers**：Origin、Cookie、X-CSRF-TOKEN；涉及重送/版本時需 Idempotency-Key、If-Match
- **Req body**：JSON body 欄位未能靜態抽取，必須對照 handler 與測試
- **Res**：2xx JSON（欄位見 handler；非正式 OpenAPI 定義）；失敗以 problem `code` 處理，服務異常不能視為成功。

### POST /api/v1/admin/products/{id}/publication-commands

- **Handler**：`Publish`（[`catalog endpoint`](../src/Modules/Catalog/Infrastructure/CatalogEndpoints.cs)；[`implementation`](../src/Modules/Catalog/Infrastructure/Legacy/CatalogService.cs)）
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
