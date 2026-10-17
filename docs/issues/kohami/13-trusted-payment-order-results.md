# 13: 完成付款並顯示可信結果

**What to build:** 訪客完成受控後端 mock 付款，訂單依可信結果查詢更新；未知結果顯示待確認，付款後改價不覆寫 Price Snapshot。正式服務商沙盒不屬目前 mock 階段。

**Blocked by:** 04：驗證金流契約與選型；12：訪客結帳與可靠庫存承諾

**Status:** ready-for-agent

**Work status:** claimed

- [ ] 由訪客待付款訂單發起 Payment Attempt 至沙盒付款、可信確認及訂單結果的完整旅程可驗證。
- [ ] 不以前台返回或畫面成功當成收款證據；未能確認的結果顯示待確認，可信失敗才顯示相應失敗。
- [ ] 通知驗證、訂單／金額匹配及查詢依確認契約處理，偽造或不匹配通知不能改變有效交易。
- [ ] 付款後由後台改價，顧客仍看見原訂單價格、優惠及費用，Price Snapshot 不被現行商品覆寫。
- [ ] 覆蓋成功、拒絕、付款服務失敗與未知結果；本票使用沙盒，不執行正式收款。

## 現行 mock 階段範圍（2026-10-17）

付款只由受控後端mock結果確認；顧客顯示模擬付款，不收真卡資料，保留金額／幣別／原操作核對。

詳細以 [全流程mock政策](../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為準；正式 provider sandbox／收款不在目前範圍。此票仍須補齊本機 mock 的付款旅程、可信結果、失敗／未知結果與 Price Snapshot 實測，不能以歷史證據或畫面 mock 將票結案。

## M2本機mock規劃補充（2026-10-13）

現行補充見[M2結帳／訂單詳規](../../planning/kohami/commerce/CHECKOUT_ORDER_PLAN.md)與[訪客存取契約](../../planning/kohami/identity/GUEST_ORDER_ACCESS.md)：保留15分鐘、原操作重送、Unknown不重付、取消／到期晚到成功補償與自己的訂單範圍。14狀態／5存取與靜態線框只為規劃，真正SQL／授權／API／瀏覽器證據仍NOT_EXECUTED。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：R04–R06／AC04–AC06；User Stories 22–25。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。
- 2026-10-17：Issue 12 已完成，依使用者指示開始 Issue 13；沿用受控後端 mock 範圍，不啟用真實金流。

## 2026-10-17 實作進度

- 結帳結果頁新增三語「重新查詢狀態」按鈕。頁面只接受既有 checkout status API 回應；查詢失敗時保留上次伺服器結果並提示，不自行推斷付款結果。
- 新增 Playwright 瀏覽器回歸案例：從首頁查詢後進入結果頁，再重新查詢，使狀態由 AwaitingPayment／Created 更新為 Confirmed／Succeeded；目錄現價為 TWD 1,200 時，畫面仍顯示訂單快照商品 TWD 1,000、優惠 TWD 100、運費 TWD 60、總額 TWD 960。
- `npm run typecheck`、`npm run build`、Issue 13 Playwright 1/1 通過。Playwright 使用 API route mocks，證明 Storefront 顯示契約，不代表本次有新的 API／SQL 付款驗收。
- Commerce SQL harness 本次付款期限／逾期成功補償／取消競爭案例通過；其後在獨立 RefundAcceptance 的 worker 斷言失敗（exit 134），整體 SQL harness 不通過。該退款案例有 `finally` 清理。本次沒有重設 fixture。
- 新增 Playwright 案例涵蓋 Failed／Unknown 結果與 status refresh 503 時保留最後伺服器結果及價格快照。本回合一般 sandbox 執行在 Chrome 子程序啟動階段以 `SIGABRT`／`EPERM` 中止；經核准重試後 Chrome 啟動成功，但因 `https://localhost:3000/` 沒有服務而 `ERR_CONNECTION_REFUSED`，尚未進入案例，故本次未驗證通過。
- RefundAcceptance 失敗檢查到 worker 只有在 SQL job 可被指定且當前 `security_dispatch_allowed` 授權時才回傳 1；目前 dedicated fixture native status 因 `DirectoryNotFoundException` 無法確認 migration ledger／執行版本。本回合未套用 migration、未重設 fixture，也未確認根因。
- Issue 04 mock event／Issue 06 API-worker 付款旅程及 Issue 01 Unknown reload 的既有來源綁定證據仍見[驗證紀錄](../../architecture/VERIFICATION.md)；不以歷史證據替代本票剩餘驗收。

## 2026-10-17 後續驗證

- 新增失敗／Unknown 與 refresh 503 保留最後伺服器結果的瀏覽器案例；先前因未啟動 Storefront 而未執行。補上 Nuxt hydration 等待後，在 HTTPS 本機 Storefront 執行兩個 Issue 13 Playwright 案例，成功結果與失敗／Unknown／503 案例各 PASS 1/1。測試使用 API route mocks，僅驗證消費端顯示契約。
- 發現 runtime `Session` schema 將任何非空 `state` 都套用登入者欄位，拒絕合法 Anonymous API 回應並造成 HTTP 500。依 canonical session branches 修正 runtime projection，並以 API contract 回歸測試確認 Anonymous 與 AdminAuthenticated 回應有效、Anonymous 權限不為空時無效。`Core.Tests` 11/11、Release solution build 0 warnings／0 errors。
- 重新生成 runtime OpenAPI 時，同步依批准 operation 清單動態計數（目前 26），不再寫死 34；這是投影產生器與輸出一致性修正，不改動核准清單。
- 在隔離暫存工作區新建 SQL 2025 dedicated fixture（14337），執行 Commerce SQL harness 全部通過，包含付款到期補償與 RefundAcceptance worker dispatch。先前 14336 fixture 的 RefundAcceptance 失敗來自其舊 schema／stored procedure 狀態；沒有對 14336 套 migration 或 reset。測試後已依精確 Compose marker 移除新建 14337 fixture 與 volumes。
- 真 API／SQL aftercare runner 驗證 strict TLS、API 安全 guard、checkout owner scope、付款 worker 成功及 Nuxt SSR 的 actor scope；後段在建立售後合成訪客能力時因 `SYNTHETIC_ORDER_NOT_FOUND` 中止，runner 清理本次 synthetic rows／私密 session artifact。這不是完整 aftercare／Issue 13 PASS。
- `npm run typecheck` PASS。尚未以 SQL 先改商品目錄現價，再查詢既有 Checkout／Order 金額快照；也尚未讓一個完整 API／SQL／browser 旅程覆蓋成功、拒絕、服務失敗與 Unknown。

## 2026-10-17 OwnerId 與 SQL 價格快照後續驗收

- 修正 Issue06 aftercare acceptance 的 Owner 對應：訪客 Cart／Checkout／Order 由 `kohami.command` actor 擁有，並非 Admin `UserId` 或 BrowserId。Fixture 現在明確簽發合成 command actor cookie；Order capability 仍綁定獨立 BrowserId，cleanup 僅移除記錄的 synthetic actor／browser／user 與其訂單關聯資料。
- Checkout／Order 建立後，在 SQL transaction 內將該 SKU 的 Catalog Inventory 現價改動 TWD 1，讀回確認新現價可見，並逐一比較持久化 Checkout／Order snapshot；兩快照保持不變後 rollback 價格變更。
- 以新建的 SQL Server 2025 dedicated fixture（14338）執行 migration 0001–0008 與 Commerce SQL harness：PASS。`python3 scripts/m0/run-issue06-aftercare-acceptance.py`：PASS；覆蓋 strict TLS、Origin／antiforgery guard、checkout owner scope、付款 worker 成功、SQL snapshot 價格不變、guest capability、配送、退貨、驗收與 mock refund；Playwright PASS 1/1，SQL persisted-state assertion PASS，synthetic rows／私密 session artifact 清理 PASS。驗收後 fixture 依精確 Compose marker 清理。
- 本次旅程實際覆蓋成功付款結果；拒絕、付款服務失敗與 Unknown 的真 API／SQL／browser 結果旅程仍未完成。先前的 Playwright 結果案例仍是 mocked API consumer evidence，不能替代該缺口。Issue 13 維持 `claimed`，Issue 14 仍受阻。

## Resolution

本次完成結果頁伺服器狀態重新查詢、快照顯示保護與 mocked browser 回歸案例；匿名 Session runtime contract 亦已修正。隔離新 fixture 的 Commerce SQL harness 通過；aftercare OwnerId 對應已修，SQL 價格變更不改寫 Checkout／Order 快照，且成功付款後的真 API／SQL／Nuxt SSR／browser 售後旅程通過。Issue 13 整體仍為 `claimed`，不可解除 Issue 14 依賴。待完成：拒絕、付款服務失敗與 Unknown 的真 API／SQL／browser 結果旅程。
