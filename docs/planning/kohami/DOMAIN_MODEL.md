# 本機 mock 核心：領域與資料契約提案

日期：2026-10-13｜READY_FOR_REVIEW｜非已執行migration／正式Schema

來源：[政策](../../proposals/kohami/04-demo-commerce-policy.md)、[術語](GLOSSARY.md)、[API](API_SPEC.md)。DOM編號為本規劃內穩定追蹤；所有金額TWD整元、時間UTC儲存／Asia/Taipei顯示，輸入不接受前台指定權限或最後狀態。

## 1. 實體、所有權與約束

| ID／實體 | 必要欄位與關聯（邏輯，未定實體SQL名稱） | 核心約束 |
| --- | --- | --- |
| DOM-01 Product／SKU／Translation | ProductId、Slug、Locale、SKU、顏色、尺碼、材質、量測、公開狀態、示意價、素材權利來源／版本 | SKU唯一；(ProductId,Locale)唯一；下架不刪歷史快照 |
| DOM-02 StockBalance／Movement | DatasetId、SkuId、OnHand、Reserved、Version；MovementId、SourceOperationId、Kind、Qty、OrderLineId | OnHand≥Reserved≥0；可售=OnHand−Reserved；同業務異動唯一，不用人工庫存調整覆寫已承諾 |
| DOM-03 Cart／Quote／ShippingFeeVersion | CartId、Owner或匿名能力範圍、Lines、Version；QuoteId、SKU／價／優惠／示意運費版本、ExpiresAt | Cart不保留庫存；Quote只是報價，建立訂單時重驗；費用不能缺值默認0 |
| DOM-04 Order／OrderLine／PriceSnapshot | OrderId、CheckoutId、DatasetId、Mode、MemberId?、GuestContactRef、各狀態、Version；原單价／優惠分攤／商品及運費實付、Qty | Mode=mock；快照不可變；保存來源SKU與展示名稱，改商品不覆寫 |
| DOM-05 Reservation／ReservationLine | ReservationId、OrderId、SkuId、Qty、ExpiresAt、State、Version | 首建15分鐘；一筆訂單最多一個有效保留，所有SKU同一交易承諾；重送不展延 |
| DOM-06 PaymentAttempt／AdapterEvent | AttemptId、OrderId、Amount、Currency、State；DatasetId、Adapter、EventId、PayloadHash、ReceivedAt、ProcessedAt | 操作／事件去重；原交易／金額／幣別匹配；未知不誤判失敗，事件不可跨資料集 |
| DOM-07 RefundCase／RefundOperation | CaseId、OrderId、理由／明細／申請者；每OrderLine的RefundedQty/PendingQty與實付額度、原運費一次額度；OperationId、Amount、State、ReservedAmount、ActorId、PermissionSnapshot、Version | Owner具refund.execute＋MFA；原快照及品項可退量核對；累計完成＋未決保留≤已確認模擬實收；各原品項數量／實付額及運費也須原子保留，Unknown保留至確認；按原UnitAllocation未占用ordinal升序固定選取，Operation綁單位實付，重送不換單位 |
| DOM-08 Shipment／Return／Inspection | ShipmentId、OrderId、State、Version；ReturnLines／Qty；Inspection結果、可再售數量、檢查者與時間 | 付款確認且訂單有效才出貨；退回不等於合格，入庫異動唯一 |
| DOM-09 DemoReceipt／Adjustment | ReceiptId(DEMO-)、OrderId、金額與來源；AdjustmentId、RefundOperationId、Amount、State | 無稅務效力；每原業務操作有效憑證／調整唯一，退款成功不等於調整成功 |
| DOM-10 Identity／AccessGrant／GuestChallenge | UserId、Email驗證、PasswordHash、Role／Permission、MfaState、SecurityVersion；ChallengeHash、ResourceKind/ResourceId、Intent、RequestNonce、expires／consumed | 密碼／token不公開；角色撤銷使新請求失效；訪客限單筆，不憑Email自動歸戶 |
| DOM-11 Audit／Outbox／Idempotency | 操作者／範圍／原因／結果／時間；事件／payload最小必要欄位／投遞狀態；Scope、Key、RequestHash、ResourceRef、Result | 交易與outbox一起commit；操作識別跨重啟有效，不能短期cache到期後重建同交易 |
| DOM-12 DemoDataset／Notification／SupportCase／DataRequest | DatasetId、fixture版本、CreatedAt、ResetVersion；收件persona、事件、內容／狀態；CaseId、Kind、原聯絡Ref、MemberId?、PendingVerification/Verified/Processing/Closed、Assignment、最小內容、Version | 僅合成資料；收件匣授權；public受理不授案例讀寫，單案原聯絡驗證後才處理資料權利；challenge依ResourceKind/Intent隔離不可跨用；重設只限指定隔離資料集，不刪其他環境 |

後續物理Schema由上述約束映射；候選EF Core rowversion配合交易、條件更新／鎖定及唯一索引。**單有rowversion或先查再寫不能證明不超賣／不超退。** EF Core可偵測更新競爭；業務裁決與重試仍須實作驗證。[Microsoft EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)（核對2026-10-13）。

## 2. 分離狀態機

| 子領域 | 合法主要狀態／轉移 | 不變條件 |
| --- | --- | --- |
| 訂單承諾 | AwaitingPayment → Confirmed 或 Cancelled／Expired；Confirmed只有未交承運且取消交易裁決成功才→Cancelled並建立獨立退款案件；取消／到期晚到成功保留原狀態＋CompensationRequired | 不使用一個status涵蓋所有子領域；Confirmed不被舊失敗事件回退 |
| Reservation | Held → Consumed 或 Released | 付款確認時OnHand−Qty、Reserved−Qty並記唯一扣減；未付取消／到期Reserved−Qty一次 |
| 付款 | Created → Processing → Succeeded／Failed／Unknown；Unknown可查核至Succeeded／Failed | 沒有有效Processing/Unknown/Succeeded且已明確失敗、保留仍有效才可新嘗試；成功後舊非終態／失敗不降級；額外收款另列異常 |
| 退款案件 | Requested → Submitted／Rejected／Withdrawn；Submitted的結果由操作投影 | 未發起才可拒絕／撤回；客服可提案，執行只有具權限Owner |
| 退款操作 | Processing → Succeeded／Failed／Unknown；Unknown只能查同操作→Succeeded／Failed | Processing/Unknown保留額度；Failed須證明未執行才釋放；重試新操作須同案件且重新授權 |
| 配送 | Pending → Shipped → InTransit → Delivered；InTransit→Exception→InTransit／Returned | 都是mock；取消與Shipped同版本交易競爭；Delivered不回退 |
| 退貨與驗收 | Requested → Accepted／Rejected；Accepted→Received→Inspected；Inspected明細區分可再售／隔離 | 每項數量≤原可退數量；入庫不由退款成功觸發 |
| 憑證／調整 | Pending → Processing → Confirmed／Failed／Unknown；查核與同操作重試 | 退款與稅務示意狀態獨立；未付取消不產生已付憑證 |

取消已付款但未交承運：同一交易禁止出貨、建立退款案件；原保留若已Consumed，只以唯一反向Movement回補原扣減，不再釋放Held。無法確認原異動／履約狀態列例外，不盲目補庫存。退款未知不改取消結果。已交承運轉Return／Inspection，不走直接回補。

## 3. 寫入交易與事件邊界

- 建立checkout：驗證Quote／身份／所有SKU → 原子保留所有SKU → 建Order＋Snapshot＋Reservation＋Idempotency＋Outbox，一起commit；任一SKU不足全部rollback。
- 付款結果：驗證EventId/PayloadHash一致（同ID不同內容列異常，不默默覆寫）／去重Event → 驗證原操作 → 以Order／Reservation版本裁決 → 扣減或晚到成功補償案件 → 同交易記Outbox／Audit。Adapter收到成功不等於已確認訂單成立。
- 發起退款：鎖定／條件更新同Order退款額度，確認權限與可退明細 → 原子保留Amount、各OrderLine可退數量／分攤金額、原運費可退額＋建唯一Operation＋Outbox＋Audit。無法取得可退額不能先發adapter再扣。
- 應用mock結果：系統查原Operation及Event去重，處理Succeeded／明確Failed／Unknown及額度，獨立建Receipt Adjustment待辦。重送先驗現行身份再辨識原操作／版本；Actor撤權後禁止新派送；已發起結果由系統接收並對帳，不遺失結果。
- Background worker：續處理持久化Outbox、15分鐘到期、同操作查核與憑證調整；重啟不重新起時計，不丟unknown額度。測試clock可注入，但只有受控測試入口可修改，不接受顧客請求任意時間。
- Delivery策略提案：Outbox至少一次投遞，consumer去重；未知退款重查而非重發；明確未受理失敗才可重試dispatch。同操作異常保留原因、次數、NextAttemptAt；達測試情境預設3次查核仍未知時進管理待辦，不釋放額度或變Failed。重試限制是工程預設，不是服務期限。

## 4. 權限與資料生命週期

訂單所有權每次後端查核；初次checkout可發分離短效checkout.read（同瀏覽器看非PII狀態／總額）與checkout.pay（只發該筆有效保留內付款命令）能力，兩者均不能作完整訪客訂單／售後授權。後續完整查閱須會員所有權或guest.read；取消／售後須會員所有權或另取得guest.action的原聯絡驗證及明確動作綁定，read只讀權不能直接寫入。查詢權不授權更改身份、收件人或退款去向。

會員登入不自動接收guest訂單；若實作主動歸戶，必須具名操作＋原聯絡驗證＋未屬另一會員。正式個資保存分類仍延期，本機只用合成資料，測試清除／保留／備份重套刪除清單機制仍可驗證。必要金融稽核不得被角色或顧客任意改寫；demo重設須環境及資料集雙重限制。

M0物理型別／額度與固定單位分攤候選見[SQL結構提案](SQL_SCHEMA_PLAN.md)；本輪未執行DDL／migration。

## M3 來源台帳與退款種類補充（提案）

DOM-06新增CollectionLedger，唯一(dataset,adapter,collectionId)；Event去重與金融來源去重分開。SourceFingerprint僅含正規化金融綁定／金額／幣別，各Event另驗PayloadHash；同來源衝突列異常、不重複入帳。

DOM-07每RefundOperation必綁SourceCollectionId及CaseKind。OrderRefund保留原單位／原shipping與來源金額；PaymentCompensation限該晚到／額外collection餘額，不佔原商品slots、不回補庫存、不改原訂單全退。符合資格的shipping-only案件只佔原shipping與來源額度，不虛構商品明細。任一來源完成＋未決≤可信實收，所有來源合計亦核對；Unknown保持保留。

付款套結果同交易核Event與Collection唯一性，再裁決Order/Reservation與來源分類；退款固定Order→CollectionBalance→排序UnitAllocation→ShippingBalance鎖序，補償略過商品／shipping slots。DOM-10/11的撤權／Started裁決、憑證獨立結果與稽核同[M3詳規](M3_AFTERCARE_REFUND_PLAN.md)，資料生命週期見[對帳資料提案](RECONCILIATION_DATA_PLAN.md)。

## 整合API的識別與收件銜接

[跨階段一致性](API_CROSS_STAGE_CONSISTENCY.md)：Quote原金額／ordinal與公開內容凍結，但尚無OrderLine/unit實體識別；checkout原子分配非空原單位ID並保持不變。DOM-07 CaseKind＋Basis區分正常Merchandise、ShippingOnly與CollectionCompensation，來源與slots分開。DOM-08 AdminReturn讀已收原單位、Inspection及少件／錯品例外；Inspection只此案實收未驗單位，例外不自動變原商品可售庫存。這些仍需真SQL／API實證，沒有改DDL。


## Phase 2素材、收藏與匯出增補（邏輯提案）

[FITTING_ASSET_OUTFIT_PLAN](FITTING_ASSET_OUTFIT_PLAN.md) §7定義Asset Revision／Rights／Compatibility／Saved Outfit／Quota／Export／Revocation約束。同版bytes不可變，收藏按原版本且展示／下載權利逐次驗，商品可購買性獨立；DOM-01–12原商務模型不改，新增物理表未實作。

## 會員儲值餘額與履約補充（提案）

後續錢包邏輯概念、訂單／Shipment／Return／Refund 分離狀態、帳本與扣抵交易不變條件見[會員儲值與履約詳規](WALLET_SETTLEMENT_SHIPPING_PLAN.md)。實付與贈送餘額分帳；帳本是不可變來源，讀取餘額可用投影；餘額扣抵與訂單快照、庫存異動、冪等、稽核及 outbox 原子提交。本節不新增已批准 DOM 編號、SQL schema、API 或事件，不改 M0 mock 的 PaymentAttempt 契約。
