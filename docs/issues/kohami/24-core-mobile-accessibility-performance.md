# 24: 核心購物的鍵盤可及性與效能驗收

**What to build:** 桌面瀏覽器可用鍵盤完成核心購物旅程；以明確記錄的本機條件量測核心頁效能並修正問題。iPhone Safari 與 VoiceOver 人工驗收延後為[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)，不作本票目前結案門檻。

**Blocked by:** 17：三語購物旅程與可索引網址；22：購物政策與客服入口

**Status:** ready-for-agent

**Work status:** claimed

- [x] 以桌面瀏覽器鍵盤驗證首頁、搜尋、SKU、購物車、報價、結帳及付款結果的操作、焦點與錯誤／恢復狀態；響應式尺寸維持自動化 viewport 覆蓋。
- [ ] 對三語核心頁量測 LCP P75 ≤ 2.5 秒、INP P75 ≤ 200 ms、CLS P75 ≤ 0.1，記錄硬體、瀏覽器、網路、樣本、期間與限制；不以本機結果宣稱正式 SLA。
- [x] 核心旅程成功、拒絕、等待確認與失敗恢復可操作，修復發現問題後重驗受影響旅程。
- [ ] 樣本不足或量測條件未確認時列為未驗證，不以單次開發機量測宣稱正式營運達標。

## M4規劃追蹤（2026-10-13）

參照[M4驗收／故障復原](../../planning/kohami/delivery/VALIDATION_PLAN.md)與[本機操作](../../planning/kohami/delivery/LOCAL_DEMO_RUNBOOK.md)。本輪補齊規劃與證據模板，產品NOT_EXECUTED；原驗收清單、依賴及工作狀態維持。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：N01；User Stories 6–7；核心 R01–R07／AC01–AC07。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Resolution（2026-10-18；已完成子範圍與延後選配）

- 共用 Storefront 應用殼新增繁中／日文／英文略過導覽連結；首頁、商品、購物車、結帳、會員、政策、客服及後台頁面皆提供可聚焦的 `main-content` 目標。
- 新增 `apps/storefront/tests/issue24-accessibility.spec.ts`：在 390×844 viewport 的三語政策頁驗證略過連結是第一個 Tab 停靠點、鍵盤啟用後焦點移至主內容、網址片段正確且沒有水平溢位。先確認舊版因找不到連結而失敗，再實作後 PASS，Playwright 1/1。
- `npm run typecheck` PASS；相依 Issue 22 `tests/issue22-policies-support.spec.ts` 回歸 PASS 3/3，涵蓋三語頁面、390px 寬度及既有鍵盤焦點。
- 當時的略過導覽子範圍不代表完整無障礙或效能驗收；對比、200%／400% 縮放及全核心旅程錯誤／等待／恢復仍待桌面驗收。正式硬體／網路矩陣及正式 SLA 均未驗。Issue 24 維持未結案。
- iPhone Safari 與 VoiceOver 人工驗收已依目前決策從本票必備範圍移除，詳見[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。先前裝置連線與截圖僅作歷史背景，不代表裝置可及性通過。

### 2026-10-18 後續：結帳狀態的鍵盤恢復與本機效能樣本

- 鍵盤驗收發現結帳結果頁在重新查詢時把按鈕設為原生 `disabled`，Chrome 會移除其焦點。改以 `aria-disabled`／`aria-busy` 呈現忙碌狀態，既有 `refreshing` 守門仍阻止重複請求；完成後焦點留在原按鈕。
- 新增 `apps/storefront/tests/issue24-checkout-keyboard.spec.ts`：以 Tab／Enter 操作狀態刷新，驗證 Processing、Succeeded、Failed、Unknown 與 503 恢復；焦點保留、Unknown 不提供重新啟動付款、最後成功讀取的金額快照保留。新案例 PASS 1/1；Issue13 結果／失敗恢復及 Issue24 三語略過導覽合併回歸 PASS 4/4；`npm run typecheck` PASS。
- 本機首頁診斷取樣 30 次：Mac arm64、Chrome 155.0.8059.84、1280×800、本機 HTTPS、無網路／CPU 節流、合成 API mock。LCP P75 40 ms、CLS P75 0；Event Timing 記錄到 ≥16 ms 互動事件 25/30 次，該子集 P75 24 ms。INP 數值不完整，因事件門檻排除了較短事件；結果僅供本機比較，不能代表完整 Web Vitals、其他頁面、iPhone 或正式網路／SLA。
- 歷史上未完成 Safari／VoiceOver 實機人工驗收；此項已移至選配，不作目前必備門檻。從首頁至售後的桌面鍵盤旅程、已確認網路矩陣及多核心頁效能樣本仍待完成；Issue24 未結案。

### 2026-10-18 桌面鍵盤核心旅程與多頁 LCP／CLS 診斷

- 新增 `apps/storefront/tests/issue24-core-keyboard-journey.spec.ts`：以 Tab／Enter／Space、搜尋輸入及原生選單按鍵完成略過導覽、商品搜尋、SKU 選擇、加入購物車、試算報價、確認條件與建立合成結帳。`npx playwright test tests/issue24-core-keyboard-journey.spec.ts --config=playwright.config.ts --reporter=line --trace=off` PASS 1/1；Issue24 略過導覽／Checkout 鍵盤及 Issue13 結果回歸合併 PASS 5/5；`npm run typecheck` PASS。
- 桌面本機診斷每頁 30 次：首頁、政策、購物車、結帳；Mac arm64、Chrome 155.0.8059.84、1280×800、本機 HTTPS、無網路／CPU 節流、合成 API route stub、同一瀏覽器 context／暖快取。四頁各 30/30 有 LCP 與 CLS 樣本；各頁 LCP P75 24 ms、CLS P75 0。
- 此次沒有量 INP，也未測日文／英文頁、錯誤旅程、真 API／SQL、行動尺寸效能或真實網路；暖快取與未節流的開發環境數字只供本機比較，不證明正式 Core Web Vitals 或 SLA。完整桌面鍵盤錯誤／售後旅程、Q-01 固定 viewport／冷熱情境、對比／縮放與行動裝置人工驗收仍未完成。

### 2026-10-18 後續範圍決策

- 使用者決定先排除 iPhone 與 VoiceOver 人工驗收；需要時再依[選配改善計畫](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)評估與執行。
- 桌面鍵盤旅程與效能量測仍屬 Issue24 目前範圍。自動化響應式 viewport 不等同 iPhone 實機或 VoiceOver 驗收。

### 2026-10-18 後續：桌面鍵盤失敗恢復與本人售後

- 新增 `apps/storefront/tests/issue24-keyboard-error-aftercare.spec.ts`：以鍵盤操作，驗證報價與建立訂單遇到 503 後保留 Email／確認狀態、焦點可留在可重試按鈕並成功重試；本人訂單查詢的 404／503 使用安全訊息，保留查詢值；退貨申請遇到 503 時保留表單值與焦點，接著可成功重試並看到案件時間軸。
- 為避免忙碌中的原生 `disabled` 移除焦點，報價、建立訂單與退貨申請按鈕以 `aria-disabled`／`aria-busy` 呈現忙碌狀態；操作 handler 同時拒絕重複送出。按鈕的表單／資料前置條件仍以原生驗證或禁用條件維持。
- `npm run typecheck` PASS。Playwright 合併執行 `tests/issue24-keyboard-error-aftercare.spec.ts`、`tests/issue24-core-keyboard-journey.spec.ts`、`tests/issue24-checkout-keyboard.spec.ts`、`tests/issue24-accessibility.spec.ts`、`tests/issue22-policies-support.spec.ts`：8/8 PASS。全部 API 使用合成 route stub，測試期間服務只綁 localhost，未連 SQL、真實付款或 LAN。
- 這只完成桌面報價／訂單建立錯誤，以及本人訂單查詢／退貨申請恢復子範圍；商品搜尋、加車錯誤、完整退貨後續操作、多語效能、INP、對比／縮放與 Q-01 冷熱情境仍未驗。Issue24 維持 `claimed`，不代表完整 WCAG AA 或正式 SLA 通過。iPhone Safari／VoiceOver 維持選配且未驗證。

### 2026-10-18 桌面三語核心頁冷／暖快取診斷

- 在 Mac arm64、Headless Chrome 155、1440×900、本機 HTTPS、無 CPU／網路節流下，量測繁中／日文／英文的首頁、政策與購物車；每個語系／頁面組合 30 次，冷快取 15 次、暖快取 15 次。冷快取每次導覽前清除 Chrome browser cache；暖快取沿用同一瀏覽器 context。沒有啟動 API／SQL，量測頁面為本機展示狀態。
- 九個頁面組合均取得 30/30 LCP 與 CLS 樣本。LCP P75 範圍 20–84 ms，CLS P75 範圍 0–0.0121，均低於 Q-01 的規劃目標；這些極低數值反映本機未節流的展示環境，不能代表正式使用者體驗或 SLA。
- 每次導覽點擊頁面標題一次；Event Timing observer 設定 16 ms 門檻，取得 251/270 次點擊互動候選，觀測子集 P75 為 16 ms。由於較快事件會被門檻排除、互動只包含單一標題點擊，這不是完整 INP 驗收。
- 另以 Headless Chrome 模擬 390×844 CSS viewport、deviceScaleFactor 1 與 touch：三語首頁、政策及購物車各 30 次，冷／暖各 15 次。LCP P75 為 28–84 ms、CLS P75 均為 0，Event Timing 點擊候選 270/270、子集 P75 32 ms；九組皆 0/30 水平溢位。這是響應式 viewport 自動診斷，不是 iPhone 實機驗收。
- 測量未涵蓋 API／SQL、購物操作成功與錯誤互動、其他頁面、真實網路、200%／400% 縮放或對比檢查；Event Timing 不等同完整 INP，因此 Q-01 與 Issue 24 維持未結案。iPhone 實機與 VoiceOver 人工驗收仍只列[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-18 後續：桌面重排、搜尋／加車失敗恢復與對比抽樣

- Playwright 首次重現三語首頁在 720px CSS viewport 的水平溢位：繁中／日文文件寬 830px、英文 903px。`ProductDiscovery.vue` 篩選列改在 900px 以下換成雙欄；回歸檢查三語首頁在 720px 與 320px viewport 均無水平溢位。這是縮小 CSS viewport 的重排模擬，不是瀏覽器原生縮放或實機驗收。
- 新增 `tests/issue24-accessibility.spec.ts` 案例，使用鍵盤操作搜尋與加車的合成 503 失敗及成功重試。Red 階段發現加車請求期間原生 `disabled` 會移除焦點；改用 `aria-disabled`／`aria-busy` 保留焦點，並在頁面 pending 時拒絕重複加車。失敗後焦點保持於加車按鈕，按 Enter 可重試成功。
- 驗證命令：`npm run typecheck` PASS；`npx playwright test tests/issue22-policies-support.spec.ts tests/issue24-accessibility.spec.ts tests/issue24-checkout-keyboard.spec.ts tests/issue24-core-keyboard-journey.spec.ts tests/issue24-keyboard-error-aftercare.spec.ts --config=playwright.config.ts --reporter=line --trace=off` PASS 10/10。Nuxt 僅綁 localhost，所有 API 由合成 route stub 回覆，未連 API／SQL／付款或 LAN。
- Headless Chrome computed-color 抽樣涵蓋三語首頁／政策／購物車的 187 個可見文字節點，沒有發現低於 WCAG AA 一般文字 4.5:1／大字 3:1 門檻的樣本。這不涵蓋所有動態狀態、焦點指示或非文字對比，不能視為完整 WCAG AA 審查。
- 互動延遲另取樣三語首頁搜尋、政策頁鍵盤略過導覽及購物車數量變更，各 30 次，共 270 次。16ms Event Timing 候選出現在 111/270 次樣本，記錄到 193 個不同互動候選；有候選的分組樣本最大延遲 P75 為 16ms。低於 16ms 的互動被 API 門檻排除，且環境未節流、API 為合成回覆，因此這仍不是完整 INP 或正式 SLA 證據。
- 後續在桌面 Chrome `/zh-TW/policies` 以 200%／400% 原生縮放人工複核：政策文字仍可閱讀與垂直捲動，當次未見水平捲軸；略過導覽、商店、客服與語言連結的焦點外框可見。限定色值計算樣本為正文／背景 14.36:1、次要文字／背景 6.21:1、導覽連結／頁面背景 9.68:1、焦點色／頁面背景 6.49:1。這只完成政策頁的桌面抽樣，不是全站縮放／對比或 WCAG 稽核。
- Issue24 維持 `claimed`。完整 INP、全站焦點／非文字對比、真實網路／硬體矩陣仍未完成；iPhone Safari／VoiceOver 維持[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-18 後續：購物車、結帳與會員訂單縮放等效重排

- 新增 `apps/storefront/tests/issue24-zoom-core-pages.spec.ts`，使用合成 API route stubs 覆蓋購物車商品／數量／試算按鈕、結帳狀態／金額，以及登入會員訂單／總額。以 1280px 桌面基準的 640px 與 320px CSS viewport 分別檢查 200%／400% 等效重排；必要標題、主要資料與控制項可見，允許垂直捲動，頁面及 body 均無水平溢位。Playwright PASS 1/1。
- 首次 320px 結帳結果斷言重現文件寬 342px；長付款期限令摘要 CSS Grid 定義欄位保留最小內容寬。結帳頁 `dd` 設 `min-width: 0` 與 `overflow-wrap: anywhere` 後，三頁在 640px／320px 均通過。`npm run typecheck` PASS。
- 驗收使用 localhost HTTPS Nuxt 與合成瀏覽器回應，未啟動或連接 API／SQL／付款服務，未讀寫 persistent fixture。Playwright 使用暫存設定與本機 Headless Chromium，專案既有 `channel: chrome` 在此執行環境無法啟動；此證據是 CSS viewport 等效檢查，不是這三頁以桌面 Chrome 原生縮放的人工簽核。先前原生 200%／400% 人工複核仍只涵蓋政策頁。
- 不涉及 API、資料、事件、交易或授權變更。iPhone 與 VoiceOver 仍維持未驗證並列於[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)；完整全站原生縮放／對比、INP、冷熱情境與正式 SLA 仍未完成，Issue24 維持 `claimed`。

### 2026-10-18 後續：桌面核心路由焦點與停止控制巡檢

- 新增 `apps/storefront/tests/issue24-focus-stop-sweep.spec.ts`，以桌面 Chrome 1280×900 和合成 API 回應，巡檢首頁、購物車、政策、客服及會員帳號五個繁中路由。每頁先以略過導覽連結進入主要內容，再逐一 Tab 到所有可見、可操作的連結與控制項，確認順序可達且焦點指示可見。
- 驗證 `npm run typecheck` PASS；Issue22 政策／客服及 Issue24 無障礙、結帳、核心購物旅程、錯誤恢復與新焦點巡檢合併 Playwright PASS 13/13。服務僅使用 localhost，API 由測試合成回應，未連 SQL、付款服務或 LAN。
- 原始碼巡檢未發現自動播放、計時動畫或持續移動內容，因此本次範圍沒有需要加入停止控制的對象。這是對目前 Storefront 原始碼的有限檢查，不代表全站 WCAG 稽核或其他瀏覽器／輔助科技驗證。
- 此次只覆蓋初始展示狀態的五個桌面路由；結帳結果與錯誤／重試焦點由既有鍵盤案例覆蓋。完整動態狀態、非文字焦點對比、原生縮放全站巡檢、iPhone 與 VoiceOver 仍未驗證；Issue24 維持 `claimed`。

### 2026-10-18 後續：購物車原生 400% 縮放

- 在桌面 Chrome 本機 synthetic 展示頁以原生 400% 瀏覽器縮放檢查購物車。修正前文字輸入框寬約 194 CSS px，超出約 120 CSS px 的父欄；文件 `scrollWidth=214px`、可視 `clientWidth=152px`，可重現水平溢位。
- 購物車一般輸入框與選單補上父欄寬度、零最小寬度；核取方塊保留原尺寸。修正後原生 Chrome 指標為 `innerWidth=156`、`devicePixelRatio=8`、`clientWidth=152`、文件 `scrollWidth=152`、body `scrollWidth=136`；購物車輸入框／選單均落在父欄內，無水平溢位。
- `npx playwright test tests/issue24-zoom-core-pages.spec.ts --config=playwright.config.ts --headed --reporter=line --trace=off` PASS 1/1，合成 API 回覆下購物車、結帳結果與會員訂單在 640／320／160 CSS px 均通過內容／控制項可見與水平溢位檢查；`npm run build` PASS。原生 400% 實測目前只涵蓋購物車；結帳與會員訂單的原生 400% `scrollWidth` 尚未逐頁量測，160px 是補充 CSS viewport 測試。
- 瀏覽器展示使用本機 HTTP 預覽與合成 API fixture；未連接 API／SQL／付款或 LAN。Issue24 仍為 `claimed`；全站縮放／對比及 iPhone 與 VoiceOver 仍未完成，後者維持[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。

### 2026-10-18 後續：桌面 Chrome 原生縮放窄視窗補強

- 桌面 Chrome 200% 檢視購物車、結帳結果與會員訂單時主要內容可讀；400% 在目前視窗形成低於 320 CSS px 的有效版面，原會員訂單標題／訂單編號被擠窄。此視窗不等同 1280px 基準下標準 320px 的 400% 等效驗收。
- 為 ≤240px 補上購物車單欄、結帳摘要垂直排列、會員訂單長字串折行；原縮放等效 Playwright 案例加上 160px 補充寬度，保留 640px／320px 驗收。重建後以目前 Chrome 縮放工作階段逐頁查看三個本機展示路由，AX 樹仍列出主要標題、資料與控制項；此人工查看未量測 `scrollWidth`，不作為 400% 無溢位簽核。
- `npm run typecheck` 與 `npm run build` PASS。Playwright 指令在瀏覽器啟動階段失敗：本機 Chrome headless process SIGABRT，Playwright 清理回報 `kill EPERM`；尚未執行到 viewport 斷言，因此 160px 回歸仍待可用的 headless runner 驗證。
- 僅修改展示版 CSS、測試與驗收紀錄；未變更 API、資料、交易、權限或付款。Issue24 維持 `claimed`；本機窄視窗縮放、完整全站原生縮放及對比仍未簽核，iPhone／VoiceOver 維持未驗證並列於[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-18 後續：縮放重排回歸重跑

- 修正 viewport 驗收：Chrome 的 `window.innerWidth` 保持設定寬度，但非 overlay 垂直捲軸會令 `documentElement.clientWidth` 少 15px；改以 `window.innerWidth` 核對設定值，並以 `clientWidth` 比對文件／body 寬度及超界元素。
- `npx playwright test tests/issue24-zoom-core-pages.spec.ts --config=playwright.config.ts --headed --reporter=line --trace=off`：PASS 1/1，Chrome channel 順利啟動。購物車、結帳結果及會員訂單在 640px、320px、160px CSS viewport 均通過主要內容／控制項可見與無水平溢位斷言；API 僅由 synthetic route stubs 回覆。
- 160px 補充 viewport 比本機原生 400% 畫面推估寬度更窄，但不是原生縮放本身的量測。本輪原生 Chrome 對本機 HTTPS 顯示未受信任憑證警告，未略過；本機 HTTP 展示站的 cart API 不可用。因此三個核心頁的原生 400% `scrollWidth` 仍未驗證，不作完整縮放簽核。
- Playwright 第一次執行在 640px 因將 `clientWidth` 誤當設定 viewport 寬度而失敗（640 預期、625 實際）；修正判定後同一案例通過。Issue24 仍維持 `claimed`。

### 2026-10-18 後續：結帳結果與會員訂單原生 400% 縮放

- 延續桌面 Chrome 原生縮放驗收，在 400% 下分別檢視結帳結果與會員訂單。兩頁量測皆為 `devicePixelRatio=8`、`innerWidth=156px`、`clientWidth=152px`；文件 `scrollWidth=152px`，未見水平溢位或超出視窗的內容。
- 結帳結果頁的主要內容與摘要可在頁面中閱讀並垂直捲動；會員訂單卡片捲動後可見，長訂單編號會折行，狀態與總額仍可讀。量測只代表當次桌面 Chrome 視窗與本機 synthetic preview。
- 本次只補記前次已完成的原生瀏覽器檢視，沒有重跑瀏覽器或測試；未連 API、SQL、付款或 LAN。其他視窗／硬體、真實網路、完整 WCAG 與正式 SLA 不在此證據範圍。
- Issue24 維持 `claimed`。完整 INP、全站焦點指示與非文字對比仍未完成；iPhone Safari／VoiceOver 仍排除於目前驗收並列在[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-18 後續：三語首頁避免目錄水合位移與冷／暖快取互動樣本

- 三語首頁先前在用戶端掛載後才載入商品目錄，初始畫面與目錄插入後的購物車區塊位置相差約 1,127 CSS px；合成資料回歸樣本 CLS 約 0.14–0.18。首頁改以 SSR `useAsyncData` 載入相同商品查詢，並避免首次掛載重複載入。新增 `issue24-performance-layout-shift.spec.ts` 覆蓋三語首頁初始／穩定版面與 CLS ≤0.1；修正前回歸失敗、修正後通過。
- 桌面 Chrome 155、Mac arm64、1440×900、本機 HTTPS、無 CPU／網路節流、合成 API 回覆下，三語首頁／政策／購物車各 30 次（冷、暖快取各 15 次），共 270 次導覽。九組均為 30/30 LCP、CLS 與 Event Timing 衍生互動延遲樣本；LCP P75 為 40–124 ms，CLS P75 為 0–0.0081，衍生 INP P75 為 16–24 ms，組內最大值為 48 ms。所有頁面回應 200，無合成 API 錯誤；沒有連接 API／SQL、付款或 LAN。
- INP 使用 Event Timing 與互動工作階段聚合，包含搜尋／清除、略過導覽、購物車數量／配送／試算等有限互動；這是未節流本機展示環境的工程診斷，不是正式使用者 INP、全站旅程或 SLA 證據。實際頁面採 `no-store`，冷／暖差異亦不代表正式快取效益。手機與 VoiceOver 維持未驗證並排除於本輪。
- 驗證：新增版面回歸及桌面鍵盤、購物車縮放、搜尋／加車焦點案例合併 Playwright PASS 4/4；`npm run typecheck` PASS；`npm run build` PASS。Issue24 維持 `claimed`，其他全站無障礙、真實硬體／網路與正式 SLA 驗收仍待完成。

### 2026-10-18 後續：桌面核心互動固定節流成功／錯誤恢復量測

- 新增可重跑的[Issue24 互動延遲量測腳本](../../../apps/storefront/scripts/issue24-interaction-latency.mjs)。使用 production build 本機 preview、Chrome 155.0.8059.84（macOS arm64）、1440×900、CPU 4× slowdown、150 ms network latency、1.6 Mbps 下行／750 Kbps 上行；每個 synthetic API response 固定延遲 120 ms。單一 browser context 暖資產快取、每趟獨立頁面，完整重跑 30 趟。
- 每趟量 15 個操作到穩定 UI 的時間：搜尋失敗／重試、加車失敗／重試、購物車數量失敗／重試、報價失敗／成功、建立訂單失敗／成功、售後查單 404／503／成功、退貨申請失敗／成功。此指標包含模擬服務回覆與 UI 更新，屬端到端操作診斷，不等於 INP。

| 操作（每項 n=30） | P50 (ms) | P75 (ms) | 最大 (ms) |
| --- | ---: | ---: | ---: |
| 搜尋失敗呈現 | 355.9 | 362.9 | 386.5 |
| 搜尋重試成功 | 215.8 | 221.4 | 230.5 |
| 加車失敗呈現 | 315.2 | 317.1 | 325.6 |
| 加車重試成功 | 317.0 | 319.7 | 333.3 |
| 數量更新失敗呈現 | 306.0 | 308.5 | 319.5 |
| 數量更新重試成功 | 304.7 | 306.1 | 315.2 |
| 報價失敗呈現 | 314.1 | 318.1 | 337.0 |
| 報價重試成功 | 318.0 | 321.5 | 338.1 |
| 建立訂單失敗呈現 | 325.9 | 326.8 | 331.6 |
| 建立訂單成功與後續狀態呈現 | 817.0 | 820.5 | 822.6 |
| 售後查單 404 呈現 | 220.4 | 224.2 | 233.5 |
| 售後查單 503 呈現 | 327.8 | 329.6 | 335.2 |
| 售後查單成功 | 221.1 | 223.4 | 232.6 |
| 退貨申請失敗呈現 | 386.3 | 390.7 | 393.2 |
| 退貨申請重試成功 | 331.2 | 334.6 | 341.9 |

- 另收集 1,257 筆 Event Timing 互動事件候選，Observer 門檻為 16 ms；候選 P50／P75 均為 16 ms，最大 48 ms。小於 16 ms 的事件不會出現在此集合，故此分佈是左截尾資料，不能作完整 INP。建立訂單成功 P75 約 821 ms，包含建立訂單後的購物車與付款狀態讀取；不能和 200 ms INP 規劃目標直接比較。
- `npm run build` PASS，量測腳本 30/30 完整旅程完成。全程使用瀏覽器合成 route fixture，沒有連 API／SQL／付款或 LAN；快取為暖資產，不含冷啟動／真實服務延遲。本機 throttle 診斷不代表正式使用者數據、完整 Web Vitals 或 SLA。Issue24 維持 `claimed`，完整 field INP、真實硬體／網路、其他路由及正式 SLA 仍未驗收。

### 2026-10-18 後續：日英流程固定節流量測與三語比較

- 將同一個[互動延遲量測腳本](../../../apps/storefront/scripts/issue24-interaction-latency.mjs)擴充為三語執行；`ISSUE24_LOCALES` 可選語系，預設 `zh-TW,ja,en`，每語由 `ISSUE24_SAMPLES` 控制樣本數（預設 30）。各語使用本地化的搜尋、商品、購物車、結帳及售後標籤，跑相同 15 項錯誤呈現／恢復流程。
- 使用 Chrome 155.0.8059.84（macOS arm64）、1440×900、4× CPU slowdown、150 ms network latency、1.6 Mbps 下行／750 Kbps 上行；每個 synthetic API response 固定延遲 120 ms。production build 本機 preview、單一 browser context 暖資產快取、每趟獨立頁面。三語各 30 趟，共 90 趟、1,350 個操作到穩定 UI 樣本；瀏覽器 route fixture 全程使用合成資料，沒有連 API／SQL／付款／LAN。
- 每列均為每語 `n=30`，時間單位為 ms：

| 操作 | 語系 | P50 | P75 | 最大 |
| --- | --- | ---: | ---: | ---: |
| 搜尋失敗呈現 | 繁中 | 351.4 | 365.9 | 392.0 |
| 搜尋重試成功 | 繁中 | 220.4 | 223.1 | 227.7 |
| 加車失敗呈現 | 繁中 | 315.3 | 320.7 | 326.6 |
| 加車重試成功 | 繁中 | 315.5 | 318.3 | 338.3 |
| 數量更新失敗呈現 | 繁中 | 301.4 | 304.4 | 313.4 |
| 數量更新重試成功 | 繁中 | 301.4 | 305.6 | 311.4 |
| 報價失敗呈現 | 繁中 | 312.3 | 313.4 | 330.2 |
| 報價重試成功 | 繁中 | 319.2 | 322.0 | 334.9 |
| 建立訂單失敗呈現 | 繁中 | 323.4 | 324.2 | 350.6 |
| 建立訂單成功與後續狀態呈現 | 繁中 | 816.8 | 819.3 | 929.7 |
| 售後查單 404 呈現 | 繁中 | 221.1 | 224.0 | 233.8 |
| 售後查單 503 呈現 | 繁中 | 327.3 | 329.2 | 331.7 |
| 售後查單成功 | 繁中 | 222.2 | 225.0 | 232.9 |
| 退貨申請失敗呈現 | 繁中 | 387.3 | 389.6 | 398.0 |
| 退貨申請重試成功 | 繁中 | 330.6 | 333.5 | 337.0 |
| 搜尋失敗呈現 | 日文 | 340.2 | 351.2 | 375.8 |
| 搜尋重試成功 | 日文 | 219.5 | 223.2 | 229.6 |
| 加車失敗呈現 | 日文 | 318.2 | 322.5 | 331.8 |
| 加車重試成功 | 日文 | 315.1 | 316.9 | 325.9 |
| 數量更新失敗呈現 | 日文 | 300.5 | 301.6 | 307.8 |
| 數量更新重試成功 | 日文 | 299.2 | 300.8 | 304.1 |
| 報價失敗呈現 | 日文 | 315.3 | 317.5 | 334.2 |
| 報價重試成功 | 日文 | 318.1 | 319.9 | 331.6 |
| 建立訂單失敗呈現 | 日文 | 322.1 | 323.6 | 326.3 |
| 建立訂單成功與後續狀態呈現 | 日文 | 816.5 | 818.6 | 834.7 |
| 售後查單 404 呈現 | 日文 | 221.5 | 222.8 | 226.2 |
| 售後查單 503 呈現 | 日文 | 325.2 | 327.7 | 331.8 |
| 售後查單成功 | 日文 | 224.1 | 225.5 | 231.2 |
| 退貨申請失敗呈現 | 日文 | 387.1 | 389.7 | 393.3 |
| 退貨申請重試成功 | 日文 | 318.6 | 332.4 | 335.1 |
| 搜尋失敗呈現 | 英文 | 339.9 | 343.9 | 364.4 |
| 搜尋重試成功 | 英文 | 217.5 | 221.2 | 233.1 |
| 加車失敗呈現 | 英文 | 317.7 | 322.2 | 326.1 |
| 加車重試成功 | 英文 | 315.9 | 317.8 | 321.4 |
| 數量更新失敗呈現 | 英文 | 300.3 | 301.5 | 308.9 |
| 數量更新重試成功 | 英文 | 299.7 | 301.6 | 306.6 |
| 報價失敗呈現 | 英文 | 310.9 | 313.8 | 326.5 |
| 報價重試成功 | 英文 | 318.7 | 320.7 | 338.0 |
| 建立訂單失敗呈現 | 英文 | 322.6 | 324.0 | 325.0 |
| 建立訂單成功與後續狀態呈現 | 英文 | 817.4 | 820.6 | 835.2 |
| 售後查單 404 呈現 | 英文 | 221.1 | 223.2 | 237.4 |
| 售後查單 503 呈現 | 英文 | 326.5 | 328.6 | 331.9 |
| 售後查單成功 | 英文 | 223.6 | 225.2 | 230.3 |
| 退貨申請失敗呈現 | 英文 | 388.7 | 390.4 | 393.7 |
| 退貨申請重試成功 | 英文 | 328.4 | 331.7 | 336.3 |

- 每語各收集 1,260 筆 Event Timing 互動事件候選，Observer 門檻 16 ms；三語候選 P50／P75 均為 16 ms，最大均為 40 ms。小於門檻的事件不在集合內，屬左截尾診斷，不能作完整 INP。訂單成功量測約 817–821 ms，包含建立訂單後狀態／購物車更新，不能直接與 INP 門檻比較。
- 驗證：production build PASS；三語各 1 趟 smoke journey PASS；三語完整 30/30 journey PASS（共 90/90），每語 15 項皆 30 個樣本；`node --check` 與 `git diff --check` PASS。原始 JSON 保存在未提交的 `.ai/runs/issue24-latency-trilingual-20261018/metrics.json`。Issue24 維持 `claimed`；這仍是固定條件的 synthetic 工程診斷，不代表實際使用者表現、完整 Web Vitals 或 SLA。

### 2026-10-18 後續：固定節流下三語核心頁 LCP／CLS

- 新增 `apps/storefront/scripts/issue24-page-vitals.mjs`，沿用上一輪互動延遲量測的 Chrome 155.0.8059.84（macOS arm64）、1440×900、CPU 4× slowdown、150 ms 網路延遲、1.6 Mbps 下行／750 Kbps 上行及每個合成 API 回應固定 120 ms。每語共用單一 browser context，資產快取暖身後量測；首頁、政策、購物車各 30 次，三語合計 270 次導覽。
- LCP 以 navigation PerformanceObserver 候選最大時間計算；CLS 排除 `hadRecentInput`，依 1 秒間隔／5 秒視窗取最大 session window。頁面 `networkidle`、字型載入完成後再觀察 500 ms。每次導覽皆取得 LCP 與 CLS，9 組全部回應 200，無頁面例外。

| 語系／頁面（每組 n=30） | LCP P50 (ms) | LCP P75 (ms) | LCP 最大 (ms) | CLS P75 | CLS 最大 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 繁中／首頁 | 232 | 232 | 244 | 0 | 0 |
| 繁中／政策 | 240 | 244 | 248 | 0 | 0 |
| 繁中／購物車 | 220 | 228 | 236 | 0.0041 | 0.0041 |
| 日文／首頁 | 224 | 228 | 236 | 0 | 0 |
| 日文／政策 | 228 | 232 | 236 | 0 | 0 |
| 日文／購物車 | 216 | 224 | 236 | 0.0043 | 0.0043 |
| 英文／首頁 | 208 | 216 | 220 | 0 | 0 |
| 英文／政策 | 212 | 216 | 232 | 0 | 0 |
| 英文／購物車 | 216 | 220 | 232 | 0.0038 | 0.0038 |

- 各組 LCP P75 為 216–244 ms；CLS P75 為 0–0.0043，低於票券工程目標 LCP 2.5 秒及 CLS 0.1。小幅 CLS 僅出現在空購物車頁，三語均不超過 0.0043；未發現需另行修正的版面跳動。
- 原始樣本與量測條件保存在未提交的 `.ai/runs/issue24-fixed-throttle-vitals-20261018/metrics.json`。這是固定節流、本機合成 API fixture 的桌面診斷，不代表 field Core Web Vitals、真實網路／服務、正式 SLA 或手機／VoiceOver 驗收；Issue24 維持未結案。

### 2026-10-18 後續：本機 HTTP 縮放回歸與非文字色彩抽樣

- Issue24 縮放案例重跑前確認本機 `localhost:3000` 與既有 preview `127.0.0.1:43173` 都提供 HTTP；repo Playwright 設定的 `https://localhost:3000` 會得到 `ERR_SSL_PROTOCOL_ERROR`。本次以執行後刪除的暫存設定改指 `http://localhost:3000`，未改正式設定。
- `npx playwright test tests/issue24-zoom-core-pages.spec.ts --config=.issue24-local-http.config.ts --headed --reporter=line --trace=off`：headed Chrome PASS 1/1。購物車、結帳結果與會員訂單在 640／320／160 CSS px 均通過主要內容／控制項可見及無水平溢位檢查；API 使用合成 route stubs。這是 200%／400% 等效 viewport 回歸與 160px 補充檢查，不是本次新做的原生縮放量測。
- 本機 Chrome 合成展示中抽樣 zh-TW 首頁、政策、客服、購物車、結帳結果與會員訂單的可見控制項：主要按鈕填色／白色 10.26:1、次要按鈕 7.76:1、原生輸入框邊界／白色 4.54:1、略過導覽深色填色／頁面背景 14.36:1。先前政策頁鍵盤人工檢視記錄的焦點色／頁面背景為 6.49:1；本次 CSS probe 沒有逐一鍵盤觸發所有焦點狀態。
- 靜態卡片分隔線 `#b0a7a0`、`#ddd5d0` 對白色分別為 2.36:1、1.45:1；這次抽樣未把它們當作可操作控制項邊界或狀態指示，是否需依卡片分組的視覺用途納入非文字對比仍待人工判定。此次只抽樣 zh-TW 預設呈現，未窮舉動態／禁用狀態、所有焦點狀態與所有圖示，因此不作完整 WCAG 非文字對比簽核。
- 暫存 HTTP 設定與色彩 probe 均已刪除；沒有 API、SQL、付款、帳號服務或持久 fixture 讀寫。Issue24 維持 `claimed`；iPhone Safari／VoiceOver 仍排除於目前驗收並列於[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-18 後續：三語卡片邊界、焦點與停用按鈕對比

- 前次 `localhost:3000` 是舊 `.output/server/index.mjs` 建置版，不含當前工作樹變更；該次舊樣式讀值不作本次修正後證據。另啟動僅綁 `127.0.0.1:3001` 的 HTTPS Nuxt dev preview，使用既有本機憑證，未中斷 3000 埠程序。
- 以 Chrome 155、Mac arm64、1280×800 唯讀檢查 zh-TW／ja／en 首頁、政策與客服共 9 個路由，HTTP 200。卡片區塊採 `#8a817b` 邊界對白色底 `3.81:1`；視覺檢查確認邊界承擔卡片分組用途，因此由原先 1.45–2.36:1 調深。相同邊界色套用首頁商品卡、購物車報價、商品規格／尺寸表、會員訂單卡與政策／客服／帳號分組；列表內裝飾分隔線維持原色。
- 三語各頁鍵盤前 8 個 Tab 停靠點皆有可見焦點樣式；略過連結與導覽使用 3px `#235b9d`，對白底 `6.87:1`、對頁面底 `6.49:1`。語系連結使用 Chrome 原生 1px 焦點框 `rgb(0, 95, 204)`，對白底 `5.98:1`。本次僅記錄前 8 個停靠點，不宣稱逐一盤點整頁所有控制項／圖示。
- 在本機首頁的可見按鈕上暫時模擬原生 `disabled` 屬性後，計算樣式為 `#625c58`／白字、透明度 1、`not-allowed` 游標，文字對比 `6.58:1`；先前未套停用色時會與一般按鈕同色。實際送出中使用 `aria-disabled`／`aria-busy` 的按鈕保留主色與鍵盤焦點，白字對主色 `10.26:1`。這是 DOM 樣式狀態檢查，不代表完整操作生命週期驗收；停用組件依 WCAG inactive-component 對比例外，不以例外作為降低文字可讀性的理由。
- 實作變更後 `npm run typecheck` PASS；`tests/issue24-zoom-core-pages.spec.ts` 在本機 HTTPS 工作樹 preview、合成 API route stubs 下 PASS 1/1，含購物車、結帳結果與會員訂單 640／320／160 CSS px 回流檢查。一次五案例回歸有 3 PASS、2 FAIL：兩個鍵盤搜尋案例因同名搜尋按鈕造成 Playwright strict locator 歧義；另一次重跑略過連結測試在日文路由未觀察到預期 hash。這些測試穩定性／互動驗收待修，不能據此宣稱鍵盤旅程全部通過。另一次縮放案例重跑 PASS 1/1；`issue24-accessibility.spec.ts` 的 200%／400% 等效 viewport 案例 PASS。
- 對比抽樣與停用樣式修正只覆蓋可見卡片、樣本焦點及按鈕狀態；不作完整 WCAG 2.2 AA 簽核。動態商品錯誤／載入、圖示及整頁所有狀態仍需後續驗收。Issue24 維持 `claimed`，iPhone Safari／VoiceOver 維持選配範圍。

### 2026-10-18 後續：三語桌面路由與動態購物／結帳焦點順序

- 擴充 `apps/storefront/tests/issue24-focus-stop-sweep.spec.ts`：桌面 Chrome 1280×900 下巡檢日文與英文首頁、購物車、政策、客服及會員路由；逐一以 Tab 驗證可見控制項依 DOM 順序可達，焦點指示可見，略過導覽將焦點移至主內容。
- 以合成 API route fixture 提供有商品購物車，驗收數量輸入、交付方式及重新試算等控制項；結帳結果頁先成功讀取，再由鍵盤觸發 503 刷新，確認安全錯誤訊息出現且焦點留在刷新按鈕，接著巡檢錯誤狀態下的主內容焦點順序。沒有修改產品頁面、API、資料、事件或交易行為。
- 驗證：`npm run typecheck` PASS；`npx playwright test tests/issue24-focus-stop-sweep.spec.ts --config=playwright.config.ts --reporter=line --trace=off` PASS 3/3；Issue22／Issue24 路由、鍵盤、縮放、結帳及售後回歸合併 PASS 15/15。所有瀏覽器 API 回應均為合成 fixture，僅 localhost HTTPS；未連 API／SQL／付款服務或 LAN。
- 此證據為桌面 Chrome 自動化焦點順序抽樣，不是完整 WCAG 2.2 AA、所有語系以外頁面／狀態、原生縮放、實際硬體、iPhone／VoiceOver 或正式效能 SLA 簽核；Issue24 維持 `claimed`。

### 2026-10-18 後續：桌面鍵盤驗收定位器與多語略過連結

- 修正搜尋送出按鈕的測試定位，限定於 `search` 區域；報價失敗提示改依錯誤內容定位，避免商品載入提示與報價錯誤提示同時存在時的 Playwright strict-mode 歧義。
- 將繁中、日文、英文政策頁略過導覽拆成獨立案例；逐語系確認第一個 Tab 停靠點、鍵盤啟用、主內容焦點、`#main-content` URL 片段及 390px viewport 無水平溢位。獨立案例排除連續切換語系時的頁面狀態干擾。
- 驗證：`npm run typecheck` PASS；本機 HTTPS 預覽以 headed Chrome 執行 `tests/issue22-policies-support.spec.ts`、`tests/issue24-accessibility.spec.ts`、`tests/issue24-checkout-keyboard.spec.ts`、`tests/issue24-core-keyboard-journey.spec.ts`、`tests/issue24-keyboard-error-aftercare.spec.ts`，PASS 12/12。暫存 Playwright 設定與輸出均置於本機／暫存路徑；API 使用合成 route fixtures，未連 SQL、真實付款或 LAN。
- 重跑前的失敗已由上述 12/12 回歸覆蓋：日文 hash 失敗在各語系隔離後未重現；兩處搜尋按鈕與報價 alert 歧義已改為區域／內容定位。Issue24 仍維持 `claimed`，不代表完整 WCAG 2.2 AA、全站鍵盤或正式效能 SLA 已通過；iPhone Safari／VoiceOver 維持未驗證並列於[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-18 後續：日英購物車數量與試算失敗恢復焦點

- 日文與英文桌面購物車合成 fixture 新增數量更新與費用試算的 503 失敗／重試流程。Playwright 首次重現數量更新後焦點落到頁面、不在可操作欄位；購物車更新結束後現在會把焦點還給數量欄，讓使用者能修正或重試。試算結束後焦點會回到試算按鈕，錯誤及成功插入內容時均可繼續操作。
- `setQuantity` 在另一項購物車請求處理中拒絕重複呼叫；數量／試算成功與失敗後均以鍵盤操作驗證，並檢查錯誤提示、重試成功後的數量與報價總額。
- 驗證：`npm run typecheck` PASS；headed Chrome 本機 HTTPS `npx playwright test tests/issue24-focus-stop-sweep.spec.ts --config=playwright.config.ts --headed --reporter=line --trace=off` PASS 3/3；與 `issue24-core-keyboard-journey.spec.ts`、`issue24-keyboard-error-aftercare.spec.ts` 合併回歸 PASS 6/6；`git diff --check` PASS。三語／動態 API 均由合成 route fixture 回覆；預覽僅綁 localhost，未連 API、SQL、付款服務或 LAN。
- 自動化證據限桌面 Chrome、日英購物車及合成 503／成功回應，不代表全站 WCAG 2.2 AA、其他瀏覽器、行動裝置或實際服務故障驗收；Issue24 維持 `claimed`。

### 2026-10-18 後續：動態焦點指示非文字對比斷言

- 擴充 `apps/storefront/tests/issue24-focus-stop-sweep.spec.ts`：既有桌面三語 Tab 焦點掃描會確認 3:1 焦點外框對鄰接不透明背景；日英購物車數量更新失敗／成功、報價失敗／重試及結帳刷新失敗的焦點復原停點也加入同一斷言。只檢查目前以 CSS outline 呈現的焦點指示，不量測頁面所有圖示或控制項狀態。
- `npm run typecheck` PASS；Playwright `--list` 可解析並列出 3 個案例。瀏覽器案例本輪未執行：測試啟動前 Nuxt loopback readiness 等待 120 秒逾時（本機 listener 可建立，但測試環境無法連上）；因此本輪沒有新的實際 computed-style 對比結果。暫存設定已刪除、45123 listener 已停止。
- 未改產品 CSS／行為、API、資料、事件或交易。此測試覆蓋補強尚待可連 loopback 的環境執行確認；既有焦點色抽樣不等同這次動態狀態的實測。Issue24 維持 `claimed`，不宣稱完整 WCAG 2.2 AA 或全站非文字對比驗收。

### 2026-10-18 後續：SSR 共用 fixture 與日英鍵盤回歸

- 新增專用 Playwright fixture API，Nuxt SSR 與瀏覽器互動都讀取同一份合成商品資料；CLS 案例另檢查伺服器輸出的三語商品名稱及 hydration mismatch。fixture 僅綁 loopback、資料存在記憶體，不連 SQL、真實 API、付款服務或 LAN。
- 新增日文／英文鍵盤案例，使用 Tab／Enter 與原生選單按鍵完成略過導覽、商品搜尋、SKU 選擇及加入購物車。專用 config 啟動 fixture；一般 Playwright config 排除這兩個專用 fixture 案例，避免對接展示資料庫。
- 驗證：`npx playwright test --config=playwright.issue24-fixture.config.ts --reporter=line --trace=off` PASS 2/2；`npm run typecheck`、`node --check tests/fixtures/issue24-api-server.mjs`、`git diff --check` PASS。測試重用已存在的 localhost Nuxt preview；沒有停用或覆蓋該程序。
- 此結果是合成 SSR／瀏覽器一致性與日英局部鍵盤回歸，不代表真 API／SQL 驗收、完整 WCAG、Issue24 結案或正式效能 SLA；iPhone Safari／VoiceOver 仍依決策列為[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-18 後續：桌面錯誤恢復回歸與核心頁效能證據複核

- 目前工作樹 HTTPS 預覽下，Issue24 桌面鍵盤核心旅程、結帳／售後失敗重試、焦點巡檢及三語略過連結／縮放自動化合併重跑 PASS 12/12；`npm run typecheck` PASS。詳細命令、錯誤環境排除、hydration mock 限制與效能樣本條件見[架構驗證紀錄](../../architecture/VERIFICATION.md#issue24-桌面錯誤恢復回歸與三語核心頁效能證據複核2026-10-18)。
- 已複核的固定節流效能資料涵蓋三語首頁、政策與購物車，各 30 次導覽；LCP P75 216–244ms、CLS P75 0–0.0043。此為既有本機 synthetic 診斷資料，不代表本輪重新量測、完整 INP、正式 SLA 或 Issue24 完成。
- Issue24 維持 `claimed`。完整 INP、全站焦點／非文字對比及正式效能驗收仍待完成；iPhone Safari／VoiceOver 維持未驗證並列於[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-18 後續：桌面動態焦點停點對比重跑

- `tests/issue24-focus-stop-sweep.spec.ts` 以 Desktop Chrome 155.0.8059.84 和僅綁 `127.0.0.1:3001` 的本機 HTTPS preview 執行，3/3 PASS。繁中／日文／英文鍵盤焦點掃描與日英購物車／結帳錯誤恢復停點，均通過可見 focus outline 及至少 3:1 非文字對比斷言；未保存逐停點 ratio 數值。
- API 僅由合成 route fixture 回覆，沒有連 API、SQL、付款服務或 LAN。完整命令、環境及限制見[架構驗證紀錄](../../architecture/VERIFICATION.md#issue24-桌面動態焦點停點非文字對比重跑2026-10-18)。
- 本結果只補上被 loopback readiness 逾時中斷的自動化抽樣，不代表全站焦點／非文字對比、完整 WCAG 2.2 AA 或效能 SLA 已通過。Issue24 維持 `claimed`；iPhone Safari／VoiceOver 維持未驗證並列於[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-19 後續：桌面三語鍵盤焦點與非文字對比巡檢

- 桌面 Chrome 155.0.8059.84、1280×900、本機 synthetic preview 巡檢 zh-TW／ja／en 共 24 個路由狀態、219 個鍵盤焦點停點及 57 個可見啟用控制項；焦點順序／可見性與略過導覽焦點確認通過，控制項非文字色彩抽樣未發現低於 3:1 的案例。詳細範圍與限制見[架構驗證紀錄](../../architecture/VERIFICATION.md#issue24-桌面全三語鍵盤焦點與非文字對比巡檢2026-10-19)。
- `npm run build`、`npm run typecheck` PASS。這是有限的桌面自動化抽樣，不代表全站 WCAG 2.2 AA、所有狀態、完整 INP 或正式 SLA。Issue24 維持 `claimed`；iPhone Safari／VoiceOver 仍未驗證並列於[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-19 後續：首頁、政策與客服頁原生縮放抽樣

- 桌面 Chrome 155.0.8059.84 在本機合成展示預覽以原生 200%／400% 檢視繁中首頁、政策與客服頁。文字可讀並可垂直捲動；首頁導覽、商品展示區與搜尋欄在窄版面仍可見，當次畫面未見明顯水平裁切或水平捲軸。
- 這是有限畫面人工抽樣，未量測 DOM 寬度指標，也未逐一操作商品／購物流程；首頁商品與購物車服務當時不可用。故不宣稱全站無溢位、完整互動或完整 WCAG 驗收通過。詳細限制見[架構驗證紀錄](../../architecture/VERIFICATION.md#issue24-桌面-chrome-首頁政策與客服原生縮放抽樣2026-10-19)。
- Issue24 維持 `claimed`；iPhone Safari／VoiceOver 維持未驗證並列於[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-19 後續：縮放等效版面中的購物車鍵盤操作

- 新增 `apps/storefront/tests/issue24-zoom-cart-interaction.spec.ts`，在 640px／320px CSS viewport（1280px 桌面基準的 200%／400% 重排等效寬度）以鍵盤修改購物車數量，確認小計更新，再以 Tab 到達移除、優惠碼、交付方式與重新試算控制，並以 Enter 取得合成報價摘要；兩種寬度均無水平溢位。
- 縮放版面、購物車鍵盤互動與焦點巡檢合併 Playwright PASS 5/5；`npm run typecheck` PASS。測試使用本機 Nuxt preview 與 Playwright 合成 API route stub，不連 API／SQL／付款服務或 LAN。
- 這是 CSS viewport 重排等效自動化，並非桌面 Chrome 原生縮放下的人工簽核；只驗購物車數量與試算操作，不代表所有核心流程、完整 WCAG 2.2 AA 或正式效能 SLA。Issue24 維持 `claimed`；iPhone Safari／VoiceOver 維持未驗證並列於[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-19 後續：桌面 Chrome 原生 200%／400% 購物車鍵盤驗收

- 使用桌面 Chrome 與 localhost-only 合成購物車，確認瀏覽器原生縮放指示分別為 200% 與 400%。兩種縮放下皆以鍵盤將數量更新（2→3、3→4），小計分別更新為 TWD 3,000／4,000；Tab 可到達移除商品、優惠碼、交付方式與重新試算控制，按 Enter 可取得合成報價 TWD 3,060／4,060。
- 400% 畫面採單欄直向排列，文字及控制項仍可辨識，允許垂直捲動；截圖範圍未見明顯水平裁切。此為單一繁中購物車流程的人工抽樣，未量測 `scrollWidth`／`clientWidth`，不代表其他路由、全站無水平溢位或完整 WCAG 2.2 AA 通過。
- API fixture 僅在 `127.0.0.1` 提供合成購物車與報價；未連接專案 API、SQL、付款服務或 LAN，未修改產品程式碼。驗收後已停止兩個本機服務，Chrome 原生縮放還原為 110%。Issue24 維持 `claimed`；iPhone Safari／VoiceOver 仍排除於目前範圍並列於[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-19 後續：三語商品、結帳結果與會員訂單頁 LCP／CLS

- 桌面 Chrome 155.0.8059.84、macOS arm64、1440×900 CSS px；CPU 4×、網路延遲 150ms／下載 1.6Mbps／上傳 750Kbps，合成 API 固定延遲 120ms。每語系／頁面量測 30 次，冷快取 15 次（新 context、停用快取），暖快取 15 次（獨立暖身後共用 context）；共 9 組、270 次導覽。每次均 HTTP 200，無 page error。

| 語系／頁面 | 樣本（冷／暖） | LCP P50 (ms) | LCP P75 (ms) | CLS P75 |
| --- | ---: | ---: | ---: | ---: |
| 繁中／商品詳情 | 30（15／15） | 244 | 264 | 0.0036 |
| 繁中／結帳結果 | 30（15／15） | 216 | 232 | 0 |
| 繁中／會員訂單 | 30（15／15） | 444 | 1,808 | 0.0274 |
| 日文／商品詳情 | 30（15／15） | 216 | 228 | 0.0028 |
| 日文／結帳結果 | 30（15／15） | 216 | 232 | 0 |
| 日文／會員訂單 | 30（15／15） | 212 | 440 | 0.0305 |
| 英文／商品詳情 | 30（15／15） | 220 | 228 | 0.0028 |
| 英文／結帳結果 | 30（15／15） | 212 | 232 | 0 |
| 英文／會員訂單 | 30（15／15） | 216 | 444 | 0.0222 |

- 九組 LCP P75 為 228–1,808ms、CLS P75 為 0–0.0305，均低於 Q-01 規劃門檻 LCP 2.5 秒／CLS 0.1。初測繁中會員訂單頁冷快取 LCP P75 為 1,812ms、暖快取為 432ms；日文與英文會員頁冷快取較低、暖快取約 444ms。後續獨立重測確認繁中冷載入偏高可重現，詳見下節；原因仍待定位。
- 使用既有 production build 與僅綁 `127.0.0.1` 的臨時合成 API，未連真實 API、SQL、付款服務或 LAN。原始 270 筆資料留在本機 `/private/tmp/issue24-core-vitals-20261019.json`，未提交。此測量為實驗室 LCP／CLS 診斷，不是 field Core Web Vitals、完整 INP、實際服務／網路、正式 SLA 或手機驗收；Issue24 維持 `claimed`，iPhone Safari／VoiceOver 仍排除於目前範圍並列於[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-19 後續：會員訂單頁冷／暖快取差異重測

- 只重測 zh-TW、ja、en 的會員訂單頁 `/account`，每語系冷快取 30 次（各用新 browser context 並停用快取）、暖快取 30 次（共用 context，先做一次不計分暖身），共 180 次導覽。桌面 Chrome 155.0.8059.84、macOS arm64、1440×900 CSS px、CPU 4×、網路延遲 150ms／下載 1.6Mbps／上傳 750Kbps、合成 API 固定延遲 120ms；量測與上一輪相同，頁面 networkidle、字型完成後再觀察 500ms。

| 語系／快取 | 樣本 | LCP P50 (ms) | LCP P75 (ms) | LCP 最大 (ms) | CLS P75 | 超過 1 秒 LCP 樣本 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 繁中／冷 | 30 | 1,816 | 1,856 | 2,732 | 0.0274 | 30/30 |
| 繁中／暖 | 30 | 488 | 572 | 4,416 | 0.0222 | 3/30 |
| 日文／冷 | 30 | 196 | 208 | 1,356 | 0.0305 | 1/30 |
| 日文／暖 | 30 | 424 | 432 | 0.0305 | 0/30 |
| 英文／冷 | 30 | 200 | 208 | 0.0222 | 0/30 |
| 英文／暖 | 30 | 432 | 436 | 0.0222 | 0/30 |

- zh-TW 冷快取高 LCP 在第二批 30 次仍全數超過 1 秒，P75 1,856ms；暖快取 P75 572ms，另有 3 次超過 1 秒。ja／en 冷快取 P75 約 208ms。這證實本機合成設定下有可重現的語系／快取差異，但尚未量測 LCP 元素歸因與資源瀑布，不能推定根因或外推到正式使用者。
- 全部 180 次 HTTP 200、page error 0。原始第二批樣本保存在本機 `/private/tmp/issue24-member-orders-repeat-20261019.json`，未提交；服務只綁 `127.0.0.1`，量測後已停止，未連 API／SQL／付款或 LAN。此為桌面實驗室 LCP／CLS 診斷，不是 field Core Web Vitals、完整 INP、真實網路／服務、SLA 或手機驗收。Issue24 維持 `claimed`，iPhone Safari／VoiceOver 仍排除於本輪並列於[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-19 後續：會員訂單頁暖快取命中與 LCP 候選元素

- 以桌面 Chrome 155.0.8059.84、macOS arm64、1440×900 CSS px、CPU 4×、150ms 網路延遲／1.6Mbps 下行／750Kbps 上行及每次合成 API 120ms 延遲，檢查 zh-TW／ja／en 的 `/account`；每語系冷快取與暖快取各 10 次，共 60 次。冷樣本使用新 context 並停用快取；暖樣本共用 context，先做一次不計分暖身。

| 語系 | 冷 LCP P75 | 冷資產快取命中 | 暖 LCP P75 | 暖資產快取命中 | 主要 LCP 候選元素（冷／暖） |
| --- | ---: | ---: | ---: | ---: | --- |
| 繁中 | 1,768 ms | 0/190 | 384 ms | 180/180 | `h2#claim-heading`／同左 |
| 日文 | 204 ms | 0/190 | 384 ms | 180/180 | `h1`／`h2#claim-heading` |
| 英文 | 200 ms | 0/190 | 380 ms | 180/180 | `h1`／`h2#claim-heading` |

- 快取證據來自 Chrome DevTools Protocol 的 `Network.requestServedFromCache`／`Network.responseReceived.fromDiskCache`，範圍是 `/_nuxt` 靜態資產；每個暖語系樣本組的 180/180 資產請求均命中。合成會員／訂單 API 由頁面初始化的 `fetch` wrapper 回覆，未用會停用 HTTP cache 的 Playwright route interception。先前使用 route interception 的初始快取樣本作廢，不納入此表。全部 60 次回應 HTTP 200，page error 0。
- 三語暖快取 LCP P75 均低於 Q-01 的 2.5 秒規劃目標；繁中冷快取 P75 為 1.768 秒。這只證明目前本機合成條件下資產命中與頁面 LCP 候選元素，不定位差異根因，也不能外推為真實會員、正式網路或 field Core Web Vitals 表現。原始 60 筆結果留在本機 `/private/tmp/issue24-member-orders-cache-attribution-fetchfixture-20261019.json`，未提交；量測腳本為 `/private/tmp/issue24-member-orders-cache-attribution-fetchfixture-20261019.mjs`。沒有修改產品程式碼，Issue24 維持 `claimed`，iPhone Safari／VoiceOver 仍排除於目前範圍。

### 2026-10-19 後續：會員訂單頁 LCP 元素與資源瀑布歸因

- 沿用前述桌面 Chrome 155.0.8059.84、macOS arm64、1440×900 CSS px、CPU 4×、150ms 網路延遲／1.6Mbps 下行／750Kbps 上行與 120ms 合成 API 延遲，三語 `/account` 各做 3 次冷載入與 3 次暖載入，共 18 次導覽。冷樣本使用新 context 並停用快取；暖樣本共用 context 並先暖身。量測 LCP 候選文字、面積與出現時間，以及 `PerformanceResourceTiming` 的請求／回應時間。

| 語系 | 冷 LCP（中位數／範圍） | 首個主標題面積 | 歸戶標題出現時間中位數 | 冷載入主要 LCP 候選 |
| --- | ---: | ---: | ---: | --- |
| zh-TW | 1,776 ms／1,776–1,780 ms | 5,850 px² | 1,768 ms | `h2#claim-heading`，8,052 px² |
| ja | 196 ms／196–208 ms | 7,136 px² | 1,757 ms | `h1`，7,136 px² |
| en | 192 ms／192–192 ms | 5,957 px² | 1,756 ms | `h1`，5,957 px² |

- 三語載入相同 19 個 Nuxt 資產，主要 JavaScript bundle 的資源時序相近；例如 83.6 KB bundle 的回應結束約落在 1,408–1,422 ms，沒有發現 zh-TW 專屬慢資源。`claim-heading` 約在三語相近時間出現；繁中區塊的 LCP 面積比首標題大，因此成為最後 LCP 候選。這解釋了本機合成條件下的語系差距是候選元素面積／內容組合差異，不是繁中資源下載較慢。
- 三語暖載入 LCP 中位數為 384／380／380 ms（zh-TW／ja／en）。所有 18 次導覽均 HTTP 200、page error 0。原始結果及量測腳本保留於本機 `/private/tmp/issue24-member-orders-waterfall-final-20261019.json` 與 `/private/tmp/issue24-member-orders-waterfall-final-20261019.mjs`，未提交。量測只使用合成會員／訂單與 localhost production build；不代表 field Core Web Vitals、真實服務／網路、完整 INP、SLA 或手機驗收。未修改產品程式碼；Issue24 維持 `claimed`，iPhone Safari／VoiceOver 仍排除於目前範圍。

### 2026-10-19 後續：三語桌面核心互動 INP 實驗室估算

- 使用 production build preview、Desktop Chrome 155.0.8059.84、macOS arm64、1440×900；CPU 4× slowdown、150 ms 網路延遲、1.6 Mbps 下行／750 Kbps 上行及合成 API 固定 120 ms 延遲。服務僅綁 `127.0.0.1`，Playwright 全程使用合成回應。
- zh-TW／ja／en 各完成 30 趟互動旅程，每趟 15 個成功／錯誤恢復互動。Event Timing 只觀測 duration ≥16 ms 的項目，按 `interactionId` 分組，以每趟最慢的已觀測互動估算 visit INP，再取 30 趟 P75；每語系有 1,260 筆 entries，所有旅程至少觀測到一筆符合門檻的互動。

| 語系 | Visit INP estimate P75 | 最大估算值 |
| --- | ---: | ---: |
| zh-TW | 40 ms | 56 ms |
| ja | 40 ms | 48 ms |
| en | 40 ms | 48 ms |

- 結果低於 Q-01 的 200 ms 本機規劃目標，但只代表此合成桌面實驗室估算，不是 field INP、正式 Core Web Vitals、SLA 或手機驗收；16 ms 門檻排除較短事件，未涵蓋腳本外互動。原始逐筆資料與臨時量測程式未保留，因此不延伸推論其他互動表現。
- 本結果補充桌面效能證據，不改變 Issue24 的 `claimed` 狀態。iPhone Safari／VoiceOver 仍未驗證並依目前決策排除於本輪，列在[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### Issue24 Event Timing 門檻下的三語短互動代理量測（2026-10-19）

- 延續前一輪三語核心互動旅程，將觀測器擴充為記錄 click／keydown／input／change 的 `Event.timeStamp` 至第一個 `requestAnimationFrame` callback 間隔，保留逐事件值、操作名稱與旅程編號。每語系 30 趟、每趟 15 個成功／錯誤恢復操作，共 90 趟；Desktop Playwright Chromium 145.0.7632.6、macOS arm64、1440×900、CPU 4×、150ms 網路延遲／1.6Mbps 下行／750Kbps 上行、合成 API 固定 120ms。preview 僅綁 `127.0.0.1`。

| 語系 | 首影格代理事件數 | 代理值 <16ms | 代理事件 P75 | 每趟最大代理值 P75／最大值 | Event Timing ≥16ms entries |
| --- | ---: | ---: | ---: | ---: | ---: |
| zh-TW | 660 | 660 | 4.7 ms | 11.866／14.566 ms | 399 |
| ja | 660 | 660 | 4.7 ms | 10.866／12.832 ms | 321 |
| en | 660 | 660 | 4.8 ms | 10.966／14.866 ms | 187 |

- 在此測試條件下，前次 `durationThreshold: 16` 的 Event Timing 集合沒有列出低於門檻的事件；新的首影格代理記錄在三語每趟均觀察到低於 16ms 的事件。這確認先前的 Event Timing 診斷集合會漏列較短的回應代理事件；代理值量的是 rAF callback，不是確認完成的螢幕繪製，不能據此修正或取代 visit INP。與前輪 Chrome 155 的 40ms visit INP 實驗室估算不是同一瀏覽器／量測指標，不能直接解讀為改善幅度。
- 原始 1,980 筆事件與逐趟摘要保留於 `.ai/runs/issue24-sub16ms-proxy/metrics.json`。`node --check scripts/issue24-interaction-latency.mjs`、`npm run build` 與三語完整旅程（30/30 每語）均 PASS。量測使用合成 route 回覆，不連真實 API、SQL、付款、LAN；數據不代表 field INP、Core Web Vitals、正式 SLA 或手機驗收。Issue24 維持 `claimed`，iPhone Safari／VoiceOver 仍依目前決策排除於本輪並列於[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### 2026-10-19 後續：Chrome 155 固定節流三語互動追溯量測

- 使用 production preview、Chrome 155.0.8059.84、1440×900、CPU 4×、150ms 網路延遲、1.6Mbps 下行／750Kbps 上行與固定 120ms 合成 API；只綁 loopback。三語各完成 30 趟，每趟 15 類操作，每類 n=30。以 16ms 門檻的 Event Timing entries 按 `interactionId` 分組，取逐趟最長已觀測互動，再計 P75。

| 語系 | 逐趟最大候選 P75 | 30 趟最大值 |
| --- | ---: | ---: |
| 繁中（zh-TW） | 40 ms | 40 ms |
| 日文（ja） | 32 ms | 40 ms |
| 英文（en） | 32 ms | 40 ms |

- 這是腳本化合成實驗室估算，低於 Q-01 的 200ms 規劃門檻，但不是 field INP、正式 Core Web Vitals 或 SLA，不能據此標記正式驗收通過。標準 lab 測量與 field INP 的界線及 TBT lab proxy 見[官方量測指引](https://web.dev/articles/vitals-measurement-getting-started)。完整 entry、interactionId、操作歸戶與逐趟摘要保存在未提交的 `.ai/runs/issue24-inp-traceable-20261019/metrics.json`，程式同目錄保存。
- Issue24 維持 `claimed`；Q-01 正式 field 驗收仍未完成。iPhone Safari／VoiceOver 維持未驗證並排除於本輪，列於[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。詳細量測條件與限制見[架構驗證紀錄](../../architecture/VERIFICATION.md)。

### 2026-10-19 後續：三語桌面長任務 TBT 估算

- 使用 Desktop Chrome 155.0.8059.84、macOS arm64、1440×900 CSS px、CPU 4×、150ms 網路延遲／1.6Mbps 下行／750Kbps 上行與固定 120ms 合成 API 回應。預覽只綁 `127.0.0.1`；涵蓋 zh-TW／ja／en 的首頁、政策、購物車，每組 10 趟，共 90 次載入。
- Chrome Long Tasks API 以 50ms 為長任務門檻；每趟從 FCP 起算，到 Playwright `networkidle` 後 5 秒為止，將窗口內長任務的 `max(0, duration − 50ms)` 加總。90 趟均觀測到 FCP 與 Long Tasks API，HTTP 200，沒有 page error 或符合門檻的長任務；因此每組 P50／P75／最大值均為 0 ms。

| 語系／頁面 | 樣本數 | TBT 估算 P50／P75／最大值 | HTTP 200／FCP |
| --- | ---: | ---: | ---: |
| zh-TW／首頁 | 10 | 0／0／0 ms | 10／10 |
| zh-TW／政策 | 10 | 0／0／0 ms | 10／10 |
| zh-TW／購物車 | 10 | 0／0／0 ms | 10／10 |
| ja／首頁 | 10 | 0／0／0 ms | 10／10 |
| ja／政策 | 10 | 0／0／0 ms | 10／10 |
| ja／購物車 | 10 | 0／0／0 ms | 10／10 |
| en／首頁 | 10 | 0／0／0 ms | 10／10 |
| en／政策 | 10 | 0／0／0 ms | 10／10 |
| en／購物車 | 10 | 0／0／0 ms | 10／10 |

- Lighthouse 未安裝，且未取得 trace-based Time to Interactive（TTI）；本結果是依明確窗口計算的 Long Tasks API TBT 估算，不是官方 Lighthouse TBT 分數。零值只表示觀測窗口沒有捕捉到超過 50ms 的長任務，不代表實際互動延遲為零或 field INP／Core Web Vitals 達標。Google 的[量測指引](https://web.dev/articles/vitals-measurement-getting-started)指出 TBT 是 lab INP proxy，lab 與 field 結果須分開解讀。
- 原始 90 趟資料與可重現腳本留在未提交的 `.ai/runs/issue24-tbt-lab-20261019/`。只使用合成路由回應與 localhost production preview，未連 API、SQL、付款服務或 LAN；沒有修改產品程式碼。Issue24 維持 `claimed`，本結果不改變正式驗收狀態；iPhone Safari／VoiceOver 仍未驗證並依目前決策排除於本輪。

### 2026-10-19 桌面三語目標尺寸修正與 Lighthouse Accessibility

- 將共用語言切換連結、商品頁返回商品目錄連結，以及會員頁商店／購物車導覽連結擴至至少 24×24 CSS px。新增 Playwright 幾何尺寸回歸，並確認三語商品、會員與結帳頁均顯示合成內容；首次執行重現商品頁語言連結高度 18px，修正後 PASS 3/3。`npm run typecheck` PASS。
- Lighthouse 13.5.0、Desktop preset、Accessibility only、Chrome 155，在商品 `/products/issue24-vitals-shirt`、結帳 `/checkout/issue24-vitals`、會員 `/account` 三頁及 zh-TW／ja／en 共九路由執行。九路由 Accessibility score 均為 1.00，`target-size` 均 PASS、0 failing targets。報告保留於本機 `/private/tmp/kohami-lighthouse-*.json`。
- 量測使用 localhost HTTPS Nuxt 開發預覽及只綁 `127.0.0.1:5080` 的暫存合成 API fixture；商品、匿名會員、空購物車與結帳資料皆為合成回應，沒有連 SQL、真實會員、付款或 LAN。此結果只代表這九個桌面 lab 頁面與 Lighthouse 規則，不是完整 WCAG AA、行動裝置／VoiceOver 驗收或正式 SLA；iPhone Safari／VoiceOver 仍依目前決策排除於本輪。

### 2026-10-19 桌面三語效能證據充分性判定

- 複核既有紀錄後，桌面合成實驗室已有可用的 LCP／CLS 子集證據：三語首頁、政策與購物車各 n=30；三語商品詳情、結帳結果與會員訂單頁各 n=30，均分冷／暖快取。後者 LCP P75 為 228–1,808 ms、CLS P75 為 0–0.0305；首頁／政策／購物車先前固定節流組的 LCP P75 為 216–244 ms、CLS P75 為 0–0.0043。已量頁面在各自記錄條件下均低於 Q-01 的 LCP／CLS 門檻。三語互動旅程已有每語 30 趟的 Event Timing visit-max estimate（P75：40／32／32 ms），另有 Long Tasks API TBT estimate；兩者均只作實驗室診斷，不能標成正式 INP。
- 本輪重用上述證據，未重測相同組合，也沒有證據支持產品效能修正。共同量測條件為 macOS arm64、Desktop Chrome 155.0.8059.84、1440×900 CSS px、CPU 4×、150 ms 網路延遲、1.6 Mbps 下行／750 Kbps 上行與固定 120 ms 合成 API 延遲；量測期間為 2026-10-19，樣本數依各紀錄列示，資料來自 localhost production preview 與合成 fixture。會員訂單頁另有獨立冷／暖重測及 LCP 資源歸因，詳見[架構驗證紀錄](../../architecture/VERIFICATION.md)。
- 證據尚不足以完成本票效能驗收：N01／Q-01 有門檻與樣本方法，但未指定唯一的「核心頁」路由清單；已量頁面只覆蓋體驗規格 SCR-01–09 的一部分。Event Timing 門檻下的 visit estimate 不是 field INP，TBT 也不是 INP；沒有正式使用者 field data、正式測量期間或正式服務／硬體／網路矩陣。因此效能項及「條件未確認時列未驗證」項目維持未完成，Issue24 維持 `claimed`。這不表示已量桌面 lab 數據未達門檻，也不構成正式 Core Web Vitals／SLA 通過宣告。
- 後續應先在 N01／Q-01 採用明確的桌面核心路由矩陣，以及每路由／語系的狀態、快取與操作樣本設計，再決定所需 INP 資料來源；範圍未確認前，不以缺少路由或合成代理值補成通過。iPhone Safari／VoiceOver 仍依決策列為選配且未驗證。

### Issue24 桌面三語完整鍵盤購物與付款結果旅程（2026-10-19）

- 新增 `apps/storefront/tests/issue24-desktop-full-keyboard-outcomes.spec.ts`，以桌面 Chrome 的鍵盤操作串接三語略過導覽、商品搜尋、SKU 選擇、加入購物車、報價、建立合成訂單、進入唯讀結帳結果與狀態刷新。
- zh-TW 驗證處理中→模擬成功；ja 驗證處理中→模擬拒絕；en 驗證處理中→未知結果。三語皆另驗證狀態查詢 503 時保留上次付款結果與合成 TWD 1,060 金額，使用鍵盤重查後成功；焦點留在刷新按鈕。未知狀態提供不重複啟動付款提示。
- `npm exec -- playwright test tests/issue24-desktop-full-keyboard-outcomes.spec.ts --config=playwright.config.ts --reporter=line --trace=off`：PASS 3/3；`npm run typecheck`：PASS。測試只使用 synthetic route stub 與本機 localhost HTTPS 預覽，沒有連 SQL、真實 API／付款服務或 LAN。
- 使用專案共用商品 fixture，使 SSR 與瀏覽器端商品／翻譯資料一致。503 fixture 維持失敗直到人工觸發重查，避免 GET 自動重試使錯誤狀態瞬間消失；這是測試 fixture 行為，未修改產品程式碼。此為桌面瀏覽器自動化驗收，不代表人工可及性簽核、完整 WCAG AA、正式交易或 SLA；iPhone Safari／VoiceOver 仍依目前決策列選配並未驗證。

### 2026-10-19 後續：桌面三語購物與付款主旅程效能路由矩陣

- 依使用者確認，核心效能路由限桌面購物與付款主旅程：首頁（含商品列表／搜尋）、商品詳情、購物車（含結帳表單）、結帳結果。對應實作路由為 `/{locale}`、`/{locale}/products/:slug`、`/{locale}/cart`、`/{locale}/checkout/:id`；SCR-02 列表與 SCR-04B 結帳表單目前分別嵌在首頁及購物車，沒有獨立路由。
- 使用 MacBook Pro／Apple M5 Pro／48 GB／macOS 26.6 arm64、Google Chrome 155.0.8059.84、1440×900、CPU 4×、150 ms latency、1.6 Mbps 下行／750 Kbps 上行、合成 API 固定 120 ms。每個三語路由冷快取 n=30、暖快取 n=30，分開計算；共 720 筆導覽，另有 24 次暖身。冷樣本使用新 context 並停用 browser cache；暖樣本同一 context 暖身後重用，CDP 冷組命中 0、暖組均有資產快取命中。量測期間 2026-10-19 07:07–07:42 UTC，完整表格、P75 方法、已保存的原始檔及命令見[架構驗證紀錄](../../architecture/VERIFICATION.md#issue24-三語桌面購物與付款主旅程冷暖快取-lcpcls2026-10-19)。
- 24 個語系／路由／快取分層均收齊 30/30 LCP／CLS，HTTP 200 30/30、page error 0。LCP P75 範圍 208–1,896 ms；CLS P75 範圍 0–0.0392，均低於 Q-01 工程門檻。購物車冷載入 LCP P75 約 1.9 秒，仍低於 2.5 秒；未發現需修正的可重現門檻違反。本機 production preview 與合成 fixture 只綁 loopback，沒有連正式 API、SQL、付款服務或 LAN。
- Event Timing 16 ms 門檻下的既有三語互動旅程估算（P75 40／32／32 ms）及 TBT 僅為本機實驗室診斷，不是完整 INP。專案目前沒有公開正式網域或可查 URL-level CrUX 樣本；未加裝第一方 RUM。正式 field INP／來源／期間仍未驗證，所以效能驗收 checkbox 維持未勾選，Issue24 維持 `claimed`。本結果不代表正式 SLA、field Core Web Vitals 或完整 WCAG 通過；iPhone Safari／VoiceOver 維持選配且未驗證。
### Issue24 field INP 資料來源與觀測方案（2026-10-19；待正式環境與治理前置）

- 專案尚未設定正式公開網域，且使用者決定目前不將正式網域納入規劃、不啟用 RUM，也不收集訪客效能事件。若未來正式網域納入規劃並部署，再查詢三語公開且可索引核心頁的 CrUX URL-level desktop 資料；目前優先維持本機合成環境，不新增訪客遙測程式碼。
- 公開且可索引的核心頁，首選查詢 Chrome UX Report（CrUX）URL-level API 的 `interaction_to_next_paint` P75，指定 `formFactor=DESKTOP`，逐一查詢三語首頁／搜尋列表及已選定代表商品頁的 canonical HTTPS URL。每次保存實際 URL、查詢日期、`collectionPeriod.firstDate/lastDate`、各指標 P75 與資料缺漏。CrUX 是 28 日滾動期間；API 不提供可用的逐筆樣本數，因此樣本數欄記為「提供者未揭露」，不自行估算。只有該 URL 的 desktop INP P75 可查且資料期完整時，才可作該 URL 的 field 證據；origin-level、其他語系或其他頁面資料不得替代缺失的 URL-level 結果。
- 購物車／結帳結果屬私人或 `noindex` 頁面，不能假設 CrUX 會提供其 URL-level 樣本；origin-level CrUX 也無法證明這些路由各自達標。這些頁面若要完成 field INP，需先通過第一方 RUM 的隱私與營運治理，再於正式環境觀測。
- 第一方 RUM 的評估方案（尚未核准或實作）：以有效 page visit 作樣本單位，逐一分開 zh-TW／ja／en、四個核心路由模板與 desktop form factor；不以互動事件筆數充當樣本數、不把無 INP 回報的 page visit 填成 0。建議至少連續觀測 28 日，且每個路由／語系分層至少取得 400 筆有效 INP page-visit 樣本。400 筆是本專案建議的最低設計值，約可將 P75 的 95% 秩區間控制在 70.7–79.3 百分位（以二項次序統計量近似）；它不是 CrUX 公布的門檻、也不保證量測無偏。未達任一分層的期間或樣本條件時，該分層維持未驗證，不合併補數。
- RUM 上線前需由產品／隱私責任人確認目的與告知、收集欄位、抽樣率、保存期限、存取／刪除流程及事件上報端點。候選資料僅含路由模板、語系、裝置類別、瀏覽器主版本、產品版本與 Web Vitals 數值；不得收集帳號／session／訂單／付款識別碼、自由輸入內容、完整 URL query、DOM 內容或逐筆操作名稱。必要核准、服務端接收與資料生命週期設計完成前，不引入 SDK、不送出事件。
- RUM 觀測需在正式路由與量測版本穩定後起算，報告首末日期、有效 page visit 樣本數、缺失率及資料排除規則。每個語系／路由分開報告 LCP、INP、CLS P75；不得把缺值補成 0 或跨語系／路由合併。版本、觀測條件未確認或樣本不足時維持未驗證。
- 目前沒有正式網域規劃、CrUX 查詢結果或經核准的 RUM 治理／收集來源。此方案只固定未來網域納入規劃時的查詢方法，不代表法務／隱私審查完成、field 指標通過或 SLA 成立；效能 checkbox 保持未勾選，Issue24 維持 `claimed`。
- 購物車與結帳結果頁為私人／`noindex` 頁面，等待 CrUX 不會自動補足這些路由的 field INP 證據；除非日後另有明確治理決策，否則維持未驗證。CrUX 的正式公開頁結果、origin-level 結果、本機 Event Timing 與 TBT 均不得代替缺少的路由級 field INP。

### 2026-10-19 本機合成購物旅程與網域範圍決策

- 使用者確認目前正式網域暫不納入規劃，先以本機方式啟動環境；本輪選擇完成 Issue24 合成購物／付款旅程，不建立新的 SQL fixture。第一方 RUM 維持停用，CrUX 僅在未來正式網域納入規劃後評估。
- 重新執行 `npm exec -- playwright test tests/issue24-desktop-full-keyboard-outcomes.spec.ts --config=playwright.config.ts --reporter=line --trace=off --output=/private/tmp/issue24-local-keyboard-run`：PASS 3/3（zh-TW／ja／en）。此測試以 Playwright route stubs 合成商品、購物車、報價、結帳與付款狀態，驗證桌面鍵盤旅程及成功、拒絕、未知、處理中與查詢失敗恢復；不呼叫資料庫 API 或付款服務。
- 當前既有 SQL fixture 的 API readiness／商品端點仍為 503，故本輪結果只證明 Storefront 的本機合成 UI 旅程可用，不能證明本機真 API／SQL／worker 全功能可用。先前三語 LCP／CLS lab 矩陣仍可作其明列條件下的工程診斷；正式 field INP 與 SLA 未驗證，效能 checkbox 維持未勾選，Issue24 維持 `claimed`。iPhone Safari／VoiceOver 依決策仍為選配且未驗證。

### 2026-10-19 後續：隔離本機 SQL fixture 準備

- 依使用者接續決策，在獨立暫存 checkout `/private/tmp/kohami-issue24-runtime` 建立新的 dedicated SQL Server 2025 synthetic fixture，loopback port `14337`，strict TLS；migration `0001`–`0012` 與完整 Commerce SQL harness 均 PASS。fixture Compose project marker 為 `kohami-issue06-c4b6be6d4ea8`，設定及私密材料留在該 checkout 的 `.local/m0/`（mode 0600）。既有 `localhost:14336`／`14348` fixtures 未變更，沒有 reset 或連接正式 API／付款／LAN。
- Provisioner 未建立 Owner、原生權限或 permit；fixture 僅證明隔離 SQL schema／Commerce 合成 harness 可用，尚未證明 Issue24 的 live API／worker／Storefront 購物旅程。不得把此結果標成完整功能已啟用或交易驗收完成。
- 主 checkout 已有 API `5080` 與 Storefront `3000` 程序，唯讀 API 探測為 `/health` 200、`/ready` 503；主 checkout runtime 設定指向舊 `localhost:14336/KohamiM0`。本輪未停止既有程序，也未對該 fixture 套 migration。新 fixture 的 API／worker live 驗收待既有服務埠可安全使用後執行。
- 正式網域目前不在規劃、RUM 未啟用，field INP／正式 SLA 未驗證；三語 LCP／CLS lab 證據仍限於各紀錄列明的條件。效能項保持未完成，Issue24 維持 `claimed`；iPhone Safari／VoiceOver 仍為選配且未驗證。

### 2026-10-19 後續：隔離連接埠與本機旅程重驗

- 為避免干擾既有 `localhost:3000`／`127.0.0.1:5080` 服務，新增受限 loopback API port `5080–5099`、Storefront port `3000–3099` 設定，並允許 Nuxt 使用獨立 build directory。使用方式見[本機操作手冊](../../planning/kohami/delivery/LOCAL_DEMO_RUNBOOK.md)。
- HTTPS `localhost:3001` 隔離 Nuxt 實例的桌面三語鍵盤購物／付款結果測試 PASS 3/3；使用標準 Playwright config 的 `KOHAMI_STOREFRONT_ORIGIN` 設定。瀏覽器旅程仍使用 Playwright synthetic API route stubs，沒有連 SQL。
- `/private/tmp/kohami-issue24-runtime` dedicated SQL fixture 的 runner strict TLS 與合成 SKU setup 通過；API 在 60 秒內未 ready，未進入 checkout assertions。Runner 清理本輪建立的 carts、quotes、checkouts、orders、idempotency、reservations 與 SKU rows，並保留私有 API log。主 checkout `.local/m0` 既有 `issue12-*` 私有路徑使唯讀 `--check-only` 拒絕執行；該路徑未變更。API 單次 `/health` 回報 `database-connected`，但 `/ready` 未取得結果，所以隔離 SQL live acceptance 仍未驗證。
- 此項不是效能量測，沒有新增 LCP／INP／CLS 樣本，也不提升 field Core Web Vitals、SLA 或完整交易驗收狀態。效能 checkbox 維持未勾選，Issue24 維持 `claimed`；iPhone Safari／VoiceOver 仍為選配且未驗證。

### Issue24 dedicated SQL API readiness 診斷（2026-10-19）

- 在隔離 checkout 以不覆寫舊證據的新 runner 路徑重試，API `/health` 與 `/ready` 均通過，strict TLS 與合成 SKU 建立通過；但第一個 `POST /carts` 回 `503 SERVICE_UNAVAILABLE`，沒有進入購物／付款斷言。API log 無例外細節，原因未確認。每輪僅清理 runner 記錄的本輪合成資料，保留既有 SQL fixture 與所有舊 log/state；API `5080` 已有 listener，未停止或重用。
- dedicated SQL readiness 不等於 live API 購物功能可用；真 API／SQL／worker 購物旅程仍未驗證。此輪沒有新的 LCP／INP／CLS 資料，field INP 與正式 SLA 維持未驗證；效能 checkbox 不勾選，Issue24 維持 `claimed`。正式網域與 RUM 仍不納入目前規劃，iPhone Safari／VoiceOver 維持選配且未驗證。

### Issue24 dedicated SQL maintenance gate 根因確認（2026-10-19）

- 使用 dedicated fixture strict TLS 與隔離 API loopback port 重跑，API `/ready` 回 200；首個 `POST /carts` 回 `503 SERVICE_UNAVAILABLE`。一次性本機診斷僅記錄 SQL error number/state，結果為 SQL `50002`、state `1`，對應 `CommerceStore` 的 `MAINTENANCE_REQUIRED` gate。
- API 對外維持泛化錯誤回應；本輪未修改產品程式、SQL gate 或 fixture 權限。Runner finally PASS，僅清理本輪登錄的 synthetic rows 與 SKU，fixture 保留。
- 此 dedicated fixture 尚未建立 Owner／permit。不得直接更新 `m0.SecurityState` 或用 DBA／SA 繞過維護狀態；須先依本機操作手冊走原生授權／reconciliation，取得明確啟用證據後再重跑 API／Storefront 購物與付款旅程。此旅程仍未驗證，沒有新增 LCP／INP／CLS 樣本；field INP／正式 SLA 未驗證，效能 checkbox 不勾選，Issue24 維持 `claimed`。正式網域與 RUM 不納入目前規劃，iPhone Safari／VoiceOver 維持選配且未驗證。

### Issue24 三語首頁與商品詳情本機唯讀載入核對（2026-10-19）

- 為避免首頁／商品頁瀏覽器掛載時的購物車載入可能建立 cart，本次只用停用代理的 loopback HTTP GET 檢查，不執行購物車、結帳、付款或 SQL 寫入。Storefront `https://localhost:3001` 三語首頁各 1 次均 HTTP 200；`/zh-TW/products/demo-linen-shirt`、`/ja/products/demo-linen-shirt`、`/en/products/demo-linen-shirt` 各 1 次均 HTTP 200，SSR HTML 分別含正確的繁中、日文、英文商品名稱。API `http://127.0.0.1:5081/health`、`/ready` 及 `/api/v1/products?limit=1` 各 1 次均 HTTP 200；API 回報 mock／`demo_m0`，商品清單回應有 3 筆 items。
- 重現方式（每項為單次 GET spot check）：`for locale in zh-TW ja en; do curl --noproxy '*' -k -sS "https://localhost:3001/${locale}"; curl --noproxy '*' -k -sS "https://localhost:3001/${locale}/products/demo-linen-shirt"; done`；API 依序執行 `curl --noproxy '*' -sS http://127.0.0.1:5081/health`、`.../ready`、`.../api/v1/products?limit=1`。此命令只執行 GET；不得用瀏覽器 hydration 推論頁面不會觸發 cart mutation。
- 此核對確認目前 loopback Storefront SSR 與 mock API 的唯讀資料路徑可載入，不是新的效能測量，也不證明購物／付款寫入旅程或所有頁面功能可用。核心四路由既有 LCP／CLS lab 矩陣仍按其原列設備、節流、樣本及期間解讀；Event Timing 候選不是正式 INP。Field INP、正式 SLA、真實使用者資料與 iPhone Safari／VoiceOver 仍未驗證；效能 checkbox 維持未勾選，Issue24 維持 `claimed`。

### 2026-10-19 後續：三語核心頁 SSR 圖片與開發模組資源核對

- 使用 Python 3.14.8 對既有 `https://localhost:3001` Nuxt 開發伺服器發出 GET，停用代理並忽略 localhost 憑證驗證；沒有啟動瀏覽器或 hydration。zh-TW／ja／en 首頁及 `/products/demo-linen-shirt` 六頁各取 1 個 SSR 樣本，均 HTTP 200、`text/html`。首頁列出 15 張展示商品圖片；商品詳情各列出該商品圖片。去重後 15 個 PNG 資源均 HTTP 200、`image/png`。頁面引用的 21 個不同 Nuxt／Vite 開發模組資源亦均 HTTP 200。
- Vue `?vue&type=style` 開發入口回 `text/javascript`，屬開發模組回應；本次未驗證 production CSS bundle、字型、瀏覽器實際套用樣式、圖片解碼或載入時序。字型資源未出現在本輪 SSR 靜態引用清單。檢查約於 2026-10-19 13:25 UTC 執行；各頁及各唯一資源只請求一次，無 viewport、瀏覽器、CPU／網路節流或快取控制。
- 這是開發模式資源可取用性核對，不是新的 LCP／INP／CLS 樣本，也不證明 API／SQL、購物／付款寫入或 production 資源通過。field INP／正式 SLA 維持未驗證，Issue24 效能合併檢查維持未完成、狀態 `claimed`；iPhone Safari／VoiceOver 仍列選配且未驗證。

### 2026-10-19 後續：三語核心頁隔離 production preview 資源載入

- 從本機 `main` commit `6afe3a3802ed239c70519b17d41d1081cd8c8a13` 的 Storefront 建立 `/private/tmp/kohami-issue24-production-assets-20261019` 隔離副本，沿用唯讀 `node_modules` symlink，執行 `npm run build`：PASS。第一次使用 checkout 外絕對 `NUXT_BUILD_DIR` 的嘗試因 Vite 找不到 `.nuxt/tsconfig.app.json` 失敗；改在隔離副本使用預設相對 build directory 後成功。主 checkout 既存 `apps/storefront/.output` 未覆寫。
- 以 production Nitro server 加動態 loopback 合成 API stub，對 zh-TW／ja／en 首頁及 `/products/demo-linen-shirt` 共 6 個路由各瀏覽 1 次。環境為 macOS 26.6 arm64、Headless Google Chrome 155.0.8059.84、1440×900 CSS px、未節流；CPU 型號與記憶體本次無法確認。頁面 JavaScript 停用，API stub 只供合成商品 GET，拒絕非 GET；stub 記錄非 GET 數為 0。六頁均 HTTP 200／`text/html`、cache-control `no-store`；CSS 失敗資源 0、圖片失敗 0。
- 首頁有 7 個 inline stylesheet，商品詳情有 3 個；皆成功套用於文件，主內容計算樣式為 `system-ui, sans-serif`，最大寬度 1184 px。六次商品圖片均完成解碼、naturalWidth 1024、HTTP 200／`image/png`。`document.fonts.status` 為 `loaded`，但沒有外部字型網路請求；頁面使用系統字型。這只確認本機 production preview 在此 Chrome 條件下的 CSS／圖片呈現，不驗字型檔載入或 iOS／Safari。
- 驗證 runner 位於 `/private/tmp/kohami-issue24-production-assets-20261019/apps/storefront/verify-assets.mjs`。本輪未測購物／付款互動、未啟用 hydration、沒有量測 LCP／INP／CLS、未設定 CPU／網路節流或控制快取，不能推論效能門檻、正式 SLA 或 field Core Web Vitals 通過。Issue24 效能驗收仍未完成、狀態 `claimed`；正式 INP／SLA 未驗證，iPhone Safari／VoiceOver 維持選配且未驗證。

### 2026-10-19 後續：購物車與結帳結果 production 資源呈現

- 延用隔離 production preview 與動態 loopback synthetic API stub，於三語 `/{locale}/cart`、`/{locale}/checkout/checkout-issue24-assets` 各瀏覽 1 次（共 6 次）。macOS 26.6 arm64、Headless Chrome 155.0.8059.84、1440×900 CSS px、未節流 CPU／網路；JavaScript 停用。每頁 HTTP 200／`text/html;charset=utf-8`／`no-store`，各有 3 個 inline stylesheet，未見失敗 CSS、圖片或字型請求；這兩類頁面本身沒有圖片。Cart 主內容為 `system-ui, sans-serif`，checkout 主內容為瀏覽器預設 `Times`；程式碼未見共同全域字體規則，故記為現況，不據此宣告缺陷。`document.fonts.status=loaded`，沒有外部字型請求。
- Checkout SSR 狀態查詢依語系各一次，另有共用 auth session 與商品列表 GET；stub 共觀察 5 個 GET、0 個非 GET。JavaScript 停用時 cart 的 `onMounted` 載入不執行，因此其呈現不代表購物車資料畫面驗收；checkout 使用 synthetic status 供 SSR 呈現。沒有連 SQL、正式 API、付款服務、LAN 或訪客遙測。
- 可重現命令：`node /private/tmp/kohami-issue24-production-assets-20261019/apps/storefront/verify-assets.mjs`（在隔離 preview／stub 可綁定 loopback 的本機執行環境）。每路由僅 n=1，未計算 P75，也未收集 LCP／INP／CLS；不涵蓋互動、水合、快取分層、網路情境或正式 field data。此資源檢查不改變 Issue24 效能驗收狀態；正式 INP／SLA 仍未驗證，效能項維持未勾選。
