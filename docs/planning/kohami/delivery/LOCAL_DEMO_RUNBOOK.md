# 本機操作與驗收命令

更新：2026-10-16。這是操作與命令的唯一文件；執行順序見 [主計畫](IMPLEMENTATION_PLAN.md)，驗收判準見 [VALIDATION_PLAN](VALIDATION_PLAN.md)。本輪沿用既有 dedicated synthetic SQL fixture，完成 Commerce／API／Nuxt SSR 售後驗收；沒有啟動真實 provider 或原生 OS 驗收，命令與證據見[驗證紀錄](../../../architecture/VERIFICATION.md)。

## 1. 先決條件與作用範圍

位於專案根目錄；固定 candidate、工具／鎖檔及 dedicated 合成資料集。確認 .NET SDK 與 Node 符合 global.json／package.json，SQL 版本、TLS、角色及私有材料與目標相符。Node 範圍以 apps/storefront/package.json 為準，不另外建立版本權威。

私有設定與 key 只存被忽略的 .local；本機 fixture 可能已清除，不從歷史 localhost 證據推定存在。普通 start 不得偷偷 migration／seed，不直接改 maintenance／權限表解 gate。任一步無法辨認目標即保持 BLOCKED。

SQL schema 僅由明確 provisioning／`migrate-native-local` 命令套用版本化向前遷移；每個版本以來源 SHA-256 固定，單一遷移與台帳紀錄在同一 SQL transaction。專用 `m0_migrator` 僅供該命令使用，具 `m0`／`m0commerce`／`ops` schema 的 DDL／DML 權限，不供 API／worker 使用，也不屬於 `db_owner`。既有 schema 若無遷移台帳會 fail closed，須先做明確基線採用；已套用來源不可編輯，變更新增版本。沒有反向遷移；故障回復要靠資料庫備份／還原流程，此流程不由本機 migration acceptance 代替。

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
| Issue 06 worker crash/restart／failure replay matrix | `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" /usr/local/share/dotnet/dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll` | dedicated fixture 驗證 NotStarted 已提交工作續跑、未到期 Started lease 不被搶占、兩程序競爭 expired Started payment／receipt job、付款與 receipt job 原子提交、receipt-only replay、Unknown 不作新操作、完成工作重播不 dispatch、舊 fence 拒絕及 synthetic rows 清理。只證明 mock adapter 的 SQL／lease 邊界，不模擬外部 provider 呼叫中 kill process；不執行 reset，保留 fixture |
| Issue 06 M0-AC06 付款截止／取消競態 | `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll` | 在同一 dedicated Commerce harness 驗到期前一秒、15 分鐘等號、取消後晚到成功與取消／成功並行；固定時鐘、SQL 終態及 consume／release／補償唯一性；確認每組 synthetic rows 清理及 SKU 庫存還原。mock adapter 範圍，不代表真實 provider 呼叫中斷；不執行 reset，保留 fixture |
| Issue 06 M0-AC07 退款額度／併發／撤權追蹤 | `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll` | 同一 dedicated Commerce harness 啟動同 unit、不同 line、原 shipping 的同步競爭；驗證 1,590 原 collection 上限、Unknown 保留及查核原 operation、撤權在 Started 前釋放與 Started 後續追。只使用合成資料與 mock worker trusted-result seam；不代表完整 HTTP/browser 退款或真實 provider 結算；不執行 reset，保留 fixture |
| Native 合成檢查 | `dotnet run --project tests/Native/NativeTests.csproj -c Release --no-restore` | 合成測試驗證目前 fail-closed 行為；Identity 自動化檢查依其適用案例執行 |
| Native SQL migration acceptance | `dotnet tests/Native/bin/Release/net10.0/NativeTests.dll --sql-migrations .local/m0/fixture-private.json` | 僅接受當前 repo 自有 `.local/m0/fixture-private.json`、strict TLS、loopback 14336–14399 及有效 dedicated fixture marker；驗證版本台帳、同版重跑、合成升級與故障時 DDL／資料／台帳同交易 rollback；結束清除本案例 migration 與 probe table，保留 fixture |
| 前端還原 | `npm --prefix apps/storefront ci` | 依現有 package-lock；可能連網及寫 node_modules，不更新產品依賴版本 |
| 前端型別與建置 | `npm --prefix apps/storefront run typecheck`、`npm --prefix apps/storefront run build` | 寫產物；不等於 browser／SQL 旅程 |
| M0-AC11 Storefront SSR HTML | `node apps/storefront/tests/m0-ac11-ssr.mjs` | 需先 build Storefront；啟動本測試自有的 mock HTTP API stub（127.0.0.1:5080）及 Nuxt production server（localhost:3000），驗證三語首屏摘要／失敗狀態、mock/noindex/no-store、locale 不改 transaction ID 與 cookie forwarding；不連 SQL、不代表真實 Commerce API／actor scope，結束會停止自有程序。固定 ports 已被使用時不要執行。 |
| M0-AC11 CheckoutStatus API／SQL＋Nuxt SSR actor scope | `python3 scripts/m0/run-issue06-aftercare-acceptance.py` | 僅接受已存在且 marker／strict TLS 驗證通過的 Issue06 dedicated fixture；在同一 API／worker／Nuxt SSR 售後旅程驗證 owner browser 可讀真實 CheckoutStatus 並看到摘要、另一個 API 簽發的 browser 得到 API 404 與 SSR generic unavailable；另驗 SQL／Playwright 售後狀態。Runner 清理本輪 synthetic rows/session artifact，保留 fixture；不驗證真實登入/TOTP、外部 provider 或資金結算。 |
| 文件候選 | `python3 scripts/build_kohami_canonical.py --check`、`python3 scripts/check_kohami_canonical.py` | Python 須可用 PyYAML／jsonschema／openapi-spec-validator；只驗文件，不生成 client 或改 DB |
| runner 回歸 | `python3 -m unittest discover -s tests/m0 -v` | Python runner 設定／scope 回歸；不等於真 API 產品旅程 |
| 既有 SQL 狀態 | `./infra/sql-server/manage.sh status`、`./infra/sql-server/manage.sh check` | 僅驗該 README 所述環境，不取代 dedicated 產品 fixture、TLS 或最小權限證據 |
| 本機 API＋Nuxt 啟動 | `python3 scripts/m0/run-local.py` | 需 API Release DLL、.local/m0/runtime-private.json 及 browser key/cert；啟動自己建立的程序，不 provisioning 或繞原生授權。Ctrl+C 停自己的子程序並保留 DB |
| 獨立 worker | `dotnet run --project apps/worker/Worker.csproj -c Release --no-restore` | 執行前核對實際 csproj 路徑與 M0_CONFIG 為 worker 最小權限設定；不可盲目用 API／migrator credential；runner 可能已啟 worker，避免重複 |
| Issue 01 真旅程 | `python3 scripts/m0/run-issue01-acceptance.py` | 只允許 pinned localhost:14336/KohamiM0 dedicated fixture 與合成設定、預建 DLL、前端依賴；核對腳本前置，會寫測試 rows／啟動本次服務，不能對一般 DB 執行 |
| Issue 02 真旅程 | `python3 scripts/m0/run-issue02-acceptance.py` | 需 scripts/m0/provision-issue02-fixture.py 定義的專用 disposable fixture、原生授權／確認與既定來源；runner 會啟停自身服務並依流程移除自有 fixture／私有材料。執行前核對目標、效果與人工前置 |
| Issue 06 專用 SQL fixture 前置 | `python3 scripts/m0/provision-issue06-fixture.py` | 需 Native／Commerce Release DLL、Docker Compose、`.local/m0` 不存在且 loopback 14336 空閒；新建唯一 `kohami-issue06-*` 專案／volumes，strict TLS 與 Commerce SQL harness 通過後保留 fixture，供 Issue 06 故障／重播證據使用；不建立 Owner／原生權限，不碰既有專案 |
| Issue 06 HTTP/API／SQL 售後驗收 | `python3 scripts/m0/run-issue06-aftercare-acceptance.py` | 僅接受已存在且 marker／strict TLS 驗證通過的 Issue06 dedicated fixture；啟停 runner 自有 API／worker，只清除唯一 synthetic rows 並還原 Maintenance／SKU，保留 fixture。覆蓋 API／SQL，不代表 browser UI 或完整 worker crash matrix |
| Issue 06 browser UI 售後驗收 | `python3 scripts/m0/run-issue06-aftercare-acceptance.py` | 僅接受已存在且 marker／strict TLS 驗證通過的 Issue06 dedicated fixture；啟停本次 API／worker／Nuxt 與 Playwright，從 storefront 驗訂單／DEMO 收據提示、配送、退貨、驗收與 mock 退款。只清除本輪 synthetic rows、還原 Maintenance／SKU，保留 fixture |
| Issue 06 Identity AC08／09 安全與 SSR 驗收 | `python3 scripts/m0/run-issue06-aftercare-acceptance.py --identity-only` | 僅接受已存在且 marker／strict TLS 驗證通過的 Issue06 dedicated fixture；啟 API／Nuxt，不啟 worker 或建立 Commerce 訂單；驗錯 Origin／CSRF、logout／舊 cookie replay、SSR 返回頁面。只清本輪 synthetic Identity rows/session artifact，保留 fixture |

`scripts/check_kohami_planning_readiness.py` 與 `scripts/check_kohami_security_operations_plan.py` 標為 **DEPRECATED_SNAPSHOT_ONLY（不再作現行階段的通過閘門）**：其 native7／NOT_CREATED 斷言已過時，程式保留追溯，本次未修改其判定邏輯。不得把改期待值的診斷當正式 PASS；後續若重建現行 validator，須依批准來源核對範圍並保留反例測試。對目前文件採用與產品進度，以批准範圍／逐項證據為準。

## 3. 還沒綁定的命令

| 缺口 | 哪張票補 | 交付時必須提供 |
| --- | --- | --- |
| 選配：獨立 DB restore／cutoff 後重套及對帳 | [選配改善計畫](../options/ISOLATED_RESTORE_CUTOFF_OPTION.md) | 不列本機整體驗收；若另行採用，依專用隔離目標、備份／key 相依、dispatch 禁用、撤權／刪除／hold／帳務重建與停止方法 |
| 完整三語／手機／人工可及性／效能 | kohami/17、24、26 | 實際 browser／硬體／網路／樣本與腳本、人工覆核，不能以一個 UI mock 測試代替 |
| 會員餘額／換裝／正式模式 | 各功能票 | 契約採用與實作後綁定真命令；本文件不虛構尚未存在的入口 |
| clean setup／seed／stop／reset／交付重建 | kohami/26、member-wallet/05、production-launch/01 | 各目標效果、權限、前置／版本、資料保存、未知操作續查與安全回復方法 |

## 4. 啟停、故障與展示

目前 API health 表示程序／資料庫連通，不能單憑 200 或 activation 字串宣稱 live readiness；核同一 runtime principal 的 maintenance／權限與真正受保護操作。若另行啟動瀏覽器 trust 或 native confirmation 選配改善，兩者分別留證，不靠 ignoreHTTPSErrors 升格；原生維護操作本身仍須符合既有授權與 fail-closed 規則。

關閉只停止本次啟動的 API／Nuxt／worker，保留資料與未決 Operation；Unknown 不改 Failed、保留期限不重置。連線失敗先核目標／TLS／schema，不輸出秘密 connection string；不停止其他容器或全域 prune。已 Started 未決只查原來源，不另建退款。

展示順序與成功／拒絕／Unknown 及退款斷言統一見驗收文件 FINAL-CORE／WALLET／FITTING；使用獨立案例，不為下一幕任意改上一幕金融終態。Reset 需明確目標與效果，不覆寫原 DB、不直接改權限或 maintenance 解 gate。獨立 restore 演練屬選配，見專用改善計畫。
