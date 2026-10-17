# 文件盤點、整理與維護報告

更新：2026-10-15。本輪「唯一執行主計畫與去重」結果見本文末段；前段清冊及驗證是第一輪整理紀錄。範圍為整個 `docs/`；逐檔盤點 Markdown、OpenAPI、JSON Schema、YAML、fixture、線框來源／預覽及 Word 的用途、權威、狀態、引用與工具 consumer。

## 分析結論與處置

1. 原規劃入口是多輪更新追加的歷史串流；與已有產品程式、MFA 採用及候選契約相矛盾。已重寫主入口、規劃索引、交接及交付計畫，將現在狀態與有日期的證據分開。
2. 商品、結帳、售後、身份、換裝及安全詳規具不同責任，依主題歸位；現行架構與未採用的設計候選維持明確區別。保留 `SPEC.md` 的票券規格慣例。
3. M0–M4 是階段代號，不是模組。技術基線文件使用用途檔名，M1／M2／M3／M4 詳規改為商品、結帳、售後及本機驗收名稱。保留驗收 ID、操作 ID、fixture dataset、cookie、SQL schema、namespace 和 scripts/m0，避免名稱清理改變產品相容性。
4. OpenAPI 分來源檔、canonical 候選與 runtime 34 操作子集各有用途；不因內容重疊就刪除生成來源。事件原 schema 與合併 schema 亦有 builder／checker 依賴。保留並分類，來源雜湊只因文件路徑／內容整理重綁，不代表新採用或產品驗收。
5. 政策 01–03 已採用，mock 政策覆蓋其中當前階段條件；保留未被覆蓋的交易／身份／資料規則。38 張票包含未完成工作與採用依據，不以日期舊為由移除。選配是延後，並非已棄用。
6. SVG 是可維護線框來源，PNG 是閱讀預覽；兩者均保留。規劃 fixture 不直接變成正式 seed。私有 `.local/` 與使用中資料未搬移或重設。

## 移除

- `docs/planning/kohami/KOHAMI-規劃書-v0.1.docx`：原索引已明確標示過時，內容由較新 Markdown 取代；移除重複閱讀副本與入口引用。本次未建立新 Word。
- `docs/planning/kohami/CHANGE_IMPACT.yaml`：只描述最初空 berry-match 目錄的單次文件寫入，含已失效的無 Git／無現有程式前提；不含現行產品批准，已由本報告的影響說明取代。
- `docs/` 內 `.DS_Store`：本機目錄 metadata，非文件；已清除，不提交。

未刪除仍有唯一需求、採用決策、交易不變條件或工具依賴的文件。原始內容可由 Git 歷史追溯；本次未另建歸檔副本。完整搬移及移除路徑見 [DOCUMENTATION_MIGRATION.json](DOCUMENTATION_MIGRATION.json)。

## 本次 Change Impact

- 風險類別：`docs_style_test`。範圍為文件分類、現行狀態澄清、交付規劃及 consumer 路徑相容；不改 API 路由、DTO、角色、事件、SQL、交易或 UI 行為。
- 輸入：現行文件入口、ADR、政策、批准範圍、逐項實作狀態、票券、程式結構及文件工具。
- 輸出：主題目錄、用途命名、單一文件入口、清冊、搬移表、網站交付工作包。
- consumer：Markdown／Agent 讀者、文件 checker／builder、core34 projection、API 契約載入、測試內容複製、驗收 runner、AIPS 衍生索引。路徑變更同步更新；工具僅改路徑與來源指紋。
- 資料／事件：只重定位規格和合成案例；保留業務資料及事件結構。沒有 migration／seed／服務啟動、SQL 寫入或外部發布。
- 影響調查：AIPS bounded traversal 為文件風險、圖覆蓋不完整；以全專案引用掃描、搬移表、連結／來源指紋及既有 checker 補足靜態 consumer 核對。動態產品驗收未由此推定。
- TDD 不適用於純文件搬移與規劃；本次採結構、契約、引用、生成一致性與既有架構檢查，不宣稱新增產品行為的 red／green 實作。

## 尚需保留的狀態區別

規劃快照與模板描述設計或當時證據，不是目前 runtime 全貌；已在入口明示。舊詳規中有日期的 `NOT_EXECUTED` 與批准紀錄保留，避免竄改歷史；現在狀態查逐項實作狀態，不再靠不同文件的更新段落自行拼接。

完整網站如何開始、各工作包依賴與驗收見[開發計畫](../planning/kohami/delivery/IMPLEMENTATION_PLAN.md)。會員餘額及正式營運仍有待決規則，本次沒有把規劃採用擴成實作／收款／部署授權。

## 完整文件清冊

下表覆蓋整理後所有原有文件及主要新入口；本報告與搬移 JSON 為維護紀錄，不重複列入自身清冊。

| 路徑／文件 | 處置與用途 |
| --- | --- |
| [KOHAMI 文件入口](../README.md) | 保留 |
| [ADR-0001：首版採非 AI 固定模特三視圖換裝](../adr/0001-preset-avatar-fitting.md) | 保留 |
| [ADR-0002：採 ASP.NET Core／.NET、Vue.js 與 Microsoft SQL Server](../adr/0002-dotnet-vue-sqlserver.md) | 保留 |
| [ADR-0003：模組化單體、Clean Architecture 與 TDD](../adr/0003-modular-monolith-and-tdd.md) | 保留 |
| [領域文件](../agents/domain.md) | 保留 |
| [議題追蹤：本機 Markdown](../agents/issue-tracker.md) | 保留 |
| [Triage 標籤](../agents/triage-labels.md) | 保留 |
| [模組化單體與 Clean Architecture](../architecture/ARCHITECTURE.md) | 保留 |
| [五步驟重整與搬移計畫](../architecture/MIGRATION.md) | 保留 |
| [模組責任與交易邊界](../architecture/MODULES.md) | 保留 |
| [PATH_MIGRATION.json（機器契約／資料）](../architecture/PATH_MIGRATION.json) | 保留：歷史驗證／搬移依據 |
| [本次重整驗證紀錄](../architecture/VERIFICATION.md) | 保留：歷史驗證／搬移依據 |
| [Core.openapi.json（機器契約／資料）](../contracts/runtime/Core.openapi.json) | 保留：已採執行子集 |
| [01: 確認結帳與庫存交易規則](../issues/kohami/01-checkout-inventory-rules.md) | 保留：獨立票券／依賴與紀錄 |
| [02: 確認配送、售後與發票規則](../issues/kohami/02-fulfillment-aftercare-invoice-rules.md) | 保留：獨立票券／依賴與紀錄 |
| [03: 確認身份、權限與資料政策](../issues/kohami/03-identity-permissions-data-policies.md) | 保留：獨立票券／依賴與紀錄 |
| [04: 建立模擬金流契約與情境驗證](../issues/kohami/04-payment-provider-contract-validation.md) | 保留：獨立票券／依賴與紀錄 |
| [05: 建立模擬物流與交易憑證契約](../issues/kohami/05-logistics-invoice-contract-validation.md) | 保留：獨立票券／依賴與紀錄 |
| [06: 確認商店技術方案與品質基準](../issues/kohami/06-commerce-technology-quality-baseline.md) | 保留：獨立票券／依賴與紀錄 |
| [07: 查核品牌、商家與商品來源](../issues/kohami/07-brand-merchant-product-verification.md) | 保留：獨立票券／依賴與紀錄 |
| [08: 從後台上架到顧客查看商品](../issues/kohami/08-publish-product-to-storefront.md) | 保留：獨立票券／依賴與紀錄 |
| [09: 維護可銷售庫存與缺貨狀態](../issues/kohami/09-sellable-stock-and-availability.md) | 保留：獨立票券／依賴與紀錄 |
| [10: 從首頁搜尋與篩選商品](../issues/kohami/10-home-search-filter-journey.md) | 保留：獨立票券／依賴與紀錄 |
| [11: 確認 SKU、購物車與完整費用](../issues/kohami/11-sku-cart-price-review.md) | 保留：獨立票券／依賴與紀錄 |
| [12: 訪客結帳與可靠庫存承諾](../issues/kohami/12-guest-checkout-stock-reservation.md) | 保留：獨立票券／依賴與紀錄 |
| [13: 完成付款並顯示可信結果](../issues/kohami/13-trusted-payment-order-results.md) | 保留：獨立票券／依賴與紀錄 |
| [14: 處理付款逾時、取消與晚到成功](../issues/kohami/14-payment-races-and-compensation.md) | 保留：獨立票券／依賴與紀錄 |
| [15: 每日對帳與付款異常處理](../issues/kohami/15-daily-reconciliation-payment-exceptions.md) | 保留：獨立票券／依賴與紀錄 |
| [16: 會員購物與受保護的訂單紀錄](../issues/kohami/16-member-checkout-private-orders.md) | 保留：獨立票券／依賴與紀錄 |
| [17: 三語購物旅程與可索引網址](../issues/kohami/17-three-locale-shopping-journey.md) | 保留：獨立票券／依賴與紀錄 |
| [18: 出貨與顧客配送追蹤](../issues/kohami/18-shipping-and-tracking.md) | 保留：獨立票券／依賴與紀錄 |
| [19: 退貨申請、客服與驗收](../issues/kohami/19-return-support-inspection.md) | 保留：獨立票券／依賴與紀錄 |
| [20: 受控退款與財務核對](../issues/kohami/20-controlled-refunds-reconciliation.md) | 保留：獨立票券／依賴與紀錄 |
| [21: 交易憑證與退款發票處理](../issues/kohami/21-invoice-and-refund-adjustments.md) | 保留：獨立票券／依賴與紀錄 |
| [22: 購物政策與客服入口](../issues/kohami/22-policies-and-support-entry.md) | 保留：獨立票券／依賴與紀錄 |
| [23: 最小化報表與資料生命週期](../issues/kohami/23-minimal-reports-data-lifecycle.md) | 保留：獨立票券／依賴與紀錄 |
| [24: 核心購物的手機、可及性與效能驗收](../issues/kohami/24-core-mobile-accessibility-performance.md) | 保留：獨立票券／依賴與紀錄 |
| [25: 商務監測、安全與災難復原](../issues/kohami/25-commerce-security-monitoring-recovery.md) | 保留：獨立票券／依賴與紀錄 |
| [26: 核心商店獨立交付與啟用審查](../issues/kohami/26-core-store-delivery-readiness.md) | 保留：獨立票券／依賴與紀錄 |
| [27: 合法素材的最小換裝實證](../issues/kohami/27-licensed-fitting-pilot.md) | 保留：獨立票券／依賴與紀錄 |
| [28: 確認換裝、收藏與匯出契約](../issues/kohami/28-fitting-outfit-export-contracts.md) | 保留：獨立票券／依賴與紀錄 |
| [29: 從素材審核到首次三視圖換裝](../issues/kohami/29-review-to-first-three-view-fitting.md) | 保留：獨立票券／依賴與紀錄 |
| [30: 跨頁比較並回到真實商品購買](../issues/kohami/30-fitting-session-to-purchase-fallback.md) | 保留：獨立票券／依賴與紀錄 |
| [31: 獨立停用、授權撤銷與快取撤除](../issues/kohami/31-fitting-suspension-rights-cache-revocation.md) | 保留：獨立票券／依賴與紀錄 |
| [32: 會員明確收藏與版本失效提示](../issues/kohami/32-explicit-saved-outfits-version-lifecycle.md) | 保留：獨立票券／依賴與紀錄 |
| [33: 訪客與會員下載授權示意截圖](../issues/kohami/33-licensed-outfit-reference-export.md) | 保留：獨立票券／依賴與紀錄 |
| [34: 四組角色與公告尺寸完整驗收](../issues/kohami/34-four-avatar-families-supported-sizes.md) | 保留：獨立票券／依賴與紀錄 |
| [35: 首批 SKU 相容性與換裝資格公告](../issues/kohami/35-initial-sku-compatibility-publication.md) | 保留：獨立票券／依賴與紀錄 |
| [36: 換裝效能、備援與資料邊界驗收](../issues/kohami/36-fitting-performance-fallback-data-validation.md) | 保留：獨立票券／依賴與紀錄 |
| [37: 換裝啟用範圍與交付審查](../issues/kohami/37-fitting-enablement-readiness.md) | 保留：獨立票券／依賴與紀錄 |
| [38: 以實際營運紀錄驗證服務目標](../issues/kohami/38-operational-service-target-evidence.md) | 保留：獨立票券／依賴與紀錄 |
| [開發交接與待決清單](../planning/kohami/delivery/IMPLEMENTATION_PLAN.md) | 保留 |
| [規劃索引](../planning/kohami/PLANNING_INDEX.md) | 保留 |
| [PLANNING_READINESS.yaml（機器契約／資料）](../planning/kohami/PLANNING_READINESS.yaml) | 保留 |
| [KOHAMI 核心商店與模特換裝參考規格](../planning/kohami/SPEC.md) | 保留 |
| [本機 mock 核心：領域與資料契約提案](../planning/kohami/architecture/DOMAIN_MODEL.md) | 保留 |
| [SQL Server 結構與交易設計提案](../planning/kohami/architecture/SQL_SCHEMA_PLAN.md) | 保留 |
| [本機完整 mock：技術架構與信任邊界](../planning/kohami/architecture/TECHNICAL_ARCHITECTURE.md) | 保留 |
| [售後、退款與對帳詳規](../planning/kohami/commerce/AFTERCARE_REFUND_PLAN.md) | 保留 |
| [商品上架、探索、購物車與報價詳規](../planning/kohami/commerce/CATALOG_PLAN.md) | 保留 |
| [結帳、付款結果與自己的訂單詳規](../planning/kohami/commerce/CHECKOUT_ORDER_PLAN.md) | 保留 |
| [模擬對帳、最小報表與資料生命週期提案](../planning/kohami/commerce/RECONCILIATION_DATA_PLAN.md) | 保留 |
| [會員儲值餘額扣抵、訂單履約與退貨退款規劃](../planning/kohami/commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md) | 保留 |
| [API_ACCOUNT_ACCESS.openapi.yaml（機器契約／資料）](../planning/kohami/contracts/API_ACCOUNT_ACCESS.openapi.yaml) | 保留：設計來源／候選，依範圍採用 |
| [API_ASSET_WORKFLOW.openapi.yaml（機器契約／資料）](../planning/kohami/contracts/API_ASSET_WORKFLOW.openapi.yaml) | 保留：設計來源／候選，依範圍採用 |
| [API_CANONICAL.openapi.yaml（機器契約／資料）](../planning/kohami/contracts/API_CANONICAL.openapi.yaml) | 保留：設計來源／候選，依範圍採用 |
| [M1–M3 API 跨階段一致性與提案差異](../planning/kohami/contracts/API_CROSS_STAGE_CONSISTENCY.md) | 保留：設計來源／候選，依範圍採用 |
| [API_FITTING.openapi.yaml（機器契約／資料）](../planning/kohami/contracts/API_FITTING.openapi.yaml) | 保留：設計來源／候選，依範圍採用 |
| [API_M0.openapi.yaml（機器契約／資料）](../planning/kohami/contracts/API_M0.openapi.yaml) | 保留：設計來源／候選，依範圍採用 |
| [API_M1_M3.openapi.yaml（機器契約／資料）](../planning/kohami/contracts/API_M1_M3.openapi.yaml) | 保留：設計來源／候選，依範圍採用 |
| [本機 mock API 契約提案](../planning/kohami/contracts/API_SPEC.md) | 保留：設計來源／候選，依範圍採用 |
| [API_TOTP_MFA_OPTION.openapi.yaml（選配機器契約）](../planning/kohami/options/API_TOTP_MFA_OPTION.openapi.yaml) | 移至 options：未採用登入強化候選 |
| [Canonical 契約整合計畫](../planning/kohami/contracts/CANONICAL_CONTRACT_PLAN.md) | 保留：設計來源／候選，依範圍採用 |
| [Canonical 候選整合報告](../planning/kohami/contracts/CANONICAL_INTEGRATION_REPORT.md) | 保留：設計來源／候選，依範圍採用 |
| [CANONICAL_OPERATION_MAP.yaml（機器契約／資料）](../planning/kohami/contracts/CANONICAL_OPERATION_MAP.yaml) | 保留：設計來源／候選，依範圍採用 |
| [ACCEPTANCE_EVIDENCE_TEMPLATE.yaml（機器契約／資料）](../planning/kohami/delivery/ACCEPTANCE_EVIDENCE_TEMPLATE.yaml) | 保留 |
| [完整網站開發與交付計畫](../planning/kohami/delivery/IMPLEMENTATION_PLAN.md) | 保留 |
| [本機驗收、復原與交付規劃](../planning/kohami/delivery/VALIDATION_PLAN.md) | 保留 |
| [本機 mock 操作與展示手冊提案](../planning/kohami/delivery/LOCAL_DEMO_RUNBOOK.md) | 保留 |
| [驗證與批准計畫](../planning/kohami/delivery/VALIDATION_PLAN.md) | 保留 |
| [APPROVED_SCOPE.yaml（機器契約／資料）](../planning/kohami/delivery/technical-baseline/APPROVED_SCOPE.yaml) | 保留 |
| [核心 DEC-01–03 審閱與 M0 範圍確認](../planning/kohami/delivery/technical-baseline/CORE_DECISION_REVIEW.md) | 保留 |
| [IMPLEMENTATION_STATUS.yaml（機器契約／資料）](../planning/kohami/delivery/technical-baseline/IMPLEMENTATION_STATUS.yaml) | 保留 |
| [本機技術與交易基線驗收](../planning/kohami/delivery/technical-baseline/READINESS.md) | 保留 |
| [技術基線來源與命令綁定計畫](../planning/kohami/delivery/technical-baseline/SOURCE_BINDING_PLAN.md) | 保留 |
| [SOURCE_BINDING_TEMPLATE.yaml（機器契約／資料）](../planning/kohami/delivery/technical-baseline/SOURCE_BINDING_TEMPLATE.yaml) | 保留 |
| [商品與交易三語內容契約](../planning/kohami/experience/CONTENT_LOCALE_CONTRACT.md) | 保留 |
| [本機 mock 展示：畫面與操作規劃](../planning/kohami/experience/EXPERIENCE_DESIGN.md) | 保留 |
| [本機展示視覺提案](../planning/kohami/experience/VISUAL_SYSTEM.md) | 保留 |
| [M1-cart-mobile.png（靜態線框；非產品實證）](../planning/kohami/experience/wireframes/M1-cart-mobile.png) | 保留：來源與預覽各有用途 |
| [M1-cart-mobile.svg（靜態線框；非產品實證）](../planning/kohami/experience/wireframes/M1-cart-mobile.svg) | 保留：來源與預覽各有用途 |
| [M1-product-desktop.png（靜態線框；非產品實證）](../planning/kohami/experience/wireframes/M1-product-desktop.png) | 保留：來源與預覽各有用途 |
| [M1-product-desktop.svg（靜態線框；非產品實證）](../planning/kohami/experience/wireframes/M1-product-desktop.svg) | 保留：來源與預覽各有用途 |
| [M2-order-status-mobile.png（靜態線框；非產品實證）](../planning/kohami/experience/wireframes/M2-order-status-mobile.png) | 保留：來源與預覽各有用途 |
| [M2-order-status-mobile.svg（靜態線框；非產品實證）](../planning/kohami/experience/wireframes/M2-order-status-mobile.svg) | 保留：來源與預覽各有用途 |
| [M3-admin-refund-desktop.png（靜態線框；非產品實證）](../planning/kohami/experience/wireframes/M3-admin-refund-desktop.png) | 保留：來源與預覽各有用途 |
| [M3-admin-refund-desktop.svg（靜態線框；非產品實證）](../planning/kohami/experience/wireframes/M3-admin-refund-desktop.svg) | 保留：來源與預覽各有用途 |
| [Preset、BodyProfile 與私有素材證據登錄提案](../planning/kohami/fitting/ASSET_EVIDENCE_REGISTRY_PLAN.md) | 保留 |
| [ASSET_EVIDENCE_REGISTRY_TEMPLATE.yaml（機器契約／資料）](../planning/kohami/fitting/ASSET_EVIDENCE_REGISTRY_TEMPLATE.yaml) | 保留 |
| [素材匯入、審核與事件交付提案](../planning/kohami/fitting/ASSET_IMPORT_REVIEW_PLAN.md) | 保留 |
| [素材建置 HTTP 契約與跨資源整合提案](../planning/kohami/fitting/ASSET_WORKFLOW_API_INTEGRATION_PLAN.md) | 保留 |
| [換裝機器契約與身份整合提案](../planning/kohami/fitting/FITTING_API_INTEGRATION_PLAN.md) | 保留 |
| [固定模特素材、收藏與匯出詳規提案](../planning/kohami/fitting/FITTING_ASSET_OUTFIT_PLAN.md) | 保留 |
| [FITTING_EVENT_CANONICAL.schema.json（機器契約／資料）](../planning/kohami/fitting/FITTING_EVENT_CANONICAL.schema.json) | 保留 |
| [FITTING_EVENT_CONTRACT.schema.json（機器契約／資料）](../planning/kohami/fitting/FITTING_EVENT_CONTRACT.schema.json) | 保留 |
| [FITTING_IDENTITY_OVERLAY.yaml（機器契約／資料）](../planning/kohami/fitting/FITTING_IDENTITY_OVERLAY.yaml) | 保留 |
| [FITTING_IMPORT_MANIFEST.schema.json（機器契約／資料）](../planning/kohami/fitting/FITTING_IMPORT_MANIFEST.schema.json) | 保留 |
| [FITTING_PILOT_EVIDENCE_TEMPLATE.yaml（機器契約／資料）](../planning/kohami/fitting/FITTING_PILOT_EVIDENCE_TEMPLATE.yaml) | 保留 |
| [最小素材實證、效能與成本計畫](../planning/kohami/fitting/FITTING_PILOT_PLAN.md) | 保留 |
| [PNG 解碼、加工、hash 與画布詳規](../planning/kohami/fitting/PNG_PROCESSING_PLAN.md) | 保留 |
| [PNG_PROCESSING_PROFILE.yaml（機器契約／資料）](../planning/kohami/fitting/PNG_PROCESSING_PROFILE.yaml) | 保留 |
| [FITTING_CASES.yaml（機器契約／資料）](../planning/kohami/fixtures/FITTING_CASES.yaml) | 保留：隔離合成規劃資料 |
| [M1_CATALOG.json（機器契約／資料）](../planning/kohami/fixtures/M1_CATALOG.json) | 保留：隔離合成規劃資料 |
| [M2_ORDER_CASES.json（機器契約／資料）](../planning/kohami/fixtures/M2_ORDER_CASES.json) | 保留：隔離合成規劃資料 |
| [M3_REFUND_CASES.json（機器契約／資料）](../planning/kohami/fixtures/M3_REFUND_CASES.json) | 保留：隔離合成規劃資料 |
| [帳號生命週期與授權維護規劃](../planning/kohami/identity/ACCOUNT_ACCESS_LIFECYCLE_PLAN.md) | 保留 |
| [本機身份、MFA 與能力安全詳規](../planning/kohami/identity/AUTH_SECURITY_PROFILE.md) | 保留 |
| [訪客訂單、單次動作與會員歸戶存取提案](../planning/kohami/identity/GUEST_ORDER_ACCESS.md) | 保留 |
| [帳號、TOTP 與商務 API 整合規劃](../planning/kohami/identity/IDENTITY_API_INTEGRATION_PLAN.md) | 保留 |
| [後台登入強化選配](../planning/kohami/options/ADMIN_MFA_HARDENING_OPTION.md) | 移至 options：登入強化建議 |
| [選配：本機 Owner 初始化與離線復原](../planning/kohami/options/OWNER_BOOTSTRAP_RECOVERY_OPTION.md) | 保留：延後選配，未採用 |
| [SECURITY_OPERATIONS_PROFILE_OPTION.yaml（機器契約／資料）](../planning/kohami/options/SECURITY_OPERATIONS_PROFILE_OPTION.yaml) | 保留：延後選配，未採用 |
| [決策、假設與來源登錄](../planning/kohami/product/DECISIONS_ASSUMPTIONS.md) | 保留 |
| [KOHAMI 領域術語表](../planning/kohami/product/GLOSSARY.md) | 保留 |
| [KOHAMI 日系服飾電商規劃書](../planning/kohami/product/PRODUCT_PLAN.md) | 保留 |
| [本機 key／cert 生命週期提案](../planning/kohami/security/KEY_CERT_LIFECYCLE_PLAN.md) | 保留 |
| [SQL 最小權限、維護與復原提案](../planning/kohami/security/SQL_LEAST_PRIVILEGE_PLAN.md) | 保留 |
| [票 01：結帳與庫存政策建議 v0.1](../proposals/kohami/01-checkout-inventory.md) | 保留 |
| [票 02：配送、售後與發票政策建議](../proposals/kohami/02-fulfillment-aftercare-invoice.md) | 保留 |
| [票 03：身份、權限與資料政策建議 v0.1](../proposals/kohami/03-identity-permissions-data.md) | 保留 |
| [全流程 mock 展示階段政策 v0.2](../proposals/kohami/04-demo-commerce-policy.md) | 保留 |
| [票 06：.NET／Vue／SQL Server 技術實證基準 v0.2](../proposals/kohami/06-commerce-technology.md) | 保留 |
| [KOHAMI 現行政策入口 v0.2](../proposals/kohami/README.md) | 保留 |
| [QUALITY_PROFILE.yaml（機器契約／資料）](../quality/QUALITY_PROFILE.yaml) | 保留 |

## 本次實際驗證（2026-10-15）

- `python3 /private/tmp/kohami_docs_check.py`：PASS，695 個本機 Markdown 檔案連結無缺失；136 個原有 tracked 文件皆有保留、搬移或移除處置。該腳本為本次暫存檢查，不是專案正式安裝依賴。
- `python3 -m unittest discover -s tests/architecture -v`：PASS，3 tests；搬移映射、Domain 依賴及專案參考有效。
- 使用既有 `/private/tmp/aips-plan38-validation/bin/python` 執行 `scripts/build_kohami_canonical.py --check`：PASS，140 操作候選可重現；執行 `scripts/m0/project_contract.py` 重投影 core34，與原契約比較只差文件指標及來源指紋。
- 同一 Python 環境執行 `scripts/check_kohami_{api_contract,asset_api_contract,asset_planning,canonical,fitting_contract,identity_contract,totp_contract}.py`：7 個 checker 全部 PASS。它們是文件／schema 檢查，未執行產品。
- `scripts/check_kohami_planning_readiness.py`：FAIL，既有 checker 硬編碼 `M0 core34/native7/12AC only`，目前批准範圍與 ledger 為 `native4`。未為了通過工具擴大批准範圍。
- `scripts/check_kohami_security_operations_plan.py`：FAIL，既有 checker 硬編碼 `canonical.status == NOT_CREATED`，現行 ledger 為 `CORE_ADOPTED_PHASE2_PROPOSED`。未倒退文件狀態以滿足舊斷言。
- 對上述兩 checker 僅於記憶體將過時期待值改為目前文件狀態的診斷，餘下斷言全部通過；此為定位證據，不計為正式 checker PASS。兩個 checker 的邏輯修正留在後續工程工作，檔案內本次只同步路徑。
- 整理前另有 PNG profile 的來源指紋與選配 profile 的 `.gitignore` 指紋過期；已閱讀來源並重綁目前指紋。沒有修改 `.gitignore`、採用狀態或產品驗收狀態。
- JSON／YAML 結構比較：搬移前後除了文件路徑、來源指紋及同目錄狀態檔引用外，沒有 API／DTO／角色／事件／合成資料行為差異。
- `dotnet build tests/Core/Core.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false --verbosity minimal`：環境阻擋，缺 `tests/Core/obj/project.assets.json`（NETSDK1004）。`dotnet test ... --no-restore` 未回報測試數，因此不計產品測試 PASS。未還原新套件。
- `dotnet build Kohami.slnx -c Release --no-restore --verbosity minimal`：無完成證據，已停止本次建置程序；不能宣稱 solution build PASS。
- `git diff --check`：PASS。沒有執行 SQL fixture、HTTP／browser、原生 OS 動作、正式服務或部署。

兩個過時 checker 應在下次確認符合專案模型要求的工程工作中，依批准來源更新快照斷言並保留反例檢查；不能藉調整期待值推定產品通過。現行 AIPS 衍生索引已補齊語意及新路徑；未提交的 watched 文件會讓工具 freshness 顯示 STALE，未將其宣稱為乾淨 revision。


## 指示來源與異動範圍

- 使用者全域指示：由本次訊息提供，實際來源檔案路徑無法確認。
- 專案指示：`/Users/lucas/Coding/main/kohami/AGENTS.md`（已讀取）。
- AIPS 固定層：`/Users/lucas/.local/share/aips/system/harness/BOOTSTRAP.md`、`/Users/lucas/.local/share/aips/system/SYSTEM_CORE.md`。
- AIPS 本次選定協定：`/Users/lucas/.local/share/aips/system/orchestration/PLANNING_PACKAGE.md`、`/Users/lucas/.local/share/aips/system/orchestration/REQUIREMENT_CLARIFICATION.md`、`/Users/lucas/.local/share/aips/system/orchestration/PROJECT_IDENTITY.md`、`/Users/lucas/.local/share/aips/system/orchestration/DOCUMENTATION_SYNC.md`、`/Users/lucas/.local/share/aips/system/orchestration/CONFORMANCE.md`；以及 `/Users/lucas/.local/share/aips/system/docs/human/MAINTENANCE.md`。只套用本專案文件整理與規劃相關規則。
- 本次未使用 `SKILL.md`，未委派子代理。

67 筆搬移、2 筆文件移除，以及其餘所有新增／修改的專案檔案路徑，統一列於 [DOCUMENTATION_MIGRATION.json](DOCUMENTATION_MIGRATION.json) 的 `moves`、`removed`、`updated_supporting_files`、`created_supporting_files`。專案根目錄為 `/Users/lucas/Coding/main/kohami`，清單路徑皆相對於此根目錄。

輔助異動限 Markdown／YAML／JSON 引用及指紋、文件工具路徑、API 預設契約檔位置、Core 測試內容複製路徑與 AIPS 衍生索引。未修改產品業務規則、資料庫、原有票券進度或歷史 AIPS run 紀錄。

本次暫存輔助檔位於 `/private/tmp/kohami_docs_reorganize.py`、`kohami_docs_content.py`、`kohami_docs_finish.py`、`kohami_docs_rehash.py`、`kohami_docs_bind_sources.py`、`kohami_docs_check.py` 及 `/private/tmp/kohami-docs-before-hashes.json`；不屬專案執行依賴，未提交，也未保存原始對話或效能 trace。


## 第二輪：唯一執行主計畫、去重與缺口補齊（2026-10-15）

本輪明確授權文件合併、補齊與棄用。開始先核對 AGENTS、現行架構／模組、票券依賴、批准／實作狀態；Change Impact 限本機文件與文件工具來源路徑，不執行產品、SQL、事件或外部發布。AIPS 文件風險 traversal 仍為 BOUNDED_WITH_UNKNOWNS，registry 已按閱讀後的來源更新；uncommitted watched 文件的 freshness 不宣稱 CURRENT。

### 使用者已選的四項決策

核心商店先獨立交付；首輪會員儲值只驗實付、暫不發放贈送金；Issue 22 驗展示政策與客服、正式 07 移至上線閘門；整體先核心本機→實付會員儲值本機→換裝另版本機→正式營運。主計畫的 EXEC-01–04 是目前唯一決策摘要；後續未決各附至少兩方案、建議與決策時點，未推定採用。

### 合併與棄用

- `PLANNING_HANDOFF.md` 合併至 `delivery/IMPLEMENTATION_PLAN.md` 後移除；保留 DEC／NEXT 穩定 ID 於主計畫，原引用與 checker 來源路徑同步。
- `delivery/LOCAL_ACCEPTANCE_PLAN.md` 的 12 項本機驗收、故障／restore、交付條件合併至 `delivery/VALIDATION_PLAN.md` 後移除；不另保留同內容副本。
- `product/PRODUCT_PLAN.md` 只保留產品目標、商業／成本假設；R01–R20 移至 `SPEC.md`，SPEC 的重複 AC／N 表改引用唯一驗收定義。畫面、領域、權限、架構及品質規則各回專屬來源；Next／Vendure／PostgreSQL 未採候選與空專案等過時現況敘述棄用。
- 38 張既有票共用的基準、授權、證據及旅程規則移至 `docs/agents/issue-tracker.md`；保留逐票需求、驗收、Comments、Resolution 及工作狀態。
- 錢包舊 W0–W4 路線表不再與主計畫並行維護；本機實付與正式資金分開，贈送金混用規則標延後選配。
- 兩個含過時 native7／NOT_CREATED 斷言的 checker 不再列為現行通過閘門，標 DEPRECATED_SNAPSHOT_ONLY，仍保留程式／來源追溯；未修改判定邏輯或偽造 PASS。

### 補齊

- 執行主計畫明列起始 Issue 06、01–03 已完成政策、04／05 及後續票的完整檔名、逐階段順序、可操作產品及終點；06 的起手環境與最後結案分開，不重選框架。
- `docs/issues/member-wallet/01–05` 定義合成可信入金、實付帳本、原子結帳、退款／人工履約／對帳、獨立復原及交付；`06` 為停用的贈送金選配。`docs/issues/production-launch/01-go-live-readiness.md` 補上本機交付與營運後觀測之間的正式啟用前驗收。
- Issue 22 依已選方案只依 19；正式 07 由上線票把關。38 明確依正式啟用票及實際正式啟用後執行。
- VALIDATION_PLAN 提供 FINAL-CORE／WALLET／FITTING／LAUNCH／OPS 五個完成閘門與具體演示、成功／拒絕／未知／故障／復原證據。操作手冊將已有命令與仍缺命令、目標／副作用／前置分列；未假造不存在的 command。

### 本輪驗證與限制

- `/private/tmp/kohami_execution_check.py`：45 張票皆能從主計畫找到，44 張在必要順序（含已完成政策輸入），1 張贈送金選配延後；依賴無循環，推薦序列滿足 hard dependencies，原 01–03 resolved／06 claimed 保留。
- `/private/tmp/kohami_docs_check.py`：原 136 tracked 文件均有保留／搬移／合併／移除處置，本機文件連結全數解析；結構化契約無路徑／來源指紋以外的行為差異。
- `python3 -m unittest discover -s tests/architecture -v`：PASS，3 tests。
- 既有七個 API／素材／身份／TOTP／canonical 文件 checker 及 canonical `--check`：PASS。兩個 legacy snapshot checker 仍在原過時斷言 FAIL，未計通過；後續替換需維持反例檢查及實際批准邊界。
- `git diff --check`：PASS；本輪未執行 .NET build、SQL／HTTP／browser／原生儀式或部署。文件補齊不代表產品已實作，TDD 不適用本輪純文件變更。

本輪額外實際讀取 AIPS 指示：`/Users/lucas/.local/share/aips/system/orchestration/CHANGE_IMPACT.md`；專案 `AGENTS.md` 重新讀取，其餘實際套用來源見前述來源清單。未使用 Skill 或子代理。完整檔案異動仍只維護於 DOCUMENTATION_MIGRATION.json，暫存輔助檔位於 `/private/tmp/kohami_*v2.py`、`/private/tmp/kohami_execution_plan.py`、`/private/tmp/kohami_execution_check.py` 等，不是專案產品依賴，未保存使用者原始對話或效能 trace。

## 本機 commit 內容檢查觀察（2026-10-15）

AIPS 唯讀 content_scan 回報 CONTENT_PII_BLOCKED，逐檔命中既有契約的合成範例信箱。已確認首次四處命中來自原契約且使用保留範例網域；移除 runtime 示例後其他規劃來源仍會命中，因此已還原示例，避免不必要地改動 schema 驗收樣本。本次未修改安全政策、未使用編碼或 ignore marker，亦不略過 Git hooks。此診斷是合成樣本的分類問題，並非已發現真實個人資料或產品測試失敗。

使用者已明確確認保留合成範例並建立本機 commit。提交前核對暫存區 32 處信箱字串均為保留範例網域，未包含未分類信箱或 .local 私有材料；採人工確認合成資料分類，未修改 AIPS 規則或略過 Git hooks，未授權遠端 push。
