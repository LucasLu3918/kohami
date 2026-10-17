# Canonical 候選整合報告

## 現行適用範圍（2026-10-15）

本文保留需求、設計與具日期的驗證依據；目前已有 .NET／Nuxt／SQL 程式、core34 採用子集與部分產品實證。完整 131 操作候選已建立，核心細則已部分採用；其餘功能／Phase2 不因此視為已批准或完成。產品逐項狀態、範圍與來源優先順序請由[文件入口](../../../README.md)查閱；舊段落中的尚未建立／未決／全部未執行須依原日期與案例範圍解讀。


2026-10-13｜CANDIDATE_READY_FOR_REVIEW／PROPOSED｜未採用／產品 NOT_EXECUTED

本輪依明確要求完成文件候選整合：核心97、條件式Phase 2_36，共131操作；歷史M0排除重複。原五份HTTP、Manifest/Event與identity overlay保留byte基準。單一[canonical候選](API_CANONICAL.openapi.yaml)及[逐操作map](CANONICAL_OPERATION_MAP.yaml)供審閱，未生成.NET/Nuxt client或部署路由。相容性不是backward-compatible聲明，所有wire／條件／scope差異待DEC-01/02/04採用。

適配性：適合以單一candidate避免同名schema覆寫，同時保留分期標記與拒絕未啟用能力。Core97是路由交付profile；登入強化另列選配且不納入核心路由；Phase2路由及新增grant/purpose必須分別啟用。完整candidate的schema能表示Phase2值，不代表核心server可接受；執行時未採用值仍deny。生成client前必須選採用profile、版本與generator，不能直接使用全檔默認。

## 1. 十四組差異的具體處置

| 差異 | 候選處置／保留的驗收 |
| --- | --- |
| CAN-01 | 五來源source→operationId/hash精確131筆；每操作core/phase2，native命令不加HTTP操作數 |
| CAN-02 | components採Commerce/Account/Fitting/AssetWorkflow前綴，local refs重寫；cookie同name核一致；仅Problem/Session/AdminAsset與event語意明確共享 |
| CAN-03 | 一份Problem保留五必要欄位與可選fieldErrors；每操作每已宣告HTTP status約束code enum，新增canonical錯誤映射明列為提案；原封閉enum非自動相容 |
| CAN-04 | Session狀態條件：Anonymous null/空permissions，Member無admin permissions；保留既有wire狀態，不將選配登入強化納入核心profile |
| CAN-05 | password登入建立受限session；敏感操作proof不得作業務replay，登入強化僅屬選配。補shipping.configure/Dataset與demo.control/Dataset近期purpose，修來源fee未映射與scenario recent/verified矛盾；這兩新core目的需審閱 |
| CAN-06 | 131筆authorization roles/permission/resource/commandMap；Owner不替scope，Support無refund.execute；Phase2新增owner-only grant條件与permission→scope、purpose→kind一併編入candidate，activation仍BLOCKED |
| CAN-07 | cookie方案按同server name保留，用途隔離；private no-store，登入回應明示mutually exclusive stage/session清理；browser/request nonce与proof不放URL/log，SSR current actor request隔離仍需實作 |
| CAN-08 | 每操作ETag aggregate/expected fields、required header及業務idem各列；User/access/grant及Session/Slot分版、現權先於replay、proof單次。實際SQL locks/source read仍待實證 |
| CAN-09 | 原TWD/unit allocation/保留15分鐘/Unknown/晚到補償DTO與語意保留；商務不被換裝事件改動；normal/shipping/compensation操作各自scope |
| CAN-10 | [九種fitting event registry候選](../fitting/FITTING_EVENT_CANONICAL.schema.json)合併六原變體＋三workflow變體，oneOf拒未知，不新增PNG/registry事件；Inbox/outbox/current state/epoch與worker handler待實作 |
| CAN-11 | 同語意AdminAsset統一加effectiveRightsRecordId；Active必非null、非Active為null，child ETag仍parent。Draft/Alignment candidate不升Approved；PNG/profile依賴詳規見NEXT-04 |
| CAN-12 | 原guest browser export及member收藏scope、原版與替代另存/不同id/未配置quota拒絕保留；Ready非刪檔、交付再核rights，terminal export不可復活 |
| CAN-13 | 原三語fallback/locale DTO保持原來源；map private cache與consumer，未知enum不默認成功；實際serializer/UI/fallback/SSR尚需候選證據 |
| CAN-14 | 本機builder/checker、來源/輸出hash、131操作consumer與schema例/負向拒絕可重跑；產品DTO/client generator與版本/命令仍null，不製造生成client或runtime PASS |

以上為DOCUMENTED_IN_CANDIDATE，不是Human adoption或產品差異關閉。operation map的consumer是邏輯責任，implementedConsumerSource=null；並非宣稱已找到Nuxt/.NET source。DTO前綴只改內部名稱，其餘語意/required/enum/error code變更須審閱；candidate新版本0.1.0-candidate.1不代表正式API版本。

## 2. 錯誤、授權與回應的審閱重點

共用Problem採URI type／code max100與可選fieldErrors，替代原自由字串/較短code/部分封閉enum。逐操作code map由候選通用HTTP拒絕＋該操作領域拒絕組成；不可將domain錯誤直接當一般隱私可披露內容。公眾account proof仍一般ACCOUNT_PROOF_INVALID，存在/未知同受理；title/fieldErrors不可含原件/個資/secret。`x-error-code-map`與response schema一致，產品handler必須只回對應status/code，缺mapper阻擋交付。

新增shipping.configure、demo.control近期purpose是原契約接縫的修正提案，有限Dataset target且需同session/version/server近期記錄；未採用前不發此proof。Fee/scenario wire body保持原形狀，行為更嚴需人工確認。Phase2四權限只Owner、精確scope/command proof；私有證據native內容權限仍未加HTTP或網站grant，不能借新scope取原件。

stage不足時不取會員/後台資料；getSession可以公開投影Anonymous/受限stage，但不回額外permissions。Member本人服務仍依server ownership；path/admin分類不是安全middleware，map只是工程契約。每個已實作consumer須綁scope解析、目前身份/授權、錯誤映射、aggregate read與SQL裁決來源，再執行wire/越權/競爭案例。

## 3. NEXT-04交接與命令

[PNG詳規](../fitting/PNG_PROCESSING_PLAN.md)與[profile](../fitting/PNG_PROCESSING_PROFILE.yaml)固定受限static PNG、候選worker資源上限、三view画布、scale再平移、透明像素與四hash語意。SkiaSharp4.153.1僅研究候選；native/RID/package/worker/isolator與實際canvas仍UNBOUND。數值/格式限制與picture quality仍DEC-06/07；不要把JSON/openapi例通過當有界PNG decode通過。

本輪實際可重跑**文件命令**（在repo root、有PyYAML/jsonschema/referencing/openapi-spec-validator環境）：

```sh
python scripts/build_kohami_canonical.py --check
python scripts/check_kohami_canonical.py
python scripts/check_kohami_planning_readiness.py
```

builder只建planning候選；--check只比對候選，不生成client。來源hash不符須審閱基準；不能靜默換來源再宣稱相容。Python dependency版本另記實際驗證環境；PNG／.NET／Nuxt的實作命令目前未綁定。無pipeline/module生成器採用。

## 4. 實作前仍必要的工作

DEC-01/02/04採用candidate的共用wire/error/proof及Phase2權限；DEC-06/07依decoder/canvas/真素材pilot校準。先綁M0身份、SQL與SSR；各產品案例仍按其範圍留NOT_EXECUTED。隔離 restore／cutoff 與原生 OS 確認列選配改善，不阻擋本機整體驗收。Core97可獨立交付，Phase2須具合法素材/權利/尺寸/成本/裝置證據；目前技術基線 native 綁定限 key/SQL 維護來源。兩個Gate的人類採用/實作範圍批准仍各自記錄，不由本次整合推定。
