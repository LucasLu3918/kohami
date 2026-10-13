# KOHAMI 規劃文件索引

## 現行入口與版本限制

優先閱讀 [現行政策 v0.2](../../../.scratch/kohami/proposals/README.md) 及 [全流程 mock 詳規](../../../.scratch/kohami/proposals/04-demo-commerce-policy.md)。目前交付為 mock 展示，已選 ASP.NET Core／Nuxt SSR／SQL Server；17個純規則測試及獨立 SQL 開發環境已驗證，產品尚未完成。

**Word v0.1 已過時：** 以下 Word 連結保留為原規劃快照，不包含後續政策／技術決策，不可作為現行驗收來源。本輪維護 Markdown 政策，未重製 Word；若需新整合閱讀版，應依現行來源重製並逐頁驗證。本文後续 v0.1「空目錄／無Git／未選技術」屬建立時紀錄，不是現在狀態。

版本 v0.1｜日期 2026-10-13｜狀態 READY_FOR_REVIEW（文件可審閱，產品尚未批准實作）

## 閱讀入口

1. [產品規劃書](PRODUCT_PLAN.md)：產品目標、交付範圍、旅程、需求、領域、架構候選及成本。
2. [Word 規劃書](KOHAMI-規劃書-v0.1.docx)：整合主文、驗證、術語、決策與 ADR 的審閱版本。
3. [驗證與批准計畫](VALIDATION_PLAN.md)：外部查核、需求驗收及分層批准。
4. [術語表](GLOSSARY.md)：統一重要概念及邊界。
5. [決策與假設](DECISIONS_ASSUMPTIONS.md)：D396–D446、歷史取代關係及來源完整性。
6. [ADR-0001](../../adr/0001-preset-avatar-fitting.md)：固定非 AI 三視圖的理由、替代與再審議條件。
7. [本次異動範圍](CHANGE_IMPACT.yaml)：本地文件邊界，與產品實作授權分開。

Markdown 是後續維護來源；Word v0.1 是歷史整合閱讀快照，已明示過時；現行政策以 Markdown 更新為準，重製新版 Word 時須渲染驗證。

## 套件狀態與適用性

| 文件面向 | 所在位置／狀態 | 說明 |
| --- | --- | --- |
| 產品計畫與需求 | PRODUCT_PLAN §§1–4 | 可審閱需求基準 |
| UX 與視覺方向 | PRODUCT_PLAN §3、§6 | 頁面責任及既定風格；完整線框／視覺系統待設計 |
| 領域模型 | PRODUCT_PLAN §§5–7、GLOSSARY | 概念責任及不變條件；非資料庫契約 |
| 技術架構 | PRODUCT_PLAN §8 | 候選比較待實證及選型 |
| 驗證／品質／批准 | VALIDATION_PLAN | 計畫已定義，產品驗證 NOT_EXECUTED |
| 分期／成本／人力 | PRODUCT_PLAN §§2、9–11 | 順序已定；報價、估算及日期未定 |
| 決策與來源 | DECISIONS_ASSUMPTIONS、ADR | 可見新決策直接追溯，舊決策依總結保留 |
| 外部研究報告 | 本次 N/A | 任務是整理既有訪談，沒有授權執行外部查核 |
| API／Schema／實作 Profile | 當前 N/A | 未批准實作且技術／業務細節待定；開工前另建 |
| 可執行產品／部署／正式合規結論 | 本次 N/A | 本次只製作文檔，未實作或對外發布 |

不建立選型已定或工程可開工的假象。未決事項依外部證據、完整來源及詳規審查補齊。

## 來源與重現

來源為指定對話「設計樹訪談」，ID `6acc802e-b1b0-83e8-84f7-bacbe80d021a`，現行基準為 Round 18。實際來源範圍與 S14–S18 定位見決策登錄，不保存原始聊天、工具參數或執行輸出。

本次明確授權將規劃文件放在 `/Users/lucas/Coding/main/berry-match`；此處初始為空且不是 Git 專案。僅把 KOHAMI 的文件收在專屬子目錄，不推定既有專案更名、建庫或開發批准。

閱讀其他文件不需隱藏聊天上下文。重建 Word 所需內容皆在此套件；編排採繁體中文、層級標題、可編輯表格及頁碼。後續重新產生後仍需渲染與逐頁檢查。
