# 03: 完成會員餘額原子結帳與訂單

**What to build:** 會員以實付餘額完成一筆購物，重新整理可查回同一訂單；餘額不足不建單。

**Blocked by:** member-wallet/02；kohami/16

**Status:** ready-for-agent

**Planning status:** PLANNED_NOT_IMPLEMENTED

## 完成與驗收

- [ ] 同一 SQL transaction 驗會員／Quote／費用／全部 SKU／餘額，原子提交扣抵、帳本、訂單快照、庫存與 outbox；任一失敗全回滾。
- [ ] 同 key 重送回原單與原扣抵；相同 key 不同內容拒絕；不同請求並行不產生負餘額、超賣或重複有效扣抵。
- [ ] 履約開始前以權威已確認扣抵裁決，不沿用 mock 外部 PaymentAttempt 的 Unknown 當錢包成功；訪客不可提交會員餘額結帳。
- [ ] 先以失敗測試保護多 process 競爭、最後一件／最後一筆餘額、回應遺失及 transaction failure，再驗真 API／SQL browser。

## 來源與範圍

- [執行主計畫](../../planning/kohami/delivery/IMPLEMENTATION_PLAN.md)是順序與待決事項唯一來源。
- [會員儲值詳規](../../planning/kohami/commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)定義業務語意及驗收 ID；本輪只補票券，不宣稱契約採用、實作或測試完成。
- [驗收定義](../../planning/kohami/delivery/VALIDATION_PLAN.md)、[共用作業規則](../../agents/issue-tracker.md)。

## Comments

- 2026-10-15：依使用者確認的「核心商店→實付會員儲值→換裝另版→正式營運」補齊缺少的交付票；贈送金暫不發放。本票未開始實作。
