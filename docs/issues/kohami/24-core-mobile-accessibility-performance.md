# 24: 核心購物的手機、可及性與效能驗收

**What to build:** 完整購物旅程可用鍵盤及輔助技術操作；依確認的裝置／網路矩陣驗證核心頁效能並修正問題。

**Blocked by:** 17：三語購物旅程與可索引網址；22：購物政策與客服入口

**Status:** ready-for-agent

**Work status:** claimed

- [ ] 依已確認可及性與裝置矩陣，驗證首頁至搜尋、SKU、結帳、付款、會員及售後的名稱、焦點、錯誤提示與輔助技術操作。
- [ ] 對三語、手機版及已定核心頁量測 LCP P75 ≤ 2.5 秒、INP P75 ≤ 200 ms、CLS P75 ≤ 0.1，記錄網路、樣本、期間與限制。
- [ ] 核心旅程成功、拒絕、等待確認與失敗恢復可操作，修復發現問題後重驗受影響旅程。
- [ ] 樣本不足或量測條件未確認時列為未驗證，不以單次開發機量測宣稱正式營運達標。

## M4規劃追蹤（2026-10-13）

參照[M4驗收／故障復原](../../planning/kohami/delivery/VALIDATION_PLAN.md)與[本機操作](../../planning/kohami/delivery/LOCAL_DEMO_RUNBOOK.md)。本輪補齊規劃與證據模板，產品NOT_EXECUTED；原驗收清單、依賴及工作狀態維持。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：N01；User Stories 6–7；核心 R01–R07／AC01–AC07。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Resolution（2026-10-18；三語鍵盤略過導覽子範圍）

- 共用 Storefront 應用殼新增繁中／日文／英文略過導覽連結；首頁、商品、購物車、結帳、會員、政策、客服及後台頁面皆提供可聚焦的 `main-content` 目標。
- 新增 `apps/storefront/tests/issue24-accessibility.spec.ts`：在 390×844 viewport 的三語政策頁驗證略過連結是第一個 Tab 停靠點、鍵盤啟用後焦點移至主內容、網址片段正確且沒有水平溢位。先確認舊版因找不到連結而失敗，再實作後 PASS，Playwright 1/1。
- `npm run typecheck` PASS；相依 Issue 22 `tests/issue22-policies-support.spec.ts` 回歸 PASS 3/3，涵蓋三語頁面、390px 寬度及既有鍵盤焦點。
- 此子範圍不代表完整無障礙或效能驗收。當時未執行 VoiceOver／Safari、對比、200%／400% 縮放、全核心旅程的錯誤／等待／恢復操作，也未收集效能樣本；後續取樣見下節。正式硬體／網路矩陣及正式 SLA 均未驗。Issue 24 維持未結案。
- iPhone Safari／VoiceOver 驗收：**UNVERIFIED**。裝置為 iPhone 15 Pro Max、iOS 27.0.1；本輪未在 iPhone 上操作 Safari 或 VoiceOver。Mac 預覽連線回報 `ERR_CERT_AUTHORITY_INVALID`，未安裝或信任任何 CA 憑證，且已停止 LAN 預覽；此錯誤不代表 iPhone 本身的憑證信任結果。

### 2026-10-18 後續：結帳狀態的鍵盤恢復與本機效能樣本

- 鍵盤驗收發現結帳結果頁在重新查詢時把按鈕設為原生 `disabled`，Chrome 會移除其焦點。改以 `aria-disabled`／`aria-busy` 呈現忙碌狀態，既有 `refreshing` 守門仍阻止重複請求；完成後焦點留在原按鈕。
- 新增 `apps/storefront/tests/issue24-checkout-keyboard.spec.ts`：以 Tab／Enter 操作狀態刷新，驗證 Processing、Succeeded、Failed、Unknown 與 503 恢復；焦點保留、Unknown 不提供重新啟動付款、最後成功讀取的金額快照保留。新案例 PASS 1/1；Issue13 結果／失敗恢復及 Issue24 三語略過導覽合併回歸 PASS 4/4；`npm run typecheck` PASS。
- 本機首頁診斷取樣 30 次：Mac arm64、Chrome 155.0.8059.84、1280×800、本機 HTTPS、無網路／CPU 節流、合成 API mock。LCP P75 40 ms、CLS P75 0；Event Timing 記錄到 ≥16 ms 互動事件 25/30 次，該子集 P75 24 ms。INP 數值不完整，因事件門檻排除了較短事件；結果僅供本機比較，不能代表完整 Web Vitals、其他頁面、iPhone 或正式網路／SLA。
- Safari／VoiceOver 仍 **UNVERIFIED**。尚未完成從首頁、搜尋、SKU、購物車到售後的完整鍵盤／輔助技術旅程，也未在手機及已確認網路矩陣上量測效能；Issue24 未結案。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。

