# M1–M3 API 跨階段一致性與提案差異

2026-10-13｜READY_FOR_REVIEW｜Authority PROPOSED｜產品NOT_EXECUTED

[整合OpenAPI](API_M1_M3.openapi.yaml)包含76個operation／112個schema，覆蓋M1商品／cart／quote、M2建單／查閱／guest action、M3配送／收件／驗收／退款／憑證／差異／最小報表與資料案件。沿用M0身份bootstrap與登入子集；不是完整46群組全產品契約。登入強化、完整帳號與AccessGrant維護、換裝及真provider API在本檔範圍之外；選配登入強化另見options。

格式依[OpenAPI3.1.0官方規格](https://spec.openapis.org/oas/v3.1.0.html)（2026-10-13核對），保持與M0格式一致；不推定需更換API工具版本。Security Requirement宣告cookie組合，不能自動實作角色／scope／CSRF。x-authorization、x-version-target、x-case-basis與x-server-invariants為實作者必要檢查來源。

## 1. 跨階段一致性

| 邊界 | 明確契約／server規則 | 驗證與限制 |
| --- | --- | --- |
| M1→M2 金額 | 整數TWD安全範圍0..9007199254740991、零總額關閉；原1800−270＋60=1590，商品單位425/552/553，乘加checked | script核fixture／範例；真SQL仍未跑 |
| Quote→Order | Quote保留內容／語言／費用版本與ordinal分攤，不保留庫存；Quote尚無OrderLine/unit實體ID為null。建checkout原子產生非空唯一ID並固定，原金額內容不重算 | 同key重送原Order；不同資源不可共用原單位，server後置約束需產品測試 |
| Cart→Quote→保留 | cart是現價可變模型；Quote5分鐘候選、Reservation15分鐘既定；If-Match不延長期限，Quote/SKU/費用失效拒絕 | 409 QUOTE_CHANGED/STOCK_CHANGED；無運費不回0替代 |
| Lookup→action | checkout.read/pay不查完整Order；guest.read只此單，cancel/return另guest.action；claim需member與claim能力同時 | claim security同一object為AND；兩個object為OR，script核宣告，後端仍要scope驗證 |
| 原款→退款 | 正常商品非空lines、amountTwd僅預期核對值；server取原未占用ordinal，保留來源金額/unit/shipping；Unknown不釋放 | M0 request形狀保留，112 DTO例與20拒絕例不等於資金實證 |
| 運費／補償 | shipping-only先提原Order案件、server核資格，request無商品lines／amount；晚到/Extra補償案件由可信collection事件生成，全剩額，不耗原商品/shipping slots | route必核CaseKind/Basis；不能借換route繞過額度，案例明示不同來源 |
| Event→Collection | 投遞EventId去重與(dataset,adapter,collectionId)金融來源唯一分開；SourceFingerprint排除EventId/投遞時間，金融欄位衝突列差異 | 來源completed＋pending不得超Collected；server-only資料，無顧客偽webhook |
| Return→Inspection→庫存 | 已收原單位ID／Quantity可查，少件／多件／錯品exceptions分開；只此案已收且未驗單位可inspection，同request不能重複unit | ReceivedQuantity=unique unitIds長度；Restock唯一Movement，Quarantine不入可售；refund不加庫存 |
| Refund→Receipt／報表 | refund結果與DEMO調整分開；來源receipt獨立；netConfirmed=collected−成功，available另扣pending，1038與485不同 | 不硬改Unknown結案，不以額外款退回把原商品整單全退 |
| 版本與重送 | strong ETag含引號，expectedVersion無引號；依原Order/Case/Operation/SKU/Dataset查版本。先驗現行身份再合法重送原結果；不要因舊版本重建業務 | 409 key衝突／412舊版本／428缺header；撤權Started後結果由系統繼續核，非Actor新派送 |
| UI與公開資料 | public商品無Reserved/cost/權利來源Ref，Admin draft另帶sourceRightsRef；Order遮罩聯絡，private/challenge no-store | cookie HttpOnly/Secure等提案；不回secret/session proof；mock只合成與隔離投遞 |

## 2. M0相容性與authority

M0原檔逐byte不變，整合檔x-m0-baseline-sha256記其指紋。沒有已採用canonical或產品consumer可作breaking基線，不宣稱向後相容。實作時必指定同一版整合提案並完成必要採用，不可同時混用兩份DTO。

重疊提案差異：CartItem補localized公開內容／可售原因／price/stock版本；Cart新增promotionCode。PriceSnapshot補lines／原單位／語言與price/promotion版本，Quote補version；CheckoutCreated/Status間接擴充snapshot。RefundOperation補sourceCollectionId/kind/units/shippingTwd/dispatchState，普通退款request仍含expected amount assertion。若既有client依M0額外欄位禁止／必填型別建立，升版須重新核DTO與範例，不默認生成client可共用。

其他新路徑補公開／後台讀模型、Product publication command、shipping-only提案／執行、原退款／憑證查核、受控差異處置／job、聚合報表、合成reset與單案驗證；不提供任意status PATCH。登入強化仍不因本契約有AdminAuthenticated狀態就認為機制已實作。

## 3. 可重跑文件檢查

執行 `python scripts/check_kohami_api_contract.py`，需環境已有PyYAML、jsonschema、openapi-spec-validator；本輪使用既有AIPS Python，沒有安裝依賴或持久化原始輸出。檢查OpenAPI、local refs、每schema例、唯一operationId、HTTP header大小寫去重、path params、寫入CSRF／版本／idempotency、scope組合及公開欄位黑名單；另核M1/M2/M3原快照／單位及退款來源、庫存、報表算例。

20個負向schema案例涵蓋zero/fractionalQty、成功偽造、live mode/memberId、非合成Email、空正常退款lines、負／小數／越界金額、補償amount/bank/payment/lines、client-supplied verification flag、非法配送command、Receive少收件證據、普通退款無單位且無運費、補償耗商品單位、收件原單位與例外皆空。這些證明DTO文件約束，不證明SQL／後端授權／API transport拒絕；可跨欄位的條件必由server＋產品contract/integration tests驗。

結果：文件檢查PASS 76 operations／112 schemas／113 schema examples／20 negative cases；M0原檔未變。獨立唯讀覆核指出CSRF重複、收件讀模型不足及退款示例不平衡，已修正並加入回歸檢查。後續覆核的全錯品exception-only收件與獨立SnapshotLine算例亦已修正；收件例外不轉原商品庫存。真.NET/Nuxt/SQL/瀏覽器/provider驗收仍NOT_EXECUTED；不以此解除票券依賴或稱LOCAL_COMPLETE。

## 4. 操作展開索引

所有下列路徑以/api/v1候選prefix，個別method有唯一operationId。M0-inherited表示沿用bootstrap子集，不含登入強化選配。

| OP | Method／path | operationId | 階段 |
| --- | --- | --- | --- |
| OP-17 | GET `/auth/csrf` | `getCsrf` | M0-inherited |
| OP-17 | POST `/auth/sessions` | `createSession` | M0-inherited |
| OP-17 | GET `/auth/session` | `getSession` | M0-inherited |
| OP-03 | POST `/carts` | `createCart` | M0-inherited |
| OP-03 | GET `/carts/current` | `getCurrentCart` | M0-inherited |
| OP-04 | PUT `/carts/current/items/{skuId}` | `setCartItem` | M0-inherited |
| OP-05 | DELETE `/carts/current/items/{skuId}` | `removeCartItem` | M1 |
| OP-06 | POST `/checkout-quotes` | `createQuote` | M0-inherited |
| OP-07 | POST `/checkouts` | `createCheckout` | M0-inherited |
| OP-08 | GET `/checkouts/{id}/status` | `getCheckoutStatus` | M0-inherited |
| OP-09 | POST `/checkouts/{id}/payment-attempts` | `createPaymentAttempt` | M0-inherited |
| OP-26 | POST `/admin/refund-cases/{id}/operations` | `executeProductRefund` | M3 |
| OP-27 | GET `/admin/refund-operations/{id}` | `getRefundOperation` | M3 |
| OP-35 | POST `/admin/demo/scenarios` | `enqueueDemoScenario` | M0-inherited |
| OP-01 | GET `/products` | `listProducts` | M1 |
| OP-02 | GET `/products/{slug}` | `getProduct` | M1 |
| OP-05 | PUT `/carts/current/promotion` | `setCartPromotion` | M1 |
| OP-10 | GET `/orders` | `listOwnOrders` | M2 |
| OP-10 | GET `/orders/{id}` | `getOwnOrder` | M2 |
| OP-11 | POST `/orders/{id}/cancellations` | `cancelOwnOrder` | M2 |
| OP-12 | GET `/orders/{id}/refunds` | `listOwnRefunds` | M2 |
| OP-13 | POST `/orders/{id}/return-requests` | `requestOwnReturn` | M2 |
| OP-14 | GET `/orders/{id}/receipts` | `listOwnReceipts` | M2 |
| OP-21 | POST `/guest-order-verifications` | `requestGuestVerification` | M2 |
| OP-22 | POST `/guest-order-verifications/confirm` | `confirmGuestVerification` | M2 |
| OP-38 | POST `/orders/{id}/member-claims` | `claimOrderForMember` | M2 |
| OP-24 | GET `/admin/orders` | `listAdminOrders` | M3 |
| OP-24 | GET `/admin/orders/{id}` | `getAdminOrder` | M3 |
| OP-25 | POST `/admin/orders/{id}/refund-cases` | `createOrderRefundCase` | M3 |
| OP-25 | GET `/admin/refund-cases` | `listRefundCases` | M3 |
| OP-25 | GET `/admin/refund-cases/{id}` | `getRefundCases` | M3 |
| OP-25 | GET `/admin/payment-compensation-cases` | `listCompensationCases` | M3 |
| OP-25 | GET `/admin/payment-compensation-cases/{id}` | `getCompensationCases` | M3 |
| OP-25 | GET `/admin/order-shipping-refund-cases` | `listShippingRefundCases` | M3 |
| OP-25 | GET `/admin/order-shipping-refund-cases/{id}` | `getShippingRefundCases` | M3 |
| OP-26 | POST `/admin/payment-compensation-cases/{id}/operations` | `executePaymentCompensation` | M3 |
| OP-26 | POST `/admin/order-shipping-refund-cases/{id}/operations` | `executeShippingOnlyRefund` | M3 |
| OP-27 | POST `/admin/refund-operations/{id}/checks` | `checkRefundOperation` | M3 |
| OP-28 | POST `/admin/refund-cases/{id}/decisions` | `decideRefundCase` | M3 |
| OP-29 | POST `/admin/orders/{id}/shipments` | `createShipment` | M3 |
| OP-29 | POST `/admin/shipments/{id}/transitions` | `transitionShipment` | M3 |
| OP-37 | GET `/admin/returns` | `listReturns` | M3 |
| OP-37 | GET `/admin/returns/{id}` | `getReturn` | M3 |
| OP-37 | POST `/admin/returns/{id}/transitions` | `transitionReturn` | M3 |
| OP-30 | POST `/admin/returns/{id}/inspections` | `inspectReturn` | M3 |
| OP-31 | POST `/admin/products` | `createProductDraft` | M1 |
| OP-31 | GET `/admin/products` | `listAdminProducts` | M1 |
| OP-31 | PUT `/admin/products/{id}` | `updateProductDraft` | M1 |
| OP-31 | GET `/admin/products/{id}` | `getAdminProduct` | M1 |
| OP-31 | POST `/admin/products/{id}/publication-commands` | `publishOrUnpublishProduct` | M1 |
| OP-32 | GET `/admin/stock-balances` | `listStockBalances` | M1 |
| OP-32 | POST `/admin/stock-adjustments` | `adjustStock` | M1 |
| OP-33 | GET `/admin/shipping-fee-versions` | `listFeeVersions` | M1 |
| OP-33 | POST `/admin/shipping-fee-versions` | `createFeeVersion` | M1 |
| OP-34 | GET `/admin/audit-events` | `listAuditEvents` | M3 |
| OP-34 | GET `/admin/reconciliation` | `listReconciliationCases` | M3 |
| OP-34 | POST `/admin/reconciliation-jobs` | `queueReconciliationJob` | M3 |
| OP-34 | GET `/admin/reconciliation-jobs/{id}` | `getReconciliationJob` | M3 |
| OP-34 | GET `/admin/reports` | `getAggregateReport` | M3 |
| OP-45 | POST `/admin/receipt-adjustments/{id}/checks` | `checkReceiptAdjustment` | M3 |
| OP-45 | POST `/admin/reconciliation-cases/{id}/resolutions` | `resolveReconciliationCase` | M3 |
| OP-35 | GET `/demo/inbox` | `getOwnDemoInbox` | M3 |
| OP-36 | POST `/admin/demo/reset-previews` | `previewDatasetReset` | M3 |
| OP-36 | POST `/admin/demo/resets` | `resetDataset` | M3 |
| OP-42 | POST `/support-cases` | `createSupportCase` | M3 |
| OP-42 | GET `/support-cases/{id}` | `getOwnSupportCase` | M3 |
| OP-42 | POST `/admin/support-cases/{id}/responses` | `respondSupportCase` | M3 |
| OP-40 | POST `/account/data-requests` | `createDataRequest` | M3 |
| OP-40 | POST `/admin/data-requests/{id}/decisions` | `decideDataRequest` | M3 |
| OP-46 | POST `/case-verifications` | `requestCaseVerification` | M3 |
| OP-46 | POST `/case-verifications/confirm` | `confirmCaseVerification` | M3 |
| OP-45 | GET `/admin/receipt-adjustments/{id}` | `getReceiptAdjustment` | M3 |
| OP-40 | GET `/account/data-requests/{id}` | `getOwnDataRequest` | M3 |
| OP-40 | GET `/admin/data-requests/{id}` | `getAdminDataRequest` | M3 |
| OP-42 | GET `/admin/support-cases/{id}` | `getAdminSupportCase` | M3 |
| OP-25 | POST `/admin/orders/{id}/shipping-refund-cases` | `createShippingOnlyRefundCase` | M3 |

## 最終差異覆核

唯讀覆核確認全錯品例外收件與500×1−75=425獨立行項修正成立，無剩餘必要矛盾。12檔實際差異、76條索引與機器操作一致、內部連結、M0逐byte／票券未變及M4全部未執行已核對；只屬規劃契約證據。


## 身份補充與跨文件檢查

[帳號／grant契約](API_ACCOUNT_ACCESS.openapi.yaml)、[身份整合差異](../identity/IDENTITY_API_INTEGRATION_PLAN.md)已補19operations，與帳號19／本檔商務76共95無衝突method/path與operationId；同名Schema仍各檔local scope。原來源byte指紋由scripts/check_kohami_identity_contract.py固定；文件PASS不是authority採用、merge canonical或runtime安全保證。
