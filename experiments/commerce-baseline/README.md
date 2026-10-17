# .NET 商務規則有界實驗

這是票 06 的純規則實驗，使用 .NET SDK `10.0.401`／`net10.0` 確認已採用政策可由公開接縫表達並測試。適配性：有條件適合；下列實測僅涵蓋合成規則，票 06 與產品驗收仍未完成。

## 執行方式

在本目錄執行，`global.json` 固定實測 SDK，不自動切換版本：

```sh
dotnet test Kohami.CommerceBaseline.slnx --nologo -m:1 -p:UseSharedCompilation=false -nodeReuse:false
dotnet build Kohami.CommerceBaseline.slnx --nologo -c Release -m:1 -p:UseSharedCompilation=false -nodeReuse:false
```

還原使用 SDK 內建 xUnit 範本所選的 Microsoft.NET.Test.Sdk、xUnit 與 runner 版本，沒有安裝全域工具。單 worker／關閉共用編譯用於受限本機環境；runner 仍需要本機 IPC，初次 NuGet 還原需要網路。驗證時 CLI home 與套件快取放 `/private/tmp`，bin／obj／測試報告不提交。

## 已實測證據（2026-10-13）

| 階段 | 公開行為／獨立例子 | 結果 |
| --- | --- | --- |
| 金額 RED → GREEN | 101×15%→實付86；103×50%→51；三件103×50%→總154，折扣總額只捨入一次；0%／100% | 5 failed／0 passed →5 passed |
| 保留 RED → GREEN | 同 key 同內容重送同快照、不重保留／不展延15分鐘；不同內容衝突；缺貨無部分保留；零應付拒絕 | 4 failed／5 passed →9 passed |
| 狀態 RED → GREEN | 取消／到期釋放一次；UNKNOWN 待確認；有效保留成功成立一次；重送成功／舊失敗不降級；晚成功需要補償 | 8 failed／9 passed →17 passed |
| REFACTOR | 共用保留釋放邏輯，public state traces 保持綠燈 | 17 passed／0 failed／0 skipped |
| Release build／Release tests | 相同 solution，warnings-as-errors | 成功，0 warnings／0 errors；17 passed |

RED 證據為實際 runner 的 NotImplementedException／斷言失敗。初次 sandbox MSBuild 命名管線、NuGet proxy 連線拒絕屬基礎設施失敗，不當成規則 RED。

## 接縫及範圍

`CheckoutPolicy.DiscountedSubtotal` 使用 decimal、於折扣總額一次 0.5 向上捨入。`SyntheticCheckoutStore` 是單一合成 SKU、單程序、依序呼叫、記憶體內的 `Start`／`Read`／`Cancel`／`Expire`／`RecordTrustedPayment` 實驗。Request／Price Snapshot 是不可變 record；相同 key 比對數量、價錢、折扣與運費。NT$232 範例為兩件101、15%折扣、運費60；晚收款保留原總額含運費供完整補償使用。0應付拒絕，不改收1元。

到期為 `now >= ExpiresAt`；成功結果到達會先檢查期限，即使排程尚未執行也不成立到期保留。時鐘由合成測試輸入，不能作正式系統的客戶端可信時間。成功先成立後的取消交由票02已付款取消／售後流程，本接縫維持成立。

`RecordTrustedPayment` 的 caller 必須已驗證供應商真偽、交易識別、金額與 TWD 幣別；此處只有預先可信的合成 outcome，未驗證 callback 或前台返回值。`CompensationNeeded` 僅表示需提出全額補償申請，不表示退款已核准／執行／成功；雙人提案核准仍依票03。

## 尚未驗證／不得推論

- SQL Server 最後一件並行、跨程序去重、唯一約束、鎖／隔離、rollback、整筆多 SKU 原子性、SQL 環境。
- 持久化、程序中斷／重新啟動、時鐘／排程、事件儲存與重播、對帳／備份還原、可靠投遞、補償案件唯一性。
- 付款 attempt 版本、供應商簽章／查詢、金額幣別不符、額外重複收款、退款、配送與發票；每個 operation 僅表達一筆可信結果，不能代替真實事件契約。
- 最大餘數商品分攤／退款快照、優惠資格與期限、價格重驗確認等完整結帳政策。
- ASP.NET Core API、身分授權、雙人退款、稽核／個資保存刪除、秘密管理。
- Vue.js SPA／SSR／SSG、三語可索引內容、可及性、瀏覽器旅程、效能負載、部署與成本。

無 SQL 連線、Migration、UI、HTTP endpoint、正式交易、顧客資料、付費服務或供應商呼叫。不能用17個記憶體內測試宣稱公開交易不超賣、復原或票06已完成。
