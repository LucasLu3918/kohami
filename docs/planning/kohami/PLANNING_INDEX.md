# 規劃索引

更新：2026-10-15。**開始執行只讀 [IMPLEMENTATION_PLAN](delivery/IMPLEMENTATION_PLAN.md)，目前續做 Issue 06。** 本頁只負責找檔案，不另維護開發順序或狀態。驗收只見 [VALIDATION_PLAN](delivery/VALIDATION_PLAN.md)，操作命令只見 [LOCAL_DEMO_RUNBOOK](delivery/LOCAL_DEMO_RUNBOOK.md)。

[功能規格](SPEC.md)保留於功能根目錄；[規劃機器快照](PLANNING_READINESS.yaml)保留原 kohami/01–38 的追溯，不包含新增的錢包／正式啟用票。產品實際狀態見 [IMPLEMENTATION_STATUS](delivery/technical-baseline/IMPLEMENTATION_STATUS.yaml)。

## 產品定位、術語與歷史決策

- [決策、假設與來源登錄](product/DECISIONS_ASSUMPTIONS.md)
- [KOHAMI 領域術語表](product/GLOSSARY.md)
- [產品目標與商業假設](product/PRODUCT_PLAN.md)

## 畫面、視覺與三語

- [商品與交易三語內容契約](experience/CONTENT_LOCALE_CONTRACT.md)
- [本機 mock 展示：畫面與操作規劃](experience/EXPERIENCE_DESIGN.md)
- [本機展示視覺提案](experience/VISUAL_SYSTEM.md)
- [M1-cart-mobile.png](experience/wireframes/M1-cart-mobile.png)
- [M1-cart-mobile.svg](experience/wireframes/M1-cart-mobile.svg)
- [M1-product-desktop.png](experience/wireframes/M1-product-desktop.png)
- [M1-product-desktop.svg](experience/wireframes/M1-product-desktop.svg)
- [M2-order-status-mobile.png](experience/wireframes/M2-order-status-mobile.png)
- [M2-order-status-mobile.svg](experience/wireframes/M2-order-status-mobile.svg)
- [M3-admin-refund-desktop.png](experience/wireframes/M3-admin-refund-desktop.png)
- [M3-admin-refund-desktop.svg](experience/wireframes/M3-admin-refund-desktop.svg)

## 領域與技術設計提案

- [本機 mock 核心：領域與資料契約提案](architecture/DOMAIN_MODEL.md)
- [SQL Server 結構與交易設計提案](architecture/SQL_SCHEMA_PLAN.md)
- [本機完整 mock：技術架構與信任邊界](architecture/TECHNICAL_ARCHITECTURE.md)

## 商品、訂單、售後與會員餘額

- [售後、退款與對帳詳規](commerce/AFTERCARE_REFUND_PLAN.md)
- [商品上架、探索、購物車與報價詳規](commerce/CATALOG_PLAN.md)
- [結帳、付款結果與自己的訂單詳規](commerce/CHECKOUT_ORDER_PLAN.md)
- [模擬對帳、最小報表與資料生命週期提案](commerce/RECONCILIATION_DATA_PLAN.md)
- [會員儲值餘額扣抵、訂單履約與退貨退款規劃](commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)

## 帳號與授權

- [帳號生命週期與授權維護規劃](identity/ACCOUNT_ACCESS_LIFECYCLE_PLAN.md)
- [選配清單](options)
- [訪客訂單、單次動作與會員歸戶存取提案](identity/GUEST_ORDER_ACCESS.md)
- [帳號與商務 API 整合規劃](identity/IDENTITY_API_INTEGRATION_PLAN.md)
- [選配清單](options)

## key/cert 與 SQL 權限

- [本機 key／cert 生命週期提案](security/KEY_CERT_LIFECYCLE_PLAN.md)
- [SQL 最小權限、維護與復原提案](security/SQL_LEAST_PRIVILEGE_PLAN.md)

## API 設計來源與候選

- [API_ACCOUNT_ACCESS.openapi.yaml](contracts/API_ACCOUNT_ACCESS.openapi.yaml)
- [API_ASSET_WORKFLOW.openapi.yaml](contracts/API_ASSET_WORKFLOW.openapi.yaml)
- [API_CANONICAL.openapi.yaml](contracts/API_CANONICAL.openapi.yaml)
- [M1–M3 API 跨階段一致性與提案差異](contracts/API_CROSS_STAGE_CONSISTENCY.md)
- [API_FITTING.openapi.yaml](contracts/API_FITTING.openapi.yaml)
- [API_BASELINE.openapi.yaml](contracts/API_BASELINE.openapi.yaml)
- [API_M1_M3.openapi.yaml](contracts/API_M1_M3.openapi.yaml)
- [本機 mock API 契約提案](contracts/API_SPEC.md)
- [選配清單](options)
- [Canonical 契約整合計畫](contracts/CANONICAL_CONTRACT_PLAN.md)
- [Canonical 候選整合報告](contracts/CANONICAL_INTEGRATION_REPORT.md)
- [CANONICAL_OPERATION_MAP.yaml](contracts/CANONICAL_OPERATION_MAP.yaml)

## 固定換裝、素材及權利

- [Preset、BodyProfile 與私有素材證據登錄提案](fitting/ASSET_EVIDENCE_REGISTRY_PLAN.md)
- [ASSET_EVIDENCE_REGISTRY_TEMPLATE.yaml](fitting/ASSET_EVIDENCE_REGISTRY_TEMPLATE.yaml)
- [素材匯入、審核與事件交付提案](fitting/ASSET_IMPORT_REVIEW_PLAN.md)
- [素材建置 HTTP 契約與跨資源整合提案](fitting/ASSET_WORKFLOW_API_INTEGRATION_PLAN.md)
- [換裝機器契約與身份整合提案](fitting/FITTING_API_INTEGRATION_PLAN.md)
- [固定模特素材、收藏與匯出詳規提案](fitting/FITTING_ASSET_OUTFIT_PLAN.md)
- [FITTING_EVENT_CANONICAL.schema.json](fitting/FITTING_EVENT_CANONICAL.schema.json)
- [FITTING_EVENT_CONTRACT.schema.json](fitting/FITTING_EVENT_CONTRACT.schema.json)
- [FITTING_IDENTITY_OVERLAY.yaml](fitting/FITTING_IDENTITY_OVERLAY.yaml)
- [FITTING_IMPORT_MANIFEST.schema.json](fitting/FITTING_IMPORT_MANIFEST.schema.json)
- [FITTING_PILOT_EVIDENCE_TEMPLATE.yaml](fitting/FITTING_PILOT_EVIDENCE_TEMPLATE.yaml)
- [最小素材實證、效能與成本計畫](fitting/FITTING_PILOT_PLAN.md)
- [PNG 解碼、加工、hash 與画布詳規](fitting/PNG_PROCESSING_PLAN.md)
- [PNG_PROCESSING_PROFILE.yaml](fitting/PNG_PROCESSING_PROFILE.yaml)

## 唯一執行計畫、驗收與操作

- [ACCEPTANCE_EVIDENCE_TEMPLATE.yaml](delivery/ACCEPTANCE_EVIDENCE_TEMPLATE.yaml)
- [網站執行主計畫：檔案、順序、產品與驗收](delivery/IMPLEMENTATION_PLAN.md)
- [本機操作與驗收命令](delivery/LOCAL_DEMO_RUNBOOK.md)
- [驗證與批准計畫](delivery/VALIDATION_PLAN.md)
- [APPROVED_SCOPE.yaml](delivery/technical-baseline/APPROVED_SCOPE.yaml)
- [核心 DEC-01–03 審閱與 Foundation 範圍確認](delivery/technical-baseline/CORE_DECISION_REVIEW.md)
- [IMPLEMENTATION_STATUS.yaml](delivery/technical-baseline/IMPLEMENTATION_STATUS.yaml)
- [本機技術與交易基線驗收](delivery/technical-baseline/READINESS.md)
- [技術基線來源與命令綁定計畫](delivery/technical-baseline/SOURCE_BINDING_PLAN.md)
- [SOURCE_BINDING_TEMPLATE.yaml](delivery/technical-baseline/SOURCE_BINDING_TEMPLATE.yaml)

## 合成規劃資料

- [FITTING_CASES.yaml](fixtures/FITTING_CASES.yaml)
- [M1_CATALOG.json](fixtures/M1_CATALOG.json)
- [M2_ORDER_CASES.json](fixtures/M2_ORDER_CASES.json)
- [M3_REFUND_CASES.json](fixtures/M3_REFUND_CASES.json)

## 未採用／延後選配

- [選配清單](options)
- [隔離資料庫還原與 cutoff 對帳選配改善](options/ISOLATED_RESTORE_CUTOFF_OPTION.md)
- [選配：本機 Owner 初始化與離線復原](options/OWNER_BOOTSTRAP_RECOVERY_OPTION.md)
- [正式網域與搜尋曝光選配改善](options/PUBLIC_DOMAIN_SEO_OPTION.md)
- [本機深度效能診斷選配](options/LOCAL_PERFORMANCE_DEEP_DIVE_OPTION.md)
- [SECURITY_OPERATIONS_PROFILE_OPTION.yaml](options/SECURITY_OPERATIONS_PROFILE_OPTION.yaml)
