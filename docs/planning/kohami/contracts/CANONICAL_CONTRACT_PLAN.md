# Canonical 契約整合計畫

## 現行適用範圍（2026-10-15）

本文保留需求、設計與具日期的驗證依據；目前已有 .NET／Nuxt／SQL 程式、core34 採用子集與部分產品實證。完整 131 操作候選已建立，核心細則已部分採用；其餘功能／Phase2 不因此視為已批准或完成。產品逐項狀態、範圍與來源優先順序請由[文件入口](../../../README.md)查閱；舊段落中的尚未建立／未決／全部未執行須依原日期與案例範圍解讀。


2026-10-13｜PROPOSED／READY_FOR_REVIEW｜canonical候選已建立、未採用／產品NOT_EXECUTED

適配性：適合先整理差異与來源，再按已確認交付分期整合。保留原基準可以核對變更，不需要現在直接合併所有Schema。這份文件規劃如何形成canonical，不授權生成client／建立產品／替使用者採用新政策。

## 1. 範圍與來源

[PLANNING_READINESS](../PLANNING_READINESS.yaml)固定來源hash與實際operation/schema數。現行canonical候選含131個唯一method/path及operationId；歷史Foundation的13操作不重複加總。核心mock為97；Phase 2為換裝20＋素材工作流程16＝36。登入強化候選細節只在選配文件。數量僅辨識本輪已展開HTTP範圍，不表示完整功能／event／命令／UI／資料庫／測試已完成。

建議先審閱整合核心97操作，再依素材／權限採用與實證開Phase 2。這沿用Foundation–M4核心及Phase 2另交付的既有順序，不把素材前置拉進核心驗收；既定非AI三視圖方向亦不因延期刪除。

候選canonical位置為 `API_CANONICAL.openapi.yaml`，已建立為**未採用候選**。本輪一檔含conditional Phase 2並標core95/phase2_36；登入強化細節集中於選配文件，正式生成／交付仍需依採用範圍固定；不得用生成器默認值決定交付範圍。檔名、Schema內部命名可由工程處理，但會改wire行為／權限／scope／法定告知／產品承諾的差異需審閱。

## 2. 差異處置表

| ID | 實際差異／風險 | 整合提案及完成證據 |
| --- | --- | --- |
| CAN-01 | Foundation是歷史子集，五份current不重複但不含離線命令；不能將153當全產品操作數 | 選核心97、完整候選131範圍，保留source→operationId一對一map；未選階段不對外提供操作 |
| CAN-02 | 各檔同名Problem/Session/Selection/AdminAsset只是local namespace | 內部Schema候選前綴Commerce/Account/Fitting/AssetWorkflow，不改payload field；逐一local ref重寫，只有結構/語意確認一致才共享 |
| CAN-03 | Problem共用type/title/status/code/traceId，部分有fieldErrors、code有free string或closed enum | 一份公開Problem形狀提案保留五必要欄位及可選fieldErrors；建立每endpoint允許error code與HTTP映射，不把所有code開給所有操作。未知/PII內容不直接回client |
| CAN-04 | 原商務Anonymous例子含非null userId/challenge/time，與Foundation及身份補充語意不一致 | Anonymous：userId/expiry為null、permissions空；Member不升admin；AdminAuthenticated完整字段一致。修sample与state條件而非原基準靜默覆寫 |
| CAN-05 | 登入增強不屬現行canonical；候選內容集中於選配文件 | Email/password登入依目前server授權建立session；登入方式不改變現行授權 |
| CAN-06 | commerce x-authorization、account role/permission allowlist與未啟用fitting overlay並非自動一致 | 逐操作effectiveRole/permission/resource/crossDataset map；未知deny；Support不因聯集取得refund.execute，Owner不代替權限；Phase 2新值未採用前不可使用 |
| CAN-07 | Session、auth-stage、rebind-stage与fitting-browser能力不同；nonce亦不同用途 | Same-origin/CSRF/Origin同BrowserId但用途隔離，cookie Path/rotation與SSR request state明確；proof不放URL/log。登入/登出/撤權清私有state，無權stage不讀資料 |
| CAN-08 | User/access/grant、Order、Asset、Session/Slot ETag不同；重送有業務與proof兩類 | header/body對同aggregate，來源read可查；先驗現權再business replay，同key同指紋不展期限或雙執行；proof單次consume不靠business key復活 |
| CAN-09 | 商務快照/Reservation/Quote／退款normal/補償來源與source collection/event delivery不同 | TWD整元與unit allocation、15分鐘保留／重送不展延、Unknown保留、晚到全額補償跨DTO/SQL/worker核一致；換裝狀態不改cart/Order/Stock |
| CAN-10 | 原6 Event與WorkflowEvent新增3type/kind、Rights epoch/aggregateVersion/rowversion不同 | 固定canonical registry與consumer allowlist，不能直接送新增type給原schema；transactional outbox/inbox、current state、fencing/restore與清理去重留證 |
| CAN-11 | AdminAsset原形狀沒有新的effectiveRightsRecordId，Rights child ETag是parent；Alignment候選非Approved | 加parent rights pointer與版本映射，Pending/終態不隱含授權，唯一current有效權利；坐標/加工/hash規格先綁source，Compatibility對真素材另審 |
| CAN-12 | Guest export與Member source、原Saved Outfit/替代另存不同 | 同browser binding不移轉成Member資源；原版不換圖，替代新id；quota未配置拒保存；Ready不立刻刪server暫存且交付前重新核權利 |
| CAN-13 | Content locale fallback／三語DTO／enum未知值與SSR/private cache跨階段 | zh-TW/ja/en的locale保留行為與translation fallback按既有提案；enum未知不當成功或任意status PATCH，private response不入公開cache；wire序列化與UI狀態測試 |
| CAN-14 | source hashes、schema生成命令/版本、核心及Phase 2 checks都未形成已採用canonical candidate | 精確範圍/commit/registry版本/DTO source固定，保留差異報告與人工採用；generated client只是下游選項，package/版本與命令未指定，不製造假的生成PASS |

各差異的原來源詳見[身份整合](../identity/IDENTITY_API_INTEGRATION_PLAN.md)、[商務一致性](API_CROSS_STAGE_CONSISTENCY.md)、[換裝](../fitting/FITTING_API_INTEGRATION_PLAN.md)、[素材工作流程](../fitting/ASSET_WORKFLOW_API_INTEGRATION_PLAN.md)。Proposal數字、limits與權限不由文件存在轉成ACCEPTED。

## 3. 整合步驟與驗證

1. 固定採用範圍與來源hash，列wire／權限／data／event／consumer變更及未採用項目。原提案和歷史Foundation保持可回查。
2. 先決定共用Problem/Session及cookie/stage的語意，再轉換local ref。不可只concat paths/components或按同名選最後一份。
3. 逐operation映射來源、request/response/error/permission/resource/ETag/idem／隱私，準備成功與拒絕wire例子。純rename不改對外DTO；有breaking behavior明列審閱。
4. 建立canonical **candidate**，先標PROPOSED；doc validation通过只證明結構/範例，不產人工採用。源hash變動須重新審差異，不能順手更新鎖檔掩蓋變更。
5. 採用後才綁.NET/Nuxt實際DTO/serialization/client schema、source命令/版本及必要SQL/身份實證；正式開工範圍與先行Foundation另明確確認。
6. CHK-01的doc stage與product wire stage分列；CHK-02–08不能因canonical文件存在就PASS。核心97可先交付，Phase 2須另有素材/權限/裝置證據。

最少負向邊界：Anonymous混入User、stage越權、Support退款、錯resource scope、跨Dataset/cursor/私有資源、錯aggregate ETag、同key異指紋、未配置quota、新enum未採用、舊event復活rights、Source/hash缺失仍Approve、UI或生成client默認未知state成功。

## 4. 未完成但已定位的事項

Canonical候選已建立但尚未採用；protected bootstrap/recovery、key/cert與實際命令、Preset/BodyProfile／證據登錄工具、decoder/hash/畫布source綁定、真素材與裝置／成本、DTO/SQL/SSR/E2E產品實證都另有缺口。[交接表](../delivery/IMPLEMENTATION_PLAN.md)列下一步與決策／證據類型，不把它們混成「全部規劃完成」或全部當使用者要回答的問卷。


## 文件候選整合完成

[本輪整合報告](CANONICAL_INTEGRATION_REPORT.md)、[候選API](API_CANONICAL.openapi.yaml)、[逐操作consumer map](CANONICAL_OPERATION_MAP.yaml)與[九種event候選](../fitting/FITTING_EVENT_CANONICAL.schema.json)完成上述CAN-01–14的文件處置。差異為DOCUMENTED_IN_CANDIDATE，仍待Human採用；不代表實作/相容性/產品驗收完成。NEXT-04詳規另見[PNG計畫](../fitting/PNG_PROCESSING_PLAN.md)。
