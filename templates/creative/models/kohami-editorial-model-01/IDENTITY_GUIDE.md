# KOHAMI Sweet Street Model 身份指南

## 角色定位

- 角色 ID：`kohami-editorial-model-01`，虛構的 18 歲成年日籍女性服飾型錄模特。
- 身份主圖：[`04-identity-master-selected.png`](references/04-identity-master-selected.png)。這是使用者選定的第三張生成角色圖，也是目前唯一的臉部身份錨點。
- 視覺方向：日系清新甜美、自然生活感、柔和窗光與日常咖啡店／居家場景；穿搭以 KOHAMI 商品為主。
- 核心辨識點：柔和橢圓臉、自然比例、深棕色長髮、輕薄空氣瀏海、深色眼睛、淡妝、暖中性膚色，以及帶一點笑意的放鬆表情。

## 參考圖片用途

| 圖片 | 用途 | 身份鎖定狀態 |
| --- | --- | --- |
| `04-identity-master-selected.png` | 臉型、五官、髮色／髮長、瀏海與整體年齡感 | 使用者選定的唯一身份主圖 |
| `05-expression-window-smile.png` | 窗邊自然微笑與近景光線 | 補充表情參考；尚未由使用者逐張審核 |
| `06-pose-seated.png` | 坐姿、手部位置與安靜表情 | 補充姿勢參考；尚未由使用者逐張審核 |
| `07-pose-walking.png` | 全身比例、步行動態與回望表情 | 補充姿勢參考；尚未由使用者逐張審核 |
| `generation-runs/2026-10-25/qwen-image-2.1-edit/outputs/01-natural-girly.png` | 奶油白襯衫、鼠尾草綠裙與自然站姿 | 使用者核准的流程基準圖（2026-10-25） |
| `generation-runs/2026-10-25/qwen-image-2.1-edit/outputs/02-shibuya-soft-casual.png` | 粉色針織背心、格紋裙與回望步態 | 使用者核准的流程基準圖（2026-10-25） |
| `generation-runs/2026-10-25/qwen-image-2.1-edit/outputs/03-retro-feminine.png` | 花卉洋裝、針織外套與窗邊坐姿 | 使用者核准的流程基準圖（2026-10-25） |
| `01`–`03` 原始參考圖 | 品牌氛圍、穿搭及型錄構圖 | 不作為乾淨臉部身份圖；保留原檔 |

`05`–`07` 三張補充照使用固定本機 Z-Image-Turbo 純文字生圖，沒有把身份主圖傳入模型。`08`–`10` 三款穿搭概念由 Qwen Image 2.1 編輯 profile 直接引用身份主圖生成；使用者於 2026-10-25 核准這三張作為未來穿搭流程的基準案例。這代表流程與本次樣圖獲接受，不保證未來每次生成都完全一致。

## 生圖固定規則

1. 一張圖只放一位成年模特、一套服裝與一個自然動作；以真實相機感、自然膚理、細碎髮絲、柔和但不均勻的日光呈現。
2. 每次只變更商品、姿勢、表情或背景中的必要項目。臉型、五官比例、深棕長髮、空氣瀏海、膚色與 18 歲成年感保持不變。
3. 服裝細節以商品來源圖為準。型錄照需呈現衣領、袖口、衣長、圖案、材質視覺和顏色；不自行增添商品資訊。
4. 使用繁忙度低的居家、日系咖啡店或簡潔棚拍背景。姿勢像攝影師捕捉到的生活瞬間，避免僵硬正面站姿。
5. 本角色的穿搭生成以 `profiles/qwen-image-2.1-edit-int8.yaml` 為首選本機流程；每次都把 `04-identity-master-selected.png` 作為 `image_1`，每張由身份主圖重新開始，不以前一張穿搭照串接，以降低臉部漂移。專案一般生圖預設仍為 AIPS `z-image-turbo`。不得自動切換、下載權重、使用雲端或自動 fallback。
6. 執行前先讀 [`docs/agents/model-image-generation.md`](../../../../docs/agents/model-image-generation.md)，並通過該 profile 的即時 preflight。
7. Qwen profile 已通過本機 runtime／權重／輸入輸出預檢，三張概念圖已由使用者核准；未來每批仍須人工檢查身份與成品，不得宣稱完全身份鎖定，也不得把提示詞或固定 seed 當作身份鎖定。
8. Qwen Research License 商用資格確認前，生成圖僅供內部概念使用；沒有商品來源圖時不得宣稱精確 SKU。每張輸出記錄模型 ID、workflow／profile fingerprint、seed、尺寸、輸出雜湊和人工審核狀態。依使用者明確要求，本角色的完整生圖提示只保存於 [`PROMPT_LIBRARY.yaml`](PROMPT_LIBRARY.yaml)；不保存聊天記錄。

## 驗收

逐張檢查臉部與髮型是否接近身份主圖、年齡是否仍明確為成人、表情姿勢是否自然、手部與五官是否合理、商品是否忠實、構圖是否完整。關鍵身份或商品細節漂移就退回重做。補充照待使用者確認後，才標記為已接受參考。

## 可追溯資料

- 角色特徵與圖檔雜湊：[`../../CHARACTER_PROFILE.yaml`](../../CHARACTER_PROFILE.yaml)
- 穿搭影像方向：[`../../STYLE_PROFILE.yaml`](../../STYLE_PROFILE.yaml)
- 每張生成圖的本機執行資料與審核狀態：[`../../CHARACTER_ARTWORK_MANIFEST.yaml`](../../CHARACTER_ARTWORK_MANIFEST.yaml)
- 生成政策與模型固定規則：[`../../../../docs/agents/model-image-generation.md`](../../../../docs/agents/model-image-generation.md)
