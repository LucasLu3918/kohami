# KOHAMI｜現行專案文件

> 基準：2026-10-24 `main@7427c94`，靜態核對原始碼與 SQL DDL；**本次沒有連線實際 SQL 或執行新一輪 .NET/Playwright 驗收**。外層只描述現有系統；未完成的工作全部在 `todo/`。

## 一頁式總覽

[**開啟完整 HTML 總覽**](project-overview.html)：雙模式、現行架構／交易資料流、API 操作索引、資料庫表字典、尚缺的工作與選配及矛盾；可離線用瀏覽器檢視與搜尋。

## 現行架構與運行資料

| 文件 | 範圍 |
|---|---|
| [architecture.md](architecture.md) | Nuxt 3/Vue + ASP.NET Core/.NET 10 + SQL Server 2025 模組化單體、mock/API 切換、現行交易與售後流程，附獨立 SVG 圖 |
| [api-spec.md](api-spec.md) | 程式已掛載 53 項 method+path 的 Header、Req/Res、錯誤與契約覆蓋狀態 |
| [database-schema.md](database-schema.md) | 由原始 DDL/Migration 整理之 30 張表、型別、欄位、用途及 schema（**非 live DB dump**） |
| [operations.md](operations.md) | 目前本機開發啟動、建置、測試和 CI 命令 |
| [contracts/runtime/Core.openapi.json](contracts/runtime/Core.openapi.json) | API 現行使用的 27 個 runtime OpenAPI operations |
| [quality/VALIDATION_PROFILE.yaml](quality/VALIDATION_PROFILE.yaml) · [SECRET_SCAN_POLICY.yaml](quality/SECRET_SCAN_POLICY.yaml) · [requirements-validation.txt](quality/requirements-validation.txt) | GitHub CI / security gate 實際讀取的執行設定 |

## 商品穿搭影像與視覺方向

| 文件 | 範圍 |
|---|---|
| [agents/model-image-generation.md](agents/model-image-generation.md) | 本機商品穿搭圖生成規範、固定模型 profile、生成前檢查、身份一致性與可選衣物遮罩改色後處理 |
| [planning/kohami/experience/VISUAL_SYSTEM.md](planning/kohami/experience/VISUAL_SYSTEM.md) | KOHAMI 商店視覺方向、商品呈現與模特兒影像原則 |

角色身份、不可變辨識特徵及人工驗收基準見 [`templates/creative/models/kohami-editorial-model-01/IDENTITY_GUIDE.md`](../templates/creative/models/kohami-editorial-model-01/IDENTITY_GUIDE.md)；日系造型、場景／姿勢選項、提示骨架與失敗案例以同目錄 [`PROMPT_LIBRARY.yaml`](../templates/creative/models/kohami-editorial-model-01/PROMPT_LIBRARY.yaml) 為準。兩者補足操作知識，不取代本節的生圖流程文件。

## 唯一的未完成規劃目錄

`docs/todo/` **恰好三個檔案**：
- [plan.md](todo/plan.md)：必需但尚未交付、或實際證據仍未完成的部分，含依賴與驗收
- [options.md](todo/options.md)：可選改善方案；預設未開啟
- [confuse.md](todo/confuse.md)：歷史說法與現行程式矛盾、裁決與待確認

**圖解**：[系統架構](images/system-architecture.svg)｜[交易資料流](images/order-data-flow.svg)，圖片不計入文件 15 檔上限。

## 現行真實狀態

`apps/storefront`（Nuxt，mock 預設，API 模式僅本機）、`apps/api` 和 `src/Modules`（IdentityAccess、Catalog、Commerce）、`infra/kohami-sql` 和 `tools/native-operations/migrations`（`Kohami` / `kohami_local`）。新 checkout 僅限會員。Wallet top-up 是 synthetic card/mock credit，不是正式收款；Cloudflare static demo 不等於 production。

任何未經實證、正式上線/外部 provider、四角色三視圖換裝、監控與復原要求均移入三份 todo；**完成的架構/行為不重複列待辦**。不把待批准規劃寫作既有功能。歷史中受 hash-bound contract 驗證器依賴的輸入檔案移至 `tests/fixtures/kohami-legacy/`，僅供**歷史回歸測試**而非文件權威。

文件總數：**15 個檔案**（SVG 圖除外）。本機生成與視覺方向文件分別以 `agents/` 與 `planning/` 中的上述文件為準。
