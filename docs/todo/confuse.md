# 規劃與現行程式矛盾／待決對照

> 只記錄「規劃與現在不同」或「權威來源不一致」。**現行主分支程式、已採政策與適用階段**優先；但程式存在不等於法務批准或產品已驗收。基準 `0d54903e`，2026-10-24。

| ID | 舊規劃／說法 | 目前實際狀況與證據 | 暫定裁決／待做 |
|---|---|---|---|
| CF-01 | Guest 可以新建 checkout/訂單（舊 `kohami/12`、早期 B2C 決策） | 2026-10-22 `kohami/16/26` 與 Storefront／Commerce 已改成 **會員才可建立新訂單**；歷史訪客訂單仍使用舊 proof/capability 查看或售後 | **以現行會員限定為準**；舊 guest checkout PASS 屬歷史範圍。保留歷史訪客相容性；不得將匿名新訂單重新打開 |
| CF-02 | 規劃設計表以 `KohamiM0`、`KohamiTest`、`m0`、`m0commerce`、`demo_m0` 為現在 DB | `infra/kohami-sql` 和服務 SQL 查詢指向 `Kohami`、`kohami_local`、`identity_access`、`commerce`；migration 0016 移轉 namespace/data | **目前應採新名稱**；仍需唯讀 live schema 比對 catalog tables/ops，不能盲用舊 DDL |
| CF-03 | `docs/README.md`（10/17）仍稱「下一張未完成票 Issue25」 | Issue25 `claimed`，Issue26 `claimed`；10/24 已有 member wallet top-up UI，最新 `main` 不是 10/17 snapshot | 新 docs/todo 作最新切面，原 README 標註歷史；建議 docs/README 僅轉介，不複製狀態 |
| CF-04 | `Core.openapi.json` 與批准範圍稱 core34，似乎已涵蓋所有 API | 程式現有 runtime OpenAPI 27 個 Method+Path；模組掛載另有 26 個未全部收錄的操作。規劃候選 `API_CANONICAL.openapi.yaml` 範圍更大，且未全部採用 | **已掛載路由以程式碼為準**。27 是本次 runtime document 計數，**不能與歷史 core34 不加定義直接比較**；完成 route coverage contract gate |
| CF-05 | 2026-10-20 舊摘要 wallet 付款 POST 曾回 503/500、瀏覽器驗收受阻 | 同一 issue 後續已補 SQL 權限/200 schema，wallet SQL 幂等/競爭多項 PASS；10/22 有部分 Playwright 6/6，10/24 又加入 top-up 畫面 | 分時序讀證據；早期故障是歷史事件，**不能宣稱全部仍壞**，也不能推定最新新增頁已完整驗收 |
| CF-06 | 「會員餘額儲值」可解讀為真實信用卡入金 | `TopUpWallet` 使用 synthetic card 檢查、`commerce.wallet_mock_credit`，`mode:"mock"`；正式錢包決策 `member-wallet/01` 仍 needs-info | UI 必須清楚標「模擬儲值／無真實扣款」，禁用真資金通道，待獨立批准與 provider sandbox 驗收 |
| CF-07 | 文件有 admin MFA/TOTP、Google OAuth 與 Owner user-presence 設計 | 目前 `PasswordOnlyAuthSchema.sql` 與 `SecurityService` 採本機密碼 session；MFA／Passkey 屬未採用選配 | **現行密碼版優先**；未落地的 OAuth/MFA schemas 只能寫作 options，不宣稱目前強制 |
| CF-08 | 原網站規劃曾提可透過 AI 進行換裝／上傳真人照片 | ADR 0001、fitting 27–37 均採**非 AI 固定四角色、三視圖、無本人照片**；換裝尚未完成 | 保留非 AI 版本；不得把生圖用於站內即時換裝管線，合法服飾素材可另行製作但不能未授權上線 |
| CF-09 | Cloudflare 公開展示 = 真 API+SQL 商店上線 | Nuxt public demo 預設 mock；proxy `apiOrigin` 強制 `http://127.0.0.1`，且 `noindex`。公開靜態展示不是正式收款 | Cloudflare 只作 demo，真 SQL API 現階段 localhost，不得公開本機／無TLS loopback API |
| CF-10 | `kohami/23/24` resolved 代表正式 backup/INP/行動無障礙達標 | 實證主要是 local SQL／lab 桌面條件；真 field CWV/VoiceOver/正式營運 restore 未通過 | 本機 PASS 保留，正式部分標 DEFERRED/NOT_VERIFIED；不得「驗收通過」概括所有子範圍 |
| CF-11 | `member-wallet/03` 付款完成就等於事件 Outbox 已完工 | 付款原子扣抵已有 SQL 驗收，但 Outbox/confirmed consumer 明確未做 | 保留空缺；無 consumer 時 Option A 不建立假事件並記 DEFERRED，需真正履約事件再啟用 |
| CF-12 | 以部分文件中 `PARTIAL_NOT_Foundation_COMPLETE` 判斷整個網站尚不能展示 | 新一輪 P3/P4、本機雙模式、Storefront route 與最新錢包功能已存在；Foundation AC12 淨機重建仍 partial | 每個 gate 以當時 commit/環境分類；產品局部可展示不代表乾淨移交與正式恢復完成 |
| CF-13 | 全部原有 docs 必須壓成 20 個文件以外皆刪除 | 既存 `docs/` 逾百份證據／規劃／openapi 並帶日期，直接刪除會破壞可追溯性及對照 | **本輪精簡新輸出 ≤20 檔**，原始 docs/ 先保留歷史；若要刪舊文件需另案移轉引用並完整驗證（不在此文件整理 PR 執行） |

## 統一判讀規則

1. **程式是否存在**：查 routes/service/SQL migration，不能只看規劃 `Status: resolved`。
2. **是否能跑**：查目標 commit、fixture、測試方法、結果；資料庫未連線者只稱靜態盤點。
3. **是否可正式營運**：另需合規/商戶/權利、真實入金、退款、部署/復原/監控、正式 human go-live 批准。
4. 須改業務政策的衝突，一律採現行產品行為做文件快照，不擅自把舊決策變成新需求；留待管理者審議。
