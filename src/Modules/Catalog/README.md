# Catalog

責任：商品與 SKU 草稿、三語內容、公開版本、商品價格發布與目錄稽核。

目前實作供本機 mock 階段使用：Owner 可建立合成商品草稿；Owner 或取得 Product 範圍 `catalog.write` 的 Merchandiser 可讀取、更新、發布及下架商品。非 Owner 管理操作要求已完成 MFA。Owner 建立商品可使用既有密碼驗證階段，並在寫入交易中重新確認有效 Owner／Dataset grant、session、安全版本、帳號狀態及 maintenance 狀態。

Catalog 保存 draft 與 public snapshot；公開查詢只讀 Published snapshot，價格以該 SKU 公開價格為準，可售狀態則從 Commerce Inventory 即時讀取。發布、下架、商品/SKU 資料、庫存價格及 audit event 在同一 SQL transaction 中更新。建立、更新、發布／下架使用 `m0commerce.Idempotency`，避免同一 actor 重送重複操作。

展示目錄包含 15 個合成商品，透過 `0007_catalog_demo_showcase` migration 建立 45 個 S/M/L SKU、估算材質／衣服尺寸及 TWD 價格。每項圖片由已快取的本機 Z-Image-Turbo 模型生成，存於 `/demo-assets/products/<slug>.png`；`synthetic_ai_generated_demo` 是限定的合成素材標記，公開 DTO 不回傳權利參考。合成商品與價格、庫存均只供展示，不代表實際商品、實測、供應量或正式販售條件。

保留 `/demo-assets/neutral.svg` 作為中性合成占位。展示 seed 的初始庫存為合成數值；Owner／Merchandiser 的日常庫存維護與正式庫存行為由 Issue 09 驗收。正式商店仍需另行核實實際商品資料、素材來源權利、材質／量測與售價。

SQL schema 由 native migration `0005_catalog` 管理；授權交易重驗由 IdentityAccess 的 `0006_catalog_authorization_recheck` 提供。API 僅獲得 Catalog 明確物件權限、`Inventory` INSERT 與該程序 EXECUTE，不增加資料庫角色、table grants 或 schema 廣泛權限。

Issue 08 本機 mock journey 已通過 dedicated SQL／瀏覽器 acceptance；不代表真實商品資料、照片權利或正式營運驗收。未完成範圍見 [Issue 08](../../../docs/issues/kohami/08-publish-product-to-storefront.md)。

[完整模組責任表](../../../docs/architecture/MODULES.md)
