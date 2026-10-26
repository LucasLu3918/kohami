# KOHAMI 模特與本機生圖規範

此文件是未來 KOHAMI 商品型錄 AI 生圖的角色與執行指引。畫面方向以[本機展示視覺系統](../planning/kohami/experience/VISUAL_SYSTEM.md)為準；API、價格、SKU、庫存及模特換裝功能均由各自的正式契約裁決。

## 模特角色鎖定

- 角色 ID：`kohami-editorial-model-01`。她是虛構、明確成年的 18 歲日籍女性型錄模特；不是任何真實人物或會員。角色資料以[身份指南](../../templates/creative/models/kohami-editorial-model-01/IDENTITY_GUIDE.md)及[角色 profile](../../templates/creative/CHARACTER_PROFILE.yaml)為準。
- 顯示名稱：伊織。固定辨識點：柔和橢圓臉、深棕長髮與輕薄空氣瀏海。兩位模特共用標準：明確成年 18 歲、自然成人比例、白淨真實膚理、清純活潑表情與日式淡妝；不可見皺紋，法令紋及口角紋休息時不顯、微笑時僅淡淡自然呈現。避免疲態、濃妝與塑膠磨皮。各角色臉型與髮型以自己的身份主圖為準。
- 服裝方向：帶日式甜美感的成年女性日常女裝型錄；合身但不緊繃，層次輕盈，燕麥、米白、霧玫瑰、鼠尾草、柔和橄欖、石板藍等低彩度色系。姿勢和表情像被攝影師捕捉到的自然瞬間；不性感化、不過度修身、不使用校服或暗示未成年。
- 場景方向：日式公園、安靜日式街道、日式建築內部／緣側、日系咖啡廳、居家、書店、河岸或小型棚拍空間；背景克制，不搶服裝焦點。伊織與沙織身份主圖均已由使用者選定；其他參考照只供各自登記的姿勢、表情、角度或穿搭用途。
- 角色資料夾位於 [`kohami-editorial-model-01`](../../templates/creative/models/kohami-editorial-model-01/)；`04-identity-master-selected.png` 是身份主圖，`05`–`07` 是表情／姿勢補充照，`09-iori-three-angle-supplemental.png` 是以該身份主圖為 image_1 的三角度補充參考。它不是商品圖或身份主圖；目前恢復檔案與 run manifest 的 SHA-256 不一致，僅能按輸入角色記錄歸屬伊織，位元組來源尚未校驗。
- 身份主圖已由使用者選定，但固定 Z-Image-Turbo workflow 是純文字生圖，不能輸入主圖作身份條件。不得宣稱跨姿勢身份鎖定或已訓練 LoRA；真正需要固定同一張臉時，先註冊並預檢支援身份圖輸入的本機 profile。

## 固定生成模型與本機狀態

- 專案依 [OPT-18](../todo/options.md#opt-18) 的本機目標為 FLUX 3 Klein；使用已登錄的 `flux3-image-klein-distilled-int8` profile，且每次都要重新通過 AIPS／ComfyUI runtime 與權重預檢，不自動換模型或 fallback。
- FLUX Klein 與 `z-image-turbo` 都是文字生圖流程，不能接收模特身份圖和商品圖。FLUX profile 曾完成一張風格概念照，但沒有雙參考商品編輯證據；這兩個流程不得用來宣稱身份或 SKU 已鎖定。
- 先前使用者指定的 Qwen Image 2.1 `qwen-image-2.1-edit-int8` 雙參考流程，是目前已觀察成功的商品穿搭案例；每張 `image_1` 放角色身份圖、`image_2` 放精確 SKU／色號來源，並從來源重新編輯。這是具體任務中明確選用的流程，不是全域模型預設。棉麻襯衫重試證明單姿勢設定可避免拼圖，但由 cream 衍生的藍／鼠尾草色仍只能列概念候選，鈕扣數也未完全通過商品核對。
- Qwen Image Edit 2511 profile 在目前 Apple MPS 主機曾因記憶體壓力遭 SIGKILL，FP8 cast 不受 MPS 支援；環境或權重改變並通過一張實際推論前，保持停用。
- 使用者於 2026-10-26 確認目前產圖機制可公開使用；資產仍記錄實際模型授權名稱與原始條款，不把此使用者確認寫成模型授權條款的修改。單張成功只證明該次輸出可完成，不代表其他 SKU、色號、角色或模型也通過品質驗收；失敗細節見下方案例紀錄與 `PROMPT_LIBRARY.yaml`。

## 商品穿搭圖規則

- 每次只生成一張獨立商品照片，不要多姿勢拼圖、接觸表、漫畫、插畫、文字、商標、水印或多商品拼接。
- 每款商品／SKU 圖庫開始前，從符合身份參考、已預檢本機編輯流程及該用途授權條件的角色中隨機指定一位模特；同一款圖庫沿用同一位，下一款可重新抽選。拍攝單與輸出 manifest 記錄角色 ID、身份參考 SHA-256 和抽選 seed。伊織與沙織均已定案為角色身份主圖；姿勢／角度補充圖不自動升格為身份錨點。
- 來源商品照片是商品形狀的主依據。提示只改變模特穿著、姿勢、鏡頭與背景，不得改領口、袖長、衣長、口袋、縫線、釦子、圖案、材質外觀或色彩。參考編輯要盡量保留原商品，不能用文字提示重畫商品取代來源圖。
- 目標商品是下著時，搭配合適上衣，不額外加裙子；目標商品不是下著時，若適合，優先搭配蕾絲、薄紗與緞面層次的裙裝。洋裝、外套、配件或裙裝會遮住／不合商品時，改用最適合商品且清楚呈現 SKU 的方案；材質層次只用在陪襯服裝，不能加到商品本身。
- 商品卡用一致的 1:1 或接近方形構圖；模特需呈現足夠服裝範圍，頭部、袖口、裙褲下襬與商品關鍵細節不可被裁切。首頁 hero 才使用寬幅橫式構圖。
- 先做上衣、下身、洋裝各一款的三張 pilot。舊有 9 張拼貼／插畫混入輸出皆為淘汰素材，不得回收作商品圖。
- 尺寸、價格、顏色名稱與庫存只由 Catalog API／正式商品資料顯示；不得從模特體型推算尺寸或宣稱合身。合成穿搭照片不能冒充真人實穿、授權代言或換裝功能。

### 工作流優化建議

每款商品依「一張平拍＋四張穿搭」先做一頁拍攝單，再逐張執行。拍攝單先隨機指定一位合格模特，鎖定其身份圖與 SKU／色號平拍來源；再依商品類別及相容性選裙裝或其他搭配，為四張穿搭圖各選一個場景、姿勢、取景與需露出的商品細節。同一款圖庫全程使用抽中的模特。四張至少涵蓋三個場景，相鄰照片不重複場景，單一場景最多兩張。平拍不計入場景數。這個排程讓多樣性可檢查，同時維持身份、雙參考圖與商品忠實度。

生成前逐一核對兩張參考圖的 staging 檔名、順序及 SHA-256，通過 live preflight 後一次送一張；等輸出和 manifest 都完整，再審核身份、SKU、構圖和場景後開始下一張。逾時或命令中斷時先查 queue/history、AIPS trace、檔案和 manifest，確認工作結果再處理，避免重複推論或遺漏輸出。Qwen Image Edit 2511 雖有 Apache-2.0 權重與通過的 live preflight，本機 MPS 推論仍未通過；本主機 48 GiB 記憶體上的 profile 目前停用，直到受支援的低記憶體本機權重或較大記憶體執行環境通過單張實測。

## 自然人像提示骨架

提示詞採五項短句：角色身份（雙參考時由 image_1 鎖定）、一個動作、目標商品關鍵結構（由 image_2 校對）、一個場景／取景，以及柔和自然光的寫實型錄照。不要重複描述參考圖已提供的五官，也不要堆疊多場景、多姿勢或長串同義形容詞。單張輸出須明確寫成一個人、一套穿搭、一張連續照片，避免拼貼／分格。Z-Image-Turbo 沒有 negative-prompt 欄位，避免另造負向欄位。

```text
One photorealistic Japanese fashion catalog photo of the same clearly adult, age-18 model in image_1, wearing the exact garment in image_2; one relaxed sleeve-adjusting pose in a quiet Japanese cafe, soft daylight, full outfit visible in one continuous frame. Fresh light Japanese makeup, natural fair skin, no visible wrinkles.
```

先前完成單張商品穿搭候選的本機流程是 Qwen Image 2.1 雙參考 edit；每張都以該角色 identity master 為 `image_1`、指定商品來源為 `image_2`，由原圖開始編輯，不串接前張結果。這是已觀察到的成功案例，不代表全域預設或所有商品都可行；只有當任務明確選用此 profile、live preflight 通過且商品來源足以核對時才執行。Qwen Edit 2511 在目前 Apple MPS 主機停用。

## 驗收與來源紀錄

逐張人工檢查角色臉部／髮型一致、確實只有一張照片、商品輪廓與來源相符、色彩可辨、服裝完整入鏡、手部與五官自然、背景不搶商品、解析度與裁切符合型錄。任何商品細節漂移或身份漂移都拒收，不以平均分數抵銷關鍵錯誤。

每張核准圖保留來源商品 ID／原圖雜湊、model ID／revision、runtime／workflow 版本、角色參考雜湊、seed、尺寸、日期、輸出雜湊與人工檢查狀態。依使用者明確要求，此角色資料夾在 [`PROMPT_LIBRARY.yaml`](../../templates/creative/models/kohami-editorial-model-01/PROMPT_LIBRARY.yaml) 保存完整生圖提示、prompt hash、profile/workflow 快照與每次執行來源；不保存聊天全文、憑證或不相關的人像資料。其他生成工作仍以 fingerprint 追溯為預設。商店瀏覽只提供已審核的靜態圖，不在顧客瀏覽時呼叫模型推論。

### 生圖前置檢查與失敗復原

2026-10-25 流程回顧：一次雙參考 Bundle 的 live preflight 回報 ComfyUI HTTP 404。沿 AIPS `verify_comfy` 呼叫順序追查，該階段已通過 `/system_stats` 與 `/object_info`，後續會逐一以 Bundle 指定的 staging 檔名呼叫 `/view?...type=input`，並比對檔案 SHA-256；因此這次 404 是參考圖尚未以相同檔名存在於 ComfyUI input staging，或 Bundle 檔名和實際 staging 名稱不一致。它不是權重缺失證據。不要對這類非重試錯誤重送工作或換模型。

開始每張雙參考編輯前依序確認：

1. 從專案根目錄的角色身份主圖和精確 SKU／顏色平拍圖建立 Bundle `input_images`，記錄兩張圖的 SHA-256 與順序（身份 `image_1`、商品 `image_2`）。
2. 在 ComfyUI Desktop 將這兩個來源檔上傳到 input staging，明確指定安全且唯一的檔名；把相同順序與檔名填入 `comfyui.input_image_names`。不要沿用上一款商品的檔名或假設 staging 仍存在。
3. 執行 AIPS live preflight。只有兩次 `/view` 讀回的 bytes 都與 Bundle 來源 SHA-256 相符、profile／workflow schema 也通過時才執行。遇到 `provider_http_error`、404 或 hash mismatch，停止該張工作，核對 staging 檔名與來源；修正後重新 preflight，不修改模型、不自動 fallback。
4. 以單張工作執行並等待 AIPS 回報 `COMPLETE`；核對輸出檔與執行 manifest 都已寫入，manifest 的輸入雜湊、輸出雜湊、seed、prompt／workflow hash 和 elapsed time 齊全後才開始下一張。批次工作期間不要中止 `generate-set`。若命令中斷或結果不明，先查 ComfyUI queue/history、AIPS trace、輸出檔及 manifest，將已完成輸出補做核對與登錄後才可重送，避免產生「ComfyUI 有圖但 AIPS 無紀錄」的孤兒輸出或重複推論。
5. 若輸出服裝結構、顏色或模特臉部不合格，將該張標記拒收並保留原因；同一 SKU gallery 只重生被拒收的姿勢，其他已核准輸出不覆寫。每個顏色都要提供相符的 SKU 平拍參考與獨立對照記錄，不可把基礎色商品圖標成另一色的精確商品照。

### 品質標準與失敗案例紀錄

每張商品穿搭圖分開判斷「模特身份品質」與「商品 SKU 忠實度」；任一項不通過就拒收，不以其他優點抵銷。角色細節以[身份指南](../../templates/creative/models/kohami-editorial-model-01/IDENTITY_GUIDE.md)為唯一角色標準，服裝搭配與變化池以[提示知識庫](../../templates/creative/models/kohami-editorial-model-01/PROMPT_LIBRARY.yaml)為準，執行細節及案例以本節為準。不要在多份文件重複維護同一組可變選項。

| 日期／案例 | 實際結果與原因 | 預防／復原規則 |
| --- | --- | --- |
| 2026-10-24，FLUX 候選 prompt upsampler | Apple MPS 生成 262/1024 tokens 耗時 40 分 44 秒後中止；直接繞過 upsampler 的測試雖於 71 秒完成，結果是插畫拼貼而非可用型錄照。 | 此候選不是 active profile；不重複無界等待或用失敗輸出。若經核准測試，先設有界 prompt 長度與 timeout，並以單張結果核對是否完整寫實照片；不合格即停止，不以該 profile 自動替換現行流程。 |
| 2026-10-25，雙參考 ComfyUI HTTP 404 | Runtime `/system_stats` 與 `/object_info` 正常，但預檢讀取 Bundle 指定的 input staging 檔名時回 404；兩張參考圖尚未以相同名稱放入 ComfyUI input。 | 先上傳身份圖和商品圖，以本次唯一檔名 staging；逐字核對 Bundle 名稱、順序與 SHA-256，再重跑 live preflight。這是來源 staging 不一致，不是權重缺失，不要重送推論或換模型。 |
| 2026-10-26，Qwen Edit 2511 Apple MPS 記憶體 | 48 GiB Apple Silicon 主機使用 BF16 主模型時，ComfyUI 日誌回報載入約 38.97 GB diffusion weights 及約 7.91 GB text encoder；單張 4-step 1024px 工作約 588.8 秒後 ComfyUI 子程序遭 SIGKILL，無輸出 hash。FP8 cast 實際執行回報 `mps_dtype_unsupported`。降至 768px 後 `memory_pressure` 只剩約 0.7 GiB free，工作於採樣前中止。 | 只允許在已驗證的受支援低記憶體本機權重或有足夠 RAM 的主機上重新單張試產。FP8 cast 不支援 MPS；不要重跑 BF16、不要再降低參考解析度反覆試、不要自動切換模型或雲端。重新執行前先通過 profile／runtime 預檢並檢查可用記憶體。 |
| 2026-10-25，wool-coat 按鈕結構 | 商品來源有 6 顆鈕，生成圖簡化成 4 顆；提示已列出鈕數仍未阻止結構漂移。兩次同類修正仍未忠實呈現。 | 生成前把 SKU 拆成可目視核對的關鍵特徵清單（含數量、位置、排列、領口、袖口、口袋、下襬、指定色）；生成後逐項比對來源。提示文字不能取代來源圖或驗收。兩次相同結構錯誤後停止該 SKU／姿勢組合，將來源與差異記錄為 `REVISE`，改用較靜態、能清楚呈現細節的姿勢或等待更合適的編輯方式，不再原 prompt 連續重抽。 |
| 2026-10-26，角色 profile 多姿勢造成拼圖 | 同一 Qwen Image 2.1 Bundle 雖提示單一站姿，角色 profile 仍同時允許 front、three-quarter、profile；首張輸出因此把三個姿勢排成 triptych。 | 每張圖的臨時 character profile 只允許本張的一種 pose 與 expression；prompt 明確指定單一人物、單一姿勢、單一連續照片。先生成單張 pilot 並目視確認，再依序執行其餘圖；拼圖輸出直接排除，不重複原設定抽卡。四張修正候選記於本次 RUN_SUMMARY。 |
| 2026-10-26，棉麻襯衫概念色版結構漂移 | 以奶油色平拍生成 powder_blue、muted_sage 概念色版時，強提示能保留長袖與胸袋，但按鈕數量仍有漂移；模型完成單張照片不代表 SKU 結構正確。 | 生成前列出可數量商品特徵，生成後逐項與來源比對。只由單一色版來源生成的其他色款必須標為概念圖；結構有一項不清楚就不當作可上架 SKU 圖。取得各色實拍來源或能可靠保真的編輯流程後再做商品展示。 |
| 2026-10-26，ComfyUI 輸出 hash 與 manifest 不符 | 從 ComfyUI output 目錄按檔名恢復的三視角圖，SHA-256 與執行 manifest 記錄不同；檔名與 `image_1` 可辨角色，但不能證明恢復檔就是 manifest 的原始 bytes。 | 完成後立即從本次 AIPS 回傳的輸出路徑封存檔案並比對 manifest hash。hash 不同就停止匯入／上架，從同一 run 的正式輸出重新恢復；無法匹配時標示來源未驗證，不把它當已驗證素材，也不盲目重跑推論。 |
| 2026-10-26，ComfyUI 輸出 hash 與 manifest 不符 | 從 ComfyUI output 目錄按檔名恢復的三視角圖，SHA-256 與執行 manifest 記錄不同；檔名與 `image_1` 可辨角色，但不能證明恢復檔就是 manifest 的原始 bytes。 | 完成後立即從本次 AIPS 回傳的輸出路徑封存檔案並比對 manifest hash。hash 不同就停止匯入／上架，從同一 run 的正式輸出重新恢復；無法匹配時標示來源未驗證，不把它當已驗證素材，也不盲目重跑推論。 |
| 2026-10-25，人像偏暗或顯老 | 部分 hover 模特照的臉部偏暗、疲態明顯，和設定的年輕清新型錄品質不一致。 | prompt 固定明亮柔和的正面補光、自然但有精神的眼神、輕透成人妝感、自然膚理及柔和表情；拒收曝光不足、眼下陰影過重、疲憊／顯老感、蠟像磨皮或身份漂移。先改光線與表情描述再重生，不靠過度磨皮補救。 |

每張生成前採用以下順序，避免一次改太多條件而無法定位問題：

1. **鎖定輸入：** 選定已核准且在此主機支援的 profile；身份圖固定為 `image_1`，同款同色商品圖固定為 `image_2`。來源檔、SHA-256、順序與本次唯一 staging 名稱寫入 Bundle；沒有該色商品來源時，預先標記「概念圖」，不可核准為 SKU 商品照。
2. **整理 SKU 特徵：** 從商品圖列出顏色、輪廓、領型、袖長、鈕扣數與排列、口袋、印花／織紋、衣長和下襬。不清楚就標「不可判讀」；關鍵特徵看不清時先補清楚來源圖，或將該輸出限定為概念圖，不以 prompt 代替證據。
3. **選一個變化方向：** SKU／色號固定；先為整組商品圖庫隨機指定一位合格模特並固定身份參考，再依商品類別挑選相容搭配。非下著商品優先考慮蕾絲、薄紗、緞面層次裙裝；若會遮住或不適合 SKU，就選最適合商品的方案。接著從知識庫挑各圖的造型公式、場景與主姿勢，再搭配細微表情、妝髮、最多兩項配件。記錄選項及 seed；避免連續圖重複相同的造型、髮型、配件、場景、姿勢和取景。可隨機變化的是模特指派（每組一次）和陪襯元素，不是同組內的身份或目標 SKU。
4. **即時預檢：** 核對 runtime/profile/workflow、權重與 dtype 支援、ComfyUI queue/recovery 狀態、兩個 staging 檔名及讀回 hash、尺寸與單張設定。每張 Bundle 的 pose 和 expression 必須各只有一個值，prompt 也只寫該動作；profile 若保留多姿勢選項、輸入 404/hash 不符、schema／權重不合或環境已知不支援，就停止、不送推論。
5. **首張 pilot 閘門與逐張核驗：** 每個 SKU／色號先出一張，通過「單人、單套、單張連續照片、身份、人像、可見 SKU 結構與顏色」全部檢查才繼續其餘照片。關鍵鈕數等細節要能從來源和輸出逐項核對；若被手、包、外搭、摺痕或裁切遮住，就拒收或改靜態姿勢，不以整體相似度放行。
6. **有限修正：** 每次拒收都登記具體差異與狀態（`REVISE`／`REJECT`），只改一個可定位的因素並保留原輸出。相同根因最多做兩次針對性修正；兩次後仍錯就停下該 SKU 組合，更新來源／策略或列為待解，不無限抽卡。
7. **核准與上架：** 封存 AIPS 正式輸出後，實際檔案 SHA-256 必須與 manifest 相同；不符即停止匯入，保留為未驗證並從同一 run 恢復。manifest 記錄 SKU／色號、模特、姿勢、profile/workflow、輸入／輸出 hash、seed、審核與授權狀態。只有來源完整、SKU 全項通過、授權允許且人工核准的圖才能進展示。

一致性量表採硬性閘門，不合格項不得用平均分數通過：

- **身份：** 同一角色的臉型、五官比例、髮色／髮長辨識點、膚色及成人感符合 identity master；同款圖庫維持同一身份。表情、髮型整理可細微變化。
- **人像品質：** 明亮柔光、清楚眼神、自然膚理、清爽淡妝與自然姿態；不接受昏暗黯淡、顯著疲老、濃妝、塑膠磨皮、成人年齡感丟失或臉部變形。
- **SKU：** 指定 SKU／色號來源與輸出一致；顏色、輪廓、數量特徵及關鍵結構均可核對。看不清楚不等於通過，必須重生或更換展示角度。
- **畫面：** 一張照片、一位模特、一套完整穿搭；服裝重點可辨，畫面沒有拼貼、插畫化、文字／商標、水印、重複肢體或遮擋。
- **變化：** 每組 SKU 圖庫開始前隨機選定一位合格模特，同組固定 identity 與 SKU，允許日系搭配、妝髮、姿勢、表情、場景和鏡頭變化。至少 5 張時，使用至少 4 種造型公式、3 種髮型整理、3 個場景與 4 種姿勢；不以同組換臉或改 SKU 製造多樣性。

### 目前本機狀態

2026-10-24 固定 `z-image-turbo` profile 已通過 ComfyUI 本機 preflight，且完成三張姿勢／表情補充照生成。2026-10-25 Qwen Image 2.1 曾完成歷史穿搭編輯；2026-10-26 限制每個 Bundle 僅一種姿勢後，棉麻襯衫四張雙參考候選完成單張照片輸出。2026-10-26 Qwen Edit 2511 雙參考 profile 的 live preflight 通過，但 BF16 在目前 Apple MPS 主機遭 SIGKILL；FP8 cast 不支援 MPS；768px 低 latent 診斷在嚴重記憶體壓力下中止。Qwen Edit 2511 目前仍遭 Apple MPS 記憶體限制；棉麻襯衫 Qwen 2.1 候選僅存放在內部 generation-runs，網站 gallery 尚未更新。ComfyUI catalog 未提供權重檔 SHA-256。
