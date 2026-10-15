# SQL 最小權限、維護與復原提案

2026-10-13｜PROPOSED／READY_FOR_REVIEW｜NOT_EXECUTED

適配性：有條件適合。獨立 API／worker／migration／backup／native維護 principal 能縮小錯用範圍；實際object grants、EF transactions與signed procedures須依物理schema/M0綁定。本輪不執行CREATE LOGIN/GRANT/REVOKE或migration，不修改現有SA、db_owner或DB資料。

## 1. 現有基準與目標

[既有隔離SQL](../../../infra/sql-server/README.md)只證明KohamiTest的`kohami_test`非sysadmin、具db_owner、rollback/restart smoke可用；不是產品最小權限或TLS/restore/並行證據。現有Apple Silicon amd64模擬測試不等於官方x64平台保證。保留實驗，不把其登入或包含SA的私有env交給產品程序。

候選另設產品DB `KohamiLocalDemo`（名稱提案，未建立），與KohamiTest分開；可先用同一專用SQL instance，不增雲端費用。Native/provisioning確認instance/DB指紋與mock allowlist，不以DB名字像Kohami就允許任何SQL。跨DB資料不可授給runtime；必要master/tempdb/system metadata可見範圍需實測，不能宣稱所有instance資訊都隱藏。

SQL role是服務權限，網站Owner/Support/Member是應用權限；連線pool用service principal不會自動按網站User/role切成SQL user。物件／Dataset／MFA/權限與PII閘門仍由後端每request驗證，SQL schema權限不能代替它；服務程序被接管時可能存取其被授予的資料，這是本機威脅範圍限制。

## 2. 權限矩陣

下表為待採用logical role，登入名／secret ref與object/procedure allowlist待source綁定；`public`必要基礎權限不等於授予任意資料讀寫。

| 角色 | 允許的最小作業提案 | 禁止／界線 |
| --- | --- | --- |
| api_runtime | 指定catalog/order/cart等mutable表的必要SELECT/INSERT/UPDATE及受控刪除；身份/ledger寫入依精確procedure或table/column allowlist | 不含db_owner/db_datareader/db_datawriter/DDL、GRANT/ALTER ROLE、backup/restore、ops bootstrap執行；Audit／已成立價格快照／financial ledger不得直接UPDATE/DELETE |
| worker_runtime | 讀指定pending work/outbox、claim/lease、處理結果與inbox/ledger append等必要受控procedure | 不改Users/Grants/MFA，不執行退款新請求／native ops、DDL、權限管理、backup/restore；已Started只回原source結果 |
| migrator | maintenance期間產品DB內指定schema的DDL及依candidate核准的migration DML | 非server role/sysadmin；不長期掛API；若工具確需db_owner則列獨立time-bound例外且runtime仍禁止，不能自動預設 |
| backup_operator | 產品DB BACKUP所需權限（例如專用db_backupoperator）及受控檔案目的地 | 不授RESTORE/server login/DDL或任意其他DB；DB backup檔案由SQL服務帳號寫，檔案讀取/加密另管 |
| restore_operator | 原生人工指定新target DB／instance的復原作業，依SQL要求短期權限與核准scope | 不交給產品API；不能從runtime直接RESTORE／覆寫原DB／任意DB；SQL所需權限可能更廣，工具allowlist及OS gate不等於SQL硬隔離 |
| native_operations | M0僅涵蓋grant維護／key rotation／restore所需的精確procedures與最小狀態投影；Owner bootstrap/recovery procedures 列未來選配 | 不授一般DML/DDL或db_owner，不能從網站呼叫；stored procedure必須核permit/action/Dataset/epoch及記Audit，不依client flag升權 |
| report_reader | 只讀指定遮罩／彙總view，供必要診斷；與網站Analyst是否使用分開 | 不讀PasswordHash/seed/PII raw tables，不INSERT/UPDATE/DELETE/execute privileged commands |
| instance_provisioner | 原生操作者初始化必要DB/login/role/cert/migration權限，依實際SQL要求 | SQL高權限只在明確作業使用，非應用連線；不把既有SA登入當永久API credential |

Microsoft說明permissions及db_owner/db_backupoperator等角色能力不同；既有固定role不能被稱為「非sysadmin所以最小權限」。[SQL permissions](https://learn.microsoft.com/en-us/sql/relational-databases/security/permissions-database-engine?view=sql-server-ver16)、[Database roles](https://learn.microsoft.com/en-us/sql/relational-databases/security/authentication-access/database-level-roles?view=sql-server-ver16)（核對2026-10-13）。

## 3. Schema／寫入策略與隔離

候選object groups：catalog/order mutable、identity安全資料、immutable ledger/snapshot/Audit、outbox/inbox、ops maintenance及report views。這是分類，不新建多服務或假定物理schema已採用；沿既有[SQL規劃](SQL_SCHEMA_PLAN.md)映射真表與EF source。

對mutable資料可用有限object/column DML；涉及不可變Ledger、授權/復原版本與native操作，候選精確EXECUTE-only procedure使DB內約束/transaction/Audit有單一入口。不是強制所有EF CRUD改SP；若選SQL module signing或ownership chain，需核簽章／owner權限與跨schema效果，不把EXECUTE-only誤稱一定不能間接存取其他物件。TRUSTWORTHY與跨DB ownership chaining預設保持OFF；需要例外另審，不放寬為了讓腳本成功。

原單快照與Audit只追加／受控必要資料處理，商品改價不覆訂單。DELETE僅明確可刪mutable資料；financial/安全稽核資料保留策略仍依已規劃規則。具權資料請求也只能走受控程序與必要例外，不給服務任意批次抹除紀錄。

每principal獨立secret reference，不共用SA/test password或同一連線字串。API/worker只由backend注入自己的CredentialRef；前端、Nuxt public runtimeConfig或收件匣沒有SQL credential。Native操作及migration憑證不注入API service，具體OS/broker isolation需M0實測，未驗證保持BLOCKED。

## 4. Provision／migration／rotation

規劃順序：native確認instance fingerprint/產品DB scope→provision精確login/user/custom role→受控migration→授object/procedure allowlist→以runtime credential跑正/負向scope probe→移除migration/native暫時authority→啟API/worker。Schema變更使allowlist/role manifest hash過時時阻擋啟動，不用授db_owner補救未知SQL錯誤。

SQL password rotation候選新principal版本或變更現登入密碼，兩種需先定實際driver/pooling切換策略；新ref/credential先驗角色與TLS／health，再drain舊connection pool、撤舊login/secret。ALTER LOGIN密碼不保證已存在連線全部中斷，因此需實測撤舊session、無pending operation重派與connection health；不可只更新本機file。

migration階段需maintenance/fencing／backup checkpoint／精確candidate與人工範圍核准；失敗保持維護，不能把不可逆migration稱transaction rollback一定可回復。補救依已審閱forward-fix或獨立restore，不從一般demo reset還原產品DB。

## 5. Backup／restore權限與TLS

BACKUP與RESTORE需要不同SQL權限，restore至新DB與既有DB亦不同；依官方能力核對後授必要native operator，不能假定backup_operator可以restore。[SQL RESTORE permissions](https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-transact-sql?view=sql-server-ver16)。如所需server權限廣於單一target，另記限制並由原生tool/OS作業確認，不能聲稱SQL ACL本身已限定只restore那個名字。

只允許新的獨立target，不用WITH REPLACE覆寫原DB或任意target；SQL backup路徑、宿主mount及service讀寫ACL需精確allowlist且不接受client path。復原過程維持API/worker/financial dispatch disabled，檢查key/ring／namespace、schema/hash與後cutoff撤銷/hold/credential消耗台帳，最後以原生permit核准當前最小grant，舊backup角色/帳號不能自動開放。

採用的產品TLS候選Encrypt=True/TrustServerCertificate=False，驗chain/SAN/hostname與SQL服務持有的私鑰；現有KohamiTest自簽TrustServerCertificate=True只屬明示開發例外，不改既有連線或假稱產品通過。[key/cert詳規](KEY_CERT_LIFECYCLE_PLAN.md)列輪替／故障與Nuxt入口分離。

## 6. M0負向scope probes（全NOT_EXECUTED）

| ID | 驗證 |
| --- | --- |
| SQL-AC01 | API/worker必要讀寫成功，其他DB/表資料／secret／權限管理／DDL拒；實際effective permission報告不含secret |
| SQL-AC02 | API與worker無db_owner/寬固定role/sysadmin，snapshot/Audit直接UPDATE/DELETE拒，native ops不可從runtime執行 |
| SQL-AC03 | exact procedures與module signing/ownership chain限制核對，TRUSTWORTHY/crossDB chain未放寬 |
| SQL-AC04 | migration僅maintenance已核scope，結束撤credential/pool；不能給runtime永久高權限救錯 |
| SQL-AC05 | backup與restore分開、target新DB，錯instance/DB/path拒、原DB不覆寫，ledger/keys缺失維持disabled |
| SQL-AC06 | password/login輪替後舊pool/session確實不繼續授權，pending/Unknown source不重派 |
| SQL-AC07 | product TLS chain/hostname拒絕案例與明示test例外，ARM模擬結果不冒稱原生x64保證 |
| SQL-AC08 | connection/ref只到各backend consumer，API/worker拿不到native credential，OS broker界線有實證 |

SQL角色/物理表/程序／命令/TLS/broker仍需source綁定與採用，本次無產品DB／login變更或真正permissions probe。
