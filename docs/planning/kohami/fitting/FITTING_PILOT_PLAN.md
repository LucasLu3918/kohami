# 最小素材實證、效能與成本計畫

2026-10-13｜PROPOSED／READY_FOR_REVIEW｜NOT_EXECUTED

目的：依票27與V07–V11取得一組合格固定模特身形×一個真實SKU×Front/Right/Back的證據，再判斷四組角色與公告尺寸／約20 SKU的可行性。先完成[匯入／審核](ASSET_IMPORT_REVIEW_PLAN.md)和[空證據模板](FITTING_PILOT_EVIDENCE_TEMPLATE.yaml)，不製作真素材或代表採購／啟用已授權。

## 1. 實證前置與失敗出口

票08前置未resolved則票27產品實證仍阻擋。計畫文件不解除票依賴。需確認：資料只有固定角色／真商品而非會員本人照片；來源非AI且來源／展示／加工／下載用途證據具體；至少一組三視圖來源不猜背面；匯入工具/decoder可受控执行；人工責任人與實際裝置可用；採購/製作如有成本先取得相應支出授權。

來源／權利未知 → BLOCKED_RIGHTS；素材品質不合格 → FAIL_QUALITY；無可用裝置／測量接口 → NOT_EXECUTED_DEVICE；支持範圍未定 → NEEDS_DECISION，不直接改為較少角色或所有尺碼都可用。繼續提供真實照片／尺寸表購物備援，核心Foundation–M4可獨立交付。

## 2. 最小樣本與人工檢查

1. 固定一個Avatar Family＋Size Variant＋BodyProfile revision、同一真SkuId/Garment Size，以及三視圖角色/服裝/對齊revisions與hash。
2. 人工記錄角色識別、比例／姿勢、適當底層服飾、顏色、袖子／衣長／輪廓、量測、背面來源、遮擋／透明邊緣；每項Pass/Fail/Unknown與證據定位。重大錯位或未知權利不可發布。
3. 同組做三視圖切換、角色介紹→明選尺寸、缺素材／撤銷fallback、匯出品牌參考聲明與mock標示。展示不作真人合身／性別或健康推斷。
4. 把一次合格組合的製作／修改／審核人時、重工次數與傳輸/儲存量記錄，不能由一組結果外推其他身形/尺寸/SKU一定合格。

## 3. 真實裝置與情境矩陣

至少一台實際iOS Safari手機、一台實際Android Chrome手機與桌面瀏覽器作**候選最小矩陣**；實際型號／OS/browser/build由可取得設備記錄，不指定購買新機、不宣稱代表全部手機。取得不到的欄位NOT_EXECUTED，不以WebKit/裝置模擬代替真Safari驗證。既有核心browser/可及性檢查仍按M4。

每裝置各測冷載入、同tab素材就緒後切換、cache失效／權利撤銷、反覆三視圖切換20輪、背景再focus、匯出／取消／expiry。先記本機正常連線，再提案固定受控network條件（延遲/頻寬/丟包實際值必須記錄）；本機loopback結果不能冒稱對外網站效能。

| 量測 | 定義與證據 | 規劃目標／限制 |
| --- | --- | --- |
| 首次載入 | navigation開始→Introduction／第一組有效素材完成首次paint，decode/bytes分列 | 不納入就緒切換P75；目標待樣本校準 |
| 就緒後切換 | 已有完整該組三視圖decoded素材，使用者選視角／組合事件→對應畫面完成paint | 沿用N02 P75≤1秒目標，非已達標 |
| 傳輸量 | request數、wire bytes、decoded像素、fetch重複與metadata條件 | no-store受控gate成本不被隱藏；以實測評估策略 |
| 記憶體 | 可用browser工具／OS觀測來源、基線／峰值／切換後回復；工具無值標UNAVAILABLE | 不用JS heap假稱GPU/整個process內存；門檻待設備實測 |
| 匯出 | 建立→Ready與下載完成分列，PNG尺寸/bytes、渲染CPU/內存來源 | 5分鐘/2048px/並行1件仍工程提案；先驗不OOM/清理可追蹤 |
| 可及性 | 鍵盤、視角selected語意、焦點、狀態通知／fallback／VoiceOver或TalkBack人工 | 列適用檢查，不因自動掃描即AA全數通過 |

試驗protocol提案：每device/scenario至少30次完整就緒切換，分開cold與warm、不混裝置。原始時間樣本由量測工具只在實證授權的證據工作區保存；本輪模板不保存對話/secret，必要證據去識別化。P75採sorted samples nearest-rank第ceil(0.75*n)個，記n／失敗與timeout；failed/timeout不丟棄成「成功樣本P75」，達標需無失敗且完成样本符合門檻。樣本數不足標INCONCLUSIVE；30次是pilot方案，非穩定統計或正式SLA保證。值不可量測就null，不填0。

## 4. 負擔與成本表（不填假報價）

| 成本項 | 輸入與估算方式 | 狀態 |
| --- | --- | --- |
| 來源／授權 | 每來源一次性／續期／使用範圍、實際報價或自有來源證據 | 金額、人員、期限未有證據→null/UNKNOWN |
| 模特製作 | 各Family×公告variant的準備、三視圖加工、審核與修改人時 | 四組不等於四張圖；列實際支援variant |
| 服裝加工 | 真SkuId×支持Avatar variant×三視圖；可重用部分另列 | 約20 SKU不是20商品款，也不是每SKU支援所有variant |
| 工程與QA | 匯入／gate／版本／收藏／匯出／撤銷／restore与device驗证人時 | 本機核心估算不自动包含Phase 2 |
| 維護 | 更版、權利更新／撤銷、重工、復原演練、審核頻率與責任 | 不假定備援人員已到位 |
| 儲存／流量 | 原始+交付三視圖+暫存exports，實測bytes×支援組合／重試／訪問情境 | 本機cloud/broker費用N/A；磁碟、電力、設備與工時仍有成本 |

輸入有人時h與已授權費率r才算h×r；無費率只報h區間，不以0代表免費。分一次性／每月維護／按量，用幣別／日期／quote validity／假設／樂觀-預期-風險範圍，避免多次計算可重用原素材。只一組pilot時不產完整商業預算承諾。

擴展表：逐列variant與SKU，分別標NotTested/Pass/Fail/Unsupported、3views檔案數、品質證據、權利用途、人時/bytes。公告支持清單只從Pass＋仍有權利資料產生，不用全矩陣乘積當已合格資產。

## 5. 可審閱輸出與採用

Pilot證據包須綁定真artifact版本／commit、manifest/hash、角色／SKU與三視圖、來源/權利ref、人工審查、裝置條件／n／統計／失敗、成本來源、fallback/撤銷／匯出與限制。使用[FITTING_PILOT_EVIDENCE_TEMPLATE](FITTING_PILOT_EVIDENCE_TEMPLATE.yaml)填真證據；review status保持PENDING直到實際審閱，不從validator產生Human approval。

首組pilot合格是「此組可審閱實證」，不是四組/20 SKU正式啟用，也不自動決定尺寸/quota或新權限。達標但成本未知仍NEEDS_DECISION_COST；效能不合格可提出減傳輸／加工調整／支持設備提案，素材不合格可縮支持組合提案；任何改變既定四組方向／三視圖／產品承諾由使用者確認。

## 6. Pilot驗收（全部NOT_EXECUTED）

| ID | 必要證據 |
| --- | --- |
| PILOT-AC01 | 非AI來源、真SKU與三視圖／hash可追溯；權利用途逐項證據 |
| PILOT-AC02 | 人工尺寸／顏色／輪廓／遮擋及背面來源，Unknown不算Pass |
| PILOT-AC03 | 真iOS/Android/桌面條件與樣本；cold/warm分列，失敗不刪除，n不足無達標 |
| PILOT-AC04 | 就緒切換P75、傳輸與内存來源／缺值限制；無假裝GPU／SLA結論 |
| PILOT-AC05 | fallback正常購物、撤銷及expiry無新bytes、下載帶標示与清理 |
| PILOT-AC06 | 分項人時／報價/UNKNOWN與可重用量；不以首組外推全SKU |
| PILOT-AC07 | 擴展矩陣仅公告Pass有權利組合；未測variant/SKU不可「全支援」 |
| PILOT-AC08 | 真版本證據包與人工採用／剩餘決策；不解除原票依賴或推定正式啟用 |
