# 01: 確認實付儲值規則、可信來源與契約

**What to build:** Owner 能審閱本機合成入金、餘額購物與退款的完整流程；正式入金仍關閉。

**Blocked by:** kohami/16（會員結帳基礎）。本機合成餘額子範圍已由使用者明確決定採用合成帳戶／測試入帳，可供 kohami/26 使用；不解除完整錢包、實付入金、退款或會計分類的待決事項。

**Status:** needs-info

**Work status:** claimed (本機 synthetic-only 子範圍已核定；正式政策仍待決)

**Planning status:** PLANNED_NOT_IMPLEMENTED

## 完成與驗收

- 本機交付決策（2026-10-20）：起始餘額只可由隔離合成帳戶／測試入帳建立；不採用真實入金、前端任意餘額修改或外部付款服務。此決策只涵蓋本機測試子範圍，不代表下列完整正式契約已採納。

- [x] 確認本機只使用隔離合成帳戶／測試入帳；實付與贈送分帳方向保留，但本版不發放、不扣抵贈送金，前端不顯示可用贈送額度。
- [x] 核定本機可信來源為 dedicated `demo_m0` fixture 的受限 synthetic-credit procedure；使用合成會員、唯一 source ID 與 SQL 交易，不以登入前端或可編輯 balance 欄位入帳。正式入金通知與正式確認主體仍未決。
- [x] 將正式入金、資金覆核與退款目的地的選項及建議保留在主計畫 WAL-D01／02／03；受影響部分未決不實作，不推定法律或會計分類。
- [x] 審閱本機 02 子範圍：餘額讀取沿用登入會員本人授權；帳本採穩定 `(CreatedAt, EntryId)` 降冪游標；同 source 重送由 SQL 拒絕並只在 dedicated fixture 以 source 查回既有分錄；只在 dedicated fixture 驗證 projection 重建。此切片不新增 Owner／公開入金 API、Settlement、Refund 或餘額直接寫入。
- [ ] 正式 Wallet／Settlement／Refund API、資金覆核、法定退款來源、正式 SQL 回復與 Owner 管理路由尚未審閱／核准；正式決策前維持關閉。

本票因此**只完成本機合成子範圍的決策與技術邊界**；完整票維持 `needs-info`，正式資金功能未解鎖。

## 來源與範圍

- [執行主計畫](../../planning/kohami/delivery/IMPLEMENTATION_PLAN.md)是順序與待決事項唯一來源。
- [會員儲值詳規](../../planning/kohami/commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)定義業務語意及驗收 ID；本輪只補票券，不宣稱契約採用、實作或測試完成。
- [驗收定義](../../planning/kohami/delivery/VALIDATION_PLAN.md)、[共用作業規則](../../agents/issue-tracker.md)。

## Comments

- 2026-10-15：依使用者確認的「核心商店→實付會員儲值→換裝另版→正式營運」補齊缺少的交付票；贈送金暫不發放。本票未開始實作。
- 2026-10-20：完成本機 synthetic-only 子範圍審閱並解鎖 member-wallet/02 的限定切片。正式入金、Settlement、退款、贈送金及會計分類仍待決；不把本機合成入帳稱為實付或正式錢包完成。
