# M2 結帳、付款結果與自己的訂單詳規

> **現行 MFA 決議（2026-10-13）：** 使用者明確「先採用B」，第一版後台採 Email／密碼＋驗證器 TOTP，Passkey 留後續。下文先前「待決／Passkey優先」是歷史狀態，已由此決議取代。方案選擇阻擋已解除；註冊／proof／復原機器契約與產品實證仍待完成，不能標為 PASS。詳見 [身份詳規](AUTH_SECURITY_PROFILE.md)。

日期：2026-10-13｜READY_FOR_REVIEW｜提案／合成情境，產品NOT_EXECUTED

來源：[現行政策](../../proposals/kohami/04-demo-commerce-policy.md)、[M1報價](M1_CATALOG_PLAN.md)、[訪客存取](GUEST_ORDER_ACCESS.md)、[API總綱](API_SPEC.md)、[情境fixture](fixtures/M2_ORDER_CASES.json)。全外部商務服務mock、本機限定及15分鐘Reservation已確認；後台 MFA 已選 Email／密碼＋TOTP，註冊／proof／復原契約與產品實證仍未完成。

後續會員儲值閉環方向見[儲值與履約詳規](WALLET_SETTLEMENT_SHIPPING_PLAN.md)：會員登入後以已確認的實付／贈送分帳餘額扣抵；錢包扣抵、訂單、庫存異動須同 SQL transaction。此為 M0 mock 之後提案，不改本文件目前的 mock `PaymentAttempt`、訪客 mock 流程或 NOT_EXECUTED 狀態；未來錢包階段須另展開合約與驗收。

## 1. 從費用確認到建立訂單

SCR-04B確認合成SKU／Quantity、TWD商品／優惠／示意運費、聯絡Email、模擬配送方式與條款標示；訪客不必註冊，會員不要求非必要性別／生日／照片。只收合成persona資料；正式配送姓名／電話／地址需求延期，不把本輪mock區域當真配送地址驗證。

OP-07以同一Quote、CartVersion、Idempotency-Key建立Order/Snapshot/Reservation；server重新驗全部SKU／Quote expiry／費用／身份／總額，任何一件不足全rollback。回checkoutId/orderId、原snapshot、ExpiresAt及分離read/pay能力；未收到回應也不換key，重送先驗目前合法能力，再找原操作，不能因Quote已過期而再次建單。

建單後Cart不變成歷史單據；原OrderLine保存商品／語言內容與分攤。建議顧客成功確認後保留本次原車可檢視，但在UI明示已有原checkout，繼續同筆不重建；若另開新購物車則生成新操作並重確認，不用多按付款按鈕當第二張訂單。

登入中斷提案：不自動合併不同來源cart、不因Email相同歸戶；保留當前cart/原browser能力，登入後明確選擇是否使用當前車。原anonymous操作重送仍沿原BrowserId binding，不把它改為新member操作；完整歸戶另用OP-38。這是待採用UX規則，沒有修改現有app。

## 2. 模擬付款與不確定結果

OP-09只在有效checkout.pay或本單member、原Order有效/Held/未到期、沒有Processing/Unknown/Succeeded有效嘗試時允許建立。首次Created可發起；明確Failed且Reservation仍有效才可新Attempt，重試不展延Expiry。

Payment adapter的情境由受控後端及Owner test設定決定，不在顧客UI暴露「選成功／直接改paid」；顧客只有開始模擬付款、查看結果與受保護取消／客服入口。畫面不能要求真卡號或銀行帳號。

建議查核UX：第一次讀狀態後，每3秒查同checkout，60秒仍未結論顯示「待確認，可稍後回來」；不持續高頻輪詢、不卡住頁面，也不把60秒稱失敗或服務期限。後端持久化查核仍依原Operation／Outbox繼續，Reload／切語言不重發付款。3/60秒是本輪工程提案，正式效能/維運實證後調整。

| Order／有效付款 | 頁面文字與允許入口 | 庫存／補償 |
| --- | --- | --- |
| AwaitingPayment＋Created/Failed，Held有效 | 待模擬付款；可開始或明確失敗後重試，取消須適當授權 | 既有15分鐘，不展延 |
| AwaitingPayment＋Processing/Unknown | 模擬付款處理中／待確認；查看同筆、驗證取消或客服，不可另付 | Held至ExpiresAt；未知不是Failed |
| Confirmed＋Succeeded | 模擬付款已確認；到自己的訂單／模擬配送 | Held→Consumed一次 |
| Cancelled/Expired＋尚未Success | 已取消／保留到期；付款仍可能查核，不再支付 | Held→Released一次 |
| Cancelled/Expired＋Succeeded | 原單維持取消／到期；晚到成功已提出全額模擬退款待處理 | 不再保留庫存／出貨；CompensationRequired與RefundCase獨立 |
| Confirmed＋額外成功收款事件 | 原有效付款不降級；額外事件進異常／補償查核 | 不多扣原訂單庫存，原每筆模擬實收分別追溯，不忽略多餘事件；額外款補償不得占用原商品退貨單位，M3依獨立入帳來源展開 |

權威時間提案：在SQL交易／鎖內用server UTC及ReservationState裁決；只有Held且serverNow<ExpiresAt才能consume；到期等號視為無有效保留。已Release後，即使event自帶較早paid_at也不能恢復。倒數是提示，不以browser clock或URL強制付款／到期。這是既定15分鐘規則的工程邊界解讀，需M2競爭情境驗證。

## 3. 訂單查閱、取消與售後入口

SCR-05B／06B查自己的Order；checkout.read只看非PII金額／付款／保留結果，不直接讀完整歷史單。guest完整查看與取消／return/claim按[存取契約](GUEST_ORDER_ACCESS.md)，只讀權不能升寫入。未付取消亦走OP-11適當member或guest.action(cancel)，不把checkout.pay偷偷當取消權。

訂單頁分區：原交易快照→Order承諾→Payment→Shipment→Return/Inspection→Refund Operations→DemoReceipt Adjustments。允許項由server授權／狀態投影；前台按鈕可灰置並提示理由，但後端仍逐次查權限。查閱頁不提供收件Email／地址／退款去向修改；帳號Email變更也不覆寫原單聯絡快照。

取消與Shipment創建同Order版本交易裁決：未付只Release一次；已付未交承運→取消＋唯一反向Movement＋退款案件；已交承運不能宣告取消成功，轉Return。退款處理中不等於款項入帳，更不自動復活原單／庫存。

售後申請提案：本人／單次guest.action(return)提交原Line與Qty；理由可選、無照片附件。受理只建Requested，不能由顧客或客服表單直接退款／入庫。具體Inspection／退款執行留M3；入口不強迫註冊或要求提供真實個人資料來測試。

## 4. 訂單讀模型與DTO邊界

OP-10完整Order DTO提案欄位：id/mode/version/currency、placedAt、localeAtCheckout、snapshot、原Line公開名/color/GarmentSize/Qty/UnitPaidAllocations、orderState/paymentSummary/shipmentSummary/refundSummary/receiptSummary、maskedContact、actions與reasonCodes。只返必要資料，不返password/token/PersonaCapability、原始adapter payload、內部審查備註或未遮罩地址。

退款summary分requested/processing/unknown/succeeded/failed與已完成／未決TWD，不用單一「已退款」涵蓋部分/全額。receiptSummary獨立；refund成功不自動宣告憑證已完成。GET返回與各子域結果一致的聚合ETag，不只未改過的Order rowversion；有變動不被共享cache或舊SSR當作終態。

## 5. 通知與恢復

交易commit與Outbox一起寫，合成投遞persona由原交易聯絡綁定，不依任意clientemail。通知key由dataset＋原business event＋recipient/channel去重；發送失敗不rollback訂單或二次付款。舊／亂序通知須帶發生時間與指向受保護查閱入口，不以老郵件内容覆寫目前可信狀態。

付款Unknown不寄「已成功」或「失敗請重付」；晚到成功通知說明原單仍取消／到期、補償待處理，不能承諾已退資金。mock投遞只隔離收件匣／站內紀錄，不外寄。

API/worker crash重啟使用同Operation/EventId/持久化deadline；Outbox至少一次去重、查核Unknown不新發付款／退款。只具checkout.read而能力失效時，回Email單筆查詢入口，不要求再付款。共享裝置登出／撤銷後清client私有payload，從server不允許舊scope繼續取資料。

## 6. 驗收（全部產品NOT_EXECUTED）

| ID | 情境／預期 | 追蹤 |
| --- | --- | --- |
| M2-AC01 | guest/member皆可費用確認；缺欄位／過期Quote／單SKU不足不留部分承諾 | 12、OP-07、Q-06 |
| M2-AC02 | 建單回應丟失同key重送只有原Order，切語言／登入不變原匿名binding | 12/16/17、DEMO-03 |
| M2-AC03 | browser success／URL偽paid不成立，後端結果核原Attempt金額／幣別／dataset | 13、OP-09、DEMO-02 |
| M2-AC04 | Processing/Unknown不能新付，明確Failed有效期內才可重試且不展延 | 13/14、Q-06 |
| M2-AC05 | 等號deadline、取消／成功／Expire競爭與crash一致，late success不復活 | 14、DOM-04/05、Q-07 |
| M2-AC06 | 原Order有效成功與額外成功事件獨立追台帳，不多扣庫存或忽略多收 | 14/15、DEMO-10 |
| M2-AC07 | read/pay不能取完整Order或取消；guest action與member所有權正反向完整 | 16、GUEST_ORDER_ACCESS |
| M2-AC08 | Email相同不自動歸戶，歸戶單次proof／競爭只能一位member，原聯絡不改 | 16、OP-38 |
| M2-AC09 | 查Order分離狀態／原價語言快照；改價／下架／退款結果不覆寫原snapshot | 16/17、OP-10 |
| M2-AC10 | 通知failed／duplicate/亂序無第二次業務效果；Unknown無錯誤成功或重付文案 | 15/16、DOM-11/12 |
| M2-AC11 | 取消／出貨衝突、售後入口無強制註冊或圖片，Requested不等於退款 | 14/16、OP-11/13 |
| M2-AC12 | 三語／手機／鍵盤與live region；私有SSR no-store/noindex，登出不殘留他人資料 | 17、CHK-03/04/05 |

本輪只核對規劃與合成case，真正SQL／API／瀏覽器／通知測試未執行。對應機器契約下一步須展開OP-10/11/12/13/14/21/22/38，沿用M0共用DTO而不改其既有authority；本輪不生成產品。

## 本輪文件與線框檢查

2026-10-13：14個交易狀態／5個存取案例、1590原快照與M1一致、SVG/PNG格式／尺寸、文件連結與票12–17狀態／依賴不變檢查通過。手機訂單線框已離線渲染與目視確認，沒有執行產品E2E。唯讀政策複核完成；Email15分鐘與MFA5分鐘候選各別限定，取消單不剝奪仍有效的售後查閱權。全部產品M2-AC仍NOT_EXECUTED。
