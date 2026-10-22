# 26: 核心商店獨立交付與啟用審查

**What to build:** 整合本機商品、購物、合成會員儲值扣抵、履約、權限與安全證據及正式啟用延期清單，形成可審閱的本機核心商店候選成果，不依賴換裝完成。

**Blocked by:** kohami/24：本機桌面核心旅程與三語 LCP／CLS lab 子範圍已完成；Issue25 僅要求與本機結帳安全及資料一致性相關的證據，不要求正式監控、備份／還原、RPO／RTO 或 field SLA 完成。member-wallet/01–03 的本機合成餘額子範圍是本票驗收輸入；完整實付錢包與退款仍不在此依賴內。

**Status:** claimed

- [x] 對指定候選版本彙整核心購物、付款競爭、退款、履約／發票、權限、合規與復原的實際證據及未通過事項。
- [x] 本機會員以內部 API 扣抵專用合成帳戶／測試入帳餘額，原子更新帳本、訂單與庫存；不足、重送、並行及回滾結果可查。不得稱為實際付款／實付儲值，也不連第三方付款 API。
- [ ] 停用或尚未提供換裝時仍可完成正常商品瀏覽、訪客／會員購物與售後，換裝素材不阻擋核心交付。
- [ ] 核對最終輸入、輸出、資料、事件及消費者影響，尤其語言切換、改價快照與付款／庫存／退款的一致性。
- [x] mock 交付範圍、運費／人員權限機制、測試限制及正式服務資格／費率／人力延期清單可審閱，未決項不默認解決。
- [x] 形成可供使用者核准的交付與啟用資料；本票完成不自動部署、正式收款或批准 Phase 2 啟用。

## 現行 mock 階段範圍（2026-10-13）

本票交付為mock核心商店的可審閱候選，無實際收款／寄送／開票。正式商家資格、真實費率、人員與供應商證據留延期清單，不阻擋mock交付；正式啟用仍另確認。

依使用者目前決策，訪客可瀏覽及使用尚未登入的購物車；**建立新訂單必須登入會員**，訂單 Email 直接使用會員帳號 Email。因此原驗收項「訪客／會員購物」中的訪客結帳不適用，改按訪客瀏覽／購物車、登入會員結帳驗收。

本機結帳新增的儲值扣抵只使用隔離合成帳戶或明確標示的測試入帳。無使用者自行修改餘額的路徑，餘額只能由受控合成入帳／扣抵 API 與帳本交易改變。此範圍不宣稱完整錢包、退款或真實資金流程通過。

詳細以 [全流程mock政策](../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為準；本節優先於以下完整產品／歷史來源中的真實服務或雙人退款要求。既有阻擋編號保留，按本階段契約是否完成判斷。新增驗收依據與未驗項目記錄於下方 Comments；文件中的範圍不代表 Issue26 整體已完成。

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
- 2026-10-20：新增 `commerce.wallet_synthetic_credit`，僅供 dedicated fixture migrator 使用；限制 `kohami_local` marker、`@example.test` 活躍會員、合成來源、單筆上限 NT$50,000、交易與來源唯一性，API 沒有入金路由或執行權。新增 Native 命令固定讀 `.local/kohami-sql/identity` 並只接受 `localhost,14349/Kohami/kohami_local`，驗 marker 後才遷移。0013 已套用；缺會員拒絕 SQL 案例 PASS。會員登入、成功入帳、餘額不足回滾、併發雙花、checkout／order／庫存交易及瀏覽器付款仍未驗證。
- 2026-10-20：本機 API 5084 使用本輪建置版本，`/health` 與 `/ready` HTTP 200；原服務 5083 對先前 contract 回 `/ready` 503。兩者均回報 `activation=requires-native-reconciliation`，資料庫 `Maintenance=True`。macOS Secure Enclave issuer 建立失敗（`OSSTATUS_-34018`，缺少支援 entitlement）；未建立 Owner／permit、未關 maintenance。Issue26 保持 claimed，不能宣稱完整本機購物付款通過。
- 2026-10-20：以專案 `scripts/foundation/run-local.py` 將當前程式啟動於 `https://localhost:3004`／`http://127.0.0.1:5085`；API `/ready` HTTP 200，zh-TW／ja／en 首頁 SSR 均 HTTP 200 且包含商品內容。受保護寫入仍因 native reconciliation／maintenance gate 阻擋；這不是完整 checkout 驗收。
- 2026-10-20 本機開發模式決策：本機顧客流程改用 `local-development-bootstrap`，不需 Apple Developer／Secure Enclave／Owner authority。CLI、API health／run-local 提示及會員購物車身分已實作；Native Operations 與 API Release build 通過（0 warnings／errors）。尚未執行 bootstrap 或 live SQL／Storefront checkout 驗收，故本票維持 `claimed`，會員登入、錢包成功扣抵、庫存／訂單原子性仍未驗證。管理員商品變更與 Owner-only 售後權限不由此 synthetic member 取得。
- 2026-10-20 本機合成結帳 follow-up：`local-development-bootstrap` 已在 marker 驗證的 `localhost:14349/Kohami/kohami_local` 成功建立 `@example.test` 合成會員與 NT$50,000 測試入帳，將該 dedicated fixture 的 maintenance gate 解除；憑證只存於 ignored `.local/` mode 0600。現有 API `/health`、`/ready` 均 200，三語頁與商品 API 均可讀取（45 項商品）。合成會員登入、錢包餘額、購物車、報價及訂單建立成功；內部 wallet payment API 回 `503 SERVICE_UNAVAILABLE`，訂單仍為 `AwaitingPayment`，餘額維持 NT$50,000，確認失敗交易未扣款。唯讀 SQL principal 檢查確認 `kohami_api` 對 `Collections` 與 `RefundUnits` 的有效 `INSERT` 權限為 0；wallet debit procedure 本身成功於交易內扣 NT$1 後 rollback，確認失敗在後續帳務列寫入權限。此為經 Storefront proxy 的 API 驗收，不是 Playwright／實際瀏覽器付款驗收；無第三方付款連線。修正與 dedicated SQL 重驗仍待完成；Issue26 維持 `claimed`，成功扣抵後狀態、餘額不足回滾、重送／並行與庫存／帳本／訂單原子性尚未驗證。
- 2026-10-20 本機合成錢包寫入修正進度：新增並套用 migration `0014_wallet_payment_api_writes`，只授予 `kohami_api` 對 `Collections`、`RefundUnits` 的 INSERT；不提供直接餘額／帳本寫入或退款權限。套用後一次合成付款已於 SQL 原子提交 NT$1,940 扣款、`CheckoutDebit`、訂單 `Confirmed`、付款 `Succeeded`，錢包餘額 NT$48,060；但 API 在提交後的 runtime response contract 驗證因只宣告 HTTP 202、未宣告 wallet path 的 HTTP 200，回傳 `500 INTERNAL_ERROR`。該 checkout 不重送。已補 canonical/runtime HTTP 200 wallet response schema 與契約回歸；乾淨新訂單、充分／不足額回滾及瀏覽器流程仍待驗。此 HTTP/API 驗收不代表 Playwright／瀏覽器付款或第三方付款驗收。
- 2026-10-20 本機開發模式合成付款驗收：另修正付款結果 `version` 欄位契約（`tx.Save` 會在持久化時補入版本）及移除沒有 body version 時產生 ETag 的錯誤要求。Commerce wallet safety contract、API Release build 均 PASS（0 warnings／0 errors）；以新 checkout 完成商品、購物車、報價、訂單、wallet payment API 流程，付款 HTTP 200、`Succeeded`、訂單 `Confirmed`，NT$1,940 扣抵，餘額 NT$44,180→NT$42,240。API `/health`、`/ready` 及 zh-TW／ja／en 頁面均 HTTP 200，商品 API 45 項。先前因回應契約缺陷回 500 的三筆 checkout 經唯讀查證皆已原子提交、各扣 NT$1,940；沒有重送。驗收使用本機 API，並非 Playwright／瀏覽器購物操作；餘額不足回滾、冪等重送、並行雙花仍未驗，無第三方付款服務。Issue26 保持 `claimed`。


- 政策更新紀錄（2026-10-13）：使用者確認金流、退款、物流、發票先mock，運費／人員先規劃、管理者退款；更新目前範圍但不改產品完成狀態。

- 2026-10-20 本機合成錢包 SQL 驗收：新增 `tests/Commerce/WalletSqlAcceptance.cs` 與 `--wallet-sql-acceptance <kohami-project-root>` 命令。Release build（Commerce.Tests）PASS，0 warnings／0 errors；`--wallet-safety-contract` PASS；指定 `localhost:14349/Kohami/kohami_local` marker／fingerprint 檢查通過後執行不足餘額、同鍵重放與雙訂單並行扣款案例，全部 PASS。不足額結果為 Commerce handler `409 WALLET_INSUFFICIENT_BALANCE`，錢包餘額／扣款帳本、checkout/order、collection、held reservation 無部分提交；同鍵成功重放回原回應，只新增一筆扣款／collection；兩筆總額相同的訂單以一筆合成額度競爭，恰一成功、一筆不足額，餘額不為負且失敗訂單維持 `AwaitingPayment`／reservation `Held`。runner finally 清除三個唯一合成會員、四筆 checkout/order、wallet/account/ledger、冪等／collection／reservation，並回補本輪預留庫存；重驗無殘留，maintenance gate 前後相同。沒有呼叫第三方付款服務。這是 Commerce service handler＋live SQL acceptance，並非 HTTP host／Storefront／Playwright 瀏覽器驗收；當前外部 `/health` 與 `/ready` 探測各回 403，不能作為本輪服務 smoke 通過。Issue26 整體仍為 `claimed`，完整瀏覽器購物、退款／履約／發票與其餘交付項目尚未因此完成。

- 2026-10-20 本機服務重查：舊 API 5083 的 `/health` 為 200、`/ready` 為 503、商品 API 為 200；舊 API body 帶有目前程式已移除的 `activation` 欄位。前次 403 本輪未重現，5083／3003 既有程序未停止。依 runbook 在隔離 5088／3007 啟動目前 checkout，`/health`、`/ready`、商品 API 與 zh-TW／ja／en 首頁 SSR 均 HTTP 200，頁面含商品資料。此為 API／SSR smoke，不是本輪瀏覽器結帳驗收；Issue26 仍 `claimed`。
- 2026-10-20 瀏覽器結帳續驗：本機 `kohami-sql` 合成會員憑證檔存在且 Unix mode 為 `0600`；未讀取或輸出憑證內容。Playwright 1.58.2 的系統 Chrome 啟動探測重現 `SIGABRT` 與清理子程序 `EPERM`；隨附 Chromium 未安裝，暫存安裝命令 `PLAYWRIGHT_BROWSERS_PATH=/private/tmp/kohami-playwright-browsers ./node_modules/.bin/playwright install chromium` 因下載網域不在執行環境 allowlist 回 HTTP 403。未執行瀏覽器購物／付款，未修改 SQL、餘額、訂單、庫存或 gate。既有 `WalletSqlAcceptance` 清理器只負責其每次新建的測試會員；目前登入用的固定本機合成會員不在該清理器範圍，故在瀏覽器測試可用的一次性會員與精確資料清理流程完成前，不以該帳戶執行付款。Issue26 保持 `claimed`；既有 API／SQL 成功及不足額／冪等／併發證據不等同瀏覽器驗收。
- 2026-10-20 本機瀏覽器 smoke 續驗：Playwright Chrome launch 再次以 `SIGABRT` 結束並在清理回 `EPERM`。依 `LOCAL_DEMO_RUNBOOK.md` 啟動隔離預覽 `KOHAMI_API_PORT=5091 KOHAMI_SITE_PORT=3011 python3 scripts/foundation/run-local.py`；API `/health`、`/ready`、`/api/v1/products` 與 Storefront `/zh-TW`、`/ja`、`/en` 均 HTTP 200。桌面 Chrome 對 `https://localhost:3011/zh-TW` 回報 `ERR_CERT_AUTHORITY_INVALID`，未安裝／信任憑證或忽略 TLS 錯誤。未登入、建立購物車、結帳或呼叫 SQL；停止本輪 API／Nuxt 後啟動器清除暫存 Origin 設定。Issue26 維持 `claimed`；瀏覽器商品互動與錢包付款仍未驗證。後續需處理本機預覽憑證信任流程，並具備每次執行專用合成會員與精確清理後再做 Playwright／瀏覽器付款驗收。

- 2026-10-22 本機會員結帳與 Issue25 安全證據更新：依使用者決定，新訂單只允許登入會員，訂單 Email 直接取會員帳號 Email。`a7ff19e` 完成會員卡片 checkout 與 acceptance 修正；`c8e1c14` 修正會員錢包 checkout 並新增僅供 dedicated 測試 fixture 的 API 權限遷移 0029。Issue11／16 綜合 acceptance 的 Playwright 6/6 通過，涵蓋匿名 checkout 拒絕、錯誤會員 Email 拒絕、會員卡片 checkout、訂單本人／Owner 隔離及以登入前 browser-owned cart 完成錢包 checkout；專用 fixture 清理及完整 `SecurityState` 還原通過。Native 測試 64/64 通過，Commerce `--wallet-safety-contract` 通過；0029 readiness 已在 `kohami-sql-test` 驗證。未連第三方付款服務。
- Issue25 本機必要安全輸入：匿名 HTTP／CSRF 拒絕 11/11、SQL 身分／授權測試 9/9、有限合成非 Owner HTTP 矩陣 14/14 通過。0030 只授予 API role 執行單一商品目錄授權判斷 procedure；合成矩陣涵蓋未知訂單／收據隱藏、CSRF／Origin 拒絕及一位 Merchandiser 的三個越權操作。Maintenance 與完整 `SecurityState` 精確還原；完整 route × actor × scope HTTP 矩陣仍未完成，詳見 [Issue25](25-commerce-security-monitoring-recovery.md) 與下方驗證紀錄。
- 新訂單會員限定行為及會員卡片／錢包 checkout 的本機驗收已取得證據；當時完整售後、退貨／退款、履約／發票及最終影響彙整仍未完成，因此 Issue26 維持 `claimed`。

### 2026-10-22 候選證據與延期項目索引

程式候選基準：產品程式 `e4216a3`（含會員錢包 checkout `c8e1c14`、卡片 checkout `a7ff19e` 及 0030 catalog authorization grant）；Issue25 有限 HTTP 測試與驗收 helper 為 `3d9f276`。本節把目前證據與未決工作綁定至這組 main 程式版本；以下結果限於本機 mock／合成資料。

| 範圍 | 證據與結果 | 狀態／限制 |
| --- | --- | --- |
| 瀏覽、登入與購物 | 三語商品 SSR、P3/P4 storefront、會員登入後 browser-owned cart checkout；本輪會員／訪客拒絕與購物驗收 Playwright 6/6 | 核心會員購物路徑通過；訪客不得建立新訂單 |
| 卡片與錢包付款 | 合成卡片 checkout、會員錢包 checkout、餘額不足回滾、冪等重送及併發扣款；Commerce wallet safety contract 與 Native 64 項通過 | 合成／mock 支付；未連第三方或真實資金 |
| 訂單、庫存與資料一致性 | checkout／order／wallet ledger／collection／reservation SQL readback；不足額、重放及併發 SQL acceptance | 已驗會員錢包原子結帳子項；不含完整售後及退款閉環 |
| 權限與安全 | Issue25 匿名 HTTP／CSRF 11/11、SQL 身分／授權 9/9、有限合成非 Owner HTTP 矩陣 14/14；0030 已套用至專用測試 fixture，Maintenance 及完整 `SecurityState` 還原通過 | 14/14 僅含匿名拒絕與單一 Merchandiser 越權案例；完整登入 actor × route × scope HTTP 矩陣仍未完成 |
| 售後、退款、履約與發票 | 專案另有 mock handler／SQL 驗收紀錄，參見 [VERIFICATION](../../architecture/VERIFICATION.md) 與 Issue20–21 | 尚未在同一候選版完成會員售後 journey、跨會員拒絕、完整退款／履約／發票對帳與恢復後一致性驗收 |
| 影響追溯 | cart／quote／checkout／wallet／庫存各有契約與局部 SQL／瀏覽器證據 | 尚無同版完整 input/output/data/event/consumer 對照；語系切換保留狀態、價格快照／缺貨與退款 worker／事件影響仍待核對 |
| 正式資格與復原 | DEMO-01–12 mock 政策、Issue25 監控／復原限制及延期決定 | 正式商家／付款／物流／發票資格、費率、人力、備份目標、RPO／RTO 與月可用性不在本機證據內；不宣稱通過或啟用 |

Issue26 仍是 `claimed`。候選證據彙整、mock／正式延期範圍及交付審閱資料已可審閱；會員合成錢包原子結帳子項仍是唯一已完成的產品旅程驗收。會員售後 journey 與同版端到端輸入／輸出／資料／事件／消費者追溯仍待驗收。本索引不構成部署、公開啟用或正式收款核准。

### 2026-10-22 交付審閱資料（候選 `e4216a3`）

本節僅整理已提交至 `main` 且可由專案驗證紀錄追溯的結果。產品程式候選基準為 `e4216a3`，包含會員卡片結帳 `a7ff19e`、錢包結帳 `c8e1c14` 及 API 商品目錄授權遷移 `061e5ef`；有限 HTTP 測試與驗收 helper 提交於 `3d9f276`。本節是供審閱的證據與延期摘要，不改變售後旅程、完整影響追溯與全面授權驗收仍未完成的狀態。

#### 已提交候選證據

| 範圍 | 已有證據 | 證據界線 |
| --- | --- | --- |
| 商品瀏覽與登入結帳 | 三語首頁 SSR／商品資料 smoke；會員卡片與錢包結帳 Playwright 6/6，包含匿名 checkout 拒絕、錯誤會員 Email 拒絕、登入會員下單及本人訂單隔離。 | 證明所記錄的本機 mock 路徑；不代表正式付款或所有語系切換後的購物車狀態均驗證。 |
| 錢包交易一致性 | 專用 SQL 驗收涵蓋餘額不足回滾、冪等重放、並行扣抵、訂單／錢包帳本／collection／庫存 reservation readback 與合成資料清理；候選包含 0029 權限遷移。 | 只涵蓋明確標示的合成餘額與 mock 支付；不含真實入金、實付退款或完整錢包履約閉環。 |
| 訂單本人隔離 | Issue16 合成會員 A/B 測試驗證訂單清單只含本人訂單，A 讀取 B 的訂單回 404；同候選 checkout 瀏覽器驗收也含本人／Owner 訂單隔離。 | 沒有驗證會員跨帳號讀取收據、退貨、取消或退款操作。 |
| 本機安全輸入 | Issue25 匿名 HTTP／CSRF 11/11、SQL 身分／授權 9/9、有限非 Owner HTTP 矩陣 14/14；0030 僅授予 API 執行商品目錄讀取授權程序，專用 fixture readiness 與 Maintenance／完整 `SecurityState` 還原有紀錄。 | 14/14 只有匿名拒絕及單一 Merchandiser 越權操作；完整登入 actor × route × scope HTTP 矩陣仍待驗。 |
| 售後、退款、履約與憑證 | Issue19–21 舊候選涵蓋退貨／驗收、退款 worker、顧客退款摘要與 DEMO 收據調整；2026-10-22 新增 dedicated fixture 真實會員 HTTP aftercare journey，A/B 跨會員拒絕 4/4、客服 scoped shipment/return、檢驗與 RefundCase SQL readback 通過。 | 新 journey 以合成已付款訂單作售後前置資料，不重驗 checkout；客服流程到 RefundCase `Requested`，未執行退款／worker 終態，因 `refund.execute` 為 Owner-only 且本次不建立 Owner。尚未驗完整 Storefront 售後 UI 與全部 actor × route × scope 矩陣。 |

已提交的逐次命令、驗收結果及限制見[驗證紀錄](../../architecture/VERIFICATION.md)；各項售後歷史驗收見[Issue19](19-return-support-inspection.md)、[Issue20](20-controlled-refunds-reconciliation.md)及[Issue21](21-invoice-and-refund-adjustments.md)。Issue25 未結案內容與目前待補 HTTP 矩陣見[Issue25](25-commerce-security-monitoring-recovery.md)。

#### 有限資料與消費者影響追溯

| 邊界 | 來源輸入 → 可觀察結果 | 資料／工作消費者 | 證據狀態 |
| --- | --- | --- | --- |
| 會員結帳 | 已驗證會員身分與帳號 Email、購物車 SKU／數量、伺服器計算的 Quote／運送選項 → Checkout／Order；Order owner 與聯絡 Email 綁定登入會員，卡片等待付款或合成錢包即時扣抵。 | Commerce SQL 寫入 Checkout、Order、Reservation；錢包路徑另寫扣款帳本、Payment、Collection 與 RefundUnits。Storefront 購物車／結帳頁與本人訂單頁讀取這些 API 結果。 | checkout／錢包原子性及訂單本人讀取已有候選證據。前端不得裁決會員身分、金額、庫存或付款結果；無同版完整 consumer graph。 |
| 價格、運費及庫存 | Cart SKU／數量與伺服器商品價格、促銷及支援地區／配送方式 → Quote snapshot 固定商品、折扣、運費及 fee version；建立訂單時再次檢查並保留庫存。 | Commerce order／checkout 與 Inventory／Reservation；商品頁、購物車及費用摘要顯示伺服器回傳結果。 | 有局部費率版本、價格快照、庫存不足／扣款回滾紀錄；跨語系保留 cart、改價後完整瀏覽到拒絕／重新報價仍缺本候選同版端到端核對。 |
| 出貨及退貨 | 已付款 Order → 真實會員 session 讀本人訂單／收據及提出 Return；Order-scoped CustomerSupport 建 Shipment、推進送達與 Return Inspection。 | Shipment／Return、Order 版本、RefundUnits、庫存 movement 與 RefundCase；本人 Order projection 讀取 aftercare 摘要。 | dedicated fixture HTTP／SQL journey 已驗會員本人和 A/B 跨會員拒絕；前置付款訂單為合成 seed，未重驗 checkout，亦未驗完整 Storefront 售後 UI。 |
| 退款及 DEMO 憑證 | 符合條件的 RefundCase、退款授權與原始 Collection／商品單位額度 → RefundOperation；worker 依可信 mock 結果結算或保留 Unknown。 | Refund Jobs／worker 更新 operation、Collection、退款單位與 Order summary；成功退款後 DEMO receipt adjustment worker 更新顧客憑證投影。 | Issue20–21 已有舊候選 SQL／worker／API 證據；不代表 `e4216a3` 的同版售後閉環、跨會員授權矩陣或正式財務／稅務結果通過。Wallet collection 的退款仍明確不支援。 |
| 錢包事件與背景消費者 | 合成錢包 checkout 同一 SQL transaction 扣抵並建立已確認訂單。 | 已提交紀錄明確指出目前未採用 wallet-confirmed Outbox consumer，故不建立對應 Jobs／事件；退款與 DEMO 憑證是各自既有 worker 工作。 | checkout 原子交易有 SQL 證據；完整 event／consumer coverage 未知，不推定存在未驗證消費者。 |

以上為依已提交 Commerce handler、API route、Storefront contract／頁面及驗證紀錄所做的有限追溯，相關入口包括 [CommerceService.cs](../../../src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs)、[Aftercare.cs](../../../src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs)及 [API Program.cs](../../../apps/api/Program.cs)。這不是完整 Impact Graph；目前全域 API／data／events coverage 不完整，consumer coverage 為 unknown。Issue25 HTTP 矩陣完成後仍須核對本票直接消費者及其候選版本，才能判斷此核取項是否通過。

#### 延期與未驗範圍

- **仍待候選版驗收**：Storefront 售後完整互動與呈現；會員查看退款執行／worker 終態及 DEMO 收據調整。已完成的 API journey 只到 Return Inspection 與 RefundCase `Requested`；另須擴充完整 actor × route × scope × session 矩陣。拒絕案例已比對主要商務 SQL snapshot，尚未覆蓋全部稽核及 consumer 狀態。
- **安全與復原**：Issue25 完整登入角色／資源 scope HTTP 矩陣仍待完成。交易監測／告警、正式備份／還原、付款／退款／訂單復原對帳、RPO ≤ 1 小時、RTO ≤ 8 小時、月可用性與復原後發票／庫存一致性均未驗；依本機 mock 階段決議，不以本票推定其已完成。
- **正式服務資格與成本**：正式商家／收款資格、支付／物流／發票供應商資格與費率、正式憑證及稅務效力、配送／客服／退款人力、正式備份目標與保留責任均未驗或待營業主體／專業審查。不得由 mock 報價、合成卡號或 DEMO 收據推定正式服務已可用。
- **不納入此候選核准**：真實收款／入金、真實寄送、正式開票、正式部署／公開啟用，以及 Phase 2 換裝。換裝素材與其權利、裝置效能及成本仍依 Issue27 後續票處理。

#### 審閱與啟用狀態

目前資料可供審閱本機 mock 候選、已通過案例、驗收界線與延期事項。Issue26 的完整售後 UI／退款執行、完整影響追溯、安全矩陣及其他未勾選核取項尚未完成，**本票仍為 `claimed`，不得標為 `resolved`**。本文件不授權部署、正式收款、公開啟用或 Phase 2 啟用；需待缺口完成並另行取得相應啟用決定。

### 2026-10-22 Issue25 有限 HTTP 矩陣補充（程式候選 `3d9f276`）

Issue25 矩陣 runner 的 Maintenance 時序修正與 evidence 已提交至 `main`（`3d9f276`）。在 marker-verified dedicated `kohami-sql-test` 上，匿名未知訂單／收據路由、CSRF／Origin 拒絕，以及單一 synthetic Merchandiser 的三個越權操作共 14/14 通過；測試合成資料清除與完整 `SecurityState` readback 還原通過。細節與實際命令見 [驗證紀錄](../../architecture/VERIFICATION.md) 及 [Issue25](25-commerce-security-monitoring-recovery.md)。

這只完成有限的匿名與單一 Merchandiser 拒絕案例，不能代表全部 API route × actor × scope × session 矩陣。Issue26 仍需售後會員 journey、同版輸入／輸出／資料／事件／消費者追溯及其餘核取項證據；本票維持 `claimed`，不授權部署或正式啟用。

### 2026-10-22 Issue26 會員售後 HTTP 補充

在 marker-verified dedicated `kohami-sql-test` 執行真實 API HTTP／SQL 驗收。兩名合成會員各自登入；A 讀取本人訂單與收據並提出退貨，B 讀 A 的訂單／收據、提出退貨及取消均回 404，拒絕前後訂單、退款單位、文件數、庫存與 collection snapshot 相同。合成 CustomerSupport 只持有該 Order scope 的 `shipment.manage`、`support.respond`、`return.inspect`、`orders.read`、`refund.request`、`refund.check`，SQL readback 確認沒有 Owner 或 `refund.execute` grant；客服完成 Pending→Shipped→InTransit→Delivered，接著退貨 Requested→Accepted→Received→Inspected。Member order projection、Return、RefundUnits、RefundCase、InventoryMovement 與獨立合成 SKU 庫存 readback 一致。

本輪精確清除合成會員、session、SecurityAudit、grants、訂單、收據、Collection、RefundUnits、出貨、退貨、RefundCase、InventoryMovement、冪等紀錄與唯一合成 SKU；SQL readback 確認無殘留。Dedicated fixture 完整 `SecurityState`／Maintenance 以 Issue11 helper 還原並 readback。退款執行及 worker 終態仍延期，因 `refund.execute` 為 Owner-only；本輪未建立 Owner。這是已付款 order 前置資料的售後驗收，不包含新 checkout、Storefront UI 或全量權限矩陣，故 Issue26 維持 `claimed`。

### 2026-10-22 Storefront 會員訂單售後唯讀明細

會員中心訂單卡片新增可展開的詳細資料，展開時延遲呼叫本人限定的 `GET /api/v1/orders/{id}` 與 `GET /api/v1/orders/{id}/receipts`，呈現付款確認、配送時間軸、退貨／客服時間軸、退款摘要與收據／調整憑證；商品圖片、商品資訊、規格與數量仍顯示於訂單卡片。登出或卸載列表時清除明細快取。本段紀錄對應版本的 UI 僅讀取，當時沒有加入取消／退貨寫入操作；後續取消 UI 見下方補充。退貨申請 UI 與其路由契約差異仍待釐清。

`cd apps/storefront && npm run typecheck` PASS；`cd apps/storefront && npx playwright test tests/issue26-member-order-details.spec.ts --config=playwright.config.ts` PASS，1/1。瀏覽器測試以 API mock 驗證延遲讀取與 UI 呈現，不連 SQL fixture，亦非 live member session 驗收。完整輸入／輸出／資料／事件／消費者 graph 尚未完成；本票維持 `claimed`，既有未勾選驗收項不因此視為通過。

### 2026-10-22 Storefront 會員待付款訂單取消 UI

會員可在展開本人訂單明細後取消待付款訂單；按鈕只在後端 order detail 的 `actions` 含 `Cancel` 且 `allowed=true` 時顯示。送出時使用 detail `version`、強 `If-Match`、CSRF token 與冪等鍵。收到 202 後更新訂單摘要與明細；409／412 會刷新最新狀態且不自動重送。刷新期間維持訂單卡片與展開狀態，pending/error 以可存取狀態訊息呈現，並提供繁中、日文及英文文字。此範圍只新增取消 UI，不含退貨申請 UI，也不改動核取項或 Issue26 的 `claimed` 狀態。

`npm --prefix apps/storefront run typecheck`、`npm --prefix apps/storefront run build`、`git diff --check` 與 AIPS `impact-validate` 均 PASS。隔離 Nuxt production preview 上執行 `issue26-member-cancellation.spec.ts` 與既有 `issue26-member-order-details.spec.ts`，合計 Playwright 6/6 PASS，使用合成 API mock 驗證 eligibility、CSRF／Origin／`If-Match`／冪等鍵與 request body、202 刷新、pending、409／412 無盲目重送及一般錯誤呈現。未呼叫 live Commerce API，未執行 SQL、取消交易或庫存釋放／回補驗收；因此這些測試不構成取消與庫存 SQL 一致性的證據，也不代表 Issue26 結案。
