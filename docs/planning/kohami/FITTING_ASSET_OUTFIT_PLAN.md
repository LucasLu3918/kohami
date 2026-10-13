# 固定模特素材、收藏與匯出詳規提案

日期：2026-10-13｜READY_FOR_REVIEW／PROPOSED｜產品驗證 NOT_EXECUTED

依據：[ADR-0001](../../adr/0001-preset-avatar-fitting.md)、[術語](GLOSSARY.md)、[規格](../../../.scratch/kohami/spec.md)、票27–37及[身份整合](IDENTITY_API_INTEGRATION_PLAN.md)。本文件補齊 Phase 2 的規劃，不擴張 M0–M4 核心交付、不解除票券依賴、不代表素材採購／製作／啟用批准。既有四份 OpenAPI 保留；以下是 API 輪廓，尚不是 canonical 機器契約。

## 1. 適配性與決策邊界

固定非 AI 三視圖及版號可追溯收藏符合已接受 ADR，適合繼續規劃。資產交付閘道有条件適合本機展示：能在每次請求檢查權利，代價是靜態直送效能較低；效能須由票27實測，不能假定達標。第一版不新增公共分享頁、本人照片／量測輸入、AI、多層穿搭或自由旋轉。

| 項目 | 現行界線／提案 | 實作前必要決議或證據 |
| --- | --- | --- |
| 角色 | 男女各 A／B；初始女性 A 正面 Introduction，沒有暗選 Body Size Profile | 四組可識別素材與人工品質證據 |
| 尺寸 | Avatar Size Variant 與 Garment Size 分別選擇；不據此推斷顧客身體 | 依素材列支援清單，不假定全 S／M／L／XL |
| 換裝 | 一次一件、正面／右側面／背面；每個公告組合須三視圖完整合格 | 首組真實 SKU 及三視圖；再擴至約20 SKU 的成本評估 |
| 收藏 | 只在 Explicit Outfit Save 後建立；最多20組仍 PROVISIONAL | 上限、超限與刪除策略需採用；示例 quota=20 僅測試資料 |
| 匯出 | 訪客與會員均可依權利主動下載、標示品牌模特參考 | 展示／加工／下载各項權利及實際標示文字 |
| 工程數字 | nonce／export期限5分鐘、一次1張PNG、最多長邊2048px、最多並行1件，均提案 | 裝置／渲染成本實測後採用；不是既定服務承諾 |

未決項不阻擋文字提案。權利或支援尺寸缺少證據時，阻擋相應素材發布與產品啟用；不把規劃通過寫成票27／28 resolved。

## 2. 素材與相容性生命週期

Asset Revision 保存不可變 payload hash、角色／服裝識別、尺寸、三視圖檔案、對齊參數版本、來源證據參照。檔案與參數有任何變更必須新 revision；不可在同版號下替換 bytes。

權利記錄與素材技術審核分開。每次交付都核對目前權利（display、transform、download）、使用範圍／期限、素材及組合狀態；過去「已發布」不能掩蓋今天撤銷。三視圖需共同版號／集合追蹤，不用某張成功代替另外兩張品質驗收。

| 維度 | 提案狀態／轉移 | 規則 |
| --- | --- | --- |
| Revision | Draft → InReview → Approved；InReview → Rejected；Approved → Retired 或 Suspended；Suspended → Approved需新審核 | Draft／InReview不公開。Retired原版可依權利繼續重現舊收藏，但不供新選擇；物件移除則顯示無法重現 |
| 權利 | Pending／Active／Expired／Revoked | Active也要檢查權利項目與範圍；Revoked不直接復活同一授權，須新權利證據與審核 |
| 組合 | Draft → Approved；Approved ↔ Suspended（恢復需人工證據） | key包含 Avatar Size Variant、SkuId、角色／衣服 revisions、對齊revision；三視圖完整才 Approved |
| 全站換裝 | Enabled／Suspended | Suspended停新展示、保存及匯出，不變商品可購買性 |

發布交易鎖定所引用 revisions、rights、Compatibility Matrix 版本，檢查三視圖、底層服飾、顏色、遮擋、比例、量測／衣長、授權範圍與期限；記錄操作者、證據、reason及版本。渲染服務只能使用 allowlisted 本機素材物件，拒絕客戶端 URL、任意檔案路徑或外部抓取。

操作管理權限建議新增 `fitting.read`、`fitting.manage`、`asset.publish`、`asset.revoke`；預設只提案給 Owner，Merchandiser是否可管理待採用。不能把 `catalog.write`、`access.manage` 自動當作這些權限。近期TOTP的 `asset.publish`／`asset.revoke` purpose/resource binding 尚需加入身份 canonical，現行 MFA enum 不默默修改。

## 3. Fitting Session 與商品邊界

瀏覽器 tab 記憶體保留暫存選擇，不先持久化會員資料；重新整理可回 Introduction，若日後需要跨頁 sessionStorage須另核對期限／登出清理。本機 Nuxt 共用前端狀態可保留跨頁操作，SSR request state 不共用、不把私人 Saved Outfit嵌入公開商品快取。

使用者先選 Body Size Profile，再明選服裝 SKU；角色尺寸同名不等於服裝尺寸。選中的商務 SKU 必須與送入 cart 的 SkuId 相同；切角色不得偷偷變更 Garment Size，也不把換裝成功當成有庫存、保留或合身推薦。

不支援組合時保留原明確選擇，顯示「此組合未提供模特換裝參考」及正常照片／尺寸表。不要自動改角色或改訂單尺碼；使用者可主動改選支援組合。新頁使用目前已審核版，舊收藏則按原版重現。

載入／切換順序競爭：每次選擇有 client selection sequence，晚到舊回應不得覆蓋新選擇；伺服器回應描述完整 selection與 version。暫存讀取不給永久公開穿搭網址。換裝失效不清 cart／Quote，不改 Order Price Snapshot，重新加車仍走原商務驗證。

## 4. Explicit Outfit Save 與失效處理

只有 Active Member 明確點擊保存，才寫自己的 Saved Outfit。登入不自動搬移或保存 guest嘗試；管理帳號也不能按角色讀取其他會員收藏。Support查閱需另案受控資料權利流程，不以通用後台列表暴露私人選擇。

保存記錄 DatasetId／MemberId、Avatar Family／Size Variant／Body Size Profile revision、SkuId／Garment Size、View Direction、角色與服裝三視圖 revision、對齊及Compatibility revision、保存時權利證據參照、CreatedAt／rowversion。僅存配置與追溯，不預設持久化渲染PNG或真人尺寸。保存时刻可重現且具展示權才成功；歷史參照不代表永遠擁有權利。

| 結果碼（read projection，不是可任意PATCH的status） | 對外行為 |
| --- | --- |
| Reproducible | 原版仍完整且目前允許展示，按原版顯示；是否可匯出另算 |
| Unavailable | 全站／組合／revision Suspended，或原檔案缺失；显示reason與備援，不丟掉收藏 |
| RightsUnavailable | 原權利到期／撤銷／展示項不允許，立即隱藏預覽且停匯出，只回最小識別及原因 |
| ReplacementAvailable | 原版無法重現且有審核替代；同時保留原不可用原因，不把替代稱為原圖 |

商品缺貨／不可購買另外回 `productAvailability`；只要原圖仍合法，商品下架不必自動破壞原收藏；cart仍按目前可購買性阻擋。不向無權會員洩露內部權利契約／私人證據。

替代採使用者主動確認後 **建立新 Saved Outfit**，回傳 `replacesOutfitId`，舊筆保留；不偷偷覆寫歷史revision。可以刪除舊筆，但刪除是另一個明確操作。quota未採用前政策讀取應明示 `configured=false`，產品保存未綁定政策時回 POLICY_NOT_CONFIGURED，不默認無限或強制20。

已採用限額後：同Member／Dataset quota鎖與新增同交易；刪除後釋放一次；失效但未刪的收藏亦計入。重送相同 Idempotency-Key＋指紋回原結果不多計，回應遺失後可查原操作；新key相同配置仍代表另一次明確保存，UI可提示重複但不靠比圖去重。

讀／刪都只限本人，跨帳號與不存在以一致404處理。刪除帶If-Match＋Idempotency-Key，撤權後不能拿舊成功回應重播取得私人內容；先驗目前身份。登出、Suspended／Closed、SecurityVersion變動清除前端私有cache及待下載狀態，後端仍逐次驗證。

## 5. Outfit Reference Export

來源可為當前明確配置或本人Saved Outfit；訪客以browser-bound短效export能力，只能讀自己建立的操作。客戶端提供選擇／revision參照與nonce，不能提交 arbitrary HTML／SVG／canvas／URL給伺服器執行；後端自行讀allowlisted素材並產圖。

示意生命週期：Queued → Rendering → Ready；任一步可 Failed／Expired／Revoked。Ready只表示目前產圖可用，非永遠可下載；權利檢查失敗立即Revoked並移除暫存。冪等操作查核不新產图、不延長expiry；非冪等選擇變更須新key。

1. 建立時查display＋transform＋download等實際需要的權利、完整revision和相容性；訪客CSRF／Origin／限流也必須驗。
2. Worker取工作、產圖完成前與交付 bytes 前再次查目前revision／rights／全站狀態。素材更版不換job來源；版本被撤銷不能因Queued較早就放行。
3. 所有視圖保留「KOHAMI 模特換裝參考｜非本人試穿／非合身保證」與mock展示標示（文字提案）；下載不含會員識別、Email、Session、訂單／公開追蹤網址或原始檔案metadata。缺標示視為失敗。
4. PNG僅單一已選視角，UTF-8可及操作文字與下載檔名不依賴图片本身。下載不可用時保留畫面狀態及重試入口，不自動另建多件工作。
5. 檔案經授權endpoint交付，Cache-Control:no-store；沒有永久公開URL、可直接取得bytes的物件key或匿名「全部exports」清單。GET可重取同操作但不延長期限，member export限定本人有效session；guest绑定建立时browser capability。
6. 成功／失敗／過期／登出後清暫存blob與前端object URL；有界job清理排程，個別job權利先於排程生效。不把短效token寫入log/querystring。

5分鐘／1件／2048px是工程候選，壓力與記憶體實測後決議；重啟中工作從持久化狀態核對，不能默認Succeeded或跨Dataset重建。已下載或已顯示在裝置記憶體中的圖無法可靠撤回，不承諾遠端刪除使用者檔案。

## 6. 撤銷、交付與復原

本機素材以受控endpoint读取，首版提案no-store，不置於可繞過檢查的Nuxt public目錄／裸靜態server。asset ID即使公開已知，每次讀取仍要查目前權利。權利撤銷先交易標記拒絕新交付並記不可變Audit＋outbox，再清除已有應用cache／export暫存；清理失敗保持拒絕，標記cleanup pending可重試，不回滾撤銷。

已開頁面於切視角／重新保存／匯出／重新載入時重驗；另提案可見tab每30秒核對權利epoch與window focus重驗，epoch變動清除renderer bytes／預覽／export blob，顯示fallback。30秒不構成即時撤回保證；離線畫面與已交付bytes無法保證刪除。若未來引入CDN／service worker／公開快取，須另設purge證據、最長存活與故障停用驗收，本機無CDN不是purge實測通過。

restore先維持fitting/export dispatch disabled，重新套用備份cutoff後的素材撤銷／rights到期／最新會員停用與SecurityVersion，再檢查實物hash；缺少revocation ledger或合法權利證據則阻擋啟用。舊backup中的Ready export不能直接復活，必須Expired或Revoked，重新明確建立並重驗權利。沿用[M4](M4_LOCAL_ACCEPTANCE_PLAN.md)獨立target DB，不修改交易快照。

## 7. 資料交易與事件消費者

| 邏輯紀錄 | 約束與競爭 |
| --- | --- |
| AssetRevision／ViewSet | (revision,view)唯一、完整三視圖／hash不可變；實物刪除不cascade收藏 |
| RightsRecord／RevisionRightsLink | 有效起迄、用途、證據參照／version；拒絕未知權利，不用bool已授權涵蓋全部 |
| CompatibilityMatrix | 完整角色尺寸×SKU×revisions唯一；所有引用需同Dataset或明確allowlisted共享素材scope |
| SavedOutfit／QuotaPolicy | 本人所有權、不可變selection；Member＋Dataset鎖串行化quota；配置政策版本可追溯 |
| ExportOperation | actor/browser hash、selection hash、expiry、state、權利epoch、私有物件ref；冪等scope與payload一致 |
| Revocation／Audit／Outbox | source operation唯一；先拒絕交付再清理，事件重複不恢复失效素材、不重建圖片 |

事件以 `AssetRightsRevoked`、`FittingSuspended`、`AssetRevisionRetired`、`SavedOutfitDeleted`為候選名稱，包含Dataset／source operation／受影響revision／epoch，無會員選擇列表或PII。消費者：asset交付、export取消／清理、Saved Outfit read projection、Nuxt fallback、restore ledger。尚未建立事件schema，這些名稱不是已部署topic。

## 8. API 與畫面輪廓（待完整機器契約）

FOP為本文件追蹤，不改原OP-01–46。所有私人response no-store；cookie mutations要CSRF／Origin、業務寫入冪等、versioned管理／刪除操作If-Match。資產回應不得洩露本機path；一次一件與三視圖仍由server驗。

| ID／候選method/path | 使用者與必要驗證 | 必要輸出／拒絕 |
| --- | --- | --- |
| FOP-01 GET `/fitting/catalog` | public、只列Approved且權利Active | 四組角色介紹／支援Body Size清單／政策版本；Introduction不隱含size |
| FOP-02 POST `/fitting/selections/resolve` | public browser nonce、限流 | 完整selection、三視圖availability、current revisions、商品可購買性分開；不建立收藏／庫存 |
| FOP-03 GET `/fitting/assets/{id}/content` | 每次rights／revision／scope檢查 | allowlisted bytes或一致不可用；no-store，拒絕裸URL绕過 |
| FOP-04 GET／POST `/account/outfits` | Active Member本人；POST冪等／配置quota | 列表版本／明確新Saved Outfit；POLICY_NOT_CONFIGURED／QUOTA_EXCEEDED／FITTING_UNAVAILABLE |
| FOP-05 GET／DELETE `/account/outfits/{id}` | 本人；DELETE If-Match／冪等 | 原版本＋重現projection／204；跨人或不存在一致404 |
| FOP-06 POST `/account/outfits/{id}/replacements` | 本人明確確認、quota／冪等 | 新id＋replacesOutfitId；不改舊版本；替代過期／不相容409 |
| FOP-07 POST `/fitting/exports` | guest browser能力或本人Member，CSRF／Origin | 202 operation id／expiry；不得回公共download URL |
| FOP-08 GET `/fitting/exports/{id}`；GET `.../{id}/content` | 同browser能力或本人目前session、rights逐次驗 | 狀態／有標示PNG；Expired／Revoked無bytes；跨actor一致404 |
| FOP-09 GET `/admin/fitting/assets`；GET `.../{id}` | 新fitting.read scope＋MFA，待整合 | 審核最小證據／eTag，不暴露私有收藏 |
| FOP-10 POST `/admin/fitting/assets/{id}/transitions` | 新publish/revoke權限＋近期purpose-boundTOTP＋If-Match | 受限SubmitReview／Approve／Retire／Suspend／Revoke，不接受任意status |
| FOP-11 POST `/admin/fitting/compatibilities/{id}/transitions` | 新fitting.manage＋MFA／If-Match | 組合批准／停用與原因；必要素材證據檢查 |
| FOP-12 POST `/admin/fitting/suspensions` | 新fitting.manage＋近期敏感操作purpose待整合 | 全站或受限組合停用、reason／epoch；恢復另明確審核command |

SCR-F01 `/fitting`：Introduction、尺寸明選、單件與三視圖、loading／unsupported／fallback、匯出結果。SCR-F02 `/account/outfits`：只讀本人集合、重現原因、替代另存、明確刪除與quota。SCR-F03 `/admin/fitting`：審核證據、權利用途／期限、發布／撤銷／cleanup結果。新SCR僅增補既有SCR-03／06B的入口，不修改M0–M4完成條件。

鍵盤操作不依賴拖曳；視角按鈕有文字與selected語意，錯誤及結果以狀態區通知，維持焦點。不把模特尺寸寫成「你的尺寸」；模特參考聲明在展示與下載均保留。

## 9. 驗收與剩餘工作

[合成情境矩陣](fixtures/FITTING_CASES.yaml)記錄16項FIT-AC，全部產品NOT_EXECUTED：明確尺寸／SKU、unsupported、三視圖缺失、原版／替代、權利分項、跨會員、quota競爭、匯出撤銷競爭、重啟、cache拒絕、restore、late response及權限整合。實證層：CHK-01契約／版本、02 SQL競爭、03身份／素材權利、04端到端／SSR、05可及性、06裝置效能、07restore、08人工素材／交付。

下一步先展開 FOP 機器契約／consumer與身份permission/purpose差異；再完成素材實證計畫、加工成本與裝置量測。真素材尚未取得，不製作假授權證據。票27完成前不宣稱公告尺寸／约20 SKU可用；票28採用前不宣稱20收藏／匯出數值已確認。Phase 2實作及正式營運仍需相應授權與必要人工證據。
