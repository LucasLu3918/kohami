# 素材建置 HTTP 契約與跨資源整合提案

2026-10-13｜PROPOSED／READY_FOR_REVIEW｜產品 NOT_EXECUTED

[API_ASSET_WORKFLOW](../contracts/API_ASSET_WORKFLOW.openapi.yaml)補16操作／25schemas；核心商務76＋帳號19＋換裝20＋素材16，共131個跨文件唯一method/path與operationId；登入強化不納入核心候選，Foundation排除重複。五份原OpenAPI、Manifest/Event schema與身份overlay雜湊保留。Schema名字各檔獨立命名空間，不直接合併同名Problem／AdminAsset；不是canonical全產品API或已執行功能。

適配性：有條件適合。使用既定.NET/Nuxt/SQL／本機worker與固定素材範圍，不增加AI、外部broker或公共上傳。參照[匯入計畫](ASSET_IMPORT_REVIEW_PLAN.md)、[素材契約](FITTING_API_INTEGRATION_PLAN.md)及[未啟用身份overlay](FITTING_IDENTITY_OVERLAY.yaml)；容量／時間／座標邊界均工程提案，素材品質／權利與正式啟用仍需人工採用及實證。

## 1. 可重現建置順序與循環依賴

1. Admin setup-context回server選定Dataset、目前政策與已登錄的Preset/BodyProfile refs。未配置政策回false＋null，寫入409 IMPORT_POLICY_NOT_CONFIGURED；不提供假支援尺寸。Preset/BodyProfile登錄與來源證據工作區未建置時阻擋真素材接受，不能用client任意id代替。
2. Avatar先按獨立三視圖匯入、技術驗證後接受為Draft，再人工審核／權利確認。角色尺寸與SKU仍獨立，不從會員身體收資料。
3. 建立Alignment候選：綁Avatar revision、variant/profile與SkuId，記NORMALIZED_CANVAS_V1座標與三視圖scale/offset、原始幾何證據；**不要求尚不存在的Garment revision**。候選參數不可修改，要更正就新版本；它不是Approved相容性或合身結論。
4. Garment匯入使用既有alignment候選id；接受後建立自己的Draft revision，確認技術文件與來源／權利。既有Manifest的alignment欄位不改。
5. Compatibility建置綁定兩個Asset revisions、profile/SKU与Alignment候選，三份expectedVersion与資源身份同交易核對後只建Draft。再走原GET compatibility取得ETag與Approve，人工核實实际三圖與加工規格。

座標提案：原點為素材画布左上角，offsetX/Y是相對原画布寬/高的比例，scaleX/Y各自為正比例；先縮放再平移，以同一view set加工規格畫布為基準，保持固定三視圖。範圍scale≤10、offset±1仅工程候選，不認定目前素材皆可用或需非等比例拉伸。指定decoder輸出色彩／alpha／畫布规格、BodyProfile允許registry、參考證據匯入工具與hash canonicalization規格尚需綁定實作；不默默採用瀏覽器預設或推算顧客尺寸。

## 2. 權限與資源範圍

所有角色的操作都須有效permission與精確scope。讀取fitting.read，建立／匯入fitting.manage，權利生效asset.publish；原scope enum仍未啟用新值。資源ID由server查所屬Dataset，不能由request body指定Member／另一Dataset規避。

| 操作／scope | 有效grant |
| --- | --- |
| setup、Import Session及slots、Alignment讀取 | fitting.read＋Dataset；僅grant某個AssetRevision不提供整站素材建置索引 |
| 新Import/slotPUT/Complete/Accept/Abort、新Alignment、新Compatibility | fitting.manage＋精確Dataset |
| parent Asset的Rights list/detail | fitting.read＋AssetRevision或同Dataset覆蓋 |
| 新Rights Pending／RejectPending | fitting.manage＋精確parent AssetRevision |
| ConfirmActive | asset.publish＋精確parent AssetRevision與人工證據 |

沒有新增Alignment/ImportSession/RightsRecord grant kind；以Dataset或parent Asset管理，避免未採用新權限的隱性擴張。讀Rights child必須驗rightsId屬path parent，跨parent／Dataset一致404。建立/讀取範圍與原20操作scoped Asset讀取不同，不能因知道Import id就跨Dataset讀。Owner角色不取代有效權限，grant不允自授。

## 3. Session與Slot版本、並行與封存

| 動作 | If-Match與expected來源 | 結果／競爭裁決 |
| --- | --- | --- |
| createImport | 無尚不存在的Session版本；expectedPolicyVersion是setup提供政策版 | server核當前配置／容量／registry，201 Session／三個Empty slots；30分鐘提案expiry不因重送延長 |
| slot GET/PUT | If-Match为Slot.version，不是Session.version | 同一slot不同bytes不覆寫；不同views可并行。PUT成功slot與parent版本一起增加，回Slot ETag |
| Complete | GET Import Session最新ETag＋body expectedSessionVersion同来源 | 鎖定Receiving及三slot已commit的hash/大小／manifest身份，封存→Validating202；舊父版412、仍缺圖409 |
| Accept | Validated Session ETag＋expectedSessionVersion | 唯一(dataset,session) acceptance，201新Draft Asset，回Location/ETag為新Asset，不冒充Session版 |
| Abort | Session ETag＋expectedSessionVersion | Receiving可取消；Validating若已Started或無法证明未Started則409，不盲目刪worker處理中的檔案 |

PUT先進私有request物件，不覆原slot path；處理前及commit前都核Parent Receiving/expiry、current permission与slot If-Match。commit時鎖Parent/Slot並保存來源hash、檔案ref與新Session版，Complete競爭不能封存未commit bytes。若父已封存／過期／撤權，丟棄此次未引用staging物件，不回寫旧version。

X-Source-SHA256只是client聲稱，server必須重新算每次實際body；key指紋包含實際hash與slot/view/version/route。即使重送宣稱hash相同，實際body不同仍拒絕，不以header字串跳過驗證。串流byte/decode/pixel有界，OpenAPI binary maxLength不是防壓縮炸彈保證；原始hash與安全交付hash分開。

Slot PUT、Complete與Accept以同key／同body失回應重送取原操作，先驗目前權限／proof／expiry，不再派decode或建revision；業務replay成功後才處理原If-Match，不能因原成功升版而把失回應重送一律412。另key同slot不覆已接收bytes；同Session另key接受已存在Draft回409及同scope既有Asset識別，不建立第二筆。

Worker結果綁封存Session version、三slot集合hash與lease/fencing token；只有当前Validating且相同epoch才可commit Validated/Rejected。過期／Abort／restore／錯epoch結果不復活舊Session。處理Started後仍追結果／清理，不假稱即刻停止。Accept需要目前Validated、未過期、實物hash存在与權限；SQL失败不得公開未引用物件，orphan清理不碰已commit引用。

## 4. 權利記錄、有效指標與素材版本

RightsRecord屬精確Asset，首版不新增跨Asset共享record/link HTTP API；若將來共享，沿既有全linked revisions原子撤銷規則另展開。每次建立保存display/transform/download宣告、有限UTC有效起迄、私有證據ref與原因，只為Pending/selectedForDelivery=false；證據ref字串或UI點擊不構成法定授權證明。

ConfirmActive只允Pending，有人工審查证据、validFrom<validUntil且validFrom≤now<validUntil、所需來源与引用可定位；用asset.publish權限、parent ETag与expectedAssetVersion。同交易選為唯一current delivery record、舊record取消selected，記Audit、提高parent Asset/rightsEpoch。舊有效記錄不被改內容，歷史保留；取目前pointer不從最寬鬆或最晚日期record任意選權利。

Active可自然到期Expired或以原Asset RevokeRights撤銷至Revoked，selected隨之失效；Rejected/Expired/Revoked不能Confirm重活。若取得新合法證據，另建Pending record并重審；新record生效也不把原Revoked export復活或把Asset Draft/Suspended自動改Approved。權利成立、技術品質Approved與Compatibility Approved仍三個不同門檻；display/transform/download要按實際用途逐項驗。

Rights collection/detail回應ETag均為**parent Asset＋rights aggregate**，recordVersion只追溯；與原GET AdminAsset之version一致，body expectedAssetVersion同来源。新canonical需要在AdminAsset projection增加effectiveRightsRecordId與delivery指標規格，原baseline形狀本輪不修改。所有舊export、saved original與公開素材每次交付都查目前pointer/interval/epoch；權利替換不重寫Saved Outfit的原Asset revisions。

## 5. Compatibility唯一性與依賴裁決

Alignment候選read ETag作expectedAlignmentVersion；Avatar/Garment用各AdminAsset ETag。建立時鎖／比較三refs，驗profile/variant/SKU/加工畫布一致、可定位来源與三視圖完整；版本競爭409 DEPENDENCY_VERSION_CONFLICT，header/body本aggregate錯版則原412/422規則。依賴Version指實際aggregate revision，不能前台填目前時間或無關ETag。

唯一鍵(dataset,variant,bodyProfileRevision,sku,avatarRevision,garmentRevision,alignmentRevision)。同key/body回同Draft，不同key重建相同組合409並給該scope既有id；不用強制複製新id掩蓋重複。初建selection預設Front僅記錄管理candidate，顧客仍明選view／BodyProfile，不能自動加車或隱含角色尺寸。

Approve沿原Compatibility操作，依當時重新核實素材／權利／Alignment與審核證據；建Draft不是可展示。引用條件不合可保持Draft供更正建新candidate，不隱藏Failure；immutable Alignment不PUT修改、不支持任意script/URL/矩陣程式輸入。

## 6. 事件、consumer與canonical差異

原六種Event schema不改；新OpenAPI的WorkflowEvent提出AlignmentCandidateCreated／CompatibilityDraftCreated／AssetRightsConfirmed三个最小變体，producer业务/Audit/outbox同transaction，eventId/deliveryAttempt分開、current-state consumer裁決。這些新type/kind在原schema會被拒／隔離，必須canonical registry与consumer allowlist共同採用，不能把OpenAPI component存在當允許立即發送。

| 來源／consumer | 整合必要工作 |
| --- | --- |
| 原Manifest | 本地deep copy保持來源欄位；三slot實際hash/decoded/source checks不是JSON schema所證明 |
| original Fitting20 | AdminAsset之有效rights pointer、Alignment/Compatibility references序列化／畫布規格與讀寫stage统一 |
| 原身份流程 | Dataset建立／Asset權利精准proof及effective grants需先採用overlay；不增加角色旁路 |
| Gate/export/outfit | 逐次查最新rights而非歷史寬鬆record；新rights不得恢復terminal export；cart/Order獨立 |
| Event registry/outbox | 合併原6＋新3变体、kind/版本/處理器，拒未知；new events不自動啟動render/publish |
| restore/cleanup | dispatch epoch、后cutoff撤銷/rights selection/已接受Session ledger与檔案hash核對；缺來源先阻擋 |

全部新入參不收MemberId、私人照片、任意URL/path、完整權利契約文字；證據管理接口、PNG decoder/library與隔離命令、原始/交付hash canonicalization、數值政策和實證仍需source綁定。這些是實作／實證前置，不據schema已通過宣稱系統建成。

## 7. 補充產品驗收（全部NOT_EXECUTED）

| ID | 外部可驗證判準 |
| --- | --- |
| AAPI-AC01 | 未配置setup/未知preset拒建，Dataset有權才可新增；Asset限scope不能讀全Dataset |
| AAPI-AC02 | 對齊candidate先於Garment匯入，三view/座標不可變；新params新revision，不假稱Approved |
| AAPI-AC03 | 三slot不同view並行成功，同view不同bytes僅一commit，parent封存競爭無覆寫 |
| AAPI-AC04 | client hash與bytes不符拒絕；所有actual bytes/decode limits及staging不公開 |
| AAPI-AC05 | Complete失回應同key同job，worker舊fencing/expiry/Abort不復活，Validating不當成功 |
| AAPI-AC06 | Accept唯一Draft与source/delivery hash；另key重複不建第二筆，失回應正確Location/版本 |
| AAPI-AC07 | Rights parent/record/version錯用拒；Pending不授權；精確asset.publish／人工證據才能生效 |
| AAPI-AC08 | Rights有效区間/selection唯一／終態不可重活，新record不改原Saved Outfit與terminal export |
| AAPI-AC09 | Compatibility refs/body/SKU与3版本同交易核，重複組合不複製，Approve另完整審查 |
| AAPI-AC10 | 新事件未採用拒／隔離，重複或亂序不Publish／復活，restore維持門禁至ledger完整 |

scripts/check_kohami_asset_api_contract.py只核OpenAPI、合成正負shape、來源hash／身分scope標記／133個現行canonical操作與本地複製；不產PNG、不執行SQL，也不建立法律權利與人工批准。


## Registry與私有證據來源接續

[登錄詳規](ASSET_EVIDENCE_REGISTRY_PLAN.md)定義既有setup-context/Import/Rights/Alignment/Compatibility對registry的解析時點與拒絕出口；private native內容權限另待採用。現行canonical含133個HTTP操作，沒有新增registry HTTP或runtime事件；REG-AC01–12均NOT_EXECUTED。
