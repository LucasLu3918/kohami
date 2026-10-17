# SQL Server 結構與交易設計提案

## 現行適用範圍（2026-10-15）

本文保留需求、設計與具日期的驗證依據；目前已有 .NET／Nuxt／SQL 程式、core34 採用子集與部分產品實證。完整 133 操作候選已建立，核心細則已部分採用；其餘功能／Phase2 不因此視為已批准或完成。產品逐項狀態、範圍與來源優先順序請由[文件入口](../../../README.md)查閱；舊段落中的尚未建立／未決／全部未執行須依原日期與案例範圍解讀。


日期：2026-10-13｜READY_FOR_REVIEW｜沒有執行DDL／migration

來源：[領域](DOMAIN_MODEL.md)、[API](../contracts/API_SPEC.md)、[M0](../delivery/technical-baseline/READINESS.md)。本文件把邏輯約束映射為候選SQL結構，不替代實際migration／整合測試。沿用獨立KohamiTest開發DB，表名與索引屬提案。

## 1. 型別與跨資料集隔離

- 主體ID候選uniqueidentifier，對外以不透明字串表示；所有商務資料包含DatasetId。跨表FK使用(DatasetId,Id)複合關聯及對應unique約束，不只前台傳dataset filter。
- 金額bigint、整數TWD；API數值限制0..9007199254740991以維持JavaScript安全整數，運算以checked精確整數／更大中間值並查輸出溢位，不用float。此為技術資料範圍，不是正式售價／退款政策上限。退款操作Amount>0，Quote／Snapshot總額按零元訂單不開放規則處理。
- Qty候選int正整數，所有乘法／總和先檢查；合成資料使用小數量。時間datetime2(7) UTC；顯示Asia/Taipei；Expiry由伺服器時計建立。
- 可變實體使用rowversion，對外ETag為base64url包裝的不透明版本；它不是日期時間或商業event序號。[Microsoft rowversion](https://learn.microsoft.com/en-us/sql/t-sql/data-types/rowversion-transact-sql?view=sql-server-ver16)（2026-10-13核對）。實際強ETag須對回應representation變化有效，聚合讀模型另用父Version或組合digest，不能只用沒變的Order行版本。
- 可列舉狀態用nvarchar＋CHECK／EF轉換固定值，禁止任意status輸入。mode以CHECK限定mock；沒有real配置或混合資料升格。

## 2. 表與索引

| 表群組／候選鍵 | 欄位摘要／關聯 | 唯一／CHECK／主要索引 | 首批 |
| --- | --- | --- | --- |
| DemoDatasets | DatasetId PK、FixtureVersion、Mode、ResetVersion、State | Mode=mock；重設版本匹配 | M0 |
| Identity users／claims＋AccessGrants | Identity UserId、DatasetId、EmailVerified、SecurityVersion；Actor、Permission、Scope、起迄 | 同資料集合成Email唯一；授權查Actor／有效起迄；不為刪dataset直接公開密碼／credential秘密 | M0 |
| AuthChallenges／Sessions | Id、DatasetId、UserId?、ResourceKind/Id、Intent、NonceHash、ProofHash?、ExpiresAt、ConsumedAt；SessionSecurityVersion | 原challenge與消耗原子化；查expiry；credential proof不保存原文 | M0 |
| Optional authentication credentials | 採用獨立登入強化前不進入本基線；資料欄位、保護與復原方式見[選配計畫](../options/ADMIN_MFA_HARDENING_OPTION.md) | 待另行採用與驗證 | Option |
| Products／Skus／Translations | Dataset、商品slug、SKU顏色尺碼、價、狀態、Version；Locale內容 | dataset內SKU唯一、(Product,Locale)唯一；價≥0 | M0 fixture，M1完整 |
| StockBalances／Movements | dataset/sku、OnHand、Reserved、Version；Operation/OrderLine、Kind、Qty | CHECK OnHand≥Reserved≥0；(SourceOperation,Line,Kind)唯一，查sku／原異動 | M0 |
| Carts／CartLines／Quotes／FeeVersions | Owner或能力Ref、Version；原費用版本／expiry、總額 | CartLine(cart,sku)唯一；固定fee非負；Quote immutable | M0 fixture Quote，M1完整 |
| Orders／OrderLines／UnitAllocations | Order／CheckoutId、Dataset、Member?／GuestContactRef、State、Version、快照；每單位商品實付分攤 | CheckoutId唯一；immutable snapshot；unit ordinal唯一；各Qty／amount約束及業務總和核對 | M0 |
| Reservations／ReservationLines | Order、ExpiresAt、State、各SKU Qty | Order一個reservation；Held/Consumed/Released；Expiry/State索引，Qty>0 | M0 |
| PaymentAttempts／PaymentEvents | Order、原金額／幣別、State、Version；Adapter/EventId/Hash | Event(dataset,adapter,id)唯一；有效Attempt生成鎖Order裁決，不只查再insert | M0 |
| RefundCases／RefundOperations／RefundLines／RefundBalances | 案件、Actor、明細；Processing/Unknown/Succeeded/Failed；每原OrderLine可退Qty/實付額與shipping額度 | 鎖同Order與原Line額度，完成＋pending不超原實收／原Qty；shipping原額度唯一，Unknown保留 | M0邊界實證，M3完整 |
| Shipments／Returns／ReturnLines／Inspections | Order、State、Version；原Line、Qty、再售Qty／結果 | 再售Qty≤ReceivedQty；出貨／取消鎖同Order；入庫Movement唯一 | M3 |
| DemoReceipts／Adjustments | DEMO識別、Order、RefundOperation、Amount、State | 原來源Operation唯一；同dataset FK；退款結果與憑證分開 | M0 mock契約，M3完整 |
| IdempotencyRecords | Dataset、ActorOrCapabilityId、OperationKind、Resource、KeyHash、RequestHash、ResultRef | 同scope/key唯一；保留隨原操作生命週期，不以24小時cache過期重建 | M0 |
| Outbox／AdapterOperations | EventId、Dataset、Kind、原操作Ref、State、LeaseOwner/Until、NextAttemptAt、Attempts | consumer/Event去重；有界lease及掃描索引；Unknown不是新派送 | M0 |
| AuditEvents | Dataset、Actor、PermissionSnapshot、Operation、Reason、Outcome、Time、TraceId | 日常只讀；沒有任意刪改API；不包含完整request或秘密 | M0 |
| Notifications／SupportCases／DataRequests | Persona、原事件、內容、State；CaseKind/原聯絡、PendingVerification、Assignment、Version | 驗證依ResourceKind/Intent隔離；單案scope；必要最小資料 | M0通知，M3案件 |

M0實證可先採最小相關表與合成fixture，但每個宣稱通過的案例必須跑真SQL約束；不要用in-memory EF／SQLite替代SQL Server並行證據。ASP.NET Identity內建表由選定框架migration產生，不手寫未知欄位；本表是額外dataset／session／權限需求。

## 3. 原子交易與鎖定策略提案

先讀後寫／rowversion只會偵測衝突，不足以保證多SKU保留或退款額度。本機提案採短交易、條件UPDATE＋必要UPDLOCK/HOLDLOCK；明確記錄隔離級別與SQL Server設定，M0比較／驗證死鎖及吞吐。這是工程候選，不宣稱查過文件就已證明正確。

1. 多SKU保留：排序所有SkuId，以固定順序取得／更新StockBalance；條件OnHand−Reserved≥Qty。任一不足全rollback；Order／Snapshot／Reservation／Idempotency／Outbox一起commit。
2. 付款／到期／取消：鎖Order及Reservation，先去重原操作／EventHash，決定Consume或Release其中一條；Consumed扣OnHand與Reserved，Released只扣Reserved。晚到成功留Cancelled/Expired＋補償，不能再搶庫存。
3. 已付未出貨取消：同Order鎖與Shipment創建競爭；唯一反向Movement回補原OnHand，不能同時Release＋再次回補。
4. 退款：固定Order→CollectionBalance→UnitAllocation排序→ShippingBalance順序；原子保留Qty／PaidAllocation／Amount。按原OrderLine的未占用UnitAllocation ordinal升序固定挑選單位，Operation綁定所選UnitAllocationId及原實付。選取、佔用與總額同交易；原單位不可被另一操作重退。相同操作重送沿用所選單位，Unknown不釋放。兩筆退款即使總額未超收款，也不能對同原商品單位重退。Unknown同時保留三種額度，Failed須確認未受理才釋放。
5. Adapter結果／outbox：交易之外做受控模擬工作，交易內套結果／去重／稽核；至少一次投遞靠Idempotency及事件鍵，SQL exception／deadlock重試以同原操作，不能重建退款／延展Reservation。
6. 撤權派送：claim outbox不等於已發起外部工作；新派送前再次驗有效權限與SecurityVersion，與撤權訂定可觀測裁決邊界。已發起結果由系統追蹤，不靠操作者仍登入才入帳。

## 4. Migration、還原與未決

實作首migration前記schema版／對應Git SHA；乾淨DB與已有seed DB都驗upgrade。不可逆變更需rollback/forward-fix計畫，不用demo reset代替migration rollback。本輪不執行migration、備份、刪庫或SQL命令。

restore於新目標DB，用同fixture及未決操作核對：OnHand/Reserved、RefundBalances、事件去重、outbox、撤權／刪除清單。既有SQL環境restart smoke不代表產品restore。

以下須M0實證後固定：聚合ETag形式、具體鎖定／死鎖重試、身份migration版本與選配登入credential儲存、加密key／DataProtection持久化。這些有明確候選與驗收，不以未完成實證稱最終Schema已採用。

## M3 來源與保留結構補充（提案，未執行DDL）

新增CollectionLedger：DatasetId/Adapter/CollectionId唯一，OrderId/PaymentAttemptId、CollectedAmount/Currency、SourceFingerprint與可信確認時間；另CollectionBalances記已成功／未決金額。正規化fingerprint不含EventId或投遞時間，各PaymentEvent保留PayloadHash，來源衝突進差異案件。

RefundOperation必需SourceCollectionId及CaseKind；同dataset FK。正常OrderRefund的UnitAllocationRef／ShippingReservation約束原來源與原snapshot；補償不建立商品RefundLine或StockMovement。符合資格的shipping-only案允許無商品明細，只保留原shipping額度，不變更M0非空lines DTO。每來源Succeeded＋Pending≤Collected，正常unit/shipping不得重占；交易鎖序統一Order→CollectionBalance→排序UnitAllocation→ShippingBalance，使用條件更新與必要鎖，須真SQL併發實證。

DemoReceipt/Adjustment也綁SourceCollectionId，額外／晚到來源不改正常商品憑證。reset採DatasetResetRevision及新資源識別或版本化歷史Ref；Audit保存最小原資源快照，禁止cascade刪除稽核或讓舊Audit誤指重建fixture。archive/FK／restore策略須真SQL證據，詳[資料提案](../commerce/RECONCILIATION_DATA_PLAN.md)。

## M1–M3整合契約銜接（提案）

Quote保存原Line內容／ordinal與金額分攤；OrderLine/UnitAllocation實體ID在checkout同交易生成，凍結原快照，不能以Quote假設已存在OrderFK。RefundCase增加Basis（Merchandise/ShippingOnly/CollectionCompensation）與CaseKind配對檢查，shipping-only綁原正常來源，晚到／Extra由可信Collection事件產生。ReturnReceivedUnits與InspectionUnits需同dataset／Return／原OrderLine/unit關聯，唯一原單位驗收／入庫，exceptions另留少件／多件／錯品，不能入原商品slot。見[整合一致性](../contracts/API_CROSS_STAGE_CONSISTENCY.md)，本輪未執行migration。


## 身份認證資料接縫

認證器憑證儲存與鎖序仍屬選配身份強化議題；本計畫不新增登入因子資料表或鎖定任何方案。


## 帳號／授權機器契約的版本接縫

[身份整合](../identity/IDENTITY_API_INTEGRATION_PLAN.md)分開User rowversion、target access-set version與grant version／ETag；public browser nonce、member近期email.change、current email request／normalized唯一性與LastOwner鎖需原子持久化。未來grant與expiry每請求核server時鐘，不以batch延後撤權；沒有DDL或SQL實證。


## Phase 2素材與收藏增補（尚未migration）

依[FITTING_ASSET_OUTFIT_PLAN](../fitting/FITTING_ASSET_OUTFIT_PLAN.md) §7：不可變revision/hash、每revision/view唯一、權利用途／期限與版本、完整Compatibility key、Member/Dataset quota鎖、export actor／selection hash／expiry、唯一撤銷事件及Audit/Outbox。刪除檔案不得cascade刪Saved Outfit；撤銷先拒絕交付再重試清理。FK／索引／rowversion與競爭SQL待實作驗證，不把資料表候選視為已部署。
