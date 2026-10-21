# 本機技術與交易基線驗收

## 現行適用範圍（2026-10-15）

本文保留需求、設計與具日期的驗證依據；目前已有 .NET／Nuxt／SQL 程式、core34 採用子集與部分產品實證。完整 131 操作候選已建立，核心細則已部分採用；其餘功能／Phase2 不因此視為已批准或完成。角色與資源授權由後端裁決；可選登入強化見[選配清單](../../options)。產品逐項狀態、範圍與來源優先順序請由[文件入口](../../../../README.md)查閱；舊段落中的尚未建立／未決／全部未執行須依原日期與案例範圍解讀。


日期：2026-10-17｜READY_FOR_REVIEW；身份安全細則／來源／實證依各自狀態管理

本輪只做規劃。首批成果是「真.NET／SQL＋Nuxt本機SSR＋身份閘門＋受控mock adapter」的垂直實證，不是完整商店；框架、全mock、本機限定已確認，細節仍提案。

## 1. 合約輸入與實作範圍

- [身份詳規](../../identity/AUTH_SECURITY_PROFILE.md)：匿名／會員／管理／guest／checkout能力分離；登入強化選配見身份安全基線。
- [SQL結構](../../architecture/SQL_SCHEMA_PLAN.md)：型別、複合FK、rowversion／ETag、原子多SKU及退款額度、重啟／還原。
- [Foundation OpenAPI](../../contracts/API_BASELINE.openapi.yaml)：歷史PROPOSED子集13個operation、20個DTO；現行採用候選以[canonical整合](../../contracts/CANONICAL_INTEGRATION_REPORT.md)為準。現有[46個OP總綱](../../contracts/API_SPEC.md)仍涵蓋完整核心，不被subset刪除或悄悄取代。

Foundation只使用合成SKU、cart、quote、訂單及退款案件fixtures；管理退款由已模擬付款fixture驗額度，未交付後台商品／售後完整UI。M1–M3才建正式核心mock旅程；Foundation可同時驗證必要邊界，不假稱後續票已完成。

先後順序：契約／fixture及額度規則 → 純DTO與真SQL連線／migration → 最小Nuxt SSR＋同origin入口 → 登入／授權與敏感操作重新驗證證據 → mock operation及持久化worker → 併發／重啟／原候選報告。依賴未解只停止相關部分，不重新詢問已確認框架／本機／mock。

## 2. 最小fixtures與可觀察情境

| Foundation驗收ID | 輸入／操作 | 可觀察預期／拒絕邊界 | 追蹤 |
| --- | --- | --- | --- |
| Foundation-AC01 | 固定本機dataset與SKU／Identity fixtures，fresh DB與已有seed升版 | 不跨dataset FK或讀資料；啟動顯示mock，未知schema拒ready，不丟其他DB資料 | DOM-01/10/12、Q-03/08 |
| Foundation-AC02 | Browser取得CSRF→create cart兩次同key，故障後同key重送 | 一個cart；CSRF/BrowserId輪替不重建原command；錯token/origin拒絕 | OP-03/17、Q-03/06 |
| Foundation-AC03 | Quote過期／價變／無費用／不支援區域／小數或溢位 | 422/409及清楚code；保留cart，不保留部分SKU、不用0元默認 | OP-06/07、DEMO-04 |
| Foundation-AC04 | 最後一件20個不同client並行，多SKU其中一件不足 | 最後一件一筆承諾；多SKU全成功或全rollback；可查SQL movement／snapshot | DOM-02/05、DEMO-03、Q-06 |
| Foundation-AC05 | 同一Checkout付款重送、明確失敗／Unknown／額外成功事件 | 同操作唯一；Unknown不重付；額外成功列異常；前台success不能改DB | OP-08/09/35、DEMO-02 |
| Foundation-AC06 | 用固定clock在15分鐘到期、取消、成功競爭；kill/restart | Consume或Release一條；晚到成功補償，不恢復原單；未決事件跨重啟有對帳 | DEMO-03/08、Q-07 |
| Foundation-AC07 | 支付fixture退款不同line/同line併發，含原shipping、Unknown與明確未受理Failed | 金額／原品項Qty／shipping都不超退；Unknown保留、Failed確定未執行才釋放；撤權後已發起仍查核 | OP-26/27、DEMO-07 |
| Foundation-AC08 | 帳號登入與敏感操作的session、proof及錯誤綁定 | 會員驗證不授予後台permission；管理操作依有效 permission／scope | AUTH_PROFILE、DEMO-05、Q-03 |
| Foundation-AC09 | 撤權／登出／復原碼重放；SSR切persona／回上一頁 | 舊管理session不可發新操作；原結果可由系統追蹤；沒有HTML/cache洩漏他人資料或秘密 | DOM-10/11、Q-03 |
| Foundation-AC10 | 受控success/decline/unknown/duplicate/mismatch、worker雙實例與outbox lease過期 | 一次有效效果；event同ID不同hash列異常；claimed不假稱已發起；新dispatch依現行權限裁決 | DEMO-02/10、Q-06/07 |
| Foundation-AC11 | Nuxt zh-TW/ja/en首次SSR、無JS查看摘要及失敗結果 | HTML含必要商品／mock標示，noindex；locale不改transaction ID；不要求完整M1設計稿 | SCR-01/04/05、Q-05 |
| Foundation-AC12（選配） | 由乾淨 checkout 執行 locked restore/build，並由第二位操作者重跑 dedicated setup／seed／fixture 核對 | 限新建隔離 checkout 與專用 fixture；不得 reset 展示資料 | 選配CHK-08；[選配計畫](../../options/CLEAN_CHECKOUT_REBUILD_HANDOFF_OPTION.md) |

本 readiness 清單建立時全部產品情境均為 NOT_EXECUTED。2026-10-14 已在專用 localhost:14336 synthetic fixture 完成 Issue 01 對應的 Foundation-AC03–05 購物旅程驗收；其餘項目維持各自狀態，詳見 [Foundation 實作狀態](IMPLEMENTATION_STATUS.yaml) 與[驗證紀錄](../../../../architecture/VERIFICATION.md)。OpenAPI 解析／schema 範例檢查只算文件契約結構證據，不算 Foundation-AC 通過。

## 3. 執行命令與證據模板

既有可用命令：`./infra/sql-server/manage.sh status`／`check`；純規則實驗命令見[experiment README](../../../../../experiments/commerce-baseline/README.md)。本輪不重跑既有實驗，這些命令也不能證明新產品Foundation通過。

現已有 apps/api、apps/worker、apps/storefront 與 tests。實際工具及驗證紀錄見文件入口與架構驗證；本來源綁定模板仍須按個別候選補齊，不把空模板當作不存在產品程式。

每案例證據最小欄位：CaseId、candidate SHA、fixture/schema版本、host與SQL/browser版本、真實命令、time/clock、成功／拒絕／故障結果、限制。兩worker或多client記process差異，不能以多次循序呼叫充當併發。只存必要去識別結果，未獲同意不長期保存使用量trace／secret／原始請求。

## 4. Foundation完成與後續票

Foundation完成須：身份／授權契約與AC08/09有實證；本Foundation操作切片與現行canonical核心／身份proof契約一致；SQL/SSR/worker核心邊界對應情境有候選證據；無未決重大缺陷；本機setup/seed/start/stop清楚且不污染SQL環境。API machine contract必須先採用或修訂；本輪不創造Gate1/2批准。

完成Foundation不自動把06結案；依票06完整品質／成本／部署邊界驗收判斷。04/05按mock契約的證據結案，正式服務清單仍延期；08起商品旅程沿任務圖推進。換裝與收藏20組不屬Foundation；真物流/金流/發票／人員／費率仍延期。

未完成項：canonical/安全細則採用、實際Identity／SQL／SSR來源綁定與產品實證。已確認的授權與交易方向不重問；尚未採用的proof/權限細則只阻擋相依實作，獨立規劃可繼續。首批開始產品實作仍需使用者明確指示，不能把再次「規劃」讀成建立app／migration授權。

## 本輪文件結構檢查（非產品驗收）

2026-10-13：OpenAPI3.1.0 spec validator、13個唯一operationId／20schema／129引用、20個正schema範例與8反例、內部連結與diff whitespace檢查通過。DTO範例各自是合成結構示例，不代表已串成可執行fixture或完成金額／權限／SQL業務測試。上述為當時文件結構檢查；歷史驗證結果僅保留於架構驗證紀錄，其餘案例依實際執行狀態記錄。

檢查工具版本：openapi-spec-validator 0.9.0、jsonschema 4.26.0；僅使用既有本機runtime，沒有安裝套件。


## 現行來源綁定與驗收交接

[Foundation來源詳規](SOURCE_BINDING_PLAN.md)及[空模板](SOURCE_BINDING_TEMPLATE.yaml)列12組BIND、12案例與12命令slot。現行canonical候選已整合，但未Human採用；source/command/evidence仍UNBOUND。既有SQL smoke與純規則測試不升格Foundation產品結果；原13-operation文件檢查是歷史證據。
