# data-flow

Commerce 同一 SQL transaction 裁決訂單、庫存、付款與退款；Fulfillment 不直接改帳，前端不裁決交易。Catalog 發布時同一 transaction 更新商品公開 snapshot、SKU／Inventory 價格與稽核事件；公開讀取只組合 Published snapshot 與有效 SKU 庫存。

2026-10-17 storefront homepage sends locale, q, colorId, garmentSize, availability, sort, limit and cursor to public `GET /products`. Catalog SQL searches Published public translation/SKU data and joins active `identity_access.CatalogProductSkus` with `commerce.Inventory`; same-SKU filters are applied before pagination. The existing flat `items` response shape remains and `nextCursor` is additive. Search reads stock and creates no reservation; Cart/Quote remain authoritative for later purchase validation. See Issue 10 and `docs/planning/kohami/contracts/API_SPEC.md`.

來源：`docs/architecture/MODULES.md`。本檔為衍生索引；以原文件為準，未知 runtime 證據不推定通過。
