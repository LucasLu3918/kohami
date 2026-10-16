# 09: 維護可銷售庫存與缺貨狀態

**What to build:** 營運人員可追溯地調整庫存，商品頁反映 Sellable Stock，缺貨 SKU 無法加入有效購買流程。

**Blocked by:** 08：從後台上架到顧客查看商品

**Status:** ready-for-agent

**Work status:** claimed

- [x] 授權人員維護單一初期倉庫的庫存，Movement 可追溯，Sellable Stock 與 Reservation／扣減明確區分。
- [x] 前台 SKU 庫存與缺貨提示來自權威資料，過期或缺貨狀態不能形成新的有效購買承諾。
- [x] 拒絕越權、不合法數量及會破壞既定庫存不變條件的調整，依政策處理既有保留。
- [ ] 以後台調整至前台結果的旅程驗證成功、拒絕與失敗；結帳時的並行保留另由後續結帳票驗收。

## 本機mock階段規劃補充（2026-10-13）

現行依 [M1詳規](../../planning/kohami/commerce/CATALOG_PLAN.md) 與 [三語內容契約](../../planning/kohami/experience/CONTENT_LOCALE_CONTRACT.md)規劃：商品／價格／量測／圖像可使用明示的合成資料與自有占位，不能宣稱真實商品來源／素材資格已查核。原完整產品／實物驗收保留為正式資料階段要求。商品可售、缺貨、下架與換裝資格分開；cart不保留庫存，Quote與15分鐘Reservation分開。

本輪已完成 Issue 09 本機 mock 實作與 Domain／dedicated SQL 驗收；瀏覽器 Playwright 規格已加入，但尚未在具有 migration history 與 showcase seed 的乾淨 dedicated fixture 執行，因此 Work status 保持 claimed。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：R02–R03、R17／AC02–AC03、AC17；User Stories 14、21、66、79。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Implementation progress

- 新增 `GET /api/v1/admin/stock/{skuId}` 與 `POST /api/v1/admin/stock-adjustments`；Owner／Merchandiser 權限依既有 `stock.adjust` scope。Movement 保存 actor、reason、delta、OnHand／Reserved／Sellable 快照及時間。
- Inventory.Stock 表示可售量；OnHand 為可售量加 Held Reservation。調整以既有 Commerce transaction／security lock 執行，不能減至保留量以下；同一 idempotency key 不重複異動。
- 商品頁顯示權威 SKU Sellable Stock／缺貨狀態。CartItem 與 Quote 會重查目前庫存及數量，缺貨 SKU 不形成新的購買承諾。
- PASS：`dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false`；Domain acceptance（含 7 個庫存規則案例）；`dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll issue09-stock-sql <dedicated-config>`（8 項 SQL acceptance）；`npm run typecheck`；`npm run build`。
- NOT PASSED：Playwright 瀏覽器旅程尚未執行成功。既有 fixture 遷移回報 `SQL_MIGRATION_BASELINE_REQUIRED`（SQL 51120），且該 fixture 未提供 showcase catalog seed；未重建或重設資料庫。使用全新 disposable dedicated fixture 後執行 `scripts/m0/run-issue06-aftercare-acceptance.py --stock-only`。
- 完整 Commerce SQL suite 另被既有 `DatasetReadIsolationAcceptance` 假設 `sku_demo_01` 有庫存所阻擋；此資料當時為 OutOfStock。Issue 09 獨立 SQL acceptance 已通過。

## Comments

- 2026-10-17：Issue 09 實作與 targeted SQL acceptance 完成；瀏覽器驗收受 dedicated fixture 缺 migration baseline／showcase seed 阻擋，Work status 維持 claimed，先建立乾淨 disposable fixture 再解除。

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。

