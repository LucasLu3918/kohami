# 08: 從後台上架到顧客查看商品

**What to build:** 授權人員建立 Product 與唯一 SKU，發布真實照片、材質、尺寸和售價；顧客可查看，越權及不完整上架被拒絕。

**Blocked by:** 06：確認商店技術方案與品質基準

**Status:** ready-for-agent

**Work status:** claimed

- [x] 本機 mock 從 Owner 後台建立 Product 與顏色／服裝尺寸 SKU，到顧客查看、改價重發及下架的完整旅程已驗證；正式商品資料另依本票範圍驗收。
- [ ] 商品頁提供真實照片、材質、衣服量測／尺寸表與權威 TWD 售價；素材不是本人照片或 AI 生成流程。
- [x] 必要三語內容缺漏、重複 SKU／variant、遠端或非合成媒體及零價由 Domain 拒絕；未發布商品不公開，匿名建立遭拒，公開快照不含素材權利參考。
- [ ] 商品與價格變更保留依矩陣要求的 Audit Record，驗證成功、拒絕及保存／讀取失敗的外部行為。

## 本機mock階段規劃補充（2026-10-13）

現行依 [M1詳規](../../planning/kohami/commerce/CATALOG_PLAN.md) 與 [三語內容契約](../../planning/kohami/experience/CONTENT_LOCALE_CONTRACT.md)規劃：商品／價格／量測／圖像可使用明示的合成資料與自有占位，不能宣稱真實商品來源／素材資格已查核。原完整產品／實物驗收保留為正式資料階段要求。商品可售、缺貨、下架與換裝資格分開；cart不保留庫存，Quote與15分鐘Reservation分開。

本輪僅新增規劃與靜態線框；本票仍未完成，核取／依賴／Work status不改，MFA待決不被默認解決。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：R02、R07／AC02、AC07；User Stories 11–14、64、67、79–80。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。
- 2026-10-16：本次實作依本機 mock 範圍進行；使用者確認由 Owner 建立商品，Merchandiser 僅管理已授權商品，以保留現行 Product scope 與 MFA 規則。
- 2026-10-16：已新增 Catalog draft／public snapshot、Owner 建立與 Product 範圍管理 API、SKU 價格發布／下架稽核、公開商品頁與管理頁，以及領域驗證案例。此工作仍為 claimed：本機依賴還原未完成，dedicated SQL 與瀏覽器旅程尚未執行，因此驗收清單維持未勾選；不代表本票 resolved。
- 2026-10-16：完成 Issue 08 本機 mock acceptance。Dedicated SQL 2025 fixture（localhost:14337，migration 0006）與 Playwright 1/1 通過：Owner 建立、slug 衝突拒絕、草稿隱藏、發布與公開頁、公開資料遮罩、改價不提前改 public snapshot、重新發布、下架 404；SQL 核對最終 Unpublished／Inventory 價格／create、update、publish、unpublish audit，並清除合成列及 Owner 權限。API 授權重驗改由 `m0.security_catalog_actor_allowed` 在 security lock 內執行，API 不需讀取 `m0.Grants`。本機中性占位與材質／量測均為合成資料；真實商品照片、來源權利及商品資訊未驗證，保存／讀取失敗的外部行為亦未測，因此本票仍 claimed，不代表正式商品驗收完成。原 14336 fixture 因缺 `ops.SchemaMigrations` 於 DDL/DML 前安全停止，未採用舊 baseline。

