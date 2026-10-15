# 05: 驗收會員實付餘額閉環與復原

**What to build:** 完整實付餘額本機版可重建、演示與復原；原核心功能持續可用。

**Blocked by:** member-wallet/04；kohami/25；kohami/26

**Status:** ready-for-agent

**Planning status:** PLANNED_NOT_IMPLEMENTED

## 完成與驗收

- [ ] 完成 WALLET-AC01–10 與 FUL-AC01–03 中本版適用條件；贈送金相關條件標 DEFERRED（未啟用），不可把 DEFERRED 算整體規格全數 PASS。
- [ ] 在獨立隔離目標 restore，以帳本重建餘額並對入金、扣抵、退款及庫存；重套 cutoff 後撤權／刪除／hold，不重複資金或通知效果。
- [ ] 以同一候選驗雙花、超賣、超退、跨會員、重放、crash/restart、兩 worker 與未知結果；合成 SQL／API／browser 與人工證據分列。
- [ ] 另一操作者依手冊重建並完成正常／拒絕／未知旅程；沒有真實收款，贈送金功能及正式啟用仍關閉。

## 來源與範圍

- [執行主計畫](../../planning/kohami/delivery/IMPLEMENTATION_PLAN.md)是順序與待決事項唯一來源。
- [會員儲值詳規](../../planning/kohami/commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)定義業務語意及驗收 ID；本輪只補票券，不宣稱契約採用、實作或測試完成。
- [驗收定義](../../planning/kohami/delivery/VALIDATION_PLAN.md)、[共用作業規則](../../agents/issue-tracker.md)。

## Comments

- 2026-10-15：依使用者確認的「核心商店→實付會員儲值→換裝另版→正式營運」補齊缺少的交付票；贈送金暫不發放。本票未開始實作。
