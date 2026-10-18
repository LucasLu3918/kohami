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

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。

