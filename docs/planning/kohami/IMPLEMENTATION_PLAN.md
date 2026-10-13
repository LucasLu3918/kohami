# 本機完整 mock：品質、里程碑與交付計畫

> **現行 MFA 決議（2026-10-13）：** 使用者明確「先採用B」，第一版後台採 Email／密碼＋驗證器 TOTP，Passkey 留後續。下文先前「待決／Passkey優先」是歷史狀態，已由此決議取代。方案選擇阻擋已解除；註冊／proof／復原機器契約與產品實證仍待完成，不能標為 PASS。詳見 [身份詳規](AUTH_SECURITY_PROFILE.md)。

日期：2026-10-13｜READY_FOR_REVIEW｜未批准產品實作／未執行本輪產品測試

## 1. 輸入與交付成果

採用的方向：全外部商務服務mock、本機完整展示、ASP.NET Core／Nuxt SSR／SQL Server。新工程與視覺詳規供審閱；不能從本文件標記反推使用者已接受全部細節或允許正式部署。

交付目標為可重建的核心購物、後台商品／庫存與單人授權退款旅程；使用合成資料、隔離通知、持久化mock結果。換裝素材與正式服務延期，仍保留38張票，政策規格完成不等於產品完成。

閱讀依序：[畫面](EXPERIENCE_DESIGN.md) → [視覺](VISUAL_SYSTEM.md) → [領域](DOMAIN_MODEL.md) → [API](API_SPEC.md) → [架構](TECHNICAL_ARCHITECTURE.md) → [品質Profile](../../quality/QUALITY_PROFILE.yaml)。

## 2. 可展示里程碑與依賴

| 里程碑 | 可展示成果／必要證據 | 票券關係 | 預估工作量（人日，提案） |
| --- | --- | --- | --- |
| M0 基準 | 真.NET連SQL、migration/seed隔離；Nuxt SSR三語骨架；身份／MFA候選與受控adapter最小實證；固定契約／環境命令 | 04/05/06實證，保持依賴至各票符合完成條件 | 2–4 |
| M1 商品到費用確認 | 三語商品後台／列表／詳細、庫存、cart、Quote、運費版本與下架／缺貨狀態 | 08–11、17相關切片；07正式品牌／真商品延期部分不得誤稱查核完成 | 6–10 |
| M2 結帳到自己的訂單 | 訪客／會員、SQL全SKU保留、付款成功／未知／到期晚到補償、快照、受保護查詢與通知 | 12–17；不以服務商延期清單阻擋mock，但04/05仍須完成mock證據 | 8–12 |
| M3 管理配送與退款 | 模擬配送／退回／Inspection、Owner單人退款、可退額度、憑證調整、台帳及稽核 | 18–23；完整流程需M2，不只做一個退款按鈕 | 8–12 |
| M4 本機可重現交付 | 三語／手機／可及性、併發與重啟／restore、文件／樣本／啟停命令、限制與交付審閱 | 24–26；38真實營運仍延期；換裝27–37另交付 | 4–8 |

M0各情境可並行調查，但交易與身份實證未過不宣稱票06 resolved。每票仍按原依賴與本階段範圍結案，不因表格切片完成就解除全部阻擋。

## 3. 工時、成本與信心限制

估算是假設性工作量，不是報價或截止承諾：熟悉.NET/Nuxt的小型團隊、核心mock包含必要後台與MFA、不含完整換裝／真素材製作／對外部署。初估樂觀20–30人日、預期35–50人日、風險調整55–80人日；信心低至中。里程碑分項為工作量區間，不能直接等同日曆時間；整合／校對／缺陷修正另有餘裕，MFA整合實證與SQL競爭是主要估算不確定性。

現有程式只是純規則實驗，不把17測試視為大部分產品已完成。M0後依實際整合改動與工具支援重估；未有團隊工時／費率不填金額。新雲端／金流物流發票費用本輪N/A；開發設備、時間、電力、素材授權與維護仍有成本。NT$8,000/月原規劃不是本機完整交付總預算。

## 4. 測試裝置、資料與方法

固定可重建fixtures：至少3商品、6SKU（含缺貨／下架／多顏色尺寸）、20單位／最後一件場景、非零示意運費、優惠捨入／同餘分攤、多SKU不足、部分與全額退款、失效費用版本、4後台角色＋撤權、會員A/B＋guest、每種adapter結果。數字是測試資料，不是正式商品／費率政策。

瀏覽器提案：Playwright Chromium／Firefox／WebKit＋Mac Safari人工；WebKit自動化不稱已測真Safari。桌面1440px、手機390px與320px邊界、200%縮放／400%reflow；記錄實際browser/build、硬體、網路、fixture與sample。不用裝置模擬宣稱所有真手機通過。

主要端到端：商品→cart→費用→mock付款→自己的訂單→取消／退貨→管理者退款→顧客狀態＋憑證。失敗注入：斷線後同操作查詢／重送、逾時未知、金額不符、事件重複／亂序、最後一件與多SKU並行、退款未知／併發、角色撤銷中途、worker雙實例、process crash、SQL rollback／獨立restore。

可及性提案以[W3C WCAG2.2](https://www.w3.org/TR/WCAG22/) AA作目標；手動適用性、鍵盤／VoiceOver／焦點與交易限時檢查不可省略。不因axe/Lighthouse無錯就宣稱符合全部AA。15分鐘保留屬既有商務規則，須對timing要求給可驗證的解法／必要性評估，不用偷偷延長保留掩蓋衝突。

## 5. Required Checks（待實作綁定命令）

目前已有140操作canonical候選，但無產品solution、Nuxt app或E2E config，下表不是可執行命令清單；實作M0時記錄真實路徑／命令／版本。既有 `dotnet test` 純規則與 `./infra/sql-server/manage.sh check` 可驗既有範圍，不替代下列checks。

| Check ID | 覆蓋／追蹤 | 方法與完成證據 |
| --- | --- | --- |
| CHK-01 | OP所有展開、DOM enum、Q-08 | OpenAPI解析、範例及唯一operationId；DTO錯誤映射／serialisation contract tests |
| CHK-02 | DEMO-02/03/07/08、Q-06 | 真SQL交易／並行／退款額度／重啟；固定clock與兩worker，test report綁candidate |
| CHK-03 | DEMO-05/10/11、Q-03 | 後端物件授權/MFA/撤權/CSRF、secret與PII網路／日誌檢查 |
| CHK-04 | SCR核心旅程、DEMO-01/04/06/09/12、Q-05 | 瀏覽器E2E成功／空白／失敗及三語SSR、所有結果明示mock |
| CHK-05 | Q-04 | 自動掃描＋手動鍵盤／VoiceOver／縮放／對比／timing；記錄適用條文和限制 |
| CHK-06 | Q-01/02/09 | 固定環境benchmark／browser traces＋CPU/RSS/圖片體積；不得把失敗請求混成成功延遲 |
| CHK-07 | DEMO-03/07/10、Q-07 | 獨立mock DB備份／restore、outbox與pending額度／刪除清單重套、對帳不重演交易 |
| CHK-08 | Q-08/10、26 | clean setup/seed/start/stop/reset docs、候選差異與狀態/權限追蹤、人工交付審閱 |

每次報告：candidate Git SHA、fixture版本、真實命令、條件、結果、限制；NOT_EXECUTED保持未執行。必須確認主要成功與拒絕行為，不只status code或測試數量。

## 6. LOCAL_COMPLETE 與批准邊界

LOCAL_COMPLETE需：新環境照文件建立同一合成資料集；啟停不污染現有SQL或資料；前後台完整核心mock旅程；核心三語和權限；所有適用Required Checks有candidate證據，重大阻擋零；12個DEMO＋Q條件追蹤完備；收件匣／重設無跨身份或跨環境能力；既有實驗與新產品證據清楚分開。

本輪產出是規劃審閱包（Gate1準備），不發明Human approval。先審閱／採用新架構、身份與視覺／品質／估算提案，再確認實作順序與首里程碑（Gate2）；不能以「繼續規劃」推定批准產品實作。這些關卡不阻擋本輪把規劃寫完整。

正式啟用仍需資格、費率、真服務契約／沙盒、金融授權人員、法定資料分類／條款、正式主機授權及真實安全復原；本機mock LOCAL_COMPLETE不是正式營運。Wordv0.1保持標示過時，不新增閱讀版以免本輪提案被誤當已批准規格。

## M0具體契約更新

首批範圍與12個M0-AC情境見[M0_READINESS](M0_READINESS.md)。M0 subset是歷史參考，現行已有canonical候選，驗證parser/schema/reference只算文件結構；身份細則與契約採用待完成不作產品開工批准。M0-AC08沿已採MFA B，仍需本機TOTP實證，其他SQL／獨立DTO規劃可繼續。

## M1規劃深化

票08–11的本階段商品／庫存／搜尋／cart／Quote行為見[M1_CATALOG_PLAN](M1_CATALOG_PLAN.md)，10個M1-AC情境及合成三語fixture可作下一輪規劃／實作輸入。兩張靜態線框已渲染並目視檢查，這不是產品瀏覽器E2E或Q標準通過；M1完整DTO/機器契約仍須由提案採用後展開。MFA細則採用與實證待完成不解除後台實作M0依賴。

## M2規劃深化

票12–17參照[M2詳規](M2_CHECKOUT_ORDER_PLAN.md)、[訪客存取](GUEST_ORDER_ACCESS.md)與12個M2-AC。以M1原快照1590合成案例承接成功／Unknown／Expired／Cancelled／晚到及額外成功，原子與權限須真正SQL/API/瀏覽器驗證；靜態線框／fixture核對不代替完成。MFA細則採用與實證待完成不阻擋這批文件，但受控Owner情境實作仍不能繞過M0後台身份閘門。

## M3深化交付範圍（提案）

票18–23按原依賴推進，以[M3詳規](M3_AFTERCARE_REFUND_PLAN.md)12項M3-AC及[對帳資料提案](RECONCILIATION_DATA_PLAN.md)為驗收來源。先凍結完整M3來源／商品／shipping-only／補償契約，再實作授權及來源台帳、履約／驗收、受控退款／獨立憑證、差異與合成dataset重設。必須真SQL測額度／單位／運費競爭與去重，瀏覽器驗角色／MFA／顧客狀態，Outbox crash與獨立DB restore留證。靜態圖／fixture算例不滿足上述驗收；MFA細則採用與實證待完成仍阻擋相依身份實作。下一待深化為M4本機驗收、復原與交付操作。

## M4規劃深化

票24–26參照[M4驗收](M4_LOCAL_ACCEPTANCE_PLAN.md)12項案例、[本機操作](LOCAL_DEMO_RUNBOOK.md)與[證據模板](M4_EVIDENCE_TEMPLATE.yaml)。先綁candidate／環境／真命令，再核三語／權限／可及性、SQL競爭／crash／隔離restore及獨立重建審閱。文件ready不等於產品checks PASS；本機LOCAL_COMPLETE與正式營運／換裝啟用保持不同證據邊界。

## M1–M3機器契約深化狀態

本輪[整合OpenAPI與差異](API_CROSS_STAGE_CONSISTENCY.md)已展開M1–M3範圍，替代先前「完整M1–M3待展開」的工作狀態；仍需審閱採用與真正DTO/SQL/API產品驗證。MFA/帳號契約已有補充及canonical候選，採用與產品命令仍待完成，不稱Gate2或LOCAL_COMPLETE。CHK-01先跑文件腳本，再以實作之序列化與錯誤contract tests綁candidate。


## Phase 2規劃交接（不併入M0–M4完成）

[FITTING_ASSET_OUTFIT_PLAN](FITTING_ASSET_OUTFIT_PLAN.md)補齊票27–37中的素材／版本／收藏／匯出語意，原票阻擋關係與Work status保留。接續FOP機器契約及身份授權差異、合法素材實證／成本／裝置量測，再依票29–37完整旅程實作與驗收。首組實證不等於四組或20 SKU公開可用；核心M4完成也不等於Phase 2完成。


## 規劃整合與开工前交接

[PLANNING_HANDOFF](PLANNING_HANDOFF.md)及[PLANNING_READINESS](PLANNING_READINESS.yaml)以核心104/Phase2_36分列待採用與Required Checks；[CANONICAL_CONTRACT_PLAN](CANONICAL_CONTRACT_PLAN.md)規劃來源/ref/Session/Problem/MFA/權限／ETag/event／consumer整合。已建立未採用canonical候選、未綁產品命令、未批准Gate2；來源checker PASS是doc stage，不改8個product CHK/38票狀態。MFA B已採用，其安全細則及bootstrap/source／runtime證據另補。


## M0來源與命令綁定交接

[M0來源矩陣](M0_SOURCE_BINDING_PLAN.md)與[空模板](M0_SOURCE_BINDING_TEMPLATE.yaml)把DTO/SQL/SSR/Owner/key/TOTP/worker/clock/restore/fixture/交付逐項對應12情境与CHK。SOURCE_BOUND不是產品PASS或新批准；具體source與literal命令仍null，Phase2不併入M0。
