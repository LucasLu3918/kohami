# 04: 完成實付退款、人工履約與帳本對帳

**What to build:** 營運記錄配送與退貨驗收，授權者執行合成退款；顧客看見退款與餘額，對帳差異可追蹤。

**Blocked by:** member-wallet/03；kohami/20；kohami/21；kohami/23

**Status:** ready-for-agent

**Planning status:** PLANNED_NOT_IMPLEMENTED

## 完成與驗收

- [ ] 依 01 採用的退款目的地及原扣抵來源執行；本機退款不對外移動真資金，不任意將贈送金或未確認款當實付。
- [ ] 以 SQL 原子保留原 collection／settlement 的金額、商品單位及 shipping 額度；Unknown 不釋放或重發，並行／重放不超退。
- [ ] 人工配送操作需 permission／scope／Order 版本與稽核；取消／出貨競爭唯一，Inspection、入庫、退款與憑證狀態獨立。
- [ ] 取得入金→扣抵→退款→餘額／訂單／庫存的完整對帳、差異案件與真 API／SQL browser 證據。

## 來源與範圍

- [執行主計畫](../../planning/kohami/delivery/IMPLEMENTATION_PLAN.md)是順序與待決事項唯一來源。
- [會員儲值詳規](../../planning/kohami/commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)定義業務語意及驗收 ID；本輪只補票券，不宣稱契約採用、實作或測試完成。
- [驗收定義](../../planning/kohami/delivery/VALIDATION_PLAN.md)、[共用作業規則](../../agents/issue-tracker.md)。

## Comments

- 2026-10-15：依使用者確認的「核心商店→實付會員儲值→換裝另版→正式營運」補齊缺少的交付票；贈送金暫不發放。本票未開始實作。
