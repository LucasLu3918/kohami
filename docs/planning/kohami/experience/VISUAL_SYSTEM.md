# 本機展示視覺提案

日期：2026-10-24｜使用者已選第三張全頁畫布作為本輪視覺目標，並選定第三張生成角色作為模特身份主圖｜補充姿勢照已由本機生成，待審閱

## 本輪 GRL 商店樣板（2026-10-21）

使用者以第三張 KOHAMI 全頁畫布校準本輪方向：寬幅日系型錄主視覺、左側短文案／右側模特照片、白底商品區，以及方形偏橫的穿搭商品圖與簡潔色票／尺寸按鈕。保留 KOHAMI 名稱、現有商品、價格、SKU、庫存與購物流程；不沿用畫布中虛構商品資料。GRL 仍是商品探索層級的參考，不複製第三方品牌、真人照片或交易宣稱。

| 畫面／元件 | 本輪基準 |
| --- | --- |
| 共用 shell | 白底與淡玫瑰色表面、灰黑文字、細灰分隔線；KOHAMI 字標左置、導覽右置。保留必要的本機模擬／合成資料提示，但以窄幅淡色帶呈現 |
| 首頁主視覺 | 滿版寬幅照片，左側標題／短文案／主要 CTA，右側同一位成年合成模特穿著主推商品；手機先文案、後圖片，避免遮字或裁掉服裝 |
| 新品列表 | 標題先於搜尋與篩選列，商品以四欄桌面／三欄平板／兩欄手機呈現；卡片無外框，穿搭圖一致裁切，商品名、色票、尺寸、價格與庫存清楚分層 |
| 商品詳情 | 桌面左側大圖／縮圖、右側商品名／新台幣價格／顏色尺寸／庫存與加車；下方材質、衣服尺寸表及相關導覽；手機改為圖在上、資訊在下 |
| 管理登入 | 同品牌與灰白表單語彙，明確標示管理登入；帳密欄位、送出、loading／失敗、登出後狀態；不沿用消費者註冊／購物金活動區 |
| 管理清單 | 共用輕量管理 shell；商品、SKU、發布狀態、價格與庫存的唯讀資訊，沒有本版未授權的編輯／發布／調庫存按鈕 |

首輪在 1440px 桌面與 390px 手機審閱首頁、詳情、管理登入；另檢查窄螢幕與鍵盤操作。版型優先對齊 GRL 的內容寬度、欄位比例、網格密度、字級及色彩；不以不同的合成圖片逐像素相等作通過條件。色彩取樣與對比需實測，不能從參考站外觀推定可及性已通過。不複製 GRL 名稱、真人照片、第三方支付／物流標章或海外代購浮層為本專案能力。

### 商品素材與價格呈現

- 保留合格的 15 款平拍圖；至少涵蓋上衣、針織、裙／褲、洋裝及外套。每款須有獨立 Product、正確圖文、SKU、顏色、衣服尺寸與庫存，不以同款尺碼湊 15 個商品。
- 依 2026-10-23 新指示，重新啟動 3 款日系型錄穿搭樣張試作（上衣、下身、洋裝）；先前 9 張拼貼／插畫混入素材維持淘汰，不回填商品。
- 2026-10-24 使用者將唯一目標模型改為 Tongyi-MAI **Z-Image-Turbo**，專案識別 `z-image-turbo`。採 AIPS 固定 ComfyUI profile `z-image-turbo`／`z_image_split_loader`，使用本機 `z_image_turbo_bf16.safetensors`、`qwen_3_4b.safetensors` 與 `ae.safetensors`；固定 8-step sampler，不 fallback、不走雲端。模型卡標示 Apache-2.0，公開商用前仍要遵守授權及素材審核規範。[官方模型卡](https://huggingface.co/Tongyi-MAI/Z-Image-Turbo)。
- 2026-10-24 使用者選定第三張生成角色作為身份主圖，存於 [`kohami-editorial-model-01/references/04-identity-master-selected.png`](../../../../templates/creative/models/kohami-editorial-model-01/references/04-identity-master-selected.png)。固定 Z-Image-Turbo profile 通過本機 preflight；另完成三張 1024×1024 姿勢／表情補充照。它們以純文字方式生成，沒有輸入身份主圖，故尚未證明跨姿勢或穿搭的身份一致性；權重檔 SHA-256 仍無可觀測值。
- 人物、服裝語彙、提示組裝與拒收標準集中於[模特與本機生圖規範](../../../agents/model-image-generation.md)及[身份指南](../../../../templates/creative/models/kohami-editorial-model-01/IDENTITY_GUIDE.md)。後續商品照需採支援身份圖輸入的本機 workflow，並逐張核對模特臉部、髮型、妝感、柔和日光、場景和商品輪廓；未完成身份一致性驗收前，不把補充照標示為同一身份鎖定成果。
- 每張穿搭圖都要與對應商品原圖逐項比對領口、袖口、釦子、縫線、圖案、材質視覺、衣長與顏色；任何差異足以改變商品認知即退件。平拍／細節原圖保留作為商品準確性參照，不以生成模型替代來源資料。
- 素材採本地批次生成、挑選、壓縮後靜態提供；商店瀏覽不等待模型推論。既有圖檔原尺寸 1024×1024；對卡片及大圖產生合適變體、保留 aspect ratio／width／height，首屏主圖與下方 lazy loading 分別處理。
- 2026-10-25 使用者核准 Qwen Image 2.1 身份圖編輯流程為 `kohami-editorial-model-01` 穿搭生成首選，身份主圖固定為 `image_1`；三張基準照與逐次 preflight／授權限制見[模特生圖規範](../../../agents/model-image-generation.md)。這項角色專用選擇不改變其他用途仍採 `z-image-turbo` 的專案預設。
- 首輪保存產品資產所需的模型／工作流版本、seed、生成日期與來源／用途證據；這是可重製素材紀錄，不記錄聊天全文或原始工具 trace。未確認來源的既有圖片不能僅憑頁面宣告視為已驗證。
- 預設繁中與 TWD，保留既有三語資料。GRL 可參考款式、價格階梯、顏色／尺碼呈現；例如查閱商品的日圓售價是 ¥1,499，不能直接拿相同數字當新台幣售價或假稱自動匯率換算。先提商品價格表，再經後端資料匯入維持卡片／詳情／quote／order 一致；展示材質／衣服尺寸維持合成估算標示。
- 人像穿搭圖不宣稱真實模特、實際合身或可換裝；不擴及 Fitting 的素材權利與三視圖相容性。

Nuxt 沿用 Composition API／TypeScript。建議元件邊界為 StoreHeader／StoreFooter、HeroBanner、ProductCard／ProductGrid、ProductGallery、SkuSelector、AdminLoginForm／AdminProductList；route 組裝功能元件，API／session 狀態由既有或功能 composable 協調。價格、庫存與權限仍由後端裁決。執行順序及採用決策見 [主計畫](../delivery/IMPLEMENTATION_PLAN.md#0-本輪可-demo-收斂計畫2026-10-21)。

## 方向與範圍

承接既有日系服飾品牌與半寫實日系型錄角色方向。商店建議採留白、米白背景、深色文字、莓色重點；商品實拍／合法示意素材為主，避免視覺模擬暗示真人合身。KOHAMI名稱、商標及字標權利仍待查核，不建立虛構認證或物流合作標章。

此為可直接試作的設計提案，未產生完成畫稿、商標或素材授權。既有角色風格不代表以下商店色票已獲採用。

| Token／元件 | 建議值／行為 |
| --- | --- |
| VIS-01 色彩 | background #FAF7F2、surface #FFFFFF、text #262326、muted #5F5860、accent #83334F、border #D8D0D5；所有實際配對量測對比，不以色碼聲稱合規 |
| VIS-02 字體 | 使用具繁中／日文／拉丁覆蓋的系統sans-serif堆疊；本文16px起、行高1.6，標題依層級；不新增付費字型或外部追蹤請求 |
| VIS-03 間距 | 4px基礎，常用8/12/16/24/32；表單群組可辨，手機頁邊16px，桌面內容最大1200px |
| VIS-04 控制 | 常用點擊目標至少44×44 CSS px作UX建議；focus可見且不被固定列遮住，禁用有文字原因 |
| VIS-05 商品卡 | 圖片保留比例與尺寸避免位移，價格／SKU／缺貨文字清楚；換裝資格獨立標示 |
| VIS-06 按鈕 | 每區一個主要動作；危險操作附確認；loading仍保留label，後端冪等不依賴disabled |
| VIS-07 狀態 | badge＋文字＋下一步；待確認不能用紅色「失敗」，mock完成不使用「已入帳」 |
| VIS-08 表格 | 後台交易可掃讀；小螢幕改摘要卡或局部可辨橫向捲動，不讓整頁難以操作 |
| VIS-09 圖片 | 合法授權、用途、版本及替代文字；不能以生成／未審核素材取代真實商品準確性 |
| VIS-10 動態 | 非必要動態可關閉，遵循prefers-reduced-motion；不使用輪播／動畫阻擋購買 |

## 元件狀態與審閱方式

每個元件需設計 default／hover／focus／disabled／loading／error，表單另有valid與read-only。Checkout Summary、Order Status Timeline、Refund Confirmation、Demo Banner、Permission Denied、Empty State為共用元件，不在不同頁發明矛盾文案。

下一個可審閱成果建議為首頁、商品頁、結帳、訂單、管理退款五個桌面／手機線框與色彩樣式；本輪是文字線框與token提案，不聲稱視覺稿已完成。素材未備妥可用明示占位圖驗證布局，不能公告真實商品／換裝資格。追蹤 [畫面規劃](EXPERIENCE_DESIGN.md) SCR-01–09與 [品質規劃](../../../quality/QUALITY_PROFILE.yaml)。
