# 17: 三語購物旅程與可索引網址

**What to build:** 繁中、日文、英文涵蓋商品、購物車及付款狀態；語言切換保留有效 SKU／購物車，交易維持 TWD。

**Blocked by:** 10：從首頁搜尋與篩選商品；14：處理付款逾時、取消與晚到成功；16：會員購物與受保護的訂單紀錄

**Status:** ready-for-agent

**Work status:** resolved（本機 mock 範圍）

- [x] 三語各有語系前綴網址；公開首頁／商品在設定 HTTPS `NUXT_PUBLIC_SITE_URL` 後輸出 canonical、hreflang 與索引指示。既有授權商品維護／發布流程沿用 Issue 08。
- [x] 切換語言保留商品 SKU、顏色／服裝尺寸、搜尋條件與 SKU 選擇；購物車內容跨語系仍以同一 session 讀取，金額維持 TWD。
- [x] 價格／庫存繼續由同一 Catalog／Commerce API 提供；會員歷史訂單保留同一訂單與金額，狀態文案依語系呈現。
- [x] 驗證無結果、成功、拒絕、待確認付款切語；商品缺譯保留目前語系並明示繁中 fallback。會員、購物車與結帳頁維持 noindex。

## M2本機mock規劃補充（2026-10-13）

現行補充見[M2結帳／訂單詳規](../../planning/kohami/commerce/CHECKOUT_ORDER_PLAN.md)與[訪客存取契約](../../planning/kohami/identity/GUEST_ORDER_ACCESS.md)：保留15分鐘、原操作重送、Unknown不重付、取消／到期晚到成功補償與自己的訂單範圍。14狀態／5存取與靜態線框只為規劃，真正SQL／授權／API／瀏覽器證據仍NOT_EXECUTED。

本票原依賴／checkbox／Work status不改；本輪不實作或將本票標完成。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：R01／AC01；User Stories 1–5、65。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。

## Resolution

**Work status:** resolved（2026-10-17，本機 mock 範圍）。Storefront 以 `/zh-TW`、`/ja`、`/en` 前綴提供 SSR routes；舊 `?lang=` 與無前綴網址轉址到語系路徑。首頁搜尋條件、商品 SKU 選擇、購物車及結帳狀態切語均保留 query／交易識別。公開首頁與有效商品頁在設定 HTTPS `NUXT_PUBLIC_SITE_URL` 後輸出絕對 canonical、三語 hreflang／x-default，並允許索引；本機預設、無效商品、會員、購物車、結帳結果維持 noindex。金額、庫存、訂單與付款狀態仍來自既有服務，幣別仍為 TWD。

實作與日期驗收見[Issue 17 驗證紀錄](../../architecture/VERIFICATION.md#issue-17-三語購物旅程與語系網址2026-10-17)。此為本機 mock Storefront 驗收，不代表已設定公開網域、部署網站或對搜尋引擎發布。

