# 決策、假設與來源登錄

版本 v0.1｜2026-10-13

## 1. 來源與涵蓋範圍

對話：[設計樹訪談](chatgpt-conversation://6acc802e-b1b0-83e8-84f7-bacbe80d021a)。本次可讀到五組使用者／助理往返：使用者確認 Round 14–18，以及助理更新至 Round 18。工具未提供更早頁面；所取得的五份助理文字均未觸及本次每項 20,000 字元上限。

來源定位：S14 = Round 14 回覆；S15 = Round 15 回覆及下一輪 Q403–420；S16 = Round 16 回覆及下一輪 Q421–436；S17 = Round 17 回覆及下一輪 Q437–446；S18 = Round 18 最終總結。已確認是使用者在下一則回覆明確接受推薦內容，並以其補充要求優先。

D396–D446 可由當前來源直接定位，其中 D399 為休眠條件，D417 的收藏 20 組與 D419 的效能數值仍具暫定／待驗證性質。D001–D395 僅保留可見總結及轉向關係；不宣稱已重建全部原文或逐項有效狀態。

## 2. 狀態解讀

| 狀態 | 意義 |
| --- | --- |
| ACCEPTED | 使用者已確認且適用於現行產品基準，不等於允許實作。 |
| SUPERSEDED | 已由較新決策取代，歷史保留。 |
| DORMANT | 本版不啟用，未來需另案決策。 |
| PROVISIONAL | 訪談規劃基準，仍非最終商業承諾。 |
| VALIDATION_REQUIRED | 須有客觀證據確認，未執行驗證。 |
| ENGINEERING_DETAIL | 後續依產品規則做工程設計，未形成最終契約。 |

編號上限 446 是歷史紀錄，不是現行有效數量。下面列出的 51 個編號亦不能全部計為無條件 ACCEPTED。

## 3. 可直接追溯的決策登錄

| 編號 | 現行內容 | 狀態／補充 | 來源 |
| --- | --- | --- | --- |
| D396 | 全網站角色建立、服裝準備與換裝不使用 AI | ACCEPTED | S15、S18 |
| D397 | 固定 2D／2.5D 三視圖，不做完整 3D 旋轉 | ACCEPTED | S15 |
| D398 | 正面、右側面、背面 | ACCEPTED | S15 |
| D399 | 若未來採本人形象，保留真實外貌 | DORMANT；D403 使其不適用首版 | S15、S16 |
| D400 | 角色／服裝姿態、比例及對齊一致 | ACCEPTED | S15 |
| D401 | 男女各兩組基礎角色 | 方向經 D404、D418 細化確認 | S15、S16 |
| D402 | 基礎角色有尺寸變體並使用對應服裝素材 | 方向經 D406–412 細化確認 | S15、S16 |
| D403 | 首版不收本人照片，改用預設模組 | ACCEPTED；使用者補充優先 | S16 |
| D404 | 同分類兩組為不同身體比例的成年標準模特 | ACCEPTED | S15 Q404、S16 |
| D405 | 顧客不受帳號性別限制，自由選擇角色 | ACCEPTED | S15 Q405、S16 |
| D406 | 尺寸依首批 SKU，優先評估 S／M／L／XL | ACCEPTED；實際支援範圍待資產確認 | S15 Q406、S16 |
| D407 | 角色量測與衣服實際量測分開 | ACCEPTED | S15 Q407、S16 |
| D408 | 公告支援角色尺寸都要具完整三視圖 | ACCEPTED | S15 Q408、S16 |
| D409 | 服裝三視圖有真實／合法授權／人工核對來源 | ACCEPTED | S15 Q409、S16 |
| D410 | SKU 顏色尺寸視角正確，證明安全才共用素材 | ACCEPTED | S15 Q410、S16 |
| D411 | 審核對齊與遮擋，不合格組合禁止提供 | ACCEPTED | S15 Q411、S16 |
| D412 | 跨男女身形依相容性開放，不預設皆可套用 | ACCEPTED | S15 Q412、S16 |
| D413 | 商店預先製作去背、圖層及對齊素材 | ACCEPTED | S15 Q413、S16 |
| D414 | 訪客可直接換裝，不強制註冊 | ACCEPTED | S16 |
| D415 | 首版一次換一件目標服飾 | ACCEPTED | S16 |
| D416 | 換裝選擇對應真實 SKU、價格、庫存及尺寸 | ACCEPTED | S16 |
| D417 | 保存設定優先；暫定最多 20 組會員收藏 | 保存原則 ACCEPTED；數量 PROVISIONAL | S15 Q417、S16、S18 |
| D418 | 四組角色及公告尺寸／視角組合通過驗收才上線 | ACCEPTED | S16 |
| D419 | 素材已載入後切換 P75 ≤ 1 秒 | 規劃目標；VALIDATION_REQUIRED | S16、S18 |
| D420 | AI 推論用途停用，另估素材製作與傳輸成本 | ACCEPTED；不授權新增支出或轉撥 | S15 Q420、S16 |
| D421 | 半寫實日系型錄插畫 | ACCEPTED | S17 |
| D422 | 同角色不同尺寸保留視覺識別 | ACCEPTED | S17 |
| D423 | 適當中性基礎服裝 | ACCEPTED | S17 |
| D424 | 公開角色身高與必要圍度 | ACCEPTED | S17 |
| D425 | 首批換裝優先上衣、針織衫 | ACCEPTED | S17 |
| D426 | 角色身形與衣服尺碼分別選擇 | ACCEPTED | S17 |
| D427 | 不相容組合不強行換裝 | ACCEPTED | S17 |
| D428 | 外觀參考，不保證合身；提供實際量測入口 | ACCEPTED | S17 |
| D429 | 女性 A 正面為初始展示，不預選身形尺寸 | ACCEPTED；D437 補明介紹模式 | S17、S18 |
| D430 | 明確三視角控制，不暗示 360° | ACCEPTED | S17 |
| D431 | 缺貨 SKU 可保留合格展示，不能購買 | ACCEPTED；取代舊 D286 的 AI 成本前提 | S16、S17 |
| D432 | 正式名稱「模特換裝參考」 | ACCEPTED | S17 |
| D433 | 免費、不限正常使用次數 | ACCEPTED | S17 |
| D434 | 訪客暫存，會員可保存至帳號 | ACCEPTED；D443 補明主動保存 | S17、S18 |
| D435 | 角色、服裝及輔助素材具合適商業權利 | ACCEPTED | S17 |
| D436 | 收藏關聯版本，失效時揭露或提供替代 | ACCEPTED | S17 |
| D437 | 初次介紹圖，明確選定尺寸後才真正換裝 | ACCEPTED | S18 |
| D438 | 當前瀏覽保留角色、身形、視角，不當本人資料 | ACCEPTED | S18 |
| D439 | 約 20 個通過三視圖及相容性驗收的 SKU | ACCEPTED 為規劃規模；實際數量依合格素材 | S18 |
| D440 | 商品列表與頁面明示換裝資格 | ACCEPTED | S18 |
| D441 | 後台獨立停用問題換裝，不必下架商品 | ACCEPTED | S18 |
| D442 | 載入失敗退回照片／尺寸表，購物不中斷 | ACCEPTED | S18 |
| D443 | 訪客登入後主動選擇才保存穿搭 | ACCEPTED | S18 |
| D444 | 訪客與會員可下載授權且有標示的截圖 | ACCEPTED | S18 |
| D445 | 首版無永久公開穿搭分享網址 | ACCEPTED | S18 |
| D446 | 不推斷真實性別、體重、體型或健康資料 | ACCEPTED | S18 |

## 4. 歷史方向的取代關係

| 歷史群組／已知編號 | 現行處理 | 新依據 |
| --- | --- | --- |
| 個人照片／本人形象；D050、D255 等 | SUPERSEDED；首版無該資料流程 | D403 |
| 逐次 AI 試衣；D005、D051 等 | SUPERSEDED；改用固定資產 | D396–398、D413 |
| AI 工作佇列、生成重試及等待 | SUPERSEDED；不能列為現行必要依賴 | D396、D397 |
| 每週生成配額；D054、D367、D386–388 等 | SUPERSEDED；正常換裝不限次數 | D433 |
| P90 90 秒；D258 | SUPERSEDED；互動目標改為 D419 | D419 |
| 20 張 AI 圖片／D341 等 | SUPERSEDED；保存設定優先 | D417、D436 |
| AI 供應商、GPU、D371 NT$12,000／月 | DORMANT；不採購、不轉撥 | D420 |
| D393–395 首次建三視圖 | 重新解釋為團隊事前準備，非會員初次上傳 | D403、D413 |
| D224 訪客僅看示範 | 由訪客可操作固定換裝的新範圍取代 | D414 |
| D286 缺貨不優先生成 | AI 成本前提消失；缺貨可展示有效模組 | D431 |
| D337 品牌 AI 模特 | 首版範圍不納入，採非 AI 授權素材 | D396、S17、S18 |
| D314 SKU 換裝資格 | 原則保留，依相容性另行審核 | D416、D440 |
| D370 核心技術 NT$8,000／月 | PROVISIONAL；未證明可達成可靠性 | S16、S18 |
| D322 商店先上線 | 原則保留；換裝另行通過驗收啟用 | S17、S18 |

此表是可見來源明示的群組與已知編號，不是完整 D001–D395 的逐筆判定。

## 5. 暫定基準、假設與未決項

品牌 KOHAMI、參考售價、免運門檻及預算屬 PROVISIONAL。原始免運門檻數值未出現在目前可讀來源，不填造數字。平台、供應商、正式成本、素材與效能皆維持 VALIDATION_REQUIRED。

本次文件編排假設：Markdown 為維護來源，Word 為審閱版本；頁面清單及狀態責任是需求整理草案，尚非正式 API／狀態機；現有空目錄存放 KOHAMI 規劃文件，不代表 berry-match 已被重新命名。

尚須補齊早期詳細規則，至少包括付款方式、保留期限、取消條件、優惠疊加、退款與發票處理、運費／免運、登入方式、通知及資料保存期限。這是來源完整性與詳規工作，不任意重開全部訪談。

## 6. 決策更新原則

新決策須記錄來源、取代關係及影響需求；對已確認的產品行為有變更，先提出範圍／成本／風險供審查。工程細節依已確認規則處理，不能以實作方便改寫使用者決定。任何規劃狀態都不構成外部執行或產品上線批准。

## 7. 2026-10-13 工程政策採用

來源：使用者明確回覆「01／02／03 採用」；採用[政策審閱包 v0.1](../../../.scratch/kohami/proposals/README.md)。此更新為下表範圍的後續政策來源，不改寫 D396–D446 的產品方向。

| 新政策 ID | 已採用內容 | 狀態／限制 |
| --- | --- | --- |
| P-ACCEPT-01 | 票 01 P01–P08、TWD 整元／單優惠碼與快照分攤、15 分鐘保留、重送不展延、零元訂單暫不開放、晚到付款全額退款補償申請 | ACCEPTED；供應商能力、沙盒、並行／補償測試未執行；不免除雙人退款核准 |
| P-ACCEPT-02 | 契約驗證本島配送、固定費用表、交承運前免費取消、部分退貨不追補免運、門檻免運先不啟用、週一至週五服務日曆方向 | ACCEPTED；正式運費、營業工時及服務商仍待證據，出貨／客服數值仍為規劃目標 |
| P-ACCEPT-03 | Email＋密碼會員、受限 Email 單筆訪客查詢、後台 MFA、不同人退款提案／Owner 核准、逐類保存上限、不推測式追蹤 | ACCEPTED；具名人員／代理未指定，法定資料分類與處理者契約待正式審查 |

早期來源未定的上述方向已由本輪明確採用補齊；UNKNOWN 契約／費率與正式營運限制仍維持 UNKNOWN。採用政策不等於供應商或正式上線批准。

## 8. 2026-10-13 技術家族採用

P-TECH-01：使用者指定「ASP.NET Core, .NET Core, Vue.js, Microsoft SQL Server」，採用後端／前台／資料庫家族，取代未採用候選。正式來源為 [ADR-0002](../../adr/0002-dotnet-vue-sqlserver.md)，.NET 10 LTS／Vue 3 為受支援版本基準；SQL Server 版本與 edition、Vue HTML 方式、實證環境與部署仍待決。純 C# 規則實驗不替代真 SQL／付款／身份／復原證據，也不批准付費或正式營運。

## 9. 2026-10-13 Nuxt SSR 與本地 SQL 開發建置

P-TECH-02：使用者明確選擇「Vue 採 Nuxt SSR」，採 Vue 3＋Nuxt SSR，解除前台 HTML 方式待決。P-ENV-01：使用者尚無 SQL 測試環境並請求協助本地 Docker，授權隔離、loopback-only SQL Server Developer 開發建置與必要 smoke；不批准付費主機或正式資料。詳細來源見 [ADR-0002](../../adr/0002-dotnet-vue-sqlserver.md)與[環境文件](../../../infra/sql-server/README.md)。ARM 模擬的開發結果與官方支援 x64 平台驗證分開，產品交易／權限／復原仍須實證。

## 10. 2026-10-13 全流程 mock 展示政策

現行詳規：[mock 政策 v0.2](../../../.scratch/kohami/proposals/04-demo-commerce-policy.md)。方向來源為本輪使用者要求及追問回覆；詳規中的工程整理與可選建議分別標示，不推定全部新提案已批准。

| ID | 決策／狀態 | 取代範圍與限制 |
| --- | --- | --- |
| P-DEMO-01 | ACCEPTED：金流、退款、物流、發票全部先 mock | 正式服務商串接延期；模擬不能轉為真實交易，不開票／寄送／退款資金 |
| P-DEMO-02 | ACCEPTED：退款依管理者權限單人操作與顯示狀態 | 現階段取代 P-ACCEPT-01/03 的不同人退款要求；預設映射具退款權限 Owner。正式退款授權制度另決，未指定真人／代理不阻擋 mock |
| P-DEMO-03 | ACCEPTED：運費與人員先規劃機制 | 固定版本示意費用表、具名帳號／權限及代理配置能力；不指定實際運費、真人、品牌物流或服務承諾 |
| P-DEMO-04 | ENGINEERING_DETAIL：狀態、操作識別、額度保留、受控情境與 DEMO-01–12 | 可審閱且待實作；相容於已採用不變條件，不聲稱新測試通過 |
| P-DEMO-05 | PROPOSAL：收藏滿額由本人刪除／覆寫，建議20組 | 未採用；仍待素材實證與票28確認，不寫死上限或自動刪除 |

P-ACCEPT-01/02/03 的其他基準保留；之前表內雙人及供應商限制是歷史範圍，不作本階段前置條件。正式法定資料分類、售後條款／能力及權利審查維持待辦。本輪未新增法律結論或對外承諾。

## 11. 本機完整展示與工程提案

P-LOCAL-01 ACCEPTED：使用者在本輪詢問明確選擇「先本機完整展示」，不部署可分享外網站台。P-PLAN-01 PROPOSAL：畫面／API／領域／架構／品質／交付詳規見[工程審閱包](PLANNING_INDEX.md)；新token、身份/MFA詳規、量測條件與工作量估算不是Human已採用事實。API Authority=PROPOSED，無原產品canonical API比較基準。M0後依SQL/MFA支援實證修訂，框架不再重選。

P-FIT-PLAN-01：收藏20組、實際支援素材／尺寸及下載權利維持未採用／待證據；本輪核心展示規劃不解除票27／28的依賴。正式政策、服務商與法定分類仍延期。Gate1／Gate2不填造人類批准。

## 12. M0契約與身份查核

P-M0-01 PROPOSAL：SQL表／型別／原子額度、13operation/20schema M0 OpenAPI及12個M0-AC情境，來源[M0_READINESS](M0_READINESS.md)。API仍PROPOSED，沒有DDL／產品code／新的實測。P-M0-ID-01 NEEDS_DECISION：本輪後台MFA問句尚待人類選擇；維持Passkey優先需應用整合第二段閘門，或先Password+TOTP。官方指出native Passkey不是內建2FA；不將技術support自動推為已驗證MFA，不默認變更原政策。

P-M0-TYPE-01 ENGINEERING_PROPOSAL：整元TWD的API數值不超JavaScript安全整數，SQL bigint；rowversion不是時間，聚合ETag需覆蓋representation。退款按固定未占用原商品單位ordinal選取並鎖定，保留Unknown與重送原單位；這些細節仍待採用及M0實證。

## 13. M1商品、內容與線框提案

P-M1-01 PROPOSAL：商品發布、同SKU篩選、cart明細／價變、三語fallback與Quote5分鐘，來源[M1_CATALOG_PLAN](M1_CATALOG_PLAN.md)／[內容契約](CONTENT_LOCALE_CONTRACT.md)。Quote不是Reservation，15分鐘庫存預留政策不變；5分鐘是本輪未採用工程建議。P-M1-02 SYNTHETIC_FIXTURE：三商品六SKU與1590算例／60示意運費不是正式商品、售價或費率推薦。P-M1-03 DESIGN_PROPOSAL：兩張靜態線框依VISUAL_SYSTEM提案，未聲稱品牌已採用／產品已完成。後台MFA人類選擇仍待回覆，本輪不替使用者選。

## 14. M2結帳／訂單與訪客存取提案

P-M2-01 PROPOSAL：[M2詳規](M2_CHECKOUT_ORDER_PLAN.md)新增登入不自動合併cart、權威deadline等號、3秒查核／60秒轉稍後查閱UX等工程細節，沒有代用戶採用或當正式時效承諾；既有15分鐘Reservation與Unknown不重付不變。P-M2-02 ENGINEERING_PROPOSAL：[Guest read/action/claim](GUEST_ORDER_ACCESS.md)沿既有原Email單筆驗證，read不擴為寫入，完整歸戶不因相同Email自動發生。Email/reset/guest15分鐘建議與MFA5分鐘各別配置。

P-M2-03 SYNTHETIC_CASES：14個狀態／5個存取與手機訂單線框均是合成示例，原快照1590來自M1fixture；沒有實收／入帳／寄送／開票，沒有新產品測試結果。MFA方案仍待人類選擇。

## 2026-10-13 M3詳規提案登錄

延續已採用全mock與單Owner退款，新增[M3售後退款](M3_AFTERCARE_REFUND_PLAN.md)與[對帳資料](RECONCILIATION_DATA_PLAN.md)可審閱提案：金融來源去重獨立於通知、正常商品／shipping-only與晚到額外款補償分開、Unknown保留、憑證結果獨立、報表最小聚合與reset留稽核。每日Asia/Taipei 00:10為候選工程排程，不是新營運承諾；來源額度規則深化既定不超退政策。MFA選擇未採用、真運費／人員／供應商／法定分類仍延期。文件一致性檢查不代表產品或正式營運通過。

## 2026-10-13 M4本機交付提案

新增[M4驗收](M4_LOCAL_ACCEPTANCE_PLAN.md)、[操作手冊](LOCAL_DEMO_RUNBOOK.md)及候選證據模板：不自動migration未知DB、stop保留資料、restore使用隔離target並先禁dispatch／重套cutoff後撤權刪除hold、未知不重演交易；既定本機合成範圍下的工程提案，未執行。MFA仍待決，完整M1–M3機器契約與產品命令待綁定。未新增正式運費／供應商／人員／對外部署或營運SLA承諾。

## 2026-10-13 M1–M3整合API深化

新增[整合OpenAPI](API_M1_M3.openapi.yaml)及[跨階段一致性](API_CROSS_STAGE_CONSISTENCY.md)。M0byte不變；重疊DTO擴充另記，不宣稱backward compatible或canonical採用。新OrderLine/unit實體ID於checkout產生，Quote只凍結原金額內容／ordinal；shipping-only提案與可信來源補償分開，AdminReturn收件例外可查。文件validator PASS不能代替產品。MFA仍待決，沒有選TOTP／Passkey、生成client或產品實作。


## 2026-10-13 帳號與授權生命週期深化

P-IDAC-01 PROPOSAL：[帳號與授權維護](ACCOUNT_ACCESS_LIFECYCLE_PLAN.md)定義狀態、改Email／reset邊界、限時代理grant、最後Owner防鎖死、撤權／Started與restore裁決及16項驗收；新細節尚未採用，ACCT路徑未納OpenAPI。沿既有全mock／四角色／單Owner退款，未選MFA、未啟動產品或自動給代理人永久權限。
