# 模擬對帳、最小報表與資料生命週期提案

日期：2026-10-13｜READY_FOR_REVIEW｜無真銀行對帳／法定分類結論

## 1. 對帳主體與規則

每筆可信collectionId有一份不可任意改寫來源台帳：Dataset/Adapter/CollectionId、PaymentAttempt/Order、TWD收款、原來源hash與時間；EventId用於投遞去重，不取代資金來源識別。normal/late/extra分類留Audit證據，client不能自訂。

SourceFingerprint是正規化金融欄位（原Order/PaymentAttempt綁定、金額、幣別與可信collection識別）的hash，不含EventId／投遞時間；各Event另留PayloadHash。正常重送時間不同不視為金融來源變更；同來源金融欄位衝突需查核。

可核對：Collection→Payment/Order/Snapshot→Reservation/StockMovement→RefundOperation來源與slots→DEMOReceipt/Adjustment→Outbox/通知→Audit。每來源Collected−SucceededRefund−Processing/UnknownReserved＝Available≥0；正常商品來源再核Qty與unit/shipping。Unknown不以隔日批次到期釋放，不直接生成平衡數字消除差異。

目前只採用由授權營運人員手動觸發的對帳；自動執行頻率、cutoff、jobId 與重啟補跑候選已移至[對帳排程選配](../options/RECONCILIATION_SCHEDULING_OPTION.md)，不構成本階段必要範圍或已採政策。

## 2. 差異工作台

### 重大差異通知（目前 mock 政策）

- 重大差異限定為 `NON_PRIMARY_COLLECTION_REQUIRES_REVIEW`、`COLLECTION_ORDER_MISSING`、`REFUND_PENDING_MISMATCH`、`REFUND_SETTLED_MISMATCH`、`REFUND_AMOUNT_MISMATCH`、`REFUND_SOURCE_MISSING`、`COMPENSATION_COLLECTION_MISSING`。此清單表示需要營運人員優先查核，不代表已判定真實資金損失或會計分類。
- 通知通道僅限 KOHAMI 後台站內通知。通知由案件明確指派／改派操作建立，與案件指派及冪等結果在同一 SQL 交易提交；重送同一指派不重複建立通知。收件人為該案件目前被指派、且仍具案件 `reconciliation.resolve` 權限的個人。
- 通知僅含案件 ID、finding code、指派人與時間，不含顧客資料、金額或原始金融證據。改派後原收件人的通知不再顯示；撤銷案件權限後也不再顯示。未指派差異維持在人工工作台，不寄送外部 Email、不發送群組／角色通知。
- 報表保持唯讀，載入 finding 不會寫入通知；只有明確指派時才建立站內通知。未執行中的工作台不會自動偵測新重大差異；自動偵測／排程屬選配，Email、即時推播與通知確認流程也未採用，不能視為本機 mock 已支援。

| 差異 | 可做處置 | 明確不可做 |
| --- | --- | --- |
| Payment成功無有效Order/晚到 | 核來源／原取消/expiry，建立同collection補償案件 | 強改Order成功或重新搶庫存 |
| 同collection多通知／hash不同 | 去重原來源，內容衝突警示／查核與受控事件證據 | 每Event新增一筆實收、覆寫已收金額 |
| 真正extra collection | 獨立payment compensation，不變原商品退款slots | 重複退款原商品／shipping來源 |
| Refund Unknown／掛帳 | 查原operation／adapter、保留額度與單位，顯示責任人／下一步 | 新發同目的退款、人工填成功數字 |
| Refund成功但DEMO調整失敗 | 重查同來源調整／明確未受理重試，追Outbox | 再退一次錢或rollback已退 |
| Stock不平／重複Movement | 查原業務與唯一異動，限定庫存角色具原因修正 | 直接覆寫Reserved或用refund成功補庫存 |
| 通知投遞失敗 | 同事件/收件persona重試與案件入口 | 重做付款／退款、含secret或完整PII郵件 |

差異Resolution綁來源、最小證據、reason、Actor/permission/time/版本，不能以UI「已處理」改金融終態。closed前再核新事件與未決，過期版本回412。Owner與被授權Support只在scope內看案件；沒有原始provider／SQL／銀行回應外洩。

## 3. Analyst最小報表

本機合成聚合提案只含日期、有效訂單數、各來源已確認收款、成功退款、未決保留、差異件數；註明mock與cutoff。正常商品與額外/晚到款分列，不把重複通知算銷售。RefundUnknown未入成功退款欄。

指標定義：有效訂單數以cutoff時已確認、未取消／到期的Order去重；晚到／額外collection不增加訂單數。已確認收款減成功退款是現金淨額示意，尚未扣Unknown保留；可退款餘額另扣未決，兩者不混用。主算例原來源1590−552＝1038，扣Unknown553後可退485。這些不是利潤或正式財務報表。

Analyst無會員/Email/電話/地址/單筆交易/私人收藏／身形選擇／素材選擇drilldown，沒有完整PII匯出。允許的日期區間固定server校驗，匯出也同範圍與遮罩規則；沒有Phase3轉換追蹤或個人推論。小樣本demo數字是合成資料，正式營運報表再評估可識別性與用途，不因此宣稱已匿名化。

## 4. 保存、權利請求與清除

沿用[已採用身份資料政策](../../../proposals/kohami/03-identity-permissions-data.md)的逐類暫定工程上限與例外原則，不在本輪重算法律期限或指稱所有Order需5/10年。2026-10-18 已採保存目標為：帳號未活動 24 個月後通知＋30 日、配送資料結案後 180 日、客服紀錄結案後 1 年、一般診斷／登入失敗資料 90 日、權限／安全稽核與報表讀取稽核 1 年、備份滾動最長 35 日；這些值不是通用法定期限。法定會計／發票最小分類仍待營業主體／專業審查。真正資料到期處理需該類政策／用途／起算／hold 明確，不能因分類 UNKNOWN 就任意刪證據或永久保留所有資料。

目前已實作現有來源可安全執行部分：報表讀取稽核只記固定合成 dataset、可解析操作者、UTC 時間、授權結果及有效日期範圍，不記報表內容、IP、session、Email 或交易識別；worker 以 UTC 截止清理報表與 `m0.SecurityAudit` 一年以前資料、`m0.AuthFailures` 九十日以前資料。IdentityAccess 以 `m0.Users.LastActivityAt` 提供 UTC 最近活動時間，並實作已驗證 Email 私有 mock 通知及 30 日寬限後停用登入；外部郵件未接入，未知活動時間不處理。配送售後 worker 將已配送／退回 Shipment 在終態事件滿 180 日後去識別，並將無退款／庫存證據關聯的明確拒絕 Return 在拒絕事件滿一年後去識別；保留最小訂單歷史摘要，無可信結案節點、未終結或有相關售後／財務證據的資料不處理。上述清理冪等且不觸及 Order、Collection、付款／退款、發票或交易稽核資料。API 僅有報表稽核 INSERT 權限；期限清理由 worker 執行。外部承運紀錄、地址／電話來源、其他客服來源、已檢查退貨的明確結案來源、一般診斷日誌及備份生命週期仍待來源／機制成立；accounting/tax 保留分類維持待決。

本機只處理合成dataset：reset前有preview範圍/計數/版本、Owner＋demo.reset permission與精確dataset scope、確認目標；無法辨識資料集就拒絕，禁止drop整庫/刪SQL volume。交易fixture可重建；原業務稽核依既定保護與保存處理，reset留外於被重建範圍的最小ResetAudit，不能借reset任意抹除拒絕／授權／派送證據。Audit來源採歷史識別與最小快照，不為reset強制刪成孤兒；具體FK/archive策略須SQL實證。

DataRequest依PendingVerification→Verified/Assigned→Decision/Processing→Closed，讀／更正／停止／刪除範圍與理由分開；guest case challenge不能跨成Order action。保留例外列類別／理由／覆核日期、暫停利用，不把「已刪帳號」當帳務與稽核全部消失。回應不含密碼hash、復原碼、token或無關他人資料。

若另行採用[隔離 restore／cutoff 選配改善](../options/ISOLATED_RESTORE_CUTOFF_OPTION.md)，restore到獨立mock DB後先重套撤權／刪除／hold，核來源與已處理operations再啟用；不能復活已撤授權或重跑交易。此演練不屬本機整體驗收。合成資料reset與真實個資刪除／法定保留不是同一驗收證據。

## 5. 交付檢查

票15/20/21/23與M3-AC06/08/09/10/12：來源唯一、amount/slots平衡、重啟補跑、差異不硬改終態、報表不洩單筆／PII、合成資料清除留證；獨立 restore／cutoff 留在選配改善。Issue 15 核准的本機人工 mock 範圍已完成；本詳規其他票券及 M3 案例仍未全部交付，Issue 15 的範圍與限制見[結案驗證](../../../architecture/VERIFICATION.md#issue-15-storefront-手動處置與證據結案2026-10-17)。只有[合成fixture](../fixtures/M3_REFUND_CASES.json)結構與算例可於本輪核對。
