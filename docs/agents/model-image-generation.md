# KOHAMI 模特與本機生圖規範

此文件是未來 KOHAMI 商品型錄 AI 生圖的角色與執行指引。畫面方向以[本機展示視覺系統](../planning/kohami/experience/VISUAL_SYSTEM.md)為準；API、價格、SKU、庫存及模特換裝功能均由各自的正式契約裁決。

## 模特角色鎖定

- 角色 ID：`kohami-editorial-model-01`。她是虛構、明確成年的 18 歲日籍女性型錄模特；不是任何真實人物或會員。角色資料以[身份指南](../../templates/creative/models/kohami-editorial-model-01/IDENTITY_GUIDE.md)及[角色 profile](../../templates/creative/CHARACTER_PROFILE.yaml)為準。
- 固定辨識點：柔和橢圓臉、自然五官比例、深棕色長髮、輕薄空氣瀏海、暖中性膚色、深色眼睛與低彩度自然妝感。維持明確成年年齡感與自然成人比例；表情可有細微變化，不固定使用髮飾。
- 服裝方向：帶日式甜美感的成年女性日常女裝型錄；合身但不緊繃，層次輕盈，燕麥、米白、霧玫瑰、鼠尾草、柔和橄欖、石板藍等低彩度色系。姿勢和表情像被攝影師捕捉到的自然瞬間；不性感化、不過度修身、不使用校服或暗示未成年。
- 場景方向：簡潔居家、日系咖啡店或小型棚拍空間，柔和窗光、自然膚質與溫暖白色平衡；背景克制，不搶服裝焦點。使用者已選定第三張生成角色作為身份主圖；其他使用者提供的原始參考照只供品牌／穿搭氛圍參考。
- 角色資料夾位於 [`kohami-editorial-model-01`](../../templates/creative/models/kohami-editorial-model-01/)；其中 `04-identity-master-selected.png` 是身份主圖，`05`–`07` 是本機生成的表情與姿勢補充照。補充照使用純文字生圖，尚待使用者逐張檢視，也未證明每張都是同一張臉。
- 身份主圖已由使用者選定，但固定 Z-Image-Turbo workflow 是純文字生圖，不能輸入主圖作身份條件。不得宣稱跨姿勢身份鎖定或已訓練 LoRA；真正需要固定同一張臉時，先註冊並預檢支援身份圖輸入的本機 profile。

## 固定生成模型與本機狀態

- 目前唯一 active 目標仍是 Tongyi-MAI **Z-Image-Turbo**；專案邏輯 key 與 AIPS ComfyUI profile 均為 `z-image-turbo`。固定 workflow topology 為 `z_image_split_loader`，只支援本機文字生圖；不得自動切換或 fallback。
- 固定本機元件：`z_image_turbo_bf16.safetensors`、`qwen_3_4b.safetensors`（`lumina2`）及 `ae.safetensors`。ComfyUI Desktop 0.42.5 的 live catalog 已列出三項元件；固定 sampler 為 8 steps、CFG 1、`res_multistep`／`simple`。
- 額外接入一個明確 opt-in 的本機候選 profile：[`flux3-image-klein-distilled-int8.yaml`](../../templates/creative/models/kohami-editorial-model-01/profiles/flux3-image-klein-distilled-int8.yaml)，使用專案固定 workflow [`flux3-image-klein-distilled-api.json`](../../templates/creative/models/kohami-editorial-model-01/workflows/flux3-image-klein-distilled-api.json)。該 profile 透過 AIPS `dynamic_local`，僅在非 Git EPHEMERAL workspace 中使用；ComfyUI 本機 schema／權重預檢通過，固定為 16 steps、CFG 1、Euler／Simple，保留 Qwen 9B FLUX prompt upsampler，但將 TextGenerate 限制為 48 tokens／最多 32 個英文單字，以避免本機 MPS 長時間自回歸生成。它不會取代目前 active 的 Z-Image-Turbo，亦不會自動 fallback。
- 本角色 `kohami-editorial-model-01` 的穿搭生成流程已於 2026-10-25 經使用者核准，以本機 Qwen Image 2.1 身份圖編輯 profile 作為首選；身份主圖固定放在 `image_1`，每張從主圖獨立編輯，逐次先通過 live preflight。這是角色專用工作流優先設定，不變更其他用途的 `z-image-turbo` 專案預設。
- 新增本機身份圖編輯 profile：[`qwen-image-2.1-edit-int8.yaml`](../../templates/creative/models/kohami-editorial-model-01/profiles/qwen-image-2.1-edit-int8.yaml)，workflow 為 [`qwen-image-2.1-edit-api.json`](../../templates/creative/models/kohami-editorial-model-01/workflows/qwen-image-2.1-edit-api.json)。ComfyUI 0.42.5 本機 schema、三項權重、identity image staging hash 與 AIPS preflight 均通過；2026-10-25 以身份主圖為 `image_1` 完成三張編輯概念照，MPS 單張耗時約 160–189 秒。第一張因隔離 workspace 的自動 Style Profile 預設動漫角色設定集而視覺拒收；明確改為單張寫實攝影 profile 後，三張成功輸出均為單張寫實穿搭照片。三張概念照已由使用者核准為此角色未來穿搭流程的基準案例；小樣本不保證未來身份完全一致。身份圖編輯只用於穿搭概念；沒有商品來源圖時，不代表精確 SKU 商品照。
- Qwen Image 2.1 權重採 Qwen Research License；商業資格未確認前，所有輸出僅限內部概念測試。此角色穿搭流程依使用者核准優先使用 Qwen；其他用途仍以 `z-image-turbo` 為預設。不得自動改模型、雲端 fallback 或下載其他權重。
- FLUX 3 Klein profile 只支援文字生圖，沒有身份圖或商品圖輸入 binding；它不能保證不同姿勢中的同一人臉部一致，也不能取代人工核准身份參考後的商品編輯工作流。ComfyUI catalog 不提供已安裝權重雜湊。2026-10-24 首次推論停在 Qwen 9B TextGenerate prompt upsampler：Apple MPS 記錄 40 分 44 秒生成 262/1024 tokens（約 7.5 秒/token），接著工作被中斷，沒有到 KSampler，也沒有圖片輸出。直接提示詞繞過 upsampler 的驗證圖在 71 秒完成，但輸出成插畫拼貼，品質拒收。恢復 upsampler 並限制 48 tokens／32 個英文單字後，ComfyUI 在 Apple MPS 上於 571.6 秒完成單張寫實照片；AIPS Bundle timeout_seconds 應設 900 秒保留執行餘裕。該 FLUX 候選使用舊版角色特徵，不代表目前選定的模特身份；此 profile 不是 active 模型。
- FLUX 3 Image Klein 權重採 FLUX Kommunity License；BFL 說明年營收低於 US$5M 的個人／公司可免費商用，其他條件須依完整授權確認。KOHAMI 商用資格未核實前，不把此 profile 用於商業成品。[BFL 官方說明](https://bfl.ai/blog/flux-3-image-klein)
- 2026-10-24 固定 Z-Image-Turbo profile 再完成三張本機補充照：窗邊近景微笑、咖啡店坐姿、咖啡店外全身行走。ComfyUI Desktop 0.42.5 各張輸出 1024×1024；逐張耗時 38.015 秒、37.485 秒、41.211 秒。輸出與 profile／workflow／seed／提示 fingerprint 記錄於 [`CHARACTER_ARTWORK_MANIFEST.yaml`](../../templates/creative/CHARACTER_ARTWORK_MANIFEST.yaml)。這證明三張工作成功完成，不代表跨姿勢身份一致或商品品質已驗證。
- 推論時 runtime 為 ComfyUI Desktop 0.42.5、輸出 1024×1024、seed 24102402、8 steps；輸出 SHA-256 為 `47e4778b8f12e0267dfac39b80ce643dfdbd9567167b6f04a120c451f3faf87e`。權重檔雜湊未由 ComfyUI catalog 提供，勿把檔名當作權重 SHA-256。
- 固定 checkpoint 與 seed 都不能保證人物一致。身份主圖已選定，未來需引用同一張圖；但只有具備影像輸入能力的本機 workflow 真正接收該圖，才可測試身份條件生成。
- 只用本機 ComfyUI，不切換雲端 provider。每次執行前使用非 Git EPHEMERAL workspace 建立 Bundle，明確選擇當次已核准的 profile 並通過 live preflight；不得以自動 fallback 替代。新工作流須先經 AIPS dynamic-local schema／權重驗證。現行文字生圖 profile 僅適用於風格與姿勢概念稿，不以它宣稱同一模特穿搭批次已固定。
- Z-Image-Turbo 官方模型卡標示 Apache-2.0；商業使用前仍需遵守授權全文及商店素材審核規則。[官方模型卡](https://huggingface.co/Tongyi-MAI/Z-Image-Turbo)

## 商品穿搭圖規則

- 每次只生成一張獨立商品照片，不要多姿勢拼圖、接觸表、漫畫、插畫、文字、商標、水印或多商品拼接。
- 來源商品照片是商品形狀的主依據。提示只改變模特穿著、姿勢、鏡頭與背景，不得改領口、袖長、衣長、口袋、縫線、釦子、圖案、材質外觀或色彩。參考編輯要盡量保留原商品，不能用文字提示重畫商品取代來源圖。
- 商品卡用一致的 1:1 或接近方形構圖；模特需呈現足夠服裝範圍，頭部、袖口、裙褲下襬與商品關鍵細節不可被裁切。首頁 hero 才使用寬幅橫式構圖。
- 先做上衣、下身、洋裝各一款的三張 pilot。舊有 9 張拼貼／插畫混入輸出皆為淘汰素材，不得回收作商品圖。
- 尺寸、價格、顏色名稱與庫存只由 Catalog API／正式商品資料顯示；不得從模特體型推算尺寸或宣稱合身。合成穿搭照片不能冒充真人實穿、授權代言或換裝功能。

## 自然人像提示骨架

Z-Image-Turbo 固定 ComfyUI workflow 以單一正向 prompt 產圖，沒有 negative-prompt 欄位。用自然短句先描述人物與單一動作，再補服裝、場景、光線和鏡頭；將成品描述為一張完整連續的寫實照片。以下是文字生圖的視覺基底，不提供身份鎖定；每張再指定一個動作：

```text
An unstaged Japanese everyday-fashion catalog photograph of a fictional adult Japanese woman, age 18, with a soft oval face, natural facial proportions, long dark-brown hair and airy see-through bangs. Her minimal makeup, realistic skin texture and relaxed genuine smile feel fresh and sweet. She turns slightly toward a cafe window while adjusting one sleeve; a few loose hairs catch the soft morning light. Eye-level 50mm photograph, warm quiet Japanese cafe interior, gentle natural shadows, candid expression, outfit clearly visible in one continuous frame.
```

此角色穿搭生成使用本機 Qwen Image 2.1 edit workflow 作為首選。每張都以 `04-identity-master-selected.png` 為唯一身份輸入 `image_1`，由主圖開新編輯，不串接前張結果；若有商品來源圖，將商品來源圖分別指定為 product reference；逐款只補來源可確認的服裝細節，保留領口、袖長、衣長、圖案、材質視覺及顏色。每次使用一個動作，例如整理袖口、坐姿望向窗外或行走中回望；以自然膚理、少量碎髮、柔和窗光和細微表情增加生活感。文字生圖時可採用相同人物描述與主圖作人工比對，但不可宣稱模型直接沿用主圖身份。畫面保持一位成年模特、一套穿搭、一張照片，商品完整清楚。

## 驗收與來源紀錄

逐張人工檢查角色臉部／髮型一致、確實只有一張照片、商品輪廓與來源相符、色彩可辨、服裝完整入鏡、手部與五官自然、背景不搶商品、解析度與裁切符合型錄。任何商品細節漂移或身份漂移都拒收，不以平均分數抵銷關鍵錯誤。

每張核准圖保留來源商品 ID／原圖雜湊、model ID／revision、runtime／workflow 版本、角色參考雜湊、seed、尺寸、日期、輸出雜湊與人工檢查狀態。依使用者明確要求，此角色資料夾在 [`PROMPT_LIBRARY.yaml`](../../templates/creative/models/kohami-editorial-model-01/PROMPT_LIBRARY.yaml) 保存完整生圖提示、prompt hash、profile/workflow 快照與每次執行來源；不保存聊天全文、憑證或不相關的人像資料。其他生成工作仍以 fingerprint 追溯為預設。商店瀏覽只提供已審核的靜態圖，不在顧客瀏覽時呼叫模型推論。

### 目前本機狀態

2026-10-24 固定 `z-image-turbo` profile 已通過 ComfyUI 本機 preflight，且完成三張姿勢／表情補充照生成。2026-10-25 `qwen-image-2.1-edit-int8` profile 完成三款穿搭／姿勢身份圖編輯測試，使用者核准三張作為此角色未來穿搭流程的基準，並指定該流程為首選。這些文字描述穿搭不是商品照片，精確商品上架仍須輸入商品來源圖、逐張確認商品細節和人物身份，並先完成 Qwen Research License 商用資格審查。ComfyUI catalog 未提供權重檔 SHA-256。
