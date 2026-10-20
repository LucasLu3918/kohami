# 26: 核心商店獨立交付與啟用審查

**What to build:** 整合本機商品、購物、合成會員儲值扣抵、履約、權限與安全證據及正式啟用延期清單，形成可審閱的本機核心商店候選成果，不依賴換裝完成。

**Blocked by:** kohami/24：本機桌面核心旅程與三語 LCP／CLS lab 子範圍已完成；Issue25 僅要求與本機結帳安全及資料一致性相關的證據，不要求正式監控、備份／還原、RPO／RTO 或 field SLA 完成。member-wallet/01–03 的本機合成餘額子範圍是本票驗收輸入；完整實付錢包與退款仍不在此依賴內。

**Status:** claimed

- [ ] 對指定候選版本彙整核心購物、付款競爭、退款、履約／發票、權限、合規與復原的實際證據及未通過事項。
- [ ] 本機會員以內部 API 扣抵專用合成帳戶／測試入帳餘額，原子更新帳本、訂單與庫存；不足、重送、並行及回滾結果可查。不得稱為實際付款／實付儲值，也不連第三方付款 API。
- [ ] 停用或尚未提供換裝時仍可完成正常商品瀏覽、訪客／會員購物與售後，換裝素材不阻擋核心交付。
- [ ] 核對最終輸入、輸出、資料、事件及消費者影響，尤其語言切換、改價快照與付款／庫存／退款的一致性。
- [ ] mock 交付範圍、運費／人員權限機制、測試限制及正式服務資格／費率／人力延期清單可審閱，未決項不默認解決。
- [ ] 形成可供使用者核准的交付與啟用資料；本票完成不自動部署、正式收款或批准 Phase 2 啟用。

## 現行 mock 階段範圍（2026-10-13）

本票交付為mock核心商店的可審閱候選，無實際收款／寄送／開票。正式商家資格、真實費率、人員與供應商證據留延期清單，不阻擋mock交付；正式啟用仍另確認。

本機結帳新增的儲值扣抵只使用隔離合成帳戶或明確標示的測試入帳。無使用者自行修改餘額的路徑，餘額只能由受控合成入帳／扣抵 API 與帳本交易改變。此範圍不宣稱完整錢包、退款或真實資金流程通過。

詳細以 [全流程mock政策](../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為準；本節優先於以下完整產品／歷史來源中的真實服務或雙人退款要求。既有阻擋編號保留，按本階段契約是否完成判斷。所有新增產品案例仍 NOT_EXECUTED。

## M4規劃追蹤（2026-10-13）

參照[M4驗收／故障復原](../../planning/kohami/delivery/VALIDATION_PLAN.md)與[本機操作](../../planning/kohami/delivery/LOCAL_DEMO_RUNBOOK.md)。本輪補齊規劃與證據模板，產品NOT_EXECUTED；原驗收清單、依賴及工作狀態維持。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：Phase 1；V01–V06、V11–V12；User Stories 85–87。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。
- 2026-10-20：依使用者決定改成本機產品交付；Issue24 已完成的桌面必要範圍作為輸入，Issue25 只納入必要安全／一致性證據。會員儲值只用合成帳戶／測試入帳，第三方付款與正式資金仍排除。
- 2026-10-20：新增 `m0commerce.wallet_synthetic_credit`，僅供 dedicated fixture migrator 使用；限制 `demo_m0` marker、`@example.test` 活躍會員、合成來源、單筆上限 NT$50,000、交易與來源唯一性，API 沒有入金路由或執行權。新增 Native 命令固定讀 `.local/kohami-sql/m0` 並只接受 `localhost,14349/KohamiM0/demo_m0`，驗 marker 後才遷移。0013 已套用；缺會員拒絕 SQL 案例 PASS。會員登入、成功入帳、餘額不足回滾、併發雙花、checkout／order／庫存交易及瀏覽器付款仍未驗證。
- 2026-10-20：本機 API 5084 使用本輪建置版本，`/health` 與 `/ready` HTTP 200；原服務 5083 對先前 contract 回 `/ready` 503。兩者均回報 `activation=requires-native-reconciliation`，資料庫 `Maintenance=True`。macOS Secure Enclave issuer 建立失敗（`OSSTATUS_-34018`，缺少支援 entitlement）；未建立 Owner／permit、未關 maintenance。Issue26 保持 claimed，不能宣稱完整本機購物付款通過。
- 2026-10-20：以專案 `scripts/m0/run-local.py` 將當前程式啟動於 `https://localhost:3004`／`http://127.0.0.1:5085`；API `/ready` HTTP 200，zh-TW／ja／en 首頁 SSR 均 HTTP 200 且包含商品內容。受保護寫入仍因 native reconciliation／maintenance gate 阻擋；這不是完整 checkout 驗收。


- 政策更新紀錄（2026-10-13）：使用者確認金流、退款、物流、發票先mock，運費／人員先規劃、管理者退款；更新目前範圍但不改產品完成狀態。
