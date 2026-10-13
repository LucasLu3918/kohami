# 規劃交接、待採用與開工前清單

2026-10-13｜READY_FOR_REVIEW｜非產品完成／非實作批准

目前核心M0–M4與Phase 2的主要旅程、領域、API、素材與驗收計畫已具可審閱文件，五份current HTTP提案140操作；已有未採用canonical候選。讀本文件→[canonical差異](CANONICAL_CONTRACT_PLAN.md)→[機器狀態清單](PLANNING_READINESS.yaml)，即可核對來源與剩餘工作，不需歷史對話。

## 1. 已決事項保持有效

政策01–03、本機完整mock、金流／退款／物流／發票mock、ASP.NET Core/.NET 10／Nuxt SSR／SQL Server、MFA方案B（Email/password＋TOTP）、非AI固定模特三視圖／免本人照片方向已確認。登入不自動收藏、保存原版、外觀參考非真人合身保證沿用既定ADR。這些不再列為需要重問的問題。

實際運費／退款授權人員／備援先規劃機制；本機使用合成persona/費率、Owner單人有權退款與MFA。正式服務與真資金權限、真費率／人員／供應商／法定分類另案確認，不阻擋mock文字規劃，也不代表可正式收款或配送。

## 2. 待採用決策分組

| ID | 要審阅的具體內容 | 建議／時點 |
| --- | --- | --- |
| DEC-01 | 核心104操作与CAN-01–09/13/14的wire、版本、權限與錯誤整合 | 建議先核核心mock candidate；Phase 2不是核心M4前置。批准範圍不可從繼續規劃推定 |
| DEC-02 | TOTP註冊/近期/復原細則、密碼與cookie/session期限／單次proof/credential替換 | B方法已採用，此組只採用細則；protected bootstrap與key/recovery命令尚需具體計畫，不讓Email單封停MFA |
| DEC-03 | 核心UX／三語fallback／QA與效能門檻／本機展示文案 | 先採用可驗收目標，實際結果再以CHK留證；不虛構AA/SLA或正式法律告知採用 |
| DEC-04 | Phase 2 Owner fitting.read/manage及asset.publish/revoke、精確purpose/kind與CAN-10–12 | 素材管理開工前審阅；原權限enum先維持，其他角色不默認授權 |
| DEC-05 | 收藏上限及超限／替代另存計數 | 20仍暫定；需採用後配置，未配置拒保存。免費正常使用不等於無限儲存或繞安全限流 |
| DEC-06 | import/export TTL、byte/pixel/並行及座標/加工數值 | 先按候選規劃，依實際decoder/裝置pilot校準後採用，非SLA承諾 |
| DEC-07 | 首批公告尺寸、variant×SKU與匯出允許用途 | 先取得合法三視圖／權利／品質／成本证据，只公告合格組合；維持四組方向，不默認全尺寸／約20 SKU都合格 |

這是審閱分組，不要求一次全部回答；核心与Phase 2分開處理。工程可自行處理無實質風險的命名／檔案／ref細節；產品行為／安全／資料／成本／兼容性差異保留必要確認。

## 3. 尚需規劃與實作綁定

| ID | 剩餘項目 | 分類／下一個可做的工作 |
| --- | --- | --- |
| NEXT-01 | canonical candidate與逐操作consumer map | 已形成140操作未採用候選與14差異處置；採用後再綁實作consumer及client工具 |
| NEXT-02 | 初始Owner/bootstrap、離線recovery、key/cert/SQL最小權限 | 已有機制詳規；native helper/broker、命令、key及SQL來源仍待綁定與M0實證 |
| NEXT-03 | Preset/BodyProfile與私有來源／權利／review evidence登錄 | 已有登錄詳規與空模板；private native權限待採用、工具／原件／量測及人工證據仍待綁定 |
| NEXT-04 | PNG decoder/library、四類hash／畫布規格與隔離source | 已有詳規及profile；native/RID/worker/隔離來源與真素材pilot仍UNBOUND |
| NEXT-05 | 全部產品checks與交付命令 | 實作／實證；M0身份/SQL/SSR→M1–M3核心→M4，Phase 2按原票依賴另交付；文字不能補成PASS |

NEXT-02–04已有機制詳規；三項尚未完全綁定命令／工具／來源，是定位完成的規劃缺口，不是產品已實作；可继续不依賴人員／採購的機制詳規。NEXT-05不是再增加文字就能完成，需真正授權實作與執行證據。

## 4. 開工與交付證據

[PLANNING_READINESS](PLANNING_READINESS.yaml)的EVD-01–11对应CHK-01–08、M0／M4模板與素材pilot。全部產品status保持NOT_EXECUTED、command/evidence/person=null；現有17純規則PASS與SQL smoke是既有實驗證據，不能升格為產品SQL併發、TOTP或E2E通過。

核心必要：實際.NET/Nuxt/SQL candidate與最小grant、auth/CSRF/TOTP/撤權、交易與worker重啟、SSR/三語/可及性、效能與獨立restore、clean setup/seed/start/stop與人工交付。Phase 2另需Preset/evidence/decode來源、合法非AI素材、quality/rights三視圖、真裝置与成本範圍；正式費率／人員／service與法律分類延期。

Gate 1文件可審閱不等於Human採用；Gate 2實作範圍批准另記；產品所有checks有候選證據才談LOCAL_COMPLETE。統計操作數、schema/負向案例數與工具退出0都不能取代上述關卡。

## 5. 來源與目前票況

來源入口：[現行mock政策](../../../.scratch/kohami/proposals/README.md)、[ADR0001](../../adr/0001-preset-avatar-fitting.md)、[ADR0002](../../adr/0002-dotnet-vue-sqlserver.md)、[決策](DECISIONS_ASSUMPTIONS.md)、[實作與checks](IMPLEMENTATION_PLAN.md)。Word v0.1仍是過時快照；現行Markdown及明示較新決議優先，較早「MFA待決」「OpenAPI未建立」屬歷史狀態。

38票保留原triage與Work status，01–03 policy resolved、06 claimed，其餘未完成；本輪不改票況、不解除依賴、不推定開工／採購／正式發布。機器checker會核對這個票況及來源hash；來源改變後需要重新審阅，不把舊狀態表當永遠正確。


## NEXT-02機制詳規接續

[初始Owner／native復原](OWNER_BOOTSTRAP_RECOVERY_PLAN.md)、[key/cert生命週期](KEY_CERT_LIFECYCLE_PLAN.md)、[SQL最小權限](SQL_LEAST_PRIVILEGE_PLAN.md)與[安全操作profile](SECURITY_OPERATIONS_PROFILE.yaml)已補齊可審閱規則。NEXT-02現為DOCUMENTED_PROPOSAL_SOURCE_UNBOUND，不代表命令可用或已採用：原生根信任/helper/broker、bootstrap stage、key/namespace/TLS、實際SQL allowlist仍待source/M0。DEC-02新增native授權例外/profile/期限與key-cert/SQL細則審閱，MFA B不重問；操作／key／cert／帳號／SQL均NOT_EXECUTED。


## NEXT-03 私有證據登錄接續

[Preset/BodyProfile與私有證據詳規](ASSET_EVIDENCE_REGISTRY_PLAN.md)及[空登錄模板](ASSET_EVIDENCE_REGISTRY_TEMPLATE.yaml)補齊精確來源版本、native內容門禁、人工Review、失效hold、反向依賴與復原。NEXT-03為DOCUMENTED_PROPOSAL_SOURCE_UNBOUND；12項REG-AC均NOT_EXECUTED。DEC-04/07仍需審閱native私有工作台權限、真量測與素材允許用途；本文不自動啟用overlay。viewer/helper/SQL/root/key與真來源仍UNBOUND，未新增HTTP操作或實際證據。


## NEXT-04與canonical候選整合接續

[PNG詳規](PNG_PROCESSING_PLAN.md)與[profile](PNG_PROCESSING_PROFILE.yaml)完成12項PNG-AC規劃；[整合報告](CANONICAL_INTEGRATION_REPORT.md)及[逐操作map](CANONICAL_OPERATION_MAP.yaml)形成core104＋條件Phase2_36候選。Candidate可審閱但未採用，所有產品NOT_EXECUTED；新shipping.configure/demo.control近期purpose與四個Phase2權限需DEC-01/02/04審閱，數值/畫布限制需DEC-06/07及pilot。
