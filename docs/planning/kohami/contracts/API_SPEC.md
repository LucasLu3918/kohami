# 本機 mock API 契約提案

## 現行適用範圍（2026-10-15）

本文保留需求、設計與具日期的驗證依據；目前已有 .NET／Nuxt／SQL 程式、core34 採用子集與部分產品實證。完整 140 操作候選已建立，核心細則已部分採用；其餘功能／Phase2 不因此視為已批准或完成。非 Owner 後台角色採密碼＋TOTP，Owner TOTP 為選配。產品逐項狀態、範圍與來源優先順序請由[文件入口](../../../README.md)查閱；舊段落中的尚未建立／未決／全部未執行須依原日期與案例範圍解讀。


日期：2026-10-13｜READY_FOR_REVIEW｜Authority: PROPOSED

此Markdown保留完整46操作群組總綱；新增[M0 OpenAPI子集](API_M0.openapi.yaml)，13個operation／20個DTO，Authority同為PROPOSED，未生成client或產品程式。沒有已批准canonical API／breaking comparison baseline。MFA proof在方案待決階段不納入機器契約，不能把subset當全產品API完成。依據 [畫面](../experience/EXPERIENCE_DESIGN.md)、[領域](../architecture/DOMAIN_MODEL.md)、[政策](../../../proposals/kohami/04-demo-commerce-policy.md)。

## 1. 共用協定

- 提案prefix `/api/v1`、JSON、UTC ISO8601時間、不透明識別、locale=zh-TW/ja/en；Mode只有mock，DatasetId由伺服器環境／session選擇，不信任顧客傳入mode或跨資料集ID。
- 讀取GET無寫入／交易／核准副作用。建立資源201，排入處理202附Location與operationId，查詢200；失去連線不代表失敗，以同操作查詢／冪等重送。
- 權限：public只讀商品；cart使用不可猜能力cookie；checkout.read只讀同筆非PII狀態；checkout.pay另授單訂單／期限內付款命令；member限本人；guest.read限驗證單筆查詢；guest.action須另取得明確動作驗證；backend角色限範圍及MFA。
- 商務command、客服／資料案件建立等業務寫入要求 `Idempotency-Key`；scope為Dataset＋Actor或匿名能力＋Operation種類＋目標資源，PayloadHash含完整有效payload與版本。相同scope/key但不同payload→409 KEY_CONFLICT。同key同payload回原resource／結果，不多建操作；token輪替不得使原操作重建。
- 認證／challenge/MFA驗證POST不套業務ETag或可重播秘密的冪等快取；使用限流、綁定request nonce、單次proof消耗與session管理。驗證重送不能回吐原token／復原碼／重建登入權；遺失回應先查有效session，否則重新驗證。
- 已有業務資源的修改／命令要求 `If-Match` 版本，回應帶ETag／version；缺少428、舊版本412。重送冪等先辨識原操作；合法同key重送不因版本已改而重做；回原結果仍先查現行身份／scope／資料集／撤權，PermissionSnapshot與key不是授權替代品。單次guest.action已消耗後，原網路重送只可查／回該綁定操作，不可新發動作。缺貨／無效費用／未知退款／超額等業務衝突409，欄位錯422，未登入401、角色不足403；無權查訂單回一致404，不洩漏存在；限流429＋Retry-After。
- Cookie session候選：HttpOnly、Secure、適當SameSite；寫入含antiforgery token及Origin檢查，不存JWT於localStorage；同origin代理避免寬鬆CORS。cookie身份會需要CSRF防護，[Microsoft ASP.NET Core antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)（核對2026-10-13）。
- 錯誤形狀：`{type,title,status,code,traceId,fieldErrors?}`；code穩定，文案由三語UI映射；不含stack、SQL、token或他人資料。範例code：STOCK_CHANGED、QUOTE_EXPIRED、PAYMENT_UNKNOWN、REFUND_LIMIT、REFUND_UNKNOWN、PERMISSION_CHANGED、DEMO_ONLY。
- 分頁cursor不透明，limit預設20／最大100，穩定排序；只在授權集合內分頁，不能先取資料再於前台filter。商品排序可name/price，其他未允許值422。
- 金額為精確整數TWD，禁止小數／非有限值；JSON/SQL/前台共同約定安全整數範圍，M0提案固定API安全整數0..9007199254740991、SQL bigint，checked計算避免溢位；其他完整DTO採用時同規則。Quantity正整數；總和／乘法需溢位檢查，不用浮點進行優惠分攤。

## 2. 操作清單

下列為規劃行為；request/response欄位是最小邊界，最終OpenAPI須展開每個DTO、enum與錯誤，不以表格當作已完成機器契約。

| OP | Method／相對path | 權限／MFA | 輸入 → 輸出／副作用 | DOM |
| --- | --- | --- | --- | --- |
| OP-01 | GET `/products` | public | q、locale、colorId、garmentSize、availability、sort、limit、cursor → 已發布商品摘要、價格、可售／換裝資格及 nextCursor | 01、02 |
| OP-02 | GET `/products/{slug}` | public | locale → SKU、示意素材／量測、價格、版本；不含私有來源文件 | 01、02 |
| OP-03 | POST `/carts`；GET `/carts/current` | public建立；cart scope讀 | 建cart能力cookie或查自身Lines/version；無Reservation | 03 |
| OP-04 | PUT `/carts/current/items/{skuId}` | cart | qty/version → cart；失效SKU拒絕 | 03 |
| OP-05 | DELETE `/carts/current/items/{skuId}`；PUT `/carts/current/promotion` | cart | 移除或code/clear → cart；單碼，不立即鎖價 | 03 |
| OP-06 | POST `/checkout-quotes` | cart/member | cartVersion、mock配送方式／區域 → quoteId、ExpiresAt、完整費用及版本 | 03 |
| OP-07 | POST `/checkouts` | cart/member | quoteId、cartVersion、合成聯絡／配送、確認 → checkout/order、snapshot、expires、分離checkout.read／checkout.pay能力；原子保留 | 02、04、05、11 |
| OP-08 | GET `/checkouts/{id}/status` | checkout.read或本人member | → 非PII金額／訂單承諾／付款狀態／到期時間；不讀完整訂單 | 04–06 |
| OP-09 | POST `/checkouts/{id}/payment-attempts` | checkout.pay/member | expectedVersion → attemptId、Processing；不接受client success或情境status；已有Processing/Unknown/Succeeded不能新建，明確Failed且保留有效才可重試 | 05、06、11 |
| OP-10 | GET `/orders`；GET `/orders/{id}` | member本人；guest.read僅單筆 | → 分離狀態、原快照、可操作項、遮罩聯絡；guest不可列所有orders | 04、07–10 |
| OP-11 | POST `/orders/{id}/cancellations` | member本人或guest.action(cancel) | expectedVersion/reason → 取消裁決及退款案件，如需；不立即宣稱退款完成 | 02、04、05、07、08 |
| OP-12 | GET `/orders/{id}/refunds` | 本人/guest.read | → 各原退款操作、未決額與明示模擬結果 | 07 |
| OP-13 | POST `/orders/{id}/return-requests` | 本人或guest.action(return) | lines/qty/reason（可選）；無附件 → 售後案件；一般客服另以最小必要表單受理 | 07、08 |
| OP-14 | GET `/orders/{id}/receipts` | 本人/guest.read | → DEMO憑證及調整；只讀，非稅務文件 | 09 |
| OP-15 | POST `/auth/registrations` | public＋限流 | 合成email/password → 統一受理及驗證投遞；不自動給後台角色 | 10、12 |
| OP-16 | POST `/auth/email-verifications` | challenge＋限流 | challenge/token → 綁定帳號Email驗證；GET連結不consume | 10 |
| OP-17 | POST `/auth/sessions`；GET `/auth/session` | credentials/session | 登入→member session、Owner session或其他管理角色的MFA challenge；讀目前有效身份／權限，不洩漏token | 10 |
| OP-18 | POST `/auth/mfa-verifications` | 非 Owner 管理登入challenge或已選擇啟用 TOTP 的 Owner | authenticator proof → 後台MFA session；不得接受client mfa=true | 10 |
| OP-19 | POST `/auth/reauthentications`；POST `/auth/password-reset-requests`；POST `/auth/password-resets` | session或reset challenge | 近期敏感驗證或限流統一reset流程；變更撤舊session，不以email復原撤MFA | 10、12 |
| OP-20 | DELETE `/auth/session` | session | 登出撤session；前台清除私有資料與cache | 10 |
| OP-21 | POST `/guest-order-verifications` | public＋限流 | orderId/email/intent(read,cancel,return,claim) → 統一受理；發到原合成聯絡管道，不回token | 10、12 |
| OP-22 | POST `/guest-order-verifications/confirm` | challenge/request nonce＋限流 | token＋intent → 該單短效scope；read只讀，動作scope單次、不可跨用途／訂單 | 10 |
| OP-23 | POST `/admin/access-grants`；DELETE `/admin/access-grants/{id}` | Owner＋有效permission/scope | target/permission/scope/起迄 → 具名授權／撤銷；不可自授越權或改不可變稽核 | 10、11 |
| OP-24 | GET `/admin/orders`；GET `/admin/orders/{id}` | Owner需有效permission/scope；被指派Support維持MFA | → 授權案件集合及必要資料；Merchandiser不取得PII | 04、07、08 |
| OP-25 | POST `/admin/orders/{id}/refund-cases` | Owner需案件permission/scope；案件Support維持MFA | originalLines/reason → Requested；提出案件不授權執行 | 07 |
| OP-26 | POST `/admin/refund-cases/{id}/operations` | Owner＋refund.execute／案件scope | lines/amount/shipping/reason/expectedVersion → operationId/Processing，原子保留各原品項數量／金額與原運費可退額 | 07、11 |
| OP-27 | GET `/admin/refund-operations/{id}`；POST `.../{id}/checks` | Owner需read/查核permission/scope；非Owner維持MFA | 查原結果或排入原操作查核；Unknown不新發退款 | 06、07、11 |
| OP-28 | POST `/admin/refund-cases/{id}/decisions` | Owner＋refund.execute／案件scope | Reject/理由或未執行撤回→版本化案件結果；Processing/Unknown不可當撤回釋放 | 07 |
| OP-29 | POST `/admin/orders/{id}/shipments`；POST `/admin/shipments/{id}/transitions` | Owner需履約permission/scope；授權Support維持MFA | 建立或受限command→mock配送；檢查已付款／取消競爭，不接受任意status | 04、08、11 |
| OP-30 | POST `/admin/returns/{id}/inspections` | Owner需履約permission/scope；授權Support維持MFA | lineQty/可再售結果/reason → Inspection及唯一入庫；需已受理／收件證據 | 02、08、11 |
| OP-31 | POST `/admin/products`；PUT `/admin/products/{id}` | Owner或Merchandiser需商品permission/scope；非Owner維持MFA | 三語內容、SKU、示意價格與權利指標 → 草稿／發布檢查；有Version | 01、11 |
| OP-32 | GET `/admin/stock/{skuId}`；POST `/admin/stock-adjustments` | Owner需stock.adjust／SKU scope；Merchandiser維持MFA | 查詢OnHand／Reserved／Sellable／Movement；sku/quantityDelta/reason → 唯一StockAdjustment Movement；不可扣減Held Reservation | 02、11 |
| OP-33 | POST `/admin/shipping-fee-versions` | Owner＋有效permission/scope | 中性方式/合成區域/fee/有效範圍 → 新版本；舊Snapshot不改 | 03、11 |
| OP-34 | GET `/admin/audit-events`；GET `/admin/reconciliation` | Owner需授權只讀範圍；非Owner維持MFA | → 遮罩稽核／模擬台帳差異；無通用SQL或原始secret輸出 | 06、07、11 |
| OP-35 | POST `/admin/demo/scenarios`；GET `/demo/inbox` | scenario:Owner＋demo.control permission/scope；inbox:該合成persona能力 | allowlisted情境/原操作 → 模擬事件；通知只讀自己的測試persona，不公開全站驗證碼 | 06、10、12 |
| OP-36 | POST `/admin/demo/reset-previews`；POST `/admin/demo/resets` | Owner＋demo.reset permission/scope | datasetId → 計數與短效preview；確認同版本dataset＋preview → 僅指定合成資料重建 | 11、12 |
| OP-37 | POST `/admin/returns/{id}/transitions` | Owner需履約permission/scope；指派Support維持MFA | Accept/Reject/Receive等受限command＋理由/版本 → 合法Return狀態；Received才可OP-30驗收 | 08、11 |
| OP-38 | POST `/orders/{id}/member-claims` | member＋guest.action(claim) | 原聯絡驗證／明確選擇 → 若尚未屬其他member才歸戶；不按Email批次收編 | 04、10、11 |
| OP-39 | POST `/account/email-change-requests`；POST `/account/email-changes/confirm` | 本人＋近期重新驗證/新管道challenge | 新Email → 合成驗證與舊管道通知；confirm變更、撤舊session，不變更原交易聯絡快照 | 10、12 |
| OP-40 | POST `/account/data-requests`；POST `/admin/data-requests/{id}/decisions` | 本人或public限流受理待驗證guest；Owner案件permission/scope | 最小合成聯絡／權利種類 → PendingVerification案件及統一受理；OP-46驗證原聯絡後才處理或披露資料，Owner處置留保留分類／理由；不直接抹除必要稽核 | 10–12 |
| OP-41 | GET `/admin/access-grants`；GET `/admin/users` | Owner＋read permission/scope | 範圍受限的合成帳號／權限列表；不回hash／復原碼或私人收藏 | 10 |
| OP-42 | POST `/support-cases`；GET `/support-cases/{id}`；POST `/admin/support-cases/{id}/responses` | public限流提交；本人/驗證單案讀；Owner或指派Support需案件permission/scope；非Owner維持MFA | 最小必要合成聯絡／文字 → 統一受理、案件；訪客查閱另驗證，無檔案/照片上傳 | 10、11、12 |
| OP-43 | POST `/auth/mfa-enrollments`；POST `/auth/mfa-enrollments/confirm` | 密碼已驗證的受限enrollment stage；更換需現有MFA或受控復原 | 選定authenticator challenge/proof → 綁定並驗證MFA；受限stage不能進任何業務後台 | 10 |
| OP-44 | POST `/auth/mfa-recoveries`；POST `/auth/mfa-recoveries/confirm` | 限流、帳號秘密＋單次離線復原碼或另審受控人工復原 | 驗證→撤舊管理session、受限重新綁定MFA；不以單封Email撤MFA，不公開復原碼 | 10、11 |
| OP-45 | POST `/admin/receipt-adjustments/{id}/checks`；POST `/admin/reconciliation-cases/{id}/resolutions` | Owner對帳permission/scope | 同原憑證調整查核或具原因的差異處置 → 原操作結果／稽核；不手動硬改Unknown為成功 | 06、07、09、11 |
| OP-46 | POST `/case-verifications`；POST `/case-verifications/confirm` | public限流/challenge＋request nonce | caseKind(support,data)/caseId/原聯絡管道/intent → 統一受理、單案只讀或指定動作能力；不可跨案、不能借guest.read寫入 | 10、12 |

一個OP群組含同旅程相關路徑；OpenAPI展開時每個method/path有唯一operationId，保留OP追蹤，不把群組當作一個OpenAPIoperation。退貨／帳號／MFA維護以OP-37–46銜接；guest資料請求及客服查閱另以原合成聯絡管道單案驗證，不擴張guest.read的訂單查詢用途。MFA authenticator的具體challenge DTO需先做M0實證再固定；任何未列動作不可用任意status PATCH替代。換裝API留票28，不由這份核心清單冒稱全產品API完成。

## 3. 最小請求／回應示例

OP-01 搜尋僅查詢已發布商品的公開名稱／描述與有效 SKU ID；colorId、garmentSize、availability 以同一個 active SKU＋Inventory 列匹配。`availability=in_stock` 僅回傳至少一個符合條件且 Sellable Stock 大於 0 的商品／SKU，預設仍可查看缺貨商品。q 使用 Unicode NFKC 正規化，最多 100 字；locale 僅接受 `zh-TW`、`ja`、`en`。limit 預設 20、上限 100，sort 僅 `name`／`price`；cursor 綁定搜尋與篩選條件，穩定排序並以 ProductId 作最後 tie-break。未知參數或不合法值回 422。現有資料沒有商品分類欄位，因此分類篩選須待分類資料契約確立後增加，不能由 slug 或 UI 推測分類。

```json
{
  "quoteId": "quote_demo_a",
  "cartVersion": "v3",
  "contact": {"email": "buyer@example.test"},
  "delivery": {"method": "mock_home", "region": "demo_supported"},
  "confirmed": true
}
```

OP-07只有合成fixture有效內容；Quote由後端計算，request不能覆寫price、discount、stock、Mode、MemberId或付款結果。回應包含snapshot／expires但PII查閱另驗證。

```json
{
  "operationId": "refund_demo_a",
  "mode": "mock",
  "state": "Processing",
  "amountTwd": 600,
  "displayNotice": "模擬退款，無實際資金移動",
  "version": "v8"
}
```

600僅示例合成金額，不是售價／運費建議。處理中202不等於退款完成；OP-27查核才投影受控結果，未知保留額度。

## 4. 權限與契約驗證計畫

每個敏感OP須正向＋401/403/跨資源404＋版本／重送／內容衝突，並核對DTO不含多餘PII。SSR不以middleware「已登入」取代API物件授權；SSE／polling若使用也查同範圍。所有mock情境只呼叫受控adapter入口，不暴露供應商偽webhook作顧客API。

完成OpenAPI後執行解析、所有operationId唯一、request/response/error enum一致、範例驗證及真正產品contract tests。生成器成功不是業務驗收；大多數產品檢查仍NOT_EXECUTED。Issue 11 僅對 OP-05 購物車刪除／優惠及 OP-06 quote 顯示的本機 mock 子集有 dedicated SQL／browser 證據，詳見[驗證紀錄](../../../architecture/VERIFICATION.md)；整合候選 Authority 仍是 PROPOSED，沒有因此升格為 runtime 基線。

## M0契約深化

本輪[M0準備](../delivery/technical-baseline/READINESS.md)、[身份詳規](../identity/AUTH_SECURITY_PROFILE.md)、[SQL結構](../architecture/SQL_SCHEMA_PLAN.md)銜接初次匿名去重、強ETag、退款單位分攤與官方法規格限制。非 Owner 後台角色採用 TOTP；Owner TOTP 為選配，未把 Passkey 內建認證當成原生 2FA。

## M2結帳與受保護訂單詳規

新增[M2_CHECKOUT_ORDER_PLAN](../commerce/CHECKOUT_ORDER_PLAN.md)及[GUEST_ORDER_ACCESS](../identity/GUEST_ORDER_ACCESS.md)。OP-07/09重送／Unknown／期限、OP-10完整Order read model與OP-21/22/38的read/action/claim範圍已補情境；完整機器契約仍待展開，M0subset不被悄悄修改。cancel/return/claim回應只含該動作最小結果／識別／reason/version，不因command成功順便發整份Order或PII。

## M3 OP-26群組補充（提案，非完整OpenAPI）

正常商品退款沿用原非空lines＋server原分攤；新增兩個不同案件資源：`POST /admin/payment-compensation-cases/{id}/operations`，以及 `POST /admin/order-shipping-refund-cases/{id}/operations`。兩者需Owner／refund.execute／案件scope、Idempotency-Key與If-Match；body只版本／原因／確認，由server案件綁可信來源及全剩額或符合資格原shipping額。client不可指定任意payment／bank／Amount，不能將正常案轉補償或偽造商品lines。

Unknown持續保留來源額度及適用slots，只查原Operation；不派送新同目的退款。來源重複通知不新增collection，衝突／資格失效回對應錯誤；成功退款與DEMO憑證各自投影。M0 OpenAPI仍只有正常退款子集；這兩路徑與完整M3 DTO／錯誤機器契約尚待實作前展開。詳[M3退款詳規](../commerce/AFTERCARE_REFUND_PLAN.md)。

## M1–M3整合機器契約（2026-10-13）

新增[整合OpenAPI](API_M1_M3.openapi.yaml)76 operations／112 schemas，覆蓋M1–M3商務售後及資料案件；[跨階段一致性／差異](API_CROSS_STAGE_CONSISTENCY.md)記錄M0重疊DTO擴充、權限、版本、收件與原來源退款規則。M0原檔保留；兩檔均PROPOSED，不能混用或稱相容基線。先前各節「待展開」為當時紀錄，本輪範圍已完成文件展開；MFA proof／完整帳號與授權維護／換裝仍在本檔之外。`scripts/check_kohami_api_contract.py` 驗文件形狀與合成算例；截至 2026-10-17 因環境缺少 PyYAML 未能執行。產品大多數仍 NOT_EXECUTED，Issue 11 OP-05／06 本機 mock 子集例外，證據見驗證紀錄。


## 帳號與授權維護語意補充

[帳號生命週期提案](../identity/ACCOUNT_ACCESS_LIFECYCLE_PLAN.md)深化OP-15/16/19/20/23/39/41，新增ACCT暫存維護路徑；尚未納入機器契約，不改既有76operations數量。公開受理／單次proof與版本化grant命令分離，最後Owner與撤權Started裁決待審及SQL實證。


## TOTP OP-18/19/43/44 補充契約

[獨立OpenAPI](API_TOTP_MFA.openapi.yaml)補TOTP enrollment/confirm、login proof、reauth challenge/proof、recovery challenge/confirm與codes rotation、context讀取。9operations為補充範圍，M0/M1–M3原檔不變，不能拼接當canonical；OP-17 password stage cookie與Session映射差異見[TOTP接縫](../identity/TOTP_MFA_LIFECYCLE_PLAN.md)。秘密DTO無可用範例，不使用proof冪等重播。


## 帳號／授權機器契約展開

[補充OpenAPI](API_ACCOUNT_ACCESS.openapi.yaml)涵蓋OP-15/16/19/20/23/39/41與ACCT接縫共19operations；[整合登錄](../identity/IDENTITY_API_INTEGRATION_PLAN.md)對齊member password／admin TOTP、nonce、User/access/grant版本與原商務consumer，明示尚未合併canonical。三份104操作唯一不是全產品覆蓋，換裝等範圍外不稱完成。


## Phase 2換裝API輪廓接續

[FOP-01–12](../fitting/FITTING_ASSET_OUTFIT_PLAN.md)涵蓋public resolve／受控素材、本人收藏／替代另存、guest或本人匯出及素材管理。尚未機器展開，不增加既有104個唯一操作計數；新fitting/asset權限與Owner scope需明確canonical整合，不能借用catalog.write。


## 換裝機器契約展開

[API_FITTING](API_FITTING.openapi.yaml)新增20個操作、28schemas；[整合表](../fitting/FITTING_API_INTEGRATION_PLAN.md)保留FOP追蹤，原POST suspensions改singleton control read/transition，新fitting context不借帳號proof nonce。商務76＋TOTP9＋帳號19＋換裝20＝124個唯一跨文件操作，非canonical／全產品完成。原四份契約hash不變。


## 素材建置HTTP詳規

[API_ASSET_WORKFLOW](API_ASSET_WORKFLOW.openapi.yaml)新增AWOP-01–13共16method/path，補Import/Slot、Alignment、Rights、Compatibility初建。舊20FOP操作與原基準不變；总140個跨文件唯一操作。新event3變體在補充schema中PROPOSED，原6型event registry未修改，未採用前拒/隔離；[整合表](../fitting/ASSET_WORKFLOW_API_INTEGRATION_PLAN.md)核actor/proof、版本、失回應與建立狀態。
