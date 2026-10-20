# 本次重整驗證紀錄

## Issue 25 本機 API 負向授權驗收計畫（2026-10-20）

- 依 Issue25 route／handler 來源盤點，新增逐 endpoint 覆蓋的負向驗收計畫：管理路由驗證匿名、缺 permission／錯誤角色、錯誤資源 scope、過期／撤銷 grant 與 session、maintenance；顧客路由驗證會員跨 owner 與 guest capability 綁定／到期／重放；另將公開商品草稿隔離及非 GET／HEAD Origin／antiforgery middleware 拒絕分列。
- 每個狀態變更拒絕案例都要求核對資料、版本及相關 audit／idempotency 狀態無副作用；預期 HTTP status／error code 須先依實際 handler／契約確認，容許依隱藏資源策略使用 404，不以通用狀態碼代替驗收。
- 本次為測試規劃與文件同步，沒有連 API／SQL、啟動服務或執行案例，也沒有改動 grant、maintenance 或資料。全部新案例維持 `NOT_EXECUTED`，Issue25 原四項驗收不勾選；未授權 Owner/native authority 的案例需先列 `BLOCKED`，不得建立新信任根。
- 依據為人工檢視的 Identity／Commerce／Catalog route maps、handler/service 與 `SecurityRulesTests`。全域 Impact Graph／Retrieval Index stale 且覆蓋不完整，沒有宣稱完整 consumer 覆蓋；細節與逐路由清單見 [Issue25 負向授權驗收計畫](../issues/kohami/25-commerce-security-monitoring-recovery.md)。

## Issue 23 本機交付範圍結案（2026-10-20）

狀態：使用者核准的本機 mock 生命週期範圍已 resolved；不代表正式營運保存分類或未建置來源已驗收。

- 本次依 2026-10-20 使用者明確決定，將 Issue 23 的完成範圍限定為現有本機 mock 資料來源與已採工程政策。已完成證據沿用下列 2026-10-18 專用 SQL／worker／Storefront 驗收紀錄：Analyst 授權與最小報表、安全／報表稽核期限、登入失敗清理、account inactivity、訪客 proof 到期清除，以及 Shipment／Return 的邊界、來源關聯、最小摘要與冪等處理。本輪是範圍決議及文件同步，沒有重跑產品程式或測試。
- 不存在獨立的一般客服案件／訊息／附件與持久化一般診斷日誌來源，故不新增收集或清理，也不將不存在來源標成期限清理通過。
- 產品備份來源、目標及生命週期依先前決定延後；已採 35 日工程上限仍未實作或驗證。正式會計／稅務分類及法定期限待營業主體、法域與專業審查；mock DEMO 憑證不視為正式帳務憑證。
- Issue 23 本機範圍 resolved 後，Issue 25 的明列前置依賴已滿足；但 S4 主計畫仍要求先完成 Issue 24。Issue 25 的監測／告警與備份復原驗收仍未完成，不能由本次範圍決議推定通過。
- Impact traversal 對 Issue23 文件 seed 回報 `BOUNDED_WITH_UNKNOWNS`，seed 未映射至 Impact Graph；人工檢視 Issue23、Issue25、文件入口、執行主計畫與本驗證紀錄的直接引用及狀態。全域 API／data／events coverage partial、consumer coverage unknown，未宣稱圖完整或 `READY`。
- 驗證：`git diff --check`；文件狀態、Issue25 dependency 與主計畫順序人工交叉檢視。未執行 runtime build、SQL fixture、Storefront typecheck 或產品 acceptance，因本輪未改產品程式及驗證行為。

<a id="issue-23-analyst-aggregate-report-2026-10-18"></a>
## Issue 23 Analyst 最小聚合報表與保存稽核（2026-10-18）

狀態：已完成核准的本機 Analyst 最小聚合報表子範圍；Issue 23 整體仍為 `claimed`。

### Issue 23 帳號最近活動時間來源（2026-10-18）

狀態：活動時間來源與專用 SQL acceptance PASS；未活動通知與 30 日後處置已有本機 mock 實作；本節紀錄的是先前驗證範圍，配送／客服資料去識別後續結果見本檔「Issue 23 配送與客服資料去識別」。備份生命週期及會計／稅務分類仍待決，Issue 23 維持 `claimed`。

- 新增 migration `0010_issue23_account_activity`：`m0.Users.LastActivityAt` 可為 NULL，依可取得的最新 `Sessions.AuthenticatedAt` 回填，並建立非 NULL 篩選索引。成功登入與有效已驗證 session 的 `security_touch_session` 更新 UTC 時間；每帳號最多每五分鐘一次，避免舊時間倒退。沒有歷史 session 的帳號維持未知；不保存路徑、內容、IP 或逐次活動。API contract 未變。
- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`：PASS，0 warnings／0 errors。`dotnet build tools/native-operations/Native.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`：PASS，0 warnings／0 errors。
- `python3 scripts/m0/provision-issue06-fixture.py --port 14348`：在 `/private/tmp/kohami-account-activity` 隔離 worktree 建立 loopback SQL Server 2025 dedicated synthetic fixture；strict TLS、migration 0001–0010 與完整 Commerce SQL harness PASS。新增 assertions 驗證成功 setup-only 登入記錄時間、有效 session 更新、較舊時間不倒退、過期／撤銷 session 不更新。fixture 建立的活動測試 user/session 由 finally 清理；驗收後依精確 Compose marker `kohami-issue06-ee165efe0455` 刪除容器、network、volumes，並移除該 worktree 的 `.local/m0` 私密設定。主 checkout `.local/m0` 未使用。
- `git diff --check`：PASS。乾淨 feature worktree 缺少 `apps/api/obj/project.assets.json`，`dotnet restore apps/api/Api.csproj --disable-parallel` 長時間無輸出後中止；之後在 `main` checkout 以既有 NuGet assets 執行 `/usr/local/share/dotnet/dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`：PASS，0 warnings／0 errors。這證明本次 API source 可編譯，但不代表 clean restore。Acceptance 直接覆蓋新增 migration 的 session 更新行為；從舊資料回填的獨立遷移前資料案例未在本次 fixture 建立，回填依 migration SQL 檢視，未獨立實測。
- 全域 Impact Graph 的 API／data／events／consumer coverage 仍是 partial／unknown。帳號生命週期 worker 僅對已驗證 Email 且活動時間可知者建立私有 mock 郵件證據；通知成功後 30 日仍未活動才停用登入，保留帳號資料並保護最後一位有效 Owner。外部郵件供應商未接入。

- 後台報表以 `reports.read`＋固定 `Report:operations` scope 授權，從 dedicated synthetic `demo_m0` 讀取 Order 與 Collections；不新增資料表、migration、事件或寫入流程。日期使用 UTC `placedAt` 作為聚合日，範圍含首尾最多 31 日；結果按 UTC 日期及來源類型分組。頁面／API 回應標示合成 mock 與非正式財務用途，不提供 PII、order／collection ID、SKU 明細或匯出。
- `dotnet build tests/Commerce/Commerce.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`、`dotnet build apps/api/Api.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS，0 warnings／0 errors；`npm run typecheck`（`apps/storefront`）：PASS。
- 設定 `KOHAMI_M0_TEST_CONFIG` 指向 dedicated loopback fixture 後執行 `dotnet tests/Commerce/bin/Debug/net10.0/Commerce.Tests.dll issue23-report "$KOHAMI_M0_TEST_CONFIG"`：12 項 SQL acceptance assertions PASS。設定標記 dedicated，連線檢查限制於 loopback `KohamiM0`；fixture migrator 建立 synthetic Order／Collections，產品唯讀帳號查詢；finally 驗證本次合成 rows 已清理，沒有重設資料庫。
- `npx playwright test tests/issue23-analyst-report.spec.ts --config=playwright.config.ts --output=/private/tmp/kohami-issue23-playwright --reporter=list`：2/2 PASS；驗證 UTC／mock 限制、摘要顯示、日期查詢與 403 安全錯誤。此瀏覽器案例使用 API route mock，不代表 live HTTP 授權測試。`git diff --check`：PASS。
- 限制：此日期分桶不代表銀行／付款入帳日；`discrepancyCount` 只計入訂單的 `compensationRequired` 標記，畫面以「待補償訂單數」呈現。無正式 provider、真實財務來源或外部報表驗收。全域 Impact Graph API／data／events 覆蓋維持 partial／unknown。
- 新增 migration 0009 建立 `m0commerce.ReportAccessAudit`、日誌時間索引及固定清理程序；`M0SchemaContract` 與 migration manifest 同步。API 只可 INSERT 報表存取稽核，worker 依 UTC 日執行清理：登入失敗資料 90 日、安全稽核與報表稽核 1 年；cutoff 使用嚴格早於期限的 UTC 時點，重跑冪等。報表稽核包含 actor（可解析時）、RequestedAt、授權結果及有效日期範圍，不含結果內容或 IP／session／Email。
- `dotnet build tests/Commerce/Commerce.Tests.csproj --no-restore --configuration Release --maxcpucount:1 --nodeReuse:false -p:UseSharedCompilation=false --verbosity minimal`、`dotnet build apps/api/Api.csproj --no-restore --configuration Release --maxcpucount:1 --nodeReuse:false -p:UseSharedCompilation=false --verbosity minimal`、`dotnet build apps/worker/worker.csproj --no-restore --configuration Release --maxcpucount:1 --nodeReuse:false -p:UseSharedCompilation=false --verbosity minimal`：PASS，均 0 warnings／0 errors。`dotnet restore tests/Native/NativeTests.csproj --locked-mode --verbosity minimal`、Native Release build 與 `dotnet tests/Native/bin/Release/net10.0/NativeTests.dll`：PASS，34 項 assertions；migration manifest／readiness checksum 一致。
- `python3 scripts/m0/provision-issue06-fixture.py --port 14348`：在隔離 `/private/tmp/kohami-issue23-retention-check` checkout 建立 loopback SQL Server 2025 dedicated fixture；strict TLS 與 migrations 0001–0009 PASS，完整 Commerce SQL harness PASS。Issue 23 SQL assertions 驗證成功／拒絕授權均寫入最小 report audit、非法日期拒絕、90 日登入失敗及一年安全／報表稽核過期清理與 cutoff boundary 保留；worker 使用固定清理程序與最低權限通過真 SQL。清理若失敗會退避重試且不中斷業務 worker。所有合成資料與一次性 fixture 在驗收後清理，原 `.local/m0` 未讀寫／重設。
- 2026-10-18 帳號未活動處置新增 migration `0011_issue23_account_inactivity`、worker-only 私有 mock 郵件證據、30 日後停用登入及一年證據／lifecycle audit 清理；已知時間以外不處理，最後一位有效 Owner 不停用。外部郵件未接入。
- `/usr/local/share/dotnet/dotnet build apps/worker/Worker.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`、同參數的 `apps/api/Api.csproj` 與 `tools/native-operations/Native.csproj`：PASS，均 0 warnings／0 errors。使用主 checkout 已還原的 NuGet assets 副本，未執行 restore。
- `git diff --check`：PASS；Python 依 Native `SqlMigrations` batching 規則重算 0011 source hash，與 `M0SchemaContract.Supported` 一致。SQL migration 未套用，SQL／worker 行為的產品測試未執行，不能標為 PASS。
- 當時尚未實作配送／客服資料清理；後續結果見本檔「Issue 23 配送與客服資料去識別」。其他一般診斷日誌與備份保留仍待資料來源／機制，會計／稅務分類待正式確認。沒有清理任何訂單、collection、付款／退款或交易憑證；Issue 23 仍為 claimed。

<a id="issue-23-配送與客服資料去識別2026-10-18"></a>
## Issue 23 配送與客服資料去識別（2026-10-18）

狀態：核准的可證明結案資料去識別子範圍 PASS；Issue 23 整體維持 `claimed`。

- `AftercareRetention` 僅依最後一個可解析 UTC 歷史節點且節點狀態符合現行終態判斷期限。Delivered／Returned Shipment 超過 180 日後只保留訂單連結、狀態、歷史時間摘要及期限標記；Rejected Return 超過一年後只保留訂單／品項關聯、數量、狀態及歷史時間摘要。操作者、自由文字、SKU／單位識別碼、顯示通知及檢查細節移除，記錄 Owner 改為 `retention_redacted`。
- worker 每日既有保存工作執行期限處理；明確拒絕退貨若連到 RefundCase 或 InventoryMovement 即保留。非終態、期限邊界內、缺少可信最後事件及已檢查但無明確結案節點者保留。處理不變更 Order、Collection、付款／退款／發票、金融操作、庫存移動及稽核證據；無 API／OpenAPI 或事件變更。
- TDD SQL 驗收新增 13 項檢查：Shipment／Return 到期與期限邊界、非終態、售後／金融關聯保留、明確結案證據、最小摘要、敏感欄位移除、金融證據不變及重跑冪等。
- `dotnet build tools/native-operations/Native.csproj -c Release --disable-build-servers -m:1 -p:NuGetAudit=false -v:minimal` 及 `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --disable-build-servers -m:1 -p:NuGetAudit=false -v:minimal`：PASS，均 0 warnings／0 errors。以暫存的 HEAD checkout 與本次變更建立專用 loopback SQL Server 2025 fixture，migrations 0001–0011 與完整 Commerce SQL harness PASS，新增生命周期 assertions 全數通過。第一次主機既有 fixture 因缺少 migration 0009 的 ReportAccessAudit 表未符合前置條件；未對其執行 migration 或重設，後改用全新 dedicated fixture 完成驗收。
- 專用 fixture、synthetic rows、暫存 checkout 與其 volume/network 已清理；既有本機展示資料庫及兩個原有 SQL 容器未變更。一般診斷日誌、備份生命周期、其他客服來源、已檢查退貨的結案來源及會計／稅務分類仍待處理。

<a id="issue-23-已檢查退貨結案與去識別2026-10-18"></a>
## Issue 23 已檢查退貨結案與去識別（2026-10-18）

狀態：已檢查退貨的一年期限去識別子範圍 PASS；Issue 23 整體維持 `claimed`。

- `RefundResult` 只有在可信退款結果成功，且 RefundCase 指向同一個 Inspected Return、退款操作指向同一 RefundCase、Return 檢查資料指向同一個 `Applied` InventoryMovement 時，才在同一交易寫入最小 closure：`RefundSucceeded`、UTC `closedAt`、case ID 與 operation ID。待處理／未知退款不會寫入 closure；重複結果不改寫既有標記。
- 每日 retention worker 重新驗證 RefundCase、`Succeeded` RefundOperation、`Applied` InventoryMovement 與同一訂單／Return 關係；逾一年後移除 Return 的 Owner、原因／自由文字、SKU、unit IDs、inspection 細節，只保留訂單／品項關聯、數量、狀態、精簡歷程、結案參照與 retention stamp。來源退款與庫存證據保持原樣。未結案、來源缺漏／不匹配、待處理或剛好在一年邊界的資料保留。沒有回推舊 Return 的結案日期。
- TDD 紅燈先在 isolated SQL Server fixture 重現「已有結案標記的逾期 Inspected Return 未去識別」。最終版本 `dotnet build tools/native-operations/Native.csproj -c Release --disable-build-servers -m:1 -p:NuGetAudit=false -v:q` 與 `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --disable-build-servers -m:1 -p:NuGetAudit=false -v:q` 均 PASS，0 warnings／0 errors。`python3 scripts/m0/provision-issue06-fixture.py --port 14353`：migrations 0001–0011 與完整 Commerce SQL harness PASS；20 項 lifecycle assertions及 refund acceptance 中未知／成功／重複成功 closure assertions PASS。
- 驗收使用暫存 HEAD checkout，僅複製本次兩個 Commerce source 與兩個測試檔。專用 fixture、合成記錄、容器／volume／network 與暫存 checkout 已由腳本及暫存清理流程移除；既有本機資料庫未重設，兩個原有 SQL 容器未觸及。API／OpenAPI／migration／公開事件沒有變更。
- 限制：不包含外部承運或郵件來源、其他客服資料來源、一般診斷日誌、備份生命週期與會計／稅務分類；Issue 23 仍未結案。

<a id="issue-23-訪客-proof-到期清理2026-10-18"></a>
## Issue 23 訪客 proof 到期清理（2026-10-18）

狀態：使用者核准的訪客 request／delivery／capability 到期即清除範圍 PASS；Issue 23 整體仍為 `claimed`。

- migration `0012_issue23_guest_proof_retention` 建立到期及關聯索引與 worker-only procedure。Worker 在既有 retention transaction 以 UTC 時點執行；procedure 依序刪除到期 request 的 `PrivateDeliveries`、`GuestRequests`，再刪除到期 `GuestCapabilities`。界線為 `ExpiresAt <= @Now`，未到期資料保留；重跑冪等。沒有增加 API、欄位或事件。
- 新增 10 項 dedicated SQL assertions：request／delivery／capability 的到期前一 tick、到期當下、未到期保留、依賴刪除與重跑冪等。
- `dotnet restore tests/Commerce/Commerce.Tests.csproj --locked-mode`、Commerce Release build 與 Native Operations Release build：PASS，build 均 0 warnings／0 errors。`python3 scripts/m0/provision-issue06-fixture.py --port 14339`：全新 dedicated loopback SQL Server 2025 fixture 套用 migrations 0001–0012，完整 Commerce SQL harness PASS；包含本次 Issue 23 lifecycle SQL acceptance。Fixture 不建立 Owner，使用合成資料，既有展示資料庫未重設。
- 對已跑完完整 harness 的同一 fixture 再單獨執行 `issue23-report` 時，既有 maintenance gate 回覆 `MAINTENANCE_REQUIRED`；此重跑未通過，故驗收證據採首次全新 fixture 上完整 harness 的成功結果。`git diff --check`：PASS。
- 驗收後依專用 Compose project 清理容器、network 與 volumes，並移除該 worktree 的 `.local/m0` 憑證及設定。全域 Project Intelligence／Impact Graph 仍受原 checkout 既存 `.ai/` 差異影響，維持 partial／unknown；本次人工檢視只涵蓋 Worker、Commerce SQL procedure、migration registry/schema hash、dedicated acceptance 及 Issue 23 文件，不宣稱全域 coverage 完整。

## Issue 23 一般診斷日誌來源盤點（2026-10-18）

- 只讀檢視 `apps/api/Program.cs`、`apps/worker/Program.cs` 與所有 `ILogger` 呼叫點：API 與 worker 都在 Host 建置時呼叫 `Logging.ClearProviders()`，程式沒有再註冊 Console、檔案、SQL 或外部 logging provider；目前 `ILogger` 訊息沒有持久化接收端。
- 因不存在持久化一般診斷日誌來源，本次沒有可執行的 90 日清理對象，沒有修改 runtime、SQL schema、權限、事件或 API，也沒有執行產品測試。既有登入失敗 90 日及安全／報表稽核 1 年保存機制各自維持，不屬本次一般診斷日誌盤點範圍。
- 90 日一般診斷資料保存目標保留為未來持久化來源的工程要求；若要採用 sink，需另定義收集欄位、敏感資料最小化、儲存位置及可驗證清理機制。這次不新增 sink。Issue 23 接續項目為備份生命週期機制與會計／稅務正式分類。

## Issue 23 備份生命週期來源盤點（2026-10-18）

- `infra/m0/compose.yaml` 建立 SQL Server fixture 專屬 named `backup` volume 並掛載至 SQL Server 的 `/var/opt/mssql/backup`；初始化容器只設定該目錄 owner。SQL provisioner 建立 `backup_operator` 並授予 `db_backupoperator`，但 repo 未找到備份命令、產生器、排程、輪替或清除作業。Restore manifest helper 只驗證必要欄位；實際獨立新目標 restore 仍 BLOCKED，沒有可用的 restore acceptance 流程。
- 以唯讀 Docker 清冊確認兩個 Issue 06 fixture 對應的 backup mounts，並在兩個 SQL container 內確認該目錄沒有檔案；未讀取其他資料、未觸碰任何 volume／container。Docker API 需要本機 socket 權限；這項容器檢查已在明確唯讀範圍內執行。
- 使用者已採用備份滾動最長 35 日為工程保存上限，但當前沒有備份產物、UTC 建立時間或受控生命週期可供清除，故沒有執行 35 日刪除，也不能宣稱清理驗證 PASS。任何清理需先綁定指定產品備份來源／範圍、保留 hold、可用 restore 路徑，並以隔離合成備份驗證 35 日邊界、逾期清除、冪等重跑及最近可用復原點。未改 runtime、SQL schema／資料、權限或備份 volumes；Issue 23 維持 `claimed`。

## 本機開發模式 synthetic member bootstrap（2026-10-20）

狀態：程式與 API Release build PASS；尚未執行 bootstrap、登入或 live SQL／購物驗收。

- Native CLI 新增 `local-development-bootstrap <repo-root>`，只綁定 `.local/kohami-sql/m0`、`localhost:14349/KohamiM0/demo_m0` 與 fixture marker。要求 migration 0013 已套用、`m0.Users`／`m0.Grants` 為空、maintenance 尚未解除且 bootstrap 狀態未初始化；只建立一名合成會員，產生 NT$50,000 synthetic test credit 並寫 audit，在同一 SQL transaction 更新 gate。它不建立 Owner／native permit，不呼叫付款服務，不 reset 或清除資料。
- 隨機會員密碼存入 ignored `.local/kohami-sql/m0/local-development-credentials.json`，Unix 權限 0600；憑證檔寫入失敗則 SQL transaction rollback。API `/health` 移除固定 `requires-native-reconciliation` 字串，避免把原生流程誤當一般 API 連線的必要條件；health 仍只確認 SQL connection，`/ready` 仍只確認 schema。
- 受影響消費者：Native Operations CLI、`scripts/m0/run-local.py`、API health endpoint、本機 runbook、Implementation／Validation plan、Issue24/25/26 與 Owner／key lifecycle 選配文件。完整 Impact Graph consumer coverage 仍 partial；這段是人工檢視的直接消費者範圍。
- 尚未執行 dedicated fixture bootstrap、登入、wallet checkout、餘額不足／重送／並行 SQL acceptance 或 Storefront browser journey；不得標記本機購物完成。Issue25 原四項營運／復原驗收仍未完成。
- `dotnet build tools/native-operations/Native.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false` 與 `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false`：PASS，0 warnings／0 errors。`python3 -m py_compile scripts/m0/run-local.py`、`git diff --check`：PASS。未執行產品測試；本次沒有連線或寫入資料庫。
- Commerce `CreateCart` 現在在有效 `MemberAuthenticated` session 下以會員 UserId 擁有新購物車；匿名訪客仍使用 command actor。已編入 API Release build；未執行 API／SQL 行為案例，請在 bootstrap 後驗證會員 checkout 的 OwnerId 及 wallet payment。

<a id="issue-16-會員私人訂單與訪客歸戶2026-10-17"></a>
## Issue 16 會員私人訂單與訪客歸戶（2026-10-17）

狀態：Issue 16 核准的本機 mock 範圍 PASS，Work status 已 resolved。既有 KOHAMI 資料庫帳號密碼流程作為 member session；SQL service acceptance 由會員 session 完成 cart quote／checkout，並確認新 Order 的 `OwnerId` 是登入 UserId。新增的訂單清單要求 `MemberAuthenticated`，SQL 只取目前 `OwnerId=@user`；本人訂單讀取維持既有 Owner 契約。訪客歸戶需要目前 member session、原瀏覽器 cookie 與未過期／未消耗的同訂單 `claim` capability；Owner 更新、capability consume 與 idempotency response 同交易完成。原聯絡 Email 和訂單 snapshot 不變，相同 Email 不自動認領。

- Commerce.Tests Debug／Release build：PASS，0 warnings／0 errors；`dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS。NuGet assets 複用自本機主 checkout，非 clean restore 證據。
- `python3 scripts/m0/provision-issue06-fixture.py --port 14343`：localhost SQL Server 2025 dedicated fixture、strict TLS、migration 0001–0008 通過。完整 Commerce SQL harness PASS；Issue 16 case 驗證會員 quote／checkout 產生正確 OwnerId、本人訂單清單、本人完整 read projection、異會員 404、同 Email 訪客單不自動歸戶、匿名 401、明確歸戶及相同 idempotency replay、歸戶出現在勝出會員歷史、第二會員拒絕，以及兩會員同時競爭一筆訪客 capability 僅一人勝出。SQL 另確認只轉移 Owner、原聯絡 Email 保留、capability 以原 idempotency key 消耗。測試建立的 user／order／capability／idempotency rows 由 harness finally 清理，sku_demo_01 stock 還原。
- `npm run typecheck`（`apps/storefront`）：PASS。`npx playwright test tests/issue16-member-orders.spec.ts --config=playwright.config.ts`：4/4 PASS；涵蓋會員清單、登出清除畫面、明確歸戶 request、匿名 session 不呼叫私有列表 API、order service 503 不保留舊資料且錯誤只顯示於所屬區塊。Playwright API 使用 route mocks，未代表 live HTTP API 測試。
- 完成後移除 Compose project `kohami-issue06-8463df8439b1` 的 containers／network／volumes，以及本次 `.local/m0` 憑證、私密設定、session key ring、Playwright artifacts、暫存 Nuxt server／TLS files 與 node_modules symlink。主 checkout `.local/m0` 及展示資料未使用或變更。
- 範圍限制：路由新增於 Commerce module contract，未增加 core34 OpenAPI 執行契約。沒有新增通知資料或外部通知通道；未驗正式 provider、NuGet clean restore 或全站語言／可索引性（Issue 17）。Issue 02 完整售後 API／退款 workflow 與 M0 總體狀態維持原限制。

<a id="issue-15-storefront-手動處置與證據結案2026-10-17"></a>
## Issue 15 Storefront 手動處置與證據結案（2026-10-17）

狀態：Issue 15 核准的本機 mock 手動對帳範圍 PASS，票券已 resolved。正式 provider／銀行對帳、外部通知及每日排程不在本票目前範圍；排程仍列選配。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal` 與 `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS，0 warnings／0 errors。`npm run typecheck`（`apps/storefront`）：PASS。
- `python3 -m unittest tests.m0.test_issue06_acceptance_runner -v`：20/20 PASS；Python `py_compile` 與 `git diff --check` PASS。
- 新增 Commerce SQL 回歸斷言：`CheckRequested` 的來源 finding 清除後會出現在 `readyToClose`；通過合成專用 Commerce SQL harness。其他 SQL 斷言確認處置重播一致、finding 存在時拒絕結案、結案後離開 active findings、來源重現會重開，金融餘額不變。
- 在 `/private/tmp/kohami-issue15-storefront` 的乾淨暫存 checkout 執行 `python3 scripts/m0/provision-issue06-fixture.py --port 14339`：PASS；localhost SQL Server 2025、strict TLS、marker-verified migrations 0001–0008 與完整 Commerce SQL harness 通過。原本主 checkout 設定指向的舊 fixture 因缺少 migration ledger，migration 正確拒絕 baseline；該 fixture 未修改。
- `python3 scripts/m0/run-issue06-aftercare-acceptance.py --issue15-manual-reconciliation-only`：PASS。專用 API／SQL／worker 報表與 query-failure 503／權限恢復 PASS；Storefront Playwright 查核、指派與站內通知 1/1 PASS；finding 清除後顯示待結案案件及提交結案證據 1/1 PASS。runner finally 清除本次合成訂單、collection、案件、通知與 session。
- 驗收後依精確 Compose project `kohami-issue06-5ea1e96b5022` 移除容器、network、volumes，並刪除暫存 checkout。主 checkout `.local/m0` 與舊 fixture 未使用或修改。
- 驗收修正了 `readyToClose` 投影將選填案件欄位當成必填而回 `422 VALIDATION_ERROR` 的缺陷；現在選填 `sourceId`、`orderId`、`operationId`、compensation case 與 actor 使用 nullable projection，必要的 case ID／code／state／version 保持必填。
- 範圍限制：證據是本機合成 mock workflow，不包含真實 provider／銀行結算、排程、自動異常偵測或外部通知。Issue 16 的唯一阻擋 Issue 13 已 resolved，建議接續執行 Issue 16。

<a id="issue-15-notification-sql-20261017"></a>
## Issue 15 重大差異站內通知 SQL 驗收（2026-10-17）

狀態：重大差異通知的 SQL/API 服務驗收與 Storefront browser journey PASS；Issue 15 仍有其他未完成驗收，不能結案。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS，0 警告／0 錯誤。
- `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS，0 警告／0 錯誤。
- `npm run typecheck`（`apps/storefront`）：PASS。
- `dotnet build tools/native-operations/Native.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal` 與 `dotnet build tests/Native/NativeTests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS；Native 專用 endpoint range 與安全診斷測試 PASS。
- `python3 -m py_compile scripts/m0/run-issue06-aftercare-acceptance.py` 與 `git diff --check`：PASS。
- `python3 scripts/m0/provision-issue06-fixture.py --port 14342`：在獨立臨時 checkout 建立 localhost SQL Server 2025 dedicated fixture、strict TLS 與 migrations 0001–0008；修正 Native fixture endpoint validator 先前只接受 14336–14339 的錯誤後，完整 `Commerce.Tests.dll` harness PASS。驗證重大差異指派與站內通知的同交易冪等、目前負責人可讀、非收件人隔離、撤權後隱藏，且案件處置與金融狀態不變。
- `python3 scripts/m0/run-issue06-aftercare-acceptance.py --issue15-manual-reconciliation-only`：PASS。API／worker／Storefront 啟動於隔離 fixture，Issue 15 API／SQL 報表、SQL 權限故障注入與恢復 PASS；`issue15-reconciliation.spec.ts` Playwright 1/1 PASS，驗證重大 finding 可指派給合成目前使用者、assignment API 200、通知 API 200、工作台顯示該 finding 的站內通知。
- 驗收後按精確 Compose project `kohami-issue06-5db01f515584` 與 port 14342 執行 `down --volumes --remove-orphans`；容器、network、volumes 及臨時 `.local/m0` 憑證已移除。主 checkout `.local/m0` 未使用或更動。
- AIPS retrieval index 已依既有授權重建並保留為未提交；因 `.ai/` 與工作樹含使用者既有差異，targeted semantic/source-registry refresh 被 AIPS 以 `working_tree_dirty` 阻擋，既有 Intelligence 主題與來源登錄未覆寫。Impact Graph 未映射 Commerce reconciliation；人工限定檢視 Commerce 路由、案件指派／SQL `Documents`、`security_dispatch_allowed` 與 Storefront 消費端，維持全域 consumer coverage 未知，不宣稱全域完整。
- 限制：驗收只涵蓋本機合成資料與站內通知 UI；外部通知渠道與排程仍未啟用，政策保持人工模式，報表 GET 唯讀，未指派 finding 不會自動通知。沒有新增 schema／migration、外部 provider 或金融狀態寫入。

## Issue 15 Storefront 手動處置工作台（2026-10-17）

狀態：Storefront 工作台與待結案案件唯讀投影已實作；本次型別檢查及 Commerce／API Release build PASS。Live API／SQL／browser acceptance 未執行；Issue 15 仍為 `claimed`。

- 工作台顯示目前報表摘要與差異案件，可經既有 resolution API 排查、連結補償或標記非適用；請求沿用 CSRF、`If-Match` 與 `Idempotency-Key`。成功後重新讀取報表。
- Commerce 報表新增 `readyToClose`，只列已排查／已連結補償且目前 finding 已消失的案件；僅此清單可執行 `CloseReconciled`。回應限必要案件欄位，不回傳完整處置歷程。每日排程依使用者決定保持停用。
- `npm run typecheck`（`apps/storefront`）：PASS。`dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS，0 warnings／errors。未執行 Storefront E2E 或 SQL fixture，因此不宣稱處置 UI／API live acceptance 通過。
- 限制：重大通知、預先責任角色指派與完整 live acceptance 尚未完成；不涉及 provider／銀行對帳、schema／migration 或金融狀態寫入。

## Issue 15 個人負責人指派（2026-10-17）

狀態：個人指派 API、Storefront 欄位及授權解析 migration 已實作；本次未執行建置、SQL fixture 或瀏覽器驗收，不代表功能驗收通過。

- 指派使用者必須為有效帳號，且具有此案件目前的 `reconciliation.resolve` 權限；只保存 opaque User ID，Email 僅作本次解析輸入。
- 指派與最近處置人分欄記錄；版本、操作者權限重驗及冪等沿用既有交易路徑。Identity 用既有帳號與授權程序解析使用者，Commerce 寫入交易再次重驗目標權限；未新增 schema、migration 或資料表權限。
- 影響遍歷涵蓋 Commerce API 路由、SQL 驗收呼叫端與 Storefront 工作台；全域 Impact Graph 未映射 Commerce API，repository-wide consumer coverage 仍未知，未宣稱 READY 或全域完整。手動核對直接呼叫端與 identity grant 規則。
- 限制：尚未驗證新 migration 在既有 fixture 的增量套用、無權限／過期／撤銷帳號拒絕、冪等重播、版本競爭及 Storefront 操作；通知與排程仍未實作。

## Issue 15 OP-45 人工差異處置 SQL 驗收（2026-10-17）

狀態：OP-45 手動處置服務／SQL 子集 PASS；Issue 15 維持 `claimed`。每日排程依使用者決定不啟用。

- `dotnet restore tests/Commerce/Commerce.Tests.csproj --locked-mode --disable-parallel -p:NuGetAudit=false`、`dotnet restore tools/native-operations/Native.csproj --locked-mode --disable-parallel -p:NuGetAudit=false`；兩者 Release build 皆 PASS，0 warnings／0 errors。未關閉 NuGet audit 的首次 restore 因 NuGet vulnerability service 不可達回 `NU1900`，改沿用本機 fixture runbook 的 `NuGetAudit=false` 後成功。
- 在隔離 checkout `/private/tmp/kohami-issue15-impl` 執行 `python3 scripts/m0/provision-issue06-fixture.py --port 14339`：PASS；全新 localhost SQL Server 2025、strict TLS、marker-verified migrations 0001–0008，完整 Commerce SQL harness PASS。新增 10 項 OP-45 SQL assertions：穩定案件 ID／版本、責任 actor、相同請求重播、差異仍存在時拒絕結案、修正後允許結案、已結案案件離開 active findings、重現時重新開啟、重新處置、歷程／版本持久化，以及冪等唯一與金融餘額不變。
- acceptance 透過 SQL-backed `CommerceService` 驗證，並以 synthetic Owner／Dataset grant 執行 SQL `security_dispatch_allowed` 當前權限重驗。合成案件、grant、user、collection、order 與冪等資料由 `finally` 清除。
- 成功後核對本次 marker `kohami-issue06-ed420a25478a`，只對 localhost:14339 執行 `M0_SQL_PORT=14339 docker compose -p kohami-issue06-ed420a25478a -f infra/m0/compose.yaml down --volumes`；容器、network、volumes 已移除，暫存 checkout 後續移除。主 checkout `.local/m0` 未使用。
- `git diff --check`：PASS。限制：尚未執行 Storefront／HTTP live journey、角色指派、通知、worker source-check job 或 provider／銀行整合；OP-45 為本機 mock 手動 workflow 子集，不能視為 Issue 15 完成。

## Issue 15 人工唯讀對帳報表驗收（2026-10-17）

狀態：核准的本機 mock 人工報表子集 PASS；Issue 15 維持 `claimed`，M2／M0 不因此視為完成。每日排程依使用者決定不啟用。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -v minimal`、`dotnet build apps/api/Api.csproj -c Release --no-restore -v minimal`：PASS，均為 0 warnings／0 errors。
- `KOHAMI_M0_TEST_CONFIG=/private/tmp/kohami-issue15-test/.local/m0/fixture-private.json dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：PASS，完整 Commerce SQL harness 結束；包含專用合成資料前置清理、Issue 15 finding／summary／唯讀 snapshot assertions 與 finally cleanup。
- `npm run typecheck`（`apps/storefront`）：PASS；`python3 -m unittest tests.m0.test_issue06_acceptance_runner -v`：PASS，15/15。
- `KOHAMI_M0_LOCAL_DIR=/private/tmp/kohami-issue15-test/.local/m0 KOHAMI_M0_FIXTURE_ROOT=/private/tmp/kohami-issue15-test python3 scripts/m0/run-issue06-aftercare-acceptance.py --issue15-manual-reconciliation-only`：PASS。使用專用 localhost SQL Server 2025 fixture、strict TLS 與 marker-verified migrations 0001–0008；API 授予合成 Owner 僅 `reconciliation.read`。Issue 14 晚到付款 SQL 前置案例 PASS；Issue 15 API 重讀保持相同報表狀態／summary／findings；Playwright 1/1 PASS。
- Commerce SQL harness 驗證晚到 collection 差異、補償待處理摘要、顧客聯絡資料不輸出，並以報表執行前後 SQL snapshot 證明 collection／DEMO receipt 資料未變。報表仍標示 `mock`／`readOnly`／`ManualOnly`。Runner 清除本輪合成資料與 session；完成 harness 後依 marker 移除專用 fixture 容器、network、volumes 與臨時 workspace，主 checkout `.local/m0` 未使用。
- 限制：不涵蓋每日排程、差異處置及關閉證據、重大通知／責任人流程、真實 provider／銀行對帳、正式財務報表，亦未完成票券所列所有成功／資料缺漏／查詢失敗案例。Issue 15 整體維持 `claimed`。

## Issue 15 來源缺漏分支 SQL 驗收補強（2026-10-17）

狀態：既有人工唯讀報表的兩種來源缺漏 finding 已有專用 SQL 驗收；Issue 15 整體仍為 `claimed`。

- 在隔離 checkout 執行 `dotnet restore tests/Commerce/Commerce.Tests.csproj --locked-mode`、`dotnet restore tools/native-operations/Native.csproj --locked-mode`，再以 Release、`--no-restore` 建置兩者：PASS，0 警告／錯誤。
- `python3 scripts/m0/provision-issue06-fixture.py --port 14339`：PASS。此命令建立全新 loopback SQL2025 fixture、套用 migrations 0001–0008，並完整執行 Commerce SQL harness；新增斷言證明 `REFUND_SOURCE_MISSING`、`COMPENSATION_COLLECTION_MISSING` 均會出現在報表，且 Collections、Orders、RefundOperations、DEMO receipts 快照讀前讀後相同。SQL harness 以 `SQL tests complete` 結束。
- `python3 -m unittest tests.m0.test_issue06_acceptance_runner -v`：15/15 PASS；`git diff --check`：PASS。
- 清理：只依隔離 checkout 內精確 `kohami-issue06-*` marker 對 port 14339 執行 Compose `down --volumes --remove-orphans`；容器、network、volumes 已移除。主 checkout `.local/m0` 未使用。
- 限制：本次沒有重新執行 Storefront/API live 旅程（API／UI 未變更）；尚未驗證 SQL 查詢失敗呈現、完整成功／來源缺漏矩陣、差異處置／關閉、重大通知／責任人、排程或真實 provider／銀行。每日排程依使用者決定不啟用，不能宣稱 Issue 15 整體完成。

## Issue 15 SQL 查詢失敗回應驗收補強（2026-10-17）

狀態：Issue 15 人工唯讀報表的 SQL 權限故障／API 錯誤回應與恢復子集 PASS；Issue 15 整體仍為 `claimed`。

- 在隔離 worktree `/Users/lucas/.codex/worktrees/issue15-query-failure/kohami` 建立 localhost:14339 SQL Server 2025 dedicated fixture；strict TLS、marker 驗證、migrations 0001–0008 及完整 Commerce SQL harness PASS。未讀寫主 checkout `.local/m0`。
- `python3 -m unittest tests.m0.test_issue06_acceptance_runner -v`：PASS，17/17；Python `py_compile` 與 `git diff --check`：PASS。Commerce、Native、API Release builds：各 0 warnings／0 errors。
- `python3 scripts/m0/run-issue06-aftercare-acceptance.py --issue15-query-failure-only`：PASS。報表正常讀取後暫時 DENY `m0_api` 對 `m0commerce.Collections` 的 SELECT；HTTP API 回 503 `SERVICE_UNAVAILABLE`，payload 僅含標準 Problem 欄位，不回 SQL 權限細節或報表內容。驗收撤銷僅本次注入的 DENY、保留／恢復既有直接 SELECT grant，再次讀取回傳 `mock`／`readOnly`／`ManualOnly` 報表。Synthetic Owner／session／Commerce rows 已清除。
- 首次 `--issue15-manual-reconciliation-only` 完整重跑中，SQL/API 查詢故障與恢復 PASS；Storefront Playwright 未通過，因前置訂單面板未出現。這是當次結果；根因與已通過的修復後完整重跑見下方[最新驗證](#issue-15-storefront-完整驗收修復2026-10-17)。
- 限制：此為專用 mock SQL/API 故障證據，不涵蓋正式服務、真實金融來源或資料庫整體中斷；沒有修改產品 runtime、錯誤契約、schema／migration。每日排程仍不啟用；差異處置／關閉與責任人／通知尚未完成。

## Issue 15 Storefront 完整驗收修復（2026-10-17）

狀態：本機 mock 人工唯讀對帳報表 API／SQL／worker／Storefront 完整 acceptance PASS；Issue 15 仍為 `claimed`，M2／M0 不因此視為完成。

- 根因：Issue 15 重用的 Issue 14 晚到付款訂單未簽發 guest read capability，Playwright 也未注入 `kohami.guest.read` cookie；API 正確拒絕並以 404 隱藏該訂單，因此 UI 訂單面板未渲染。修正僅呼叫既有合成能力 fixture 命令並載入其 cookie，未更改 API、授權政策或產品行為。
- 新 localhost:14339 dedicated fixture：strict TLS、marker-verified migrations 0001–0008 與完整 Commerce SQL harness PASS。使用本次 checkout 的私有 fixture，不讀寫主 checkout `.local/m0`。
- `python3 -m unittest tests.m0.test_issue06_acceptance_runner -v`：19/19 PASS；Python `py_compile`、`npm --prefix apps/storefront run typecheck`、`git diff --check` PASS。
- `python3 scripts/m0/run-issue06-aftercare-acceptance.py --issue15-manual-reconciliation-only`：PASS。API 重讀保持一致；SQL 權限故障回傳已泛化 503，移除注入後報表恢復；Storefront Playwright 1/1 PASS，顯示同瀏覽器有權讀取的訂單及唯讀報表。runner 清除 synthetic rows/session；其後只依本次 `kohami-issue06-*` marker 移除 localhost:14339 fixture 容器、network、volumes。
- 限制：只驗本機 mock，不涵蓋每日排程、差異處置／關閉證據、通知／責任人、真實金融來源或正式營運。每日排程仍依使用者決定不啟用。

## Issue 14 晚到付款補償提示（三語 Storefront）

狀態：本機 DEMO/mock Issue 14 API／SQL／worker／Storefront journey PASS；不代表正式 provider sandbox 或銀行對帳驗收。

- `npm --prefix apps/storefront run typecheck`：PASS。
- `apps/storefront/node_modules/.bin/playwright test --config apps/storefront/playwright.issue14.config.ts --reporter=line --trace=off`：PASS，6/6。由隔離的合成 HTTP fixture 提供 `Cancelled`／`Expired`＋`Succeeded`＋`compensationRequired` 狀態，核對繁中／日文／英文都明示原單不重開、模擬退款待處理；fixture 不連接資料庫或外部服務。
- Commerce Release build：PASS，0 warnings／0 errors；`python3 tests/m0/test_issue06_acceptance_runner.py`：PASS，14/14。
- `KOHAMI_M0_LOCAL_DIR=/private/tmp/kohami-issue14-fixture/.local/m0 KOHAMI_M0_FIXTURE_ROOT=/private/tmp/kohami-issue14-fixture python3 scripts/m0/run-issue06-aftercare-acceptance.py --issue14-late-payment-only`：PASS。使用全新 localhost:14337 SQL Server 2025 dedicated fixture（strict TLS，migrations 0001–0008），既有 14336 fixture 未修改。API／worker 將結帳保留為 `Expired`、付款記為 `Succeeded` 並建立補償；具 `orders.read` 的客服 Order API 回傳同一補償狀態；拒絕的補償 operation 為 `Failed`，SQL 確認 pending／refunded 均為 0、reservation 已釋放且 checkout compensationRequired 保留。live Storefront Playwright 1/1 PASS。
- Fresh fixture provisioning 執行完整 Commerce SQL harness，退出成功；包含付款截止前／等號、取消競爭、晚到成功及重播／亂序／金額不符與 refund worker acceptance。runner 清除本輪 synthetic rows 與 session artifact；暫存 fixture／volume 已於驗收後清除。
- 初次嘗試的既有 localhost:14336 fixture 缺少 migration history，seed schema 不含 `AuthenticatedAt`；migration 工具因 `SQL_MIGRATION_BASELINE_REQUIRED` 拒絕自動 baseline。該 fixture 未被 reset 或修補，改用上述全新 fixture 完成驗收。

## Issue 13 拒絕／Unknown／服務暫時不可用 API／SQL／browser 驗收（2026-10-17）

狀態：Issue 13 本機 mock acceptance PASS，`docs/issues/kohami/13-trusted-payment-order-results.md` Work status 已 resolved；Issue 14 依賴解除。正式 provider sandbox／真實收款不在此驗收範圍。

- 為避開原 checkout 已存在的 `.local/m0`，在 `/private/tmp/kohami-issue13-outcomes-run` 使用專用 localhost:14338 SQL Server 2025 fixture；strict TLS、migrations 0001–0008 與 Commerce SQL harness PASS。原 `.local/m0`／14336 fixture 未使用。
- `python3 scripts/m0/run-issue06-aftercare-acceptance.py --payment-outcomes-only`：PASS。API 建立拒絕與 Unknown PaymentAttempt；Commerce worker 持久化後，SQL 分別驗證 PaymentAttempt／Checkout state 為 Failed／Unknown、Order 保持 AwaitingPayment、Payment job Completed、拒絕案例 Scenario job 完成、各一筆 PaymentAttempt、零 Collection。
- API 子程序短暫停止時 status request 網路失敗；重新啟動後 API／SQL 仍回傳持久 Unknown。沒有改成 Failed、建立第二筆 attempt 或 collection。
- 同一 runner 啟動 Storefront 與 Playwright：live result 頁顯示 Mock declined／Unconfirmed。對 Unknown status refresh 注入 HTTP 503，畫面保留上次 server result；解除注入後重查真 API，仍為 Unknown。Playwright PASS 1/1。
- Synthetic cleanup 移除本輪 rows／私密 session artifact 並恢復 Catalog Inventory 起始快照；之後只依該臨時 Compose marker 移除 fixture 與 volumes。此 runner 驗證本機 API／mock outcome，不宣稱 provider 本身發生 unavailable 或已完成外部金流 sandbox 驗收。

## Issue 13 OwnerId／SQL 快照與售後旅程（2026-10-17）

狀態：aftercare 合成 Owner 修正、SQL 價格快照與成功結果 API／SQL／browser 旅程 PASS；拒絕、服務失敗與 Unknown 的真 API／SQL／browser 結果旅程仍待完成，Issue 13 維持 `claimed`。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`：PASS，0 warnings／0 errors。`python3 -m unittest tests.m0.test_issue06_acceptance_runner tests.m0.test_issue12_checkout_acceptance -v`：PASS，16/16。
- 新建隔離 SQL Server 2025 fixture（14338），strict TLS、migration 0001–0008 及完整 Commerce SQL harness 全數 PASS；沒有使用既有 `.local/m0` 或 14336 fixture。
- `python3 scripts/m0/run-issue06-aftercare-acceptance.py`：PASS。API／worker 成功付款，SQL 驗證 Checkout／Order snapshot 在 Catalog Inventory 現價變更後不變（價格變更在交易內可見、比對後 rollback）；Owner checkout scope、SSR actor scope、guest capability、配送、退貨、驗收與 mock refund 均通過。Playwright aftercare PASS 1/1，SQL order／shipment／return／refund persisted-state assertion PASS，synthetic rows／私密 session artifact 清理 PASS。之後依本次精確 Compose marker 清除 fixture 容器與 volumes。
- Owner 根因：訪客 Cart／Checkout／Order 使用 `kohami.command` command actor 作 OwnerId，Admin UserId 與 BrowserId 都不是該 OwnerId。Fixture 現明確建立 command actor cookie；售後能力仍以獨立 BrowserId 綁定訂單。
- 限制：本輪實測付款成功分支；拒絕、服務失敗與 Unknown 尚無真 API／SQL／browser 聯合證據。既有拒絕／Unknown 結果頁 Playwright 為 mocked API 測試，不足以解除 Issue 14 阻擋。

## Issue 13 後續驗證（2026-10-17）

狀態：結果頁 mock browser 子集 PASS；Core runtime session contract 修正 PASS；隔離 SQL harness PASS；真 API／SQL／SSR 部分驗證 PASS；Issue 13 整體仍 `claimed`。

- `npm run typecheck`（`apps/storefront`）：PASS。
- Storefront HTTPS 本機服務啟動後，`./node_modules/.bin/playwright test --config ./playwright.config.ts --grep 'checkout result refreshes the server-confirmed payment state' --reporter=line --trace=off` 與 `./node_modules/.bin/playwright test --config ./playwright.config.ts --grep 'checkout result distinguishes declined and unknown outcomes' --reporter=line --trace=off`：各 PASS 1/1。案例使用 API route mocks；驗證成功結果／固定金額顯示，以及拒絕、Unknown、refresh 503 時保留最後伺服器狀態。Hydration wait 避免事件綁定前互動；status GET 數量允許 Nuxt 路由載入造成的額外讀取。
- `/usr/local/share/dotnet/dotnet build Kohami.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`：PASS，0 warnings／0 errors。`/usr/local/share/dotnet/dotnet test tests/Core/Core.Tests.csproj -c Release --no-build -v:minimal`：PASS，11/11。測試確認合法 Anonymous／AdminAuthenticated session response 可通過 runtime contract，匿名權限不得非空，未註冊之 Phase 2 路由回 404。
- `scripts/m0/project_contract.py` 以本機已快取 PyYAML 執行：PASS，依目前批准清單輸出 26 個操作；不再以固定 34 作為 assertion／輸出值。未修改批准清單。
- 在 `/private/tmp/kohami-issue13-clean-sql` 建立的隔離 SQL 2025 dedicated fixture（14337）：migration 0001–0008 與 Commerce SQL harness 全數 PASS，包括 `RefundAcceptance` worker dispatch；沒有碰觸既有 14336 fixture。之後只依精確 Compose project marker 移除本次新建容器與 volumes。
- `python3 scripts/m0/run-issue06-aftercare-acceptance.py` 在隔離 fixture 上通過 strict TLS、API Origin／antiforgery guard、checkout owner scope、payment worker（payment／checkout Succeeded、jobs Completed）及 Nuxt SSR owner／other actor 顯示限制。接續售後 fixture seed 時因 `SYNTHETIC_ORDER_NOT_FOUND` 中止；runner 確認清除本輪 synthetic SQL rows／私密 session artifact。不可將整個 runner 或售後 journey 標為 PASS。
- 先前 14336 fixture 的 `RefundAcceptance` 失敗不能代表目前程式：只讀檢查顯示該 fixture 缺少現行 migration ledger 且 stored procedure 為舊版。沒有對該 fixture 執行 migration／reset。
- 限制：Playwright 結果狀態案例是 mock API consumer 測試；尚未以 SQL 實際變更 Catalog 現價並驗證既有 Checkout／Order snapshot 不變，亦尚未完成一個涵蓋成功、拒絕、服務失敗、Unknown 的真 API／SQL／browser 全旅程。Issue 13 維持 `claimed`，Issue 14 仍受阻。

## Issue 13 結果頁伺服器狀態更新（2026-10-17）

狀態：Storefront 結果頁 refresh 子集 PASS；Issue 13 整體仍 `claimed`，M2／M0 仍為 `PARTIAL_NOT_M0_COMPLETE`。

- `npm run typecheck`（`apps/storefront`）：PASS。
- `npm run build`（`apps/storefront`）：PASS，Nuxt production build 完成。
- `./node_modules/.bin/playwright test --config ./playwright.config.ts --grep 'checkout result refreshes the server-confirmed payment state' --reporter=line --trace=off`：PASS，1/1。測試透過 API route mocks 驗證首頁進入既有結帳結果頁後，再向 checkout status endpoint 查詢，狀態更新為伺服器回傳的 Confirmed／Succeeded；測試將目錄目前價格設為 TWD 1,200，結果頁仍顯示訂單快照商品 TWD 1,000、優惠 TWD 100、運費 TWD 60、總額 TWD 960。這是 Storefront consumer 驗證，不是本次 API／SQL acceptance。
- Commerce SQL harness 使用本機標記的 dedicated synthetic `demo_m0` fixture。付款 deadline 前成功、等號晚到補償、取消後成功補償及競爭序列化檢查均 PASS；接續在 RefundAcceptance 的「refund operation enters the mock worker once」斷言失敗並以 exit 134 中止，整個 harness 不可標 PASS。該案例在 `finally` 呼叫合成退款 fixture 清理；沒有執行 fixture reset。
- Issue 04 mock event 驗證、Issue 06 localhost API／worker 付款成功及 Issue 01 Unknown reload 既有證據可供本票參考，但不替代目前未完成的完整結果旅程。付款後實際改動 Catalog 價格並以 SQL 驗證 Order／Checkout snapshot 尚待執行。
- 本次新增僅讀 status refresh，不更動 API／SQL schema、付款裁決、付款工作或帳務事件；查詢失敗時保留最後伺服器結果並顯示提示。

Issue 13 瀏覽器結果案例增補（2026-10-17）：為拒絕／Unknown 結果與查詢服務失敗時保留上次回應新增 Playwright regression。執行命令 `./node_modules/.bin/playwright test --config ./playwright.config.ts --grep 'checkout result distinguishes declined and unknown outcomes' --reporter=line --trace=off` 的 sandbox 執行在 Chromium 啟動時以 `SIGABRT` 中止；核准重試後 Chromium 正常啟動，但 `https://localhost:3000/` 回 `ERR_CONNECTION_REFUSED`，測試案例未執行，結果為 NOT_EXECUTED。此回合呼叫 `Native.dll fixture-status` 亦回報 `DirectoryNotFoundException`；未套用 migration、未重設 SQL fixture。RefundAcceptance 的 worker dispatch 失敗根因仍未確認。

## Issue 12 公開 API／SQL 驗收（2026-10-17）

狀態：Issue 12 本機 mock 範圍 PASS；整體 M2／M0 仍為 `PARTIAL_NOT_M0_COMPLETE`。

- `dotnet build tools/native-operations/Native.csproj -c Release --no-restore --disable-build-servers -m:1 -v:minimal`、`dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore --disable-build-servers -m:1 -v:minimal`、`dotnet build apps/api/Api.csproj -c Release --no-restore --disable-build-servers -m:1 -v:minimal`：PASS，皆為 0 warnings／0 errors。
- `python3 -m unittest tests.m0.test_fixture_provisioning tests.m0.test_issue12_checkout_acceptance -v`：PASS，8/8；包含 fixture port 選擇、strict TLS 專用 fixture 門檻及錯誤契約檢查。
- `python3 scripts/m0/provision-issue02-fixture.py --port 14337`：PASS，僅在隔離暫存 workspace 建立 SQL 2025 dedicated fixture；遷移 0001–0008 及 Commerce SQL harness 全數通過，之後建立單次合成密碼帳號。
- `python3 scripts/m0/run-issue12-checkout-acceptance.py --check-only`：PASS，驗證 dedicated fixture 設定及 Release binaries，未改寫 SQL。
- `python3 scripts/m0/run-issue12-checkout-acceptance.py`：PASS，strict TLS；公開 API／SQL 驗證無效配送區域、過期 Cart ETag、多 SKU 缺貨 rollback、同 key checkout 原回應重播、最後一件庫存競爭僅一方成功、checkout 只對建立它的訪客可讀。SQL 清理確認兩筆成功 checkout 各有一筆 held Reservation、狀態為 AwaitingPayment／Created／mock，隨後只刪除本 runner 記錄的合成資料。
- 驗收後以精確 Compose project marker 關閉容器並刪除專用 volumes、本機私密 fixture 設定及暫存 workspace。未改動原有 fixture；付款仍為 mock，未宣稱 M2-AC 或整體 M0 通過。

## Issue 12 訪客結帳 UI 子集

以下保留當時只有 UI mock 證據的驗收快照；目前 Issue 12 最終狀態以本文上方的公開 API／SQL 驗收紀錄為準。

日期：2026-10-17。狀態：Storefront 本機 mock UI 子集 PASS；API／SQL／M2 整體 NOT_EXECUTED。

- `npm run typecheck`（`apps/storefront`）：PASS。
- `npm run build`（`apps/storefront`）：PASS，Nuxt production build 完成。
- `./node_modules/.bin/playwright test --config ./playwright.config.ts --grep Issue12 --reporter=line --trace=off`（`apps/storefront`）：PASS，1/1，約 1.8 秒。以 Playwright API route mocks 驗證購物車報價、Email／條款必填、配送改動後報價與同意重置、依目前 Quote/Cart ETag 建立待付款 mock checkout，導向結果頁；未連接 SQL/API 服務。
- 實作只改 Storefront；Commerce checkout 的伺服端重新驗價、費用／庫存檢查、Reservation 原子交易與權限程式未改。既有 Issue 01 SQL／HTTP 歷史紀錄是先前候選證據，不等同本次 Issue 12 公開結帳邊界驗收。
- 因本次無 dedicated fixture，公開交易邊界並行不超賣、跨 SKU rollback、checkout 重送／scope／版本衝突及建立失敗 cleanup 均未在本輪執行。Issue 12 維持 in-progress，M2-AC 不升格通過；完整 M0 仍為 `PARTIAL_NOT_M0_COMPLETE`。

日期：2026-10-14。狀態：STRUCTURE_COMPLETE_ISSUE01_JOURNEY_VERIFIED_M0_PARTIAL。本文件只記錄本次實際觀察；不把架構檢查當成產品驗收。原始檢查區段保留當時的阻擋狀態，後續 Issue 01 驗收見本文末段。

## 專案結構與程式

- `python3 -m unittest discover -s tests/architecture -v`：PASS，3 項；文件遷移映射、Domain 專案依賴邊界、專案參考路徑均通過。
- `dotnet build Kohami.slnx -c Release`：PASS，12 個專案、0 warnings、0 errors（本次抽離後執行）。
- Commerce 純購物車／價格規則：PASS，12 項；SQL product checks 因未指定 dedicated fixture 而 NOT_EXECUTED。
- Core API contract 測試：PASS，11 項。
- Native 合成簽章／scope／expiry／restore 防護測試：PASS，18 項；live OS confirmation NOT_EXECUTED。
- `scripts/build_kohami_canonical.py`：PASS，core 104／Phase2 36，共 140 endpoints；產品測試 NOT_EXECUTED。
- Storefront `npm run typecheck`：PASS；Nuxt production build：NOT_EXECUTED。
- `scripts/check_kohami_planning_readiness.py`：本環境缺少 `yaml`（PyYAML）套件，暫存安裝亦因套件網路不可用而未完成；未執行到快照斷言。腳本已改為掃描 `docs/issues/kohami`。不要將環境錯誤解讀成快照 PASS 或 FAIL。

## 本機服務與瀏覽器

- API `GET /health`：HTTP 200，`database-connected`、`mode=mock`、`datasetId=demo_m0`。回應中的 `activation=requires-native-reconciliation` 是目前 `apps/api/Program.cs` 寫死的值，不能證明 SQL 中即時的 maintenance／activation 狀態。
- API `GET /api/v1/auth/session`：HTTP 200，Anonymous。
- API `GET /api/v1/carts/current`：HTTP 503 `SERVICE_UNAVAILABLE`；HTTPS storefront 同路徑也回 HTTP 503。未送出 POST、加入商品、建立訂單或啟動模擬付款。
- 以 API 同一 runtime 設定／SQL principal／TLS 執行一次暫存唯讀 preflight：`BLOCKED MAINTENANCE_REQUIRED`。因此本次購物車 503 的直接阻擋原因已確認為資料庫 maintenance gate；沒有使用 DBA 權限，也沒有做任何資料變更。
- `CommerceStore.Transaction` 在每次交易前檢查 `m0.SecurityState.Maintenance`。API 把該 SQL 錯誤和所有其他 `SqlException` 都映射成相同 503，導致前端只顯示一般連線錯誤。`/health` 目前的 `activation` 字串寫死，不是 live readiness 證據。
- 瀏覽器按鈕因此保持 disabled，完整 cart／quote／checkout／payment 旅程未驗收。解除 maintenance 後，先以 API runtime principal 重跑唯讀 readiness，再驗 cart；不能以 DBA probe 代替。

維護狀態只能由已核准的原生授權／reconciliation 流程解除；不要直接更新 `m0.SecurityState` 或使用 DBA／SA 繞過 gate。

本次重整完成不自動將原 M0-AC01–11 或八個 CHK 標為全部 PASS；M0-AC12 clean checkout／獨立 handoff 已列為選配改善，未完成的產品旅程維持 BLOCKED／NOT_EXECUTED。

## Issue 01 後續隔離 SQL 驗證（首次檢查）

日期：2026-10-14。這是前述重整驗證之後的補充證據，範圍僅為本次新建的 `kohami-m0-issue01-test` 專用 SQL 2025 synthetic fixture（loopback 14336）；不代表整體 M0 或本票產品驗收完成。

- `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet run --project tests/Commerce/Commerce.Tests.csproj -c Release --no-restore`：PASS，28 項檢查。新增檢查涵蓋 SQL 冪等 key 首次使用、同請求回放原 201、不同內容回 409；未知付款到期時釋放庫存且不建立收款；逾期後可信成功只建立一次補償、不復活訂單／庫存；重複可信成功不重複收款、補償或庫存效果。測試使用專用 synthetic rows 並清理其新增資料。
- loopback API `GET /health`：PASS，回報 `database-connected`。Commerce harness 結束時將專用 fixture maintenance gate 留在關閉狀態；建立 cart 的 POST 因此回 503 `SERVICE_UNAVAILABLE`，沒有寫入 cart。未解除 gate，因此沒有進入 HTTP checkout/payment/refund，也未執行瀏覽器旅程。
- 以上 SQL 測試只提供 M0-AC05 的部分證據。HTTP 完整旅程、provider event fault matrix、退款流程與必要瀏覽器驗收仍未執行；M0-B 保持 PARTIAL，整體狀態維持 `PARTIAL_NOT_M0_COMPLETE`。

## Issue 01 購物旅程產品驗收

日期：2026-10-14。狀態：PASS（Issue 01 R03–R05／M0-AC03–AC05 範圍）；整體 M0 仍為 `PARTIAL_NOT_M0_COMPLETE`。

- `/usr/local/share/dotnet/dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -v:minimal -m:1 -p:UseSharedCompilation=false`：PASS，0 warnings／0 errors。
- `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" /usr/local/share/dotnet/dotnet run --project tests/Commerce/Commerce.Tests.csproj -c Release --no-build --no-restore`：PASS，32 項；其中 SQL 20-client last-item race 僅一筆成功，persisted stock／reservation snapshot 為 stock 0、held reservation 1；多 SKU 後續 SKU 售罄確實回滾前一筆扣庫存；結帳冪等重播、付款明確失敗後保留預留並允許重試、Unknown 到期釋放、晚到可信成功補償、額外成功補償及重複成功去重均通過。
- `python3 scripts/m0/run-issue01-acceptance.py`：PASS；Playwright 1 test／1 passed，約 5.1 分鐘。HTTP／瀏覽器涵蓋匿名購物車建立及同 key 重播、商品加入、不支援區域與客戶金額拒絕、小數／溢位數量拒絕且購物車不變、商品價變拒絕、取貨運費 0、報價到期拒絕與重試算、結帳同 key 重播、付款 Unknown 狀態重新載入後仍可查且前端不顯示重付按鈕。
- `npm run typecheck`：PASS；`./node_modules/.bin/playwright test --config ./playwright.config.ts --list`：PASS，載入 1 個測試；`python3 -m unittest discover -s tests/architecture -v`：PASS，3 項。
- runner 僅使用 localhost:14336 的 `demo_m0` 專用 synthetic fixture，結束時關閉 maintenance acceptance window、恢復商品價格並停止其自行啟動的服務。瀏覽器使用 Playwright `ignoreHTTPSErrors`，故不表示作業系統憑證信任已安裝；沒有真實金流、退款履行或配送發生。
- M0-AC03–05 因本票購物流程情境與 SQL／HTTP 證據更新為 PASS。退款 HTTP 全流程、完整 provider event fault matrix、多 worker race/replay、M0-A 原生 OS 驗收及 M0-C 復原／交接仍未通過。因此整體 M0 與 M0-B 仍維持 PARTIAL。

## Issue 02 售後 UI 驗收追蹤

日期：2026-10-15。狀態：UI mock PASS；專用 SQL harness PASS；真實 API／SQL browser journey NOT_EXECUTED。

- `npm run typecheck` 與 Nuxt production build：PASS；Commerce harness Release build：PASS，0 warnings／0 errors；SQL Server 2025 loopback fixture 上 51 項 Commerce checks PASS，包含 `orders.read` 授權正反向案例。
- 驗收 runner 的 SQL alias／TLS pin 設定回歸測試 4 項 Python tests PASS；Native Release build PASS，42 項 synthetic checks PASS。
- 真實 API／SQL browser journey 與原生 OS confirmation 未執行；本紀錄不宣稱 Issue 02 HTTP journey 或 M0-AC06～12 全數通過，整體仍為 `PARTIAL_NOT_M0_COMPLETE`。

## Issue 06 結案標準盤點（2026-10-15）

狀態：未結案。依 S0 順序，Issue 04／05 的真 API 售後及供應商契約證據仍未齊；本輪只補 Issue 06 的候選基線證據與狀態盤點，不以文件檢查代替產品驗收。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false`：PASS，0 warnings／0 errors。
- `env KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：PASS，52 項 dedicated synthetic SQL／boundary checks；涵蓋庫存競爭與回滾、checkout 冪等、付款拒絕／Unknown／逾期／晚到與重複事件，以及退款額度／worker receipt 調整邊界。只支持涵蓋到的 M0-AC，不表示 provider fault matrix 或兩個 worker 的競爭矩陣完成。
- `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false`：PASS，0 warnings／0 errors；`npm run typecheck`（`apps/storefront`）：PASS。
- Identity SQL 測試專案缺 `project.assets.json`；數次 locked restore 無套件輸出後取消。此候選的 Identity SQL 測試 NOT_EXECUTED；先前 30 項通過只列歷史證據。Native 測試專案同樣缺 assets，未重跑；模擬原生結果不替代 Secure Enclave／OS user-presence。
- 對應 M0 狀態：AC03–05 維持既有 PASS；AC07 由 NOT_EXECUTED 更新為 PARTIAL（有 SQL／mock aftercare 邊界證據，無完整 HTTP refund）；AC01–02、08–11 仍 PARTIAL；AC06、AC12 仍 NOT_EXECUTED。整體保持 `PARTIAL_NOT_M0_COMPLETE`。


## Issue 06 本機環境與共同驗收續跑（2026-10-15）

狀態：部分共同證據 PASS；Identity／Native 專案測試與 OS 驗收仍未執行；Issue 06 不結案。候選為目前本機 `main`（`04071e5`）；沒有產品程式碼異動。

- 工具：`dotnet --version` 回報 `10.0.401`；`node --version` 回報 `v26.11.1`；`npm --version` 回報 `11.20.0`。`./infra/sql-server/manage.sh status` 與 `check` 無法連上 OrbStack Docker API socket（permission denied）；現存 `.local/m0/issue06-compose-project` 標記及 localhost `14336` listener 可見。
- `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：先在一般受限 shell 中被 loopback 權限拒絕，當時未進行 SQL 寫入；以核准的本機連線範圍重跑後 PASS，輸出 51 個 PASS assertions，涵蓋本次候選的合成 Commerce SQL／boundary checks。這不等於完整 HTTP 退款、provider fault matrix 或多 worker 競爭驗收。
- `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false`：PASS，0 warnings／0 errors；`npm run typecheck`（`apps/storefront`）：PASS（npm 對現有 `http-proxy` 環境設定發出 warning）；`python3 -m unittest discover -s tests/architecture -v`：PASS，3 tests。
- Identity 與 Native 測試專案缺少 `project.assets.json`。Identity locked restore、限定本機 NuGet cache 的 Identity／Native restore 都停在「正在判斷要還原的專案」，沒有產生 assets；因此本候選 Identity SQL tests 與 Native test project 均維持 NOT_EXECUTED。Identity 先前 30 項及 Native 先前合成測試只作歷史證據。

## Issue 06 Identity／Native restore 恢復與候選測試（2026-10-15）

狀態：Identity 與 Native 候選測試 PASS；Issue 06 仍未結案。測試候選為 `04071e5` 加上 Native lock 同步（現已提交為 `cdafffc`）；本次沒有產品程式碼變更。

- NuGet 來源只有官方 `https://api.nuget.org/v3/index.json`。受限執行環境的網路代理回覆 `403 blocked-by-allowlist`，是先前 restore 沒有輸出及停在專案判斷階段的環境原因。取得核准網路存取後，Identity `dotnet restore tests/Identity/Identity.Tests.csproj --locked-mode --verbosity minimal` 約 4.3 秒完成，並生成目前候選 assets。
- Native 首次 `dotnet restore tests/Native/NativeTests.csproj --locked-mode --verbosity minimal` 明確回報 `NU1004`：目前 `NativeTests.csproj` 的相依圖包含 `IdentityAccess.Domain`，舊 lock file 未記載。以 `dotnet restore tests/Native/NativeTests.csproj --force-evaluate --verbosity minimal` 依目前專案參考同步 `tests/Native/packages.lock.json`，再以 locked restore 成功完成；lock 差異只有新增該專案參考及 Native 對它的相依項目。
- `dotnet build tests/Native/NativeTests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`：PASS，0 warnings／0 errors。`dotnet tests/Native/bin/Release/net10.0/NativeTests.dll`：PASS，47 項簽章／scope／expiry／target／namespace／cutoff 合成檢查。

## Issue 05 本機模擬物流與交易憑證契約（2026-10-15）

狀態：Issue 05 本機 mock 契約與專用 SQL 情境 PASS；不代表正式物流、稅務、供應商或整體 M0 完成。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v minimal`：PASS，0 warnings／0 errors。TDD 先新增費用政策與快照回歸案例，確認缺少 `MockShippingFeePolicy` 時建置失敗，再完成實作並重建成功。
- `KOHAMI_M0_TEST_CONFIG=.local/m0/fixture-private.json dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：PASS，專用 `demo_m0` synthetic SQL fixture 共 67 項 assertions。含宅配／取貨費用與 `fee_demo_v1` 版本、未支援區域 `FEE_UNAVAILABLE`、報價費用快照固定、出貨重複／過期事件拒絕與例外後恢復、憑證開立失敗後查詢重試、終態不可被晚到失敗降級、退款與憑證調整狀態獨立，以及測試 Cart／Quote／工作列清理。
- `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false`：PASS，0 warnings／0 errors；`npm --prefix apps/storefront run typecheck`：PASS；`python3 -m unittest discover -s tests/architecture -v`：PASS，3 tests；`python3 -m unittest discover -s tests/m0 -v`：PASS，7 tests；Identity tests：24 passed／6 skipped；Native synthetic executable：47 checks PASS。
- Core tests NOT_EXECUTED：`tests/Core/obj/project.assets.json` 缺失，未執行 restore。M0 runner 與 Identity／Native 測試在受限環境遇到 socket 權限後以核准的本機執行權限重跑；Core 未列為通過。
- 範圍只使用 mock fee schedule 與 `demo_m0` synthetic SQL；未呼叫真物流、金流、電子發票、稅務或郵件供應商，未驗正式稅費／運費政策，也未執行真 API／SQL browser aftercare。Issue 06 仍未結案，整體狀態維持 `PARTIAL_NOT_M0_COMPLETE`。

## Issue 06 本機基線續跑（2026-10-15）

候選：`588dc7917389dfd9e5c28658d11d8c6cff8a3f2a`。本次沿用既有 `kohami-issue06-*` 專用 SQL 2025 synthetic fixture（loopback `127.0.0.1:14336`）；沒有重建、刪除或改用其他資料庫。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`：PASS，0 warnings／0 errors。
- `env KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：PASS，67 個 dedicated synthetic SQL／boundary assertions；涵蓋付款、退款、庫存競爭／回滾、物流與 DEMO 憑證、scope／maintenance、事件重送及測試資料清理。
- `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`：PASS，0 warnings／0 errors。
- `npm run typecheck`（`apps/storefront`）：PASS；npm 對現有 `http-proxy` 設定顯示環境 warning。
- `dotnet tests/Native/bin/Release/net10.0/NativeTests.dll`：PASS，47 項 synthetic scope／簽章／expiry／target／namespace／cutoff checks；live OS confirmation 未執行。
- `python3 -m unittest discover -s tests/architecture -v`：PASS，3 tests。
- `python3 -m unittest discover -s tests/m0 -v`：sandbox 內因 Unix socket `PermissionError` 中止；以核准的本機執行權限重跑後 PASS，7/7 runner regression tests。

此結果支持目前 candidate 的共同建置與局部 SQL／Identity／Native 基線，不代表 12 個 M0 acceptance cases 全數通過。Issue 06／M0 尚缺完整 API／browser 售後與退款、worker crash/restart 與失敗 replay fault matrix、獨立資料庫 restore＋cutoff ledger 對帳、OS 憑證信任與 user-presence 證據；provider sandbox 與正式部署不在目前批准範圍。Issue 06 work status 保持 claimed。

## Issue 06 雙程序付款事件競爭驗收（2026-10-15）

狀態：同事件併發去重情境 PASS；完整 worker fault matrix 與 Issue 06 結案條件仍未完成。

- TDD：先將測試掛入 Commerce runner，Release 建置因缺少 `TwoWorkerPaymentRace` helper 失敗（2 個預期 CS0103）；新增 helper 後 Release 建置 PASS，0 warnings／0 errors。
- `env KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：在既有 dedicated synthetic SQL fixture PASS，69 個 `PASS` assertions。新增兩個獨立 `dotnet` worker 程序透過同一 gate 同時提交相同 mock payment event，確認一筆已套用 Inbox、一筆 Primary collection、付款／訂單成功、reservation consumed 與一份 DEMO receipt。
- race 使用唯一 synthetic IDs；完成或失敗皆清理新增 Inbox／帳務／工作／文件資料並還原 SKU 原庫存。沙箱首次 loopback 連線被 OS 拒絕，核准本機 fixture 執行後通過。
- 本案例只證明同事件競爭去重，不覆蓋 worker crash/restart、lease 過期、部分故障後重試、HTTP/browser 售後退款、獨立 restore／cutoff 對帳或 OS 信任／user-presence。Issue 06 維持 claimed。

## 目前驗收範圍補充（2026-10-15）


## Issue 06 HTTP/API／SQL 售後退款驗收（2026-10-15）


- `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS，0 warnings／0 errors。
- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS，0 warnings／0 errors。
- `python3 scripts/m0/run-issue06-aftercare-acceptance.py`：PASS。實際 localhost HTTP 流程包含 catalog、cart／quote／checkout、mock payment 與 scenario worker、shipment Delivered、guest read／return capability、return Accepted／Received／Inspected、refund API request；SQL 另核對 Confirmed Order、Delivered Shipment、Inspected Return 與 Processing／Unknown refund operation。
- 驗收發現並修正訂單摘要回應與 runtime contract 不一致：移除未定義的 shipment／receipt 明細欄位，並將內部憑證 `Issued` 映射至摘要契約 `Confirmed`。詳細收據狀態仍可由收據端點取得。
- `python3 -m py_compile scripts/m0/run-issue06-aftercare-acceptance.py`、`python3 -m unittest tests/m0/test_issue06_acceptance_runner.py -v`：PASS，3/3。完整 browser UI、worker 全部 crash／failure replay 窗口與 clean handoff 仍未完成，Issue 06 維持 claimed。

## Issue 06 worker lease recovery 驗收（2026-10-15）

狀態：mock worker 雙程序 lease recovery 案例 PASS；Issue 06 仍未結案。只使用既有 dedicated `localhost:14336/KohamiM0` synthetic fixture，沒有重建／停止容器；測試資料採唯一 ID 並由 harness 清理。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false`：PASS，0 warnings／0 errors。
- `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：PASS，75 個 PASS assertions；需允許本機 loopback SQL。一般受限 sandbox 首次連線被 OS 拒絕，核准本機執行後通過。
- 新增案例先確認有效 lease 不會被搶占，再以兩個獨立程序競爭已過期的 Started payment job；只有一個 worker 重取，fence 由 7 增至 8，成功只套用一筆 Inbox、Primary collection 與 DEMO receipt。完成工作重播不再 dispatch，舊 lease owner／fence 無法完成 job。

## Issue 06 worker crash/restart／failure replay matrix（2026-10-15）

狀態：local mock worker SQL／lease 故障矩陣 PASS；clean handoff 已整理並提交。Issue 06 仍 claimed，因 browser UI 售後旅程未執行。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS，0 warnings／0 errors。
- `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" /usr/local/share/dotnet/dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：dedicated localhost Issue06 SQL fixture 上完整 Commerce harness PASS。worker matrix 證明未到期 lease 保護、expired Started payment／receipt job 雙程序唯一重取、NotStarted commit 後續跑、付款與 receipt job 原子提交、receipt-only replay、Unknown 不建立新操作、completed replay 不 dispatch、fence 遞增及 stale fence 拒絕；worker 場景 teardown 證明無 synthetic Job／Inbox／Collection／Document 殘留。
- 首輪完整 harness 在售後 helper 因舊測試索引不存在的 `shipmentSummary.shipments[*].history` 失敗；對照 runtime contract 後將 assertion 改為目前公開的 `shipmentSummary.state`，重建與完整 harness 再跑均 PASS。這是測試期待與既有契約不一致，未改 production runtime。
- SQL fixture 未 reset，亦未停止／重建容器；測試只清理由唯一 ID 建立的資料。專案全套 Commerce harness 另保留既有 last-item reservation fixture evidence。mock adapter 不模擬外部 provider 呼叫中強制 kill process，也不證明正式 provider／restore-cutoff 行為。
- clean handoff 範圍包含候選程式／驗證文件、可重跑命令、結果／限制、AIPS Change Impact 與目標 commit。原先使用者持有的三項 `.ai/` 工作樹異動不屬本候選、保持原樣；Issue06 fixture 留存供後續復現。

## M0-AC12 clean setup／seed／handoff 重建檢查（2026-10-16）

當時證據：setup／seed 與唯讀目標核對已重現，M0-AC12 為 PARTIAL。2026-10-16 使用者決定將 clean checkout restore/build 與第二位操作者獨立重跑移列選配，不阻擋本機完成；原證據狀態不升格。

- 基準為 `e805ef0aea12c19b4fc7c2a9f868668199695ad8` 的乾淨 tracked snapshot。`python3 scripts/m0/provision-issue06-fixture.py --port 14337` 建立新 SQL Server 2025 dedicated fixture，套用 migration `0001`–`0004` 與 `CommerceSeed.sql`；wrong-instance／direct-DML denial checks 通過，完整 Commerce SQL harness 通過。
- `dotnet tools/native-operations/bin/Release/net10.0/Native.dll fixture-status "$PWD" 14337`：PASS，輸出 `FIXTURE_TLS_STRICT encrypted=1 database=PRESENT`。此唯讀檢查使用 fixture 私有設定，確認 Strict TLS 與 `KohamiM0` 存在；不核對業務列。Seed 的行為證據來自 provisioner 執行的 Commerce SQL harness。
- 此快照使用同候選既有 Native／Commerce Release build outputs；乾淨快照的 locked NuGet restore 長時間無輸出，取消後 exit 130，因此不宣稱 clean source-to-binary restore/build 通過。
- read-only probe 後，以 `.local/m0/issue06-compose-project` 確認唯一專案 `kohami-issue06-4ccc71ec7e17`，再執行 `M0_SQL_PORT=14337 docker compose -p kohami-issue06-4ccc71ec7e17 -f infra/m0/compose.yaml down --volumes`。該專案容器、network、volumes 與暫存 private material 已清理；既有 `localhost:14336` fixture 未操作。沒有執行 reset、全域 prune、Owner/native authority 或正式服務。
- 當時限制：操作者仍是本次 Codex 執行，沒有第二位人員獨立重做；因此只記 PARTIAL。後續範圍決定已將獨立 clean-checkout restore/build 與第二位操作者 handoff 列為選配，原始驗證結果仍保留。候選來源、literal commands、效果與限制見 [本機操作手冊](../planning/kohami/delivery/LOCAL_DEMO_RUNBOOK.md)、[source binding](../planning/kohami/delivery/technical-baseline/SOURCE_BINDING_TEMPLATE.yaml)及[選配計畫](../planning/kohami/options/CLEAN_CHECKOUT_REBUILD_HANDOFF_OPTION.md)。

### Clean source-to-binary restore follow-up（2026-10-16）

本輪以 `4b10b46f4ed76f89d98cdd5d70a12fc126b3c71b` 建立新的 tracked-only snapshot，未複製既有 binaries 或 `.local/` 私有資料。

- `dotnet restore tools/native-operations/Native.csproj --locked-mode`：超過 120 秒停在「正在判斷要還原的專案」，取消後 exit 130；沒有 restore 完成證據。
- 本機 NuGet cache source 的 locked restore（`--source /Users/lucas/.nuget/packages --ignore-failed-sources -p:NuGetAudit=false --verbosity diagnostic`）在 20 秒上限內仍停在 `_GenerateRestoreProjectPathWalk`／`Core.csproj` graph evaluation，未產生 `obj` assets。加上 `--disable-parallel` 的 normal verbosity 嘗試也停在 restore graph 起始階段，取消後 exit 130。
- 因 Native restore 未完成，沒有執行 Native／Commerce build、Commerce restore、fixture provision 或 `fixture-status`。本輪未連線或操作任何 SQL fixture；既有 `.local/m0` 與 localhost:14336 保持未碰觸。
- 結果：clean source-to-binary restore/build 仍 UNVERIFIED；重跑由同一 Codex 執行，沒有第二位操作者 handoff 證據。依2026-10-16範圍決定，此組驗證移列選配，M0-AC12 的 PARTIAL 證據狀態保留但不再作本機結案門檻。診斷只能確認 restore 停滯於 project graph 階段，無證據判定是網路、NuGet cache 或 MSBuild 環境造成。

## M0-AC11 Storefront SSR 結帳摘要（2026-10-16）

候選：`391b7a1f6f9bb5f776171e1e0ca980e8b07130a1`，基底 `45a52eeddfa3b0a92a13a1c2494eb05f8cbb3ea2`。新增唯讀 `/checkout/:id` SSR 頁，依 `lang` 顯示 zh-TW／ja／en 結帳摘要與付款狀態；切換語系沿用同一個 checkout ID。既有結帳狀態區增加通往同一 ID 的摘要連結。無 JavaScript 時，伺服器 HTML 已含商品列、金額、訂單／付款狀態及全流程 mock 提示；無付款／重試控制。

- `npm --prefix apps/storefront run typecheck`：PASS。
- `npm --prefix apps/storefront run build`：PASS，Nuxt production SSR build。
- `node apps/storefront/tests/m0-ac11-ssr.mjs`：PASS。測試只用 test-owned synthetic HTTP API stub 與 production Nuxt server，直接檢查首屏 HTML；zh-TW／ja／en 商品名稱與總額、同一 transaction ID、mock 標示、`noindex,nofollow`、`no-store`、Failed／Unknown 狀態、拒絕回應不洩漏 trace／摘要，並確認 SSR 將 incoming capability cookie 傳給同 origin API proxy。測試結束會停止它建立的 Nuxt／stub 程序。
- `git diff --check`：PASS。
- 限制：唯讀 `./infra/sql-server/manage.sh status` 顯示沒有運行中的 SQL containers。沒有啟動／建立 fixture、reset、migration 或寫入商品／交易資料。因此此結果只證明 SSR 渲染與 proxy 行為，未證明真實 Commerce API 的 actor/scope 授權或 SQL-backed `getCheckoutStatus`；`IMPLEMENTATION_STATUS.yaml` 與來源綁定將 M0-AC11 保持 PARTIAL，待專用 SQL/API 證據補足。未驗外部付款或資金結算。

## M0-AC11 專用 fixture CheckoutStatus actor scope 補充（2026-10-16）

候選：`b06bc61b0de9e69defdfa54c73287e0978ba1753`，基底 `4c82dccf291212ca73366deeb942b05867eabd43`。沿用 `.local/m0/issue06-compose-project` 指向的既有 `kohami-issue06-*` SQL 2025 專用 fixture；先以 Compose project marker 確認容器為 Up，再由 runner 驗證 strict TLS。沒有建立／移除 fixture、執行 migration/reset 或觸碰展示資料。

- `python3 -m unittest tests/m0/test_issue06_acceptance_runner.py -v`：PASS，7/7；涵蓋預期 API denial 的 status/code 檢查與 owner/other-browser assertion 編排。
- `python3 scripts/m0/run-issue06-aftercare-acceptance.py`：PASS。API 建立合成 checkout 後，持有 checkout capability 的 owner browser 成功讀取 status；另以 API 簽發的不同有效 browser cookie、無原 checkout cookie 查詢同一 ID，回 `404 RESOURCE_NOT_FOUND`。API 隱藏不屬於該 actor 的 checkout。
- 同一旅程的 mock payment worker 完成付款，Playwright 售後旅程 PASS 1/1，SQL 確認 Confirmed order、Delivered shipment、Inspected return 與 Pending mock refund。Runner 結束清理本輪 synthetic rows/session artifact，保留 dedicated fixture。
- TDD helper regression 驗證預期 denial 只有在 status 與 code 同時符合時通過；命令輸出不包含 API body／cookie／私密連線資料。

## M0-AC11 真 API／SQL Nuxt SSR 整合補充（2026-10-16）

候選：`ba534f50d1100f122bfee412a61cefed2828f7ff`，基底 `4a09170`。沿用已存在的 Issue06 SQL 2025 dedicated fixture；Nuxt、API 與 worker 在同一 runner 旅程啟動，沒有 provisioning、migration、reset 或重建 fixture。

- `python3 -m unittest tests.m0.test_issue06_acceptance_runner -v`：PASS，8/8。新增測試核對 SSR owner 摘要、跨 browser generic unavailable、兩次 SSR 的 cookie actor 分離；包含 Nuxt hydration key 帶 checkout ID、但拒絕頁不渲染交易 ID／商品／金額內容的案例。
- `python3 scripts/m0/run-issue06-aftercare-acceptance.py`：PASS。strict SQL TLS 通過；真 API owner browser 可查 CheckoutStatus，另一個 API 簽發的 signed browser 收到 `404 RESOURCE_NOT_FOUND`；同一 checkout 透過 Nuxt SSR 回傳 owner 的 checkout ID、mock 標示與真 API／SQL 總額，另一 actor 只看到通用 unavailable 頁，沒有渲染交易 ID、商品或金額摘要。
- 同一 runner 繼續完成 Playwright 售後旅程 PASS 1/1，SQL 確認 Confirmed order、Delivered shipment、Inspected return 與 Pending mock refund。結束清理本輪 synthetic rows/session artifact，保留 dedicated fixture。
- `git diff --check`：PASS。此項驗收只新增 runner 與測試，沒有產品 API、Nuxt 頁面、SQL schema 或資料政策變更。

## Issue 06 browser UI 售後驗收（2026-10-15）

狀態：storefront browser UI＋本機 API／SQL mock 售後旅程 PASS；Issue06／M0 其餘未執行項目不因此變成通過。

- `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`、`dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS，各 0 warnings／0 errors。
- `npm run typecheck`（`apps/storefront`）：PASS；`python3 -m unittest tests/m0/test_issue06_acceptance_runner.py -v`：PASS，4/4。
- `env KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" /usr/local/share/dotnet/dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：既有 dedicated fixture PASS，75 assertions；新增退貨摘要依 `CommerceReturnSummary` 的 `lines[]` contract shape 檢查。
- `python3 scripts/m0/run-issue06-aftercare-acceptance.py`：PASS，Playwright 1/1。API 預先建立 synthetic confirmed order；瀏覽器從 storefront 查詢訂單、查看 DEMO 收據／無稅提示、建立配送並操作 Shipped／InTransit／Delivered、提交退貨、營運受理／收件／驗收及送出 mock refund。退款 challenge 由測試 adapter 以 SQL scoped synthetic proof 回應；refund API／SQL 仍由本機真實 API／SQL mock adapter 處理。
- SQL 驗證 Confirmed order、Delivered shipment、Inspected return 與 Processing／Unknown refund operation。runner 停止僅由自身啟動的 API／worker／Nuxt，清理本輪 synthetic rows，還原 Maintenance／SKU；dedicated fixture 與 volumes 保留，沒有 reset。
- 驗收發現並修正兩個契約問題：storefront 原本預期訂單摘要帶 `shipmentSummary.shipments[]`，實際公開契約只有 `shipmentSummary.state`；CommerceOrder 的退貨摘要原本輸出與 `CommerceReturnSummary` schema 不同的欄位，遇到退貨後會被 API contract middleware 拒絕。前端現在保存 API action 回傳的配送／退貨操作資料，後端摘要符合既有 schema。

## Issue 06 M0-A Identity 來源綁定驗收（2026-10-16）

候選：`321d63abd49f430b897c65b3a0aab95514038092`。本次將 Identity proof 實作與測試綁定到此候選並重跑 dedicated SQL 測試；只使用 `.local/m0/fixture-private.json` 所指的 `demo_m0` dedicated fixture，未讀取或輸出私密連線材料。

- `M0_IDENTITY_SQL_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet test tests/Identity/Identity.Tests.csproj -c Release --no-build --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --logger 'console;verbosity=normal'`：PASS，30/30（24 純規則案例、6 個 SQL facts）。
- SQL fixture 測試以隨機合成識別值新增資料；fixture dispose 會刪除該測試使用者關聯資料並還原原 Maintenance 值。本次測試正常完成。測試 harness 使用 strict TLS 並將 SQL target 限制為 `KohamiM0`。
- 限制：此結果單獨只將 BIND-07 Identity source 與測試綁定，當時尚未涵蓋 AC08／09 的 API guard、actor/nonce/expiry、logout 與 SSR cache 證據；後續驗收結果見下節。

## Issue 06 M0-AC08／09 Identity 安全與 SSR 驗收（2026-10-16）

驗收以 `bd64f65416a8d347e73732b512459bcb58cce24f` 為 worktree 基底，使用既有專用 `demo_m0` fixture；未執行 migration/reset，runner 結束清除本輪合成 Identity rows／session artifact，保留 fixture。M0-AC08、M0-AC09 的來源綁定驗收更新為 PASS；M0 整體仍是 `PARTIAL_NOT_M0_COMPLETE`。

- `python3 scripts/m0/run-issue06-aftercare-acceptance.py --identity-only`：PASS。實際 API 對錯誤 Origin 與缺少 antiforgery token 的 POST 都回 `403 CSRF_INVALID`，並在 Identity handler 前拒絕。
- Playwright `Issue06 Identity logout rejects replay and clears SSR persona on back navigation`：PASS，1/1。瀏覽器以合成預先授權 Owner session 載入 SSR，登出後舊 session cookie 重播只得到 Anonymous，匿名頁與返回頁面的 HTML 均不再含原 userId；SSR response 為 `no-store`。
- `python3 -m unittest tests.m0.test_issue06_acceptance_runner -v`：PASS，5/5；`npm --prefix apps/storefront run typecheck`：PASS。
- 初次完整 aftercare runner 將 logout assertion 混在 Commerce 瀏覽器旅程中，因等待 DELETE 回應逾時而失敗；runner 清理了本輪合成 rows 並保留 fixture。隔離成 `--identity-only` 後，第一次返回頁面檢查遇到 navigation context 競態；改為等待身份區 DOM 後重跑成功。產品行為沒有因此修改。

## Issue 06 M0-AC06 固定時鐘付款截止與取消競態（2026-10-16）

狀態：PASS（本機 mock adapter 範圍）；M0 整體仍為 `PARTIAL_NOT_M0_COMPLETE`。測試使用既有 dedicated `demo_m0` fixture、合成識別值與可注入固定時鐘，沒有重設資料庫。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS，0 warnings／0 errors。
- `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：PASS，85 個 assertions。付款成功在 15 分鐘到期前一秒 consume reservation 並只建立一筆 primary collection；等號時先 release，再將成功記為 late collection 與一筆 compensation；重播不增加收款、補償或庫存效果。取消後晚到成功維持 Cancelled、不二次釋放庫存；取消與到期等號付款回呼使用 gate 同時開始，SQL 結果為單一序列化終態、一次 release、一筆 late collection 與一筆 compensation。
- 四組測試資料 teardown 後均驗證不存在其 checkout/order/payment/reservation/collection/job/idempotency/inbox/document，且 `sku_demo_03` 庫存回到各情境原值。相同 harness 的既有 worker recovery checks 也通過，涵蓋 persisted Unknown 不開新付款操作與 worker 重啟／重播。
- 測試初次因缺少合成 `kohami.browser` cookie 在呼叫 Cancel 前中止；補上受保護的測試 cookie 後重建並完整重跑通過。沒有因此修改產品取消或付款邏輯。
- 限制：此為本機 mock adapter／SQL transaction 證據；沒有呼叫外部 provider，也沒有在真實 provider 呼叫中 kill process。專用 fixture 連線僅在核准的本機 loopback 執行環境使用。

## Issue 06 M0-AC07 退款額度、併發與撤權後追蹤（2026-10-16）

狀態：PASS（本機 mock Commerce／SQL 範圍）；M0 整體仍為 `PARTIAL_NOT_M0_COMPLETE`。使用既有 dedicated `demo_m0` fixture、合成 Owner／案件／collection／unit 與 deterministic teardown，未重設資料庫。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS，0 warnings／0 errors。
- `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：PASS；新增 AC07 acceptance 24 assertions，完整 Commerce harness 共 109 assertions 通過。兩個同步閘門案例證明同 line／unit 競爭僅一案保留，而不同 paid lines 可並行；兩案競爭原始 `ShippingPaid=60` 時僅一案可保留 shipping，總退款 Pending 不超過原 collection 1,590。
- Unknown worker 結果保留金額、unit slots 與 shipping slot；`CheckRefund` 指向原 operation，可信查核結果將該 operation 結算一次，重複結果無額外效果。撤權在 worker `Started` 前發生時，工作標記 Blocked、operation 為 Failed 且 amount／unit／shipping 保留全數釋放；撤權在 `Started` 後發生時，系統仍追蹤同一 operation 並套用一次可信結果。
- 每組測試 teardown 驗證 collection、unit、document、job、idempotency、inbox、grant 與 synthetic user 均移除；沒有改動庫存或展示資料。
- 完整 harness 重跑時也調整 AC06 取消／付款競態的拒絕斷言，以接受實際觀察到的 409 `INVALID_TRANSITION` 或 412 `VERSION_CHANGED`。此為測試競態的合法 optimistic-concurrency outcome，未改產品程式。
- 限制：此為直接呼叫 Commerce service 的本機 mock SQL harness，不是完整 HTTP／browser refund journey；可信結果由 test-only worker seam 提供，未連外部 provider、未移動真實資金，亦不證明真實供應商拒絕／查詢契約。既有 browser acceptance 只到 refund request／Unknown。

## Issue 06 M0-AC10 mock event 與 worker dispatch 驗收（2026-10-16）

狀態：PASS（本機 mock Commerce／SQL 範圍）；已測候選提交為 `c1306e0271a2441a229d13434477921f9c7d4ed9`，基底為 `e5a8dde53aa2babde7bbe64a741abd7b878be0e7`。新增測試只有專用合成資料，不更動付款／worker 產品程式或 SQL schema。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS，0 warnings／0 errors。
- `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：PASS，完整 Commerce dedicated SQL harness。新增斷言驗證 `enqueueDemoScenario` 只將 Scenario job 入列為 `NotStarted`，回應不把 worker 說成已啟動且目標收據不變；撤銷 `demo.control` 後，該未開始工作變成 `Blocked` 且目標仍不變。測試之後恢復合成 grant，讓後續授權正向案例照常通過。
- 同一 harness 新增事件 hash 衝突檢查：已套用的成功事件以相同 Event ID、不同 outcome 重送會回 `409 KEY_CONFLICT`；Inbox 保留原事件一筆，付款仍為 `Succeeded`，collection 仍僅一筆。既有 scenario matrix 的 success／declined／unknown／duplicate／amount mismatch、兩個獨立 worker 競爭重取過期 Started payment／receipt job、single reclaim、單一效果、已完成 replay 拒絕與 stale-fence 拒絕均通過。
- Source binding 固定了 `Aftercare.cs`、`Worker.cs`、`Store.cs`、Identity `SQLSchema.sql`／`SQLGrants.sql`、Commerce `Program.cs`、`WorkerRecoveryMatrix.cs` 與 `TwoWorkerPaymentRace.cs` 的 SHA-256，詳見 `SOURCE_BINDING_TEMPLATE.yaml` 的 M0-AC10 case。SQL observation 使用 dedicated fixture admin connection；worker 授權仍由 runtime stored procedure 即時裁決。
- 專用 fixture 的測試 teardown 通過；沒有重設 DB，所有本輪合成 user/grant/job/event/ledger/document 清理，且庫存／既有展示資料保持原狀。
- 限制：情境透過 Commerce service 與 test harness 驗證，未覆蓋完整 HTTP／browser demo scenario 旅程；只用 mock adapter，沒有外部 provider 或真實資金結算。這不代表 M0-AC11／12 或整體 M0 完成。

## M0-AC01 schema readiness 與 fresh seeded migration acceptance（2026-10-16）

實作基底為 `039f5a5f5740b6b678015683726b122bc95f6cb4`。驗收在 `/private/tmp/kohami-schema-readiness-20261016` 的隔離 checkout、loopback 14337 SQL Server 2025 fixture 執行；既有 14336 Issue06 fixture未連線、未修改。

- 新增 `GET /ready`：只讀核對 migration ledger 的 ID、順序與 source hash；完整支援版本回 200 `ready`，缺失／未知／不完整／hash mismatch 回 503 `not-ready`。`GET /health` 保留資料庫連線語意。API principal可 SELECT `ops.SchemaMigrations`，對同 schema 的 `ops.DevelopmentFixtureMarker` 仍被拒絕（SQL 229），不會執行 migration 或 seed。
- 0003 migration 保持原有 source hash；新增 0004 撤銷 `m0_api` 對 `ops` schema 的廣域 SELECT DENY，再只授權 ledger SELECT，其他 `ops` 物件仍不可讀。
- API Release build、Native Release build 均 0 warnings／0 errors。Native synthetic suite PASS 59 checks，包含現行 migration source hash 對照、空／不完整／未知／hash mismatch ledger 拒絕。
- Fresh fixture 套用 0001–0004 與 `CommerceSeed.sql`；完整 Commerce SQL harness PASS。API `/ready` 基準為 200；SQL acceptance 暫時 rename ledger、改一筆 hash、插入未知 migration 時，HTTP 各回 503，還原後各回 200。
- SQL migration acceptance 驗成功 additive migration replay、注入失敗 rollback、history cleanup，並逐列 snapshot Inventory、Documents、Collections、RefundUnits；升級前後 seed snapshot hash 相同。
- Acceptance 後停止本次 API，移除僅本輪 compose project／network／containers／volumes 與臨時 private materials。原 14336 fixture保持運作，未執行 baseline adoption。
- M0-AC01 維持 PARTIAL：本次完成 schema readiness 與 fresh seeded-upgrade 缺口；既有 cross-Dataset read evidence涵蓋選定 Commerce reads，尚未覆蓋所有 M0-AC01 foreign-Dataset FK/read rejection cases。不得將本輪證據推成 AC01 PASS。

## M0-AC01 cross-Dataset FK／read isolation closure（2026-10-16）

依候選 `39dac4d22e4f7dc0f069edeb4606889a8a0a4d73` 執行於全新 loopback 14337 SQL Server 2025 strict-TLS fixture。新增 Commerce acceptance 確認 11 張含 Dataset 欄位的表都以 `CHECK(Dataset='demo_m0')` 限制資料集；三條複合 FK（Reservations→Inventory、RefundUnits→Collections、PrivateDeliveries→GuestRequests）的子鍵與父鍵都包含 Dataset。以唯一合成外部 Dataset 父列驗證三種 `demo_m0` 子資料引用均被 SQL Server 拒絕。既有八項同 ID 碰撞讀取投影亦在同一完整 Commerce SQL harness 重跑通過。

Native 與 Commerce Release builds 均 0 warnings／0 errors；完整 Commerce SQL harness PASS。既有 Identity foreign-Dataset session-cookie regression 36/36（candidate `fd00076ba858965df813cbd47dd2fa6e8a5eb7ed`），其 schema／test source 至本次基底未再變更。此次專用 fixture 的 containers、network、volumes 與私有材料均已移除，既有 14336 fixture 未存取。結合先前 `/ready` 未知 schema 拒絕及 fresh seeded-upgrade row snapshot 證據，M0-AC01 更新為 **PASS**；不代表其他 M0 案例完成。完整矩陣與命令記於[本次證據](../../.ai/runs/m0-ac01-dataset-isolation-20261016/EVIDENCE.md)。

## M0-AC02 瀏覽器 cart 冪等與 token 輪替驗收（2026-10-16）

M0-AC02 更新為 **PASS**。以 main `157c7d55ebd5e309ac1a2f7ba0efcc07f03e49aa` 為基底，使用既有 marker-verified Issue06 SQL2025 dedicated fixture，strict TLS 通過；runner 暫時開啟 Maintenance gate 並於 teardown 還原，未建立／移除 fixture、未 reset 或啟動 worker。

- `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` 與 `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`：PASS，0 warnings／0 errors；`npm --prefix apps/storefront run typecheck`：PASS；`python3 -m unittest tests/m0/test_issue06_acceptance_runner.py -v`：PASS，8/8。
- `python3 scripts/m0/run-issue06-aftercare-acceptance.py --ac02-only`：Playwright PASS 1/1。瀏覽器取得 CSRF 後 POST createCart；API 提交成功但測試代理故意回 503，模擬回應遺失。接著清除 Browser／CSRF cookies、取得新 Browser ID 與 CSRF token，以相同 Idempotency-Key 重送；回應 cart ID 與首個已提交 cart 完全相同，`/carts/current` 仍可讀。測試同時核對 Browser cookie 確實輪替、穩定的 HttpOnly `kohami.command` cookie 不變；錯 token 與錯 Origin 各回 `403 CSRF_INVALID`。
- SQL 證據：同 key 恰有一筆 createCart 冪等紀錄及一筆相同 ID／actor 的 cart；錯 token／Origin 的 key 沒有寫入紀錄。Runner 只刪本輪隨機 key 對應的 cart／冪等資料，並以 SQL 核對清理與 Maintenance 值還原。
- TDD 紅燈以修改前 runtime 重現：輪替後同 key 回傳不同 cart ID；加入穩定 command actor 後綠燈。Browser ID 可輪替，command actor 維持同一匿名操作身分。
- 限制：完整 Commerce harness 在同一既有 fixture 的 catalog isolation assertion 停止；實際 `sku_demo_01` 價格為 500、庫存為 0，而該既有案例固定期待 `InStock`。該 harness 會清除自身合成列並重啟 constraint；此 fixture 狀態未被本次 AC02 修改。沒有宣稱完整 Commerce harness 通過。

## Issue 08 Catalog 本機 mock acceptance（2026-10-16）

候選程式碼以目前 `main` 工作樹建置；API、Commerce 與 Native Release build 均為 0 warnings／0 errors。驗收使用 `/private/tmp/kohami-issue08-acceptance` 專用 source harness，loopback SQL Server 2025 port 14337，strict TLS 與 marker 驗證通過；先前 Issue06 fixture 14336 僅作唯讀 baseline 檢查，因缺 `ops.SchemaMigrations` 在 DDL／DML 前回 `SQL_MIGRATION_BASELINE_REQUIRED`，未採用舊 schema baseline、未改動資料。

- `dotnet restore tests/Commerce/Commerce.Tests.csproj --force-evaluate --disable-parallel -p:NuGetAudit=false -m:1 -nr:false`：PASS；Commerce、API、Native Release build PASS；Storefront `npm --prefix apps/storefront run typecheck` PASS；Playwright Issue08 spec discovery 列出 1 case；runner Python compile／help PASS；`git diff --check` PASS。
- `python3 scripts/m0/run-issue06-aftercare-acceptance.py --catalog-only`：Playwright PASS 1/1。覆蓋匿名建立拒絕、Owner 建立、slug 衝突、草稿不公開、發布後顧客頁內容、TWD 1,200、公開快照不含 `sourceRightsRef`、改價不提前改 public snapshot、重新發布至 TWD 1,550、下架後 404。
- SQL acceptance PASS：最終 Unpublished、唯一 Product／SKU、Inventory 價格 1,550 且 inactive、public snapshot 保留並遮罩來源權利參考，create／update／publish／unpublish audit 次數符合預期；清除本輪 Product／SKU／Inventory／idempotency 與 synthetic Owner／session／grant。Fixture 容器、volume、network、私有材料已移除。
- TDD／缺陷修正：第一次旅程揭露 API 欠缺 `m0.Grants` table read grant；維持最小權限，改新增 `0006_catalog_authorization_recheck`，在既有 security lock 內由 SQL stored procedure 重驗 actor。再次旅程發現材質 assertion 與管理頁的合成值不一致，調整 spec 後全程通過。
- 限制：本證據只涵蓋本機合成 Catalog；未驗真實商品照片、材料、尺寸／權利證明、Merchandiser 專屬 UI journey，亦未故障注入保存／讀取錯誤。不是正式商品資訊或素材授權驗收，也沒有執行獨立乾淨 checkout NuGet 網路驗證（此項依使用者指示列為選配）。

## Issue 08 保存／讀取失敗外部行為補充（2026-10-16）

候選為 `main` 上 Issue 08 acceptance harness；沒有變更產品 runtime、資料庫 schema 或 API error contract。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -nr:false`：PASS，0 warnings／0 errors；`npm --prefix apps/storefront run typecheck`：PASS；runner `python3 -m py_compile` 與 `git diff --check`：PASS。
- `python3 -m unittest tests/m0/test_issue06_acceptance_runner.py -v`：PASS，8/8；同步舊斷言以涵蓋目前 `catalog-only` runner lifecycle。
- `KOHAMI_M0_LOCAL_DIR=/private/tmp/kohami-issue08-failure-fixture/.local/m0 KOHAMI_M0_FIXTURE_ROOT=/private/tmp/kohami-issue08-failure-fixture python3 scripts/m0/run-issue06-aftercare-acceptance.py --catalog-only`：PASS。全新 dedicated SQL Server 2025 fixture 使用 loopback 14337、strict TLS、fixture marker 與 migrations 0001–0006；測試完成後清除 synthetic rows、Owner／session／grant、故障注入及此 Compose project 的 containers／network／volumes／private temp directory。既有 14336 fixture 未修改。
- Playwright save fault 1/1 PASS：Inventory INSERT 的專屬暫時 trigger 令 Catalog create 中止；API 僅回 `503 SERVICE_UNAVAILABLE`，payload 不含 SQL／trigger 細節；SQL 證明沒有 Product、SKU、Inventory、create audit 或 idempotency partial rows。
- Playwright public read fault 1/1 PASS：暫時 DENY `m0_api` 對 `m0.CatalogProductSkus` 的 SELECT；已發布商品查詢回泛化 503，沒有部分商品 payload。接著恢復 canonical SELECT grant，再驗 Owner 下架 1/1 PASS 並回 404。

## Issue 09–10 商品可售與首頁搜尋驗收（2026-10-17）

Issue 09 的新 dedicated fixture 驗收完成後，Issue 10 在同一 disposable SQL Server 2025 fixture（loopback port 14337、marker 驗證、strict TLS、migrations 0001–0007）執行。未接觸既有 port 14336 fixture；沒有清除 fixture 或重設資料庫。

- Issue 09：`python3 scripts/m0/run-issue06-aftercare-acceptance.py --stock-only`，Playwright 1/1 PASS。Owner 調整 showcase SKU 庫存至缺貨，公開頁顯示缺貨且禁止購買；runner 核對 SQL/session 清理並恢復原庫存。
- Issue 10：`python3 scripts/m0/run-issue06-aftercare-acceptance.py --search-only`，Playwright 2/2 PASS。Runner 暫存一個 showcase SKU 原庫存後設為 0；HTTP/API 驗證公開搜尋仍可找到缺貨商品、`availability=in_stock` 排除該 SKU、顏色與尺寸在同一 SKU 上匹配、SKU 搜尋、未知參數 422、limit/cursor 前後頁穩定；手機首頁驗證商品導覽、離線讀取錯誤／重試與無結果提示。測試後原庫存確實恢復，synthetic Owner/session 清理完成。
- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false` 與 `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false`：PASS，0 warnings／0 errors。Commerce domain acceptance 包含 query normalization、參數 allowlist、邊界、游標繫結案例；SQL acceptance 經 dedicated fixture 的真 API 查詢執行。
- Storefront `npm run typecheck`、`npm run build`：PASS。`python3 -m unittest tests/m0/test_issue06_acceptance_runner.py tests/architecture/test_layout.py -v`：PASS，12/12。
- 限制：目前 Catalog draft／public JSON 無 category 欄位，分類篩選需等分類資料所有權、管理與公開契約確立；`category` 現以未知參數 422 處理。沒有 schema／migration 或交易行為變更；M0 仍為 `PARTIAL_NOT_M0_COMPLETE`。
- 原 Issue 08 成功／拒絕／改價／發布旅程 1/1 PASS；SQL 核對最後 Unpublished、Inventory 價格、sanitized public snapshot 及 create／update／publish／unpublish audits，然後清除合成資料。
- 限制：failure injection 僅證明此本機 SQL/API mock 的已觀察錯誤路徑與交易回滾；真實商品照片、素材權利、正式材質／量測／售價仍未取得。Issue 08 維持 claimed。沒有執行 clean checkout NuGet restore（依使用者要求列為選配）。

## Issue 11 SKU／購物車／Quote 本機 mock acceptance（2026-10-17）


- `KOHAMI_M0_LOCAL_DIR=/private/tmp/kohami-issue11-fixture/.local/m0 KOHAMI_M0_FIXTURE_ROOT=/private/tmp/kohami-issue11-fixture python3 scripts/m0/run-issue06-aftercare-acceptance.py --cart-only`：PASS，Playwright 4/4。驗證未自動選 SKU、顏色＋服裝尺寸後加車、修改數量／移除、DEMO15 15% 折扣、server quote 的 TWD 商品／折扣／配送／總額一致、購物車不保留庫存、DELETE ETag／同 key replay／舊 ETag 412，以及改價 `QUOTE_CHANGED`、庫存降低 `STOCK_CHANGED` 對顧客的提示。
- `npm --prefix apps/storefront run typecheck`、`npm --prefix apps/storefront run build`、API 與 Commerce Release builds：PASS。兩個 .NET builds 各 0 warnings／0 errors。
- `python3 -m unittest tests.m0.test_issue06_acceptance_runner -v`：PASS，10/10；`python3 -m unittest discover -s tests/architecture -v`：PASS，3/3；runner `py_compile` 與 `git diff --check`：PASS。`python3 -m unittest discover -s tests -v` 未發現 tests，故不視為全套通過。
- 完整 `Commerce.Tests.dll` SQL harness 有一項失敗：`DatasetReadIsolationAcceptance` 讀到 `sku_demo_01` 為價格 500／OutOfStock，未符合該測試預期 InStock。Issue11 runner 使用的展示 SKU 為 `demo-mock-neck-knit-warm_gray-l`，其暫時價格／庫存變動已還原；本次結果仍列為 Commerce SQL suite 未全通過，不推定其根因。
- `scripts/check_kohami_api_contract.py` 未能執行，因環境缺少 Python `yaml`／PyYAML 套件。整合 OpenAPI Authority 保持 PROPOSED，未以文件檢查替代產品驗收。
- 限制：M1-AC06 僅本次已測操作／版本／重送範圍通過；AC07／08／10 為部分覆蓋。未驗不存在／停用 SKU 與 fee 變更、載入失敗恢復、完整 1590 固定單位分攤／5 分鐘 Quote expiry、完整三語、鍵盤／焦點可及性或效能；不代表正式商品、費用、付款／庫存承諾或真資金處理。
- 驗收完成後，依 marker `kohami-issue06-88bc7387c3a9` 與 port 14337 移除本任務建立的 disposable fixture containers／network／volumes，並清除 detached 臨時 worktree；主 checkout 的既有 `.local/m0` 未存取。

## Issue 17 三語購物旅程與語系網址（2026-10-17）

狀態：PASS（本機 mock Storefront 範圍）；未設定實際公開網域，沒有部署或向搜尋引擎提交網站。Nuxt routes 提供 `/zh-TW`、`/ja`、`/en` 前綴；舊 `?lang=` 與無前綴頁面以 301 導向對應語系。首頁搜尋條件、商品已選 SKU／顏色／尺寸以 URL 保留；語言切換維持同一購物車 session、結帳 ID、TWD 金額與伺服器交易狀態。商品缺譯明示繁中 fallback；會員訂單狀態依語系顯示。私人會員／購物車／結帳頁及本機預設仍 noindex；首頁與有效商品頁須設定 HTTPS `NUXT_PUBLIC_SITE_URL` 才輸出 absolute canonical、三語 hreflang／x-default 與 `index,follow`。

- `npm run typecheck`：PASS。
- `npm run build`：PASS，Nuxt 4.6.1 production client／SSR／Nitro bundle 完成。
- `npx playwright test tests/issue17-localized-routes.spec.ts --config=playwright.config.ts`：PASS，9/9。涵蓋 legacy redirect、商品 SKU／顏色／尺寸切語保留、缺譯 fallback、cart 同品項與 TWD、checkout Unknown／Failed／Succeeded 切語、搜尋無結果 query 保留、公開 canonical/hreflang 與會員訂單狀態翻譯／private noindex。
- 瀏覽器測試對接 localhost synthetic API fixture 與 `https://shop.example.test` 測試 origin；不讀寫 SQL、不代表正式 API／SQL 端到端或實際搜尋引擎收錄。測試資料和憑證僅建立於 `/private/tmp`，未加入版本控制。
- 檢查限制：沒有設正式 domain、sitemap/robots.txt 發布清單、部署或外部 crawl；TDD 預期失敗在實作前未跑，因此不宣稱完整 red-green-refactor。Issue 08 已有授權商品翻譯草稿／發布流程，本票沿用，未新增 CMS。


## Issue 18 出貨與顧客配送追蹤（2026-10-17）

- 實作候選新增可信 Primary 付款確認 UTC 時點、本人訂單的去識別配送歷程與三段 UTC 耗時；Storefront 顯示三語 mock 時間軸及非真實承運聲明。沒有加入營業日行事曆、SLA 判斷或 migration。
- `dotnet build tests/Commerce/Commerce.Tests.csproj --no-restore --configuration Release`：PASS，0 warnings／0 errors。`npm run typecheck`、`npm run build`（`apps/storefront`）：PASS。`npx playwright test tests/issue18-shipment-tracking.spec.ts --reporter=list`：PASS，1/1，瀏覽器使用 loopback synthetic API route。OpenAPI JSON parse 與 `git diff --check`：PASS。
- Commerce SQL 新斷言已加入 `tests/Commerce/Program.cs`，但完整 acceptance 未達到該斷言：原 runner 先在 Issue 16 因 `PRODUCT_UNAVAILABLE` 中止；暫略過此單一檢查後，又在既有 `RefundAcceptance` 的 worker dispatch 檢查中止。Dedicated `demo_m0` 未 reset，沒有將此輪 SQL 標示 PASS。
- 影響遍歷只有 `CommerceService.Order` seed 一節點，架構圖未映射且 consumer coverage unknown；人工核對 `Aftercare.cs` 授權／投影、runtime `CommerceOrder` schema 及 storefront `OrderView` consumer。未映射圖的限制記錄於 `.ai/runs/issue18-shipping-tracking/CHANGE_IMPACT.yaml`。Issue 18 尚未 resolved；本輪 SQL 公開投影／UTC 耗時後續結果見下方補充。

### Issue 18 dedicated SQL acceptance follow-up（2026-10-17）

Commerce 與 Native Release build 均 PASS，0 warnings／0 errors。使用 `python3 scripts/m0/provision-issue06-fixture.py --port 14339` 建立新的 loopback-only SQL Server 2025 fixture；strict TLS、migrations 0001–0008（含 password-only auth）及完整 Commerce SQL harness 均 PASS。SQL assertions 確認付款確認與出貨建立時間為 UTC、配送歷程含事件時點但不含 actor／eventId／reason，付款至出貨、出貨至已出貨、已出貨至已送達耗時均非負，且重送、亂序拒絕與 Exception 恢復後維持有序歷程。一次性 containers、volumes、network 與私有 fixture 設定已移除；既有 14336 fixture 未遷移或重設。

測試前置修正只觸及 acceptance harness：Issue 16 暫設一件合成庫存並在 `finally` 還原；Issue 18 SQL assertion 改用 API 實際欄位 `shippedToDeliveredSeconds`。Storefront Playwright 仍是獨立合成 API route 測試，尚未完成 live API／SQL／Storefront browser journey，因此不把 Issue 18 標記 resolved。

### Issue 18 live API／SQL／Storefront 結案驗收（2026-10-17）

此紀錄補完上方先前「live journey 尚未執行」的階段狀態。重新建置 Commerce harness 與 Native migration tool 後，以 port 14339 建立一次性 loopback-only SQL Server 2025 dedicated fixture；strict TLS、migrations 0001–0008 與完整 Commerce SQL harness 均 PASS。初次誤用既有 14336 fixture 時，舊 schema 缺少 AuthenticatedAt，驗收在 seed fixture 階段停止；未對該 fixture 執行 migration、reset 或資料變更，改用新建的 14339 fixture。

- dotnet build tests/Commerce/Commerce.Tests.csproj --no-restore --configuration Release --verbosity minimal --maxcpucount:1 --nodeReuse:false -p:UseSharedCompilation=false：PASS，0 warnings／0 errors。dotnet build tools/native-operations/Native.csproj --no-restore --configuration Release --verbosity minimal --maxcpucount:1 --nodeReuse:false -p:UseSharedCompilation=false：PASS，0 warnings／0 errors。
- python3 -m unittest tests.m0.test_issue06_acceptance_runner -v：PASS，21/21。npm run typecheck（apps/storefront）：PASS。
- python3 scripts/m0/run-issue06-aftercare-acceptance.py --issue18-shipment-only：PASS。Playwright live API／SQL／Storefront 1/1；Owner 對已付款訂單建立出貨並推進到 Delivered，guest capability 讀回本人訂單與 UTC 時間軸，最後以 SQL 直讀核對持久化歷程。
- SQL harness 另外驗證未付款訂單建立出貨回傳 409 INVALID_TRANSITION、非履約呼叫者建立或推進出貨回傳 403 FORBIDDEN，且拒絕不建立 Shipment／不改變 Pending 狀態。重複事件、亂序版本拒絕、Exception 恢復仍保有正確歷程；Order response 符合 runtime OpenAPI schema。
- Storefront typecheck、既有合成 API route Playwright 與 runner 單元測試均通過。測試範圍不包含真實承運商通知、正式 carrier、營業日或 SLA。
- 成功後依精確 Compose marker 移除一次性容器、volumes、network，確認 14339 port 釋放，並還原原 .local/m0 私有設定；原有 14336 fixture 未遷移或重設。
- Change Impact traversal 保持 BOUNDED_WITH_UNKNOWNS：API／data／events coverage partial，consumer coverage unknown；人工檢視直接 authorization、Order／Shipment SQL projection 與 Storefront timeline consumer，限制記錄於 .ai/runs/issue18-live-acceptance/CHANGE_IMPACT.yaml。

## Issue 19 退貨申請、客服與驗收（2026-10-17）

- Runtime `CommerceReturnSummary` 新增分類、公開時間軸、首次支援回應／人工升級 UTC 節點與耗時，以及驗收可再售／隔離數量。時間僅計 UTC 經過秒數；法定退貨期限、日曆／營業日及 SLA 保持待決，未推定服務承諾。
- `RequestReturn` 保持本人訂單／guest capability、已送達及可退款單位檢查。拒絕需可理解理由並釋放該案尚在 Processing 的退款單位；新操作可重申請，活動案件的單位仍被原子保留。Escalated 是需原因的手動 Support 狀態，不觸發自動派工、通知、SLA 或庫存異動。Customer projection 僅公開拒絕理由，排除 actor 及升級內部理由。
- `dotnet build tests/Commerce/Commerce.Tests.csproj --no-restore --configuration Release --verbosity quiet --maxcpucount:1 --nodeReuse:false -p:UseSharedCompilation=false`：PASS，0 warnings／0 errors；完整 Commerce SQL harness 在 loopback-only SQL Server 2025 dedicated fixture PASS，含無授權客服拒絕、拒絕原因、UTC 首次回應、拒絕後新 capability 重申請、活動單位重複申請阻擋、人工升級隱私、Inspection movement／原快照及更新後 Order response runtime OpenAPI 驗證。
- `npm run typecheck`（`apps/storefront`）：PASS；`python3 -m unittest tests.m0.test_issue06_acceptance_runner -v`：21/21 PASS；live `python3 scripts/m0/run-issue06-aftercare-acceptance.py`：Storefront Playwright 1/1 PASS，SQL 回讀確認該旅程持久化為 Delivered／Inspected／待處理 mock refund。Storefront 測試同步修正一項已過時的出貨時間軸標題斷言。
- 驗收透過專用 `/private/tmp/kohami-issue19-fixture-20261017` 副本和 port 14339 執行；Commerce harness、browser runner 各自清理合成業務資料。未改動 `.local/m0` 既有 14336 fixture。暫存 SQL 容器與資料卷在結案後移除。
- 退款執行屬下一張 Issue 20；本票只產生既有待處理退款案件。真實承運、通知、正式退款、法律權利判定及正式營運驗收均未涵蓋。

## Issue 20 受控退款與財務核對（2026-10-17）

狀態：PASS（本機 mock 受控退款／財務核對與 Storefront 顧客狀態範圍）。Storefront 現在呈現後端既有 `CommerceRefundSummary` 投影的五類退款筆數、已退款／尚保留 TWD 金額及 mock 聲明；不呈現 operation ID，也不由前端決定退款資格、金額或終態。API 合約、資料表、migration、事件與授權規則均未變更。

- `npm run typecheck`（`apps/storefront`）：PASS。專用 `tests/issue20-refund-status.spec.ts` 測試退款狀態、金額、隱私與 mock 聲明；Issue 06 aftercare live journey 另外驗證該投影與格式化金額。
- Commerce、Native migration tool、API 的 Release `dotnet build --no-restore`：各 PASS，0 warnings／0 errors。此驗收副本的 NuGet clean restore 因 vulnerability endpoint 不可用而失敗／等待；為完成驗收，使用相同候選來源的既有 build intermediates 執行 no-restore builds，故不宣稱 clean restore。
- `python3 scripts/m0/provision-issue06-fixture.py --port 14339`：建立一次性 loopback-only SQL Server 2025 dedicated fixture，strict TLS，執行 migrations 0001–0008 及完整 Commerce SQL harness；PASS。涵蓋退款額度原子保留、Unknown 保留額度、可信查詢只結算同一 operation 一次、DEMO 調整與退款分離／重試、唯讀人工核對差異／PII 排除與資料快照不變，以及授權／併發檢查。
- `python3 scripts/m0/run-issue06-aftercare-acceptance.py`：PASS。live API／worker／SQL／Storefront Playwright 1/1；合成 Owner 完成已付款訂單配送、退貨檢查與 mock 退款，顧客本人訂單頁顯示後端狀態、相同 TWD 金額與模擬聲明，且不顯示 operation ID。SQL 回讀確認訂單、配送、退貨及退款 operation；runner 清除合成業務資料。
- 初次增強瀏覽器斷言把 TWD 1880 當作無分隔字串，與 zh-TW 實際格式 `1,880` 不符；改採 `Intl.NumberFormat('zh-TW')` 後，完整 live journey 重跑通過。此為測試斷言格式修正，不是產品行為缺陷。
- Change Impact traversal 因既有 retrieval index stale 而回報 INCOMPLETE；人工檢視 runtime `CommerceRefundSummary` schema、Commerce `Aftercare.cs` Order projection 及 Storefront consumer。受影響範圍為讀取與顯示；schema／data／event／migration 無變更。完整圖追溯仍受 stale index 限制，證據記於 `.ai/runs/issue20-controlled-refunds/CHANGE_IMPACT.yaml`，此 AIPS 工作紀錄不納入產品提交。
- 專用驗收在 `/private/tmp/kohami-issue20-acceptance` 副本與 port 14339 執行；主 checkout 既有 `.local/m0` 未重設或遷移。專用容器、volumes、network 與暫存副本在完成後清理。
- 範圍限制：Provider／銀行對帳、真實資金移動、自動排程、正式發票／稅務判定及正式環境部署均未驗證；自動對帳排程維持選配。

## Issue 21 交易憑證與退款發票處理（2026-10-17）

狀態：PASS（本機 DEMO 憑證／退款調整展示與既有 SQL 行為證據）。Runtime 已在受控付款成功後建立無稅務效力 DEMO 憑證，退款成功後以獨立 worker 建立同額 DEMO 調整；顧客透過本人訂單的 `/receipts` 查詢。調整失敗／Unknown 不回滾退款，可重試同一調整而不重送退款。

- `npm run typecheck`（`apps/storefront`）：PASS。
- `npx playwright test tests/issue21-invoice-adjustments.spec.ts --config=playwright.config.ts --reporter=line --trace=off`（`apps/storefront`）：PASS，1/1。專屬合成路由驗證 API `/receipts` 呼叫、DEMO 憑證和調整狀態、同額 TWD 顯示及無稅務效力／非有效發票聲明。測試只 mock API，非 SQL-backed browser evidence。
- 本票先前 2026-10-15 的 dedicated `demo_m0` Commerce SQL runner 已記錄 49 項檢查 PASS：退款成功只建一筆調整、Unknown 與退款成功狀態分離、授權重試同一調整後顯示 `Adjusted` 且 `taxEffect=none`，不建立或重送退款。本次未重跑 SQL harness；較新的 Issue 20 全 Commerce SQL／live API 驗收亦含退款調整行為，詳見上節及 [Issue 21](../issues/kohami/21-invoice-and-refund-adjustments.md)。
- 本次僅新增 Storefront acceptance test 與文件狀態／證據；API、schema、資料、事件、worker、權限及 migration 未變更。AIPS traversal 有界且 graph consumer coverage 未完整；人工對照直接 endpoint、order-owner query、worker 與 Storefront consumer，紀錄於 `.ai/runs/issue21-invoice-refund-adjustments/CHANGE_IMPACT.yaml`。
- 範圍限制：DEMO 資料沒有稅務效力；正式發票／稅務規則、真實開票服務、provider／銀行及正式營運驗證均不在本次範圍。

## Issue 22 購物政策與客服入口（2026-10-17）

狀態：PASS（本機 mock 政策展示、客服入口與既有本人售後流程的 Storefront 驗收）。新增 `/policies` 及 `/support` 三語路由並在首頁提供入口；政策來源沿用 DEMO v0.2。客服只引導到既有本人訂單售後／退貨操作，沒有宣稱一般客服或資料權利案件 API 已實作。

- `npm run typecheck`（`apps/storefront`）：PASS。`npx playwright test tests/issue22-policies-support.spec.ts`（`apps/storefront`）：PASS，3/3。驗證 zh-TW／ja／en 政策章節、政策版本與 mock 聲明、三語客服入口、390px 手機寬度不橫向溢出、鍵盤焦點、支援入口到本人訂單售後、訂單／退貨權限拒絕的隱私安全提示、服務不可用提示、拒絕送出保留表單，以及成功 mock Return 後可看案件狀態與時間軸。
- 瀏覽器案例使用 Playwright route stub 回覆合成 API 結果；沒有啟動真 API／worker／SQL 或讀寫資料庫。Issue 19 既有專用 SQL／live journey 證據屬歷史證據，本次未重跑。
- Storefront 本次只改頁面／本人訂單售後錯誤顯示及 Playwright；沒有改 API、OpenAPI、SQL schema、授權或事件。政策沒有新增法律退貨期限、保存期限、商家資料、營業日日曆或 SLA；案件時點維持 UTC 與經過秒數。
- 一般詢問與資料權利案件送出／追蹤仍未實作；正式商家揭露、法定條款、真人客服／通知與正式 SLA 仍由 Issue 07／正式啟用審查或後續範圍處理。本地 mock 的政策頁不是正式營運條款。

## Issue 24 三語鍵盤略過導覽子範圍（2026-10-18）

Storefront 共用應用殼新增繁中、日文與英文略過導覽連結，九個頁面（首頁、商品、購物車、結帳、會員、政策、客服、後台商品目錄、後台報表）的 `<main>` 都提供 `main-content` 錨點及程式化焦點目標。沒有 API、SQL、交易、權限、OpenAPI 或事件變更。

- `npx playwright test tests/issue24-accessibility.spec.ts --config=playwright.config.ts --reporter=list`（`apps/storefront`）：先前版本依預期因找不到略過連結而 FAIL；實作後 PASS 1/1。驗證 390×844 viewports、三語文字、首次 Tab 焦點、Enter 後主內容焦點與片段、水平溢位上限。
- `npm run typecheck`（`apps/storefront`）：PASS。
- `npx playwright test tests/issue22-policies-support.spec.ts --config=playwright.config.ts --reporter=list`（`apps/storefront`）：PASS 3/3，三語政策／客服／本人售後路徑回歸通過。
- 使用本機短效 localhost HTTPS 憑證與 Chrome channel；測試頁面使用本機 Nuxt server，Issue 22 的 API 行為由合成 route stub 提供，沒有連接 API／SQL。此結果只證明 Chrome 瀏覽器自動化的局部鍵盤行為。
- 該略過導覽子範圍當時未完成全程人工可及性驗收、對比、200%／400% 縮放或完整首頁至售後旅程；效能亦未收集樣本。後續行動裝置人工驗收已移列[選配改善](../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。後續 Checkout 鍵盤與本機效能證據見下節；仍不能宣稱 Issue 24 或正式 SLA 通過。

### Issue 24 結帳狀態鍵盤恢復與本機效能樣本（2026-10-18）

- TDD 回歸案例先確認刷新按鈕原本的原生 `disabled` 會在非同步查詢時丟失鍵盤焦點。Checkout 結果頁改以 `aria-disabled` 與 `aria-busy` 標示查詢中，保留既有 `refreshing` 防重送守門；修正後 Playwright 能以 Tab／Enter 完成狀態更新且焦點維持在刷新按鈕。
- `npm run typecheck`（`apps/storefront`）：PASS。`npx playwright test tests/issue24-checkout-keyboard.spec.ts tests/issue13-trusted-payment-results.spec.ts tests/issue24-accessibility.spec.ts --config=playwright.config.ts --reporter=line --trace=off`：PASS 4/4，含 Processing／成功／拒絕／Unknown／503 錯誤恢復、舊快照保留與三語略過導覽。新測試僅使用 Playwright 合成 API route mock，未啟動 API／SQL、未連接付款服務。
- 本機首頁 30 次桌面導航樣本：Mac arm64、Chrome 155.0.8059.84、1280×800、本機 HTTPS、無網路／CPU 節流、合成 API mock。LCP P75 40 ms、CLS P75 0；對首頁的 Tab＋Enter 互動有 25/30 個 ≥16 ms Event Timing 樣本，該子集 P75 24 ms。Event Timing 設有 16 ms 門檻，不包含所有較快事件，故不把此值宣稱為完整 INP；測量也不是真實網路、行動裝置或全核心頁面證據。
- 當時行動裝置實機人工驗收未執行，後依範圍決策移至[選配改善](../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)；沒有安裝／信任 CA。完整桌面核心旅程可及性、縮放／對比、多頁效能樣本及正式 SLA 仍未驗，Issue24 尚未結案。

### Issue24 桌面鍵盤旅程與行動裝置驗收範圍決策（2026-10-18）

- 使用者決定先排除 iPhone 實機與 VoiceOver 人工驗收，若日後有優化需要再依選配計畫處理。共用 M4-AC04、CHK-05 與 Q-04 已對齊：桌面鍵盤、responsive viewport、焦點／縮放等現行範圍維持；行動裝置實機人工驗收不阻擋本機核心結案。
- 新增 `apps/storefront/tests/issue24-core-keyboard-journey.spec.ts`，以鍵盤操作略過導覽、商品搜尋、SKU 選擇、加入購物車、試算報價及建立合成結帳；只使用 Playwright 合成 API route stub，不連接 API／SQL 或付款服務。該案例 PASS 1/1；Issue24／Issue13 合併回歸 PASS 5/5；`npm run typecheck` PASS。
- 桌面本機診斷首頁、政策、購物車、結帳各 30 次：Mac arm64、Chrome 155.0.8059.84、1280×800、本機 HTTPS、暖快取、無節流及合成 API route stub；四頁各 30/30 LCP／CLS 樣本，LCP P75 均 24 ms、CLS P75 均 0。沒有量 INP，非真網路、API／SQL、行動尺寸或正式 SLA 證據。
- 此決策只調整驗收範圍，不代表完整 WCAG AA、行動瀏覽器或正式效能 SLA 已通過。

### Issue24 桌面鍵盤失敗恢復與本人售後（2026-10-18）

- `apps/storefront/tests/issue24-keyboard-error-aftercare.spec.ts` 新增 2 個合成 API route Playwright 案例：報價／建立訂單 503 與成功重試；本人訂單查詢 404／503 安全失敗、值保留，以及退貨申請 503／成功重試。
- 首次紅燈發現報價按鈕在請求期間被原生 `disabled`，失敗後焦點消失，鍵盤無法直接重試。報價、建立訂單、退貨申請改用 `aria-disabled`／`aria-busy` 保持焦點，handler 以 `pending` 守門阻擋重複提交。退貨失敗保留欄位與焦點。
- `npm run typecheck`（Storefront）：PASS。合併執行新案例、Issue24 核心旅程／checkout／skip navigation 與 Issue22 policies/support：Playwright 8/8 PASS。測試只使用合成路由；Nuxt 綁定 localhost HTTPS，不啟 API、不連 SQL／付款，也未開 LAN。
- 限制：未驗搜尋或加車失敗、所有退貨後續狀態、真 API／SQL、iPhone／VoiceOver、對比／縮放、INP 或 Q-01 冷熱場景。此證據不代表 Issue24 完成、完整 WCAG AA 或正式 SLA。

### Issue24 桌面三語核心頁冷／暖快取效能診斷（2026-10-18）

- 環境：Mac arm64、Headless Chrome 155、1440×900、本機 Nuxt HTTPS、無 CPU／網路節流；未啟動 API／SQL。測量繁中／日文／英文首頁、政策與購物車，每個語系／頁面 30 次，冷快取 15 次、暖快取 15 次。每筆冷快取導覽前以 Chrome DevTools Protocol 清除 browser cache；暖快取沿用同一 context。頁面完成載入後點擊標題一次。
- LCP 與 CLS 九組皆有 30/30 樣本。各組 LCP P75 範圍 20–84 ms，CLS P75 範圍 0–0.0121；這些本機展示環境數值低於 Q-01 規劃目標，但不代表正式網路或實際使用者 SLA。
- Event Timing 使用 16 ms `durationThreshold`；270 次標題點擊中有 251 次可觀測候選，該子集 P75 為 16 ms。事件門檻排除較快互動，且每頁只操作標題一次，因此不視為完整 INP。
- 另以 Headless Chrome 模擬 390×844 CSS viewport、deviceScaleFactor 1 與 touch；三語首頁、政策及購物車各 30 次，冷／暖快取各 15 次。九組 LCP P75 範圍 28–84 ms、CLS P75 均為 0，Event Timing 點擊候選 270/270、子集 P75 32 ms；水平溢位皆 0/30。這是響應式 viewport 自動診斷，非 iPhone 實機驗收。
- 未量 API／SQL 或購物操作互動、對比、200%／400% 縮放；Event Timing 不代表完整 INP。iPhone Safari／VoiceOver 依目前決策留在[選配改善](../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。Issue24 仍為 claimed，完整 Q-01 與人工可及性驗收尚未完成。

### Issue24 桌面重排、鍵盤搜尋／加車錯誤恢復與對比抽樣（2026-10-18）

- 720px viewport 首次測到首頁水平溢位，三語文件寬為 830／830／903px。`ProductDiscovery.vue` 篩選列斷點由 700px 調至 900px；Playwright 回歸在繁中／日文／英文各驗 720px 與 320px，六個 viewport 均無水平溢位。這代表等效 CSS viewport 重排檢查，並非實際瀏覽器縮放操作。
- 新增 `apps/storefront/tests/issue24-accessibility.spec.ts` 的搜尋／加車合成 503 測試：搜尋與加車均可鍵盤重試；加車等待／失敗期間焦點保留在按鈕，pending guard 阻止重複 mutation。`npm run typecheck` PASS；指定 Issue22／Issue24 Playwright 合併回歸 PASS 10/10，命令見 Issue24 票券。所有 API 使用合成 route stub，Nuxt 綁 localhost；未連 API／SQL／付款或 LAN。
- Headless Chrome 對三語首頁／政策／購物車的可見文字抽樣共 187 節點，未發現低於一般文字 4.5:1／大字 3:1 門檻者。此腳本只計算取得的 computed foreground/background，不涵蓋所有狀態、焦點指示、非文字對比或完整人工 WCAG AA 審查。
- Event Timing 對三語首頁搜尋、政策略過導覽、購物車數量變更各做 30 次互動嘗試。觀測到 ≥16ms 候選的嘗試為 zh-TW 13／29／0、ja 1／28／6、en 4／27／3（依序為首頁／政策／購物車）；合計 111／270 次嘗試，193 個不同候選互動，已觀測分組的樣本最大延遲 P75 為 16ms。低於 16ms 的事件未被 API 暴露，故候選值不是完整 INP。
- 後續在桌面 Chrome `/zh-TW/policies` 以 200%／400% 原生縮放人工複核：政策文字仍可閱讀與垂直捲動，當次未見水平捲軸；略過導覽、商店、客服與語言連結的焦點外框可見。限定色值計算樣本為正文／背景 14.36:1、次要文字／背景 6.21:1、導覽連結／頁面背景 9.68:1、焦點色／頁面背景 6.49:1。這只完成政策頁的桌面抽樣，不是全站縮放／對比或 WCAG 稽核。
- Issue24 維持 `claimed`。完整 INP、全站焦點／非文字對比、真實網路／硬體矩陣仍未完成；iPhone Safari／VoiceOver 維持[選配改善](../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### Issue 24 購物車、結帳與會員訂單縮放等效重排（2026-10-18）

- `npx playwright test tests/issue24-zoom-core-pages.spec.ts --config=/private/tmp/kohami-issue24-zoom.playwright.config.mjs --reporter=line --trace=off`：PASS 1/1。使用 synthetic API route stubs，在 640px／320px CSS viewport（1280px 桌面基準的 200%／400% 等效寬度）檢查購物車、結帳結果、登入會員訂單的主要內容／控制項及水平溢位。Nuxt 綁 localhost HTTPS；沒有 API／SQL／付款服務或 persistent fixture 存取。
- Red 階段在結帳結果 320px 重現 `document.scrollWidth=342px`。付款期限時間戳推高摘要 Grid 欄位最小寬；加入 `dd { min-width: 0; overflow-wrap: anywhere; }` 後三頁在兩個 viewport 均無水平溢位。`npm run typecheck` PASS。
- 此案例使用暫存 Playwright 設定與本機 Headless Chromium；專案既有 Chrome channel 在此環境無法啟動。這是縮小 CSS viewport 的自動化等效重排證據，並非三頁桌面 Chrome 原生 200%／400% 人工檢查。原生縮放人工證據仍只涵蓋 `/zh-TW/policies`。
- 沒有 API、SQL、交易、權限、OpenAPI、schema 或事件變更。Issue 24 仍未結案；原生全站縮放／對比、完整 INP 與效能情境待驗，iPhone Safari／VoiceOver 實機驗收維持選配。

### Issue24 桌面 Chrome 原生縮放窄視窗補強（2026-10-18）

- 桌面 Chrome 200% 檢視購物車、結帳結果與會員訂單時主要內容可讀；400% 下目前視窗有效版面窄於 320 CSS px，初見會員訂單標題／訂單編號被擠窄。該視窗不等同 1280px 基準下標準 320px 的 400% CSS viewport 等效檢查。
- 三頁在 ≤240px 採堆疊式排列並允許長訂單字串折行；`issue24-zoom-core-pages.spec.ts` 保留 640px／320px，並補 160px viewport。以 headed Chrome 執行 `npx playwright test tests/issue24-zoom-core-pages.spec.ts --config=playwright.config.ts --headed --reporter=line --trace=off`，PASS 1/1；三頁在 640／320／160 CSS px 均通過主要內容／控制項可見及文件／body 無水平溢位斷言。所有 API 回覆為 synthetic route stub，未連 API／SQL／付款或 LAN。
- 首次 headed Playwright 執行發現測試把 `documentElement.clientWidth` 當成設定 viewport，Chrome 預留垂直捲軸後出現 `innerWidth=640`、`clientWidth=625`；斷言改為以 `window.innerWidth` 核對設定寬度、以 `clientWidth` 驗證溢位邊界後重跑通過。
- 160px 是補充 CSS viewport 證據。後續改用本機 HTTP 預覽與獨立 synthetic API fixture，避免略過本機 HTTPS 憑證警告；購物車原生 400% 實測發現文字輸入框約 194 CSS px，溢出約 120 CSS px 的父欄，文件寬度為 214px、可視寬度為 152px。
- 購物車文字輸入框／選單設為父欄寬並允許縮窄後，以原生 Chrome 400% 復測：`innerWidth=156`、`devicePixelRatio=8`、`clientWidth=152`、文件 `scrollWidth=152`、body `scrollWidth=136`，控制項皆在父欄內。三頁 640／320／160 CSS px headed Playwright 回歸 PASS 1/1，`npm run build` PASS；結帳與會員訂單原生 400% `scrollWidth` 仍未逐頁量測。
- 僅涉及展示版 CSS、測試與紀錄。Issue24 仍為 `claimed`；全站原生縮放／對比及 iPhone／VoiceOver 未完成，後者維持[選配改善](../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### Issue24 共用 SSR／瀏覽器 fixture 與 ja/en 鍵盤回歸（2026-10-18）

- 新增 `apps/storefront/tests/fixtures/issue24-api-fixture.json` 與 loopback-only in-memory API server；Nuxt SSR 與瀏覽器請求使用相同商品 fixture。專用 `playwright.issue24-fixture.config.ts` 啟動 fixture，預設 Playwright config 排除這兩個必須使用該 fixture 的測試。
- `npx playwright test --config=playwright.issue24-fixture.config.ts --reporter=line --trace=off`：PASS 2/2。CLS 案例確認 zh-TW／ja／en SSR HTML 含對應商品名稱、hydration 無 mismatch 訊息、CLS ≤ 0.1 且購物車區位移 ≤ 100px；鍵盤案例確認 ja/en 可用鍵盤搜尋、選擇 SKU 並加入購物車。
- `npm run typecheck`、`node --check tests/fixtures/issue24-api-server.mjs` 與 `git diff --check`：PASS。此執行重用既有 localhost Nuxt preview；本輪未停止或覆蓋該服務。fixture API 僅綁 `127.0.0.1`，不連 SQL／真實 API／付款服務或 LAN。
- 此為 SSR／瀏覽器共享合成資料的 UI 證據，不含真 API／SQL 整合、完整鍵盤路徑、完整 WCAG 或正式 SLA。Issue24 仍 `claimed`；iPhone Safari／VoiceOver 仍列為選配。

### Issue24 桌面錯誤恢復回歸與三語核心頁效能證據複核（2026-10-18）

- 以目前工作樹 Nuxt HTTPS 預覽（localhost:3001）重跑 `tests/issue24-keyboard-error-aftercare.spec.ts`、`tests/issue24-core-keyboard-journey.spec.ts`、`tests/issue24-checkout-keyboard.spec.ts`、`tests/issue24-focus-stop-sweep.spec.ts` 與 `tests/issue24-accessibility.spec.ts`：PASS 12/12；`npm run typecheck`：PASS。初次誤用預設設定連到現存 HTTP localhost:3000，12 個案例都在頁面載入前因 `ERR_SSL_PROTOCOL_ERROR` 失敗；切換至獨立 HTTPS 埠後重跑通過，未停止或覆蓋既有 3000 埠服務。
- 本次 route-mock 鍵盤套件在 Nuxt 開發伺服器輸出 hydration node mismatch 警告：SSR 沒有該瀏覽器端 route stub 的商品回應，而 client mock 有商品。這不影響本次鍵盤互動斷言；SSR／client 同資料一致性由前述 loopback 共用 API fixture 專用套件另外驗證，PASS 2/2 且無 hydration mismatch。兩種證據不可互相替代。
- 三語核心頁效能使用既有固定節流樣本複核，未於本次重測：Chrome 155／macOS arm64／1440×900、CPU 4× slowdown、150ms latency、1.6Mbps 下行／750Kbps 上行、合成 API 固定 120ms；首頁、政策、購物車各語系每組 30 次，共 270 次暖資產快取導覽，全部 HTTP 200 並收齊 LCP／CLS。九組 LCP P75 為 216–244ms，CLS P75 為 0–0.0043，低於工程目標 LCP 2.5 秒／CLS 0.1。Event Timing 另設 16ms 門檻，不足以代表完整 INP；這是本機合成診斷，不是實際使用者 Core Web Vitals 或正式 SLA。原始資料位於未提交的 `.ai/runs/issue24-fixed-throttle-vitals-20261018/metrics.json`。
- 所有本次 Playwright API 回覆均為合成資料；服務只綁 localhost，沒有 API／SQL／付款服務或 LAN。Issue24 仍為 `claimed`；完整 INP、全站焦點／非文字對比及正式效能驗收未完成。iPhone Safari／VoiceOver 仍依決策列為選配。

### Issue24 動態鍵盤焦點指示對比斷言（2026-10-18）

- `apps/storefront/tests/issue24-focus-stop-sweep.spec.ts` 增加鍵盤焦點 outline 與其鄰接不透明背景的 3:1 非文字對比斷言，並掛入既有 zh-TW／ja／en 焦點停靠點掃描，以及日英購物車數量更新、報價失敗／重試與結帳刷新失敗後的焦點停點。屬測試覆蓋補強，未修改產品 CSS 或行為。
- `npm run typecheck` PASS；`npx playwright test tests/issue24-focus-stop-sweep.spec.ts --config=playwright.config.ts --list` 列出 3 個案例。實際瀏覽器執行使用暫時 HTTPS config 和 synthetic route stubs，但 Nuxt 啟動後 Playwright 仍無法連 loopback readiness URL，120 秒後逾時，未進入案例；故不記測試 PASS，也沒有新增動態狀態實測 ratio。臨時 config 已移除，loopback listener 已停止。
- 先前已記錄的全站焦點抽樣與卡片／按鈕色值仍是獨立歷史證據。此補強需在 loopback 可連通環境重跑；Issue24 維持 `claimed`，完整 WCAG 2.2 AA、全站非文字對比、INP 與正式 SLA 未完成。

### Issue24 桌面動態焦點停點非文字對比重跑（2026-10-18）

- 使用臨時 Playwright 設定指向僅綁 `127.0.0.1:3001` 的本機 HTTPS Nuxt preview，Desktop Chrome 155.0.8059.84 執行 `npx playwright test tests/issue24-focus-stop-sweep.spec.ts --config=/private/tmp/issue24-focus-run.config.ts --reporter=line --trace=off`：PASS 3/3。
- 測試巡檢的三組桌面鍵盤停點均通過可見焦點樣式、可計算 outline／背景顏色，以及非文字對比至少 3:1 斷言。測試輸出未保存逐停點比例值，因此只記 PASS，不推列實際 ratio。API 全由合成 route fixture 回覆；沒有連 API、SQL、付款服務或 LAN。臨時設定與輸出已清除。
- 這是桌面 Chrome 自動化的抽樣結果，不代表所有路由／控制項／狀態的全站對比、完整 WCAG 2.2 AA、完整 INP 或正式 SLA；Issue24 維持 `claimed`。

### Issue24 桌面全三語鍵盤焦點與非文字對比巡檢（2026-10-19）

- 使用桌面 Chrome 155.0.8059.84、1280×900、本機 preview 與合成 API 回應，檢視 zh-TW／ja／en 共 24 個路由狀態，含商品選色後狀態。完整順序 Tab 巡檢共 219 個焦點停點，未發現不可見焦點或停點順序失敗；略過導覽連結與進入主要內容的焦點也逐頁確認。
- 非文字控制項抽樣涵蓋 57 個可見且啟用的控制項實例。排除透明填色及停用控制項等初始掃描誤判後，未發現控制項填色／邊界低於 3:1；商品顏色與尺寸選單啟用後邊界約 4.62:1。此為抽樣檢查，並非所有組件、狀態與焦點外觀的完整色彩稽核。
- `npm run build` 與 `npm run typecheck`：PASS。驗收使用本機 synthetic preview，未連接 API／SQL／付款或 LAN；未保留原始輸出或 trace。
- Issue24 維持 `claimed`。本結果不代表完整 WCAG 2.2 AA、所有路由與動態狀態、INP 或正式 SLA 已通過；iPhone Safari／VoiceOver 仍未驗證並列於[選配改善](../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### Issue24 桌面 Chrome 首頁、政策與客服原生縮放抽樣（2026-10-19）

- 使用桌面 Chrome 155.0.8059.84、本機合成展示預覽，人工檢視繁中首頁、政策與客服頁在原生 200%／400% 縮放下的可讀性。三頁文字均可閱讀並可垂直捲動；首頁導覽、商品展示區與搜尋欄在窄版面仍可見。當次畫面未見明顯水平裁切或水平捲軸。
- 本次是有限的畫面人工抽樣，未量測 `scrollWidth`／`clientWidth`，未逐一操作首頁商品或購物流程；首頁商品與購物車服務當時不可用。因此不把服務互動、所有路由、全站無水平溢位或 WCAG 符合性列為通過。
- 預覽僅供本機展示，未連 API、SQL、付款服務或 LAN。Issue24 維持 `claimed`；iPhone Safari／VoiceOver 依目前決策排除於本輪並列在[選配改善](../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### Issue24 縮放等效版面購物車鍵盤操作（2026-10-19）

- 新增 `apps/storefront/tests/issue24-zoom-cart-interaction.spec.ts`：桌面 Chrome 於 640px／320px CSS viewport 下以鍵盤修改商品數量，確認合成購物車小計更新；依 Tab 順序到達移除、優惠碼、交付方式與重新試算控制，以 Enter 提交試算並讀取合成報價。兩種 viewport 的 document／body 均無水平溢位。
- `npx playwright test tests/issue24-zoom-core-pages.spec.ts tests/issue24-zoom-cart-interaction.spec.ts tests/issue24-focus-stop-sweep.spec.ts --config=playwright.config.ts --headed --reporter=line --trace=off`：PASS 5/5；`npm run typecheck`：PASS。一次 sandbox 內 Chrome 啟動因 Crashpad 權限 SIGABRT，於隔離執行環境重跑成功。最初新案例的 response waiter 在輸入事件後才設置而漏接回應，改以畫面上的合成狀態與版面指標作斷言後通過；沒有修改產品程式碼。
- Nuxt preview 暫時綁定 localhost，API 僅由測試 route stub 回覆；未連 API／SQL／付款服務、持久 fixture 或 LAN。640／320px 是 1280px 桌面基準的 200%／400% 重排等效 viewport，不能當作原生 Chrome 縮放人工測量。證據限購物車數量與費用試算鍵盤操作；Issue24 仍 `claimed`，不代表所有核心流程、完整 WCAG 2.2 AA 或正式 SLA。iPhone Safari／VoiceOver 維持未驗證並列於[選配改善](../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### Issue24 桌面 Chrome 原生 200%／400% 購物車鍵盤驗收（2026-10-19）

- 在桌面 Chrome 本機合成展示頁檢查原生縮放指示 200% 與 400%。購物車數量使用鍵盤輸入由 2 改為 3、再由 3 改為 4，分別看到小計 TWD 3,000／4,000；Tab 順序可到達移除商品、優惠碼、交付方式與重新試算，於兩種縮放按 Enter 均取得合成報價，總額分別為 TWD 3,060／4,060。
- 400% 畫面以單欄呈現購物車與報價控制，文字可辨識並可垂直捲動；截圖所見範圍未見明顯水平裁切。沒有量測 `scrollWidth`／`clientWidth`，因此不記錄為精確無溢位證據，也不外推至其他路由或 WCAG 符合性。
- 瀏覽器透過 HTTPS localhost 預覽呼叫只綁定 `127.0.0.1` 的臨時記憶體合成 API；未使用專案 API、SQL、付款服務、持久 fixture 或 LAN，未改產品程式碼。兩項服務於驗收後已停止，Chrome 縮放還原至原先 110%。Issue24 維持 `claimed`；iPhone Safari／VoiceOver 仍未驗證並列於選配改善。

### Issue24 三語商品、結帳結果與會員訂單頁 LCP／CLS 量測（2026-10-19）

本次補足既有三語首頁／政策／購物車頁面診斷未涵蓋的商品詳情、結帳結果與會員訂單頁。使用 Desktop Chrome 155.0.8059.84、macOS arm64、1440×900 CSS px、CPU 4×、150ms 網路延遲、下載 1.6Mbps／上傳 750Kbps，以及每個合成 API 回應固定 120ms。每個語系／頁面組合 30 次導覽，冷快取 15 次（新 browser context 並停用快取），暖快取 15 次（獨立暖身後共用 browser context）；合計 270 次。全部 HTTP 200，page error 0。

| 語系／頁面 | 樣本（冷／暖） | LCP P50 (ms) | LCP P75 (ms) | LCP 最大 (ms) | CLS P75 | CLS 最大 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 繁中／商品詳情 | 30（15／15） | 244 | 264 | 1,588 | 0.0036 | 0.0036 |
| 繁中／結帳結果 | 30（15／15） | 216 | 232 | 372 | 0 | 0 |
| 繁中／會員訂單 | 30（15／15） | 444 | 1,808 | 1,832 | 0.0274 | 0.0274 |
| 日文／商品詳情 | 30（15／15） | 216 | 228 | 284 | 0.0028 | 0.0028 |
| 日文／結帳結果 | 30（15／15） | 216 | 232 | 244 | 0 | 0 |
| 日文／會員訂單 | 30（15／15） | 212 | 440 | 444 | 0.0305 | 0.0305 |
| 英文／商品詳情 | 30（15／15） | 220 | 228 | 236 | 0.0028 | 0.0028 |
| 英文／結帳結果 | 30（15／15） | 212 | 232 | 244 | 0 | 0 |
| 英文／會員訂單 | 30（15／15） | 216 | 444 | 448 | 0.0222 | 0.0222 |

- LCP P75 範圍 228–1,808ms，CLS P75 範圍 0–0.0305，均低於 Q-01 規劃目標 LCP 2.5 秒／CLS 0.1。繁中會員訂單頁初測冷快取 P75 1,812ms、暖快取 432ms；日文冷快取 P75 200ms、暖快取 444ms；英文冷快取 212ms、暖快取 444ms。後續重測確認繁中冷載入差異可重現，尚未定位原因，不能據此推斷真實使用者體驗或產品缺陷。
- Smoke `node /private/tmp/issue24-core-vitals-smoke.mjs`：PASS，9/9 頁面回應 200 並呈現對應語系內容。Benchmark `node --check /private/tmp/issue24-core-vitals-benchmark.mjs`：PASS；`node /private/tmp/issue24-core-vitals-benchmark.mjs`：PASS，9 組各 30 筆，共 270 筆，無 page error。
- 使用既有 production build 與本機僅綁 `127.0.0.1` 的合成 API fixture；未連真實 API、SQL、付款服務或 LAN。原始資料留在本機 `/private/tmp/issue24-core-vitals-20261019.json`。這是桌面實驗室 LCP／CLS 診斷，不是 field Core Web Vitals、完整 INP、真實網路／服務、正式 SLA 或手機驗收。Issue24 維持 `claimed`，iPhone Safari／VoiceOver 依使用者決策排除於本輪。

### Issue24 會員訂單頁冷／暖快取差異重測（2026-10-19）

- 延續前一批量測設定，只重跑 zh-TW／ja／en `/account`，每語系冷快取 30 次、暖快取 30 次，總計 180 次；Chrome 155.0.8059.84、macOS arm64、1440×900、CPU 4×、150ms 延遲／1.6Mbps 下行／750Kbps 上行、合成 API 固定 120ms。新 context 並停用快取作冷樣本；暖樣本共用 context 並先暖身一次。LCP 取 PerformanceObserver 最大候選，CLS 依排除 `hadRecentInput` 的 1 秒間隔／5 秒 session window 計算；頁面 networkidle、字型完成後觀察 500ms。

| 語系／快取（各 n=30） | LCP P50 (ms) | LCP P75 (ms) | LCP 最大 (ms) | CLS P75 | >1,000ms LCP |
| --- | ---: | ---: | ---: | ---: | ---: |
| 繁中／冷 | 1,816 | 1,856 | 2,732 | 0.0274 | 30 |
| 繁中／暖 | 488 | 572 | 4,416 | 0.0222 | 3 |
| 日文／冷 | 196 | 208 | 1,356 | 0.0305 | 1 |
| 日文／暖 | 424 | 432 | 444 | 0.0305 | 0 |
| 英文／冷 | 200 | 208 | 220 | 0.0222 | 0 |
| 英文／暖 | 432 | 436 | 448 | 0.0222 | 0 |

- 繁中冷載入超過 1 秒為 30/30，暖載入 3/30；日文冷載入 1/30，英文冷載入 0/30；所有樣本 HTTP 200，page error 0。此結果顯示相同本機合成條件下的 locale/cache 差異可重現；本輪沒有收集 LCP element attribution 或 resource waterfall，仍無法定位原因。
- 第二批原始樣本在本機 `/private/tmp/issue24-member-orders-repeat-20261019.json`，未提交。只使用本機 loopback preview 與合成 API fixture，結束後已停止；未連 API／SQL／付款或 LAN。結果是桌面 lab LCP／CLS 診斷，不是 field Core Web Vitals、完整 INP、正式 SLA 或手機驗收。Issue24 維持 `claimed`。

### Issue24 桌面 Chrome 會員訂單頁快取命中與 LCP 候選元素（2026-10-19）

- 在桌面 Chrome 155.0.8059.84／macOS arm64、1440×900 CSS px、CPU 4×、150ms latency／1.6Mbps 下行／750Kbps 上行下，對本機 production build 的三語 `/account` 各測冷／暖快取 10 次，共 60 次；每個合成 API 請求固定延遲 120ms。冷樣本使用新 browser context 並停用快取；暖樣本共用 context 並先暖身一次。Chrome DevTools Protocol 同時記錄 `Network.requestServedFromCache` 與 `Network.responseReceived.fromDiskCache`。

| 語系 | 冷 LCP P75 | 冷 `/_nuxt` 命中 | 暖 LCP P75 | 暖 `/_nuxt` 命中 | LCP 候選（冷／暖） |
| --- | ---: | ---: | ---: | ---: | --- |
| zh-TW | 1,768 ms | 0/190 | 384 ms | 180/180 | `h2#claim-heading`／同左 |
| ja | 204 ms | 0/190 | 384 ms | 180/180 | `h1`／`h2#claim-heading` |
| en | 200 ms | 0/190 | 380 ms | 180/180 | `h1`／`h2#claim-heading` |

- 每語系暖快取組的 180/180 個 Nuxt 資產請求均有 CDP cache-hit 證據。API 使用頁面初始化 `fetch` wrapper 提供合成會員與單筆合成訂單；不使用 Playwright route interception，避免干擾瀏覽器 HTTP cache。先前 route interception 樣本因未保留快取行為而排除。60 次導覽均 HTTP 200、page error 0。腳本 `node --check /private/tmp/issue24-member-orders-cache-attribution-fetchfixture-20261019.mjs`：PASS；正式樣本命令 `node /private/tmp/issue24-member-orders-cache-attribution-fetchfixture-20261019.mjs`：PASS，60/60 導覽完成。
- 原始結果為本機 `/private/tmp/issue24-member-orders-cache-attribution-fetchfixture-20261019.json`，未提交。此為 synthetic desktop lab 診斷，資產 cache-hit 不代表 API 快取；不涵蓋真實帳號／服務、field Core Web Vitals、完整 INP、手機／VoiceOver 或正式 SLA。LCP 候選觀察仍不足以定位繁中冷載入差異，未因此更動產品程式碼；Issue24 維持 `claimed`。

### Issue24 桌面 Chrome 會員訂單頁 LCP 元素與資源瀑布歸因（2026-10-19）

- 使用既有 production build 與合成會員／單筆訂單 API fixture，對 zh-TW／ja／en `/account` 各做 3 次冷載入及 3 次暖載入，共 18 次。條件為 Chrome 155.0.8059.84、macOS arm64、1440×900 CSS px、CPU 4×、150ms 延遲／1.6Mbps 下行／750Kbps 上行、API mock 120ms；量測 LCP candidate 面積／出現時間及資源請求、回應時點。

| 語系 | 冷 LCP 中位數 | 首標題候選面積 | `claim-heading` 出現中位數 | 冷載入 LCP 候選 |
| --- | ---: | ---: | ---: | --- |
| zh-TW | 1,776 ms | 5,850 px² | 1,768 ms | `h2#claim-heading`，8,052 px² |
| ja | 196 ms | 7,136 px² | 1,757 ms | `h1`，7,136 px² |
| en | 192 ms | 5,957 px² | 1,756 ms | `h1`，5,957 px² |

- 三語請求相同 Nuxt 資產，主要 bundle 回應時序相近（83.6 KB bundle 約於 1,408–1,422 ms 完成）；未發現 zh-TW 專屬慢資源。歸戶區塊在三語都約 1.76 秒出現，繁中候選區塊面積較大，因而取代首標題成為 LCP。此前「尚未定位原因」的觀察已由本次元素歸因補充；結果僅解釋合成頁面候選元素差異，不代表正式使用者效能缺陷或真實網路行為。
- 三語暖載入 LCP 中位數 zh-TW／ja／en 為 384／380／380 ms；18 次導覽均 HTTP 200、page error 0。腳本語法檢查 PASS，18/18 導覽完成。原始結果與腳本留在本機 `/private/tmp/issue24-member-orders-waterfall-final-20261019.json`、`/private/tmp/issue24-member-orders-waterfall-final-20261019.mjs`，未提交。只使用 localhost 與合成資料，量測後已停止；未連真實 API／SQL／付款或 LAN。這是桌面實驗室診斷，不是 field Core Web Vitals、完整 INP、正式 SLA 或手機驗收；Issue24 維持 `claimed`。

### Issue24 三語桌面核心互動 INP 實驗室估算（2026-10-19）

- 使用 production build preview，Chrome 155.0.8059.84、macOS arm64、1440×900；CPU 4× slowdown、150 ms 網路延遲、1.6 Mbps 下行／750 Kbps 上行，合成 API 每次固定延遲 120 ms。服務只綁 `127.0.0.1`，全程使用 Playwright 合成回應，不連真實 API、SQL、付款服務或 LAN。
- zh-TW／ja／en 各執行 30 趟完整頁面互動旅程，每趟涵蓋 15 個成功／錯誤恢復互動。Event Timing observer 只保留 duration ≥16 ms 的項目，按 `interactionId` 分組，以每趟最慢的已觀測互動作為 visit estimate，再計算 30 趟 P75。每語系各收集 1,260 筆 Event Timing entries；每趟均有至少一筆符合門檻的互動紀錄。

| 語系 | Visit INP estimate P75 | 30 趟中最大估算值 |
| --- | ---: | ---: |
| 繁中（zh-TW） | 40 ms | 56 ms |
| 日文（ja） | 40 ms | 48 ms |
| 英文（en） | 40 ms | 48 ms |

- 三語估算 P75 均低於 Q-01 的 200 ms 規劃目標。這是本機合成實驗室估算，不是 field INP、正式 Core Web Vitals、SLA 或手機驗收；16 ms 門檻會排除較短事件，腳本也不代表使用者所有互動與裝置／網路。原始逐筆資料與臨時量測程式未保留，因此僅記錄彙總值及上述方法，不推論未觀測互動。
- 本項只補充 Issue24 的桌面效能證據，不改變產品行為或驗收狀態；Issue24 維持 `claimed`。iPhone Safari／VoiceOver 依目前決策排除於本輪並列於[選配改善](../planning/kohami/options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)。

### Issue24 Event Timing 門檻下的三語短互動代理量測（2026-10-19）

- 使用 production build preview 與合成 Playwright API route，preview 僅綁 `127.0.0.1:3000`。Playwright Chromium 145.0.7632.6、macOS arm64、1440×900、CPU 4×、網路延遲 150ms／下行 1.6Mbps／上行 750Kbps、合成 API 固定 120ms。zh-TW／ja／en 各跑 30 趟，每趟 15 個成功／錯誤恢復操作；三語共 90 趟、1,350 個 action-to-settled-UI 樣本。

| 語系 | rAF 代理事件數 | 低於 16ms | 代理事件 P50／P75／最大 | 每趟最大代理值 P75／最大 | Event Timing entries（16ms 門檻） |
| --- | ---: | ---: | ---: | ---: | ---: |
| zh-TW | 660 | 660 | 3.7／4.7／14.566 ms | 11.866／14.566 ms | 399 |
| ja | 660 | 660 | 3.6／4.7／12.832 ms | 10.866／12.832 ms | 321 |
| en | 660 | 660 | 3.9／4.8／14.866 ms | 10.966／14.866 ms | 187 |

- 逐事件代理量測以 `Event.timeStamp` 到事件後第一個 `requestAnimationFrame` callback 的間隔計算，原始資料含事件類型、時間戳、旅程與操作歸屬；逐趟摘要取該旅程最大觀測代理值。所有旅程都有代理事件，三語每趟都至少收集一筆 <16ms 事件。Event Timing 僅產生符合其 16ms observer 門檻的項目；本次兩組觀測不是一對一事件映射，因此差額不應解讀為被漏掉的 INP 數值。第一個 rAF callback 不是繪製完成證據，這項代理不能取代 INP，也不能拿來宣稱符合 200ms INP 目標。前輪 Chrome 155 的 visit INP P75 40ms 與本輪 Chromium 145 的代理統計量測不同，不能直接視為前後改善。
- `node --check apps/storefront/scripts/issue24-interaction-latency.mjs`：PASS；`npm run build`：PASS；三語量測命令 `ISSUE24_SAMPLES=30 ISSUE24_LOCALES=zh-TW,ja,en node scripts/issue24-interaction-latency.mjs`：PASS，90/90 旅程、15 類 action 每類每語 n=30。原始 1,980 筆代理事件及逐趟摘要保留於未提交的 `.ai/runs/issue24-sub16ms-proxy/metrics.json`。無真實 API／SQL／付款／LAN；不代表 field INP、正式 SLA、手機或 VoiceOver 驗收。

### Issue24 三語桌面 Event Timing 逐趟最大值追溯量測（2026-10-19）

- 使用 production build preview、Google Chrome 155.0.8059.84、macOS arm64、1440×900；CPU 4× slowdown、150ms 網路延遲、1.6Mbps 下行／750Kbps 上行、合成 API 固定 120ms。preview 僅綁 `127.0.0.1:3000`。
- zh-TW／ja／en 各跑 30 趟；每趟執行 15 類成功／錯誤恢復操作，每類 n=30，共 90 趟。Event Timing observer 設 `durationThreshold: 16`；同一 `interactionId` 取最長 entry 作互動候選，每趟取所有已觀測候選最大值，再以 30 趟計 P75。

| 語系 | interactionId entry 數 | 唯一互動數 | 逐趟最大候選 P75 | 逐趟最大值 |
| --- | ---: | ---: | ---: | ---: |
| 繁中（zh-TW） | 1,260 | 420 | 40 ms | 40 ms |
| 日文（ja） | 1,260 | 420 | 32 ms | 40 ms |
| 英文（en） | 1,257 | 419 | 32 ms | 40 ms |

- 三語 30/30 趟均至少有一筆 ≥16ms Event Timing 候選，觀測到的逐趟最大值均低於 Q-01 的 200ms 規劃門檻。此數據是腳本化合成互動的實驗室 Event Timing visit-max estimate，不是 field INP、正式 Core Web Vitals 或 SLA；不要據此標記 Q-01 正式通過。Google 的[Web Vitals 量測指引](https://web.dev/articles/vitals-measurement-getting-started)將實驗室與 field 資料分開，標準 lab INP 不可由無互動的 lab run 代替，並建議以 TBT 作一般 lab proxy。
- 原始 JSON 含 13,035 筆 Event Timing entries、每筆時間／互動 ID／操作歸戶及逐趟摘要，保存在未提交的 `.ai/runs/issue24-inp-traceable-20261019/metrics.json`；臨時量測程式同目錄保存。因門檻會排除 <16ms entries，且腳本化旅程不等於實際訪客分布，結果限於本機合成診斷。全程無真實 API／SQL／付款／LAN；Issue24 維持 `claimed`，iPhone Safari／VoiceOver 仍依目前決策排除於本輪。

### Issue24 三語桌面長任務 TBT 估算（2026-10-19）

- Chrome 155.0.8059.84、macOS arm64、1440×900 CSS px、CPU 4×、150ms 網路延遲／1.6Mbps 下行／750Kbps 上行、固定 120ms 合成 API 延遲；production preview 僅綁 `127.0.0.1`。zh-TW／ja／en 的首頁、政策、購物車各 10 趟，共 90 趟。每趟以 Chrome Long Tasks API 計算 FCP 至 `networkidle` 後 5 秒窗口內的 `max(0, duration − 50ms)` 總和。
- 9 組均為 n=10、HTTP 200 10/10、FCP 10/10、page error 0，且無符合門檻的長任務；各組 TBT 估算 P50／P75／最大值均為 0／0／0 ms。原始 JSON、逐趟 long-task 清單與腳本保存在未提交的 `.ai/runs/issue24-tbt-lab-20261019/`。
- Lighthouse 與 trace-based TTI 未可用，故此為可重現的 Long Tasks API TBT 估算，不是 Lighthouse 官方 TBT；0 ms 只表示這個觀測窗口沒有超過 50ms 的長任務，不表示互動延遲為零、field INP 或正式 Core Web Vitals 通過。依[官方量測指引](https://web.dev/articles/vitals-measurement-getting-started)，TBT 是 lab INP proxy，實驗室與 field 結果須分開解讀。僅用 localhost 與合成資料，未改產品程式／API／資料／事件；Issue24 仍為 `claimed`，iPhone Safari／VoiceOver 依目前決策排除於本輪。

### Issue24 桌面三語導覽目標尺寸與 Lighthouse（2026-10-19）

- 修正共用語言切換、商品頁返回目錄、會員頁商店／購物車連結的最小目標尺寸為 24×24 CSS px。Playwright 三語幾何尺寸與合成頁面內容檢查 PASS 3/3；`npm run typecheck` PASS。
- Lighthouse 13.5.0／Chrome 155／Desktop preset、Accessibility only，商品、結帳、會員三頁各以 zh-TW／ja／en 執行，共 9 路由。每路 Accessibility score 1.00，`target-size` 通過且沒有失敗目標。原始報告在本機 `/private/tmp/kohami-lighthouse-*.json`。
- 僅使用 localhost HTTPS 開發預覽與 loopback 合成 API fixture，沒有 SQL／真實交易資料。結果限於所列桌面頁面，不代表全站 WCAG AA、正式 SLA 或手機／VoiceOver 驗收；手機與 VoiceOver 仍列選配且未驗證。

### Issue24 桌面三語效能證據充分性複核（2026-10-19）

- 重用既有桌面 synthetic lab 結果，未重測相同路由組合，也沒有發現足以支持產品效能修正的缺陷。LCP／CLS 已涵蓋三語首頁、政策、購物車、商品詳情、結帳結果及會員訂單頁；樣本與完整數值見本文件前述量測紀錄。首頁／政策／購物車固定節流組每語每頁 n=30，LCP P75 216–244 ms、CLS P75 0–0.0043；商品詳情／結帳結果／會員訂單組每語每頁 n=30（冷／暖各 15），LCP P75 228–1,808 ms、CLS P75 0–0.0305。會員訂單頁另有冷／暖各 n=30 的重測及 LCP 候選／資源瀑布診斷。
- 已有環境紀錄為 macOS arm64、Desktop Chrome 155.0.8059.84、1440×900 CSS px、CPU 4×、150 ms 延遲、1.6 Mbps 下行／750 Kbps 上行、合成 API 固定 120 ms，量測日期 2026-10-19；各 run 的樣本數、快取策略與命令仍以個別紀錄為準。資料來自 localhost production preview 和 synthetic fixture，未連真實 API／SQL／付款服務或 LAN。
- 三語互動量測每語 30 趟、每趟 15 類成功／錯誤恢復操作；Event Timing（duration ≥16 ms）逐趟最大候選 P75 為 zh-TW 40 ms、ja 32 ms、en 32 ms。Long Tasks API TBT estimate 另見前述紀錄。Event Timing estimate 排除低於門檻的項目，TBT 依觀察窗口推算；兩者都是本機工程診斷，不能判定 INP P75 ≤200 ms 已通過。
- N01／Q-01 未明訂唯一核心路由清單，而體驗規格列出 SCR-01–09 多個購物者及管理端畫面；已量頁面只覆蓋其中一部分，無權由本輪任意推定其餘必測路由。正式 field INP、正式使用者資料來源與正式測量期間亦未由本機 synthetic run 提供。因此本輪只判定已量桌面實驗室樣本的 LCP／CLS 低於規劃門檻；不將 N01／Q-01、Issue24 或正式 SLA 標為完成，Issue24 保持 `claimed`。後續先採用核心路由與操作樣本矩陣，再補其缺項及所需 INP 資料來源。

### Issue24 桌面三語完整鍵盤購物與付款結果旅程（2026-10-19）

- 新增 Storefront Playwright 案例 `apps/storefront/tests/issue24-desktop-full-keyboard-outcomes.spec.ts`，從三語略過導覽開始，以鍵盤完成商品搜尋、SKU 選擇、加車、取得報價、確認並建立 mock checkout、開啟唯讀結帳結果，再以 Tab／Enter 刷新付款狀態。
- 三語均通過處理中狀態與 503 錯誤保留快照、金額及焦點，且可再次以鍵盤查詢。zh-TW 結果為 mock success、ja 為 mock declined、en 為 unconfirmed；未知結果會顯示不可重複啟動付款的提示。
- `npm exec -- playwright test tests/issue24-desktop-full-keyboard-outcomes.spec.ts --config=playwright.config.ts --reporter=line --trace=off`：PASS 3/3；`npm run typecheck`：PASS。測試以 Playwright API route stubs 提供合成資料；localhost HTTPS 代理 fixture 亦只連 loopback，未使用真實 API、SQL、付款服務或 LAN。
- 測試前沙盒內 Chrome 啟動 SIGABRT，使用者既有本機桌面 Chrome 驗收授權下改以一般本機權限執行；起初 localhost preview／API fixture 未啟動造成 connection refused，之後只啟動 localhost HTTPS 預覽及 127.0.0.1 合成 API 後完成驗收。測試沒有發現產品程式錯誤；首次錯誤恢復斷言由一次性 503 改為持續 503，避免 GET 自動重試隱藏錯誤 UI。
- 新案例改用共用 `issue24-api-fixture.json` 商品，單獨執行 PASS 3/3，Nuxt 輸出未觀察到 hydration mismatch。七項合併回歸也 PASS；其中仍有既有 `issue24-core-keyboard-journey.spec.ts` 自訂商品 route stub 與 SSR fixture 不同的 hydration 警告，與本次新案例無關，且不能用合併回歸輸出推論所有既有 fixture 均一致。
- 本證據完成 Issue24 桌面鍵盤操作及核心付款結果／恢復子範圍，不是人工輔助科技簽核、完整 WCAG AA、真實支付端到端或正式 SLA；iPhone Safari／VoiceOver 仍依決策列為選配並未驗證。

### Issue24 三語桌面購物與付款主旅程冷／暖快取 LCP／CLS（2026-10-19）

依使用者確認的範圍，Issue24 桌面效能路由採購物與付款主旅程。規劃畫面與已實作頁面對照如下；搜尋與商品列表在首頁，結帳表單在購物車頁，沒有獨立 `/products` 或 `/checkout` 頁面。

| 規劃畫面 | 實際三語路由 | 量測頁面／互動範圍 |
| --- | --- | --- |
| SCR-01、SCR-02 | `/{zh-TW,ja,en}` | 首頁、商品列表／搜尋 |
| SCR-03 | `/{locale}/products/:slug` | 商品詳情與 SKU 展示 |
| SCR-04A、SCR-04B | `/{locale}/cart` | 購物車、費用試算及結帳表單入口 |
| SCR-05A | `/{locale}/checkout/:id` | 唯讀結帳／付款結果與狀態查詢頁 |

本輪使用 MacBook Pro（Apple M5 Pro、48 GB、macOS 26.6、arm64）、Google Chrome 155.0.8059.84、1440×900 CSS px、CPU 4× slowdown、150 ms 網路延遲、1.6 Mbps 下行／750 Kbps 上行。Nuxt production preview 僅綁 `127.0.0.1:3000`；合成 API fixture 僅綁 `127.0.0.1:5080`，每個 API 回應固定延遲 120 ms。沒有連接正式 API、SQL、付款服務或 LAN。

每語系／路由／快取分層各收集 30 次，冷暖快取各自計算，不合併 P75。冷快取每次使用新 browser context 並停用瀏覽器快取；暖快取每個批次先做一次不計分暖身，再於同一 context 重用資產。Chrome DevTools Protocol cache events：冷組均 0 次命中，暖組各組均有命中。第一批 2026-10-19 07:07:08 UTC 開始，第二批至 07:42:43 UTC 結束；共 720 筆計分導覽及 24 次暖身。LCP 取 buffered Largest Contentful Paint 的最大 `startTime`；CLS 排除 `hadRecentInput`，依 1 秒間隔／5 秒 session window 取最大值。每次等待 `networkidle`、主要標題可見、`document.fonts.ready` 與額外 500 ms。P75 使用 nearest-rank（升冪排序後取 `ceil(n×0.75)` 筆）。

| 語系／路由 | 冷 LCP P75 (ms) | 冷 CLS P75 | 暖 LCP P75 (ms) | 暖 CLS P75 |
| --- | ---: | ---: | ---: | ---: |
| zh-TW／首頁搜尋列表 | 224 | 0 | 228 | 0 |
| zh-TW／商品詳情 | 236 | 0.0036 | 220 | 0.0036 |
| zh-TW／購物車結帳表單 | 1,892 | 0.0385 | 460 | 0.0091 |
| zh-TW／結帳結果 | 220 | 0 | 232 | 0 |
| ja／首頁搜尋列表 | 224 | 0 | 220 | 0 |
| ja／商品詳情 | 228 | 0.0035 | 228 | 0.0035 |
| ja／購物車結帳表單 | 1,896 | 0.0392 | 452 | 0.0099 |
| ja／結帳結果 | 216 | 0 | 216 | 0 |
| en／首頁搜尋列表 | 212 | 0 | 216 | 0 |
| en／商品詳情 | 228 | 0.0040 | 224 | 0.0040 |
| en／購物車結帳表單 | 1,896 | 0.0372 | 452 | 0.0078 |
| en／結帳結果 | 208 | 0 | 212 | 0 |

24 個分層均為 LCP／CLS 30/30、HTTP 200 30/30、page error 0；暖組均有 CDP 快取命中。這些合成實驗室分層的 LCP P75 介於 208–1,896 ms，CLS P75 介於 0–0.0392，低於 Q-01 規劃門檻。購物車冷載入的 LCP P75 約 1.9 秒、CLS P75 最高 0.0392；沒有超過本機工程門檻的可重現缺陷，故未改產品程式。數字只代表列明的 production build、合成 fixture、桌面 Chrome 與網路／CPU 條件，不代表 field Core Web Vitals、真實 API、正式 SLA 或行動驗收。

可重現檔案與命令：

- 本輪量測實際執行的 API／benchmark／aggregate 腳本 `node --check` 均 PASS；第二批命令 `ISSUE24_SAMPLES_PER_CACHE=15 ISSUE24_OUTPUT=/private/tmp/issue24-core-route-benchmark-batch2-20261019.json ISSUE24_STARTED_AT=$(date -u '+%Y-%m-%dT%H:%M:%SZ') node /private/tmp/issue24-core-route-benchmark-20261019.mjs` PASS，360 筆計分樣本、12 次暖身、0 failure。第一批亦產生 360 筆計分樣本與 12 次暖身。
- 合併驗證命令 `node /private/tmp/issue24-core-route-aggregate-20261019.mjs` PASS：24 個 n=30 分層，720/720 完整 HTTP 200／LCP／CLS 樣本、0 page error，所有暖組有快取命中。執行時原始腳本與資料位於 `/private/tmp/`；為保存證據，副本與原始 JSON 已歸檔於 [本輪 Issue24 run 證據](../../.ai/runs/issue24-core-route-matrix-20261019/)，包含 `api-fixture.mjs`、`benchmark.mjs`、`aggregate.mjs`、兩批原始資料與 `metrics-merged.json`。benchmark 會啟動桌面 Chrome，重跑前須先啟動 loopback 預覽與合成 API fixture。
- 第一批命令產生 360 筆計分樣本與 12 次暖身，但其原始版筆數 assertion 把暖身也計入而回傳 exit 1；原始樣本仍完整，獨立合併驗證確認 0 failure。修正 assertion 後第二批與合併驗證均 PASS，沒有因這個 harness 計數錯誤重跑或丟棄樣本。

互動方面，既有三語 30 趟完整成功／錯誤恢復旅程的 Event Timing（`durationThreshold: 16 ms`）逐趟最大候選 P75 為 zh-TW 40 ms、ja 32 ms、en 32 ms；較短事件會被 observer 門檻排除，且樣本不是實際訪客分布，故這是實驗室代理估算，不是完整 INP。TBT 亦僅為 lab proxy。專案未設定公開正式網域，這輪沒有可查的 URL-level CrUX field INP；CrUX 使用 28 日滾動資料，並只在有足夠真實使用者樣本時提供頁面／來源資料（[CrUX History API](https://developer.chrome.com/docs/crux/history-api?hl=en)）。沒有加入第一方 RUM 或收集訪客事件。正式 INP P75 與來源／期間仍未驗證；不得以 Event Timing、rAF 或 TBT 代替。

Issue24 的路由範圍已按使用者決策釐清，但 N01／Q-01 的 INP 正式資料來源與 field 期間仍未確認。因此票券的三項效能合併檢查維持未完成、Issue24 維持 `claimed`；不宣稱完整 N01／Q-01、正式 SLA、完整 WCAG 或 field Core Web Vitals 通過。iPhone Safari／VoiceOver 依決策維持選配且未驗證。

### Issue24 field INP 來源與觀測期間決策（2026-10-19）

- 可公開索引的三語首頁／搜尋列表與代表商品 URL，建議優先使用 CrUX URL-level API，查 `interaction_to_next_paint` P75 並限定 desktop form factor；按 locale 與實際 URL 分開保存查詢日期、CrUX `collectionPeriod` 首末日期、P75 與缺值。CrUX 是 28 日滾動資料，精確使用者樣本數未由 API 提供；僅記提供者未揭露，不填估算值。頁面無資料時標為未驗證，不能以 origin-level 或其他 URL 代替缺失的 URL-level 結果。[CrUX API](https://developer.chrome.com/docs/crux/api) 與[History API 資料期說明](https://developer.chrome.com/docs/crux/history-api)
- 購物車及結帳結果為私人／`noindex` 範圍，不假定會有可用 CrUX URL-level 資料。其 field 驗收若有必要，待第一方 RUM 經隱私及營運治理核准後執行；治理需覆核目的與告知、欄位最小化、抽樣、保存、存取／刪除及上報端點。現有 Storefront 未安裝 RUM SDK，未收集訪客效能事件。
- RUM 的建議設計為每個「語系 × 核心路由 × desktop」分層至少 400 個有效 INP page visits，連續 28 日觀測；不得將 interaction 數當 page-visit 樣本、無 INP 回報的 page visit 當成 0，或合併語系／路由補足數量。400 筆只是一項專案建議的樣本下限，使用二項次序統計量近似 P75 的 95% 秩區間為 70.7–79.3 百分位，並非 CrUX 官方樣本門檻，也不是偏差校正或達標保證。版本、缺失率、排除規則與窗口首末日期須隨 P75 一併報告。
- 目前未設定正式公開網域，沒有可查的正式 CrUX URL-level 結果，亦未核准或實作 RUM。因此此決策僅定義後續查核方法，field INP、正式三語 LCP／INP／CLS 及正式 SLA 均維持未驗證。lab Event Timing、rAF 與 TBT 不得替代 field INP。

### Issue24 延後 RUM、正式網域納入規劃後查 CrUX 的決策（2026-10-19）

- 使用者決定現階段不啟用第一方 RUM、不收集真實訪客效能事件，且正式網域暫不納入規劃。若未來將正式網域納入規劃並部署，再依本紀錄對三語公開且可索引的核心頁逐一查詢 CrUX URL-level desktop 資料；先確認正式 canonical URL，再保存查詢日、28 日 collection period、指標 P75、資料是否缺漏與提供者未揭露的樣本數。此決策不授權其他遙測收集；目前以本機合成環境驗證可重現的 Storefront 行為。
- 購物車與結帳結果頁維持 field INP 未驗證；若日後仍需驗證這些私人／`noindex` 路由，須另行取得明確治理決策後才可評估 RUM。公開頁 CrUX 或 origin-level 結果不得替代這些路由的缺失資料。
- 在取得可用的正式 URL-level CrUX 結果前，正式 field INP 與三語正式 LCP／INP／CLS、Issue24 效能驗收及正式 SLA 均維持未驗證；本機合成實驗室數據不因此改變其證據層級。

### Issue24 本機合成購物與付款旅程重驗（2026-10-19）

- 依使用者選擇，重跑 `npm exec -- playwright test tests/issue24-desktop-full-keyboard-outcomes.spec.ts --config=playwright.config.ts --reporter=line --trace=off --output=/private/tmp/issue24-local-keyboard-run`：PASS 3/3（zh-TW／ja／en）。旅程透過鍵盤完成搜尋、SKU 選擇、加車、報價、建立合成結帳、進入唯讀結果並刷新付款狀態，覆蓋 mock 成功／拒絕／未知、處理中與 503 查詢恢復。
- Playwright API route stubs 提供所有旅程資料；未連接 SQL、真實 API、付款服務或 LAN。此測試驗證瀏覽器端合成旅程，不能證明現有本機 API／SQL／worker 已可用。此前檢查的既有 SQL fixture `/ready` 與商品 API 為 503，未對該 fixture 執行 migration、reset 或資料寫入；本輪未建立新 fixture。
- 三語 LCP／CLS 繼續以既有桌面 lab 路由矩陣及各自量測條件作工程證據；Event Timing／TBT 不標成 INP。正式網域目前不在規劃、未啟用 RUM，field INP 與正式 SLA 仍未驗證。Issue24 保持 `claimed`；iPhone Safari／VoiceOver 仍為選配且未驗證。

### Issue24 桌面三語購物／付款核心路由 LCP／CLS 矩陣（2026-10-19）

- 依使用者確認的購物／付款旅程，量測首頁搜尋／商品列表、商品詳情、購物車／結帳表單、結帳結果四類路由，各含 `zh-TW`／`ja`／`en`；每個語系 × 路由 × 冷／暖快取分層 n=30，共 24 組、720 個計分導覽及 24 個不計分暖身。P75 使用 nearest-rank `ceil(n × 0.75)`。
- 既存合併結果 `.ai/runs/issue24-core-route-matrix-20261019/metrics-merged.json`：24 組均為 LCP／CLS 30/30、HTTP 200、page errors 0；失敗及不合格分組均為 0。LCP P75 全組範圍 208–1,896 ms，CLS P75 範圍 0–0.0392；三語購物車冷快取 LCP P75 為 1,892–1,896 ms，為本矩陣最高組，仍低於 2.5 秒 lab 目標。冷組 cache hits 為 0，暖組均有 Nuxt 資產 cache hit。
- 條件：macOS Darwin arm64、Headless Chrome 155.0.8059.84、1440×900 CSS px、CPU 4×、150 ms 延遲、1.6 Mbps 下行／0.75 Mbps 上行、合成 API 固定 120 ms；UTC 期間 2026-10-19 07:07:08–07:42:43.731。具體機型／CPU 型號／RAM 未記錄。LCP 取 navigation LCP 候選最大 startTime；CLS 排除 `hadRecentInput`，依 1 秒／5 秒 session window 計算；每次等待 network idle、主標題、字型及 500 ms。全程 localhost synthetic fixture，不連 SQL、真實 API／付款、LAN 或訪客遙測。
- 沿用既存原始樣本與腳本，未重新量測。此為指定桌面條件的 lab LCP／CLS 證據；不代表 field Core Web Vitals。未取得正式 field INP 資料來源及期間，因此 N01 整體、field INP、正式 SLA 與 Issue24 效能總項仍未完成；Issue24 維持 `claimed`，iPhone Safari／VoiceOver 維持選配且未驗證。
- 2026-10-20 對 `.ai/runs/issue24-core-route-matrix-20261019/metrics-merged.json` 做唯讀再計算，確認 24 組均為 30/30 LCP 與 CLS 樣本、HTTP 200、0 page errors，且 720 筆計分導覽、24 筆暖身、失敗 0；nearest-rank P75 與既有彙總一致（LCP 208–1,896 ms；CLS 0–0.03923）。這是對已保存證據的核對，未重新量測；不改變 lab／field 證據界線。實際機型、CPU 型號及 RAM 未記錄，故量測條件仍有硬體細節缺口。

### Issue24 後續隔離本機 SQL fixture 準備（2026-10-19）

- 在 `/private/tmp/kohami-issue24-runtime` 以目前 `main` 的乾淨提交快照建立獨立 checkout；為避免碰觸主工作樹既有 `.local/m0` 私密材料，使用全新 `.local/m0/`，並於 loopback `14337` 建立唯一 Compose project `kohami-issue06-c4b6be6d4ea8`。SQL Server 2025 strict TLS 驗證通過；migrations `0001`–`0012`、完整 Commerce SQL harness 與 synthetic row 清理 PASS。Provisioner 未建立 Owner／permit／原生權限，fixture 保留供後續本機驗收。
- 執行 `python3 scripts/m0/provision-issue06-fixture.py --port 14337`：PASS。Native／Commerce Release DLL 由主 checkout 既有建置產物複製至此乾淨快照；來源程式在主工作樹沒有未提交差異，產物時間為 2026-10-18。暫存 checkout 的 locked restore 無輸出逾兩分鐘後中止；沒有因此修改 NuGet 設定或主 checkout。
- 主 checkout 既有程序仍占用 `127.0.0.1:5080` API 與 `localhost:3000` Storefront。唯讀 `GET http://127.0.0.1:5080/health` 回 200（`database-connected`），`GET /ready` 回 503；主 checkout `.local/m0/runtime-private.json` 指向 `localhost:14336/KohamiM0`。因此沒有把該 API readiness 結果歸給新 fixture，也沒有啟動第二組會與既有埠衝突的服務、停止舊程序或遷移既有資料庫。新 fixture 尚未完成 live API／worker／Storefront acceptance。
- 本結果只證明新隔離 fixture 及其 Commerce SQL harness 可用。正式網域／RUM／field Core Web Vitals 未納入或未驗證；不能由 SQL harness 推論 Issue24 live browser journey、正式 SLA 或完整 M0 通過。Issue24 維持 `claimed`，iPhone Safari／VoiceOver 仍依決策列為選配且未驗證。

### Issue24 隔離連接埠合成旅程與 live API 驗收結果（2026-10-19）

- API 新增 `KOHAMI_API_PORT`（5080–5099）且仍綁定 `127.0.0.1`；Nuxt dev port 由 `NUXT_PORT` 控制，API proxy target／允許的 Storefront host 分別由 `NUXT_API_ORIGIN`、`NUXT_SITE_ORIGIN` 控制；`NUXT_BUILD_DIR` 可隔離同 checkout 的 Nuxt 產物。預設 port／origin 維持 5080／3000，API proxy 僅接受 loopback HTTP upstream。
- 在 `KOHAMI_API_PORT=5081` 使用 dedicated SQL fixture 啟動 API，唯讀 `/health` 回 `database-connected`；`/ready` 未取得結果。Runner 在 `/private/tmp` checkout 以 `KOHAMI_API_PORT=5082 KOHAMI_SITE_PORT=3001` 執行：strict TLS 與合成 SKU 建立通過，但 API 未於 60 秒 health window 內 ready，未進入 API checkout assertions。Runner 清理本輪記錄的合成交易／庫存資料，並保留私有 API log 供診斷。主 checkout runner 因既有 `.local/m0/issue12-*` 私有路徑而在 check-only fail closed，未改動其內容。
- 獨立 HTTPS `localhost:3001` Nuxt dev instance 使用 `/private/tmp/kohami-issue24-nuxt-3001` build directory。`KOHAMI_STOREFRONT_ORIGIN=https://localhost:3001 npm exec -- playwright test tests/issue24-desktop-full-keyboard-outcomes.spec.ts --config=playwright.config.ts --reporter=line --trace=off --output=/private/tmp/issue24-port-playwright`：PASS 3/3（zh-TW／ja／en）。此回歸使用 synthetic route stubs，不連 API／SQL。
- 驗證：`python3 -m unittest tests.m0.test_issue12_checkout_acceptance -v` PASS 6/6；`dotnet build apps/api/Api.csproj --configuration Release --no-restore --verbosity minimal /p:UseSharedCompilation=false -m:1 -nr:false` PASS 0 warnings／errors；`npm run typecheck` PASS；`git diff --check` PASS。瀏覽器在一般 sandbox 首次無法啟動，升權重跑後三語通過。
- API runner health timeout 使 live API／SQL checkout journey 仍未驗證；本輪沒有新的 LCP／INP／CLS 樣本或 SLA 結論。正式網域未納入規劃、RUM 未啟用；Issue24 維持 `claimed`，iPhone Safari／VoiceOver 仍為選配且未驗證。

### Issue24 dedicated SQL API readiness 診斷（2026-10-19）

- 在既存隔離 checkout `/private/tmp/kohami-issue24-runtime` 以新 runner 輸出路徑重試；原有失敗 log/state 保留。API `5082` 與 `5084` 均通過 `/health`，`/ready` 為 200（`ready`），strict TLS 與兩個合成 SKU 建立通過；但首個 `POST /carts` 回 `503 SERVICE_UNAVAILABLE`，未進入 checkout assertions。API log 為空，無法由現有輸出判定 SQL 或應用內部例外原因。
- 每輪 runner 只清理自身登錄的 synthetic carts、quotes、checkouts、orders、idempotency、reservations 與 SKU rows；本輪新增的 fail log 留在 `/private/tmp/kohami-issue24-runtime/.local/m0/issue12-checkout-retry-logs/`、`issue12-checkout-retry2-logs/`、`issue12-checkout-retry3-logs/`。既存 fixture 未 reset。`5080` 已有 listener，未停止或覆用；主 checkout `.local/m0` 私有輸出路徑仍因既存資料而拒絕 runner。
- 結論：dedicated SQL schema readiness 已確認，但 API 購物交易仍未驗證；`/ready` 成功不能取代 `POST /carts` 行為證據。尚未新增 LCP／INP／CLS 樣本，field INP／正式 SLA 未驗證，效能 checkbox 維持未勾選，Issue24 維持 `claimed`。下一步是在隔離 fixture 上取得安全的 API 例外診斷，再重跑購物／付款驗收；目前不需啟用 RUM 或正式網域。

### Issue24 dedicated SQL maintenance gate 根因確認（2026-10-19）

- 使用 dedicated SQL fixture `127.0.0.1:14337`、strict TLS、API loopback `127.0.0.1:5099` 與核准 Origin `https://localhost:3000` 重跑；API `/ready` 回 200，但第一個 `POST /carts` 仍回 503。一次性本機診斷只記錄 SQL error number/state，捕捉到 SQL `50002`、state `1`。
- `src/Modules/Commerce/Infrastructure/Persistence/Store.cs` 將 `50002` 用於 `m0.SecurityState` maintenance gate 未關閉時的 `MAINTENANCE_REQUIRED`。API 對外仍回泛化 `503 SERVICE_UNAVAILABLE`，沒有暴露 SQL 細節；本輪未修改產品 API 或 SQL 狀態。
- Runner finally PASS，只移除本輪登錄的 synthetic carts、quotes、checkouts、orders、idempotency、reservations 與本輪建立的 SKU rows；fixture 保留。未建立 Owner／permit，也未直接更新 gate、使用 DBA 權限或執行 checkout／付款。
- live API／SQL／worker 購物與付款旅程仍未驗證。下一步須先依本機操作手冊的原生授權／reconciliation 流程取得明確的 fixture 啟用證據；在此之前不得繞過 maintenance gate。Issue24 效能 checkbox 維持未勾選，field INP／正式 SLA 未驗證，Issue24 維持 `claimed`。

## Issue 25 本機健康檢查與可觀測性來源盤點（2026-10-19）

- 靜態檢視 `apps/api/Program.cs`、`apps/worker/Program.cs` 與現有一般診斷日誌來源盤點：兩個 Host 均呼叫 `Logging.ClearProviders()`，未註冊替代 Console、檔案、資料庫或外部 logging provider；API／worker 雖有 `ILogger` 呼叫點，但沒有設定接收端。限定搜尋 `AddHealthChecks`／`MapHealthChecks`、OpenTelemetry、logging provider、monitor／alert 字串，未找到關鍵交易監測或告警路由實作。這是本機 repository 範圍的程式與文件盤點，不代表外部服務完整性。
- API `GET /health` 只確認資料庫可連線，回報 `database-connected`、`mode=mock`、Dataset ID 及 `activation=requires-native-reconciliation`；`GET /ready` 只驗證目前支援 migration ledger，分別回 `ready` 或 `503 not-ready`。它們沒有驗證關鍵交易結果、支付差異、告警送達或值班處理能力，亦不會解除 maintenance gate。
- 初次唯讀 `curl` 被 sandbox 的本機網路政策以 `not_allowed_local` 拒絕；其後核准對 loopback 執行唯讀 GET，2026-10-19 11:07:35 UTC 的 `/health` 回 HTTP 200 `database-connected`、`activation=requires-native-reconciliation`、`mode=mock`、`datasetId=demo_m0`；`/ready` 回 HTTP 503 `not-ready`。這只證明當時資料庫連線可用，schema readiness 未通過；不代表 checkout API、worker、付款或交易健康。探測未啟停服務、連接或修改資料庫、重設 fixture、變更權限或維護狀態。
- 結論：本機有有限的 API connection／schema readiness probe，但沒有持久化一般診斷日誌、商務／關鍵交易監測、重大付款差異告警、告警分級與負責人／處理程序證據。沒有啟用 RUM、外部 alerting 或新訪客資料收集。備份／還原、RPO／RTO 與復原後對帳仍依部署目標決策延後；此盤點不完成 Issue25 任一原驗收項目，Issue25 維持未結案。
- 驗證：`git diff --check`；人工比對 API／worker Host 設定、本機操作手冊、Issue 23 一般診斷日誌來源盤點及 Issue25 範圍。未執行產品測試或服務啟動。

### Issue25 `/ready` 根因確認（2026-10-19）

- 依使用者授權重查本機 API：2026-10-19 11:27:52 UTC `/ready` 回 HTTP 503 `not-ready`；11:28:06 UTC `/health` 回 HTTP 200 `database-connected`。API 可連資料庫，但 schema readiness 未通過。
- 依使用者授權，在既有 dedicated SQL Server container `kohami-issue06-41cdf2acf9ba-sqlserver-1`（loopback port 14336）內使用 `m0_api` runtime principal，以 strict TLS 與 `/var/opt/mssql/tls/server.pem` server certificate pin 連至 `KohamiM0`。唯讀 `OBJECT_ID(N'ops.SchemaMigrations',N'U')` 查詢回 `LEDGER_MISSING`；沒有讀取業務列。這確認 readiness 根因為舊 fixture 缺少 `ops.SchemaMigrations`，不是 API 無法連線。既有 migration 工具對這類無 ledger 舊 schema 會 fail closed，回報 `SQL_MIGRATION_BASELINE_REQUIRED`；本次沒有呼叫 migration 工具。
- SQL 只執行 metadata `SELECT`；沒有寫入、migration、seed、reset、權限／maintenance 改動或啟停服務。查詢使用的私密連線資訊未輸出或保存。此根因紀錄不表示 fixture 已修復；如需修復須另依 runbook 完成舊 schema 基線採用前置與授權審核。Issue25 監控、告警、備份／還原、RPO／RTO 驗收仍未完成。

### Issue25 bounded native 診斷檢查（2026-10-20）

- 命令：`dotnet tests/Native/bin/Release/net10.0/NativeTests.dll --safe-diagnostic`；結果：`PASS native diagnostics expose only bounded reason codes`。檢查只驗 native 診斷理由碼對任意錯誤文字與含秘密字串的白名單拒絕行為，未連 SQL、啟動服務或讀取私密設定。
- `dotnet run --project tests/Native/NativeTests.csproj -c Release -- --safe-diagnostic` 的建置長時間無輸出，該次被中止；所以這筆 PASS 來自既有 Release DLL，不代表本輪建置成功。此單一檢查不代表 API／worker 日誌、授權、交易告警或復原驗收通過。

### Issue24 三語首頁與商品詳情本機唯讀載入核對（2026-10-19）

- 為避免頁面瀏覽器掛載的 cart loader 帶來寫入副作用，僅對 loopback 發出停用代理的 GET。`https://localhost:3001/zh-TW`、`/ja`、`/en` 首頁各 1 次均 HTTP 200；三語 `/products/demo-linen-shirt` 詳情頁各 1 次均 HTTP 200，SSR HTML 各含該語系商品名稱。`http://127.0.0.1:5081/health`、`/ready` 及 `/api/v1/products?limit=1` 各 1 次均 HTTP 200，API 狀態為 `database-connected`／`ready`、模式 `mock`、dataset `demo_m0`；商品列表回應含 3 筆 items。未執行 cart／checkout／payment mutation。
- 可重現命令：`for locale in zh-TW ja en; do curl --noproxy '*' -k -sS "https://localhost:3001/${locale}"; curl --noproxy '*' -k -sS "https://localhost:3001/${locale}/products/demo-linen-shirt"; done`；API 依序執行 `curl --noproxy '*' -sS http://127.0.0.1:5081/health`、`.../ready`、`.../api/v1/products?limit=1`。單次 GET spot check 只驗目前服務回應與 SSR 商品內容，不構成效能樣本或 live 商務交易驗收；不能據此推論頁面 hydration 不會建立 cart。
- 本次沒有新增 LCP／INP／CLS 樣本。既有 24 個桌面語系／路由／快取分層 LCP／CLS 紀錄仍是 localhost production preview 加合成 API 條件下的 lab evidence；Event Timing 16 ms 門檻候選及 TBT 都不是正式 INP。正式 field INP／SLA 仍未驗證，Issue24 效能合併檢查維持未完成、狀態 `claimed`；iPhone Safari／VoiceOver 仍列選配且未驗證。

### Issue24 三語核心頁 SSR 圖片與開發模組資源核對（2026-10-19）

- Python 3.14.8 以停用代理、忽略 localhost 憑證驗證的 HTTPS GET 檢查 `https://localhost:3001`：zh-TW／ja／en 首頁與 `/products/demo-linen-shirt` 各 1 次，共 6 個 SSR HTML 回應均 HTTP 200。首頁共列出 15 個不同展示商品 PNG；商品詳情均列出 linen shirt PNG，15 個 PNG GET 均 HTTP 200、`image/png`。頁面引用的 21 個不同 Nuxt／Vite 開發模組 URL 均 HTTP 200。
- 此服務是 Nuxt 開發模式；`?vue&type=style` 模組回 `text/javascript`。本次沒有用瀏覽器驗證樣式套用、production CSS／字型、圖片解碼或載入時序；各 URL 各請求一次，沒有新增 Web Vitals 樣本。此結果只證明 localhost 開發 SSR 與展示圖片／模組 URL 可取用，不涵蓋 API／SQL 商務寫入、production build 或 field Core Web Vitals。Issue24 效能項仍未結案，正式 INP／SLA 未驗證。

### Issue24 三語核心頁隔離 production preview 資源載入（2026-10-19）

- 由本機 `main` commit `6afe3a3802ed239c70519b17d41d1081cd8c8a13` 匯出 Storefront 至 `/private/tmp/kohami-issue24-production-assets-20261019`，隔離安裝輸出；`npm run build` PASS（Nuxt 4.6.1，client／SSR／Nitro，2.59 MB）。錯誤的絕對 `NUXT_BUILD_DIR` 初次嘗試因 Vite 找不到 `.nuxt/tsconfig.app.json` 失敗；以隔離副本預設相對目錄重跑 PASS。主 checkout `.output` 保持未動。
- 使用 macOS 26.6 arm64、Headless Google Chrome 155.0.8059.84、1440×900 CSS px、無 CPU／網路節流；六個三語首頁與 linen shirt 商品頁各 1 次，頁面 JavaScript 停用。production Nitro 只連動態 `127.0.0.1` 合成 API stub；stub 僅供商品 GET、拒絕非 GET，非 GET 計數為 0。六頁皆 HTTP 200／`text/html`／`no-store`，沒有失敗的 CSS 或圖片資源。
- Chromium 觀察首頁 7 個、商品頁 3 個 inline stylesheet（無外部 stylesheet URL）；主內容 computed `font-family: system-ui, sans-serif`、`max-width: 1184px`。每頁圖片均 HTTP 200／`image/png`、解碼完成、寬 1024 px。`document.fonts.status=loaded`，未發出外部字型請求，頁面使用系統字型；硬體 CPU／記憶體資料本次未能取得。runner：`/private/tmp/kohami-issue24-production-assets-20261019/apps/storefront/verify-assets.mjs`。
- 此為六個頁面各一次的本機 production 資源呈現檢查，不是效能樣本；JavaScript 停用，未驗購物／付款互動、Safari／iOS、網路情境、冷暖快取或 Web Vitals。正式 field INP／SLA 維持未驗證，Issue24 效能項維持未結案。

### Issue24 三語購物車與結帳結果 production 資源呈現（2026-10-19）

- 以隔離 production preview 搭配動態 localhost synthetic API stub，使用 macOS 26.6 arm64、Headless Chrome 155.0.8059.84、1440×900 CSS px、未節流 CPU／網路及停用 JavaScript，對 zh-TW／ja／en 的 `/cart`、`/checkout/checkout-issue24-assets` 各執行 n=1，共 6 個瀏覽。頁面全數 HTTP 200、`text/html;charset=utf-8`、`no-store`；各有 3 個 inline stylesheet，沒有 CSS／圖片／字型錯誤。這些頁面本身不含圖片。Cart 主內容 computed font 為 `system-ui, sans-serif`，checkout 主內容為瀏覽器預設 `Times`；程式碼未設定共用全域字型，不據此判定產品缺陷。瀏覽器沒有外部字型請求，`document.fonts.status=loaded`。
- API stub 收到 5 個 GET（共用 auth session 1、商品清單 1、checkout SSR 狀態 3），非 GET 0。checkout SSR 以 synthetic status 呈現；JavaScript 停用使 cart `onMounted` 載入未執行，故未驗 cart 資料呈現。未連 SQL、正式 API／付款、LAN 或 visitor telemetry。runner：`/private/tmp/kohami-issue24-production-assets-20261019/apps/storefront/verify-assets.mjs`。
- 命令：`node /private/tmp/kohami-issue24-production-assets-20261019/apps/storefront/verify-assets.mjs`。此為資源呈現抽查，每路由 n=1，沒有 P75 或 Web Vitals；無 CPU／網路節流，硬體 CPU／記憶體未取得。未驗互動、水合、快取矩陣、Safari／iOS、正式 field INP／SLA。沒有產品程式碼變更；Issue24 效能驗收維持未完成、狀態 `claimed`。


### Issue24 三語 cart JavaScript hydration 抽查（2026-10-19）

- 使用既有隔離 Nuxt production preview 與動態 `127.0.0.1` synthetic API stub，macOS arm64、Headless Chrome 155.0.8059.84、1440×900 CSS px、JavaScript 啟用、CPU／網路無節流。zh-TW／ja／en 的 cart 與 checkout result 各導覽一次（每語／頁面 n=1），單一 browser context 未清快取，`networkidle` 後讀取 DOM；不是冷／暖快取樣本。
- cart GET 回傳合成 SKU `sku_issue24_asset_fixture`、quantity 2、unit price TWD 1000；三語均顯示本地化商品名、SKU、數量 2、小計 TWD 2000。六頁皆 HTTP 200、沒有失敗 CSS／圖片／字型資源；API 共 11 GET、0 非 GET（stub 對所有非 GET 回 405 並記錄）。checkout 頁只確認路由資源載入與合成狀態 GET，未驗完整付款互動。
- 命令：`node --check /private/tmp/kohami-issue24-production-assets-20261019/apps/storefront/verify-assets.mjs`、`node /private/tmp/kohami-issue24-production-assets-20261019/apps/storefront/verify-assets.mjs`。結果 PASS。macOS 26.6／arm64 已確認；CPU 型號／記憶體受 sandbox 權限限制無法取得，冷暖快取分層未設定；每組 n=1。未收集 LCP／INP／CLS，因此沒有 P75 或正式 field Core Web Vitals 結論；Issue24 效能驗收仍未完成，狀態 `claimed`。未連 SQL、正式 API／付款、LAN 或訪客資料。runner 位於隔離 `/private/tmp`，未加入產品檔案。

### Issue24 三語桌面購物車 CLS 與 INP 證據適用性判定（2026-10-19）

- 對照 N01 的 LCP／INP／CLS 規劃門檻與既存 route matrix：購物車／結帳表單 CLS 每語系冷／暖各 n=30，P75 分別為 zh-TW 0.0385／0.0091、ja 0.0392／0.0099、en 0.0372／0.0078；nearest-rank `ceil(n × 0.75)`。最大值 0.0392 低於 0.1 lab 參考門檻。完整路由矩陣、量測定義、期間、硬體可得性、瀏覽器、viewport、CPU／網路條件、cache 狀態與限制見本文件前述「Issue24 桌面三語購物／付款核心路由冷暖快取 LCP／CLS」紀錄。
- CLS 樣本在 1 秒間隔／5 秒 session window 取最大值並排除 `hadRecentInput`，所以這是既定 localhost synthetic 桌面 lab 條件下的工程診斷；不推論 field CLS 或正式 SLA。無需為此指標另行重測。
- INP 不可由上述矩陣驗收：該矩陣只收集 navigation LCP／CLS。另存的 `.ai/runs/issue24-inp-traceable-20261019/metrics.json` 是另一批每語系 n=30 合成互動旅程，Event Timing observer 門檻為 16 ms；每語系 1,260 筆 entries 中有 90 筆 interaction entries 未映射至互動。其每趟觀察到的最慢互動 P75 為 zh-TW 40 ms、ja 32 ms、en 32 ms，僅為受門檻與合成旅程限制的 Event Timing 代理，不能替代完整 INP 或與 200 ms field 門檻作通過判定。TBT／rAF 代理同樣不能替代 INP。
- 判定：既有量測可支持本機指定條件下 LCP、CLS 的路由分層比較；field INP 與正式 field Core Web Vitals 仍未驗證。正式網域尚未納入規劃、RUM 未啟用，未收集訪客資料。Issue24 效能項維持未勾選、狀態 `claimed`；iPhone Safari／VoiceOver 維持選配且未驗證。

### Issue24 三語購物車數量錯誤／恢復互動 CLS（2026-10-19）

- runner `.ai/runs/issue24-cart-interaction-cls-20261019/measurement.mjs` 使用既存隔離 production preview（來源 commit `6afe3a3802ed239c70519b17d41d1081cd8c8a13`）；已核對 `apps/storefront/app/pages/cart.vue` 與 `apps/storefront/app/composables/useCommerceCart.ts` 在該 commit 與目前 HEAD 間無差異。Playwright 將瀏覽器 `/api/v1/*` 請求完全攔截為合成回覆，並對數量更新每趟注入兩次 503、兩次成功重試；不連 SQL、正式 API、付款服務、LAN 或 RUM。
- 使用 macOS 26.6 arm64、Headless Chrome 155.0.8059.84、1440×900 CSS px、CPU 4× slowdown、150 ms 網路延遲／1.6 Mbps 下行／0.75 Mbps 上行，stub 寫入回覆延遲 120 ms。每語系 30 個新 browser context；每 context 四階段，總計 90 趟、360 個互動階段。期間 UTC：2026-10-19 15:36:51.931–15:44:51.974。CPU 型號與記憶體未取得。每階段等待互動結果與 650 ms，CLS 依排除 `hadRecentInput` 的 1 秒 gap／5 秒 cap session window 取最大值，P75 為 nearest-rank `ceil(n × 0.75)`。

| 語系 | 數量更新失敗 CLS P75 | 第一次恢復 CLS P75 | 再次失敗 CLS P75 | 第二次恢復 CLS P75 |
| --- | ---: | ---: | ---: | ---: |
| zh-TW | 0.0361 | 0.0058 | 0.0058 | 0.0058 |
| ja | 0.0362 | 0.0063 | 0.0063 | 0.0063 |
| en | 0.0366 | 0.0049 | 0.0049 | 0.0049 |

- 各語系／階段有效樣本均 30/30，沒有意外 API 狀態；原始 shift entries（包含時間、分數、`hadRecentInput` 與可取得來源矩形）與輸出摘要保留於上述 run 目錄。結果低於 0.1 的 lab 參考門檻，但 n=30 合成互動階段只供指定 localhost 環境診斷，不證明 field CLS／Core Web Vitals、INP 或正式 SLA 通過。Issue24 效能項維持未完成與 `claimed`；field 指標、正式 SLA 及 iPhone Safari／VoiceOver 仍未驗證。

### Issue24 三語首頁加車與結帳報價錯誤／重試 CLS（2026-10-19）

- 隔離 localhost production preview 上以 Playwright 合成 route stub 量測 zh-TW／ja／en 首頁加車失敗／重試及結帳報價失敗／重試；每語系 n=30，共 90 趟、360 個階段。首頁 SSR 目錄初始請求不由 browser route stub 攔截，樣本先使用頁面重試載入合成商品，等待 Vue hydration、購物車及 SKU 選擇就緒後才開始；這段初始化不納入互動 CLS。每趟預期加車與報價各一次 503、一次 200，四個階段皆等待 UI 狀態及 650 ms。
- UTC 期間 2026-10-19 16:00:53.271–16:09:28.541；Darwin 25.6.0 arm64、Headless Chrome 155.0.8059.84、1440×900 CSS px、CPU 4× slowdown、150 ms 網路延遲／1.6 Mbps 下行／0.75 Mbps 上行；stub mutation 延遲 120 ms，每趟使用新 browser context，快取未另分層；CPU 型號／記憶體未取得。P75 為 nearest-rank `ceil(n × 0.75)`；CLS 取排除 `hadRecentInput=true` 後、1 秒 gap／5 秒上限 session window 最大值。

| 語系 | 加車失敗 | 加車重試 | 報價失敗 | 報價重試 |
| --- | ---: | ---: | ---: | ---: |
| zh-TW | 0 | 0 | 0 | 0 |
| ja | 0 | 0 | 0 | 0 |
| en | 0 | 0 | 0 | 0 |

- 12 組均 n=30；90 次加車與 90 次報價請求均得到預期 503／200。raw JSON 共有 210 筆 shift entries，其中加車失敗／重試各語系均有 30 筆、全數 `hadRecentInput=true`；報價失敗無 entries；報價重試僅英文有 30 筆、全數因近期輸入排除。候選 entries 單筆最大 shift 分數為加車 zh-TW 0.009788、ja 0.010304、en 0.020661，英文報價重試 0.002721。表內 CLS P75=0 表示演算法在觀察窗排除近期輸入 shifts 後無計分項，不等於互動沒有版面變動。
- 原始 JSON 與 runner 留於 `.ai/runs/issue24-add-quote-cls-20261019/`。這是指定桌面 localhost 合成互動 lab 診斷，不代表 field CLS／Core Web Vitals、INP 或正式 SLA；Issue24 效能項維持未完成、`claimed`，field 指標及 iPhone Safari／VoiceOver 仍未驗證。

### Issue24 互動後版面位移來源判讀（2026-10-20）

- 檢視 `.ai/runs/issue24-add-quote-cls-20261019/metrics.json` 的 210 筆 layout-shift entries 與首頁購物車模板。zh-TW／ja／en 加車失敗各 30 筆、重試各 30 筆的來源均帶 `hadRecentInput=true`，單筆最大分數依序 0.009788／0.010304／0.020661；來源 `UL`、`LABEL`、`BUTTON` 的 y 位移分別約 58／64／58 CSS px，重試方向相反。購物車標題後的 `cartError` alert 插入／移除會推動清單及控制項，歸因信心高；原始來源只有 nodeName 與矩形，未記錄 selector。
- 英文報價重試 30 筆 entries（每筆 0.002721）亦均因 `hadRecentInput=true` 排除。唯一來源為 `SECTION`，矩形由 viewport 底部 y=890.6、約 9.4 px 高轉為零矩形；失敗階段及其他語系沒有同類 entries。來源身分與使用者可見性無法確認，歸因信心低，不據此開產品缺陷。
- 判定採 CLS 與互動後位移分開呈現：建議在後續互動診斷保留操作階段、`hadRecentInput`、來源 selector／矩形、viewport 可視範圍與歸因信心；此處只建立診斷維度，不新增合成分數或門檻。這批位移不計入 CLS，CLS P75 維持原量測結果；不代表所有操作後視覺位移皆可忽略。純證據解讀與文件更新，無產品修正，Issue24 維持 `claimed`。

### Issue24 互動位移語意 selector 與 viewport 可視性重測（2026-10-20）

- runner `.ai/runs/issue24-layout-shift-visibility-20261020/measurement.mjs` 沿用加車 503／重試 200、報價 503／重試 200 的首頁合成旅程，新增語意 selector、唯一性、`hadRecentInput`、source rect 與 viewport 可視面積比例。量測期間 UTC 2026-10-19 16:37:13.024–16:45:48.259；Darwin 25.6.0 arm64、Headless Chrome 155.0.8059.84、1440×900 CSS px、CPU 4×、150 ms latency／1.6 Mbps down／0.75 Mbps up、API 寫入 stub 延遲 120 ms。使用既有 production build，loopback `127.0.0.1:3005`，各樣本新 context、快取未另分層；CPU 型號／記憶體容量未取得。無 SQL、live API、付款、LAN、RUM 或外部網路資料。
- 每語系 30 趟、每趟四階段，共 90 趟／360 階段；各階段樣本均 n=30，API 狀態及互動皆符合預期。P75 使用 nearest-rank；CLS 以 1 秒 gap／5 秒上限 session window 最大值計算並排除 `hadRecentInput=true`。12 組 CLS P75／max 全為 0。共有 210 筆 layout-shift entries，全部為近期輸入後的位移。
- 加車失敗時 `ul.cart-list` 與 `section[aria-labelledby="cart-heading"]` 下的 label／button 各三語均 30/30 與 viewport 相交，向下位移 62／64／58 CSS px；重試成功後方向相反、回復原位。首頁購物車標題後的 `cartError` 提示會推動清單及後續區域。英文錯誤時 `section[aria-labelledby="checkout-heading"]` 由 viewport 可見轉為不可見，重試後重新進入 viewport。英文報價重試另有 30/30 entries；checkout section 從底部 9.4 px 的可見範圍移到 y≈1,367 px、完全不可見。報價失敗與中／日文報價重試沒有 entries。DOM 順序支持錯誤提示推動下方區域的歸因；不代表已採用交互位移門檻。
- 決策：記錄為重現的互動版面穩定性／UX 觀察，不判定為已確認產品缺陷，因此沒有新增回歸測試或產品修正。未把近期輸入位移計入 CLS，不新增分數／通過門檻；數據不代表 INP、field CLS／Core Web Vitals、正式 SLA 或 iPhone Safari／VoiceOver 結果。Issue24 效能項維持未完成與 `claimed`。

### Issue24 三語桌面購物車錯誤回饋固定訊息區（2026-10-20）

- 採用的局部 UX 準則：加車／數量更新失敗提示出現及 200 重試清除時，既有購物車清單的文件座標 `getBoundingClientRect().top + window.scrollY` 保持不變。此座標排除瀏覽器因鍵盤焦點自動捲動造成的 viewport 座標差；準則只適用首頁購物車操作，不是 CLS／INP 門檻或正式 SLA。
- 將首頁購物車錯誤提示集中在標題後 `min-height: 3.5rem` 的 `aria-live="polite"` 訊息區，移除重複錯誤節點。三語 Playwright 測試使用預載合成 cart 與 503→200 數量更新重試，檢查錯誤顯示及清除時清單文件座標不變；所有瀏覽器 API 回覆均為合成 route stub，沒有 SQL／正式 API／付款／LAN／RUM。
- 驗證：Storefront `npm run typecheck` PASS；隔離副本 `npm run build` PASS；測試收集 3 cases PASS；隔離 production preview `http://127.0.0.1:3010` 上三語 Playwright 回歸 3/3 PASS。命令：`KOHAMI_STOREFRONT_ORIGIN=http://127.0.0.1:3010 npx playwright test tests/issue24-accessibility.spec.ts --grep 'desktop cart errors do not move' --config=playwright.config.ts`。隔離 build 來源由本輪工作樹複製至 `/private/tmp/issue24-feedback-stability-20261020/storefront`；API 寫入為 Playwright 503／200 route stub，runtime API origin 指向無服務 loopback `127.0.0.1:59999`。原有 3000／3001 未重啟或覆寫。一次對原有 3000 dev server 的嘗試測到未更新程式；不作修正驗證證據。另一次初始 viewport-top assertion 發現焦點自動捲動，故改為 document-flow 座標後通過。
- 更正上一節來源說明：位移來自首頁購物車區在標題後插入 `cartError` 提示，不是 ProductDiscovery 目錄錯誤訊息；修正只影響首頁購物車 UI。此改動不變更 CLS 計算或 recent-input 排除，也不代表 field CLS／INP、Core Web Vitals、正式 SLA、iPhone Safari 或 VoiceOver 通過。Issue24 仍未完成、`claimed`。

### Issue24 文件座標與互動自動捲動歸因（2026-10-19）

- 以隔離的當前 Storefront production build、loopback `127.0.0.1:3014` 與 Playwright synthetic API stubs 重跑 zh-TW／ja／en 首頁購物／報價旅程，各 30 個新 context，共 90 趟、360 階段；加車與報價各 90 次皆依序得到預期 503／200。期間 UTC：2026-10-19 17:26:51.613–17:35:24.596。Darwin 25.6.0 arm64、Headless Chrome 155.0.8059.84、1440×900 CSS px、CPU 4×、150ms latency／1.6Mbps down／0.75Mbps up、stub 寫入延遲 120ms；快取未分層，CPU 型號／記憶體未取得。無 SQL、live API、付款、LAN、RUM 或外部服務。
- runner 在每階段記錄 `scrollY`、目標元素 viewport top 與 document top；LayoutShift callback 另保留觀察時 `scrollY`、來源 selector／矩形、`hadRecentInput`。三語所有階段的購物車清單與控制項文件座標均 n=30、差值 0；加車時瀏覽器自動捲動 1,241–1,302 px，文件座標不變，且修正後 build 的加車失敗／重試均未觀察到 LayoutShift entries。
- 報價成功重試將 checkout 區文件座標下移 zh-TW 497.6px、ja 516.6px、en 476.6px，各 30/30，期間 `scrollY` 不變；為報價面板插入於 checkout 前方造成的文件流展開。只有 ja 有 90 筆 entries（quote failure 30、quote retry 60），全部 `hadRecentInput=true`，排除於 CLS。12 組 CLS 最近秩 P75 與 max 皆為 0。成功報價內容是預期展開；沒有已採用的互動位移門檻，因此不判缺陷。
- 舊 210 筆 records 未存 scrollY，無法回溯拆分；本輪是購物車固定提示修正後的新 build，不能與舊 build 的 entry 數直接比較。可重現 runner 與完整 JSON 位於 `.ai/runs/issue24-document-coordinate-shift-20261020/`。純量測與證據文件，未改產品行為。這是 localhost synthetic 桌面診斷，非 field CLS／INP、Core Web Vitals 或 SLA 證據；Issue24 維持 `claimed`。

### Issue24 三語桌面核心頁 LCP／CLS 重測與硬體條件（2026-10-20）

- 依 `VALIDATION_PLAN.md` N01 的購物／付款主旅程，量測 zh-TW／ja／en × 首頁搜尋／商品列表、商品詳情、購物車／結帳表單、結帳結果 × 冷／暖快取。每層 n=30，由兩批各 n=15 組成；共 24 層、720 筆計分導覽與 24 次不計分暖身。P75 為 nearest-rank `ceil(n × 0.75)`。量測期間 UTC：2026-10-19 18:32:46–19:06:21.094。
- 環境：MacBook Pro Mac17,8、Apple M5 Pro、18 核心（6 Super／12 Performance）、48 GB RAM、macOS 26.6（25G72）、arm64；Headless Chrome 155.0.8059.84；1440×900 CSS px；CPU 4× slowdown；150 ms latency、1.6 Mbps down／0.75 Mbps up；固定合成 API 120 ms delay。冷載入使用新 context、browser cache disabled；暖載入每個語系／路由使用同一 context 並先暖身一次，確認資產 cache hit。基於候選 `4b6ce282408bb0ddcaf3834e82c50304009941aa` 的 Storefront production build 與 loopback `127.0.0.1` synthetic fixture；未連 SQL、live API、付款、LAN、RUM 或訪客資料。

| 語系 | 路由 | 冷快取 LCP／CLS P75 | 暖快取 LCP／CLS P75 |
| --- | --- | ---: | ---: |
| zh-TW | 首頁搜尋／商品列表 | 220 ms／0 | 220 ms／0 |
| zh-TW | 商品詳情 | 232 ms／0.0034 | 224 ms／0.0034 |
| zh-TW | 購物車／結帳表單 | 1,892 ms／0.0385 | 452 ms／0.0091 |
| zh-TW | 結帳結果 | 216 ms／0 | 216 ms／0 |
| ja | 首頁搜尋／商品列表 | 220 ms／0 | 216 ms／0 |
| ja | 商品詳情 | 220 ms／0.0035 | 232 ms／0.0035 |
| ja | 購物車／結帳表單 | 1,896 ms／0.0392 | 464 ms／0.0099 |
| ja | 結帳結果 | 220 ms／0 | 252 ms／0 |
| en | 首頁搜尋／商品列表 | 212 ms／0 | 212 ms／0 |
| en | 商品詳情 | 224 ms／0.0034 | 220 ms／0.0034 |
| en | 購物車／結帳表單 | 1,892 ms／0.0372 | 444 ms／0.0078 |
| en | 結帳結果 | 208 ms／0 | 212 ms／0 |

- 24 層各有 30/30 LCP／CLS 樣本，30/30 HTTP 200，page errors 0，failures 0；LCP P75 範圍 208–1,896 ms，CLS P75 範圍 0–0.0392，均低於 N01 的 LCP 2.5 秒／CLS 0.1 lab 參考門檻。LCP 使用 navigation 最大 `LargestContentfulPaint.startTime`；CLS 依排除 `hadRecentInput` 的 1 秒 gap／5 秒 cap session window 最大值。載入成功判定包含 `networkidle`、主標題可見、字型完成與 500 ms 觀察窗。
- 重現命令（由 repo root 執行）：`mkdir -p /private/tmp/issue24-storefront-build && git archive 4b6ce282408bb0ddcaf3834e82c50304009941aa apps/storefront | tar -x -C /private/tmp/issue24-storefront-build && ln -s "$PWD/apps/storefront/node_modules" /private/tmp/issue24-storefront-build/apps/storefront/node_modules`；接著 `cd /private/tmp/issue24-storefront-build/apps/storefront && NUXT_API_ORIGIN=http://127.0.0.1:5199 NUXT_SITE_ORIGIN=http://127.0.0.1:3199 npm run build`。回到 repo root，啟動 `node .ai/runs/issue24-core-route-matrix-20261020/api-fixture.mjs` 與 `NITRO_HOST=127.0.0.1 NITRO_PORT=3199 HOST=127.0.0.1 PORT=3199 node /private/tmp/issue24-storefront-build/apps/storefront/.output/server/index.mjs`；執行 `ISSUE24_SAMPLES_PER_CACHE=15 ISSUE24_OUTPUT=.ai/runs/issue24-core-route-matrix-20261020/metrics-batch1.json ISSUE24_STARTED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)" ISSUE24_HARDWARE_JSON='{"model":"MacBook Pro Mac17,8","chip":"Apple M5 Pro","cpuCores":"18 (6 Super, 12 Performance)","memoryGB":48,"os":"macOS 26.6 (25G72)","architecture":"arm64"}' node .ai/runs/issue24-core-route-matrix-20261020/benchmark.mjs`；以 `metrics-batch2.json` 另跑一次，再執行 `node .ai/runs/issue24-core-route-matrix-20261020/aggregate.mjs`。preview／fixture 僅綁 loopback；兩批樣本及 scripts 留存於 `.ai/runs/issue24-core-route-matrix-20261020/`。
- 判定：只完成三語指定桌面路由與條件下的 LCP／CLS synthetic lab 子範圍；本機樣本不代表 field Core Web Vitals 或正式 SLA。沒有完整 INP／field 樣本，Event Timing／TBT proxy 不作正式 INP；RUM／正式網域未啟用且目前不在規劃中。Issue24 效能整體項維持未完成、`claimed`；iPhone Safari／VoiceOver 仍為選配且未驗證。此次無產品程式修改。

### Issue24 購物車冷載入 LCP 候選與資源瀑布診斷（2026-10-20）

- 針對冷快取矩陣中 LCP P75 最高的購物車／結帳表單路由，以 zh-TW／ja／en 各 3 次冷快取導覽記錄 LCP 候選與資源 timing。9/9 頁面、購物車與商品 API 均 HTTP 200，page errors 0；最終候選均為 `p.promotion-hint`。繁中 LCP 1,888–1,920 ms、日文 1,880–1,888 ms、英文 1,876–1,892 ms，較早的主標題候選為 212–228 ms。合成 API 請求於導覽後約 1,696–1,729 ms 開始、1,859–1,893 ms 完成，與最終候選時間接近；這是小樣本時序關聯，不能當作因果證明或 P75 估計。
- MacBook Pro Mac17,8／Apple M5 Pro／18 核心／48 GB／macOS 26.6（25G72）／arm64；Headless Chrome 155.0.8059.84；1440×900；CPU 4×；150 ms、1.6 Mbps down／0.75 Mbps up；cold 為每次新 context 並停用 browser cache；合成 API 延遲 120 ms。量測期間 UTC 2026-10-20 00:37:25–00:38:02.641。production build 與合成 API 僅綁 `127.0.0.1`。重現命令、runner 與原始 JSON 見 `.ai/runs/issue24-cart-lcp-waterfall-20261020/`。
- 沒有重現 API／頁面錯誤，9 筆候選均低於 2.5 秒實驗室工程門檻；本次不改產品程式。少量 lab 資料不是 field LCP／INP、完整 Core Web Vitals 或正式 SLA 證據。Issue24 維持 `claimed`；正式 INP／field 證據仍未驗證。

### Issue24 本機效能證據與網域選配範圍稽核（2026-10-20）

- 重用 `.ai/runs/issue24-core-route-matrix-20261020/metrics-merged.json`，以唯讀程式重算語系 × 核心路由 × 冷暖快取的 24 組 nearest-rank P75。每組 LCP／CLS 各 30 筆，共 720 筆計分導覽、24 次暖身；全數 HTTP 200、無 page error／失敗。LCP P75 範圍 208–1,896 ms；CLS P75 範圍 0–0.03923，沒有超過本機 lab 參考門檻的組別。
- 可重現稽核命令：`node --check .ai/runs/change-2c78e9188180/audit-matrix.mjs`、`node .ai/runs/change-2c78e9188180/audit-matrix.mjs`；兩者 PASS。稽核器唯讀讀取既有彙總 JSON，不改寫原始樣本。
- 量測期間、硬體、OS、Chrome、viewport、CPU／網路節流、合成 API 延遲、cache 定義與原始 runner 已記錄於前述矩陣驗證段。本次只稽核既有 JSON，沒有重跑瀏覽器、修改產品程式或收集訪客資料。
- Issue24 票券已收斂為目前決策：RUM／正式網域／CrUX 不屬本機交付；未來方案只留在 `docs/planning/kohami/options/PUBLIC_DOMAIN_SEO_OPTION.md`。field INP 與整體效能驗收仍未驗證，票券維持 `claimed`；此稽核不宣稱正式 Core Web Vitals、SLA 或完整 WCAG 通過。

### Issue24 本機桌面交付範圍結案與 Issue25 依賴（2026-10-20）

- 依使用者決定，正式網域、RUM、CrUX 與 field INP 不納入目前本機交付計畫；未來評估只保留於 `docs/planning/kohami/options/PUBLIC_DOMAIN_SEO_OPTION.md`。既有 24 組三語／核心路由／冷暖矩陣的 720 筆樣本支持指定條件下 LCP／CLS lab 子範圍；正式 field INP、field Core Web Vitals、正式 SLA 與完整 WCAG 均維持未驗證。
- Issue24 票券 Work status 設為 `resolved`，其適用範圍限於已驗證的桌面本機鍵盤購物／付款旅程及三語 LCP／CLS lab 驗收；不把未驗證項目標為通過。iPhone Safari／VoiceOver 仍為選配且未驗證。
- Issue21、Issue23 本機 mock 範圍及 Issue24 本機桌面範圍的依賴均已滿足；依 S4 順序 Issue25 成為下一張可開始的票。Issue25 監測／告警及復原驗收仍須獨立取得證據；延後的備份來源／目標未定，不能推定備份、還原或 RPO／RTO 通過。
- 本次為決策與狀態文件同步，沒有產品程式或測試變更，沒有啟用 RUM、連接正式網域或收集訪客資料。

### Issue25 本機日誌與告警治理決定（2026-10-20）

- 依使用者既定範圍採用本機合成驗收預設：不啟用 RUM、訪客遙測、正式網域、外部通知、任何日誌 provider、持久化或定期收集，也不建立告警路由。靜態檢視 API／worker Host 的 `Logging.ClearProviders()` 與現有 worker logger 呼叫，確認目前沒有已設定的日誌接收端；所見少數錯誤呼叫只輸出例外型別，並未輸出例外訊息。
- 每次驗收由啟動者即時檢查既有 `/health`、`/ready` 與該次合成驗收結果；需要處理失敗時僅使用安全 HTTP status／固定 reason code，不保存原始輸出。終端／IDE session 是否保存由操作者依本機工具設定處理；未指定具名營運負責人，這是本機開發流程而非正式值班或 SLA。
- 未來若新增持久化、定期收集、告警或正式監測，仍須先明確決定資料欄位與分類、保存／清除方式、讀取權限、具名負責人、異常條件／分級／處理時限與 sink，並確認接收端保存行為。欄位候選僅固定元件／事件代碼、嚴重度、UTC 時間及不可回指真人／交易的合成 run correlation；排除原始 exception/message、HTTP payload／headers、秘密、完整 PII、IP、session、訂單／付款識別碼。
- 備份／還原目標與授權依 Issue23 延後決議，故不推定備份、對帳或 RPO／RTO 通過。Issue25 原驗收仍全未勾選。本輪為文件決定與靜態檢視，未執行產品測試或服務操作。

### Issue25 本機存取控制與秘密處理來源盤點（2026-10-20）

- 靜態檢視 `apps/api/Program.cs`、IdentityAccess 的 endpoint／service、`.gitignore`、fixture runner 與秘密生命週期／SQL 最小權限提案。API 綁定 `127.0.0.1`，檢查 loopback、Host／port；`/api/v1` 非 GET／HEAD 請求要求設定的 Origin 與 antiforgery token。管理端點由 `SecurityService` 明確檢查 session、有效角色／permission、scope、期限及 maintenance 狀態；這不是全域 ASP.NET authorization middleware 的路由覆蓋證據。
- `.gitignore` 忽略 `.local/`；部分 fixture runner 驗證私密檔案非 symlink 且權限為 `0600`，並按 API／worker／native 用途分流設定。這些是指定程式路徑的靜態依據，未證明所有秘密的實際檔案 ACL、程序隔離、OS credential broker、Data Protection key ring 靜態保護／輪替／復原或 SQL effective permissions。對應 SQL least-privilege 與 key／cert lifecycle 文件仍標示 `PROPOSED／NOT_EXECUTED`。
- 未讀取 `.local/` 值，未連 SQL、啟停服務、執行 native authority／Owner、migration、grant 或資料寫入。沿用既有 `dotnet tests/Native/bin/Release/net10.0/NativeTests.dll --safe-diagnostic` 僅驗 Native 診斷理由碼白名單，不涵蓋 API 權限矩陣或秘密儲存。
- Issue25 仍未結案；原四項驗收保持未勾選。本次只整理本機程式來源與證據邊界，沒有新增功能或宣稱秘密保管／有效權限完整通過。全域 Impact Graph／Retrieval Index 仍不完整或過期；本輪僅人工檢視上述直接消費者與文件，不宣稱全域 coverage 或 `READY`。

### Issue25 API 路由授權矩陣來源盤點（2026-10-20）

- 靜態比對 `apps/api/Program.cs`、IdentityAccess／Commerce／Catalog route maps 與 handlers，以及 `tests/Identity/SecurityRulesTests.cs`。分類涵蓋入口 Origin／antiforgery、登入與 email challenge、grant 管理、公開 Catalog、SKU stock、報表／對帳／退款／履約管理，以及購物／訂單／訪客能力路由；每組控制與缺口見 [Issue25](../issues/kohami/25-commerce-security-monitoring-recovery.md#本機-api-路由授權矩陣來源盤點2026-10-20)。
- 現有 Identity 規則單元測試涵蓋 dataset／resource scope 比對、permission allowlist、角色拒絕、未知 resource resolver fail closed 與密碼 blocklist。它不證明全部 HTTP endpoints 採用正確授權，也不涵蓋全路由匿名／錯誤角色／到期或撤銷 grant／maintenance／session 狀態矩陣。
- 本輪未啟動服務、未連 SQL、未讀私密設定，未改 code／grant／maintenance／資料，亦未執行產品測試。API endpoint × actor × scope 的 HTTP／SQL 負向 acceptance 仍未驗證；Issue25 四項原驗收維持未勾選。
- Project Intelligence 顯示 stale；Impact Graph 對 API／consumer 的全域覆蓋仍不完整。本輪只人工檢視列出的直接 route、handler、測試與文件，不宣稱全域 coverage 或 Change Impact `READY`。

### 本機合成錢包扣抵程式路徑（2026-10-20）

- Commerce 為會員本人 checkout 增加餘額查詢與付款分支：API 從已驗證會員識別限定讀取本人錢包；結帳扣抵透過 SQL stored procedure，在既有 Commerce transaction 內以餘額條件更新餘額、追加扣抵帳本，並建立 Wallet collection／付款文件、消耗庫存 reservation、更新 Checkout／Order。訪客仍走原 mock PaymentAttempt。API principal 僅有錢包讀取與 debit procedure 執行權限，沒有直接餘額寫入或入金 endpoint。錢包付款訂單在 wallet 專用退款流程完成前，既有外部 mock refund route 明確拒絕。
- 新增 migration `0013_member_wallet_ledger`；Native authority SQL endpoint 改由 runtime profile 傳入，僅接受 `localhost,14336–14399/KohamiM0`、strict TLS、憑證 hostname `localhost`，避免 Native CLI 原固定連至 14334 而錯操作其他 fixture。
- 驗證命令與結果：`dotnet build src/Modules/Commerce/Infrastructure/Commerce.csproj -c Release --no-restore -v:minimal -m:1 /nodeReuse:false` PASS，0 warnings／0 errors；`dotnet build tests/Native/NativeTests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal` PASS，0 warnings／0 errors；`dotnet tests/Native/bin/Release/net10.0/NativeTests.dll` PASS 39 checks；`dotnet tests/Native/bin/Release/net10.0/NativeTests.dll --native-target` PASS；`dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll` PASS 35 規則案例，SQL product checks 為 `NOT_EXECUTED`（缺少 `KOHAMI_M0_TEST_CONFIG`）；`dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll --wallet-safety-contract` PASS（程式來源契約）；`git diff --check` PASS。
- 限制：以上 build／程式契約測試不驗 SQL stored procedure 實際執行、餘額不足回滾、並行雙花、API／瀏覽器會員購物或 migration 套用。合成餘額的測試入帳尚無可用入口，Native host broker／issuer 設定亦不存在；沒有建立 Owner、解除 maintenance、連線寫入或套用 migration。因此此項只完成程式實作與靜態／純程式驗證，不宣稱 live SQL 結帳及付款成立。Issue26 保持未結案；Issue25 四項驗收維持未勾選，備份／還原、完整授權矩陣、商務警示及 RPO／RTO 未驗證。

### kohami-sql 本機錢包遷移與原生授權阻擋（2026-10-20）

- 依使用者選擇，只用合成帳戶／測試入帳。新增 `wallet_synthetic_credit` 專用 SQL procedure 與 Commerce 測試 runner；入帳限 dedicated fixture 的 `demo_m0` marker、活躍 `@example.test` 會員、明確 `synthetic-test-credit:` source、每次上限 NT$50,000、交易內記帳及防重放，只有 `m0_migrator` 可執行，API 沒有入金 endpoint／權限。新增 `migrate-kohami-sql <repo-root>` 只讀 `.local/kohami-sql/m0`，硬性核對 `localhost,14349`、`KohamiM0`、`demo_m0` 與 marker fingerprint；不使用舊 `.local/m0`／14334 命令。
- `dotnet tools/native-operations/bin/Release/net10.0/Native.dll migrate-kohami-sql "$PWD"`：PASS，marker 驗證後套用 `0013_member_wallet_ledger` 到指定 fixture；再跑一次 PASS，來源 hash 一致，沒有 reset、Owner bootstrap 或 gate 變更。0013 source hash：`48d6dd679bb1f0a0c59750d68fa19d70e2bc6db1bbfaea9af35be6a1af47f935`。
- Native／Commerce／API Release 建置：分別執行 `dotnet build tools/native-operations/Native.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`、`dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`、`dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`，均 PASS，0 warnings／0 errors。`dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll --wallet-safety-contract` PASS；既有 Commerce 規則測試 PASS 35；SQL 實測 `--wallet-credit-missing-user .local/kohami-sql/m0/fixture-private.json` PASS（拒絕不存在合成會員，無資料寫入）。
- 新本機 API 5084 曾短暫供 readiness 驗證：`GET /health` 200、`GET /ready` 200；原 API 5083 未重啟，對新版 contract 的 `/ready` 為 503。之後依專案 `scripts/m0/run-local.py` 以 `https://localhost:3004`／`http://127.0.0.1:5085` 持續啟動本機 Storefront/API；API `/ready` 200，zh-TW／ja／en 首頁 SSR 均 HTTP 200、有商品內容。health 仍標示 `activation=requires-native-reconciliation`；`ops.Inspect` 唯讀顯示指定 DB fingerprint、epoch 1、`Maintenance=True`。
- 原生 issuer broker 編譯後，Secure Enclave 建立回 `SECURE_ENCLAVE_KEY_CREATE_FAILED_OSSTATUS_-34018`。未產生 issuer 公鑰、HostConfig、Owner／permit 或 native credential；未改 maintenance。故成功入帳、SQL Commerce checkout、餘額不足 rollback、並行雙花、帳本／訂單／庫存原子驗證、Storefront browser flow 均未執行。Issue26 未結案；Issue25 監測／復原與完整授權矩陣未驗證。
- 全程未讀取／列印 `.local/` 私密值，未連第三方付款服務；原 3000／3001／3002／3003、5083 服務未停止。新的 3004／5085 本機預覽保持執行；沒有宣稱正式付款、field Core Web Vitals 或 SLA。

### 本機開發 bootstrap 與合成錢包結帳 follow-up（2026-10-20）

- 執行 `dotnet tools/native-operations/bin/Release/net10.0/Native.dll local-development-bootstrap "$PWD"`，PASS。命令確認 dedicated marker 後，只在 `localhost:14349/KohamiM0/demo_m0` 建立 `kohami-local-member@example.test` 合成會員、NT$50,000 測試額度及稽核記錄，並於同一交易解除本機 maintenance gate；沒有建立 Owner/native authority。密碼保留於 ignored `.local/kohami-sql/m0/local-development-credentials.json`，mode 0600，未讀出或記錄。
- 透過本機 Storefront proxy 呼叫現有 API：登入、`/wallet/current`、在庫商品、建立購物車、加商品、報價、建立 checkout/order 均成功；樣本商品報價 NT$1,940。`POST /checkouts/{id}/payment-attempts` 回 HTTP 503 `SERVICE_UNAVAILABLE`。付款後訂單仍為 `AwaitingPayment`，payment state 為 `Created`，錢包仍 NT$50,000，扣款差額 0；未重試，因此沒有重複扣款。另一次短暫 loopback 預覽重現相同 503 與回滾。
- API `GET /health`、`GET /ready` 均 HTTP 200；`https://localhost:3004/zh-TW/`、`/ja/`、`/en/` 均 HTTP 200，商品代理回 45 項。此驗證涵蓋 API/SSR 與經 proxy 的合成流程，不是實際瀏覽器／Playwright 付款驗收。直接唯讀 SQL 權限檢查顯示 `m0_api` 對 `m0commerce.Collections` 與 `m0commerce.RefundUnits` 的有效 `INSERT` 均為 0，而 debit procedure `EXECUTE`、WalletAccounts `SELECT` 均為 1；相同 API principal 執行 NT$1 wallet debit 後在 SQL transaction rollback 成功。這確認 503 的根因為 wallet payment 之後插入 collection／refund units 時缺少 API role grants。無第三方付款連線；修正與 dedicated SQL 重驗仍待完成。Issue26 保持 `claimed`，完整錢包結帳未通過。
