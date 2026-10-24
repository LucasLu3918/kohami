> 2026-10-24 · 靜態來源基準 `main@7427c94`。現行程式／SQL DDL 為權威；不是此次執行的本機 SQL、瀏覽器或正式金流驗收。

# KOHAMI 本機操作、測試與驗收證據

> 這是可操作的**命令索引與限制**，不是這次執行紀錄。本輪只讀 GitHub 原始碼與歷史測試報告，未在本機執行 SQL/.NET/Playwright。更新日期 2026-10-24。

## 本機模組

- macOS 開發、`.NET 10`（依 `global.json`）、Node/Playwright（依 `apps/storefront/package.json`）、Python 3；SQL Server 2025 Developer 在 OrbStack/Docker，專案 `kohami-sql`，預設 loopback 14349。
- 敏感設定在 ignored `.local/`，**不得把完整 connection string、測試卡號、登入 Cookie、密碼、proof、備份包推到 Git**。
- SQL 環境參考 `infra/environments/README.md`、`infra/kohami-sql/compose.yaml`；完整指令參考 `scripts/foundation/run-local.py` 與 `infra/environments/README.md`。

## 參考操作（命令先在專案根目錄執行，須有既有安全設定）

```sh
# 靜態 source 檢查，不需要接到真實收款服務
dotnet build Kohami.slnx -c Release
python3 -m unittest discover -s tests/architecture -v
python3 scripts/check_kohami_api_contract.py
python3 scripts/check_kohami_canonical.py

# 安全的本機 demo 啟動（需要 .local / strict TLS / fixture 設定）
python3 scripts/foundation/run-local.py

# 前端相關命令，實際名稱與 lockfile 以 package.json 為準
cd apps/storefront
npm ci
npm run typecheck
npm run build
```

以上是建議驗證入口，**是否可以在特定主機上無錯執行需實際重跑**；不要看到這份列表就宣稱成功。Fixture 需 marker/fingerprint 雙確認且只對專用 `Kohami`、`kohami_local` 操作；禁止對未知 SQL instance 做 seed/reset。

## 分層驗收與紀錄範本

| 層級 | 操作/測試 | PASS 需要 |
|---|---|---|
| 文件 | `scripts/check_kohami_*.py`、新 route/API coverage 比對 | OpenAPI parse、引用有效、route 及 schema 變更沒有漂移 |
| 專案建置 | `dotnet build`, Nuxt typecheck/build | 指定 SHA + runtime + 0 error |
| API 合約 | `tests/Core`、`tests/Identity`、`tests/Commerce` | Header/body 受限、CSRF/Origin/權限 negative cases、錯誤碼對 |
| 真 SQL 合成驗收 | dedicated SQL fixture、`tests/Commerce/*SqlAcceptance.cs` | marker verified、同資料集、原子扣抵/退款/庫存、異人不可讀、fixture 清除無殘留 |
| Browser | `apps/storefront/tests/*.spec.ts` | mock/backend 分列；登入→選商品→加車→報價→結帳→付款→訂單/售後；含手機 viewport |
| worker/crash | 狀態競爭/重啟/租約 | 事件重播與 lease fencing、安全維護模式、未知款不重發 |
| 可及性與效能 | 24 範圍桌面 lab 及未來行動 VoiceOver | 標明裝置、瀏覽器、樣本、LCP/CLS/INP 方法；lab 不代表 field |
| 正式環境 | provider sandbox、restore、RPO/RTO、核准 | 未在此輪執行，必須有獨立證據與人工 go-live |

### 每個 PR 必附

```yaml
source_sha: "<commit sha>"
scope: "mock / backend-local / sandbox / production"
fixture: "<marker-verified dedicated id，不含秘密>"
command: "<完整執行命令>"
result: "PASS | FAIL | BLOCKED | NOT_RUN"
observed_at: "<ISO-8601 timestamp>"
affected_routes: []
affected_tables: []
negative_cases: []
rollback: "<可重現步驟>"
limitations: "<例如只讀靜態掃描、無獨立 restore>"
```

## 停止條件與錯誤分類

- **RUN_ENV_BLOCKED**：本機 Chrome SIGABRT、TLS 不受信、NuGet audit 網路不可及或 Chromium 403；只列環境問題，不要推定產品已失敗。留原始 stderr、版本、重現步驟。
- **PRODUCT_BUG**：實際 API 500/401/403/409/422、重扣/錯價、資料不同步；必須保留 HTTP status + problem.code + traceId + req 型別（不含秘密）與 SQL 交易結果。
- **CONTRACT_DRIFT**：runtime 2xx schema/handler 不符而變 500；修正路由/回應/測試，避免重送可能已提交的付款。
- **SECURITY_BLOCK**：權限/CSRF/資料集/維護 gate 擋下；不得為跑測試跳過 Hook/guard，應從精確 diagnostic code 修復或使用專用 fixture。

歷史驗收快照已退出現行文件目錄；目前本機驗證須依上方命令記錄實際版本、測試輸出、證據與限制，不能援用舊報告取代新驗收。
