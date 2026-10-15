# 06: 另案確認並啟用贈送購物金

**What to build:** 選擇啟用後，才可發放、使用及依規則恢復贈送金；不是目前實付版的完成前置。

**Blocked by:** member-wallet/05；明確啟用決策

**Status:** needs-info

**Planning status:** DEFERRED_OPTION

## 完成與驗收

- [ ] 在主計畫提供至少兩方案與推薦，明確決定扣抵順序、適用品項、期限／通知、取消／退貨恢復與活動來源。
- [ ] 未採用前不發放、不參與實付結帳、不把贈送金當實付退款；不以會員或營運自行改欄位繞過開關。
- [ ] 啟用須另展開契約、帳本、SQL 競爭與退款分攤、告知及真 API／SQL browser 測試，保留既有實付版回歸。
- [ ] 若暫不啟用則維持 DEFERRED，不阻擋實付版或只含實付範圍的正式審查。

## 來源與範圍

- [執行主計畫](../../planning/kohami/delivery/IMPLEMENTATION_PLAN.md)是順序與待決事項唯一來源。
- [會員儲值詳規](../../planning/kohami/commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)定義業務語意及驗收 ID；本輪只補票券，不宣稱契約採用、實作或測試完成。
- [驗收定義](../../planning/kohami/delivery/VALIDATION_PLAN.md)、[共用作業規則](../../agents/issue-tracker.md)。

## Comments

- 2026-10-15：依使用者確認的「核心商店→實付會員儲值→換裝另版→正式營運」補齊缺少的交付票；贈送金暫不發放。本票未開始實作。
