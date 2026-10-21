# KOHAMI 環境設定

`local` 是本機開發唯一 profile，目標固定為 OrbStack `kohami-sql` 的 `localhost:14349/Kohami`。執行 `make env-local`，從既有私有 API／worker 連線設定建立被忽略的 `.local/environments/local.env`。檔案權限固定為 `0600`；勿提交或貼出內容。

SQL 整合驗證與需要真 SQL 的測試使用獨立 OrbStack `kohami-sql-test`，目標為 `localhost,14333/Kohami`。以 `make sql-test-start` 啟動，測試 profile 固定為 `.local/kohami-sql-test/identity/fixture-private.json`；一般開發 API／worker 不讀取此 profile。測試服務使用專屬 SQL Server 2025、Strict TLS 與獨立 volumes，禁止改指向日常 `kohami-sql`。

`dev.env.example` 刻意保留機群、埠號、資料庫和帳密空白。取得遠端環境資料前不建立有效的 `dev.env`。啟動程式選到 `dev` 時若必要欄位空白會停止，不會退回 `local`。

API 與 worker 從同一 profile 取得 SQL endpoint 和各自帳密。TLS 維持 `Encrypt=Strict` 與伺服器憑證驗證。遷移／原生作業仍使用獨立、私有的 provisioner 設定。Redis 只預留停用欄位，未啟動服務。

## Make 指令

專案根目錄的 `Makefile` 提供含中文說明的服務入口。一般本機開發範例如下：

```sh
make env-local       # 首次建立 .local/environments/local.env
make db-migrate      # 啟動 kohami-sql，建立／更新 Kohami 資料庫
make dev             # 啟動 API 與 Storefront；Ctrl+C 停止本次程序
```

需要 worker 時執行 `make dev-worker`；只啟動網頁資料庫檢視器時執行 `make db-viewer`，瀏覽 `http://127.0.0.1:8978`。執行 `make help` 可查看全部指令。Make 指令預設讀取 `local`；選用 `dev` 時仍需先填妥 `dev.env`，空白欄位會阻止啟動。Redis 尚未導入，相關設定保持停用。

## 本機網頁資料庫檢視器

檢視器採 CloudBeaver，僅發布到 `127.0.0.1:8978`，使用 `kohami-sql` 的 Docker network，連線目標為容器內 `sqlserver:1433`。從專案根目錄執行 `make db-viewer`，會先確保 SQL migration 已套用，再建立唯讀 SQL login 並啟動檢視器：

```sh
make db-viewer
```

Provisioner 只接受 `localhost,14349/Kohami` 的 marker-verified `kohami_local`，建立 `kohami_reader`，僅加入 `db_datareader`。憑證保存在 `.local/kohami-sql/identity/db-viewer.env`，不輸出到終端。首次開啟 `http://127.0.0.1:8978` 時設定 CloudBeaver 本機管理員；新增 SQL Server 連線時填 Host `sqlserver`、Port `1433`、Database `Kohami`、SQL Server 驗證及該唯讀帳號。密碼從本機私有檔 `.local/kohami-sql/identity/db-viewer.env` 讀取，只貼入本機 CloudBeaver 表單。TLS 選 Strict，將伺服器憑證名稱設為 `localhost`，並指定容器內憑證 `/opt/cloudbeaver/certs/sql-server.pem`。勿以 `sa` 或 API／worker 帳號登入檢視器。

若 OrbStack Compose network 名稱不是 `kohami-sql_default`，先核對實際 network，再調整 compose 的 external network name；本工具不會啟停既有資料庫服務，也不會刪除資料或 volumes。停止檢視器：`docker compose -f infra/db-viewer/compose.yaml down`；工作區 volume 保留 CloudBeaver 本機設定。

SQL 整合測試可共用 `kohami-sql-test` 上的 dedicated 合成 `Kohami` 資料庫；測試不可 reset 日常 `kohami-sql` 的 `Kohami`。需要 SQL 的 runner 必須使用測試 profile，依序執行並清理合成資料；不需要 SQL 的測試維持原命令。既有 Issue 專用 fixture 命令保留歷史追溯，新 runner 不得再建立額外 SQL Server containers 或 volumes。
