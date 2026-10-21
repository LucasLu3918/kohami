# 已退役：舊 KohamiTest SQL Server

本文件保留舊 `kohami-sql-test`／`KohamiTest` 環境的歷史與安全操作細節，新的本機開發目標改為 OrbStack `kohami-sql`／`Kohami`；環境設定入口見 [`infra/environments/README.md`](../environments/README.md)。請勿再為新功能或測試建立依賴 `KohamiTest` 的命令。既有資料 volume 保留，不因設定退役而刪除。

本環境使用現有 OrbStack／Docker 與 Microsoft SQL Server 2022 Developer（`linux/amd64`），Compose project 為 `kohami-sql-test`。只開放 `127.0.0.1:14333`，資料放在專用 `data` volume，最多 4 GiB 記憶體／2 CPU；不自動隨 Docker 啟動。

## 啟動與操作

```sh
orbctl start
./infra/sql-server/manage.sh start
./infra/sql-server/manage.sh status
./infra/sql-server/manage.sh check
./infra/sql-server/manage.sh restart-probe
./infra/sql-server/manage.sh stop
```

`start` 建立私有 `.local/sql-server.env`（0600，Git 忽略），產生不同的 SA 與測試帳號密碼；不印出秘密。`stop` 保留資料 volume。不得以全域 Docker prune 或刪除其他專案來清理本環境。

## 連線

- Server：`127.0.0.1,14333`
- Database：`KohamiTest`
- User：`kohami_test`（只具本測試 DB 的 db_owner，非 sysadmin；後續正式應用需另訂更窄權限）
- Password：本機私有 `.local/sql-server.env` 的 `KOHAMI_DB_PASSWORD`，不要貼聊天、放 Git 或命令列。
- .NET 開發連線採 `Encrypt=True;TrustServerCertificate=True`，後者只用於本機自簽憑證；正式環境須可驗證的憑證。

開發連線格式：`Server=127.0.0.1,14333;Database=KohamiTest;User Id=kohami_test;Password=<本機私有密碼>;Encrypt=True;TrustServerCertificate=True;`。只供 ASP.NET Core 後端設定，不能送入 Nuxt／Vue 瀏覽器端。

SA 只供本環境初始化；應用連線用 `kohami_test`。`check` 驗證受限登入、版本、合成交易 rollback；`restart-probe` 在專用容器停止／重啟後驗證合成 marker 保留。這些不是產品庫存、付款或災難還原驗收。

## Apple Silicon 與替代環境

OrbStack 可透過 Rosetta 執行 amd64：[官方說明](https://docs.orbstack.dev/docker/)。Microsoft 曾展示 [SQL Server 2022 的 Mac 開發方式](https://devblogs.microsoft.com/azure-sql/development-with-sql-in-containers-on-macos/)；但目前 [SQL Server 容器支援文件](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/quickstart-install-docker?view=sql-server-ver16)明訂 Intel／AMD x86-64 Linux，模擬環境不受官方支援。因此本機可記錄實際開發測試結果，不能宣稱官方支援平台或正式效能／復原通過。

後續優先以現有 x64 Linux 主機跑同一 Compose（原生平台）補驗，腳本可加 `KOHAMI_DOCKER_CONTEXT=default` 使用該主機 context；有 Windows x64 測試主機則可安裝獨立 Developer instance。只有缺少主機且接受費用時，才另評雲端測試 VM。Developer 只用於開發／測試；正式 edition、授權與成本另定。

## 本次實測

2026-10-13 實測：

| 檢查 | 結果 |
| --- | --- |
| OrbStack／Docker engine | 可用；Docker 29.4.0、Compose 5.5.0 |
| SQL Server | `16.0.4295.3`、Developer Edition (64-bit)，健康狀態 healthy |
| 測試登入 | `kohami_test` 可進 KohamiTest，非 sysadmin；PASS |
| 合成交易 rollback | 寫入後 rollback，marker 維持原值；PASS |
| 容器停止／重啟持久化 | 專用 volume 的 marker 重啟後保留；PASS |
| 主機 TCP | `127.0.0.1:14333` 可連線；PASS |
| 秘密／資源隔離 | 私有 env 0600 且 Git 忽略、loopback-only、4 GiB／2 CPU；PASS |

固定官方映像 digest：`sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090`。實測主機為 Apple Silicon ARM64；上述結果是此開發環境的實際結果，不聲稱官方支援、應用程式 .NET driver 整合、庫存並行、災難還原或 RPO／RTO 已通過。不保存原始日誌或密碼。

容器目前保持啟動，供接續開發。不用時執行 `./infra/sql-server/manage.sh stop`，保留 volume。
