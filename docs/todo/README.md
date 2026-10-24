# KOHAMI 最新規劃／現況索引（讀這份即可開始）

> 2026-10-24 · **已比對 `main@0d54903e` 的程式、SQL、API、原 tickets 與規劃**。本次文件是 **static/source audit**，並非最新的本機執行驗收、真金流或 production go-live；實際資料庫未連線。本次精簡新增文件 **10 份（含 `docs/project-overview.html`）**，不直接覆寫或刪除原有百餘份歷史規劃／證據，以免失去追溯。遇到衝突以現行源碼、適用階段與已採決議為準，列入 confuse.md。

## 總入口

[**開啟單頁圖形總覽（HTML）**](../project-overview.html) — 一頁看專案現況、雙模式、架構／資料流圖、分期任務、API、資料庫、衝突及選配。

| 文件 | 閱讀目的 |
|---|---|
| [roadmap.md](roadmap.md) | 全部未完成／延後工作、P0/P1/P2、相依、驗收 |
| [confuse.md](confuse.md) | 舊規劃與最新程式／政策不一致之裁決與待確認 |
| [options.md](options.md) | 原 11 個選配＋新增 6 個工程改善選項（預設關閉） |
| [architecture.md](architecture.md) | 系統架構、mock/API 雙模式、交易與售後資料流（Mermaid 圖） |
| [api-spec.md](api-spec.md) | Runtime OpenAPI 27 項 endpoint：Headers、Req、Res、錯誤碼／schema |
| [api-extensions.md](api-extensions.md) | 已掛載的 26 項模組額外路由，明示現有契約未覆蓋部分 |
| [database-schema.md](database-schema.md) | 由 SQL DDL 靜態推導的 30 張表、欄位型別／限制／用途、migration 與查核方式 |
| [operations.md](operations.md) | 本機啟動、測試、環境限制、PR 驗收證據、故障歸類 |

## 現行事實（不可和正式上線混淆）

- **架構**：Nuxt 3/Vue SSR + ASP.NET Core/.NET 10 + SQL Server 2025（`Kohami/kohami_local`）、模組化單體；Cloudflare 只是靜態 mock public demo。
- **資料來源**：網頁 `mock` 預設，使用者可切 `backend`（僅本機代理/SQL）；新 checkout **登入會員限定**。三語 zh-TW/ja/en、商品、SKU、購物車、模擬付款、訂單/售後、會員錢包和管理頁已具程式。
- **已確認**：`kohami/01–06` 以政策/基線為主；`08–24` 有多項本機 mock 驗收，不代表正式金流與 SLA。歷史 `kohami/12` guest checkout 已被後續會員限定規則覆蓋。
- **未完成/待驗**：`kohami/07,25,26,27–38` 的剩餘/完整項目，`member-wallet/01,03–06` 未完成正式範圍、`production-launch/01`；並需驗證最近的 `#11` 雙模式修復及 `#12` 錢包儲值頁。
- **不擅自開通**：真實刷卡、第三方 provider、正式資金退款、Fitting/四固定角色三視圖、Google/Passkey/MFA、正式 SEO 都要經採用與驗收。

## 下位權威／來源

1. **現行程式**：`apps/api/Program.cs`、`src/Modules/**`、`apps/storefront/**`、`infra/kohami-sql/**`、`tools/native-operations/migrations/**`。
2. **機器可讀執行契約**：[`docs/contracts/runtime/Core.openapi.json`](../contracts/runtime/Core.openapi.json)（不是整個 API Surface）；其餘 handler 以 source 與 tests 驗證。
3. **原規劃/候選**：[`docs/planning/kohami/`](../planning/kohami/PLANNING_INDEX.md)；**候選 OpenAPI 不等於已採／部署**。
4. **原任務與時點證據**：[`docs/issues/kohami/`](../issues/kohami/)、[`member-wallet/`](../issues/member-wallet/)、[`VERIFICATION.md`](../architecture/VERIFICATION.md)。
5. **後續交付**：每次完成 roadmap 一項，更新它的來源 SHA、適用範圍、測試與衝突條目；必要時修 runtime schema/DB metadata 並維持這份索引精簡。

## 文件適用範圍

本檔與單頁 HTML 的任務狀態依 **2026-10-24 最新 main** 編排；新提交後狀態可能過期。**API 字典/欄位表從程式/DDL 自動抽取但未執行 live SQL introspection**；請參考 source links 和各文件的「待驗證」註記。所有功能/資料變更需 PR 與既有保護規則，不可因文件整理跳過 AIPS/secret scan。
