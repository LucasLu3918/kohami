# 驗證與批准計畫

## 本輪 GRL 風格本機 demo 驗收（2026-10-21）

狀態更新（2026-10-21）：P3/P4 桌機與手機視覺檢查已有 strict TLS 截圖證據；P5 會員錢包 UI、HTTP／SQL readback 與一次性 headless browser 驗收已通過並整合至本機 `main`（`ab87d81`）。依使用者決定，本輪商品圖採用現有 15 款平拍圖，不把未通過品質的 ComfyUI 模特圖 pilot 列為阻擋。可重用合成會員及可由操作員現場完成錢包結帳的 strict TLS browser 驗收仍待完成；既有 `demo-current` 僅限唯讀，不能承載該驗收。

本節是 [主計畫](IMPLEMENTATION_PLAN.md#0-本輪可-demo-收斂計畫2026-10-21) DEMO-P0–P5 的驗收定義；各項目前狀態見上方更新與逐項證據。使用者已選獨立可寫合成 fixture、15 款平拍（不要求本輪穿搭樣張）、繁中／TWD／三語及真實唯讀管理登入。管理登入沿用現有密碼登入與 server-side session；MFA 不列本 demo 的登入或授權驗收門檻，相關強化保留於[未來選配](../options/ADMIN_MFA_HARDENING_OPTION.md)。這是可 demo 交付檢查，不取代較完整 FINAL-CORE 或把 Issue25／26 未通過項目改成 PASS。

| ID | 必要案例／通過條件 | 證據 |
| --- | --- | --- |
| DEMO-LOCAL-01 環境 | 明確 allowlist 的獨立 loopback SQL／profile／marker／schema；與受保護 fixture 隔離、保留其 gate／資料／身份；無偷偷 migration／seed。當前 API `/health`、`/ready` 200 且實際指向該 SQL，三語 SSR／browser 可載入；停止與再啟動成功 | candidate／profile 非秘密識別、SQL 身份／權限與 schema、API 結果、程序擁有權及啟停紀錄；私鑰／帳密不輸出 |
| DEMO-LOCAL-02 商品 | 至少 15 個不同已發布 Product，不以 SKU 計數；各有本地圖片、名稱、TWD 售價、色／尺碼、合成尺寸／材質與庫存。卡片／詳情／報價資料一致；無 broken image。依使用者決策，本輪沿用 15 款平拍圖，模特穿搭樣張不屬於本輪必要條件 | API→SQL 商品／SKU 對照、首頁 15 張商品圖瀏覽器證據；本地圖檔請求與逐款文案／圖片一致性 QA |
| DEMO-LOCAL-03 外觀 | 依 VISUAL_SYSTEM 對照 GRL 的首頁／商品列表／詳情／管理登入，1440px／390px 截圖確認排版、字級、留白、導覽及圖片比例；窄螢幕無整頁溢出；可用入口真的可操作 | 並排截圖與差距表；不同合成素材不要求像素相等，不聲稱原站私有後台已被還原 |
| DEMO-LOCAL-04 顧客旅程 | 真 browser 經 Nuxt 同 origin 代理連真 API／SQL：瀏覽→搜尋／既有篩選→詳情選 SKU→登入一次性合成會員→加車／改數量→完整費用→內部合成餘額扣款→本人訂單；介面有 loading／失敗／缺貨狀態，重新整理可讀已提交結果 | 當前 candidate 的 browser 記錄、HTTP／SQL 對照；不以 route mocks 或 SSR 200 代替；資料由可精確清理的一次性 actor 建立 |
| DEMO-LOCAL-05 原子性 | 專用 SQL 驗不足額全回滾、同鍵重放不重扣、不同請求並行不負餘額／不超賣，以及帳本／order／collection／reservation 一致；跨會員訂單拒絕 | dedicated SQL／整合結果；歷史證據須核對 candidate／契約相容性，未跑項目明列；清理只影響本輪合成資料 |
| DEMO-LOCAL-06 管理登入 | 真 HTTP 密碼登入／登出與 server-side session；只授予指定 dataset 的 `catalog.read`（若現行契約未提供，先補最小唯讀 permission），不要求 MFA。匿名直連管理頁、一般會員、無 scope／過期／撤權 session 均不能讀管理 API；登出後再請求拒絕。唯讀清單對照 SQL；直接呼叫寫入 API 仍須由後端拒絕，不能因 UI 隱藏按鈕取得權限 | 真 HTTP／SQL/browser 的 actor×route／scope 結果，核對 session cookie 建立與登出失效，保留 Origin／CSRF 控制；不建立正式 Owner 或以 member bootstrap 冒充管理初始化 |
| DEMO-LOCAL-07 交付 | 操作手冊只列實作且驗過的啟停／顯式初始化／資料匯入／驗收命令；說明合成付款及展示限制，demo 不依賴線上 GRL 圖片、雲端生圖或第三方收款；既有受保護 fixture 與憑證未被改動 | 當前證據索引、已知限制與重啟演示；任一未完成項不能用文件存在或歷史 PASS 消除 |

新增可測試行為採 red→green→refactor；UI 樣式探索採截圖與互動／可及性檢查。價格／交易／授權不只用 mock 測試。先解決瀏覽器啟動與本機憑證信任，不能忽略 TLS 錯誤讓案例看似通過；信任設定由操作者依正式工具處理。正式資金、公開部署、完整管理寫入與 Fitting 不在本輪。

## 本階段驗收範圍更新

現行 [mock 政策](../../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為本階段追蹤矩陣。V04／V05 正式供應商證據延期，由受控 adapter 情境驗證目前應用行為；不宣稱真實服務契約已通過。AC05 使用 mock 事件；AC07 的退款改為具權限單一 Owner，並驗證越權、重送及並行行為。N03／N05／N06 與票38 的實際營運目標不能用 mock 計時證明。

本文件定義驗收條件；實際產品狀態以技術基線 IMPLEMENTATION_STATUS.yaml 與 architecture/VERIFICATION.md 的逐項、具日期證據為準，不能以規劃模板推定全部未執行或全部通過。正式上線仍有獨立驗收。

更新：2026-10-15｜唯一跨階段驗收定義；獨立 restore／cutoff 為選配改善

2026-10-20 本機開發模式決策：核心 Storefront／API／SQL 驗收使用 marker-verified `localhost:14349/KohamiM0/demo_m0` 和明確 `local-development-bootstrap` 建立的合成會員／測試儲值金。Apple Developer、Secure Enclave、Owner 原生 authority 不是本機購物 API 的前置；但本機 bootstrap 與完整 live SQL journey 尚須獨立驗收。bootstrap 拒絕非指定 fixture、schema 未就緒、已有身份／grant 或 gate 狀態不符；驗收須核對帳密只存 ignored `.local` mode 0600，且失敗時不留下會員、錢包帳本或 gate 部分變更。此決策不修改正式 Owner policy，也不代表正式安全驗收通過。

本文件定義應取得的證據；供應商、效能及合規不得由文件存在推定通過。責任角色為建議分工，不表示已指定人員或核准工作／費用。

## 1. 外部與可行性驗證

| ID | 驗證項目 | 證據與基本通過條件 | 建議責任／解鎖決策 |
| --- | --- | --- | --- |
| V01 | 品牌權利 | 商標、近似名稱與類別查核；不可接受風險解除 | 品牌／產品；正式品牌識別 |
| V02 | 商家及合規 | 實際營業主體、商品標示、消保與個資政策、收款及發票申請條件 | Owner／專業審查；正式營運資格 |
| V03 | 商品採購及價格 | 合法來源、真實商品、尺寸、供應商報價及毛利模型 | 商品／營運；數量、售價 |
| V04 | 金流 | 同場景費率、申請資格、通知可信性、退款、查詢、對帳及異常測試 | 工程／財務營運；綠界或藍新等選型 |
| V05 | 物流與發票 | 正式費用、介面、重量體積限制、履約及退款發票流程 | 工程／營運；配送、運費、免運與服務商 |
| V06 | 電商平台 | 相同交易／庫存／退款與權限場景的實證；無不可接受缺陷 | 工程／品質；框架與架構 |
| V07 | 角色素材 | 四組及公告尺寸三視圖；外觀、比例、姿態、基礎服裝與量測一致 | 美術／產品；可用尺寸及製作量 |
| V08 | 服裝相容性 | 約 20 SKU 實際外觀、顏色、遮擋與對齊審核；重大錯位不通過 | 商品／美術／品質；首批範圍 |
| V09 | 授權與匯出 | 商業展示、修改、下載及到期撤除等權利紀錄；功能不超出授權 | 內容／營運；素材啟用與匯出資格 |
| V10 | 效能及裝置 | 手機／桌面、網路情境、首次載入、就緒切換、記憶體及傳輸量 | 工程／品質；支援範圍與成本 |
| V11 | 總成本與人力 | 開發、素材、審核、維護、雲端及商務服務報價；負載情境成本 | Owner／工程／營運；預算與交期 |
| V12 | 安全與復原 | 權限、可信金流、去識別化日誌；獨立備份還原與 cutoff 對帳列本機選配改善 | 工程／安全／營運；正式上線範圍另行審查 |

每項驗證須記錄日期、版本、資料來源、測試條件、結果與限制。若不通過，記錄整改或範圍變更提案；不能將「尚無證據」填為通過。

## 2. 功能驗收矩陣

| 驗收 ID／需求 | 情境與通過條件 | 方法 |
| --- | --- | --- |
| AC01／R01 | 三語切換後有效購物車及 SKU 不遺失；交易幣別 TWD | 多語端到端流程 |
| AC02／R02 | 顏色尺寸有唯一 SKU；必要上架資訊及真實素材完整 | 商品資料與後台審查 |
| AC03／R03 | 並行購買及重送結帳不超賣、不重複有效交易 | 並行、重送、邊界測試 |
| AC04／R04 | 商品改價後已付款訂單歷史條件仍一致 | 交易快照場景 |
| AC05／R05 | 前台逾時、保留到期或取消後付款成功走可信確認與補償 | 金流沙盒／事件競爭／對帳 |
| AC06／R06 | 訪客可結帳與換裝；會員可使用既定帳號能力 | 訪客及會員旅程 |
| AC07／R07 | 角色不能越權；敏感操作依矩陣可追溯 | 權限正反向驗證 |
| AC08／R08 | 四組及公告尺寸完整三視圖，外觀一致與量測正確 | 素材逐組品質審查 |
| AC09／R09 | 不由角色 M 自動決定衣服 M／訂單尺碼，不保證合身 | UI、文案及選擇狀態檢查 |
| AC10／R10 | 不合格或未審核組合不顯示錯誤換裝，提供實際照片入口 | 相容性正反向案例 |
| AC11／R11 | SKU／價格／庫存與權威資料一致；缺貨不可被圖片誤導為可購買 | 展示至結帳整合測試 |
| AC12／R12 | 首次為女性 A 正面介紹；選定尺寸前不暗中啟用某尺寸換裝 | 全新瀏覽階段檢查 |
| AC13／R13 | 瀏覽期間保留選擇；不產生真實身體特徵推斷 | 跨頁流程及資料用途審查 |
| AC14／R14 | 登入不自動收藏；主動保存有版本關聯；失效清楚揭露 | 登入、收藏、更版與失效場景 |
| AC15／R15 | 訪客／會員可依授權下載，成果清楚標示模特示意 | 匯出及授權審查 |
| AC16／R16 | 素材失敗仍可看照片、尺寸並完成正常購物 | 失敗注入及降級旅程 |
| AC17／R17 | 換裝停用不下架合格商品；缺貨有效展示與不能購買分開 | 後台啟停與缺貨案例 |
| AC18／R18 | 網站沒有 AI 呼叫／照片上傳／本人形象流程，不推斷身體特徵 | 網路、資料流程及權限審查 |
| AC19／R19 | 沒有首版永久公開穿搭配置頁或網址 | 路由、分享與公開資產審查 |
| AC20／R20 | 正常顧客免費無生成額度；一次更換一件商品 | UI、限制與圖層流程 |

暫定 20 組收藏上限須於詳規審查確認後加入邊界案例。需求目前的測試狀態全部為 NOT_EXECUTED；此矩陣不是通過證據。

## 3. 非功能驗收

| ID | 驗收目標 | 測試前須定義 |
| --- | --- | --- |
| N01 | 本機必要：Storefront 能從已就緒的 localhost API／SQL 載入三語商品資料；LCP／CLS lab 數據只作診斷，不作啟動或購物流程閘門。正式 field INP、Core Web Vitals 與 SLA 不屬目前本機範圍 | 三語路由、API／SQL readiness、資料來源與命令；效能深度量測見[選配改善](../options/LOCAL_PERFORMANCE_DEEP_DIVE_OPTION.md) |
| N02 | 素材就緒後換裝／視角切換 P75 ≤ 1 秒 | 就緒判定、開始／完成事件、次數與初次載入區分 |
| N03 | 核心商務每月 99.9% 可用性 | 監測端點、業務成功條件、停機口徑 |
| N04 | RPO ≤ 1 小時，RTO ≤ 8 小時 | 災難情境、起迄、還原資料及金流對帳 |
| N05 | 付款確認後約 2–3 營業日出貨 | 營業日、訂單起點、承運交付與例外 |
| N06 | 客服一般兩營業日／高風險付款一營業日首次回應 | 案件分類、工時與回應定義 |
| N07 | 每日對帳、重大異常及時警示；上線前及初期每季復原演練 | 排程、警示責任、異常分級與演練證據 |

Issue24 的既有三語桌面量測是已保存的工程診斷證據，不是本機服務啟動或交易可用的必要閘門。若未來重新做效能優化，才依[本機深度效能選配](../options/LOCAL_PERFORMANCE_DEEP_DIVE_OPTION.md)固定路由、冷暖快取、樣本與環境，並分開記錄 LCP／CLS。正式 field INP、Core Web Vitals 與 SLA 必須有適當 field 資料來源；不能由 Event Timing 代理或 localhost lab 結果推定。正式網域、RUM 與 CrUX 仍只依[正式網域選配](../options/PUBLIC_DOMAIN_SEO_OPTION.md)評估。

可及性、圖片初次載入、流量及手機記憶體的正式數值門檻尚待建立，不能自填為已確認服務承諾。

## 4. 正式批准檢查表

所有項目預設未完成；沒有原對話核取介面的實際值，不沿用任何看似已勾選的文字。

### A. 產品規格審查

- [ ] 範圍、非目標、品牌暫定狀態及分期順序已理解。
- [ ] 早期決策原文補齊，交易、會員與售後細則可追溯。
- [ ] UI／UX、完整狀態機、權限、資料政策及驗收矩陣完成必要審查。
- [ ] 必要未決項已解決或清楚排除於當前批准範圍。
- [ ] 使用者以明確文字批准指定版本的產品範圍。

### B. 外部查核與實證授權

- [ ] 明確指定查核／技術實證範圍、支出與資料使用。
- [ ] 角色及商品素材的測試使用權與成本已確認。
- [ ] 平台、金物流、效能、復原及素材品質有可審查證據。
- [ ] 條件式選擇依證據由使用者決定。

### C. 實作批准

- [ ] 詳細需求、狀態機、權限與介面／資料契約足以開工。
- [ ] 技術選型、工作量、預算、人力與交付範圍已確認。
- [ ] 本次實作影響、檢查及風險範圍明確。
- [ ] 使用者明確授權實作；文件建立不替代此批准。

### D. 正式收款與營運

- [ ] 商家、商品、條款、個資與發票政策完成適當正式審查。
- [ ] 付款、對帳、退款、庫存競爭、配送與售後驗收通過。
- [ ] 安全、備份還原與交易完整性演練通過。
- [ ] 值班、客服、警示、回復與營運程序就緒。
- [ ] 正式上線及收款取得明確批准。

### E. 換裝另行啟用

- [ ] 四組角色及公告尺寸具有合格三視圖與量測。
- [ ] 公告 SKU／組合具有品質與展示、下載授權。
- [ ] 速度、失敗降級、版本失效與獨立停用驗收通過。
- [ ] 使用者批准啟用範圍，不把核心商店批准延伸為所有模組批准。

## 跨詳規的驗收來源索引

本節只索引，不複製各詳規案例。實際通過狀態須有同候選證據，不能從文件清單或模板推定。

| 範圍 | 唯一詳細條件來源 | 對應 |
| --- | --- | --- |
| 基線 | [READINESS](technical-baseline/READINESS.md) | 必要 M0-AC01–11；M0-AC12 為選配；目前結果見 IMPLEMENTATION_STATUS.yaml |
| 商品／費用 | [商品詳規](../commerce/CATALOG_PLAN.md) | M1-AC01–10 |
| 結帳／訂單 | [結帳詳規](../commerce/CHECKOUT_ORDER_PLAN.md) | M2-AC01–12 |
| 履約／退款／對帳 | [售後詳規](../commerce/AFTERCARE_REFUND_PLAN.md)、[資料對帳](../commerce/RECONCILIATION_DATA_PLAN.md) | M3-AC01–12 |
| 帳號／權限 | [帳號](../identity/ACCOUNT_ACCESS_LIFECYCLE_PLAN.md)、[身份整合](../identity/IDENTITY_API_INTEGRATION_PLAN.md) | IDAC-01–16、IAPI-AC01–10 |
| 會員實付餘額 | [會員儲值詳規](../commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)、[錢包票](../../../issues/member-wallet/05-wallet-recovery-and-delivery.md) | WALLET-AC01–10、FUL-AC01–03；未啟用贈送金標 DEFERRED |
| 固定換裝／素材 | [換裝](../fitting/FITTING_ASSET_OUTFIT_PLAN.md)、[合成矩陣](../fixtures/FITTING_CASES.yaml)、[pilot](../fitting/FITTING_PILOT_PLAN.md)、[匯入](../fitting/ASSET_IMPORT_REVIEW_PLAN.md) | FIT／PILOT／IMP／EVT |
| 換裝 API／工作流 | [FAPI](../fitting/FITTING_API_INTEGRATION_PLAN.md)、[AAPI](../fitting/ASSET_WORKFLOW_API_INTEGRATION_PLAN.md) | FAPI-AC／AAPI-AC |
| 權利來源／PNG | [REG](../fitting/ASSET_EVIDENCE_REGISTRY_PLAN.md)、[PNG](../fitting/PNG_PROCESSING_PLAN.md) | REG-AC／PNG-AC；真素材與人工證據 |
| key／SQL | [憑證](../security/KEY_CERT_LIFECYCLE_PLAN.md)、[SQL](../security/SQL_LEAST_PRIVILEGE_PLAN.md) | KEY-AC／SQL-AC；延後選配 OPS 不自動加入現行範圍 |
| 跨功能品質 | [品質 Profile](../../../quality/QUALITY_PROFILE.yaml)、本文 CHK 與本機案例 | Q-01–10、CHK-01–08、M4-AC01–12 |

## 本機交付驗收 1：候選與環境固定

一次驗收鎖定Git SHA、未提交差異摘要、完整契約版本、fixture版本／dataset、DB schema、API／Nuxt／worker build、SDK與browser版本、實際硬體及Docker平台。候選與實際驗收證據綁定同一SHA；任何產品／契約／fixture改動重新評估受影響checks，不將舊PASS直接套新SHA。獨立 clean-checkout restore/build 與第二位操作者重建屬選配CHK-08，不是本機完成門檻。

工具命令在產品建立後綁定實際路徑；現階段模板command為null，不發明可執行的API／Nuxt命令。17個純規則測試、SQL容器smoke及規劃結構檢查是分開的證據來源，不代替M4產品證據。ARM模擬SQL結果註明平台限制，不宣稱原生x64或正式效能；需要時另補隔離原生x64驗證，不新增主機費用承諾。

## 本機交付驗收 2：驗收案例與必要斷言

| ID | 必要可觀察結果 | Required Checks／追蹤 |
| --- | --- | --- |
| M4-AC01 | 已設定本機環境依文件啟動指定fixture；只loopback、HTTPS與隔離通知；stop保留資料 | 票26 |
| M4-AC02 | 會員A/B、guest與四後台角色不能跨訂單／案件；身份驗證與撤權不靠前台flags | CHK-03；Q-03；票25 |
| M4-AC03 | 三語SSR核心旅程完整；切語系不失SKU/cart/operation；手機錯誤與mock標示可讀 | CHK-04；Q-05；票24 |
| M4-AC04 | 桌面核心旅程的鍵盤／焦點／對比／縮放及限時適用性逐項有範圍相符的證據；自動掃描不替代人工。iPhone Safari／VoiceOver 實機人工驗收為選配，不阻擋本機核心結案 | CHK-05；Q-04；票24；[行動 Safari／VoiceOver 選配](../options/ISSUE24_MOBILE_VOICEOVER_OPTION.md) |
| M4-AC05 | 同一候選之完整OpenAPI/DTO/enum／錯誤映射一致，非只驗M0子集 | CHK-01；Q-08；票26 |
| M4-AC06 | 最後一件20並行只有一承諾、多SKU任一不足全rollback；各原操作重送10次效果唯一 | CHK-02；Q-06；票25 |
| M4-AC07 | 退款來源／原unit／shipping併發不重退；553Unknown保留，本案只425；補償來源獨立 | CHK-02/03；M3-AC04–07；票25 |
| M4-AC08 | crash/restart跨15分鐘保留，不延長expiry；已Started未知只查原操作，兩worker不重發 | CHK-02；Q-07；票25 |
| M4-AC09 | 選配改善：隔離 DB restore 後重套撤權／刪除／hold，核來源台帳、slots／outbox／稽核，不重演交易 | CHK-07；[選配改善計畫](../options/ISOLATED_RESTORE_CUTOFF_OPTION.md) |
| M4-AC10 | 選配改善：固定樣本分開成功／錯誤、冷／熱並重做效能診斷；本機核心只驗服務與必要資料可載入，不以 lab 數據代表正式 SLA | CHK-06；[本機深度效能選配](../options/LOCAL_PERFORMANCE_DEEP_DIVE_OPTION.md) |
| M4-AC11 | 合成reset先preview與授權，只作用指定dataset；不dropDB、刪volume或抹業務Audit | CHK-03；票25/26 |
| M4-AC12 | 所有適用必備checks有候選證據、未決表及限制；交付文件列明已知限制與操作方式 | Q-10；票26 |

DEMO-01–12逐一引用現行mock政策；M0/M1/M2/M3-AC需完整追蹤，不能只以上表12項取代原驗收。只執行本機交付必要且與變更邊界相符的交易、授權、資料一致性、啟動與資料載入檢查；效能診斷、獨立還原與 clean-checkout handoff 依各自選配採用後執行，不列本機服務可運行門檻。不以任意測試總數代替斷言。

## 本機交付驗收 3：故障演練；獨立還原與 clean checkout 為選配

所有必備故障注入先確認專用process／DB／dataset，無法辨識即停止；不停止其他Docker專案、不刪volume。交易 commit 截點、worker crash/restart、Unknown 與 outbox 重送仍屬核心驗收；獨立 DB restore／cutoff 對帳不列本機整體驗收門檻。

演練截點：交易commit前（整筆rollback）；commit後但dispatch前（持久Outbox續處理原Operation）；mock已Started但結果尚未入帳（Unknown保留且查原結果）；結果入帳後通知前（只重投原通知）；refund成功但receipt未完成（只續調整）。每截點記前後Order、Reservation、Movement、CollectionBalance、unit/shipping、Operation、Outbox與Audit狀態，不在日誌記secret或整包PII。

隔離 restore、cutoff 後撤權／刪除／hold 重套、pending 對帳與 dispatch 解禁條件，完整目標與安全前置集中於[選配改善計畫](../options/ISOLATED_RESTORE_CUTOFF_OPTION.md)。尚未執行仍維持未執行事實，但不阻擋本機核心結案；正式 RPO／RTO 不由本機演練推定。

## 本機交付驗收 4：判定與交付

每check記NOT_EXECUTED/PASS/FAIL/BLOCKED/UNVERIFIED與scope、命令、斷言、候選、限制。缺工具或必要授權實證用BLOCKED，未跑用NOT_EXECUTED，證據不完整用UNVERIFIED；N/A須適用性理由與覆核，不為失敗排除check。FAIL修復後只重跑受影響範圍與必要整合，保留舊結果關聯。

LOCAL_COMPLETE只在適用必備checks、主要成功／拒絕旅程均具完整證據，核心授權／交易／worker recovery 未決為零時評估；獨立 clean-checkout restore/build、第二位操作者重建，以及資料庫 restore／cutoff 均列選配，不作本機結案前置。使用者尚未採用之品質提案須先確認有效判準。無資料損失／越權／重退／超賣可接受例外；其餘已知限制列影響與後續，不自行降門檻。來源綁定與實測仍依個別案例補齊。

交付包含候選、setup/start/stop/reset真命令、私有設定取得方法、合成情境、checks摘要、版本／平台限制、故障處理與延期清單。restore 命令只在另行採用選配改善後編入其專屬交付。沒有真密碼／session／Email或provider憑證；驗收媒體去除token／身份資料。報告與測試產物預設先暫存，本機交付若需長期保存再確認位置／期限；本輪只保存規格模板，不保存原始輸出或效能trace。

## 規劃檢查（非產品證據）

規劃檢查涵蓋必備CHK-01–06、選配CHK-07/08、12個M4-AC引用、DEMO-01–12與Q-01–10集合、未執行狀態、選配範圍、文件連結及票24–26的工作狀態／依賴清單；這只證明文件結構一致，沒有執行setup、reset、故障注入、restore或產品驗收。

## M1–M3契約深化後的準備狀態

M1–M3機器契約已由[整合提案](../contracts/API_M1_M3.openapi.yaml)展開，與M0差異見[一致性登錄](../contracts/API_CROSS_STAGE_CONSISTENCY.md)；前述「待展開」狀態由本節更新為READY_FOR_REVIEW，尚非採用。登入強化選配與完整身份維護契約不屬本輪；真產品命令／驗收仍未完成，全部M4產品案例NOT_EXECUTED。


## 跨階段檢查責任

這是 CHK 唯一定義；主計畫只引用 ID。每次需記 candidate、fixture／schema／平台版本、實際命令、觀察斷言、結果、限制與人工覆核。證據模板是結構，不是執行結果。

| ID | 檢查範圍 | 適用時的證據 |
| --- | --- | --- |
| CHK-01 | 採用契約、DTO、enum、版本／錯誤一致 | 文件結構＋runtime 序列化／拒絕 contract tests，操作與 consumer 對應 |
| CHK-02 | 交易、冪等、競爭、worker／重啟 | 真 dedicated SQL、不同 client／process、固定 clock 與可控失敗；錢包另驗雙花；本機會員 checkout 核餘額不足、重送、併發扣款及訂單／reservation／ledger 原子回滾 |
| CHK-03 | 物件／角色／scope、近期驗證、撤權與 CSRF | 成功／拒絕／跨會員／舊 session／重放；網路及日誌無秘密／不必要個資；local-development-bootstrap 另核精確 endpoint／marker／schema／空白身份前置與失敗原子回復 |
| CHK-04 | 真 API／SQL browser 核心旅程與三語 SSR | 成功、空白、失敗、Unknown、reload／切語、權限拒絕；mock UI 證據分列 |
| CHK-05 | 桌面鍵盤／焦點／縮放／對比／限時；responsive viewport 自動化 | 桌面人工項逐項記錄適用性與結果；responsive viewport 只驗布局與溢位。iPhone Safari／VoiceOver 實機人工檢查列於[選配計畫](../options/ISSUE24_MOBILE_VOICEOVER_OPTION.md)，採用後才執行，不能只以無自動錯誤宣稱完整 AA |
| CHK-06 | 選配：效能與資源深度診斷 | 僅採用[本機深度效能選配](../options/LOCAL_PERFORMANCE_DEEP_DIVE_OPTION.md)後，量測固定環境冷／熱、成功／失敗及樣本；不得混為 SLA |
| CHK-07 | 選配：備份與隔離還原、cutoff 後對帳 | 若採用選配改善，於新目標核 restore、撤權／刪除／hold 重套、未知額度及來源去重，不重演帳務；不屬本機整體必備檢查 |
| CHK-08 | 選配：clean checkout source restore/build 與獨立 handoff | 僅於採用[選配改善計畫](../options/CLEAN_CHECKOUT_REBUILD_HANDOFF_OPTION.md)後，使用新 checkout／獨立 NuGet 套件目錄驗 locked restore/build，並由第二位操作者重跑專用 setup／seed／read-only fixture 核對；不屬本機整體驗收 |

## 最終交付閘門與示範腳本

| 閘門 | 必須完成的事項 | 驗收與產品示範 | 不得混同 |
| --- | --- | --- | --- |
| FINAL-CORE | kohami/04–06、08–26 的本機範圍；會員儲值僅採 member-wallet/01–03 本機合成帳戶／測試入帳子範圍 | 既有 demo catalog→顧客三語搜尋／篩選→登入合成會員→加車→完整費用→checkout→內部 API 合成餘額扣抵→訂單與帳本結果／重送／不足／競爭→本人訂單；另外依各票執行履約／售後／退款需要的 role／scope SQL 案例。必要交易不變條件須用 dedicated SQL 證據；Apple Developer／Secure Enclave 非本機購物前置。第三方付款與正式資金不在範圍 | 第一個完整本機顧客購物版本；合成餘額案例不稱為實際付款或完整錢包；正式監控、備份／還原、RPO／RTO 另列未驗證，不阻擋本機交付 |
| FINAL-WALLET | member-wallet/01–05；本機入金與退款契約已審閱、實付帳本與餘額、會員扣抵、人工履約及對帳就緒 | 合成可信入金→本人餘額／帳本→餘額扣抵→原子訂單／庫存→退款原來源模擬→對帳；不足／未知／重送／並行雙花／超賣／超退／跨會員拒絕。WALLET／FUL 適用案例與必備 CHK 具證據 | 實付版；不發放／扣抵贈送金，贈送相關條件為 DEFERRED，不假稱完整贈送規格 PASS；不收真資金 |
| FINAL-FITTING | kohami/27–37；合法非 AI 素材、權利／參數／quota 決策與實證、四組方向及公告尺寸／合格 SKU、版本及匯出就緒 | 初次角色介紹→明選身形→選 SKU→正／右側／背→不支援／失敗回真商品→登入不自動存→主動存原版→更版／撤權提示→依權利下載；真裝置 cold/warm／記憶體／流量、素材人工品質、撤銷 cache／跨 worker 驗證 | Pilot 一組通過不是四組完成，約 20 SKU 為原方向而非已合格數；換裝失效不能破壞購買 |
| FINAL-LAUNCH | kohami/07、production-launch/01 與本次所選 CORE／WALLET／FITTING 版本；真實業務、資金、資料、部署與營運規則明確 | 正式模式與 mock 隔離、真來源可信性／退款去向、實際角色與費用／通知／憑證、商品與條款查核、完整權限與負向旅程及監控警示；獨立 restore／cutoff 演練列選配改善，正式營運復原目標另依上線決策審查 | 本機完成不能直接上線；通過上線前驗收也不表示已有一個月營運數據 |
| FINAL-OPS | 正式啟用後的 kohami/38 | 按確認口徑與完整期間取得每日對帳、出貨／客服、月可用性與實際成本；不足標未驗證，故障保留整改。獨立 restore／cutoff 留在選配改善 | 正式營運後觀測；不拿模擬時鐘或本機 benchmark 宣稱正式 SLA 達標 |

同一候選下重大授權、資料／交易一致性、不可恢復資料損失等缺陷不得以已知限制略過。一般 N/A 需適用性理由；FAIL 修正重跑受影響案例，未跑標 NOT_EXECUTED，缺依賴標 BLOCKED，證據不完整標 UNVERIFIED。核心、實付餘額、換裝、上線及營運後觀測分別給結論，不能只寫「網站完成」。
