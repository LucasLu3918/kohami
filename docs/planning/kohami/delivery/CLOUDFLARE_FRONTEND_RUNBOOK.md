# Cloudflare 前端展示與資料模式

2026-10-23。預設本機互動 mock，頁尾「客服資源」下方切換資料模式；後端沿用既有本機 API，公開版明示尚未設定。此流程不代表正式商務服務上線。

## 資料模式

| 模式 | 資料與功能 | 適用環境 |
| --- | --- | --- |
| mock（預設） | 15 款合成商品、三語、搜尋、篩選、測試會員登入、購物車、報價、訂單及模擬付款；前端 adapter 將展示資料存在瀏覽器，不連 API／SQL、不收款 | 本機 Nuxt 與公開展示 |
| API | 透過同源 `/api/v1/*` 使用 API。已部署 API 時由服務處理；無法連線時顯示一般 API 錯誤，不回退為 mock | 本機完整服務與公開展示 |

模式使用 `kohami.data-source` 偏好 cookie，首次進站為 `mock`，選擇保留 30 天。按鈕會寫入偏好並完整重載到目前語系首頁；mock 訂單與帳本留在瀏覽器的展示資料中，切換不會寫進 SQL。後端身份 cookie 仍由 API 管理。來源偏好不是授權，也不會繞過 API 權限。後端失敗會顯示原有錯誤，絕不改用合成資料冒充後端成功。

公開建置包含商品、會員、購物車與結帳頁。預設 mock 可完成完整合成訂購流程；頁尾可隨時切換到 API。公開 Worker 目前不代理真實 API，`/api/v1/*` 會回傳 `503 SERVICE_UNAVAILABLE` problem；前端沿用一般 API 錯誤流程，不以 mock 結果取代錯誤。Cloudflare SPA fallback 支援直接開啟與重新整理購物車及動態 checkout 路由，管理頁仍不會打包。公開頁保留 `noindex` 與 `no-store`。

## 本機使用

沿用 [本機操作手冊](LOCAL_DEMO_RUNBOOK.md) 啟動既有 Storefront／API／SQL。預設 mock 可直接從商品加入購物車，使用測試會員登入後建立訂單並完成模擬付款；頁尾可切到 API，由既有 loopback API／SQL 處理請求。兩種模式可隨時切換；API 不可用時顯示正常服務錯誤，不會改以 mock 回應。

若只檢查前端，於 `apps/storefront` 執行：

```sh
npm ci
npm run test:data-source
npm run typecheck
npm run build:public-demo
npm run preview:public-demo
```

開啟 `http://127.0.0.1:8787/zh-TW/`。這不需要本機憑證或 API；在 API 模式會看到標準服務錯誤。一般 `npm run dev` 仍會透過既有 Nuxt 同源代理連本機 API。公開建置會覆寫 `.output`，請在獨立 checkout 驗證，避免與使用中的開發／預覽共用建置目錄。

## GitHub → Cloudflare Workers Builds

適配性：適合這次不依賴後端的公開展示。Workers Static Assets 直接提供靜態產物，不需要將 ASP.NET Core 或 SQL 搬進 Workers。完整後端雲端化須另行評估，不能只改一個 API URL。

1. 先依專案發布規則完成候選檢查、推送工程分支並審查合併；本文件與本機測試本身不代表已推送或已部署。
2. Cloudflare → **Workers & Pages → Create application → GitHub**，授權所需的 `LucasLu3918/kohami` 儲存庫。選擇建立 Worker；若是既有 Pages 專案，可採下方 Pages 替代流程。
3. 設定下列值，不使用 Nuxt 自動偵測的 SSR 預設部署命令。

| 設定 | 值 |
| --- | --- |
| Worker name | `kohami`（須與目前 Worker 與 Wrangler config 一致） |
| Worker entry | `worker/public-demo.js`（API 錯誤回應與靜態資產 routing） |
| Root directory | `apps/storefront` |
| Production branch | `main` |
| Build command | `npm run test:data-source && npm run typecheck && npm run build:public-demo` |
| Deploy command | `npx wrangler deploy --config wrangler.public-demo.json` |
| Build environment variable | `NODE_VERSION=26.11.1`（專案要求 Node 26+） |
| 靜態資產目錄 | config 內固定為 `.output/public` |

Cloudflare 依 `package-lock.json` 安裝依賴，Wrangler 已固定版本。不要設定本機 API 位址、SQL 連線字串、展示會員憑證或 `.local/` 私有材料。初次先使用 `workers.dev`，尚不需要自訂網域。首次連線與 GitHub App 授權由帳號擁有者完成；建置日誌成功後仍須開啟實際網址驗收。

手動部署可在完成 Cloudflare CLI 登入後執行 `npm run deploy:public-demo`；這個命令會先重新產生展示，避免誤傳上一次 SSR 的靜態片段。部署行為仍須符合專案發布授權。

Cloudflare Worker Previews 切換後不能回復舊預覽模式；production 部署仍沿用上表已成功的 `apps/storefront` 工作目錄與 `wrangler deploy` 流程。若需保留 PR 預覽，Preview 設定也須使用 `apps/storefront` 作為 Root directory，並執行相同的 Build command；Preview command 設為 `npx wrangler preview --config wrangler.public-demo.json`。`wrangler.public-demo.json` 保留空的 `previews` 區塊，讓 Wrangler 建立隔離預覽，靜態資產則使用本目錄的 `.output/public`。

根目錄也提供標準 `wrangler.jsonc`，支援從儲存庫根目錄執行預設 `npx wrangler preview` 的觸發器。根目錄 `.nvmrc` 固定 Node 26.11.1，`package-lock.json` 固定 Wrangler 4.151.3；Wrangler custom build 先安裝 storefront 鎖定依賴、執行資料來源測試與 typecheck，再產生公開靜態資產。此入口適用於實際建置紀錄仍使用 `/` 且沒有獨立 Build command 的情況；原本 `apps/storefront` 的 production 流程繼續使用上表設定。根目錄執行 `npm ci` 後，可用 `npx wrangler deploy --dry-run` 驗證完整產物而不部署；真實 Preview 仍以 Cloudflare 建置日誌與預覽網址驗收。

驗收：三語首頁與商品詳情可直接開啟／重新整理；mock 可加入購物車、登入測試會員、建立訂單並模擬付款；購物車、會員中心及 checkout 可直接進入／重新整理；頁尾可切換 API，`/api/v1/*` 回 `503 SERVICE_UNAVAILABLE` 並顯示一般 API 錯誤，沒有 mock fallback。檢查回應的 `X-Robots-Tag`、`Cache-Control`。回復時從 Workers 的 Versions & Deployments 選擇上一個已驗證版本；另在 Git 回復來源變更，避免下一次 push 又覆蓋。

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
