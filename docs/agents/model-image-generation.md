# KOHAMI 模特與本機生圖規範

此文件是未來 KOHAMI 商品型錄 AI 生圖的角色與執行指引。畫面方向以[本機展示視覺系統](../planning/kohami/experience/VISUAL_SYSTEM.md)為準；API、價格、SKU、庫存及模特換裝功能均由各自的正式契約裁決。

## 模特角色鎖定

- 角色 ID：`kohami-editorial-model-01`。她是虛構、明確成年的東亞女性型錄模特，外觀約 25–30 歲；不是任何真實人物或會員。
- 固定辨識點：深棕近黑長髮、輕柔薄瀏海、暖中性膚色、低彩度自然妝感，以及小型粉色花朵髮夾。五官、髮長、髮色、膚色、明確成年年齡感與自然成人比例應維持一致；表情可有細微變化。
- 服裝方向：帶日式甜美感的成年女性日常女裝型錄；合身但不緊繃，層次輕盈，燕麥、米白、霧玫瑰、鼠尾草、柔和橄欖、石板藍等低彩度色系。姿勢和表情像被攝影師捕捉到的自然瞬間；不性感化、不過度修身、不使用校服或暗示未成年。
- 場景方向：簡潔居家或小型棚拍空間，柔和窗光、低反差陰影、自然膚質與溫暖白色平衡；背景克制，不搶服裝焦點。延用第三張畫布的型錄氛圍，不把畫布中的單張臉直接當作已核准的永久身份參考。
- 第一張獨立角色參考圖須以本機模型生成，經人工確認後才能成為 identity reference。若沒有已核准的角色參考，不做批次商品穿搭圖。
- 角色與視覺設定的可重用資料在 [`templates/creative/`](../../templates/creative/)；使用者提供的三張原始全頁參考圖保存在 [`kohami-editorial-model-01`](../../templates/creative/models/kohami-editorial-model-01/)。第三張是主要造型方向，前兩張是服裝／型錄氛圍參考。它們尚不是乾淨人像或已核准的 identity reference。
- 目前角色外觀是參考方向；本機生成一張單人、乾淨背景的 master reference 並由使用者確認前，角色身份狀態維持 pending，不得宣稱已有穩定身份模型或已訓練 LoRA。

## 固定生成模型與本機狀態

- 使用者已選定唯一目標模型：Black Forest Labs **FLUX 3 Image Klein**；專案 key：`flux3-image-klein`；預期本機 MFLUX 命令：`mflux-generate-flux3` 與 `mflux-generate-flux3-edit`。
- 每批素材固定模型 revision、runtime 版本、工作流版本、提示詞 skill 版本與尺寸設定。每項輸出記錄模型 revision／權重雜湊、runtime、工作流 fingerprint、seed、角色參考圖雜湊、商品來源圖雜湊及輸出雜湊。
- 固定 checkpoint 不保證人物一致。每張需引用同一張經使用者核准的身份主圖，並維持角色與服裝限制；seed 只用來重現單張結果，不代替身份參考。
- 只用本機模型，不切換雲端 provider。FLUX 3 官方建議使用完整場景 caption 與 prompt upsampler；擴寫器必須是已安裝且經確認的本機模型，不把人像參考圖或提示送往雲端擴寫服務。
- **目前生成狀態：BLOCKED。** 本機 AIPS creative adapter 尚未支援 `flux3-image-klein`；目前安裝的 MFLUX 為 0.22.0，沒有 `mflux-generate-flux3` 命令，且本次 sandbox 中 Metal 裝置不可用。不得以未登錄命令、改名包裝器或雲端生成繞過檢查。AIPS adapter、runtime、權重和本機 prompt upsampler 通過 preflight 後，才可生成。
- FLUX 3 Image Klein 使用 FLUX Kommunity License。若要把成果公開用於商店，先依官方授權確認符合資格及相關限制；輸出例外不代表取得模型微調或權重散布授權。[官方模型卡與授權](https://huggingface.co/black-forest-labs/flux-3-image-klein)／[BFL 模型說明](https://bfl.ai/blog/flux-3-image-klein)

## 商品穿搭圖規則

- 每次只生成一張獨立商品照片，不要多姿勢拼圖、接觸表、漫畫、插畫、文字、商標、水印或多商品拼接。
- 來源商品照片是商品形狀的主依據。提示只改變模特穿著、姿勢、鏡頭與背景，不得改領口、袖長、衣長、口袋、縫線、釦子、圖案、材質外觀或色彩。參考編輯要盡量保留原商品，不能用文字提示重畫商品取代來源圖。
- 商品卡用一致的 1:1 或接近方形構圖；模特需呈現足夠服裝範圍，頭部、袖口、裙褲下襬與商品關鍵細節不可被裁切。首頁 hero 才使用寬幅橫式構圖。
- 先做上衣、下身、洋裝各一款的三張 pilot。舊有 9 張拼貼／插畫混入輸出皆為淘汰素材，不得回收作商品圖。
- 尺寸、價格、顏色名稱與庫存只由 Catalog API／正式商品資料顯示；不得從模特體型推算尺寸或宣稱合身。合成穿搭照片不能冒充真人實穿、授權代言或換裝功能。

## 自然人像提示骨架

採用專案安裝的 BFL `flux-image-best-practices` Skill：先寫人物與動作，再寫媒材、場景、光線與鏡頭；用短篇敘事而非堆疊形容詞。FLUX 不使用 negative prompt，改用正向描述具體說明希望看見的效果。以下提示是角色主圖與穿搭照的共同基底；每張再指定一個動作，不要求拼圖：

```text
An unstaged Japanese everyday-fashion catalog portrait of the same fictional adult East Asian woman, age 25, as the identity reference. Long dark-brown hair, light bangs, a small pink flower clip. Caught between poses, she turns slightly toward a window while adjusting one sleeve, with a small genuine smile and relaxed gaze. Soft morning window light, gentle uneven shadows, visible natural skin texture and a few flyaway hairs. Eye-level 50mm photograph, warm quiet home interior; candid and believable, garment clearly visible.
```

穿搭照需把身份主圖與商品來源圖分別標示為 identity reference、product reference；逐款只補來源可確認的服裝細節，保留領口、袖長、衣長、圖案、材質視覺及顏色。姿勢輪替使用「整理袖口並看向窗邊」「手持一朵小花、微微側身」「坐姿回頭露出淡笑」「行走中回望、裙襬自然擺動」等單一動作。避免 `perfect/flawless skin`、過度銳利、過度磨皮和靜止證件照姿勢；以自然膚理、少量碎髮、柔和但不均勻的窗光和微小表情變化增加生活感。畫面保持一位成年模特、一套穿搭、一張照片，商品完整清楚。

## 驗收與來源紀錄

逐張人工檢查角色臉部／髮型一致、確實只有一張照片、商品輪廓與來源相符、色彩可辨、服裝完整入鏡、手部與五官自然、背景不搶商品、解析度與裁切符合型錄。任何商品細節漂移或身份漂移都拒收，不以平均分數抵銷關鍵錯誤。

每張核准圖保留來源商品 ID／原圖雜湊、model ID／revision、runtime／workflow 版本、角色參考雜湊、seed、尺寸、日期、輸出雜湊與人工檢查狀態。避免保存聊天全文、原始提示全文或不必要的人像資料；提示可用 fingerprint 追溯。商店瀏覽只提供已審核的靜態圖，不在顧客瀏覽時呼叫模型推論。

### 目前本機狀態

2026-10-23 唯讀 `aips creative discover` 顯示 ComfyUI `UNAVAILABLE`、MFLUX 命令版本探測失敗、模型 `NOT_VERIFIED`。AIPS 0.83.0 的固定 executor 未登錄 FLUX 3；MFLUX 0.22.0 亦沒有 FLUX 3 CLI。模型選擇與提示詞已更新，**尚未完成 runtime 切換或生圖**。不得直接修改全域 AIPS executor 或繞過 `creative_execution`；須由受支援的 AIPS 更新提供經審核的 FLUX 3 adapter，再完成本機預檢。
