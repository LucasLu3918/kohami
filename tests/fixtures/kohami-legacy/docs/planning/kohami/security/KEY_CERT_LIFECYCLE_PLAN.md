# 本機 key／cert 生命週期提案

2026-10-13｜PROPOSED／READY_FOR_REVIEW｜NOT_EXECUTED

適配性：有條件適合。以持久化、用途分離、受控secret來源與輪替/復原測試補齊身份機密，符合local-only範圍。2026-10-20 本機開發模式不使用 Owner native issuer；一般 local session／測試會員由既有本機 API key ring 處理。macOS credential store／Secure Enclave 僅作 Owner native authority 選配，不能阻擋 localhost SQL/API 及合成購物驗收。正式環境密鑰生命週期仍未驗收；不假定官方框架已提供完整 broker，也不把本機帳密保管視為正式保護。

## 1. 用途與存取者

| ID／用途 | 保存與消費者提案 | 輪替／遺失 |
| --- | --- | --- |
| KEY-01 Session Data Protection ring | 受限、持久化且明確加密的本機ring；.NET身份/cookie/antiforgery所需provider | 常規rotation保留舊讀key；事故可撤payload與SecurityVersion，丟失不默默建空ring |
| KEY-02 Session Data Protection wrapping certificate | session ring使用受限加密PFX／OS store與broker；密碼不與檔案同處存 | 輪替保留可解舊ring的私鑰；不可只保留public cert宣稱能restore |
| KEY-04 Browser HTTPS cert | 本機localhost入口3000，Nuxt/受控入口服務讀取；開發憑證與資料加密用途分離 | 到期換部署cert，OS trust需要原生確認；不全域清其他專案dev certs |
| KEY-05 SQL TLS cert | SQL服務/server持有私鑰，client僅trust chain與host驗證；不與Browser/DP混用 | 獨立server配置／reload與.NET driver驗證；舊連線退休，不送private cert給browser |
| KEY-06 SQL service credentials | 每principal獨立secret ref，backend/worker/工具只取自己所需；Nuxt client永不含SQL密碼 | 受控輪替並測正確role後收回舊login，不能只改file但DB login仍舊密碼 |
| KEY-07 Native permit issuer與audit保護key | 僅native authority，API/worker不能取；同UID不保證硬隔離；broker須fail closed | 事故撤permit epoch、保持maintenance；保留audit驗證public refs，不把issuer秘密授給網站 |
| KEY-08 離線復原bundle unlock材料 | 密封加密副本與獨立解鎖方式／actual storage待指定，非repo或產品DB | restore演練屬選配改善；若採用，缺unlock不聲稱有完整復原；持有bundle不等於native授權 |

這裡只定義credential reference名字/消費者，不產或存secret值。候選`.local/security`目錄0700／檔案0600並Git-ignore只能作起點；授權備份在repo外加密，不能因此稱同登入user的其他程序讀不到。範圍信任本機操作者；若要抵擋root/已被接管的授權程序或多人共機，需另加OS/service隔離，不能靠加密ring或角色名稱承諾。

## 2. Data Protection與credential機密

Microsoft說明指定持久化位置後靜態加密不再自動成立，因此候選明確PersistKeysToFileSystem＋ProtectKeysWithCertificate、穩定SetApplicationName/獨立環境namespace與purpose，而非只設directory。[Microsoft配置](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0)（核對2026-10-13）。ring本身仍需有限寫權，僅加密不防止有寫權者注入key。

seed、復原碼及其專用保護方式屬未採用的登入強化細節，集中於[選配清單](../options)；本計畫不定義其資料模型或復原流程。

常規DP key預設lifetime90天是框架行為參考，不是本產品已採數值；到期與revocation/deletion不同。[Microsoft key management](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-management?view=aspnetcore-10.0)。舊ciphertext/ring依賴舊解密key，輪替不能全刪舊key；保留期限與artifact backup依賴須確認，不能永久retain或任意清理。

包裝cert輪替：先在restricted store加入new私key/cert→app new-write選new／old-read保留new+old集合→重啟/舊cookie與session密文測試→備份checkpoint→依引用/保留政策退舊。UnprotectKeysWithAnyCertificate是解舊ring支持，**不是既有ring文件自動全部重加密**。若需重包舊ring，另規劃安全工具/驗證，不用覆蓋檔案或隨手刪除實現。

失去Session ring：保留DB，以維護＋撤sessions/proofs與新security epoch重新初始化，經原生確認後重新登入；不提高業務授權。登入強化所用機密材料的遺失與復原細節見[選配清單](../options)，本計畫不把它列為現行基線。禁止DangerousUnprotect/忽略revocation作為正常救援捷徑。

## 3. Secret取得、備份與啓動

偏好broker讀取OS credential refs，必要PFX passphrase以TTY/FD或受控程序memory注入，不進CLI argv、源碼、前端runtime config、stdout/log/traces/截圖。若原生broker不可用，候選替代是repo外加密bundle＋每次人工unlock；替代不自動啓用、不用無密碼私鑰文件當降級。不要求使用者把secret貼聊天。

.NET User Secrets不是加密的可信secret vault；不能因存在Secret Manager即宣稱滿足靜態保護。[Microsoft User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0)。實際master/SA密碼、PFX、seed、issuer key不讀進規劃包；example只ref無值。

啓動健康gate需驗證：environment/instance／Dataset binding、已初始化security epoch、expected key IDs與必要舊decrypt refs、可讀取並驗證的sentinel（不回明文）、文件ACL／issuer consumer隔離、HTTPS/SQL配置、profile版本。缺key／錯namespace／無法解sentinel／authority不明→KEYSTORE_BLOCKED且不簽身份、不dispatch；不能調用框架自動建空ring讓網頁「看起來啟動成功」。public catalog可按明示degraded能力維持，受保護路由關閉。

備份bundle包括必要ring與old私key、加密DB backup引用、版本/用途/namespace/hash清單、操作audit checkpoint／cutoff、復原碼consumed/credential撤銷與grant hold ledger；raw密碼/seed不入清單。副本加密與獨立unlock需演練，預先拿回公鑰不能解密PFX。RPO/RTO未驗證，不能由備份命令退出0宣稱滿足。

獨立restore先維持gate/dispatch disabled，保留舊seedenvelope原IdentityNamespace以便技術驗證；不由任意DB欄位選擇key路徑。重套較新撤權/credential replacement/consumed proof/hold，生成新運行epoch使舊session/permits不能復活；隔離clone不開放業務登入或觸發舊付款。是否保留仍有效credential或全部重綁依事故/撤銷台賬與明確native處理，不從舊backup自行判定；缺台賬、private解密key或hash即BLOCKED。

## 4. HTTPS與SQL TLS

候選使用.NET localhost開發cert作Browser開發入口，固定Host/Origin仍`https://localhost:3000`；證書額外SAN不自動加入應用allowlist。官方dev-certs的生成/導出/trust與--clean行為不同，`--trust`改本機信任且`--clean`可能影響其他開發證書，本項目不以全域clean作輪替方案。[dotnet dev-certs](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-dev-certs)。Nuxt讀加密PFX/PEM／broker passphrase的實際入口需Foundation綁定，不假造可執行的導出命令或將password放argv。

SQL產品候選Encrypt=True/TrustServerCertificate=False、server cert有效期/chain/SAN與連接hostname一致。現有KohamiTest localhost自簽開發例外保留，但不是產品TLS驗證PASS。SQL cert與Browser cert/private keys分開，SQL Linux配置與服務權限需按官方條件執行驗證。[SQL Linux encrypted connections](https://learn.microsoft.com/en-us/sql/linux/security/encrypted-connections?view=sql-server-ver16)。如果只用既有開發例外繼續本機實驗，必須明確例外profile、loopback/容器scope/到期，不用靜默fallback替代候選policy。

## 5. 驗收（全部NOT_EXECUTED）

| ID | 判準 |
| --- | --- |
| KEY-AC01 | session ring的用途／namespace隔離、restart後舊session可讀、權限／秘密無洩漏 |
| KEY-AC02 | 明確保護at rest、key注入ACL與broker邊界；缺key/sentinel不建空ring、不dispatch |
| KEY-AC03 | 新寫/舊讀輪替及舊證private依賴，不能假裝自動重包；安全退舊有引用/備份證據 |
| KEY-AC04 | cookie/seedkey丟失救援不同，不DangerousUnprotect、不改passwordhash或復活grant |
| KEY-AC05 | 選配：加密backup＋獨立unlock真restore、原namespace/new運行epoch/後cutoff ledger核對；不屬 Foundation/M4 整體驗收 |
| KEY-AC06 | HTTPS trust/expire/Host/SAN/Nuxt實際入口與SQL chain/hostname驗證，例外不可靜默降級 |
| KEY-AC07 | 不同SQL/issuer refs只有指定consumer；issuer/Keychain的隔離與不可匯出控制依合成測試核對 |
| KEY-AC08 | 原生操作/log/health只有狀態/refs，無PFX/private/seed/token/SQL密碼；確認為僅本機mock |

OS broker/隔離與backup地點／人員、保留/輪替數值未採用；source實際平台支持／SDK/Node/SQLdriver配置未綁定，依各案例完成Foundation/M4證據。


## Owner native issuer 選配初始化

本機 development session key ring 由既有本機設定建立，僅用於 localhost mock/API，不簽發 Owner permit。若未來啟用 Owner native issuer，仍須由受控 OS provisioning 建立，確認 operator、broker 隔離、精確環境／instance、security journal；不能以 issuer 自行簽核。該根信任／helper接口仍屬選配，不能作為本機顧客購物服務的啟動條件。

正常輪替可依已採用的provider策略產生替代key，但必須在已初始化／受控ring與namespace中，記錄可信新增key/包裝依賴並更新健康metadata。Missing/corrupt ring與授權正常rotation分開裁決；更換工作路徑、restore舊設定或未知目錄不能觸發重新初始化。完整初次provisioning、正常rotation與missing-key拒絕測試皆需Foundation證據，框架預設不能單独證明這些門禁。
