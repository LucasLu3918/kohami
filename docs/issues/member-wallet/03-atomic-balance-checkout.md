# 03: 完成會員餘額原子結帳與訂單

**What to build:** 會員以實付餘額完成一筆購物，重新整理可查回同一訂單；餘額不足不建單。

**Blocked by:** member-wallet/02；kohami/16

**Status:** claimed

**Planning status:** IMPLEMENTED_WITH_OPEN_ACCEPTANCE

## 完成與驗收

- [x] 同一 SQL transaction 驗會員／Quote／費用／全部 SKU／餘額，原子提交扣抵、帳本、訂單快照、庫存與冪等結果；任一失敗全回滾。專用 SQL acceptance 已驗證餘額不足及扣抵後庫存衝突皆無部分寫入。
- [ ] Outbox：目前沒有 wallet-confirmed event consumer；此實作不建立無消費者的 `Jobs` 工作。若要把詳規的 Outbox 列為本票必要交付，須先擴大已批准 Change Impact 的事件／worker 範圍。
- [x] 同 key 重送回原單與原扣抵；相同 key 不同內容拒絕；不同請求並行不產生負餘額、超賣或重複有效扣抵。專用 SQL acceptance 使用兩個 OS process 競爭最後一筆餘額與庫存單位。
- [x] 履約開始前以權威已確認扣抵裁決，不沿用 mock 外部 PaymentAttempt 的 Unknown 當錢包成功；訪客不可提交會員餘額結帳。canonical 專用 SQL fixture 的真 HTTP host acceptance 已驗證 Unknown 外部付款拒絕出貨且不建立 Shipment、匿名結帳回 401 且無訂單／付款／庫存副作用，以及已確認錢包訂單可授權出貨並讀回 Collection／WalletPayment 狀態。
- [x] 真 API／SQL HTTP-host 整合：已通過專用 fixture 上的真會員登入／session cookie／CSRF、購物車、報價、餘額不足回滾、成功扣抵、訂單狀態與 SQL readback、登出；Storefront API-route mock Chromium 3/3 仍是獨立 UI 證據，不等同真 API browser journey。

## 來源與範圍

- [執行主計畫](../../planning/kohami/delivery/IMPLEMENTATION_PLAN.md)是順序與待決事項唯一來源。
- [會員儲值詳規](../../planning/kohami/commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)定義業務語意及驗收 ID；本輪只補票券，不宣稱契約採用、實作或測試完成。
- [驗收定義](../../planning/kohami/delivery/VALIDATION_PLAN.md)、[共用作業規則](../../agents/issue-tracker.md)。

## Comments

- 2026-10-15：依使用者確認的「核心商店→實付會員儲值→換裝另版→正式營運」補齊缺少的交付票；贈送金暫不發放。本票未開始實作。
- 2026-10-20：依使用者核准的單一原子結帳 API 範圍完成程式、契約、專用 SQL acceptance 與 Storefront UI；SQL／handler 與 UI route-mock 案例通過。Outbox 必要性及真 HTTP host／browser 整合仍未驗收，故本票維持 `claimed`，不宣稱完整會員錢包閉環完成。
- 2026-10-21：使用者選擇將 Outbox 延後至存在 wallet-confirmed consumer 時再評估；以獨立 `.local/kohami-demo/wallet03-http`／14350 fixture 通過完整 Commerce SQL harness 與 `python3 scripts/foundation/run-wallet03-http-acceptance.py --local-dir "$PWD/.local/kohami-demo/wallet03-http" --port 5086`。真 HTTP host 驗收覆蓋 CSRF、會員登入／登出 cookie、餘額不足無部分寫入、補足後結帳，以及 Checkout／Order／WalletPayment／ledger／collection／庫存 SQL readback；synthetic rows 已清理，fixture 保留供 demo。此紀錄不宣稱 Storefront 真 API Playwright、Outbox consumer 或完整售後閉環通過。
- 2026-10-22：新增專用 HTTP acceptance 案例，涵蓋匿名 `POST /checkouts` 拒絕、外部 `PaymentAttempt Unknown` 訂單不得建立 Shipment，以及訂單範圍 CustomerSupport 可為成功錢包訂單建立 Shipment，並讀回付款／Collection 狀態。Commerce.Tests Release build 通過；live runner 未執行成功：既有 `.local/kohami-demo/wallet03-http` 設定目前指向 `demo_m0`／`KohamiM0`，與 Wallet03 harness 要求的 `kohami_local`／`Kohami` 不符，故在 SQL 連線前以 `ENVIRONMENT_SCOPE_INVALID` 停止。未修改 fixture；新案例仍待符合 scope 的 dedicated fixture 執行及精確 cleanup 證據，不視為驗收通過。
- 2026-10-22：依專案核准改用 canonical `.local/kohami-sql-test/identity`，未使用或改寫 demo fixture。runner 嚴格檢查 dedicated marker、`localhost:14333/Kohami/kohami_local`、TLS 與私有檔案權限；透過 Issue11 helper 暫開 Maintenance 並保存／還原完整 `SecurityState`。真 HTTP host acceptance 通過匿名結帳拒絕、錢包扣款／訂單成功、Unknown 外部付款拒絕出貨且無 Shipment、成功錢包訂單授權出貨與 SQL readback。所有合成會員／客服、grant、session、audit／auth failure、文件、collection、reservation、refund unit、wallet、冪等紀錄均精確清理；SKU 庫存及完整 `SecurityState` readback 還原通過。Outbox consumer 仍依 10/21 決議延後，不納入本次通過範圍。
