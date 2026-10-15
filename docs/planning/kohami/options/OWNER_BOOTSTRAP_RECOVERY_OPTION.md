# 選配：本機 Owner 初始化與離線復原

狀態：DEFERRED_OPTION（暫不納入 issue、M0／後續規劃或實作；未決定是否擴充）

> 本文件保留既有候選資料供未來評估，不是現行需求、核准方案或實作指示。使用者目前選擇將相關工作移出 Issue 01／02 與 active planning；既有密碼＋TOTP 政策及批准的 M0 範圍不變。

原提案日期：2026-10-13。以下規則與數值均未採用，只供未來重新評估。

適配性：有條件適合。受控本機操作可補齊「初始無Owner」與「所有管理者失去MFA」的缺口，需獨立操作授權、maintenance gate與SQL最小權限才可安全落地。沿用[帳號/LastOwner](../identity/ACCOUNT_ACCESS_LIFECYCLE_PLAN.md)、[已採MFA B及TOTP](../identity/TOTP_MFA_LIFECYCLE_PLAN.md)、[key/cert](../security/KEY_CERT_LIFECYCLE_PLAN.md)、[SQL分工](../security/SQL_LEAST_PRIVILEGE_PLAN.md)。本輪是詳規，不建帳號、復原碼／密碼、初始grant或可執行命令。

政策選配：是否取消 Owner／管理員登入強制 TOTP 尚未選擇或核准；在決策前，本計畫的 SetupOnly、初始 Owner 啟用與 CredentialRecovery 均維持 TOTP 流程。未來即使改變登入要求，也不會自動改變 native 操作授權、maintenance gate、grant 或敏感操作近期 proof；需重新審查本計畫與驗收。[P-M0-ID-02](../product/DECISIONS_ASSUMPTIONS.md)記錄待決選項。

## 1. 角色與授權來源

App Owner是業務角色，不等於SQL sysadmin或本機維護者。Native Operations Operator是具明確本機作業權的操作者，須經OS重新驗證與每操作原生確認；不能用網站Owner cookie、Email、TOTP復原碼或「我有這份規劃」取得作業權。工具不能由HTTP呼叫，也不掛公開bootstrap/reset endpoint。

候選本機工具 `kohami-local-ops` 尚未實作／命令binding=null；inspect/preview/apply/resume/abort只是介面設計，不是現在可執行指令。API/worker不能取native操作憑證或permit簽發key；broker/OS存取隔離未經M0驗證則apply保持BLOCKED，不能假定同OS使用者的檔案ACL能隔離不同程序。實際操作人員／備援未指定不阻擋規劃；本機mock可用具名合成Owner，正式人員另定。

操作來源為Native Authority：本機operator身份、scope、只允許的action、預覽hash與單次限時permit；permit由受控helper在原生人類確認後產生，不採client `approved=true`、API傳入UserId或只有secret bundle持有證明。外部發行者私有key／OS broker具體來源仍UNBOUND，需source與獨立拒絕測試才啟用。

## 2. 共用 preview → apply → verify

1. inspect只回instance指紋、DB/Dataset/security epoch、目前可用Owner數、maintenance/dispatch狀態與預期改動計數，不讀出PasswordHash、TOTP seed、key/PFX、SQL密碼或完整證據。
2. preview綁定action、精確target User或Dataset、DB/instance指紋、目前版本、預期profile／grant差異、backup/checkpoint refs及reason。固定使用mock allowlist DB/Dataset，禁止wildcard、任意SQL、跨專案或指向正式環境。
3. 進maintenance：停止新business mutation/登入升權、fence worker dispatch；只保留inspect與必要setup/rebind路徑。取得排他lease、native與DB一致的maintenance epoch。不能只是關瀏覽器或假定停止worker就無Started操作。
4. Started payment/refund/receipt保留原operation與Unknown額度；不能重播金流或以reset清pending。已Started仍記錄既有system結果，維護不給重新派工權；NotStarted以現行裁決取消lease、不把Unknown當Failed。
5. 原生確認顯示action/target/差異與結果，簽發一次性permit（10分鐘工程提案），綁定原preview hash/epoch/DB/context。proof與secret只由受控broker/TTY/FD程序序記憶體，不進argv、chat/log/環境dump。
6. apply在DB同交易鎖Dataset管理／identity/grant版本、consume permit、記NativeOperation/Audit與變更。原key同action/hash回原非秘密operation結果；改payload/epoch/DB拒絕，過期或跨scope permit拒絕。
7. 驗證TOTP／權限／keys/SQL及不可變交易數據後才退出maintenance。中途失敗保持blocked；resume查原operation，不重新做角色授予／帳號建立或重新產生憑證。

Native host控制台帳與SQL不是同一交易：先寫可驗證Intent，成功後SQL原子變更與DB audit/journal，再補host Applied/Verified紀錄。SQL已commit但host回寫失敗→CommittedAuditPending，維持maintenance；resume核原sourceOperationId/DB commit後補log，不再次apply。原生audit或必要checkpoint不可用時不開始寫入，restore缺後cutoff台帳則阻擋，不能以空台帳視為「沒有撤權」。控制台帳不屬fixture reset，保存位置/保留期在實作綁定，記錄不含秘密。

## 3. 初始 Owner

`InitialOwnerPrepare`只在security state=Uninitialized且DB內沒有任何已建立Owner/已完成bootstrap記錄時允許；不能因目前「可用Owner=0」就再跑首次建置。SQL排他鎖／唯一bootstrap journal確保並行只一成功；已有Owner／歷史紀錄即拒，改走明確復原或移交。

準備指定新合成帳號UserId、Email聯絡、密碼由本人受控輸入；不自動按公開註冊Email升權，不提供預設密碼，不共享Owner登入。創建Owner角色／配置的有限Dataset core grant，但bootstrap gate=SetupOnly：無有效TOTP之前不能取得業務能力，setup stage permissions必須空，所有business/worker保持fenced。原始grant資料存在不代表可用Owner或可執行退款。

初始Email只用合成persona；Owner仍需原Email verification證據後才為Active，不能因本機操作者輸入Email就設定EmailConfirmed=true。SetupOnly allowlist包含本人合成Email確認、密碼登入及TOTP綁定／重新登入，沒有一般角色管理／退款。TOTP已確認但activate前遺失時，可在同bootstrap journal與native維護下僅CredentialRecovery重綁；不解除未完成Email或business gate。

建議本機全流程展示採CoreDemoOwner，但仍需採用其profile／期限；MaintenanceOnly適合只做身份維護，無完整展示業務能力。兩種待採用profile：MaintenanceOnly只access.read/user.read/access.manage；CoreDemoOwner是原account Owner allowlist的23項core權限、僅指定合成Dataset，不加Phase2新權限。前者無完整商店操作能力；後者符合本機全流程展示。profile與有限有效期必須配置，未採用／未配置拒prepare；候選7天grant不是已決SLA或永久超級權限。

setup：本人密碼驗證→受限AdminPasswordStage→TOTP初綁→確認/復原碼一次受控顯示→重新密碼＋TOTP登入。只有當前User/SecurityVersion/credential、同bootstrap operation與受控proof紀錄成立，`InitialOwnerActivate`才可由native工具verify退出SetupOnly/maintenance；此操作不從preview就簽Admin session。MFA stage/read model與bootstrap eligibility需canonical補充，原API不因Owner資料存在就允許未驗證旁路。

回應／程序失敗：Preparing已commit就resume該User/operation，不創第二Owner；setup過期只重取受限stage，不取回舊seed/codes；如果尚未TOTPready，維持SetupOnly。人工放棄只能native cancel未完成setup並留audit/歷史，不能清bootstrap journal變回「第一次」；重建需另範圍審閱，不由普通demo reset處理。

## 4. 離線復原與權限延展分開

| action | 適用與範圍 | 結果／禁止事項 |
| --- | --- | --- |
| CredentialRecovery | Active既有Owner遺失驗證器且正常password＋單次offline code途徑不可用；原生驗證target/reason/checkpoint | 提高SecurityVersion/recovery epoch，撤seed/codes/sessions/stages/proofs，只設RebindRequired；不復原或延長grant，不更改Email/MemberId/Order，不對Suspended/Closed自動啟用 |
| RenewDemoOwnerGrant | 獨立確認既有CoreDemoOwner期限需延長、無security hold/撤權事故；profile僅原core可授範圍 | 新有限grant/版本與審計，撤舊session；不得復原因安全事故撤銷的grant或順便加Phase2權限。普通網站禁止自授/自延不變，這是未採用的native authority例外，不用Owner cookie執行 |
| TransferDemoOwner | 全部Owner不可用／真正受控移交，native明確確認replacement User／固定Dataset/profile | 創建指定替代者SetupOnly，完成本人密碼/TOTP驗證後才可用；舊grant撤銷與hold按明確差異確認，不按Email批量升權，不移轉私人收藏或歸戶訂單 |

CredentialRecovery後本人建立新password stage；若忘密碼，沿正常受控reset或另外明確native密碼重置動作，**本action不偷偷改密碼**。RebindRequired需本帳號原生復原journal/epoch與密碼驗證，只可新TOTP綁定，不立刻得到管理session；重綁定＋重新登入後仍查現有效grant。沒有grant或grant過期就仍無業務能力，可另依Renew/Transfer，不把「能登入」當成「有退款權限」。

無法證明target/原生authority、security hold／缺audit checkpoint、新epoch/DB mismatch均拒。Native recovery不是Email第二因子，也不能預設回復備份中的舊密碼／credential／已撤權。普通offline recovery codes與Native break-glass權威不同，舊codes使用/旋轉紀錄在restore重套。

## 5. 失敗與驗收（產品全部NOT_EXECUTED）

| ID | 判準 |
| --- | --- |
| OPS-AC01 | Empty/Uninitialized並行prepare僅一成功；已有bootstrap／Owner不可重跑；public/Owner cookie無native權 |
| OPS-AC02 | 錯action/target/DB/hash/epoch、過期/重用permit拒；無秘密出現在argv/log/chat/preview |
| OPS-AC03 | SetupOnly無TOTP不能業務；初綁/重新登入完成才activate，stage permissions空 |
| OPS-AC04 | lost response/crash resume同operation/User，AuditPending不開門、不重復apply |
| OPS-AC05 | recovery僅rebind且撤所有舊proof/seed/code/session；不復活grant/Closed或改交易/Email |
| OPS-AC06 | grant renewal/transfer獨立原生確認與有限core scope，普通no-self-grant仍拒，Phase2不隱授 |
| OPS-AC07 | maintenance/fencing不重派Started退款、不釋放Unknown；system結果原操作可追，缺checkpoint不退出 |
| OPS-AC08 | host/DB audit雙寫失敗、舊restore或舊epoch不可復活 authority；原生broker隔離有實際證據 |

【待採用】native authority、profile/有效期、10分鐘permit與維護例外；【待source/M0】helper/命令、broker/簽章來源、DB procedures、bootstrap stage、audit與排他/fencing證據。人員／費用與真秘密未指定，按本輪規劃機制先完成。
