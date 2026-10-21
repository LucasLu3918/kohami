# OrbStack SQL 測試服務

本機 SQL 整合驗證與後續測試統一使用專用 Compose project `kohami-sql-test`，連線目標為 `localhost,14333/Kohami`。一般 Storefront／API／worker 仍使用 `kohami-sql` 的 `localhost,14349/Kohami`；測試不可連入或重設日常資料庫。

測試服務使用 SQL Server 2025 Developer、Strict TLS、loopback-only port `14333`，以及獨立的 `kohami-sql-test_data`、`kohami-sql-test_tls`、`kohami-sql-test_backup` volumes。資料目錄以外的舊 fixture volumes 不再供測試使用。Apple Silicon 上透過 OrbStack amd64 模擬執行；這是本機開發環境證據，不代表 Microsoft 官方支援模擬平台或正式效能／還原驗收。

## 啟動與檢查

在專案根目錄執行：

```sh
make sql-test-start
make sql-test-check
make sql-test-status
```

首次啟動會建立 ignored、mode `0600` 的 `.local/kohami-sql-test/identity/` 私有憑證與 credentials，啟動 SQL Server，先在同一 test `data` volume 建立合成 `KohamiM0` 歷史 baseline，再升級／改名為 dedicated `Kohami` 並移除暫存 baseline。最後套用完整版本遷移並寫入 fixture marker。這個順序符合專案既有的 0016 schema／dataset rename migration，不會複製日常 `kohami-sql` 的資料。憑證不安裝至系統信任；連線 profile 固定驗證 Strict TLS、`localhost` 憑證名稱與指定 server certificate。`check` 同時核對 Strict TLS、dedicated marker 與資料庫狀態，不重設或清除資料。

停止服務：

```sh
make sql-test-stop
```

此命令只停止 SQL Server，保留測試資料與 volumes。測試執行期間需序列化；各測試仍須使用專用合成資料並清理自己建立的 rows。不得把 `Kohami` 指向 `kohami-sql` 或展示資料。

## SQL 測試設定

SQL 測試使用自動產生的 fixture 設定，不要複製、輸出或貼上其中的 credentials：

```sh
KOHAMI_TEST_CONFIG="$PWD/.local/kohami-sql-test/identity/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll
Foundation_IDENTITY_SQL_TEST_CONFIG="$PWD/.local/kohami-sql-test/identity/fixture-private.json" dotnet test tests/Identity/Identity.Tests.csproj -c Release
```

需要 SQL 的 acceptance／integration validation 一律使用這份 profile 與 `kohami-sql-test`。不需要 SQL 的單元測試仍照原命令執行。舊 Issue 專用 fixture 的歷史 PASS 僅供追溯；新增或重跑驗收前，先將 runner 改用此測試服務，不再建立另一組 SQL Server volumes。

## 資料與清理

測試服務的 `data` volume 是唯一測試 SQL 資料目錄；`tls` 與 `backup` 為同一 Compose project 的輔助 volumes。其他舊 local-demo／Issue／wallet fixtures 與資料 volumes 已清除，不得再建立額外 SQL Server 容器或 volume。`make sql-test-stop` 不刪除測試資料。重新建立或清除測試資料必須使用明確、只指向此 dedicated project 的操作；不得使用全域 `docker prune`。一般開發資料仍留在 `kohami-sql`，不複製進測試服務。

SQL Server 2025 的容器版本採 digest pin。升級 image 前先確認既有測試 volume 可用，並保留可回復的測試資料備份；此測試服務不承擔正式資料或正式備份責任。
