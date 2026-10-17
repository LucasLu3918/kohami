# 技術基線來源與命令綁定計畫

2026-10-13｜PROPOSED／READY_FOR_REVIEW｜SOURCE_UNBOUND／產品 NOT_EXECUTED

適配性：適合。本文件把已有規則轉成首批實作的來源／命令／證據需求，能避免用架構圖、角色名稱或文件PASS代替真正執行。沿用[canonical候選](../../contracts/API_CANONICAL.openapi.yaml)、[整合差異](../../contracts/CANONICAL_INTEGRATION_REPORT.md)、[key/cert](../../security/KEY_CERT_LIFECYCLE_PLAN.md)、[SQL權限](../../security/SQL_LEAST_PRIVILEGE_PLAN.md)與[M0情境](READINESS.md)。本輪只規劃，不建立app、migration、帳號、key或產品命令。

核心canonical95操作已有候選；安全細則/wire/error與UX品質等採用仍待DEC-01、03。Phase2_36、真素材、decoder與私有證據工作台不拉進M0；其M0共用key/SQL邊界仍要驗。原13-operation M0是歷史參考，不再作目前身份契約權威。

## 1. 來源綁定的判準

來源狀態與執行結果分開：UNBOUND→SOURCE_BOUND→CANDIDATE_CHECKED。SOURCE_BOUND至少包含真實repo path或經核對外部tool/package identity、確切revision/hash、入口/設定、caller/consumer、輸入/輸出、資料/事件、安全scope及相依來源；不存在的apps/api路徑、文件示意CLI或套件名稱不算綁定。

CANDIDATE_CHECKED還要真命令、開始/結束、候選/環境/fixture版本、成功及拒絕/故障證據。不可用「先執行再補來源」、tool exit0、現有SQL smoke或純規則17案例升格產品PASS。採用來源的Human決議與SOURCE_BOUND不是同一欄；未確認產品範圍先保持阻擋，不把矩陣當新批准。

| ID | 綁定對象／責任 | 實作來源與必要證據 | 缺失出口 |
| --- | --- | --- | --- |
| BIND-01 | .NET/Nuxt/SQL toolchain／工程 | SDK/runtime/TFM、Nuxt/Vue/Node與SQL image digest/RID、package locks、source commit、dry environment identity | 不開始build/migration，版本只是候選 |
| BIND-02 | canonical→DTO/handler／後端 | 選定core profile/hash；每M0 operation的request/response/error mapper/validator/authorization source；真序列化正負例 | 缺mapper/未知enum拒交付，不按同名Schema自動合併 |
| BIND-03 | SQL schema／各service principal／資料庫工程 | 物理表/FK/unique/transaction/rowversion、role→object/procedure allowlist、API/worker/migrator分離、TLS與正負scope probes | 不用db_owner補錯；不改KohamiTest既有實驗 |
| BIND-04 | 同origin SSR與cookie／前後端 | Nuxt私有server config/API入口、CSRF/Origin/browser binding、每request actor隔離、no-store與三語HTML | stage不足不讀私有資料；unknown Origin拒 |
| BIND-06 | secret/key/cert／原生維護 | key references、session與秘密保護邊界、wrapped-at-rest與私鑰依賴、namespace、HTTPS與SQL TLS、rotation/舊key解密 | 缺key或鏈/SAN錯保持not-ready；不輸出實際secret |
| BIND-07 | Identity／後端來源綁定 | 授權/session version、復原與撤權source | 每次請求查當前授權，不由重新登入擴大業務grant |
| BIND-08 | mock adapters/outbox/worker／後端 | 有限情境registry、持久operation與source ledger、inbox去重、Started/lease/fencing/dispatch epoch；兩worker真process | Unknown不重派、不釋保留；舊epoch不可commit |
| BIND-09 | clock／並行／故障harness／品質 | 固定clock injection、barrier、20獨立client、兩worker id、kill前後commit點、SQL隔離與資料觀察 | 不用順序呼叫當併發、不等sleep碰巧命中deadline |
| BIND-10 | 選配：backup/獨立restore／原生維護 | target allowlist、backup/key dependencies、cutoff後撤權/刪除/hold/已用proof ledger、disabled dispatch與重套順序 | 不屬 M0/M4 整體驗收；若另行啟動改善，仍須不覆原DB、不重演資金，較新ledger缺失拒啟用 |
| BIND-11 | 合成fixture／seed／工程 | 版本/hash/Dataset/SKU/單位/價款快照/4角色與會員A/B/guest，隔離投遞與persona；seed範圍/重跑規則 | 不注入真費率/個資/人員、不從request任意選成功 |
| BIND-12 | setup/start/stop/驗收包／工程與交付覆核 | 真命令registry、目標/變更效果/前置/退出/故障恢復、必備CHK來源；clean checkout rebuild與獨立 handoff 列選配 | start不偷偷migration/seed；stop不把Unknown改Failed |

每項需要角色責任，不填造人員。native/helper存在也不自動給Agent或API操作權；具體人員/備援仍依原機制延期。空值表示未有實際來源，不代表0或PASS。

## 2. 有界執行顺序與退路

1. 固定Human採用範圍與core契約hash、candidate SHA／fixture；綁BIND-01/02/11。M0只實作必要垂直切片，完整M1–M3旅程仍按原票依賴。不生成未採用Phase2 client、路由或grant。
2. 綁BIND-03/06：新隔離產品DB、migration credential與最小runtime credential分開，先核DB/instance/TLS；migration錯誤停維護，按精確候選做forward-fix/獨立restore，不dropDB救錯。
3. 綁BIND-04/07：SSR同origin、匿名→Member/admin stage及敏感操作proof；可繼續獨立SQL/DTO來源工作。
4. 綁BIND-08/09：受控mock operation與真SQL額度/庫存裁決，再故障/併發。Started與結果追蹤先成立，拒用前台成功或手改DB替代。
5. 綁BIND-12：完成本機 setup/start/stop 命令與停止安全證據後提出 M0 審閱；clean checkout rebuild 與另一操作者獨立 handoff 列為選配，不作 M0 門檻。BIND-10 獨立 restore／cutoff 亦為選配改善。

每階段失敗只停止受影響操作；保存非秘密reason與必要版本，不擅自換工具/資料庫/身份方案、不拓寬權限。候選/依賴source變動時將受影響證據標STALE並重跑，不能把上個commit的PASS移到新候選。

## 3. 十二情境的實作來源／核心API追蹤

以下operationId皆來自目前canonical candidate；这里只列該案例必要入口，不宣稱M0須完成全部104或取代原驗收內容。harness/seed/native動作沒有HTTP時明示不新增endpoint。

| Case | Binding與CHK | API／輸入 | 證據的必要內容 |
| --- | --- | --- | --- |
| M0-AC01 | 01/02/03/11/12；CHK-01/08 | native setup/seed＋getSession | target DB/instance/Dataset/lock版本、跨Dataset拒絕、schema不符not-ready |
| M0-AC02 | 02/04/09/11；CHK-03/04 | getCsrf、createCart | 同key失回應只有一cart；Origin/token錯誤無commit，browser token不洩 |
| M0-AC03 | 02/03/09/11；CHK-01/02 | createQuote、createCheckout | 固定clock expiry，價/費/支援區/小數溢位拒絕，沒有部分保留 |
| M0-AC04 | 03/09/11；CHK-02 | createCheckout | 20 client/barrier與DB commit/movement，最後一件一承諾；多SKU一不足全rollback |
| M0-AC05 | 03/08/09/11；CHK-02/04 | createPaymentAttempt、getCheckoutStatus | 同source/operation dedup、Unknown不可重付、額外success独立補償 |
| M0-AC06 | 03/08/09/11；CHK-02/07 | cancelOwnOrder、getCheckoutStatus＋受控結果harness | deadline等號與晚到、kill前後transaction結果、consume/release各唯一 |
| M0-AC07 | 02/03/07/08/09/11；CHK-02/03 | executeProductRefund、executeShippingOnlyRefund、executePaymentCompensation、checkRefundOperation | source額度/單位/運費不同競爭、Unknown保留、撤權後Started由系統追原結果 |
| M0-AC08 | 04/05/06/07/09/11；CHK-03 | createSession | 後台角色與資源授權、錯誤身分與scope拒絕 |
| M0-AC09 | 04/07/09/11；CHK-03/04 | deleteSession、revokeAccessGrant | 舊session／proof拒；SSR切actor/返回上一頁无跨人HTML或秘密 |
| M0-AC10 | 03/08/09/11；CHK-02/03 | enqueueDemoScenario＋worker harness | claimed不當Started，同eventID異hash衝突，舊lease不commit，兩worker單次效果 |
| M0-AC11 | 01/04/11；CHK-04 | M0合成SSR page＋getCheckoutStatus | zh-TW/ja/en HTML/mock/noindex，不需完整M1 UI；無JS看必要摘要与拒絕結果 |
| M0-AC12（選配） | 01/11/12；選配CHK-08 | clean checkout locked restore/build、setup/seed與read-only核對harness | 僅採用選配改善後執行；固定合成資料集，不重設來源資料 |

樣本最少含成功、預期拒絕、故障/重送與未決結果；HTTP status無SQL/actor/source證據不能宣稱符合案例。M0-AC11使用fixture摘要，不因此要求先完成M1 Catalog endpoints。

## 4. 命令登錄与資料完整性

每條未來產品命令需command id、literal argv/工作目錄、實際entry source/hash、command effect（read_only/build/write_dataset/maintenance/destructive）、target instance/DB/Dataset、effective principal reference、前置checks/permit、成功退出與必要assertions、timeout/停止方式、失敗恢復。真正literal command目前null；setup/migration/seed/reset/restore不補成似真可執行腳本。

已知`infra/sql-server/manage.sh status/check`只屬既有isolated SQL實驗觀察，不綁為產品SQL permissions/交易/restore evidence；純規則實驗亦不算本M0產品測試。讀private ref不讀實際secret；證據只記object/principal reference和有效權限結果。

command registry的記錄本身不授執行權。start只在schema/身份/SQL/worker檢查通过後ready；shutdown先停止新派送并持久化未決，deadline/Unknown原樣保留；kill案例只針對隔離candidate process，不能全域停服務。

## 5. 證據與採用前清單

[空綁定模板](SOURCE_BINDING_TEMPLATE.yaml)逐binding/command/case保留source與結果空欄位，和[M4模板](../ACCEPTANCE_EVIDENCE_TEMPLATE.yaml)連接。結果分NOT_EXECUTED/PASS/FAIL/BLOCKED/STALE；本輪產品案例仍依狀態檔逐項判定，command/source不得由模板推定已填。BIND-10 為選配來源。預期拒絕符合assertion才PASS，不把工具報錯全部算FAIL或成功退出全部算PASS。文檔check只證明shape/來源，不寫入runtime結果。

執行證據先暫存去識別摘要；長期保存位置/期限仍待確認，不保存raw prompt/tool trace、password/recovery/token/connection string。候選SHA/hash、時間/環境、source version、結果与限制足夠重查；無法核對資料完整性時記UNKNOWN，不猜來源或補0。

開工前最小審閱包：DEC-01的core wire/error與新的 Dataset 重新驗證目的；DEC-02的/profile/secret/recovery細則；DEC-03的核心UX/品質目標。新角色、真服務、正式人員/費率/物流與Phase2不併入此批准。Gate1採用與Gate2首批實作範圍仍分開記錄；不因本輪要求規劃而推定批准。
