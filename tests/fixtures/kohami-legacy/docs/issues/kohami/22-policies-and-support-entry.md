# 22: 購物政策與客服入口

**What to build:** 三語顯示經審查的購物、配送、退貨與資料政策；客服入口及案件回應責任清楚可追蹤。

**Blocked by:** 19：退貨申請、客服與驗收

**Formal operations gate:** [07 商業查核](07-brand-merchant-product-verification.md)及 [正式啟用驗收](../production-launch/01-go-live-readiness.md)；本票只結案本機展示範圍。

**Status:** ready-for-agent
**Work status:** resolved

- [x] 發布有版本的展示購物、配送、退貨、退款與資料政策，三語可取得且明示 mock；正式審查與真實商家揭露由 07 及正式啟用驗收把關。
- [x] 客服入口連至既有案件流程，顧客理解案件分類、回應目標、營業日及例外，不新增未確認服務保證。
- [x] 政策顯示與實際結帳／售後行為一致，未通過商家或權利審查的內容不宣稱已符合正式營運條件。
- [x] 驗證手機與鍵盤可取得政策及提出／追蹤自己的案件，語言、載入失敗及權限拒絕有明確結果。

## 現行 mock 階段範圍（2026-10-13）

購物／客服入口明示展示模式；通知使用隔離測試收件匣／站內記錄，不寄真實Email；既有正式條款屬啟用前待審。

詳細以 [全流程mock政策](../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為準；本節優先於以下完整產品／歷史來源中的真實服務或雙人退款要求。本票依 2026-10-15 使用者決策拆分驗收：本機阻擋為 19；正式查核改由 07 與正式啟用票負責。所有新增產品案例仍 NOT_EXECUTED。

## M3詳規追蹤（2026-10-13）

參照[M3售後退款流程與驗收](../../planning/kohami/commerce/AFTERCARE_REFUND_PLAN.md)及[對帳／報表／資料生命週期](../../planning/kohami/commerce/RECONCILIATION_DATA_PLAN.md)。本次只補齊規劃、來源額度與靜態示意；本票產品驗收NOT_EXECUTED，工作完成狀態維持；依賴按本次明確決策拆分。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：User Stories 27–28、74–75；N05–N06；V02。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Resolution

（2026-10-17；僅 Issue 22 本機 mock 展示範圍）

- 新增 `/zh-TW/policies`、`/ja/policies`、`/en/policies` 展示政策與三語客服入口；政策標示 DEMO v0.2，涵蓋購物／付款、配送、退貨／退款及資料使用，未宣稱正式商家條款、法定期限、資料保存期限或服務保證。
- 首頁可進入政策與客服頁。客服只連到既有本人訂單售後／退貨流程；一般詢問與資料權利請求目前不能提交。案件時間只記 UTC 節點與經過秒數，營業日曆與 SLA 保持未定。
- 售後訂單載入及退貨送出遇到本人權限拒絕時顯示不洩漏訂單存在與否的訊息；服務故障提供稍後重試提示，失敗送出保留表單內容。
- `npx playwright test tests/issue22-policies-support.spec.ts`（Storefront，Chrome）：3/3 PASS；涵蓋三語政策／客服、手機寬度、鍵盤焦點、本人訂單售後入口、權限拒絕、服務不可用、退貨送出與追蹤。API 使用攔截的合成回應，未連線真 API／SQL。
- `npm run typecheck`（Storefront）：PASS；`python3 -m unittest discover -s tests/architecture -v`：3/3 PASS；`git diff --check`：PASS；AIPS `impact-validate`：`IMPLEMENTATION_APPROVED` 且無錯誤。Playwright 在一般 sandbox 下遇到 Chrome SIGABRT，於本機測試環境重跑 3/3 PASS。Project Intelligence refresh 因工作樹含先前保留的 `.ai` 變更回報 `working_tree_dirty`，未覆寫其內容。
- 未新增後端／OpenAPI／SQL；未驗一般客服或資料權利案件 API、真人客服、正式商家條款、法定退貨或正式 SLA。Issue 23 為下一項。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。


- 政策更新紀錄（2026-10-13）：使用者確認金流、退款、物流、發票先mock，運費／人員先規劃、管理者退款；更新目前範圍但不改產品完成狀態。

- 2026-10-15：使用者採用「Issue 22 驗收展示政策與客服；Issue 07 及新增上線驗收票負責正式查核」。移除本機對 07 的硬阻擋，未宣稱正式條款已通過。
