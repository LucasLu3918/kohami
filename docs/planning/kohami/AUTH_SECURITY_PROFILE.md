# 本機身份、MFA 與能力安全詳規

日期：2026-10-13｜MFA_B_ACCEPTED｜身份技術細節仍待審／產品 NOT_EXECUTED

來源：[既有身份政策](../../../.scratch/kohami/proposals/03-identity-permissions-data.md)、[現行mock政策](../../../.scratch/kohami/proposals/04-demo-commerce-policy.md)、[M0契約](API_M0.openapi.yaml)。Email＋密碼會員、後台MFA、Owner退款與近期驗證不重新開放；2026-10-13 使用者明確回覆「先採用B」：第一版後台採密碼＋驗證器 TOTP；Passkey 留後續。此採用不代表其他技術參數全部採用或已實作。

## 1. 官方查核與已採用方案

Microsoft .NET10 Identity提供Passkey註冊／認證，但官方明列沒有內建second-factor支援；不能將Passkey sign-in自動稱為後台MFA完成。[官方Passkey文件](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0)（核對2026-10-13）。

**方案 B 已採用：** 後台先驗 Email／密碼，再以同帳號已確認綁定的驗證器 TOTP 完成第二段驗證；通過後才建立管理 session。使用 ASP.NET Core Identity 2FA 流程作候選整合，Nuxt 前後端契約與實際支援仍需 M0 實證。TOTP 可遭釣魚的限制保留。方案 A（密碼後 Passkey proof）僅為歷史候選，不屬首版必要功能。

方案選擇阻擋已解除；OP-18/19/43/44 的 TOTP 註冊、驗證、近期驗證與復原機器契約仍待深化。secret／時鐘窗／重放保護／復原碼生命週期沿下節提案；數值及套件選型未因「採用B」自動成為人類已批准或產品 PASS。

## 2. 登入、階段與Cookie

| 階段 | 可操作範圍 | 成立／撤銷條件 |
| --- | --- | --- |
| AnonymousBrowser | CSRF bootstrap、合成cart；不能看任何歷史會員資料 | server建立不透明BrowserId／dataset綁定，cookie輪替不讓同key重建業務操作 |
| MemberAuthenticated | 本人資料與訂單；不取得後台權限 | Email／密碼驗證及有效SecurityVersion；登出／reset／停用撤銷 |
| AdminPasswordVerified | 只允許MFA註冊／proof challenge，permissions回空集合 | 密碼已驗，受限stage綁定UserId／nonce；不能先發可用後台cookie |
| AdminMfaVerified | 當時有效角色＋permission＋scope；退款／授權須近期驗證 | server驗第二段proof，時間／方法／來源綁stage；不是client mfa=true |
| GuestRead | 驗證單筆訂單只讀、遮罩資訊 | 原聯絡proof＋order＋read intent；不升會員／取消權 |
| GuestAction | 指定單筆單次cancel/return/claim命令 | 另驗原聯絡＋action intent；消耗一次，網路重送限原操作 |
| CheckoutRead／Pay | read為非PII狀態；pay只該有效保留內付款嘗試 | OP-07建立分離能力，不能進完整訂單／售後 |

Cookie提案HttpOnly/Secure/SameSite=Lax/Path=/且不設Domain；本機固定HTTPS入口 `https://localhost:3000`，所有業務API由Nuxt同origin轉送；後端與SQL仍loopback。Server對Host／Origin／forwarded headers採allowlist，不依任意Host推導登入或Passkey範圍。

GET `/auth/csrf`僅建立／回傳antiforgery token及不透明匿名BrowserId能力，不提供身份升權；token不入public cache／logs。第一個POST cart以此穩定BrowserId scope做冪等，不以尚未存在的CartId當唯一去重身份。CSRF輪替／member登入不得悄悄改變原command的owner binding；若原資源需重綁須明確授權，不接受他人key。

SSR只轉送當前請求必要cookie，不把私有資料或憑證嵌入共享HTMLcache；登出清client私有狀態。有限stage與auth cookie分開，MemberAuthenticated不能因相同UserId就被UI或API認作AdminMfaVerified。

## 3. Challenge／proof／Passkey路徑提案

每個challenge綁定DatasetId、UserId或原ResourceKind/Id、Intent、Browser/Stage nonce、Created/Expires、Consumed、SecurityVersion；資料庫原子消耗，過期／已用／錯intent／跨browser／跨帳號拒絕。challenge不是業務Idempotency結果快取，不回吐已消耗secret；若回應遺失先查有效session，否則重啟驗證。

Passkey後續候選（不屬第一版）：RP ID固定localhost，不含port；origin只允許本機HTTPS入口；userVerification=required，驗challenge、origin、RP ID hash、簽章、帳號credential綁定與UV旗標。聲稱UV不成立即拒絕，不能因authenticator庫回傳一個UserId就直接給角色。sign-count與同步credential的處理依官方機制／屬性實證，不以所有同步Passkey的counter相等直接判盜用。

密碼stage UserId與Passkey UserId必須相同；成功proof只建立受控MFA session／升權，不能用一般Passkey primary-sign-in旁路後台閘門。重新綁定需現有MFA或受控復原，不給一般member stage註冊成他人後台credential。新增／移除credential即撤舊管理session並稽核。

第一版TOTP技術提案：seed只在初次註冊當下受控顯示，不進日誌／OpenAPIexample；需要confirmed enrollment才算已綁定。server以可信clock驗碼、challenge／session綁定及防同proof重放；允許時鐘窗／限次於選定方案實證後固定，不使用Email第二因子。QR提供無照片／secret外送服務的本地呈現。

## 4. 防濫用、敏感操作與復原

工程限流提案（非已批准安全設定）：密碼登入每account5次／15分鐘、每browser/IP20次／15分鐘；MFA proof每challenge最多5次、5分鐘expiry；Email驗證／reset／guest聯絡challenge依原政策15分鐘建議值，重送撤舊枚，與MFA配置分開；guest查詢／reset統一受理並限流。所有計數跨process／restart持久化或等效共享；記錄限流code，不因查詢結果洩漏Email存在。M0驗證正常使用與拒絕，避免只有global account鎖造成易濫用DoS；正式公開網站策略另調整。

管理session閒置15分鐘、最長8小時、退款近期proof5分鐘延用原提案，新的MFA challenge5分鐘也是工程建議（Email/reset/guest用途另採15分鐘候選）。角色／permission／SecurityVersion每敏感請求及派送裁決查有效值，不只cookie聲明。撤權之後新操作拒絕；已發起退款結果由系統追蹤同操作，不丟unknown額度。

復原使用hash後單次離線碼＋帳號秘密或另案核准人工流程；不以單封Email撤MFA。使用復原撤管理session，僅發受限重新綁定stage，重新綁定並驗proof後才可退款。復原碼只在產生時受控展示，不由一般API再取回；demo帳號bootstrap由本機操作者受控注入，不是公開預設密碼。

DataProtection/key ring與TOTP secret加密key需本機持久化、受限權限、gitignore，不隨API restart更換致無法讀取或驗證。備份恢復key存取與撤銷策略是M0案例；本輪只規劃，不產生、讀取或保存真實secret。

## 5. 試驗與採用邊界

M0需：真後端拒絕未MFA／錯User／錯origin／舊challenge／撤權；browser自動化的virtual authenticator只證明流程測試，另用本機實際裝置與Safari手動驗證。無支援時回報具體限制、保留讀商品與一般member能力，但不可繞過後台MFA給退款。

本輪已選方案B，但未完成TOTP整合／機器DTO或執行登入；[M0 OpenAPI](API_M0.openapi.yaml)只含初次密碼登入／session讀取，尚未展開TOTP第二段proof。下一輪須依已採用TOTP方案及官方介面實證補OP-18/19/43/44的機器契約；其前不得把這份subset當作完整登入開工契約。

訪客read／單次action／claim的具體流程見[GUEST_ORDER_ACCESS](GUEST_ORDER_ACCESS.md)；不能用MFA的5分鐘提案覆寫所有Email challenge。
