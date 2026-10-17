# 模組責任與交易邊界

| 模組 | 所有資料／規則 | 公開互動 | 實作狀態 |
| --- | --- | --- | --- |
| Catalog | Product／SKU 描述、三語內容、價格與發布 | 提供 SKU 資訊／價格版本，發布事件 | 本機 mock API、SQL 與 storefront 已實作；正式商品資料驗收仍未完成 |
| Commerce | Cart、Quote、Checkout、Order、库存保留／異動、PaymentAttempt、Collection、RefundCase／Operation | core34 商務操作；商品／物流／身份透過契約銜接 | M0 adapter 已搬移；價格分攤 Domain 抽離 |
| Fulfillment | Shipment、Return、Inspection、受控入庫請求 | 狀態／驗收結果，向 Commerce 提交唯一庫存異動意圖 | 責任骨架，完整 M3 尚待實作 |
| CustomerCare | SupportCase、DataRequest、客服處理與本人驗證流程 | 售後案件與資料權利意圖；退款執行由 Commerce 裁決 | 責任骨架 |
| Fitting | Preset、BodyProfile、AssetRevision、Rights、Compatibility、Session、SavedOutfit、Export | SKU 識別及可換裝資訊；不裁決可購買性 | Phase2 提案骨架，未啟用 |

## 不變條件與協調

- 訂單成立：Commerce 在一個 SQL transaction 建立快照／訂單、承諾所有 SKU、記冪等結果与 outbox。
- 付款結果：驗證來源與去重；Order／Reservation 同交易裁決；未知不能當失敗，晚到成功建立獨立補償。
- 退款：原 collection、商品單位、运费及未決額度原子保留；Unknown 不釋放。
- 履約：出貨与取消竞争 Order 版本；檢查結果不直接等於退款，退款成功不直接入庫。
- Fitting：素材權利、版本与撤銷獨立于商品販售；失效不清 cart、不改 SKU 尺碼或原訂單快照。

Audit／Outbox／Idempotency 是技術機制，業務事件與去重范围仍由所属模組定義；不因共用表而建立可任意存取所有資料的巨型 shared module。

## 後續拆分條件

只有語意、生命週期、資料所有權、公開契約和一致性方案足夠清楚時，才将 Inventory、Payment 或 Refund 升為獨立模組。先保留既有 SQL 原子協調，另寫 ADR 與 consumer／rollback 計畫；不由空資料夾推導微服務需求。
