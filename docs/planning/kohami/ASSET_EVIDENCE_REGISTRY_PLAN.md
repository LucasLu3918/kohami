# Preset、BodyProfile 與私有素材證據登錄提案

2026-10-13｜PROPOSED／READY_FOR_REVIEW｜SOURCE_UNBOUND／產品 NOT_EXECUTED

適配性：有條件適合。不可變版本與私有證據索引能補足現有匯入契約的來源檢查；先以本機工作區和SQL索引設計，不新增雲端服務。沿用[ADR0001](../../adr/0001-preset-avatar-fitting.md)、[術語表](GLOSSARY.md)、[匯入計畫](ASSET_IMPORT_REVIEW_PLAN.md)與[工作流程契約](ASSET_WORKFLOW_API_INTEGRATION_PLAN.md)。本輪不提供真素材、契約、人員或合法權利證據；登錄工具、私有內容讀取權限、SQL物件與命令均待實作綁定及採用。

## 1. 資料分工與可信來源

| 記錄 | 最小內容／用途 | 不可據此推定 |
| --- | --- | --- |
| Preset／Avatar Family | Dataset、穩定系列識別、男女各A/B既定方向、公開介紹文案來源 | 四組已有素材或全尺寸可展示 |
| Avatar Size Variant | family、不可變variant版本與BodyProfile版本連結 | 與同名衣服尺碼自然相容 |
| Body Size Profile revision | 精確variant、身高/圍度參考、單位、量測方法/標記位置、來源證據版本、量測規格版本 | 會員身體資料或真人合身判定 |
| Evidence revision | Dataset、類型、原檔SHA-256、byte數、受控物件id、來源方式、上一版、登錄操作者/時間 | hash正確即合法、非AI宣告即已證實 |
| Review decision | 精確被審版本與hash集合、checklist版、逐項Pass/Fail/Unknown、人工責任與時間、原因 | 其他版本／variant／SKU亦通過 |
| Dependency link | evidence→profile/asset/rights/review/compatibility的精確版本連結 | 只憑索引即可放行內容 |

Source、rights、quality、measurement、alignment及review分別記錄；來源證據不能替代授權範圍，權利成立不能替代品質。記錄ID由登錄工具建立，引用時重新解析同Dataset與用途，不從檔名、網址或未知字串建立「可信」記錄。首次建立variant/profile可先Draft，無須尚不存在的Asset；Asset接受再引用它，避免循環依賴。素材未完成前不得將該variant公告為可換裝。

BodyProfile是模特參考資料，不能連MemberId、會員照片或推算會員健康/身體；新量測或variant連結另建revision。尺寸名稱不作唯一鍵，公開數值的單位與測量位置須可解釋；實際數值、支援尺寸與容差維持DEC-07待證據決定。翻譯改動另有內容版本，不覆寫已審核數值。

## 2. 私有工作區與內容門禁

候選布局為作業系統使用者私有資料目錄下的 `Kohami/evidence/<dataset-opaque-id>/`；絕對根路徑由本機配置，不出現在HTTP payload。分 `quarantine/`、不可變 `objects/`、最小 `index/` 與受控 `backup/`。根路徑、OS ACL、加密key reference與獨立備份位置尚UNBOUND；不建立資料夾或自動採用本輪路徑。

原製作檔及權利文件只在native工作台讀取，與網站交付PNG儲存、webroot、Nuxt public、Git與一般log分開。網站API僅取得經允許的opaque ref、版本與審核狀態；現有fitting.read/manage或Owner角色不授權讀原契約／作者資料。native工作台需另行採用精確Dataset/action的操作權限與OS操作者確認，沿用[原生操作邊界](OWNER_BOOTSTRAP_RECOVERY_PLAN.md)，不得借用bootstrap/recovery permit或自行新增grant。這是待採用功能邊界，不是已可用命令。

原檔採封存與受控查看：不在server或browser執行巨集、script、製作檔或任意外部連結；外部作者網址僅作私有來源記錄，不由API抓取。人工取得檔案後經受控匯入，server自行算hash與byte數。必要閱覽副本與原檔分開hash，副本需回溯原件；若無法安全閱覽則保留Unknown，不為了完成審核自動開檔或轉換。

檔案解析拒絕任意絕對路徑、parent traversal、跨Dataset及逃出受控根的symlink；物件id解析至已核對受控檔案，讀取時再次驗hash，避免驗完再被替換。文件類型、容量與viewer隔離allowlist未綁定前拒絕真匯入，不沿用PNG上限當文件安全證據。下載/複製私有證據亦需明確權限與稽核；日誌只記opaque id/action/result，不記契約文字、人物姓名或實際filesystem path。

## 3. 登錄、版本及操作契約

以下為native工具語意，**不是現有HTTP或可執行CLI**；canonical仍140操作，不新增未審核endpoint。

| 操作意圖 | 輸入與前置 | 結果／並行裁決 |
| --- | --- | --- |
| RegisterEvidence | 已確認Dataset/action、受控staging物件、用途、來源宣告；工具計算實物hash | 封存不可變Evidence revision，初始Pending；不自動確認rights |
| RegisterPresetProfile | 精確family/variant/profile與measurement evidence版；allowlist配置 | Draft registry revision；審核完才SelectableForImport |
| RecordReview | 目標版本/hash集合、checklist版、逐項結果、可定位證據 | 不可變Review；任一Unknown/Fail拒對應批准，記錄人員不代表已指定實際人員 |
| SupersedeEvidence/Profile | predecessor精確版本、目前ETag、變更原因 | 建新revision及新審核候選；不覆舊bytes／原收藏，不把最新版本自動套舊依賴 |
| InvalidateEvidence | 精確evidence版、目前版本、失效原因、已確認操作範圍 | 同交易記失效與dependency hold；啟動受影響功能核對，保留歷史 |
| ResolvePrivateReference | caller用途、Dataset、精確revision、目前權限 | 僅回該caller需要的metadata或受控native內容；未知／不符拒絕 |

每項寫操作有operationId、業務Idempotency-Key與實際入參指紋；同key不同hash/用途/Dataset拒絕，同key同內容失回應取原結果，不延長期限或重授權。先驗目前權限再replay；原生單次permit與business idem分開，permit消耗/回應遺失的continuation需由helper綁定原operation，不能重用已用permit發新操作。更新用registry aggregate ETag，若舊版拒絕，不能用Asset/Import Session版本代替。

物件先私有stage、驗實際hash，再SQL同交易建立索引、依賴、Audit及必要Outbox；索引失敗只留未引用orphan，不能稱登錄成功。失回應查唯一(Dataset,operation)結果。清理先核引用，不能刪已commit或review依赖物件；SQL/runtime service只讀最小索引，原檔讀取由另受控broker，不給API帳號原件目錄權限。具體procedure與broker來源待[SQL權限計畫](SQL_LEAST_PRIVILEGE_PLAN.md)及M0/Phase2實證。

## 4. 既有API的解析與審核時點

1. setup-context的presetReferences只列同Dataset、允許匯入且來源可定位的variant/profile配對；該列表不是公開展示批准。未綁registry或政策則沿現行policyConfigured=false與空候選，不填假支援尺寸。暫不擴充既有PresetReference wire形狀，其他狀態留在server registry。
2. CreateImport核對pair与policyVersion；Complete固定三slot、manifest與所有來源版本；Validated只表示技術與參照檢查，人工來源/權利仍未通過。Accept在同交易重新核registry現況、實物hash及依賴版本，只建Draft Asset。Receiving後profile/evidence失效不得靠舊setup快照接受。
3. Rights Pending的evidenceReferences只解析Rights類證據及精確Asset涵蓋範圍；ConfirmActive重新核對當下證據、人工決議、display/transform/download範圍與區間。存在的ID、Pending記錄或同人點選不能替代逐項審查。
4. Alignment／Compatibility綁profile版、Avatar/Garment/Alignment版與review hash集合；Approve另核目前來源及三視圖品質。舊profile合法版本仍可供原Saved Outfit追溯；新profile不静默替換原版。
5. 展示／匯出建立、render commit及最後交付均重查current rights與dependency hold。訂單／庫存／商品可購買性獨立；素材問題走既有fallback，不改交易快照。

證據狀態提案：Pending→Verified或Rejected；Verified可Invalidated。補正另建revision，Rejected/Invalidated不重活。Verified指指定證據項已人工核對，不等於Asset Approved或Rights Active。Superseded只標新版存在，舊版若仍合法且檔案有效可支援原收藏；若發現舊版不可信或權利撤銷，另Invalidated並套用門禁，不能以「只是更新」略過風險。

## 5. 失效、反向依賴與恢復

失效先在權威registry交易設定evidence狀態與單調registryEpoch／受影響dependency hold。網站gate於同Dataset讀目前epoch/hold；權威不可讀即拒交付，不等worker fanout才停。若依賴列表過大或不完整，先停止該Dataset換裝交付，維持正常購物；不能僅修改一個Asset留其他引用放行。

失效分類與裁決：Rights失效停止對應display/transform/download並交接既有Asset rights撤銷；品質/量測失效停止受影響換裝並交接既有Suspension；來源未知先hold待查，不能推定已構成永久權利撤銷。保留reason分類和Review，不把Invalidated直接改成所有資源同一終態。既有事件schema無registry失效事件；若需新事件須另作CAN-10 registry/consumer採用，本輪不直接送unknown event。

consumer先查current state與epoch，舊事件/舊審核不清hold；修復須新合法證據/必要新profile或Asset revision及精確人工審查，再個別解hold，仍遵守Revoked export不可復活。更新不改Saved Outfit原revision，無法重現時顯示失效與明確替代另存選項。

備份需一致索引、依賴、原件hash清單及分離加密key dependencies；復原先停交付/dispatch，核對原件與索引，重套backup cutoff後失效/撤銷/刪除ledger及新epoch。缺原件、解密key或較新ledger時保持hold；不能只restore SQL就認定證據完整。私有契約保留期限與刪除條件待實際來源和用途確認；當前不設自動刪除數字。維持最小必要引用及稽核，刪除需檢查合法保存需求與受影響交付，不以一般cache TTL清原件。

## 6. 產品驗收規劃（全部NOT_EXECUTED）

| ID | 判準 |
| --- | --- |
| REG-AC01 | 未知／跨Dataset／錯類型evidence ref拒絕；native內容讀取不能靠網站Owner/fitting.read放行 |
| REG-AC02 | hash不符、受控根逃逸或未配置viewer拒真匯入；來源宣告不升Verified |
| REG-AC03 | variant/profile可先Draft無循環；setup僅列合格匯入pair，不公告假支援尺寸 |
| REG-AC04 | BodyProfile新量測建新版本；不連會員個資，不以尺碼名稱批准相容性 |
| REG-AC05 | 同operation失回應只有一revision；不同hash重送拒；stage/SQL crash無假登錄成功 |
| REG-AC06 | review綁實際三圖／profile／加工hash；Unknown/Fail拒批准，換版不能沿舊Pass |
| REG-AC07 | setup後證據失效時Accept/Confirm/Approve重新核對並拒放行 |
| REG-AC08 | 權利失效與品質失效分原因；dependency hold先於fanout，集合未知先停換裝 |
| REG-AC09 | Superseded仍可合法重現舊收藏；Invalidated阻交付，新證據不復活terminal export |
| REG-AC10 | 舊事件/失讀/缺ledger不清hold；restore核原件/key/索引與cutoff後失效 |
| REG-AC11 | 公開DTO/log無原件路徑/契約文字/作者個資；API runtime無私有原件讀取權 |
| REG-AC12 | 清理只刪無引用orphan；原件保留未決不自動刪除，恢復有實際hash證據 |

[空登錄模板](ASSET_EVIDENCE_REGISTRY_TEMPLATE.yaml)只記欄位、planned consumer與驗收引用；沒有實際證據或產品執行結果。後續待採用private workbench權限、真來源／量測／允許用途（DEC-04/07）、bind helper/viewer/SQL/root/key與pilot，不能把本文或schema當合法素材證明。
