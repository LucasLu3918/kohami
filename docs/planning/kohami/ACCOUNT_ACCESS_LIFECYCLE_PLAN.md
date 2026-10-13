# 帳號生命週期與授權維護規劃

日期：2026-10-13｜PROPOSED／READY_FOR_REVIEW｜產品驗收 NOT_EXECUTED

**適配性：有條件適合。** 補齊身份邊界可銜接 M1 商品管理、M2 訂單歸戶、M3 退款撤權與 M4 復原。以下是可審閱工程提案，不代表採用新政策、實作 API 或完成安全認證。MFA 方式已採用 B：密碼＋驗證器 TOTP；其餘新細節仍待審，不設免 MFA 的 demo 後門。

來源：[身份詳規](AUTH_SECURITY_PROFILE.md)、[已採用身份政策](../../../.scratch/kohami/proposals/03-identity-permissions-data.md)、[mock 覆寫](../../../.scratch/kohami/proposals/04-demo-commerce-policy.md)、[API 總綱](API_SPEC.md)、[訪客存取](GUEST_ORDER_ACCESS.md)、[M3](M3_AFTERCARE_REFUND_PLAN.md)、[M4](M4_LOCAL_ACCEPTANCE_PLAN.md)。沿用四角色、會員 Email＋密碼、後台 MFA、單 Owner mock 退款。實際人員、運費與供應商維持延期。

## 1. 身份狀態與資料分離

| 帳號狀態 | 能力／轉移 | 明確禁止 |
| --- | --- | --- |
| PendingEmailVerification | 註冊統一受理；單次 proof 成功才 Active；可重送並撤舊 challenge | 不能登入、給後台角色或收編訂單 |
| Active | 本人會員能力；後台另須有效 grant＋MFA stage | 單憑 Role 名稱或相同 Email 升權 |
| Suspended | 安全停用，立即提高 SecurityVersion、撤所有相關 session／challenge；保留必要案件資料 | 舊 cookie、密碼 reset 或訪客 proof 自動解除停用 |
| Closed | 已確認關閉；停止會員與管理登入，非必要資料依已採分類清理 | 關閉等同刪除全部交易或取消所有訂單 |

暫時限速不是帳號 Suspended，Deletion request 的案件狀態也不是帳號狀態。MFA enrollment／recovery 使用獨立受限 stage；不能用 AccountStatus=Active 取代 MFA。重啟 Suspended 須 Owner 的明確安全處置；Closed 不提供一般重新啟用，本機需要展示時用新合成帳號／受控 dataset 重建，舊 UserId 不再分配。

帳號 Email 用於登入與安全通知；Order contact snapshot 用於該筆交易聯絡；MemberId 是訂單所有權。三者分開。任何 Email 變更、關閉或重新註冊不能轉移 MemberId、金融來源、退款去向或舊資料。舊信箱 proof 仍須確認帳號／資源現行狀態與 intent；涉及安全 hold 的單筆訪客能力另撤銷並稽核，不能由舊 proof 繞過 hold。

## 2. 註冊、reset 與改 Email

- 所有公開受理回覆固定 `202 Accepted`＋相同一般訊息／形狀；既有、未知、停用 Email 不回帳號 ID、投遞結果、角色或存在原因。限流計數也不可用差異回覆洩漏帳號；回應時間側通道需產品量測，文件不宣稱消除。
- 註冊只接收合成 Email、password 與既定必要同意，不接受 UserId、role、permission、mfaVerified 或任意 memberId。驗證 proof 不能直接發管理 session。mock 投遞與驗證內容限對應隔離 persona／測試 harness，不放公開 inbox 或 JSON 回覆。
- proof POST 綁 challenge、intent、nonce、browser／stage、SecurityVersion、expiry，原子消耗。GET 開連結只顯示確認頁。失效／已消耗使用一般失敗訊息；不由冪等快取再回 secret。重送撤舊 proof；匿名 nonce 由受理前 bootstrap 建立，回應不揭露是否真的建 challenge。
- reset 成功同交易更新 PasswordHash／SecurityVersion、撤既有 session 與 credential-change challenge，並投遞必要安全通知；不自動登入、不改 MFA、不重啟 Suspended／Closed。密碼規則及期限沿既有政策，不另縮短。
- 改 Email：本人近期身分驗證後要求新管道 proof，發舊管道安全通知；管理帳號另需近期 MFA。近期驗證產生的 server 能力綁 UserId、`email.change` intent、SecurityVersion 與 request nonce，不能拿退款的 proof 自動替代。
- 同一帳號只保留一個現行 email-change request；新 request 使舊 request 失效。confirm 鎖定 User、現行 request 與新 Email 唯一性，檢查版本／proof，更新 Email 後撤舊 session、所有舊安全 proof，要求重新登入。衝突使用一般 `EMAIL_CHANGE_UNAVAILABLE`，不披露其他會員。
- Email 唯一性限同 dataset、由 Identity 的一致 normalization 規則決定；不自行套 Gmail 去點或去 `+tag`，不能用兩套 normalized 值造成登入與唯一鍵不一致。Email change 不覆寫訂單快照；訪客歸戶仍依 OP-38 原單 proof＋member 明確動作。

## 3. 操作契約補齊清單

本節是語意契約候選，**沒有擴充既有 76-operation OpenAPI**。既有 OP 群組維持；新增路徑用 ACCT-* 暫存 ID，不擅自重新編 OP。後續機器契約須統一整合再審查，不以兩份不相容的 DTO 供 client 混用。

| 追蹤 | Method／path | 請求／成功結果 | 授權／版本 |
| --- | --- | --- | --- |
| OP-15 | POST `/auth/registrations` | email/password/必要同意 → 一般 202 | public、CSRF/Origin、限流；無業務冪等秘密重播 |
| OP-16 | POST `/auth/email-verifications` | challengeId/proof/requestNonce → 204 | 單次 intent proof；不自動登入 |
| OP-19 | POST `/auth/password-reset-requests` | email/requestNonce → 一般 202 | public、限流；不回存在結果 |
| OP-19 | POST `/auth/password-resets` | challengeId/proof/newPassword/requestNonce → 204 | 單次 proof；不回 session |
| OP-20 | DELETE `/auth/session` | 無 body → 204 | CSRF/Origin；撤當前 session，已登出同為 204 |
| OP-39 | POST `/account/email-change-requests` | newEmail/requestNonce → 一般 202 | 本人＋email.change 近期驗證；admin 另近期 MFA；If-Match User |
| OP-39 | POST `/account/email-changes/confirm` | challengeId/proof/requestNonce → 204 | 本人 stage＋新 Email proof；版本綁 request；成功清 session |
| ACCT-01 | GET `/account/profile` | → UserId、maskedEmail、status、version | 本人；no-store、User ETag；不回 hash／秘密／交易地址 |
| OP-41 補讀 | GET `/admin/users/{id}` | → 最小合成帳號／狀態／SecurityVersion／version／accessVersion | Owner＋MFA；User ETag，另回授權集合 accessETag 供 grant-create If-Match；無本人收藏內容 |
| ACCT-02 | POST `/admin/users/{id}/transitions` | command(Suspend/Reactivate)、reason、expectedVersion → 狀態 | Owner＋近期 MFA＋user.manage；If-Match＋Idempotency-Key；不可任意 status |
| OP-23 | POST `/admin/access-grants` | targetUserId/role/permission/scope/validFrom/validUntil/reason → 201 grant | Owner＋access.manage＋近期 MFA；If-Match target 授權集合版本＋Idempotency-Key |
| OP-23 | DELETE `/admin/access-grants/{id}` | 撤銷 → 204 | 同上；If-Match grant＋Idempotency-Key；tombstone 留存、不物理刪稽核；列表每項回 grant version／etag |
| OP-41 | GET `/admin/access-grants`；GET `/admin/users` | → cursor 分頁最小集合 | Owner＋MFA＋read scope；targetUserId 查詢不授權其他資源 |

所有 cookie mutation 使用既有 `X-CSRF-TOKEN`＋Origin；受理類／proof 類不用可重播秘密的業務冪等結果。版本化業務命令先驗現行授權，再按 actor/dataset/operation/key 查重播，才檢查新命令版本；同 key 不同 body 409，舊版本 412、缺 header 428、欄位 422、限流 429＋Retry-After。已撤權不因舊 key 回可閱私有結果。跨資源不可查採 404，無 session 401，已知操作權不足 403；公開受理不能沿這些差異洩漏帳號。

成功登入／登出／Email 變更由 server 輪替或清 cookie；Nuxt 同 origin 不快取私有 SSR HTML，不把 raw proof/hash/role 管理資料灌入 public hydration。失敗不在 URL／日誌／通知預覽留下密碼或 proof。

## 4. 授權、代理與防鎖死

AccessGrant 為可撤銷且可追溯的授權紀錄：Id、DatasetId、TargetUserId、Role、Permission、結構化 Scope(kind/id)、ValidFrom、ValidUntil、GrantedBy、Reason、RevokedAt、Version。UTC 儲存，server clock 判定 `ValidFrom <= now < ValidUntil`；不採 client 時鐘。角色與 scope 的允許組合用 allowlist，不接受任意 SQL/filter 字串。

| 角色 | 授權邊界 | 不可授權 |
| --- | --- | --- |
| Owner | 同 dataset 的核准管理；退款仍須 refund.execute／案件 scope／近期 MFA | 跨 dataset、以角色名稱跳過 server gate |
| Merchandiser | 商品／SKU／庫存範圍 | 退款、顧客聯絡資料或任意訂單清單 |
| Customer Support | 指派案件／明確履約範圍，可提出退款 | 執行退款、代顧客登入、讀密碼或全部會員收藏 |
| Analyst | 最小聚合報表 | 訂單 PII drilldown、授權管理或退款 |

新 grant 只能是操作者當下可授予的子集合；禁止自授、延長自己的 grant、為他人建立更大的 scope 或用多個 grant 組合突破角色 allowlist。新增 grant 不直接簽發後台 session；角色／grant 變更提高 target SecurityVersion，撤其舊管理 stage/session，重新登入＋MFA才取得新能力。權限過期即拒絕新請求，worker、polling、匯出也查有效時間，不能等定時批次才失效。

代理人只是另一位具名合成帳號的限時 grant，仍要自己的 MFA；不共享 Owner 密碼、不替原操作者冒名、不繼承原 session。人名／時段未指定不阻擋規劃；本輪不建代理帳號或永久超級權限。

**防鎖死提案：** 一般線上管理不允許移除、停用、關閉或縮短最後一位可用 Owner 的必要管理權。判定至少還有一位不同 Active User，具有效 access.manage、已完成選定 MFA enrollment、非 hold；不要求該人當下在線。檢查同 dataset 的管理集合鎖，兩位 Owner 並行互撤最多一筆成功，另一筆 409 `LAST_OWNER_PROTECTED`。不宣稱能保證人員永遠可用；排程過期／遺失 MFA 仍可能鎖死，需受控本機離線復原。

初始 Owner 與完全鎖死復原不由公開註冊／API 自動賦權。後續另規劃本機顯式操作者、指定 dataset、無 dispatch、最小授權、稽核的 bootstrap／recovery 命令；本輪命令 UNBOUND、無通用預設密碼。復原不能單凭 Email 撤 MFA，不能授權把處理中退款重送。操作機制須經 MFA 選定及 M0 實證後採用。

## 5. 關閉、資料刪除與競爭裁決

本人關閉透過既有 OP-40 verified data-request 案件，不增一個直接 DELETE User API。前台區分「停止登入」與「刪除資料進度」，顯示仍保留的必要交易／爭議類別與原因。確認 Close 時先撤登入／能力，再按既有分類清理；必要交易、原快照、CollectionLedger、RefundOperation、Audit 保留其依據，不用 Closed 將退款標成失敗或刪除。

必要履約／退款結果由系統持續處理。Closed 不保留會員登入，客人可另以原訂單聯絡驗證取得受限查詢／客服能力；security hold 的案件另走受控處置，不能由信箱 proof 解除。會員關閉不自動取消／歸戶／更改原退款管道，也不把前述能力升回會員。非必要收藏依原分類刪除；法定期限仍待實際資料分類，不把所有欄位統一保存 5／10 年。

| 同時事件 | 必要裁決／後果 |
| --- | --- |
| reset／改 Email vs 舊 proof | 鎖 User＋challenge；版本改變使尚未消耗 proof 失效；重送不能復活原 proof |
| 兩帳號 claim 同一訂單 | 同 Order 鎖／唯一 ownership；只有一人成功；第二人不覆寫 MemberId |
| revoke／Suspend vs refund NotStarted | 相同授權裁決序列，Started 前再核現行權；撤權勝出則不派送，既有保留額由原操作明確未派送結果釋放，不留永久 Pending |
| revoke／Close vs refund Started | 結果由系統寫回同 operation/source；Unknown 仍保留，不依帳號停用丟結果或另派一次 |
| grant 到期 vs worker claim lease | lease 不等於 Started；Started 前判期滿即拒絕。Started 已成立則追蹤原結果 |
| 兩個最後 Owner 變更 | dataset 管理集合序列裁決；不從兩份過期讀模型各自通過 |
| restore vs 新的撤權／刪除 | M4 隔離 target、禁 dispatch，重套 cutoff 後撤權／刪除／hold，再決定是否啟用；缺資訊 BLOCKED |

SQL 候選追加 UserStatus/UserVersion、授權集合版本與 email-change request 現行唯一性；沿用 Identity、AuthChallenges、Sessions、AccessGrants，不提供本輪 DDL。鎖序候選：DatasetAccessControl（涉及最後 Owner）→排序 User 授權版本→業務 Order／退款來源→Challenge；實作必須避免一條 Order→User 反向鎖路徑，SQL 並行實證前不宣稱防死鎖。Owner 組態及撤權裁決與退款 Started 的同交易協議是 M0/M3 必測接縫。

## 6. 驗收矩陣

以下每項產品狀態皆 NOT_EXECUTED；文件檢查 PASS 不提升這些狀態。每例保留 candidate SHA、dataset、DB/schema/fixture 版本、輸入角色與預期讀模型／稽核；不記 raw 密碼／proof。對應 [交付計畫](IMPLEMENTATION_PLAN.md) CHK-01 契約、CHK-02 SQL／退款並行、CHK-03 身份權限、CHK-04 瀏覽器旅程及 CHK-07 復原；[M4](M4_LOCAL_ACCEPTANCE_PLAN.md) 串接同一清單。

| ID | 情境與可觀測判準 | 接縫 |
| --- | --- | --- |
| IDAC-01 | 已有／未知／停用 Email 公開註冊、reset 相同受理形狀；限流與時間側通道另量測 | OP-15/19 |
| IDAC-02 | 已用／過期／錯 intent／跨 browser proof 拒絕；GET 不消耗；兩個 confirm 只一個提交 | OP-16/19 |
| IDAC-03 | reset 撤舊 session；不登入、不解除停用、不移除 MFA | M0 |
| IDAC-04 | Email change 舊 request 失效、唯一性競爭一人成功、舊 session/proof 被撤 | OP-39 |
| IDAC-05 | 改 Email／重註冊不改 Order snapshot 或 MemberId；claim 同單只有一個 owner | M2/OP-38 |
| IDAC-06 | member／password stage／未 MFA／偽 mfa=true 不能讀管理 users 或授 grant | M0/OP-23/41 |
| IDAC-07 | 自授、跨 dataset、超 scope、角色不相容 permission 均拒絕且無 grant 副作用 | OP-23 |
| IDAC-08 | Support 可提案不能 execute；Merchandiser 無 PII；Analyst 無 drilldown | M1/M3 |
| IDAC-09 | grant 到期當刻拒絕；舊管理 cookie 不承接新增權；worker 不信 lease | M0/M3 |
| IDAC-10 | revoke vs NotStarted 不派送、原額度最終釋放且未重發；所有競爭有稽核順序 | M3 |
| IDAC-11 | revoke vs Started 仍寫原結果；Unknown 保留、不重退、不因使用者失效丟事件 | M3 |
| IDAC-12 | 兩 Owner 並行互撤／停用，至少一名可用 Owner 保留；另一筆 409 | OP-23/ACCT-02 |
| IDAC-13 | 關閉撤登入／收藏清理；交易與必要 audit 未被抹除；受限查詢不能解除 hold | OP-40 |
| IDAC-14 | 原 key 重播先驗現權；撤權無私有結果；同 key 改 body 409，舊版本 412 | 全命令 |
| IDAC-15 | SSR／log／inbox／錯誤 DTO 不含 hash、proof 或其他帳號私有資料 | Nuxt/M0 |
| IDAC-16 | 舊備份不恢復撤權、已刪資料與過期 grant；缺 cutoff 後資訊禁止 dispatch | M4 |

## 7. 剩餘規劃與接續順序

1. 本提案審閱後補 OP-15/16/19/20/23/39/41、ACCT-* 的完整 OpenAPI／負向 DTO 樣本；整合既有 M0/M1–M3 security scheme 與 error code，保留版本差異。
2. MFA 方式已由使用者採用 B：密碼＋TOTP；選擇阻擋解除。OP-18/43/44 的 TOTP DTO／bootstrap recovery 細節仍 NEEDS_CONTRACT_AND_EVIDENCE，可接續深化，不稱產品已通過。
3. 固定模特換裝／收藏／匯出還需素材支援矩陣、權利、失效 fallback 與完整機器契約；20 組收藏限額仍提案，不擅自採用。
4. 具體產品開工與 M0 SQL/Auth/Nuxt 證據須另外授權與驗證；命令、API 契約可審閱不等於產品完成或正式營運合規。

## 8. 官方安全參考

最小權限、預設拒絕與每次請求授權依 [OWASP Authorization](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html)；統一 reset 回覆、單次短效 proof 與 session 撤銷依 [OWASP Forgot Password](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html)（核對 2026-10-13）。本產品的 last-Owner 鎖、grant 版本與跨階段裁決是工程提案推論，非這些來源保證的現成功能。
