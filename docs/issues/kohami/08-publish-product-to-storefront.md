# 08: 從後台上架到顧客查看商品

**What to build:** 授權人員建立 Product 與唯一 SKU，發布真實照片、材質、尺寸和售價；顧客可查看，越權及不完整上架被拒絕。

**Blocked by:** 06：確認商店技術方案與品質基準

**Status:** ready-for-agent

**Work status:** claimed

- [ ] 從後台登入、建立 Product 與顏色／服裝尺寸 SKU、保存到前台顯示的完整旅程可驗證，SKU 識別不重複。
- [ ] 商品頁提供真實照片、材質、衣服量測／尺寸表與權威 TWD 售價；素材不是本人照片或 AI 生成流程。
- [ ] 必要資訊缺漏、重複 SKU、未發布商品與越權操作被拒絕，顧客只取得可公開內容。
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

