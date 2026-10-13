# 換裝機器契約與身份整合提案

2026-10-13｜PROPOSED／READY_FOR_REVIEW｜產品 NOT_EXECUTED

[API_FITTING](API_FITTING.openapi.yaml)展開20操作／28schemas；與原商務76＋TOTP9＋帳號19合計124個跨文件唯一method/path與operationId，僅範圍清單，不是已合併canonical或全產品完成。M0歴史13操作排除重複計數。原四份機器契約hash保留於新檔，修改原檔前需重新審查，不由validator自动重算掩蓋差異。

**適配性：有條件適合。** 固定素材閘道／版號／本人收藏可用既定API與Nuxt同origin架構；本輪只規劃契約。真素材、製作／授權與裝置效能、正式權限採用仍是啟用前置，不因合成例子通過就放行。

## 1. 原輪廓的明確差異

| 語意 | 機器提案 | 影響／採用邊界 |
| --- | --- | --- |
| Browser context | 新GET `/fitting/context`發專用nonce及HttpOnly `kohami.fitting-browser` | 同CSRF的BrowserId，但nonce用途只fitting，不可拿去確認帳號proof；未修改原account-context |
| 原POST `/admin/fitting/suspensions` | 改單例GET `/admin/fitting/control`與POST `.../transitions` | 未部署／未canonical輪廓調整；先讀ETag，再Suspend/Resume同aggregate |
| Compatibility version | 新GET `/admin/fitting/compatibilities/{id}` | If-Match與expectedVersion有真實來源，不讓client任意造version |
| quota未配置 | GET `/account/outfits/policy`回configured=false，limit/policyVersion=null | 保存409 POLICY_NOT_CONFIGURED，並非默認20／無限；policy是靜態route優先於{id} |
| 匯出成功清理 | Ready保留私有server暫存直到expiry／撤銷；下載後可清client object URL | 若一Ready就刪檔將無法交付；同操作GET可重取但不延期限 |

FOP-01–12 trace保留，新增讀取/context以extension追蹤；只有method/path算操作。ResolveInput不能帶任意版本、URL、MemberId或本人尺寸；server選当前Approved版。回應中的Selection是固定完整references，保存時再驗目前公開組合，不能借client metadata跳過rights。

素材新版本匯入、素材製作、權利證據取得與獨立審核工具尚未機器展開；本輪admin從已存在且可追溯的素材aggregate讀／transition，不假造初始資料。這些建置缺口留給票27實證與後續import契約。

## 2. Browser、收藏與匯出能力

GET fitting/context可匿名呼叫；不能提供User/Email探測，不回cookie bytes或驗證碼。設定Path=/api/v1/fitting的browser cookie，Secure/HttpOnly/SameSite=Lax；其不可用於/account或/admin授權。CSRF cookie/nonce与fitting cookie由同BrowserId綁定，但各自用途不互換；GET context與GET csrf呼叫順序不得制造不同browser身份。有效Member session優先，不能故意選guestcookie繞Member權限／限流。

nonce候選5分鐘，重取在原expiry前返回同binding不展延。expiry輪替撤舊nonce與舊guest export能力；export也有自己的不晚於binding到期的有效期限。登入／登出／account切換／SecurityVersion變動輪替該browser binding並清私人前端state，guest操作不自動轉為Member資源，舊guest能力不可復活。新瀏覽器須建立新選擇／操作，不把能力當公共可分享URL。

Guest的ExportInput只能CurrentSelection；SavedOutfit輸入必須Active Member且原筆本人。Member export逐次查本人session/版本；stage-only、Suspended/Closed均無法讀取收藏或下載。另顯示原商品availability不構成新交易允諾；按原SkuId加車仍重驗商務。

跨actor/不存在以404；只有核對本actor後才可回409 Pending或410 Expired/Revoked。GET匯出狀態可回終態Export，但content失效無bytes。新權利撤銷可由讀取即刻投影，不必等outbox清理；已開始交付的bytes／離線圖／已下載檔案不可可靠回收。每一次完整或Range交付均需權利／身份檢查，首版提案不支援Range／conditional304避免舊快取繞過，回no-store/nosniff。

## 3. 版本、冪等與回應遺失

| 操作 | If-Match来源 | 重送與quota／結果 |
| --- | --- | --- |
| 明確save | 無既有outfit ETag；Selection中完整references與Compatibility version重驗 | 同key/body同actor回原新id；每人Dataset quota鎖＋新增同交易，不多計 |
| 刪除 | GET本人Outfit的ETag | 原key刪除tombstone可204；新key對不存在一致404，不重复減quota |
| 替代另存 | 原Outfit ETag（不是新筆／Compatibility ETag） | 同key不重建；先查原筆本人／version與目前替代，另建新id。回ETag/Location指新筆 |
| create export | 無尚不存在的Export ETag；Saved source expectedOutfitVersion核原筆 | 原key＋同browser/session與payload失回應重送，查回原operation，不再派工／展expiry；原actor binding仍有效而operation過期回410；binding也已失效則401／一致404，不回私人結果、不新建 |
| asset transition | GET AdminAsset的ETag，AssetRevision＋rights link同aggregate | command、reason、evidence、expectedVersion皆進指紋；原key重驗目前Owner/scope/proof，再回原結果 |
| Compatibility／control | 對應GET aggregate ETag | body expectedVersion與If-Match須相同revision；同交易核狀態／rights/evidence |

一般缺If-Match 428、舊版412、header/body不同revision422、同key改指紋409。近期TOTP可能在回應遺失重送前過期，可重新取得同purpose/resource proof，再以原key同body重送；proof不放業務body／指紋，不以新key補救。已撤權者不能重播先前成功取得資料。

Resolve非持久業務寫入，無Idempotency-Key；clientSequence回應必須對應requested.clientSequence，晚到舊回應不可蓋新選擇。每個Supported／Reproducible都有完整Front/Right/Back各一；不支援／權利失效沒有asset bytes參照。Schema只查shape，actual resource/bytes/hash、references同Dataset及正確SKU仍由server驗。

## 4. 身份權限與MFA差異

[FITTING_IDENTITY_OVERLAY](FITTING_IDENTITY_OVERLAY.yaml)是未啟用差異表；**MFA B已採用，新enum與權限未採用**。Owner只是角色條件，仍須實際有效授權；其他角色預設deny。不能把catalog.write／access.manage當素材操作權限。

| 新permission | 可授scope提案 | 近期proof用途與目標 |
| --- | --- | --- |
| fitting.read | Dataset／AssetRevision／Compatibility | 完整AdminAuthenticated，不需新的近期proof |
| fitting.manage | Dataset／AssetRevision／Compatibility | fitting.manage＋精確AssetRevision／Compatibility／Dataset id |
| asset.publish | Dataset／AssetRevision | asset.publish＋精確AssetRevision id |
| asset.revoke | Dataset／AssetRevision | asset.revoke＋精確AssetRevision id |

AssetTransition command map：SubmitReview/Reject/Retire/Suspend需fitting.manage；Approve/Resume需asset.publish；RevokeRights需asset.revoke。全部管理寫入近期TOTP，compatibility/control則fitting.manage；全站Dataset grant不等於可拿Dataset proof代替AssetRevision proof。Dataset覆蓋只同Dataset資源，跨Dataset拒絕。

Canonical整合需一起更新：GrantCreate/GrantRead role-permission-scope條件、GrantScope kind及權限版本，TOTP ReauthChallengeRequest/Challenge/ReauthProof/RecentProof內purpose-kind-id分支，Session effective permissions投影、每操作授權、outbox已啟動與未啟動裁決、revocation/restore ledger。擴enum不足以通過授權；原補充API仍拒新值。行政grant操作本身繼續用access.manage＋targetUser近期proof，不可用素材proof改他人權限。

首次Owner素材權限須走明確受保護bootstrap／其他有權Owner指派；現有no-self-grant不放寬。bootstrap命令與實際授權尚UNBOUND，不填假路徑或default全權；因此這份overlay不能自行讓既有Owner通過管理操作。SecurityVersion變動不替代MFA、不把Member/stage直接升為AdminAuthenticated。正式整合須取得範圍採用與M0身份實證。

## 5. 消費者與相容性

Nuxt catalog只消費公開profiles；SSR每request身份state隔離，Saved Outfit/Export/admin no-store且不入公開cache。AssetDelivery gate逐次查rights/epoch；ExportWorker僅server allowlisted素材、PNG限定，不接受任意HTML/SVG/URL及私人影像；SavedOutfit projection保留原版。Outbox重複/亂序不可回復舊rights。M4 restore dispatch disabled、重套較新撤銷／停用再檢查檔案hash；Ready export不從backup直接復活。

原檔Schema同名視為各文件內namespace，不直接拼成一個components；整合必須消除Problem/Session/ETag差異，核對DTO序列化及consumer map。新OpenAPI無跨文件$ref；來源hash使基準變動明確失敗。沒有新增HTTP request到外站、真素材下載或產品部署。

## 6. 補充驗收（產品全部NOT_EXECUTED）

| ID | 情境／外部判準 |
| --- | --- |
| FAPI-AC01 | csrf/context任一順序同BrowserId；新用途nonce不能驗account proof；重取不延expiry |
| FAPI-AC02 | 登入/登出/切actor後guest舊export不可讀；stage不可看Member收藏；他人404 |
| FAPI-AC03 | Supported三視圖各一；錯body/sku/revision拒絕；OutOfStock仍可合法參考但不能買 |
| FAPI-AC04 | save/quota未配置拒；已採用測試quota並行不超；舊key同id、改payload409 |
| FAPI-AC05 | 原版Unavailable後替代新筆，正確原ETag與新Location，不覆原版本 |
| FAPI-AC06 | create export失回應後同key查原id，expiry或cookie輪替無新job；guest不能用SavedOutfit |
| FAPI-AC07 | Ready內容權利撤銷無bytes；cleanup失敗仍拒；Range/304不繞過；標示缺失Failed |
| FAPI-AC08 | admin錯aggregate ETag／缺If-Match／expected不同拒；新權限未配置或近期purpose錯拒 |
| FAPI-AC09 | admin不同command權限、同Dataset scope與精確proof；越權/self grant與catalog.write旁路拒 |
| FAPI-AC10 | restore與亂序outbox不復活舊素材/export；SSR／actor切換與晚到回應無泄漏 |

原16項[FIT-AC](fixtures/FITTING_CASES.yaml)仍必要；本輪checker只核OpenAPI、樣本／負向shape、overlay、操作唯一與基準，不執行TOTP／SQL／renderer／UI／實際授權或效能。下一步素材最小實證及import/事件契約，仍需合法素材與必要人工確認。
