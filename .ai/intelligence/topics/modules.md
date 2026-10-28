# modules

現行模組依業務責任組織：`IdentityAccess`（帳號、session、角色、scope）、`Catalog`（商品發佈、SKU、讀取）、`Commerce`（交易、wallet、aftercare、reconciliation）。Storefront 位於 `apps/storefront/app`（pages、components、utils），API 位於 `apps/api` 與 `src/Modules`，原生限制操作位於 `tools/native-operations`。Fulfillment、CustomerCare、Fitting 不在現行架構文件的模組清單內，不代表已實作。

來源：`docs/architecture.md` §4（原 `docs/architecture/MODULES.md` 已不存在，targeted refresh 改以現行架構文件為來源）。本檔為衍生索引；以原文件為準，未知 runtime 證據不推定通過。
