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
- Issue 04 mock event／Issue 06 API-worker 付款旅程及 Issue 01 Unknown reload 的既有來源綁定證據仍見[驗證紀錄](../../architecture/VERIFICATION.md)；不以歷史證據替代本票剩餘驗收。

## Resolution

本次完成結果頁伺服器狀態重新查詢與快照顯示保護，Issue 13 整體仍為 `claimed`，不可解除 Issue 14 依賴。待完成：在受控 mock SQL fixture 上完成端到端成功／拒絕／服務失敗／Unknown 瀏覽器旅程，實際改動目錄價格後驗證已付款 Order／Checkout 的商品、優惠、運費快照仍不變，並修復或隔離 Commerce harness 的 RefundAcceptance 失敗後重跑相關完整 SQL 驗收。
