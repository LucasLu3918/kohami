# 本機 key／cert 生命週期提案

2026-10-13｜PROPOSED／READY_FOR_REVIEW｜NOT_EXECUTED

適配性：有條件適合。以持久化、用途分離、受控secret來源與輪替/復原測試補齊身份機密，符合local-only範圍。偏好macOS credential store＋受控native broker；.NET/Nuxt/容器實際接口、OS user-presence/ACL與開機方式仍需M0證據，不假定官方框架已提供完整broker。無broker證據不自動改為明文/環境dump。

## 1. 用途與存取者

| ID／用途 | 保存與消費者提案 | 輪替／遺失 |
| --- | --- | --- |
| KEY-01 Session Data Protection ring | 受限、持久化且明確加密的本機ring；.NET身份/cookie/antiforgery所需provider | 常規rotation保留舊讀key；事故可撤payload與SecurityVersion，丟失不默默建空ring |
| KEY-02 TOTP專用protect ring | 與Session獨立provider/ring/用途/包裝憑證；DB只存seed encrypted envelope及版本/context | seed仍要可解密以驗證，不能只hash；遺失只能受控重綁、不還原舊seed或跳MFA |
| KEY-03 Session/TOTP包裝cert private keys | 兩用途各自cert/key reference，受限加密PFX/OS store與broker；密碼不與檔案同處存 | 輪替保留可解舊ring的私鑰；不可只保留public cert宣稱能restore |
| KEY-04 Browser HTTPS cert | 本機localhost入口3000，Nuxt/受控入口服務讀取；開發憑證不作TOTP/DB加密key | 到期換部署cert，OS trust需要原生確認；不全域清其他專案dev certs |
| KEY-05 SQL TLS cert | SQL服務/server持有私鑰，client僅trust chain與host驗證；不與Browser/DP混用 | 獨立server配置／reload與.NET driver驗證；舊連線退休，不送private cert給browser |
| KEY-06 SQL service credentials | 每principal獨立secret ref，backend/worker/工具只取自己所需；Nuxt client永不含SQL密碼 | 受控輪替並測正確role後收回舊login，不能只改file但DB login仍舊密碼 |
| KEY-07 Native permit issuer與audit保護key | 僅native authority，API/worker不能取；同UID不保證硬隔離，user-presence/broker需實測 | 事故撤permit epoch、保持maintenance；保留audit驗證public refs，不把issuer秘密授給網站 |
| KEY-08 離線復原bundle unlock材料 | 密封加密副本與獨立解鎖方式／actual storage待指定，非repo或產品DB | 必須restore演練能解舊key，缺unlock不聲稱有完整復原；持有bundle不等於native授權 |

這裡只定義credential reference名字/消費者，不產或存secret值。候選`.local/security`目錄0700／檔案0600並Git-ignore只能作起點；授權備份在repo外加密，不能因此稱同登入user的其他程序讀不到。範圍信任本機操作者；若要抵擋root/已被接管的授權程序或多人共機，需另加OS/service隔離，不能靠加密ring或角色名稱承諾。

## 2. Data Protection與TOTP隔離

Microsoft說明指定持久化位置後靜態加密不再自動成立，因此候選明確PersistKeysToFileSystem＋ProtectKeysWithCertificate、穩定SetApplicationName/獨立環境namespace與purpose，而非只設directory。[Microsoft配置](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0)（核對2026-10-13）。ring本身仍需有限寫權，僅加密不防止有寫權者注入key。

Session與TOTP分開建provider/ring；TOTP purpose綁定server選定IdentityNamespace、DatasetId、UserId、credential version/用途，這些不可由request決定，防止搬移envelope至另一帳號。DI/provider如何在實際source配置仍UNBOUND；不把「使用Identity」視為seed已加密。復原碼只安全hash，TOTP seed可解密，兩者不同；不把密碼hash改成可逆加密。

常規DP key預設lifetime90天是框架行為參考，不是本產品已採數值；到期與revocation/deletion不同。[Microsoft key management](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-management?view=aspnetcore-10.0)。舊ciphertext/ring依賴舊解密key，輪替不能全刪舊key；保留期限與artifact backup依賴須確認，不能永久retain或任意清理。

包裝cert輪替：先在restricted store加入new私key/cert→app new-write選new／old-read保留new+old集合→重啟/舊cookie及舊TOTP密文測試→備份checkpoint→依引用/保留政策退舊。UnprotectKeysWithAnyCertificate是解舊ring支持，**不是既有ring文件自動全部重加密**。若需重包舊ring，另規劃安全工具/驗證，不用覆蓋檔案或隨手刪除實現。

失去Session ring：保留DB，以維護＋撤sessions/proofs與新security epoch重新初始化，經原生確認後重新登入；不提高業務授權。失去TOTP ring/private unwrap key：不能解舊seed，即使DB backup還在也不能復原驗證器；隔離舊credential，走native復原/新TOTP綁，再驗權限。禁止DangerousUnprotect/忽略revocation作為正常救援捷徑。

## 3. Secret取得、備份與啓動

偏好broker讀取OS credential refs，必要PFX passphrase以TTY/FD或受控程序memory注入，不進CLI argv、源碼、前端runtime config、stdout/log/traces/截圖。若原生broker不可用，候選替代是repo外加密bundle＋每次人工unlock；替代不自動啓用、不用無密碼私鑰文件當降級。不要求使用者把secret貼聊天。

.NET User Secrets不是加密的可信secret vault；不能因存在Secret Manager即宣稱滿足靜態保護。[Microsoft User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0)。實際master/SA密碼、PFX、seed、issuer key不讀進規劃包；example只ref無值。

啓動健康gate需驗證：environment/instance／Dataset binding、已初始化security epoch、expected key IDs與必要舊decrypt refs、可讀取並驗證的sentinel（不回明文）、文件ACL／issuer consumer隔離、HTTPS/SQL配置、profile版本。缺key／錯namespace／無法解sentinel／authority不明→KEYSTORE_BLOCKED且不簽身份、不dispatch；不能調用框架自動建空ring讓網頁「看起來啟動成功」。public catalog可按明示degraded能力維持，受保護路由關閉。

備份bundle包括必要ring與old私key、加密DB backup引用、版本/用途/namespace/hash清單、操作audit checkpoint／cutoff、復原碼consumed/credential撤銷與grant hold ledger；raw密碼/seed不入清單。副本加密與獨立unlock需演練，預先拿回公鑰不能解密PFX。RPO/RTO未驗證，不能由備份命令退出0宣稱滿足。

獨立restore先維持gate/dispatch disabled，保留舊seedenvelope原IdentityNamespace以便技術驗證；不由任意DB欄位選擇key路徑。重套較新撤權/credential replacement/consumed proof/hold，生成新運行epoch使舊session/permits不能復活；隔離clone不開放業務登入或觸發舊付款。是否保留仍有效credential或全部重綁依事故/撤銷台賬與明確native處理，不從舊backup自行判定；缺台賬、private解密key或hash即BLOCKED。

## 4. HTTPS與SQL TLS

候選使用.NET localhost開發cert作Browser開發入口，固定Host/Origin仍`https://localhost:3000`；證書額外SAN不自動加入應用allowlist。官方dev-certs的生成/導出/trust與--clean行為不同，`--trust`改本機信任且`--clean`可能影響其他開發證書，本項目不以全域clean作輪替方案。[dotnet dev-certs](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-dev-certs)。Nuxt讀加密PFX/PEM／broker passphrase的實際入口需M0綁定，不假造可執行的導出命令或將password放argv。

SQL產品候選Encrypt=True/TrustServerCertificate=False、server cert有效期/chain/SAN與連接hostname一致。現有KohamiTest localhost自簽開發例外保留，但不是產品TLS驗證PASS。SQL cert與Browser cert/private keys分開，SQL Linux配置與服務權限需按官方條件執行驗證。[SQL Linux encrypted connections](https://learn.microsoft.com/en-us/sql/linux/security/encrypted-connections?view=sql-server-ver16)。如果只用既有開發例外繼續本機實驗，必須明確例外profile、loopback/容器scope/到期，不用靜默fallback替代候選policy。

## 5. 驗收（全部NOT_EXECUTED）

| ID | 判準 |
| --- | --- |
| KEY-AC01 | 不同ring/purpose/namespace、跨User/credential密文拒，restart舊TOTP可解、權限/秘密無洩漏 |
| KEY-AC02 | 明確保護at rest、key注入ACL與broker邊界；缺key/sentinel不建空ring、不dispatch |
| KEY-AC03 | 新寫/舊讀輪替及舊證private依賴，不能假裝自動重包；安全退舊有引用/備份證據 |
| KEY-AC04 | cookie/seedkey丟失救援不同，不DangerousUnprotect、不改passwordhash或復活grant |
| KEY-AC05 | 加密backup＋獨立unlock真restore、原namespace/new運行epoch/後cutoff ledger核對 |
| KEY-AC06 | HTTPS trust/expire/Host/SAN/Nuxt實際入口與SQL chain/hostname驗證，例外不可靜默降級 |
| KEY-AC07 | 不同SQL/issuer refs只有指定consumer，issuer/Keychain user-presence有M0拒絕證據 |
| KEY-AC08 | 原生操作/log/health只有狀態/refs，無PFX/private/seed/token/SQL密碼；確認為僅本機mock |

OS broker/隔離與backup地點／人員、保留/輪替數值未採用；source實際平台支持／SDK/Node/SQLdriver配置未綁定，全部需M0/M4實證。


## 首次保管庫初始化補充

首次key/ring／native issuer建立由已授權OS原生provisioning流程執行，先確認operator與工具來源、broker隔離、精確環境/instance、全新或明確重建security journal；不能以待建立issuer自行簽一個approved旗標取得初次權限。此根信任／helper接口仍UNBOUND；Native初始化成功才記受控key refs、namespace與sentinel；正常API啟動不替它建第一個空ring。

正常輪替可依已採用的provider策略產生替代key，但必須在已初始化／受控ring與namespace中，記錄可信新增key/包裝依賴並更新健康metadata。Missing/corrupt ring與授權正常rotation分開裁決；更換工作路徑、restore舊設定或未知目錄不能觸發重新初始化。完整初次provisioning、正常rotation與missing-key拒絕測試皆需M0證據，框架預設不能單独證明這些門禁。
