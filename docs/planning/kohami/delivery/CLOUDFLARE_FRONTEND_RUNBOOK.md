# Cloudflare 前端展示與資料模式

2026-10-23。預設本機互動 mock，頁尾「客服資源」下方切換資料模式；後端沿用既有本機 API，公開版明示尚未設定。此流程不代表正式商務服務上線。

## 資料模式

| 模式 | 資料與功能 | 適用環境 |
| --- | --- | --- |
| 本機互動 mock（預設） | 15 款合成商品、三語、搜尋、篩選、測試會員／管理員登入、購物車、報價、訂單、測試卡付款、會員／管理員合成加值與營運摘要；所有 API 由前端 adapter 在瀏覽器模擬並存於本機展示儲存，不連 API／SQL、不收款；會員帳號 `demo@test.com`／`Kohami-Member-Demo-2026!`、管理員帳號 `demo-admin@test.com`／`Kohami-Admin-Demo-2026!`，測試會員 ID `demo-member`，測試卡 `4111111111111111` | 一般本機 Nuxt SSR |
| 後端資料 | 經既有同源 `/api/v1/*` 代理，由 ASP.NET Core 與 SQL 處理登入、商品、庫存、訂單及展示付款；資料庫可含目前的展示 fixture，不能將它直接視為正式商品資料 | 既有本機完整服務 |
| 公開靜態展示 | 使用相同合成商品和搜尋；公開建置排除會員與交易路由，寫入請求回 `DEMO_READ_ONLY`，不連 API／SQL | Cloudflare Workers／Pages |

模式使用 `kohami.data-source` 偏好 cookie，首次進站為 `mock`，選擇保留 30 天。按鈕會寫入偏好並完整重載到目前語系首頁；mock 訂單與帳本留在瀏覽器的展示資料中，切換不會寫進 SQL。後端身份 cookie 仍由 API 管理。來源偏好不是授權，也不會繞過 API 權限。後端失敗會顯示原有錯誤，絕不改用合成資料冒充後端成功。

公開建置固定停用後端，顯示「後端尚未設定」並停用切換按鈕。它僅包含商品、政策與客服頁，排除會員、購物車、結帳與管理路由；不發布 Nuxt server/API，mock 寫入仍回 `DEMO_READ_ONLY`。公開頁保留 `noindex` 與 `no-store`。本機切到後端時沿用既有功能與授權，不把前端展示模式當成 API 安全防線。

## 本機使用

沿用 [本機操作手冊](LOCAL_DEMO_RUNBOOK.md) 啟動既有 Storefront／API／SQL。預設 mock 可直接從商品加入購物車，使用測試會員登入後建立訂單並填入固定測試卡完成模擬付款；頁尾可切至「後端資料」，由本機 API／SQL 真正保存其合成交易資料。`NUXT_API_ORIGIN`、`NUXT_SITE_ORIGIN` 仍由既有啟動程式設定，API 代理仍只接受 loopback，沒有新增任意遠端 API 或資料庫直連。

若只檢查前端，於 `apps/storefront` 執行：

```sh
npm ci
npm run test:data-source
npm run typecheck
npm run build:public-demo
python3 -m http.server 4173 --bind 127.0.0.1 --directory .output/public
```

開啟 `http://127.0.0.1:4173/zh-TW/`。這不需要本機憑證或 API。一般 `npm run build` 仍產生可切換本機後端的 Nuxt SSR 版本；公開展示必須使用 `build:public-demo`。建置會覆寫目前 `.output`，請在獨立 checkout 驗證，避免與使用中的開發／預覽共用建置目錄。

## GitHub → Cloudflare Workers Builds

適配性：適合這次不依賴後端的公開展示。Workers Static Assets 直接提供靜態產物，不需要將 ASP.NET Core 或 SQL 搬進 Workers。完整後端雲端化須另行評估，不能只改一個 API URL。

1. 先依專案發布規則完成候選檢查、推送工程分支並審查合併；本文件與本機測試本身不代表已推送或已部署。
2. Cloudflare → **Workers & Pages → Create application → GitHub**，授權所需的 `LucasLu3918/kohami` 儲存庫。選擇建立 Worker；若是既有 Pages 專案，可採下方 Pages 替代流程。
3. 設定下列值，不使用 Nuxt 自動偵測的 SSR 預設部署命令。

| 設定 | 值 |
| --- | --- |
| Worker name | `kohami-frontend-demo`（須與 Wrangler config 一致） |
| Root directory | `apps/storefront` |
| Production branch | `main` |
| Build command | `npm run test:data-source && npm run typecheck && npm run build:public-demo` |
| Deploy command | `npx wrangler deploy --config wrangler.jsonc` |
| Build environment variable | `NODE_VERSION=26.11.1`（專案要求 Node 26+） |
| 靜態資產目錄 | config 內固定為 `.output/public` |

Cloudflare 依 `package-lock.json` 安裝依賴，Wrangler 已固定版本。不要設定本機 API 位址、SQL 連線字串、展示會員憑證或 `.local/` 私有材料。初次先使用 `workers.dev`，尚不需要自訂網域。首次連線與 GitHub App 授權由帳號擁有者完成；建置日誌成功後仍須開啟實際網址驗收。

手動部署可在完成 Cloudflare CLI 登入後執行 `npm run deploy:public-demo`；這個命令會先重新產生展示，避免誤傳上一次 SSR 的靜態片段。部署行為仍須符合專案發布授權。

Worker Previews 使用 Wrangler 自動偵測的 `wrangler.jsonc`，執行 `npx wrangler preview` 即可建立目前 Git 分支的預覽；設定中的 `previews` 區塊目前留空，沿用頂層靜態資產設定。

驗收：三語首頁及商品詳情直接開啟／重新整理成功，圖片正常；頁尾顯示「靜態前端展示」「後端尚未設定」；搜尋可操作；沒有應用 `/api/v1/*` 網路請求；`/api/v1/products`、`/admin/login/`、`/zh-TW/cart/` 回 404。檢查回應的 `X-Robots-Tag`、`Cache-Control`。回復時從 Workers 的 Versions & Deployments 選擇上一個已驗證版本；另在 Git 回復來源變更，避免下一次 push 又覆蓋。

## 替代方案與限制

- **Cloudflare Pages**：Root 同為 `apps/storefront`，Build command 同上，Output directory 為 `.output/public`。適合既有 Pages 專案；不使用 Node server 輸出。新專案官方建議優先 Workers。
- **日後完整後端**：可將 Nuxt、ASP.NET Core、SQL 放在同一受控主機／容器環境，沿用同源代理；或另設 HTTPS API 與受控代理。後者須另行設計網路、Host、Cookie、CSRF、預覽隔離與權限，不放寬目前 loopback 檢查來湊部署。
- 目前尚未提供公開後端網址，公開版不能切到資料庫模式；不能由訪客輸入任意 API URL。
- 自動化後端模式測試使用隔離的 HTTP 合成 fixture，只證明分流、代理與 SSR，不能宣稱真 SQL／登入／付款驗收通過。既有商務測試已在 Playwright 設定明確選用 `backend`；自行建立新 browser context 的測試也須選擇來源。
- 本次 `npm audit` 回報既有 Nuxt 相依鏈的 14 項問題（12 high、2 critical）；新增 Wrangler 未變更既有套件版本。本次未自動升級框架，正式上線仍需另行處理相依風險。

## 官方參考

- [Workers Git builds](https://developers.cloudflare.com/workers/ci-cd/builds/)
- [Build configuration](https://developers.cloudflare.com/workers/ci-cd/builds/configuration/)
- [Build image 與 Node 版本](https://developers.cloudflare.com/workers/ci-cd/builds/build-image/)
- [Workers Static Assets](https://developers.cloudflare.com/workers/static-assets/get-started/)
- [Pages framework guidance](https://developers.cloudflare.com/pages/framework-guides/)
