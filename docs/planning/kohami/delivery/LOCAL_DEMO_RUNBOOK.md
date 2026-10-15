# 本機操作與驗收命令

更新：2026-10-15。這是操作與命令的唯一文件；執行順序見 [主計畫](IMPLEMENTATION_PLAN.md)，驗收判準見 [VALIDATION_PLAN](VALIDATION_PLAN.md)。下列命令已核對檔案／設定來源，本次文件整理沒有執行服務、SQL、原生儀式或 browser 旅程。

## 1. 先決條件與作用範圍

位於專案根目錄；固定 candidate、工具／鎖檔及 dedicated 合成資料集。確認 .NET SDK 與 Node 符合 global.json／package.json，SQL 版本、TLS、角色及私有材料與目標相符。Node 範圍以 apps/storefront/package.json 為準，不另外建立版本權威。

私有設定與 key 只存被忽略的 .local；本機 fixture 可能已清除，不從歷史 localhost 證據推定存在。普通 start 不得偷偷 migration／seed，不直接改 maintenance／權限表解 gate。任一步無法辨認目標即保持 BLOCKED。

## 2. 已存在的檢查與命令（根目錄執行）

| 用途 | 實際命令／檔案 | 前置與效果 |
| --- | --- | --- |
| 工具確認 | `dotnet --version`、`node --version`、`npm --version` | 唯讀；核對專案宣告版本 |
| 架構檢查 | `python3 -m unittest discover -s tests/architecture -v` | 不讀 SQL 私有設定，驗邊界及檔案參考 |
| NuGet 還原 | `dotnet restore Kohami.slnx --locked-mode` | 依現有 lockfiles 還原，會寫 obj／cache／可能連網；缺 assets 不能跳過後假稱測試 PASS |
| 後端建置 | `dotnet build Kohami.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false` | 已還原套件；寫 bin／obj，不改 DB |
| Core 契約測試 | `dotnet test tests/Core/Core.Tests.csproj -c Release --no-restore` | 必須看到實際測試數與結果；只回建置成功不算測試通過 |
| TOTP 測試 | `dotnet test tests/Identity/Identity.Tests.csproj -c Release --no-restore` | 無 dedicated SQL 設定時不能推論 SQL 身份驗收；真 app／OS 證據另外取得 |
| 商務 harness | `dotnet run --project tests/Commerce/Commerce.Tests.csproj -c Release --no-restore` | 純規則與 SQL 能力依專用設定區分；要 SQL checks 時先驗 KOHAMI_M0_TEST_CONFIG 指向 dedicated 合成 fixture，不對展示資料重設 |
| Native 合成檢查 | `dotnet run --project tests/Native/NativeTests.csproj -c Release --no-restore` | 合成測試驗證目前 fail-closed 行為；Identity 自動化檢查不依賴 Owner TOTP 選配流程 |
| 前端還原 | `npm --prefix apps/storefront ci` | 依現有 package-lock；可能連網及寫 node_modules，不更新產品依賴版本 |
| 前端型別與建置 | `npm --prefix apps/storefront run typecheck`、`npm --prefix apps/storefront run build` | 寫產物；不等於 browser／SQL 旅程 |
| 文件候選 | `python3 scripts/build_kohami_canonical.py --check`、`python3 scripts/check_kohami_canonical.py` | Python 須可用 PyYAML／jsonschema／openapi-spec-validator；只驗文件，不生成 client 或改 DB |
| runner 回歸 | `python3 -m unittest discover -s tests/m0 -v` | Python runner 設定／scope 回歸；不等於真 API 產品旅程 |
| 既有 SQL 狀態 | `./infra/sql-server/manage.sh status`、`./infra/sql-server/manage.sh check` | 僅驗該 README 所述環境，不取代 dedicated 產品 fixture、TLS 或最小權限證據 |
| 本機 API＋Nuxt 啟動 | `python3 scripts/m0/run-local.py` | 需 API Release DLL、.local/m0/runtime-private.json 及 browser key/cert；啟動自己建立的程序，不 provisioning 或繞原生授權。Ctrl+C 停自己的子程序並保留 DB |
| 獨立 worker | `dotnet run --project apps/worker/Worker.csproj -c Release --no-restore` | 執行前核對實際 csproj 路徑與 M0_CONFIG 為 worker 最小權限設定；不可盲目用 API／migrator credential；runner 可能已啟 worker，避免重複 |
| Issue 01 真旅程 | `python3 scripts/m0/run-issue01-acceptance.py` | 只允許 pinned localhost:14336/KohamiM0 dedicated fixture 與合成設定、預建 DLL、前端依賴；核對腳本前置，會寫測試 rows／啟動本次服務，不能對一般 DB 執行 |
| Issue 02 真旅程 | `python3 scripts/m0/run-issue02-acceptance.py` | 需 scripts/m0/provision-issue02-fixture.py 定義的專用 disposable fixture、原生授權／確認與既定來源；runner 會啟停自身服務並依流程移除自有 fixture／私有材料。執行前核對目標、效果與人工前置 |
| Issue 06 專用 SQL fixture 前置 | `python3 scripts/m0/provision-issue06-fixture.py` | 需 Native／Commerce Release DLL、Docker Compose、`.local/m0` 不存在且 loopback 14336 空閒；新建唯一 `kohami-issue06-*` 專案／volumes，strict TLS 與 Commerce SQL harness 通過後保留 fixture，供 Issue 06 故障／重播證據使用；不建立 Owner／原生權限，不碰既有專案 |

`scripts/check_kohami_planning_readiness.py` 與 `scripts/check_kohami_security_operations_plan.py` 標為 **DEPRECATED_SNAPSHOT_ONLY（不再作現行階段的通過閘門）**：其 native7／NOT_CREATED 斷言已過時，程式保留追溯，本次未修改其判定邏輯。不得把改期待值的診斷當正式 PASS；後續若重建現行 validator，須依批准來源核對範圍並保留反例測試。對目前文件採用與產品進度，以批准範圍／逐項證據為準。

## 3. 還沒綁定的命令

| 缺口 | 哪張票補 | 交付時必須提供 |
| --- | --- | --- |
| 完整 provider fault matrix、多 worker fencing／重放 | kohami/04、06、25 | 同候選 literal command、兩 process／固定 clock、斷言與可重跑 fixture |
| 獨立 DB restore／cutoff 後重套及對帳 | kohami/06、25、26 | 新隔離目標、備份／key 相依、dispatch 禁用、撤權／刪除／hold／帳務重建與停止方法 |
| 完整三語／手機／人工可及性／效能 | kohami/17、24、26 | 實際 browser／硬體／網路／樣本與腳本、人工覆核，不能以一個 UI mock 測試代替 |
| 會員餘額／換裝／正式模式 | 各功能票 | 契約採用與實作後綁定真命令；本文件不虛構尚未存在的入口 |
| clean setup／seed／stop／reset／交付重建 | kohami/26、member-wallet/05、production-launch/01 | 各目標效果、權限、前置／版本、資料保存、未知操作續查與安全回復方法 |

## 4. 啟停、故障與展示

目前 API health 表示程序／資料庫連通，不能單憑 200 或 activation 字串宣稱 live readiness；核同一 runtime principal 的 maintenance／權限與真正受保護操作。瀏覽器 trust 與 native confirmation 必須各自證據，不靠 ignoreHTTPSErrors 升格。

關閉只停止本次啟動的 API／Nuxt／worker，保留資料與未決 Operation；Unknown 不改 Failed、保留期限不重置。連線失敗先核目標／TLS／schema，不輸出秘密 connection string；不停止其他容器或全域 prune。已 Started 未決只查原來源，不另建退款。

展示順序與成功／拒絕／Unknown、退款及獨立復原斷言統一見驗收文件 FINAL-CORE／WALLET／FITTING；使用獨立案例，不為下一幕任意改上一幕金融終態。Reset／restore 需明確目標與效果，不覆寫原 DB、不直接改權限或 maintenance 解 gate。執行者與另一操作者各留可重現證據後才判交付。
