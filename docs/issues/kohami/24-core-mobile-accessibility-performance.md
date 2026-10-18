# 24: 核心購物的鍵盤可及性與效能驗收

**What to build:** 桌面瀏覽器可用鍵盤完成核心購物旅程；以明確記錄的本機條件量測核心頁效能並修正問題。iPhone Safari 與 VoiceOver 人工驗收延後為[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)，不作本票目前結案門檻。

**Blocked by:** 17：三語購物旅程與可索引網址；22：購物政策與客服入口

**Status:** ready-for-agent

**Work status:** claimed

- [ ] 以桌面瀏覽器鍵盤驗證首頁、搜尋、SKU、購物車、報價、結帳及付款結果的操作、焦點與錯誤／恢復狀態；響應式尺寸維持自動化 viewport 覆蓋。
- [ ] 對三語核心頁量測 LCP P75 ≤ 2.5 秒、INP P75 ≤ 200 ms、CLS P75 ≤ 0.1，記錄硬體、瀏覽器、網路、樣本、期間與限制；不以本機結果宣稱正式 SLA。
- [ ] 核心旅程成功、拒絕、等待確認與失敗恢復可操作，修復發現問題後重驗受影響旅程。
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

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。


### 2026-10-18 後續：桌面 Chrome 原生縮放窄視窗補強

- 桌面 Chrome 200% 檢視購物車、結帳結果與會員訂單時主要內容可讀；400% 在目前視窗形成低於 320 CSS px 的有效版面，原會員訂單標題／訂單編號被擠窄。此視窗不等同 1280px 基準下標準 320px 的 400% 等效驗收。
- 為 ≤240px 補上購物車單欄、結帳摘要垂直排列、會員訂單長字串折行；原縮放等效 Playwright 案例加上 160px 補充寬度，保留 640px／320px 驗收。重建後以目前 Chrome 縮放工作階段逐頁查看三個本機展示路由，AX 樹仍列出主要標題、資料與控制項；此人工查看未量測 `scrollWidth`，不作為 400% 無溢位簽核。
- `npm run typecheck` 與 `npm run build` PASS。Playwright 指令在瀏覽器啟動階段失敗：本機 Chrome headless process SIGABRT，Playwright 清理回報 `kill EPERM`；尚未執行到 viewport 斷言，因此 160px 回歸仍待可用的 headless runner 驗證。
- 僅修改展示版 CSS、測試與驗收紀錄；未變更 API、資料、交易、權限或付款。Issue24 維持 `claimed`；本機窄視窗縮放、完整全站原生縮放及對比仍未簽核，iPhone／VoiceOver 維持未驗證並列於[選配改善](../../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。
