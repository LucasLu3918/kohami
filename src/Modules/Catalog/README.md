# Catalog

責任：商品與 SKU 草稿、三語內容、公開版本、商品價格發布與目錄稽核。

目前實作供本機 mock 階段使用：Owner 可建立合成商品草稿；Owner 或取得 Product 範圍 `catalog.write` 的 Merchandiser 可讀取、更新、發布及下架商品。非 Owner 管理操作要求已完成 MFA。Owner 建立商品可使用既有密碼驗證階段，並在寫入交易中重新確認有效 Owner／Dataset grant、session、安全版本、帳號狀態及 maintenance 狀態。

Catalog 保存 draft 與 public snapshot；公開查詢只讀 Published snapshot，價格以該 SKU 公開價格為準，可售狀態則從 Commerce Inventory 即時讀取。發布、下架、商品/SKU 資料、庫存價格及 audit event 在同一 SQL transaction 中更新。建立、更新、發布／下架使用 `m0commerce.Idempotency`，避免同一 actor 重送重複操作。

素材限制為本機自有合成占位 `/demo-assets/neutral.svg`；不代表真實商品、照片、材質或權利已查核。SKU 新建時庫存為 0，調庫存由後續 Issue 09 負責。

SQL schema 由 native migration `0005_catalog` 管理，僅在 `KohamiM0` 套用。API 只獲得 Catalog 明確物件權限與 `Inventory` INSERT；不增加資料庫角色或 schema 廣泛權限。

Issue 08 的實作已接上 API 與 storefront，但完整 dedicated SQL／瀏覽器驗收尚未執行；未執行的案例不可視為 PASS。下一步依 Issue 08 清單補驗收，再開始 Issue 09。

[完整模組責任表](../../../docs/architecture/MODULES.md)
