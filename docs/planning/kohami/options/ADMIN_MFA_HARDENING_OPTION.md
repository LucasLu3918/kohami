# 後台登入 MFA 強化選配

狀態：OPTION ONLY；目前沒有強制的後台登入第二因素政策。本文保留未採用的 MFA 強化建議與候選契約，不能作為現行需求、實作指示或驗收門檻。

適配性：有條件適合。若日後提出登入強化需求，可比較驗證器、Passkey／安全金鑰及帳號復原成本；先完成威脅模型、相容性與使用者復原評估，再決定是否採用。額外 stage、重放防護、秘密保護與撤權交易仍需應用整合及 SQL 實證。[身份安全基線](../identity/AUTH_SECURITY_PROFILE.md)是目前授權邊界；本文件與[候選 OpenAPI](API_TOTP_MFA_OPTION.openapi.yaml)只作選配改善資料，不構成政策來源或執行承諾。

若提出此選項，需先另行核准適用角色與登入政策、驗收範圍、復原方式及部署相容性。本文的參數、API、資料模型與驗收案例均為候選，不表示所有角色必須啟用，也不改變現行角色／permission／scope 規則。

## 1. 參數與框架實證邊界

| 項目 | 候選強化方向（未採用） |
| --- | --- |
| TOTP | 6 位數、30 秒 step、HMAC-SHA1；以選定 .NET 10 固定版本及實際驗證器互通實證，不假定所有 Identity 設定可直接切換 |
| 容許時鐘窗 | 當前 step ±1；拒絕更遠 proof，server clock 權威，不接受 client 指定 step；需能取得匹配 step 的 verifier 接縫，原生 bool 驗碼不足證明防重放 |
| Challenge／stage | 5 分鐘，server expiry 等號即失效；重送建立新 challenge 不延長原 stage；每 account/dataset/intent/browser 的現行 challenge 更新撤舊 |
| 防濫用 | 每 challenge 最多 5 次失敗，另沿身份詳規的 account/browser/IP 滾動限流提案；共享 SQL 計數，不靠重啟清零；不把暫時限流自動轉為永久停用 |
| 管理 session | 閒置 15 分鐘、絕對 8 小時；一般讀取可續閒置，不能續絕對時間或近期驗證 |
| 敏感近期驗證 | 5 分鐘，綁 session、User、SecurityVersion、purpose、resourceKind/id；不能把退款 proof 當授權管理 proof |
| 復原碼 | 一次 10 組，每組至少 128-bit 隨機熵；格式與輸入 normalization 在實作固定；只存安全 hash、setVersion、UsedAt，不使用短數字碼作離線復原 |

這些新增配置是可審閱專業預設，沒有因「採用 B」就宣稱全數已批准或實證。不外送 QR／secret，不選付費服務。Microsoft 說明 TOTP 有效期內可能多次驗證成功；本計畫的重放保護是額外要求，不能將框架 bool 結果直接當成單次 proof 保證。

## 2. 狀態與 cookie 接縫

| 狀態 | 取得／允許 | 禁止／終止 |
| --- | --- | --- |
| AdminPasswordVerified | OP-17 密碼驗證成功且具現行後台資格，`kohami.auth-stage`；讀自己 context、login proof、初次綁定（無 active credential）、復原 challenge | 不能讀任何業務後台、退款、角色授予或靠 password stage 替換 active seed |
| PendingEnrollment | 父 stage/session 綁定的 encrypted pending seed、enrollmentId、版本、nonce；只可單次確認 | 非 active credential；過期／取消／被新 enrollment 取代不改舊 active seed |
| AdminMfaVerified | password stage＋active TOTP login proof 成功，`kohami.session`；依有效角色／permission／scope | cookie 不是任意權限；仍查現行 SecurityVersion、hold、期限與物件授權 |
| RecoveryRebind | password stage＋單次離線碼成功，`kohami.rebind-stage`；只可讀自己 context／新 seed 綁定 | permissions 固定空；舊 active credential、所有 codes/stages/session 撤銷；不能直接簽管理 cookie |
| ReauthenticateRequired | enrollment confirm 或 code rotation 已提交、只當次展示復原碼並清所有 session/stage cookie | 不是可登入階段；重新 OP-17 密碼→下一可用 TOTP proof 才回管理能力 |

每次身份變更都輪替並清相衝突 cookie。Cookie 採 HttpOnly/Secure/SameSite=Lax/Path=/、無 Domain，本機同 origin HTTPS；資料與 stage 綁 dataset。GET `/auth/mfa-context` 只讀已成立本人 stage/session，無匿名查帳號；回 MFA aggregate ETag、stage nonce、activeCredentialId、現行可操作能力，不回 seed/code/hash。

**原契約差異：** 原 Foundation/M1–M3 OP-17 保留現行 wire 狀態相容性；既有 SessionCookie 不應被解讀為已允許後台。後續實作 OP-17 必須實際發 auth-stage cookie、產生 login challenge（無 active credential 則 null），GET 原 `/auth/session` 與新 context 同時反映相同 server 狀態。`AdminAuthenticated` 仍為既有 wire 名稱，對應文件 AdminMfaVerified。TOTP登入成功回應保留原Session欄位，mfaChallengeId固定null。M1–M3原x-recentMfaRequired consumer整合時須改查同session/purpose/resource的server紀錄，不能只看mfaVerifiedAt。原 OpenAPI 不改；補充文件不能直接拼接成已批准 canonical API，整合時須明確修訂 security scheme／Session DTO 與狀態映射。

## 3. 註冊與替換驗證器

1. 初次密碼 stage 且無 active credential，或 RecoveryRebind，或已 MFA session＋`authenticator.replace` 近期驗證，可 POST enrollment。server 檢查現行身份／User status／SecurityVersion、MFA aggregate If-Match，再建立新 pending seed。
2. 首次回應只展示 sharedKey／authenticatorUri。QR 在 Nuxt client 本地產生，可手動輸入；不進 SSR hydration、CDN、第三方 QR API、log、analytics 或 browser 永久 storage。UI 可讓本人儲存復原碼，網站不自動永久保存秘密。
3. 使用者輸入當前 TOTP，server 對 pending seed 驗證，同交易消耗 enrollment nonce／匹配 step、啟用 credential，撤舊 active seed、session/stage/recent proof/舊復原碼，更新 SecurityVersion／MFA aggregate version，產生新復原碼 hash 與最小 Audit。
4. 成功只當次回覆新復原碼，state=ReauthenticateRequired，清 cookie；不因 setup confirm 直接給業務權。重新登入若仍輸入剛確認的同 step 會拒絕，UI 提示等待下一組碼，而非秘密重播。

Pending seed 不覆寫 active seed；建立新 pending 會撤舊 pending／確認能力。Session 替換 seed 的近期 proof 綁 credential resource；create 與 confirm 都核現行權與父 session/recent record，過期即拒絕，不借五分鐘 enrollment 延長原驗證。沒有 disable-MFA endpoint，後台角色不能留單因子管理登入。

## 4. 登入與敏感操作

OP-17 密碼成功後才可對 login challenge POST `/auth/mfa-verifications`。proof body 不接受 userId、roles、permissions、mfa=true、timeStep 或任意 seed。server 校驗 stage、challenge、nonce、active credential、User 狀態／SecurityVersion；同交易消耗 challenge 和匹配 step，輪替管理 cookie。permissions 由server現行資料投影，不由 client輸入。

已 MFA session 欲做退款／授權／改 Email／reset 等指定敏感操作，先 POST `/auth/reauthentication-challenges`，server 檢查 purpose/resource allowlist 與 scope，再回 challenge。`/auth/reauthentications` 只建立同 session、purpose/resource/nonce/version 的 server-side RecentProof；JSON（含已綁定 requestNonce）是讀模型，不能將其貼到別的 session 充當 token。業務命令不接收 client 最近驗證旗標。

Purpose 對應：refund.execute→RefundCase；access.manage/user.manage/email.change→User；demo.reset→Dataset；authenticator.replace/recovery.rotate→Credential。access.manage 綁 targetUserId，不能拿同次 proof 管全部人；refund.execute 綁同案件（正常／shipping-only／補償以既定 CaseKind 判別），不借同 Order ID 擴張到所有退款案件。

登入／setup 的 proof 不自動滿足所有敏感 purpose。近期記錄有效期間可用於同 purpose/resource 的操作，但每次仍驗業務版本／冪等與 scope；不改 M3 Started／撤權裁決。到 expiry 等號或 SecurityVersion 改變立即失效。code rotation 的 requestNonce 要匹配該 `recovery.rotate` RecentProof，不接受任意新 nonce。

## 5. 重放、防並行與資料計畫

每個 credential 有 CredentialVersion、LastAcceptedStep；verifier 對 server 窗內算出匹配 step，只有 step > LastAcceptedStep 可接受。同帳號 credential 的 login/enrollment/reauth 使用同一消耗接縫，兩 worker 同 proof 只有一個提交。不只依 challenge 不同就容許同 step 重用；不能只存六位數字永久封鎖，因未來 step 可巧合相同。

SQL 交易鎖序提案 User 授權／SecurityVersion → Credential/MFA aggregate → Challenge／RecoveryCodeSet；涉及業務沿帳號提案保持授權先於 Order，不倒轉。proof 成功與 challenge consume、step advance、session/recent issuance record、稽核寫入原子化；cookie 只能在提交後發。失敗 code 更新限次但不消耗正確 proof；不回剩餘復原碼明文或是否用了哪個 step。

新增候選欄位（沒有 DDL）：PendingMfaEnrollments(EncryptedSeed、parentStage/session、expiry、intent、nonceHash、version)；AuthenticatorCredentials(EncryptedSeed、CredentialVersion、LastAcceptedStep)；RecentMfaProofs(session/User/purpose/resource/version、verified/validUntil/nonceHash)；RecoveryCodeSets(setVersion、hash、UsedAt)；AuthChallenges(intent、attempt count、consumed)及現行受限 stage。MFA aggregate version 統一支援 context ETag、enrollment create 與code rotation，不能拿 UserProfile rowversion 代替。

TOTP seed 必須可驗證，不能只 hash；依既有獨立key/key ring加密、限制 SQL／程序權，repo只記配置key名。ASP.NET Identity 預設 token storage 是否足夠及如何保護 seed須真實檢查，不假設框架自動滿足靜態加密要求。復原碼只 hash，常數時間驗證與安全隨機來源在實作核對。

Clock 回撥不降低 LastAcceptedStep；較舊 step 被拒絕並記最小 clock 診斷，不自動重置。restore 也不可回退 UsedAt／LastAcceptedStep／SecurityVersion；依 M4 重套 cutoff 後紀錄，缺資料則禁派送／BLOCKED，或受控撤全部驗證能力重新綁定後再驗，不將舊 proof 復活。

## 6. 復原、回應遺失與防鎖死

已密碼驗證的 AdminPasswordVerified（有已綁 credential）才可要求 recovery challenge。單次離線碼成功同交易撤 active seed／所有code set/session/stage/recent proof，提高版本，只建立五分鐘 RecoveryRebind；不直接 MFAVerified、不恢復任何已撤 grant。rebind過期，重新密碼驗證後僅能續同帳號 RebindRequired 流程，不能取回已消耗碼或授後台；受控 recovery epoch 保持，只允許新的限時 rebind stage，原 stage 被撤。這條續綁定能力只在已成功復原且持續 RebindRequired 的現行版本成立，不能讓從未復原的密碼stage替換seed。

沒有復原碼又不能使用驗證器時，不以 Email 解除 MFA；完整鎖定情境仍待另案評估，命令 UNBOUND，不能宣稱已具備完整救援能力。

| 遺失回應的操作 | 可查結果與安全下一步 |
| --- | --- |
| enrollment create | context只回 canEnroll／版本，不回 seed；重新建立新pending撤舊pending，不重播sharedKey |
| enrollment confirm | cookie可能未清；server舊session/stage必須已失效；重新密碼登入檢查MFA已啟用，進TOTP後可另輪替復原碼；不再次取原碼 |
| login proof | GET 原session/context，若新cookie未收到則重新密碼登入，等下一可用step；不重用已消耗proof |
| reauth proof | GET context回同session有效 recentProof讀模型；有結果即可繼續同目的動作，沒有則新challenge＋下一碼 |
| recovery confirm | 重新密碼驗證可讀受限RebindRequired流程；不能因已消耗離線碼就直接MFA登入或重新簽舊scope |
| code rotation | 重新密碼＋TOTP登入，再做近期驗證、輪替另一組；原組即使沒收到也已是現行，不能重新查原明文 |

UI 顯示 QR手動輸入、碼錯誤、下一碼等待、到期重新登入、復原碼只展示一次、復原只能重新綁定。錯誤統一 MFA_PROOF_INVALID；不洩 step、seed、hash或帳號存在；HTTP429＋Retry-After 呈現稍後重試。request/response秘密不留操作錄影／截圖／E2E trace；驗收可記 redacted狀態與case ID。

## 7. 契約與驗收

OpenAPI 9 ops／專用cookie為可審閱補充提案，未合併舊檔、不生成client或啟動產品。所有回應 no-store；POST CSRF/Origin；proof／秘密發行不使用業務 Idempotency-Key、不返回失效秘密。敏感 schema 不放可用 seed/code/recoveryCode 範例。文件 checker 只驗形狀／參照／stage宣告，不模擬或證明真驗證碼成功。

| ID | 必須驗證的可觀測行為 | 接縫 |
| --- | --- | --- |
| TOTP-AC01 | 密碼stage讀業務後台拒絕；若日後採用 MFA，所有角色須遵守明確一致的登入規則，client旗標不升權 | Foundation/CHK-03 |
| TOTP-AC02 | 初次／替換pending不啟用舊seed替換；expiry／撤pending拒確認 | OP-43 |
| TOTP-AC03 | confirm原子啟用、撤舊session／codes；再次登入用下一step，不直接給管理能力 | SQL/OP-43 |
| TOTP-AC04 | 兩worker，同step跨login／reauth／同credential兩challenge只一個成功 | CHK-02/03 |
| TOTP-AC05 | 錯帳號／browser／intent／nonce／版本／expiry proof拒絕且無session副作用 | Foundation |
| TOTP-AC06 | ±1窗邊界、前導0、client指定step拒；真驗證器與server clock互通 | verifier |
| TOTP-AC07 | 近期驗證purpose/resource綁定，退款不能變授權；版本改變／expiry等號拒絕 | M3/CHK-03 |
| TOTP-AC08 | 同離線碼並行只一個消耗；成功僅rebind，撤seed/codes/session，不恢復grant | OP-44 |
| TOTP-AC09 | recovery回應遺失／stage過期仍可受限續綁定；密碼 alone 未曾復原不能替換active | OP-44 |
| TOTP-AC10 | setup/rotation明文一次、不提供GET取回；丟回應照表復原而不重播secret | API/Nuxt |
| TOTP-AC11 | authstage到期、共享限流、重啟不清計數；不停用帳號造成永久DoS | Foundation/SQL |
| TOTP-AC12 | secret靜態加密與key持久化／權限；QR不出本機，SSR/log/trace不含secret | CHK-03/04 |
| TOTP-AC13 | Clock回撥或restore不重用已消耗step/code；cutoff後資訊缺失BLOCKED | CHK-07 |
| TOTP-AC14 | revoke先於refundStarted拒dispatch，Started先於revoke追原結果，無免MFA後門 | M3/CHK-02/03 |

如另行採用此選項，須依所選方法重定義驗收案例、整合契約並完成產品實證；此提案不代表任何認證等級。

## 8. 官方參考

2026-10-13核對：[Microsoft TOTP／QR與時鐘](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-enable-qrcodes?view=aspnetcore-10.0)、[Microsoft MFA](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/mfa?view=aspnetcore-10.0)、[RFC 6238](https://www.rfc-editor.org/info/rfc6238/)。Identity整合、window與replay接縫為本產品提案推論，仍須固定版本源碼與SQL實測；不以文件相容推為產品PASS。
