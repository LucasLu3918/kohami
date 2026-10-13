# KOHAMI 規劃文件索引

> **現行 MFA 決議（2026-10-13）：** 使用者明確「先採用B」，第一版後台採 Email／密碼＋驗證器 TOTP，Passkey 留後續。下文先前「待決／Passkey優先」是歷史狀態，已由此決議取代。方案選擇阻擋已解除；註冊／proof／復原機器契約與產品實證仍待完成，不能標為 PASS。詳見 [身份詳規](AUTH_SECURITY_PROFILE.md)。

## M2結帳、付款結果與自己的訂單（本輪）

[M2_CHECKOUT_ORDER_PLAN](M2_CHECKOUT_ORDER_PLAN.md)與[GUEST_ORDER_ACCESS](GUEST_ORDER_ACCESS.md)補Unknown、逾時／取消／晚到成功、訂單分離狀態與read/action/claim範圍；[合成case](fixtures/M2_ORDER_CASES.json)含14狀態＋5存取反例，[訂單頁線框](wireframes/M2-order-status-mobile.png)可審閱。仍是提案／產品NOT_EXECUTED，MFA保持NEEDS_DECISION。

## M1商品與費用確認規劃（本輪）

[M1_CATALOG_PLAN](M1_CATALOG_PLAN.md)補上架／缺貨／搜尋／SKU／cart／Quote詳規，搭配[CONTENT_LOCALE_CONTRACT](CONTENT_LOCALE_CONTRACT.md)及[三商品六SKU合成fixture](fixtures/M1_CATALOG.json)。[桌面商品線框](wireframes/M1-product-desktop.png)與[手機購物車線框](wireframes/M1-cart-mobile.png)可直接審閱；SVG是可維護來源。全部是規劃／合成資料，沒有產品實作或真商品證據，MFA仍NEEDS_DECISION。

## M0合約深化（本輪）

先讀[M0_READINESS](M0_READINESS.md)，再讀[AUTH_SECURITY_PROFILE](AUTH_SECURITY_PROFILE.md)、[SQL_SCHEMA_PLAN](SQL_SCHEMA_PLAN.md)、[API_M0.openapi.yaml](API_M0.openapi.yaml)。全為提案／產品NOT_EXECUTED，後台MFA相關NEEDS_DECISION。M0子集13個operation／20個DTO，不取代下方46群組總綱；SQL只列結構，未做migration。

## 本機核心 mock 工程審閱包（新增）

日期2026-10-13｜READY_FOR_REVIEW。使用者確認本機完整展示；以下新設計／門檻／估算是提案，尚未取得採用或產品實作批准，沒有測試通過宣稱。

| 文件 | 可審閱內容 |
| --- | --- |
| [EXPERIENCE_DESIGN](EXPERIENCE_DESIGN.md) | 沿用SCR-01–09與子畫面、文字線框、三語狀態、空白／錯誤／權限行為 |
| [VISUAL_SYSTEM](VISUAL_SYSTEM.md) | 日系商店視覺方向、10組token／元件建議；實際視覺稿未製作 |
| [DOMAIN_MODEL](DOMAIN_MODEL.md) | 12個DOM實體群組、資料約束、分離狀態、SQL交易與事件邊界 |
| [API_SPEC](API_SPEC.md) | 46個OP群組、授權、payload邊界、版本／冪等／錯誤；Authority PROPOSED，OpenAPI未生成 |
| [TECHNICAL_ARCHITECTURE](TECHNICAL_ARCHITECTURE.md) | 既有框架上的本機程序、身份/MFA候選、loopback／SSR／SQL／worker信任邊界 |
| [QUALITY_PROFILE](../../quality/QUALITY_PROFILE.yaml) | 七面向、10個Q條件、量測方法、N/A與限制 |
| [IMPLEMENTATION_PLAN](IMPLEMENTATION_PLAN.md) | M0–M4、8項Required Checks、工時估算與LOCAL_COMPLETE／批准邊界 |

方案方向適配本機展示；詳細審閱須確認UX／視覺、身份與MFA實證、API DTO／物理schema及品質門檻。合法換裝素材與收藏上限仍依票27/28，正式服務與法定分類延期。讀者可依本包規劃下一步，不需把歷史候選重新採用。

## 現行入口與版本限制

優先閱讀 [現行政策 v0.2](../../../.scratch/kohami/proposals/README.md) 及 [全流程 mock 詳規](../../../.scratch/kohami/proposals/04-demo-commerce-policy.md)。目前交付為 mock 展示，已選 ASP.NET Core／Nuxt SSR／SQL Server；17個純規則測試及獨立 SQL 開發環境已驗證，產品尚未完成。

**Word v0.1 已過時：** 以下 Word 連結保留為原規劃快照，不包含後續政策／技術決策，不可作為現行驗收來源。本輪維護 Markdown 政策，未重製 Word；若需新整合閱讀版，應依現行來源重製並逐頁驗證。本文後续 v0.1「空目錄／無Git／未選技術」屬建立時紀錄，不是現在狀態。

版本 v0.1｜日期 2026-10-13｜狀態 READY_FOR_REVIEW（文件可審閱，產品尚未批准實作）

## 閱讀入口

1. [產品規劃書](PRODUCT_PLAN.md)：產品目標、交付範圍、旅程、需求、領域、架構候選及成本。
2. [Word 規劃書](KOHAMI-規劃書-v0.1.docx)：整合主文、驗證、術語、決策與 ADR 的審閱版本。
3. [驗證與批准計畫](VALIDATION_PLAN.md)：外部查核、需求驗收及分層批准。
4. [術語表](GLOSSARY.md)：統一重要概念及邊界。
5. [決策與假設](DECISIONS_ASSUMPTIONS.md)：D396–D446、歷史取代關係及來源完整性。
6. [ADR-0001](../../adr/0001-preset-avatar-fitting.md)：固定非 AI 三視圖的理由、替代與再審議條件。
7. [本次異動範圍](CHANGE_IMPACT.yaml)：本地文件邊界，與產品實作授權分開。

Markdown 是後續維護來源；Word v0.1 是歷史整合閱讀快照，已明示過時；現行政策以 Markdown 更新為準，重製新版 Word 時須渲染驗證。

## 套件狀態與適用性

| 文件面向 | 所在位置／狀態 | 說明 |
| --- | --- | --- |
| 產品計畫與需求 | PRODUCT_PLAN §§1–4 | 可審閱需求基準 |
| UX 與視覺方向 | EXPERIENCE_DESIGN／VISUAL_SYSTEM | 文字線框與token提案可審閱；完成視覺稿仍待製作 |
| 領域模型 | DOMAIN_MODEL／GLOSSARY | 邏輯資料與狀態提案；尚未建立物理Schema |
| 技術架構 | ADR-0002／TECHNICAL_ARCHITECTURE | 框架已採用；本機程序／身份詳規待採用及M0實證 |
| 驗證／品質／批准 | VALIDATION_PLAN | 計畫已定義，產品驗證 NOT_EXECUTED |
| 分期／成本／人力 | IMPLEMENTATION_PLAN | 本機M0–M4與工作量範圍提案；沒有報價或交期承諾 |
| 決策與來源 | DECISIONS_ASSUMPTIONS、ADR | 可見新決策直接追溯，舊決策依總結保留 |
| 官方技術參考 | 新工程文件內來源 | W3C/Microsoft官方文件核對；沒有供應商商業選型研究 |
| API／Schema／實作 Profile | API_SPEC／DOMAIN_MODEL | 提案已建立；OpenAPI、物理Schema與產品程式尚未建立 |
| 可執行產品／部署／正式合規結論 | 本次 N/A | 本次只製作文檔，未實作或對外發布 |

不建立選型已定或工程可開工的假象。未決事項依外部證據、完整來源及詳規審查補齊。

## 來源與重現

來源為指定對話「設計樹訪談」，ID `6acc802e-b1b0-83e8-84f7-bacbe80d021a`，現行基準為 Round 18。實際來源範圍與 S14–S18 定位見決策登錄，不保存原始聊天、工具參數或執行輸出。

本次明確授權將規劃文件放在 `/Users/lucas/Coding/main/berry-match`；此處初始為空且不是 Git 專案。僅把 KOHAMI 的文件收在專屬子目錄，不推定既有專案更名、建庫或開發批准。

閱讀其他文件不需隱藏聊天上下文。重建 Word 所需內容皆在此套件；編排採繁體中文、層級標題、可編輯表格及頁碼。後續重新產生後仍需渲染與逐頁檢查。

## M3 售後、退款與對帳詳規（提案）

[M3流程與12項驗收](M3_AFTERCARE_REFUND_PLAN.md)、[來源對帳／最小報表／資料生命週期](RECONCILIATION_DATA_PLAN.md)、[合成退款案例](fixtures/M3_REFUND_CASES.json)、[管理退款線框](wireframes/M3-admin-refund-desktop.png)。文件與靜態示意可審閱；產品驗收全部NOT_EXECUTED，MFA機制仍待決。

## M4 本機驗收與交付提案

[M4驗收／復原與完成判準](M4_LOCAL_ACCEPTANCE_PLAN.md)、[操作與展示手冊](LOCAL_DEMO_RUNBOOK.md)、[候選證據模板](M4_EVIDENCE_TEMPLATE.yaml)。12項M4-AC串接8個CHK；命令未綁定、全部產品NOT_EXECUTED。M0–M4核心規劃已有階段詳規，尚需採用／身份決策與完整機器契約深化，非LOCAL_COMPLETE。

## M1–M3 API整合詳規

[完整本輪範圍OpenAPI](API_M1_M3.openapi.yaml)與[跨階段一致性](API_CROSS_STAGE_CONSISTENCY.md)：76 operations／112 schemas，M0保存原版本；MFA與全帳號／換裝契約不在本輪展開。可審閱機器契約不是採用或產品驗收。可用 scripts/check_kohami_api_contract.py 重現結構／樣本／原快照檢查。


## 帳號與授權生命週期提案

[帳號、改 Email、限時授權、最後 Owner 與16項驗收](ACCOUNT_ACCESS_LIFECYCLE_PLAN.md)補齊身份語意；既有76-operation OpenAPI不變，新增維護路徑尚待機器展開。MFA仍待決，產品NOT_EXECUTED。


## TOTP 身份契約提案

方案B已採用；[TOTP註冊、近期驗證與復原](TOTP_MFA_LIFECYCLE_PLAN.md)、[補充OpenAPI](API_TOTP_MFA.openapi.yaml)、scripts/check_kohami_totp_contract.py 可審閱／重跑。獨立9operations不直接累加成全產品API；整合OP-17/session差異與產品實證仍待完成。


## 帳號、授權與身份API接續

[帳號／授權19操作](API_ACCOUNT_ACCESS.openapi.yaml)、[身份整合與10項補充驗收](IDENTITY_API_INTEGRATION_PLAN.md)、scripts/check_kohami_identity_contract.py。商務76＋TOTP9＋帳號19＝104個跨文件唯一操作，不是全產品或canonical；原M0與兩份現行提案檔保留。產品仍NOT_EXECUTED，換裝／收藏／匯出契約可接續。


## 固定模特素材／收藏／匯出接續

[FITTING_ASSET_OUTFIT_PLAN](FITTING_ASSET_OUTFIT_PLAN.md)與[16項合成驗收矩陣](fixtures/FITTING_CASES.yaml)補齊素材發布／權利分項、原版本收藏、撤銷交付、匯出與restore。PROPOSED／NOT_EXECUTED；FOP-01–12僅API輪廓，未加入104操作機器契約，Phase 2仍另交付，尺寸／素材權利／20組上限待證據與採用。MFA B已採用，本文較早「MFA仍待決」是歷史狀態，不是現行結論。


## 換裝機器契約與身份差異

[20操作／28schemas OpenAPI](API_FITTING.openapi.yaml)、[未啟用身份overlay](FITTING_IDENTITY_OVERLAY.yaml)、[10項整合驗收與差異](FITTING_API_INTEGRATION_PLAN.md)、scripts/check_kohami_fitting_contract.py。跨四份現行提案124個唯一method/path；原M0保留不重複計。20操作涵蓋FOP輪廓與補充context/read，非全產品canonical。PROPOSED／NOT_EXECUTED；素材匯入／實證、權限採用、quota／engine limits與執行驗證仍待完成。


## 素材匯入、事件與最小實證計畫

[匯入／審核及12項驗收](ASSET_IMPORT_REVIEW_PLAN.md)、[Manifest schema](FITTING_IMPORT_MANIFEST.schema.json)、[六種outbox事件schema](FITTING_EVENT_CONTRACT.schema.json)、[實證／成本／裝置計畫](FITTING_PILOT_PLAN.md)與[空證據模板](FITTING_PILOT_EVIDENCE_TEMPLATE.yaml)已補齊可審閱提案；scripts/check_kohami_asset_planning.py 僅文件檢查。五份原OpenAPI未變，124操作不增加；import/新alignment與Compatibility建置HTTP契約尚需展開。真素材／權利／工具／裝置／成本／人工審閱仍NOT_EXECUTED，票27依賴保留。


## 素材工作流程HTTP接續

[16操作／25schemas補充API](API_ASSET_WORKFLOW.openapi.yaml)、[10項整合驗收](ASSET_WORKFLOW_API_INTEGRATION_PLAN.md)、scripts/check_kohami_asset_api_contract.py。跨現行提案140唯一操作，不是canonical／全產品；原5OpenAPI及Manifest/Event/overlay保留。新增建立僅Draft/Pending，不隱含發布；preset/evidence工具、decoder與座標/加工規格source綁定、初始授權、真素材／裝置實證與數值採用仍待完成。


## 現行規劃交接入口（2026-10-13）

[PLANNING_HANDOFF](PLANNING_HANDOFF.md)集中已決／待採用／剩餘工作，[PLANNING_READINESS](PLANNING_READINESS.yaml)固定來源與票況，[CANONICAL_CONTRACT_PLAN](CANONICAL_CONTRACT_PLAN.md)列14組整合差異。核心104／Phase2_36共140個唯一HTTP操作；canonical NOT_CREATED、產品NOT_EXECUTED、7組決策待採用、11組證據待執行。這是目前狀態入口，較早MFA待決／OpenAPI未建立字句屬歷史記錄；MFA B與已決mock政策不重問。


## Owner、離線復原、key/cert與SQL機制詳規

[Owner/bootstrap/recovery](OWNER_BOOTSTRAP_RECOVERY_PLAN.md)、[key/cert](KEY_CERT_LIFECYCLE_PLAN.md)、[SQL權限](SQL_LEAST_PRIVILEGE_PLAN.md)、[非秘密操作profile](SECURITY_OPERATIONS_PROFILE.yaml)與scripts/check_kohami_security_operations_plan.py。7 native action、8 key類別、8 SQL角色与24產品驗收；規則可審閱、來源/命令仍UNBOUND、全部NOT_EXECUTED。已有SQL db_owner/自簽例外不改，新產品最小權限/TLS不宣稱PASS；更新NEXT-02與DEC-02來源，不變契約／票況。
