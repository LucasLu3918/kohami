# 帳號與商務 API 整合規劃

日期：2026-10-13｜PROPOSED／READY_FOR_REVIEW｜產品 NOT_EXECUTED

**適配性：有條件適合。** 帳號生命週期、授權版本與身份 stage 可以沿既定 ASP.NET Core Identity／Nuxt 同 origin 架構整合；本輪補齊機器契約與差異清單，不建立產品、DDL 或 canonical API，也不把框架支援稱為安全驗收。

來源：[帳號生命週期](ACCOUNT_ACCESS_LIFECYCLE_PLAN.md)、[選配清單](../options)、[原商務一致性](../contracts/API_CROSS_STAGE_CONSISTENCY.md)、[身份政策](AUTH_SECURITY_PROFILE.md)。其餘工程數值／欄位／權限代碼仍是提案。

每個後台角色都必須具備有效授權與精確 scope 才能使用 API。逐資源授權、LastOwner 保護、版本及稽核仍照常執行。

## 1. 契約範圍與來源

| 機器文件 | 本輪範圍 | 狀態／限制 |
| --- | --- | --- |
| [M0](../contracts/API_M0.openapi.yaml) | 原13操作、20 DTO歷史子集 | byte保留，不獨立再累計 |
| [M1–M3](../contracts/API_M1_M3.openapi.yaml) | 76商務／售後／資料操作，含原OP-17 | byte保留，既有補充狀態見原一致性登錄 |
| [帳號／授權補充](../contracts/API_ACCOUNT_ACCESS.openapi.yaml) | 19註冊／驗證／reset／Email／Profile／grant操作，20schemas | 本輪新增；未合併成已採用或已實作API |

三份現行提案的 method/path 有104筆不重複操作，operationId也不重複；這只是跨文件清單，不聲稱覆蓋換裝／收藏／匯出、離線bootstrap/recovery命令或真provider API。Schema名字仍以各文件為命名空間，不能直接合併同名Problem；既有M0不與三份重複計數。後續canonical整合須固定版本與確認本節差異，不由生成器決定產品政策。

## 2. 公開受理、nonce 與proof投遞

新增 GET `/auth/account-context` 建立／讀穩定匿名BrowserId及requestNonce，候選15分鐘expiry；不接受Email、不回User或challenge是否存在。沿既有GET `/auth/csrf`取得antiforgery token，兩者必須綁同BrowserId/dataset，不因呼叫順序輪替成不同身份。context重取不得延長原nonce；expiry後發新nonce並撤舊綁定challenge，UI需重新受理。

Registration／email verification resend／password reset三個public request回同一202 `UniformReceipt`；未知、已存在、停用、Closed同形，不回UserId/challengeId/proof/投遞結果。格式錯誤可422，CSRF/Origin403，public browser/IP限流429；不能用account-specific 429／HTTP狀態／body洩存在。帳號計數與是否投遞在server內核對；時間側通道需要實測，不因同JSON就稱無枚舉。

只有對應隔離persona／受控mock驗證投遞可取得challengeId/proof，原public `/demo/inbox`仍不回秘密。瀏覽器nonce是JS可讀binding，不能單憑nonce取驗證內容。GET確認頁不消耗proof；POST綁原challenge intent/User/browser/SecurityVersion/expiry與requestNonce，單次消耗。新nonce不能套舊proof；跨裝置需在新browser重啟受理，不移轉原nonce能力。

所有public POST仍CSRF/Origin。proof不走業務Idempotency-Key；reset成功不自動登入、不變更帳號狀態或授權，也不重啟Suspended/Closed。錯誤採一般 `ACCOUNT_PROOF_INVALID`，不輸出是否User存在。重送撤同intent舊proof，不能以多封郵件建立多個有效復原入口。

Registration的privacyNoticeVersion／acknowledgedPrivacyNotice是告知版本與已閱紀錄提案，非行銷同意，不聲稱法律條文已批准。真實告知文案／版本仍UNBOUND；本機合成fixture可綁DEMO_NOTICE_v1，不替正式合規審查。新密碼形狀15–256字元，與原LoginRequest max256一致；密碼blocklist／hash／Unicode處理須實作核對，不用任意regex宣稱安全。

## 3. 一般會員再次驗證與改Email

GET `/account/security-context`回本人session nonce、requiredMethod、有效MemberRecentProof讀模型。一般MemberAuthenticated可用 POST `/auth/member-reauthentications`驗本人密碼，只建立email.change/User/self的server-side近期紀錄，候選5分鐘；不發新的登入權、token或admin role。record綁當前session/User/SecurityVersion/requestNonce。

當帳號有現行有效後台角色時，member password reauth不得升級為後台身份或取得管理permission。role判斷查當前server有效grant，不能讓前端宣稱member，或因cookie仍是舊member就繞過後台授權閘門。

POST email-change request需User If-Match、同本人最近驗證與newEmail，先受理新Email proof、通知舊Email；202不披露其他會員對新Email的占用。僅一個現行request，新request撤舊。需要取得確認ID/proof時走原受控投遞，不公開補GET查任意Email。

confirm需原request nonce與新Email單次proof、當前本人session與同User/email.change近期驗證。原先近期紀錄過期可依適用的重新驗證方式更新；**新近期nonce不改寫原email challenge nonce**，兩項binding分別核對，request原User/SecurityVersion變化即失效。15分鐘Email proof不延長5分鐘最近驗證。

confirm原子鎖User/current request、新Email normalized唯一性，更新Email/UserVersion/SecurityVersion、撤所有舊session/安全proof/recent record，並必要通知。一般不可用回 `EMAIL_CHANGE_UNAVAILABLE`，不說哪位會員占用。失回應後重新登入／查看本人遮罩profile，再決定是否新受理，不重播舊proof；不保證新Email單靠受理已成立。

任何改Email、reset、Closed／重新註冊均不能變Order contact snapshot、MemberId、Collection/refund來源或去向。訪客OP-38仍member＋原單guest.action(claim)AND授權，不能因Email相同收編或被新信箱擁有者接管歷史訂單。

## 4. 授權讀寫與角色代碼

新增grant detail GET `/admin/access-grants/{id}`，回Grant強ETag及tombstone；列表每項也有etag。unknown/不可閱404。GET `/admin/users/{id}`回User ETag，以及body的accessVersion/accessETag；三者用途不能交換。

| 命令 | If-Match／body版本 | 敏感操作近期驗證目的／資源 | 冪等與原因 |
| --- | --- | --- | --- |
| create grant | targetUser的accessETag＋expectedAccessVersion一致 | access.manage/User/targetUserId | Idempotency-Key；body reason |
| revoke grant | grant detail/list etag | access.manage/User/grant.targetUserId | Idempotency-Key；必填X-Change-Reason，單行不含秘密/PII |
| Suspend/Reactivate | User ETag＋expectedVersion一致 | user.manage/User/targetUserId | Idempotency-Key；body reason |

grant-create成功201回GrantRead與可閱detail Location；create不建立Member或管理 session。DELETE grant是邏輯撤銷，204保留最小Audit／tombstone，不物理刪關聯。相同key同body/header原因重播先檢查操作者當前授權與目標資源範圍，再回原結果；同key不同原因也409。新命令才驗版本；已撤權不能借舊key讀私有成功結果。

role維持Owner、Merchandiser、Customer Support、Analyst；wire `CustomerSupport`是既有角色的序列化，不新增第五角色。machine文件的finite permission allowlist把既有語意映射為catalog.write/stock.adjust/orders.read/refund.request/refund.execute等代碼；這是新技術提案，不是新的權力授予。未明確映射的API不得因role名稱就允許，拒絕為預設。

Owner也受permission-scope allowlist限制：access/user管理只Dataset/User，退款與履約只Dataset/Order/Case，報表只Dataset/Report，demo只Dataset，不能把Product範圍拿來授access.manage。Owner允許對應資料集/資源的核准能力；Merchandiser只catalog.write/stock.adjust，stock.adjust限Sku；CustomerSupport只Order/Case相關讀／提案／履約／原操作查核；Analyst只Report的reports.read。grant role/permission/scope結構使用JSON Schema條件限制，實際能授的子集合、scope真實存在/同dataset、Actor非Target、target Active、grant起迄與當前權限須server核對。多筆grant聯集不突破role allowlist，也不把Support升成refund.execute。

validFrom <= serverNow < validUntil，UTC儲存，必須validFrom < validUntil，無永久grant端點；時段檢查不能依賴定時批次才撤權。未來grant成立時就增accessVersion/SecurityVersion撤舊stage；生效與到期仍每請求重新計算有效permission/scope。既有member cookie不會自動取得管理權；已有 session也不能繼續使用已到期scope。

Owner user/access讀取及列表也核scope後分頁，沒有全站未過濾總數或完整Email查找。cursor綁actor/dataset/filter/sort/version，跨身份／篩選改變拒絕，候選limit20／上限50；跨頁不是固定交易快照，變更時重整列表，不用過期UI決定lastOwner。

## 5. 撤權、防鎖死與現行商務操作

User Suspend／grant變更提高SecurityVersion，撤target現有管理stage/session；Auth／worker新請求查server現權。grant-create不代target登入或自動給後台cookie。所有mutations先驗角色、現行權限與 scope，後驗版本，最後交易裁決；敏感操作不由登入方式取代；session cookie只是身份載體，不能以其中舊permissions當真。

LastOwner由同dataset管理集合序列化檢查：至少另有Active、有效Owner管理授權且無hold。必要能力提案明確含同dataset的access.read、user.read、access.manage，不能只剩一筆無讀取能力的Owner grant。Suspend／grant revoke／verified Close一起核對；兩Owner互撤不得各用舊讀模型通過。期限到期與資料一致性仍需檢查；離線復原命令未建立前不宣稱完全保證。

自己不能自授／延長自己的grant；revoke自己若仍保留另位可用Owner可提案允許，仍須scope、LastOwner與Audit檢查。無本人後台session的帳號Close仍由既有verified DataRequest處理；完成Close對最後Owner的變更也必須套同一保護，必要時案件回需先移交權限，不能以刪除請求刪掉最後Owner保護與稽核。

M3 revoke/Started沿原規則：撤權先於Started則不派送，能證明未派送才確定Failed釋保留；Started先成立由系統追原結果，Unknown仍保留，不由登入失效丟棄或重新派退款。跨備份還原重套 cutoff 後 SecurityVersion/grant 撤權／刪除／hold／已消耗 proof，僅在另行採用[選配 restore 改善](../options/ISOLATED_RESTORE_CUTOFF_OPTION.md)時驗證；runtime 若實作仍須 fail closed。程式尚未實作，不能以規劃覆核稱產品PASS。

登出DELETE `/auth/session`可在已登出時204；同server browser/session binding撤當前session，清登入cookie，不撤訂單／cart／已啟動operations。CSRF/Origin仍必須，不能因匿名fallback就接受跨站登出；不能清他人的session。

## 6. canonical整合前差異與拒絕條件

| 接縫 | 必須保留／修訂 | 不可推論 |
| --- | --- | --- |
| OP-17 Session | 依現行session契約；受限stage permission空；Anonymous欄位一致null；AdminAuthenticated保留原Session欄位 | 原Session合成例子不是已登入身份實證 |

| Identity/User/access/grant版本 | 明確資源ETag；body expected與header代表同revision | 任意opaque字串／舊profile版本可改grant |
| 原商務permission標記 | 新allowlist與M1–M3 x-authorization逐操作映射；未知拒絕 | 光parse三份檔就已覆蓋所有操作權限 |
| 重複Schema名字 | 文件內local ref獨立；整合時選一個Problem/Cookie/Session形狀與命名空間 | 同名就byte/schema相容 |
| 身份寫入與SSR | private no-store、stage不足不讀會員／後台、清client私有狀態 | middleware或UI按鈕等於API授權 |
| 正式開工 | 確認整合提案、告知版本、帳號資料與必要source/SQL證據 | 本輪文件就是產品實作授權／票券resolved |

原三份契約未修改；本輪checker驗其SHA與method/path/operationId，並核共用cookie名稱及敏感操作目的對應。固定source hash使將來修改任何原檔時需重新審查差異，不讓派生validator默默跟著source變化而假裝相容。

## 7. 補充驗收矩陣

原[IDAC-01–16](ACCOUNT_ACCESS_LIFECYCLE_PLAN.md)仍適用。以下10項補充產品驗收全NOT_EXECUTED，對應CHK-01/02/03/04/07/08，不取代原票依賴或已採用政策。

| ID | 觸發與可觀測判準 |
| --- | --- |
| IAPI-AC01 | public register/reset/resend存在／未知／停用同形；nonce不披露challenge；格式錯誤與限流不洩存在 |
| IAPI-AC02 | csrf/context不同順序同browser binding；跨browser／舊nonce/新nonce配舊proof拒；GET不consume |
| IAPI-AC03 | member password近期驗證只email.change；有效後台角色不能透過會員重新驗證旁路取得管理權 |
| IAPI-AC04 | Email proof15分鐘但近期5分鐘已過時，先重新驗證後仍核原nonce；任何版本變更撤request |
| IAPI-AC05 | User/access/grant三種ETag錯用412；缺必需428；同key改body/撤權原因409；原key先驗現權 |
| IAPI-AC06 | role/permission/scope與動態可授子集合、Target Active、同dataset、起迄、自授拒絕，無部分grant寫入 |
| IAPI-AC07 | grant預定生效與expiry當刻有效，舊member不會自動取得管理權；權限聯集不能擴Support退款能力 |
| IAPI-AC08 | Suspend/Close/grant並行維持另位可用Owner；必要read/manage缺失不能算可用；保護回409/需移交 |
| IAPI-AC09 | logout含stage、已登出204且CSRF有效；receipt不自動登入；cursor/SSR切actor不洩資料 |
| IAPI-AC10 | 失回應/restore不重播proof、復活grant或改Order來源；源碼序列化與三份契約映射有候選證據 |

## 8. 官方參考

2026-10-13核對 [OWASP Authorization](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html) 的每請求授權／最小權限，以及 [OWASP Forgot Password](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html) 的一致受理／單次proof／reset後重新登入。具體cookie、版本、角色代碼與LastOwner裁決屬本產品工程提案，非引用來源現成功能保證。


## 換裝身份整合增補（未啟用提案）

[FITTING_IDENTITY_OVERLAY](../fitting/FITTING_IDENTITY_OVERLAY.yaml)列Owner限定新增fitting.read/manage、asset.publish/revoke與精確AssetRevision/Compatibility/Dataset scope。[整合詳規](../fitting/FITTING_API_INTEGRATION_PLAN.md)要求account grant條件、scope與runtime共同整合。原enum仍不接受新值，不能以catalog.write或Owner角色繞過；新增權限未因此採用。新增專用browser cookie只有fitting用途、不提供Member/admin權限；四份現行提案124操作仍非canonical。
