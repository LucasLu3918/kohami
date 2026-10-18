# 24: 核心購物的手機、可及性與效能驗收

**What to build:** 完整購物旅程可用鍵盤及輔助技術操作；依確認的裝置／網路矩陣驗證核心頁效能並修正問題。

**Blocked by:** 17：三語購物旅程與可索引網址；22：購物政策與客服入口

**Status:** ready-for-agent

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
- 此子範圍不代表完整無障礙或效能驗收。未執行 VoiceOver／Safari、對比、200%／400% 縮放、全核心旅程的錯誤／等待／恢復操作，亦未完成 30 次 LCP／INP／CLS 樣本；正式硬體／網路矩陣及正式 SLA 均未驗。Issue 24 維持未結案。
- iPhone Safari／VoiceOver 驗收：**UNVERIFIED**。裝置為 iPhone 15 Pro Max、iOS 27.0.1；本輪未在 iPhone 上操作 Safari 或 VoiceOver。Mac 預覽連線回報 `ERR_CERT_AUTHORITY_INVALID`，未安裝或信任任何 CA 憑證，且已停止 LAN 預覽；此錯誤不代表 iPhone 本身的憑證信任結果。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。

