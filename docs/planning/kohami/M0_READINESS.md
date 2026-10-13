# M0 首批實作準備與驗證情境

日期：2026-10-13｜READY_FOR_REVIEW（獨立部分）／MFA dependent NEEDS_DECISION

本輪只做規劃。首批成果是「真.NET／SQL＋Nuxt本機SSR＋身份閘門＋受控mock adapter」的垂直實證，不是完整商店；框架、全mock、本機限定已確認，細節仍提案。

## 1. 合約輸入與實作範圍

- [身份詳規](AUTH_SECURITY_PROFILE.md)：匿名／會員／管理MFA／guest／checkout能力分離；後台方案待選。
- [SQL結構](SQL_SCHEMA_PLAN.md)：型別、複合FK、rowversion／ETag、原子多SKU及退款額度、重啟／還原。
- [M0 OpenAPI](API_M0.openapi.yaml)：PROPOSED的13個operation、20個DTO。現有[46個OP總綱](API_SPEC.md)仍涵蓋完整核心，不被subset刪除或悄悄取代。

M0只使用合成SKU、cart、quote、訂單及退款案件fixtures；管理退款由已模擬付款fixture驗額度，未交付後台商品／售後完整UI。M1–M3才建正式核心mock旅程；M0可同時驗證必要邊界，不假稱後續票已完成。

先後順序：契約／fixture及額度規則 → 純DTO與真SQL連線／migration → 最小Nuxt SSR＋同origin入口 → 初次登入與選定MFA證據 → mock operation及持久化worker → 併發／重啟／原候選報告。依賴未解只停止相關部分，不重新詢問已確認框架／本機／mock。

## 2. 最小fixtures與可觀察情境

| M0驗收ID | 輸入／操作 | 可觀察預期／拒絕邊界 | 追蹤 |
| --- | --- | --- | --- |
| M0-AC01 | 固定本機dataset與SKU／Identity fixtures，fresh DB與已有seed升版 | 不跨dataset FK或讀資料；啟動顯示mock，未知schema拒ready，不丟其他DB資料 | DOM-01/10/12、Q-03/08 |
| M0-AC02 | Browser取得CSRF→create cart兩次同key，故障後同key重送 | 一個cart；CSRF/BrowserId輪替不重建原command；錯token/origin拒絕 | OP-03/17、Q-03/06 |
| M0-AC03 | Quote過期／價變／無費用／不支援區域／小數或溢位 | 422/409及清楚code；保留cart，不保留部分SKU、不用0元默認 | OP-06/07、DEMO-04 |
| M0-AC04 | 最後一件20個不同client並行，多SKU其中一件不足 | 最後一件一筆承諾；多SKU全成功或全rollback；可查SQL movement／snapshot | DOM-02/05、DEMO-03、Q-06 |
| M0-AC05 | 同一Checkout付款重送、明確失敗／Unknown／額外成功事件 | 同操作唯一；Unknown不重付；額外成功列異常；前台success不能改DB | OP-08/09/35、DEMO-02 |
| M0-AC06 | 用固定clock在15分鐘到期、取消、成功競爭；kill/restart | Consume或Release一條；晚到成功補償，不恢復原單；未決事件跨重啟有對帳 | DEMO-03/08、Q-07 |
| M0-AC07 | 支付fixture退款不同line/同line併發，含原shipping、Unknown與明確未受理Failed | 金額／原品項Qty／shipping都不超退；Unknown保留、Failed確定未執行才釋放；撤權後已發起仍查核 | OP-26/27、DEMO-07 |
| M0-AC08 | 後台password stage、選定MFA proof、錯origin/User/UV/過期、實際本機裝置 | 未MFA無後台permission；近期proof才能退款；virtual authenticator不替代真裝置證據 | AUTH_PROFILE、DEMO-05、Q-03 |
| M0-AC09 | 撤權／登出／復原碼重放；SSR切persona／回上一頁 | 舊管理session不可發新操作；原結果可由系統追蹤；沒有HTML/cache洩漏他人資料或秘密 | DOM-10/11、Q-03 |
| M0-AC10 | 受控success/decline/unknown/duplicate/mismatch、worker雙實例與outbox lease過期 | 一次有效效果；event同ID不同hash列異常；claimed不假稱已發起；新dispatch依現行權限裁決 | DEMO-02/10、Q-06/07 |
| M0-AC11 | Nuxt zh-TW/ja/en首次SSR、無JS查看摘要及失敗結果 | HTML含必要商品／mock標示，noindex；locale不改transaction ID；不要求完整M1設計稿 | SCR-01/04/05、Q-05 |
| M0-AC12 | 指定mock DB備份→獨立restore；secret/key權限及刪除／撤權重套 | 不覆寫原DB、不復活撤權／刪資料、不重演refund；記未決額度／庫存台帳差異 | Q-07、CHK-07 |

目前全部產品情境NOT_EXECUTED。OpenAPI解析／schema範例檢查只算文件契約結構證據，不算M0-AC通過。

## 3. 執行命令與證據模板

既有可用命令：`./infra/sql-server/manage.sh status`／`check`；純規則實驗命令見[experiment README](../../../experiments/commerce-baseline/README.md)。本輪不重跑既有實驗，這些命令也不能證明新產品M0通過。

產品資料夾尚未建立；建議未來apps/api、apps/storefront、tests/core-integration與tests/browser，但路徑不當成已存在。M0實作時才寫真實dotnet／Nuxt／browser命令，固定SDK／lockfile；現在沒有可宣稱成功的產品command。

每案例證據最小欄位：CaseId、candidate SHA、fixture/schema版本、host與SQL/browser版本、真實命令、time/clock、成功／拒絕／故障結果、限制。兩worker或多client記process差異，不能以多次循序呼叫充當併發。只存必要去識別結果，未獲同意不長期保存使用量trace／secret／原始請求。

## 4. M0完成與後續票

M0完成須：選定身份機制且AC08/09有實證；13-operation subset與新增身份proof契約一致；SQL/SSR/worker核心邊界對應情境有候選證據；無未決重大缺陷；本機setup/seed/start/stop清楚且不污染SQL環境。API machine contract必須先採用或修訂；本輪不創造Gate1/2批准。

完成M0不自動把06結案；依票06完整品質／成本／部署邊界驗收判斷。04/05按mock契約的證據結案，正式服務清單仍延期；08起商品旅程沿任務圖推進。換裝與收藏20組不屬M0；真物流/金流/發票／人員／費率仍延期。

未決項：後台MFA選擇、HTTP／Identity工具相容性及更完整schema實證。選擇尚未回覆時，只停依賴的proof contract／採用，不停止SQL或獨立DTO規劃。首批開始產品實作仍需使用者明確指示，不能把再次「規劃」讀成建立app／migration授權。

## 本輪文件結構檢查（非產品驗收）

2026-10-13：OpenAPI3.1.0 spec validator、13個唯一operationId／20schema／129引用、20個正schema範例與8反例、內部連結與diff whitespace檢查通過。DTO範例各自是合成結構示例，不代表已串成可執行fixture或完成金額／權限／SQL業務測試。唯讀複核完成，MFA選擇仍NEEDS_DECISION，M0-AC仍全部NOT_EXECUTED。

檢查工具版本：openapi-spec-validator 0.9.0、jsonschema 4.26.0；僅使用既有本機runtime，沒有安裝套件。
