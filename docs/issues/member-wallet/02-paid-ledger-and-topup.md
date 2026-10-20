# 02: 完成實付帳本、合成入金與本人餘額

**What to build:** 會員看見自己的本機合成餘額與逐筆帳本；只在 dedicated fixture 核對合成入帳，不收實際款項。

**Blocked by:** member-wallet/01 的完整正式錢包仍是 `needs-info`；**已解鎖範圍僅限**隔離 `demo_m0` 合成帳戶／測試入帳、會員本人餘額與帳本讀取。這不授權新增 Owner／公開入金 API，也不解鎖外部入金、Settlement、贈送金或退款執行。

**Status:** ready-for-agent

**Work status:** claimed (僅執行已解鎖的 synthetic-only 子範圍)

**Planning status:** PLANNED_NOT_IMPLEMENTED

## 完成與驗收

- [ ] API 產品路徑不可任意改刪已確認分錄；更正採關聯反向分錄。Dedicated synthetic-credit procedure 以 source ID 防重；完全相同的 source 重送回穩定 replay 拒絕碼，再由 fixture-only SQL 查回唯一既有分錄；相同 source 的會員／金額／幣別不符皆拒絕且不改餘額。此切片不宣稱 DBA／migrator 無法直接改寫資料。
- [ ] 以 dedicated SQL fixture 驗 exact replay、並行同來源確認恰一筆、回應遺失後按 source 查回、會員／金額／幣別不符、未知 source 不可成為可用額度，及由帳本重建餘額 projection。重建只限合成 fixture，依 `SyntheticCredit - CheckoutDebit` 計算；本階段沒有 RefundCredit 或 gift entry。
- [ ] 提供登入會員本人餘額／逐筆帳本的真 API 證據；帳本使用 `(CreatedAt, EntryId)` 穩定降冪游標，拒絕另一會員資料。HTTP read 不要求 ETag；寫入冪等性由內部合成入帳 source ID 及既有 checkout command 保證。若瀏覽器環境仍受 TLS／Chromium 阻擋，明確保留該證據為未完成，不以 mock 畫面代替。
- [ ] 所有合成來源與 UI 明示 mock；不得增加正式收款、前端／Owner 入金入口、直接餘額修改、Settlement、贈送金或 Refund 執行。

## 來源與範圍

- [執行主計畫](../../planning/kohami/delivery/IMPLEMENTATION_PLAN.md)是順序與待決事項唯一來源。
- [會員儲值詳規](../../planning/kohami/commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)定義業務語意及驗收 ID；本輪只補票券，不宣稱契約採用、實作或測試完成。
- [驗收定義](../../planning/kohami/delivery/VALIDATION_PLAN.md)、[共用作業規則](../../agents/issue-tracker.md)。

## Comments

- 2026-10-15：依使用者確認的「核心商店→實付會員儲值→換裝另版→正式營運」補齊缺少的交付票；贈送金暫不發放。本票未開始實作。
