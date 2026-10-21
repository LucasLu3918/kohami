# 10: 從首頁搜尋與篩選商品

**What to build:** 手機使用者從品牌首頁找到商品，理解篩選、無結果與缺貨狀態，進入正確商品頁。

**Blocked by:** 09：維護可銷售庫存與缺貨狀態

**Status:** ready-for-agent

**Work status:** resolved

- [x] 手機首頁有明確品牌與商品入口，可搜尋、篩選並開啟對應 Product／SKU 資訊。
- [x] 搜尋結果、無結果、載入中、讀取失敗及缺貨狀態清楚可操作，不顯示未發布商品。
- [x] 控制項具可辨識名稱、合理焦點與鍵盤操作，商品圖有適當替代說明。
- [x] 完整旅程的驗證不依賴內部元件或搜尋實作；Phase 1 不以換裝素材或完整 Lookbook 作為使用前提。

## 本機mock階段規劃補充（2026-10-13）

現行依 [M1詳規](../../planning/kohami/commerce/CATALOG_PLAN.md) 與 [三語內容契約](../../planning/kohami/experience/CONTENT_LOCALE_CONTRACT.md)規劃：商品／價格／量測／圖像可使用明示的合成資料與自有占位，不能宣稱真實商品來源／素材資格已查核。原完整產品／實物驗收保留為正式資料階段要求。商品可售、缺貨、下架與換裝資格分開；cart不保留庫存，Quote與15分鐘Reservation分開。

本票實作使用伺服器端 SQL 搜尋與篩選、手機首頁控制項及公開商品連結。現有 Catalog 公開資料沒有分類欄位，因此本票提供文字、顏色、衣服尺寸與庫存篩選；`category` 仍回 422，須先採用並保存分類欄位後再擴充。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：User Stories 6–10；N01。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-17：Issue 09 已在全新 disposable dedicated fixture 通過瀏覽器驗收並 resolved；Issue 10 開始執行。

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。

## Resolution

- 完成 `GET /products` 的參數 allowlist、NFKC 搜尋正規化、locale、同一 active SKU 顏色／尺寸／庫存匹配、穩定排序與綁定查詢條件的 cursor；維持既有 `items` shape 並增加 `nextCursor`。
- 首頁提供三語搜尋、顏色／尺寸／庫存篩選、載入更多、讀取錯誤重試、無結果、缺貨提示、商品頁連結與購物車操作；元件使用標籤、鍵盤可操作表單、圖片替代文字及窄螢幕排版。
- 確認來源：[商品詳規](../../planning/kohami/commerce/CATALOG_PLAN.md)、[OP-01 API 規劃](../../planning/kohami/contracts/API_SPEC.md)、Issue 09 已 resolved。
- PASS：`dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false`；Commerce domain acceptance 內 8 個 Catalog search query rules；`dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false`；Storefront `npm run typecheck`、`npm run build`；`python3 -m unittest tests/foundation/test_issue06_acceptance_runner.py tests/architecture/test_layout.py -v`（12/12）。
- PASS：使用 marker-verified、strict-TLS、disposable dedicated SQL Server fixture（port 14337）執行 `python3 scripts/foundation/run-issue06-aftercare-acceptance.py --search-only`；Playwright 2/2。覆蓋手機寬度搜尋／商品導覽、同 SKU 顏色與尺寸、搜尋 SKU、未知參數 422、in-stock 正反例、cursor 分頁、離線讀取錯誤／重試與空結果。runner 還原測試用 SKU 原庫存並清除 synthetic Owner/session；fixture 保留供後續工作使用。
- 限制：目前公開 Catalog JSON／管理草稿沒有 category 欄位，故分類篩選仍回 422，未推測或新增分類資料。要補 category，先定義其資料所有權與管理／公開契約，再新增分類儲存與搜尋驗收。此限制不涵蓋下架／未發布資料洩漏，SQL 查詢仍限定 `State='Published'` 與 active/included SKU。

