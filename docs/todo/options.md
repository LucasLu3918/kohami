> 2026-10-24 · 靜態來源基準 `main@7427c94`。現行程式／SQL DDL 為權威；不是此次執行的本機 SQL、瀏覽器或正式金流驗收。

# 選配與優化建議（Options）

> 全部選項**預設未啟用**；優先保留本機 mock 可展示與 API/SQL 基線，不自行啟動外部真金流或新增付費 API。每項都有觸發、效益、代價與驗收，不將「文檔已規劃」冒稱「產品已完成」。

## 現有 11 個正式選配文件整併

| Option | 建議 | 適用情境／啟動條件 | 驗收與風險 |
|---|---|---|---|
| `ADMIN_MFA_HARDENING_OPTION` | 延後（管理後台正式營運時高優先） | Owner/Merchandiser 真實上線，完成 TOTP 契約與 recovery policy | 完整 auth-stage cookie、失效 session、敏感操作重認證；避免鎖死 Owner |
| `OWNER_AUTHENTICATOR_NATIVE_PRESENCE_OPTION` | 延後 | 本機高特權操作需原生 user-presence；不可只靠前端 MFA | 硬體/OS 授權、不可重播、recovery/rollback、離線行為驗證 |
| `OWNER_BOOTSTRAP_RECOVERY_OPTION` | 保留手冊，需真實操作者 | 初始化 Owner/高權限復原 | preview→apply→verify、epoch/permit/nonce，雙人覆核與異人驗證 |
| `CLEAN_CHECKOUT_REBUILD_HANDOFF_OPTION` | P1 推薦 | Foundation AC12 乾淨重建/移交仍 partial | clean clone + locked restore + build + SQL fixture + browser，一位未參與作者重跑 |
| `ISOLATED_RESTORE_CUTOFF_OPTION` | 正式上線前升 P0 | 有實際資料需復原與金流對帳時 | isolated restore、cutoff/再套事件、庫存/餘額/退款一致性，測 RPO/RTO |
| `ISSUE12_FIXTURE_BOOTSTRAP_OPTION` | 低優先 | 現有 fixture provisioning 明顯拖慢 CI 或不穩 | 與現有安全標記/maintenance 相容；不偷偷共用正式資料 |
| `ISSUE24_MOBILE_VOICEOVER_OPTION` | P1 推薦 | 對行動版真實銷售/無障礙有要求 | iOS Safari/VoiceOver 真人操作：搜尋、SKU、加車、付款錯誤/恢復；不要用桌面結果代替 |
| `LOCAL_PERFORMANCE_DEEP_DIVE_OPTION` | 視瓶頸再做 | 本機固定設備/網速基準出現顯著退化 | LCP、CLS、INP proxy 隔離量測，區分 lab/field、版本及樣本 |
| `PUBLIC_DOMAIN_SEO_OPTION` | 待商家/商品權利資料 | 正式域名、商品索引與搜尋曝光 | 有效 canonical/sitemap/robots，若仍 demo/noindex 不要假稱 SEO 已上線 |
| `RECONCILIATION_SCHEDULING_OPTION` | 待真實可信事件/責任 | 從人工對帳改自動對帳 | Exactly-once-ish reconcile、unknown/duplicate/late result、Owner 清單；歷史 00:10 台北時間只是候選 |
| `SECURITY_OPERATIONS_PROFILE_OPTION` | 分級採用 | 正式資金/個資/部署開始前 | 警示分級、最小記錄/保留、事故演練、機密分離，校準到真環境 |

來源：歷史選配案的統整；來源快照移至非權威的測試 fixture，以上條目皆非已採功能。

## 新增改善選項（依現行缺口直接納入）

| 新選配 ID | 採用建議與具體實作 | Done |
|---|---|---|
| OPT-12：Contract Drift Gate | **P0 推薦**。在 CI 自動比較 ASP.NET `Map*` 路由與 runtime OpenAPI，每一路由需有 method/path、Header、body、2xx/4xx schema、auth/CSRF 契約；不能因文檔舊 core34 覆蓋新路由 | Routes 集合差異 0；所有非 GET 含無 CSRF、錯 Origin、缺冪等/版本的拒絕案例 |
| OPT-13：Database Metadata Export | **P0 推薦**。以 read-only principal 匯出 `sys.tables/columns` 與 migration ledger metadata，與 schema 檔 diff；禁止輸出表內容 | 30 張候選表逐一釐清存在/遷移，PK/FK/check/index/權限不明項有來源 |
| OPT-14：Mock/API Parity | **P0 推薦**。以相同 fixture 跑 zh-TW/ja/en、登入、商品、Cart、Quote、Checkout、Wallet topup、Order、Return，建立輸入/狀態/顯示文字比對 | 同業務結果等價、fixture 隔離，不混用 mock local state 與真 SQL；403/409/503 可恢復 |
| OPT-15：DB JSON Schema | P1 推薦。把 `commerce.Documents.Body` 的 Kind (Cart/Checkout/Order/Shipment/...) 定義可版本化 JSON Schema；把 catalog Draft/Public JSON 欄位與 OpenAPI DTO 做映射 | 資料升級/回滾、缺欄/未知欄/舊版本讀取有測試，不只 SQL columns |
| OPT-16：Documentation Freshness | **P0 推薦**。CI 比對 docs 基準 SHA、原始 tickets 狀態、路由與 schema hash；變更功能時要求更新 docs/todo | 新 route 或 DDL 變更未更新文件即 CI FAIL；只驗本次文件，不動原始證據 |
| OPT-17：Feature Flags for Fitting | P2 推薦。固定四角色/三視圖另版，以後端/前端開關控制；未核准素材時完全不顯示入口 | 不影響商品/購物車/付款；enable/disable/撤權後快取正確失效 |
| OPT-19：Storefront Image Weight | P1 推薦。首頁 hero 與商品圖目前為 1–2 MB 的 1024×1024 PNG（`public/demo-assets`，合計約 122 MB）。改為 WebP/AVIF 變體並保留原檔作來源證據；同時更新 `ASSET_MANIFEST.json` 的 sha256 與來源紀錄，不可只改副檔名 | 首屏 hero 傳輸量明顯下降；每個新變體有對應 sha256、來源與授權欄位；Playwright 圖片尺寸與 alt 測試通過 |

## 推薦啟用順序

先做 OPT-12、13、14、16（文件／測試品質，非產品功能變更），再完成 CORE-01/02 與正式商務決策；需要正式營運後才啟用資金排程、SEO、強 MFA。對試衣僅做合法素材的非 AI 首組實證，不先造昂貴或需要外部 API 的整套生成管線。

## OPT-18：本機日系服飾型錄模特圖片（選配）

只有合法授權素材、經核准的虛構成年固定角色參考，以及通過 AIPS + FLUX 3 本機 MPS 單張預檢時才可產圖。素材身份、商品形狀、衣料、顏色須人工審核；每圖記錄來源/模型/工作流/seed/輸出雜湊。禁止雲端付費 API 及未通過 preflight 的自動 fallback。先以三款商品試行，不把型錄生圖當成線上真人換裝。
