# 02: 完成實付帳本、合成入金與本人餘額

**What to build:** 會員看見自己的實付餘額與逐筆帳本，Owner 可核對合成入金；沒有實際收款。

**Blocked by:** member-wallet/01

**Status:** ready-for-agent

**Planning status:** PLANNED_NOT_IMPLEMENTED

## 完成與驗收

- [ ] 確認分錄不可任意改刪、更正採關聯反向分錄；同來源重送只入帳一次，相同識別不同會員／金額／幣別隔離。
- [ ] 後端驗會員所有權、管理 permission／scope／近期 TOTP；會員 A 無法讀取 B 的餘額或帳本，前端傳入 confirmed 不會入帳。
- [ ] 以 dedicated SQL 驗重複通知、並行確認、回應遺失、金額不符／未知與餘額投影重建；未確認入金不成為可用額度。
- [ ] 提供本人餘額／分頁帳本與合成入金後的真 API／SQL 瀏覽器證據，來源及 UI 明示 mock。

## 來源與範圍

- [執行主計畫](../../planning/kohami/delivery/IMPLEMENTATION_PLAN.md)是順序與待決事項唯一來源。
- [會員儲值詳規](../../planning/kohami/commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)定義業務語意及驗收 ID；本輪只補票券，不宣稱契約採用、實作或測試完成。
- [驗收定義](../../planning/kohami/delivery/VALIDATION_PLAN.md)、[共用作業規則](../../agents/issue-tracker.md)。

## Comments

- 2026-10-15：依使用者確認的「核心商店→實付會員儲值→換裝另版→正式營運」補齊缺少的交付票；贈送金暫不發放。本票未開始實作。
