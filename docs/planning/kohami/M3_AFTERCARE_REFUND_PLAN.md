# M3 模擬配送、退貨驗收與管理者退款詳規

日期：2026-10-13｜READY_FOR_REVIEW｜提案，全部產品驗收NOT_EXECUTED

來源：[現行mock政策](../../../.scratch/kohami/proposals/04-demo-commerce-policy.md)、[M2訂單](M2_CHECKOUT_ORDER_PLAN.md)、[對帳與資料](RECONCILIATION_DATA_PLAN.md)、[合成案例](fixtures/M3_REFUND_CASES.json)。維持單一具refund.execute權限Owner＋MFA／近期驗證；不要求第二人或指定真人。MFA機制待決，不因規劃完成而繞過閘門。

## 1. 管理工作台與可操作範圍

SCR-09B/C/D/H依有效role/permission/案件scope讀資料：Support可受理、指派範圍內履約／驗收；Merchandiser僅商品／庫存工作；Analyst只聚合報表。Owner也不能任意改paid／refund終態、刪稽核或用特殊權限跳過額度。

每案顯示原Order/mode、原snapshot、有效payment collection、配送／Return／Inspection、退款操作、DEMO憑證與差異；未知與失敗分開。後台成功操作不等於全部子流程完成。取得資料需server物件授權，disabled按鈕只是UX。

## 2. 模擬履約與退回驗收

| 流程 | 必要command／先決條件 | 失敗／競爭與顧客結果 |
| --- | --- | --- |
| 建Shipment | 已確認有效付款＋Order有效＋未取消，授權履約角色；POST建立／版本 | 與取消鎖同Order；只有一條成立；未知payment不可出貨 |
| 模擬配送轉移 | Pending→Shipped→InTransit→Delivered；InTransit→Exception→InTransit/Returned | 記原事件識別／版本、拒舊狀態回退；不用真承運商網址或物流單 |
| 受理Return | 本人／guest action申請→Requested；Support/Owner受理Accept或附理由Reject | 無照片附件／強制註冊；不以技術Unknown當拒絕理由；正式權利基線不改寫 |
| 模擬收件 | Accepted→Received，原Line／單位／Quantity核對 | 多件／少件／錯品留例外與明細，收件不等於再售或退款 |
| Inspection | Received後逐項可再售／隔離Qty、原因、操作者／版本 | 可再售Qty≤Received；不合格隔離，不憑退款成功把庫存加回 |
| 唯一入庫 | 以原退回單位與Inspection生成一次Movement | 與退款操作分開，重送不多入庫；退貨單位不能被兩案重複收件／入庫 |

取消已付但未出貨仍走唯一反向庫存異動，不再Release consumed Reservation。已交承運需Return/Inspection，不能假裝未出貨取消。Inspection與內部核准不自行延後既有正式權利期限；本機只測機制，法定實際條款仍延期審查。

## 3. 退款來源必須分開

入帳來源不只看PaymentAttempt或EventId：同一collectionId可以收到多個通知，只有(dataset,adapter,collectionId)的一個可信collection ledger entry。新EventId且同collectionId不是額外收款；同collectionId不同金額／來源hash列異常，不覆寫或新加實收。

| CaseKind | 可退基準與額度 | 商品／庫存關係 |
| --- | --- | --- |
| OrderRefund | 已確認原有效collection＋原snapshot單位／運費，扣已成功與Processing/Unknown | 按未占用UnitAllocation ordinal固定選Quantity；既退單位不能重退；不由refund成功再入庫 |
| PaymentCompensation／晚到成功 | 取消／到期後收到獨立可信collection，本筆可退實收扣完成/未決額度；完整補償包含原運費 | 原單不復活、不占用可售庫存；不消耗正常商品退貨單位 |
| PaymentCompensation／額外收款 | 已存在有效原付款之外，另一個可信collection的可退實收 | 與原商品退款分開；不因額外款退回就把原訂單改全退或加庫存 |

每個來源有獨立Collected/Refunded/Pending/Available；所有來源合計完成＋未決不得超可信總實收。正常OrderRefund還受原商品／shipping額度限制。案例種類與collection source由後端案件決定，不接受client任意轉成補償來避開品項上限。

M0退款DTO是正常OrderRefund子集，lines不可空；M3補償提案新增 `/admin/payment-compensation-cases/{id}/operations`（OP-26群組延伸）、同permission/MFA/scope，request只需原case版本／原因與確認，不接受任意bank或另指定payment。server計算可退本筆全餘額；已有Unknown不能重發同補償。這是新機器契約待展開，沒有改動或聲稱M0 API已支援補償。

符合既定整單解除／運費退還條件且商品已退完時，可建立shipping-only案件。M3另提 `/admin/order-shipping-refund-cases/{id}/operations`（OP-26群組延伸）；後端綁定原正常collection、資格與未占用原shipping額度，request只有版本／理由／確認，不允許任意Amount。此路徑不要求商品lines、不消耗商品單位，不可偽造已退商品Quantity滿足M0 DTO。例：原1590、商品已退1530、無未決，剩原shipping60且資格成立可退60；資格不成立或shipping已完成／Unknown即拒絕。M0非空lines契約維持，完整M3 OpenAPI仍待展開。

## 4. 一次管理者操作到顧客狀態

1. 讀案件與可信來源、原退款明細、已完成／未決保留額及單位，顯示可退額與拒絕原因。
2. Owner選符合案件的原Line/Quantity、是否原shipping、原因；server依原UnitAllocation算Amount。全額取消／補償依既定政策含原shipping；部分退貨不追補免運，shipping本來源最多退一次。
3. 確認視窗顯示「模擬退款，無實際資金移動」、來源／金額／品項／案件。近期MFA／撤權／版本失效時先處理，不以關掉視窗自動重送新操作。
4. POST帶同Idempotency-Key/If-Match，原子保留collection amount＋正常退款的unit/line/shipping slots，建Operation/Outbox/Audit。顧客顯示Processing，不先報成功。
5. 受控adapter明確成功→Succeeded；明確未受理→Failed並釋放保留；逾時／未知→Unknown保留所有額度及單位，只查原操作。重複／亂序結果不可多退或覆寫成功。
6. 同Order其他獨立符合條件單位是否可退款，由該案件／原Line與所有未決slots核對：不能重發Unknown同目的退款；其他未占用單位仍須新案件／來源有效且總額足夠，不因畫面有剩額就任意指定Amount。

固定算例：原實收1590；已退552，另一原單位Unknown553，可用485（425商品＋60shipping）；正常部分退另一單位可提425，不任意退485或再退553。原單位與shipping分開，整單剩額不是任意可扣款額。

撤權：未Started的queue新派送拒絕；能證明未發起才確定Failed並釋放。queue claim／lease不是已發起證據。mock啟動裁決與有效SecurityVersion／permission一起有界核對並記Started/Audit；撤權先成立則不得開始，Started先成立則結果仍由系統查核。不要靠Actor還登入才處理result。正式外部呼叫的更嚴密授權／派送邊界留真串接階段，不把本機實證當真資金保證。

## 5. 憑證、結案與客服

正常可信付款建立DEMO憑證；普通退款來源生成同原憑證調整，部分／全額明細可追溯。額外/晚到collection只有與該來源相關的模擬收據/補償紀錄，不以額外退款改寫正常商品憑證。沒有真實發票／折讓號碼或稅務效力。

Refund succeeded不等於Receipt adjustment confirmed；憑證Unknown/Failed維持獨立待辦，重查／明確未受理重試同原調整，不能回滾已退結果或重建退款。顧客看退款完成＋憑證處理中，不要為了「整案綠燈」硬改成功。

案件結案須該來源的退款／憑證調整／Return與庫存（如適用）／對帳差異各有明確完成或經審閱的N/A理由；客服回條不算實質處置。補償案無商品退回可以N/A，但不是默默略過未知。直接換貨／延伸退貨期仍未採用，售後入口維持原政策。

## 6. 驗收（全部產品NOT_EXECUTED）

| ID | 可觀察預期 | 追蹤 |
| --- | --- | --- |
| M3-AC01 | 未付/Unknown/Cancelled不能Shipment，取消/出貨競爭唯一，配送事件不回退 | 18、OP-29、DEMO-08 |
| M3-AC02 | Return受理/Received/Inspection分開，少件/隔離可見，同單位只入庫一次 | 19、OP-30/37 |
| M3-AC03 | Support可提案但不能退款；Owner有scope/permission/近期MFA才POST，改前台flags無用 | 20、OP-25/26、DEMO-05 |
| M3-AC04 | 1590/552/553/485/425算例與原單位分攤固定；超額/負額/已退Qty/重退shipping拒絕 | 20、DOM-07、DEMO-07 |
| M3-AC05 | samekey重送一Operation；Unknown保留並只查原操作，明確未發起失敗才釋放 | 20、OP-27、Q-06 |
| M3-AC06 | 同collection多Event不增收款；真正額外collection補償不占原商品slots | 15/20、source ledger |
| M3-AC07 | 撤權先於Started不派送；已Started結果仍追蹤；lease不算已發起 | 20/25、DOM-10/11 |
| M3-AC08 | refund成功/receipt失敗獨立，重送調整不二次退款、正常與補償來源清楚 | 21、OP-14/45 |
| M3-AC09 | 結案前核所有必要子域與差異，Unknown不硬close；客服文字與通知無秘密 | 22、OP-42/45 |
| M3-AC10 | Analyst僅聚合、清除/restore不破壞必要帳務與稽核，reset限合成dataset | 23、RECONCILIATION_DATA_PLAN |
| M3-AC11 | 管理線框／手機顧客狀態一致，三語文案／鍵盤／錯誤與MFA失效可操作 | SCR-09C、CHK-03–05 |
| M3-AC12 | 真SQL source/unit/shipping併發、Outbox crash/restore及台帳一致，fixture不是實測 | CHK-02/07 |

[管理退款SVG](wireframes/M3-admin-refund-desktop.svg)／[PNG](wireframes/M3-admin-refund-desktop.png)是靜態規劃：Owner／近期驗證僅示意，不稱MFA已完成，不可點擊資金操作。票18–23依原任務圖推進，本輪不解除依賴／標resolved。

## 規劃檢查證據（2026-10-13）

合成來源金額／單位／運費、4種角色前置條件、M1快照一致、11種情境結構、SVG XML與1440×980 PNG、文件連結、6票狀態／驗收清單未變及18檔範圍已核對。管理線框已靜態渲染並檢視；只證明規劃示意可閱讀。獨立唯讀覆核指出運費單獨退款需完整契約，本版已補明獨立案件路徑；M0 DTO不變。全部M3-AC產品案例仍NOT_EXECUTED。
