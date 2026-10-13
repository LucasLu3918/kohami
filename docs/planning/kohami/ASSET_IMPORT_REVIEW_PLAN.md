# 素材匯入、審核與事件交付提案

2026-10-13｜PROPOSED／READY_FOR_REVIEW｜產品 NOT_EXECUTED

適配性：有條件適合。固定非AI三視圖可用本機隔離匯入與不可變revision管理；沒有來源／權利／審核證據時，仍可完成規劃但不得發布。沿用[ADR0001](../../adr/0001-preset-avatar-fitting.md)、[換裝契約](FITTING_API_INTEGRATION_PLAN.md)與票27/28/31；不取得或製作真素材，不新增本人照片上傳，不采购／付費／啟用產品。

## 1. 邊界與角色

首版只規劃具來源的固定角色／服裝三視圖PNG；原作者製作檔保留於受控證據工作區，網站不接受SVG/HTML/ZIP/任意URL或filesystem path。來源必須非AI、不是會員本人照片或會員身體檔案。素材製作者、匯入者、品質審核與權利確認是責任角色，实际人員仍未指定；不假設已有備援或供應商。

匯入管理沿用未採用overlay的Owner＋fitting.manage＋近期TOTP（Dataset精確id）；取得AssetRevision後其審核動作用原精確revision proof。發布仍asset.publish、撤銷仍asset.revoke，不由匯入成功或catalog.write放行。權利確認與品質審查必須記錄證據，首版本機不加「必須不同兩人」政策，也不免除人工審查；若同人擔任多角色仍分開記錄結果。權限/bootstrap/purpose未canonical整合前阻擋管理執行。

## 2. Import Session 與隔離儲存

候選流程：建立Import Session → 指定三個server slot → 私有暫存 → Complete → 技術驗證 → Validated／Rejected → 接受為Draft AssetRevision。Import Session本身不具有公開展示權，也不等於既有AssetRevision Draft/InReview/Approved狀態機。

| 狀態／動作 | 必要條件與結果 |
| --- | --- |
| Receiving | owner/Dataset綁定、manifest schema版本與三slot；Session有期限與總容量限制（工程候選），未公開 |
| Complete → Validating | 三slot bytes接收完成，server算原始hash／大小／實際decode尺寸，不採信client宣告 |
| Validated | 有界PNG檢查、正面/右側/背面對齊／對應內容、來源參照與權利記錄可定位；人工品質仍未批准 |
| Rejected | 缺圖、格式／容量／pixel／hash不符、未知來源等reason；無公開asset，修正另開session，不覆寫hash |
| Accepted | owner明確確認；建立唯一Draft revision，指向不可變safe物件、來源hash與加工規格；原權利仍Pending或未達發布条件 |
| Expired／Aborted | 拒新寫入；私有垃圾清理可重試；稽核保留最小操作／原因，不保留不必要失敗檔案 |

JSON [Manifest schema](FITTING_IMPORT_MANIFEST.schema.json)只描述session／識別／三視圖slot／來源與宣告資料，不接受任意client路徑；slot id由server簽發且屬原session/Dataset。PNG signature、Content-Type、decode、檔案／pixel上限均須一起核對，Content-Type不可單獨當安全證據。server產生儲存名稱、隔離於webroot外，權限與CSRF不能省略；這些是依OWASP原則的本產品設計。[OWASP File Upload](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html)（核對2026-10-13）。

容量工程候選：每PNG≤10MiB、整組≤30MiB、每張最多4096×4096／16,777,216 pixels、同actor receiving最多1組、session30分鐘；不是已採用服務規格。須在受限worker執行decode，限制解壓／記憶體／CPU／時間，拒絕多幀／額外可執行內容與非allowlistchunk；具體decoder／隔離設定尚未選定與驗證，不因schema通過稱安全PNG。

原始檔hash與重新編碼交付hash分開；decoder重編碼去metadata後作新不可變payload，保存加工規格版本、色彩空間與輸出尺寸記錄。加工改色、比例、透明邊緣、alpha與遮擋需重新人工檢查，不以「安全重編碼」假定外觀相同。網站只交付審核通過的server產物；不將原始staging bytes公開。

物件儲存與SQL不是同一交易：先私有stage／驗hash，再同SQL交易建立Draft revision＋引用／Audit／Outbox，未被SQL引用的物件列orphan待清理。回應遺失以同Import Session＋Idempotency-Key重取原Draft，不再建立revision；唯一(dataset,importSession,acceptance)約束。中途重啟只查實際slot/hash與既有commit，不能默認成功；撤權在acceptance前重新核對。清理與restore需避開已commit引用，不以「未公開」就刪有效Draft。

## 3. 品質与權利審查

審查固定某revision／payload hash／對齊參數／Body Size Profile與SKU；manifest或圖變動使審查失效，改建新revision。來源與right证据由授權人員於私有工作區讀取，公開API只回最小允許用途，不公開契約或作者個資。

| 審查面向 | 必要記錄／拒絕條件 |
| --- | --- |
| 来源 | 非AI製作方式、作者／來源證據ref、加工權利；缺少或無法核對不發布 |
| 權利 | display/transform/download各別允許、有效起迄、範圍與撤銷渠道；未知或不足則對應功能停用 |
| 角色 | 四組識別／該variant/profile、三視圖姿勢／比例／量測、適當Base Garment一致；不推斷會員特徵 |
| SKU | 正確顏色／服裝尺寸、袖子／衣長／材質外觀、背面來自證據而非猜測；不以同尺碼名稱相容 |
| 對齊 | 三視圖遮擋／輪廓／alpha、變形／穿幫／重大錯位；至少一組真SKU驗收 |
| 匯出 | 複合圖的transform/download權利與品牌模特聲明；標示缺失不能Ready |

Review decision包含revision/hash、checklist版本、每項Pass/Fail/Unknown、證據ref、角色責任、人員識別／時間與原因。Unknown不能折算Pass；Applied範圍只此variant×SKU×三視圖，不外推整個商品或四組/所有尺寸。正式法定權利判斷需對應實際契約確認，本提案與JSON不是法律意見或授權證明。

## 4. 相容性建置與API交接

合格Avatar与Garment revisions＋BodyProfile＋Alignment revision才組成Compatibility；每引用皆同Dataset或明确allowlisted共享素材範圍。先Draft再逐組人工Approve，發布時同交易查目前素材/rights/version与三視圖；素材Approved不代表每一組合Approved。

候選入口（本輪語意輪廓，未加入124個OpenAPI操作）：POST `/admin/fitting/imports`建立session；GET `.../{id}`查slot／etag；PUT `.../{id}/slots/{view}`傳PNG；POST `.../{id}/completions`封存；POST `.../{id}/acceptances`建Draft revision。新Compatibility／alignment/rights evidence的建立入口亦需後續canonical展開，現行20-operation契約仅管理已存在素材，不能稱全素材管理API完成。

建立／Complete／Accept需CSRF/Origin/Idempotency；slotPUT綁同session、view與hash，重送相同bytes同結果，不延session；不同bytes409且不覆已接收slot。versioned动作If-Match與expectedVersion同session aggregate，GET來源；Receiving受限於容量/expiry。Rejected／Expired不允許舊session補檔「救回」；重開新session。Accept明確命名是建立Draft，不是Approve，不發展示能力。

## 5. 事件契約與消費者

[Event schema](FITTING_EVENT_CONTRACT.schema.json)是本機outbox資料提案，非HTTP、公開topic或新訊息服務；維持SQL＋本機worker，不引入雲端broker。每event有dataset、eventId、sourceOperationId、schemaVersion、aggregateKind/id、單调aggregateVersion、dataset內單调rightsEpoch、occurredAt與最小payload。rowversion ETag不當可排序版本，rightsEpoch與aggregateVersion獨立業務序號。

| eventType／payload | 權威來源與消費者 |
| --- | --- |
| AssetRightsRevoked | AssetRevision／rights link transaction；resource gate、export取消、原收藏projection、私有檔案清理、restore ledger |
| FittingSuspended | Dataset全站或Compatibility；gate／fallback／export停止，商品Stock/Order不變 |
| AssetRevisionRetired | AssetRevision；停止新選擇，既有合法原版本仍可重現，不取消合法下載 |
| SavedOutfitDeleted | SavedOutfit；清該筆private projection／關聯exports，原transaction已釋放quota，consumer不能再减 |
| AssetRevisionDraftCreated | Import accept；審查候選索引更新；不允許公開顯示、不增加Compatibility |
| FittingResumed | Dataset或Compatibility；收到通知重新查目前rights/審核，不能按事件直接恢复bytes |

不在event放MemberId/Email/身體選擇/URL/token/原始PNG/契約內容，SavedOutfit用最小outfit id限定內部消費者，projection要在授權API讀取。producer不可由前台自填；同SQL交易提交业务、Audit與Outbox，維持eventId与deliveryAttempt分開，重送同event。Outbox可靠交付仍可能重複，需要幂等consumer；SQL方案是本產品推導，不是Microsoft範例的已部署保證。[Microsoft Transactional Outbox](https://learn.microsoft.com/en-us/power-platform/architecture/reference-architectures/transactional-outbox-pattern)（核對2026-10-13）。

## 6. 重複、亂序、清理與復原

1. Consumer先驗schema／來源／Dataset隔離；unknown schemaVersion或type進隔離pending調查，不用默認忽略。Inbox唯一(dataset,consumer,eventId)；只有影響同交易完成才標processed，不能先ack再作業。
2. event version只決定事件新舊；每次查当前SQL aggregate与rightsEpoch，舊事件不覆新狀態。處理尚未見到的新version或不同hash的同eventId列衝突，不偷偷標成功。該Dataset權威state不可讀時，交付閘道fail closed。
3. 清理是外部檔案side effect，不和DB宣稱exactly-once；鎖定対象operation/revision、重驗目前引用，幂等刪除指定私有物件，完成後記cleanup結果。刪除失敗保留Pending/Failed重試，門禁仍先拒新交付。不可因舊Suspend清理事件把後續合法重新產生的新version blob刪掉。Suspended清理只限preview/cache/export暫存，不自動刪除仍被revision引用的不可變原始／交付素材；永久刪除另依確認的保留／撤銷處置，避免Resume時無檔可用。
4. Retired不等於Revoked：僅排除新resolve／新增收藏，原收藏/其合法export可重現。Suspended停止；Resumed不復活舊已Revoked export，使用者仍需明確建立新工作。
5. restore使用新的worker dispatch epoch，所有舊Ready/Queued/Rendering export先失效；重套backup cutoff後撤銷／停用／已刪收藏與身份版本。共享rights記錄撤銷時，所有linked revisions門禁與outbox fanout須同一權威交易更新，不能只通知某一張圖。較新ledger缺失就阻擋gate啟用；Schema/example通過不能提供復原證據。

consumer lag、retry count、quarantine count、cleanup state、最大rightsEpoch差異是候選營運指標，log只操作id／reason／時間不記素材／secret。具体重試上限與backoff、worker lease/timeout需後續機器化，不把本輪文件當工作排程已實作。

## 7. 验收（產品全部NOT_EXECUTED）

| ID | 可觀測情境／判準 |
| --- | --- |
| IMP-AC01 | 僅合法Owner/scope/近期purpose可建session，catalog.write或舊grant不可匯入 |
| IMP-AC02 | 三slot缺任一、重複view、URL/path、任意HTML/ZIP、hash/decoded尺寸不符拒絕 |
| IMP-AC03 | malicious/huge PNG在decode隔離/有界限制拒，原staging不經公開URL取得 |
| IMP-AC04 | 接收中重啟/回應遺失同slot/hash同結果；不同bytes不覆寫，expiry不展延 |
| IMP-AC05 | accept失回應同key僅一Draft，撤權或變版先拒；SQL失敗無公開asset，orphan受控清理 |
| IMP-AC06 | 未知權利／品質Unknown／變更payload不發布；Approve只此組合；清楚人工證據 |
| EVT-AC01 | producer業務/outbox同交易，consumer inbox重複不雙重清理／quota／產圖 |
| EVT-AC02 | Revoked後晚到Retired/Resumed不能恢復bytes，新舊version與目前state裁決 |
| EVT-AC03 | 跨Dataset、未知schema/type或同eventId不同內容隔離；gate不可讀時拒絕 |
| EVT-AC04 | cleanup失敗仍拒交付；老事件不刪新version合法blob；重啟可追同source |
| EVT-AC05 | Retired保留合法舊收藏，Suspended阻止展示，復原不復活舊export |
| EVT-AC06 | 事件無PII/private configuration／URL，授權consumer最小資料且restore ledger完整 |

本輪check只驗Manifest/Event shape、合成例与負向案例及空證據模板。PNG decoder、人工授權、真素材、SQL／outbox race、worker/檔案清理與復原全部尚未執行。


## HTTP契約接續與建立狀態限制

[API_ASSET_WORKFLOW](API_ASSET_WORKFLOW.openapi.yaml)及[整合詳規](ASSET_WORKFLOW_API_INTEGRATION_PLAN.md)展開16操作：setup/Import三槽／Complete/Accept/Abort、Alignment候選、Asset rights記錄與Compatibility建立。Slot與Session分版，Alignment先無GarmentRevision避免循環，新接受僅Draft、新權利僅Pending。原5OpenAPI、Manifest/Event與identity overlay不改；新event三变体須canonical採用才發送，產品NOT_EXECUTED。


## 私有來源與Preset登錄接續

[證據登錄詳規](ASSET_EVIDENCE_REGISTRY_PLAN.md)補齊source/rights refs解析、不可變BodyProfile版、逐項Review及失效hold。setup候選不是展示批准，Accept前重核實物hash與registry；網站權限不授予讀原契約。工具來源與真證據仍UNBOUND，既有Manifest/Event不變。
