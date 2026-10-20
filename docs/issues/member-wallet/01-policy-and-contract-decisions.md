# 01: 確認實付儲值規則、可信來源與契約

**What to build:** Owner 能審閱本機合成入金、餘額購物與退款的完整流程；正式入金仍關閉。

**Blocked by:** kohami/16（會員結帳基礎）。本機合成餘額子範圍已由使用者明確決定採用合成帳戶／測試入帳，可供 kohami/26 使用；不解除完整錢包、實付入金、退款或會計分類的待決事項。

**Status:** needs-info

**Planning status:** PLANNED_NOT_IMPLEMENTED

## 完成與驗收

- 本機交付決策（2026-10-20）：起始餘額只可由隔離合成帳戶／測試入帳建立；不採用真實入金、前端任意餘額修改或外部付款服務。此決策只涵蓋本機測試子範圍，不代表下列完整正式契約已採納。

- [ ] 確認本機只使用合成入金；實付與贈送分帳方向保留，但本版不發放、不扣抵贈送金，前端不顯示可用贈送額度。
- [ ] 列出可信入金識別、確認主體、冪等、款項不符／Unknown、扣抵與退款來源及合法轉移，禁止用前端成功或可編輯 balance 欄位入帳。
- [ ] 將未決的正式入金、資金覆核與退款目的地以至少兩方案及推薦記錄在主計畫；受影響部分未決不實作，不推定任何法律分類。
- [ ] 審閱 Wallet/Ledger/Topup/Settlement/Refund 的 API、授權、ETag／冪等、SQL 原子邊界與遷移／回復計畫，確認本階段範圍後才解鎖 02。

## 來源與範圍

- [執行主計畫](../../planning/kohami/delivery/IMPLEMENTATION_PLAN.md)是順序與待決事項唯一來源。
- [會員儲值詳規](../../planning/kohami/commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)定義業務語意及驗收 ID；本輪只補票券，不宣稱契約採用、實作或測試完成。
- [驗收定義](../../planning/kohami/delivery/VALIDATION_PLAN.md)、[共用作業規則](../../agents/issue-tracker.md)。

## Comments

- 2026-10-15：依使用者確認的「核心商店→實付會員儲值→換裝另版→正式營運」補齊缺少的交付票；贈送金暫不發放。本票未開始實作。
