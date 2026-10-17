# 08: 從後台上架到顧客查看商品

**What to build:** 授權人員建立 Product 與唯一 SKU，完成本機合成展示商品的建立、發布、顧客瀏覽、改價與下架；正式販售所需的真實商品事實與素材權利依 Issue 07 查核。

**Blocked by:** 06：確認商店技術方案與品質基準

**Status:** ready-for-agent

**Work status:** resolved

- [x] 本機 mock 從 Owner 後台建立 Product 與顏色／服裝尺寸 SKU，到顧客查看、改價重發及下架的完整旅程已驗證；正式商品資料另依本票範圍驗收。
- [x] 本機展示目錄提供 15 件合成商品、生成圖片、S/M/L 估算材質／量測與 TWD 示意價格；頁面明示非實物、非實測與估算值。
- [x] 必要三語內容缺漏、重複 SKU／variant、遠端或非合成媒體及零價由 Domain 拒絕；未發布商品不公開，匿名建立遭拒，公開快照不含素材權利參考。
- [x] 商品與價格變更保留依矩陣要求的 Audit Record，驗證成功、拒絕及保存／讀取失敗的外部行為（本機 synthetic SQL/API 範圍）。

## 本機mock階段規劃補充（2026-10-13）

現行依 [M1詳規](../../planning/kohami/commerce/CATALOG_PLAN.md) 與 [三語內容契約](../../planning/kohami/experience/CONTENT_LOCALE_CONTRACT.md)規劃：商品／價格／量測／圖像可使用明示的合成資料與自有占位，不能宣稱真實商品來源／素材資格已查核。原完整產品／實物驗收保留為正式資料階段要求。商品可售、缺貨、下架與換裝資格分開；cart不保留庫存，Quote與15分鐘Reservation分開。

本輪僅新增規劃與靜態線框；本票仍未完成，核取／依賴／Work status不改。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：R02、R07／AC02、AC07；User Stories 11–14、64、67、79–80。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。
- 2026-10-16：已新增 Catalog draft／public snapshot、Owner 建立與 Product 範圍管理 API、SKU 價格發布／下架稽核、公開商品頁與管理頁，以及領域驗證案例。此工作仍為 claimed：本機依賴還原未完成，dedicated SQL 與瀏覽器旅程尚未執行，因此驗收清單維持未勾選；不代表本票 resolved。
- 2026-10-16：完成 Issue 08 本機 mock acceptance。Dedicated SQL 2025 fixture（localhost:14337，migration 0006）與 Playwright 1/1 通過：Owner 建立、slug 衝突拒絕、草稿隱藏、發布與公開頁、公開資料遮罩、改價不提前改 public snapshot、重新發布、下架 404；SQL 核對最終 Unpublished／Inventory 價格／create、update、publish、unpublish audit，並清除合成列及 Owner 權限。API 授權重驗改由 `m0.security_catalog_actor_allowed` 在 security lock 內執行，API 不需讀取 `m0.Grants`。本機中性占位與材質／量測均為合成資料；真實商品照片、來源權利及商品資訊未驗證，保存／讀取失敗的外部行為亦未測，因此本票仍 claimed，不代表正式商品驗收完成。原 14336 fixture 因缺 `ops.SchemaMigrations` 於 DDL/DML 前安全停止，未採用舊 baseline。
- 2026-10-16：Issue 08 failure-path follow-up PASS。全新 dedicated SQL2025 fixture（14337，strict TLS，migration 0001–0006）上，Playwright 驗證 Product 建立中的 SQL 失敗回泛化 `503 SERVICE_UNAVAILABLE`；SQL 確認沒有 Product／SKU／Inventory／create audit／idempotency 殘留。暫時撤除 Catalog SKU read 權限時，公開讀取回泛化 503 且無部分商品 payload；隨後恢復 canonical grant，Owner 下架仍成功。完整建立／改價／發布／下架旅程與 SQL audit 驗收亦通過。Runner 清除 synthetic 商品、身份、權限及故障注入；僅刪除本次唯一 Compose fixture／volumes／network。真實商品照片、來源權利及已確認商品資訊仍缺，Work status 維持 claimed。
- 2026-10-17：使用者明確將本機展示網站的首批商品改為合成資料，不要求真實商品事實。新增 15 個本機模型生成商品圖、合成材質／S-M-L 衣服量測及估算 TWD 售價的工作已開始；此決定僅適用展示網站，不代表正式販售所需商品與權利資料已查核。完成本機目錄驗收前 Work status 維持 claimed；Issue 09 尚未開始。

## Resolution

- 完成範圍：Issue 08 的本機 mock 與合成展示資料；不代表正式商品來源、材質、量測、售價或素材權利已查核。這些正式資料由 Issue 07 負責。
- 驗收證據：新建 dedicated SQL2025 fixture（localhost:14337，strict TLS），依序套用 migration 0001–0007；Commerce SQL provisioning assertions 通過。`python3 scripts/m0/run-issue06-aftercare-acceptance.py --catalog-only` 通過 4/4 Playwright 案例與 SQL 驗證，涵蓋首頁 15 張商品卡／圖片、首件商品頁與尺寸表、Owner 建立／改價／發布／下架、SQL 保存失敗回復、公開讀取失敗遮罩及權利參考不外洩。Runner 清除本輪測試列與授權，保留 dedicated fixture；未執行正式商品或權利查核。
- 後續交接：下一項為 Issue 09「維護可銷售庫存與缺貨狀態」。

