# 網站執行主計畫：檔案、順序、產品與驗收

更新：2026-10-18。此檔是「先做哪張票、先讀什麼、做完會看到什麼、下一步是什麼」的唯一權威。功能規則只在 SPEC／詳規，驗收判準只在 VALIDATION_PLAN，當前 PASS／未執行只在實作狀態與具日期證據。

## 1. 現在從哪個檔案開始

**先讀本檔；Issue 06 已依本機 mock 範圍結案，Issue 08 本機展示範圍已完成，Issue 09–10 已完成，Issue 11 購物車／SKU／報價本機 mock 子集已完成，Issue 12 訪客結帳 UI 與專用公開 API／SQL 驗收已完成，Issue 13 付款結果本機 mock 驗收已完成，Issue 14 付款逾時／取消／晚到成功補償本機 mock 驗收已完成，Issue 15 人工對帳與付款異常處置、Issue 16 會員購物與受保護的訂單紀錄及 Issue 17 三語購物旅程與語系網址之本機核准 mock 範圍均已完成。Issue 18 出貨與顧客配送追蹤、Issue 19 退貨申請／客服／驗收、Issue 20 受控退款／財務核對、Issue 21 交易憑證與退款發票處理及 Issue 22 購物政策與客服入口的本機 mock 範圍均已 resolved。Issue 23 的 Analyst 最小聚合報表、報表存取稽核與現有安全／登入日誌清理已完成；暫定工程保存上限已採用，但帳號活動通知、配送／客服資料清理、其他診斷日誌與備份生命週期仍待資料來源或通知／備份機制成立，會計／稅務分類待正式確認，因此 Issue 23 維持 claimed，仍是下一個未完成項目。** 01–03 已 resolved 的政策工作保留作輸入；04／05 的 local mock 契約也已 resolved。必要 M0-AC01–11 的來源綁定狀態均為 PASS，M0-AC12 clean-checkout restore/build 與第二位操作者重跑列為選配，見[clean checkout／handoff 選配](../options/CLEAN_CHECKOUT_REBUILD_HANDOFF_OPTION.md)。登入方式改善、原生 OS trust/user-presence 改善、獨立資料庫 restore／cutoff 對帳及自動對帳排程亦屬選配；排程細節見[對帳排程選配](../options/RECONCILIATION_SCHEDULING_OPTION.md)。Issue 02 專屬完整售後驗收、正式 provider sandbox／外部部署仍未完成或不在目前核准範圍；M0 整體仍為 `PARTIAL_NOT_M0_COMPLETE`。

2026-10-17 Issue 15 已 resolved：人工唯讀 mock 報表、OP-45 處置／結案、個人負責人指派與重大 finding 站內通知皆完成 SQL/API/Storefront 驗收；瀏覽器已完成 UI 查核、待結案 finding 顯示及證據結案，SQL 覆蓋結案冪等、差異重現後重開與金融餘額不變。自動對帳排程已移出必要本機範圍並列為選配；未指派案件不會自動通知，外部通知渠道未啟用。詳見[Issue 15 結案驗證](../../../architecture/VERIFICATION.md#issue-15-storefront-手動處置與證據結案2026-10-17)及[對帳排程選配](../options/RECONCILIATION_SCHEDULING_OPTION.md)。

2026-10-17 Issue 16 已 resolved：會員訂單清單只查目前登入 UserId；會員讀取本人訂單，跨會員以 404 隱藏；訪客訂單只接受與原訂單、瀏覽器綁定的明確 claim capability，歸戶 Owner 更新、capability 消耗與 idempotency 同一交易。Dedicated Commerce SQL harness 含雙會員並行競爭、Storefront Playwright 4/4、Storefront typecheck 與 API／Commerce Release build 均 PASS。未擴大 core34 OpenAPI 執行契約；NuGet clean restore 與 live HTTP 瀏覽器旅程未驗。詳見[Issue 16 驗證紀錄](../../../architecture/VERIFICATION.md#issue-16-會員私人訂單與訪客歸戶2026-10-17)。Issue 17 的 10／14／16 依賴已解除，並已完成本機 mock 範圍。

2026-10-17 Issue 17 已 resolved（本機 mock 範圍）：Storefront 三語前綴 route、legacy query 轉址、語言切換保留搜尋／SKU／購物車／checkout 狀態，並有依 HTTPS 公開站點設定的 canonical／hreflang。會員訂單狀態與商品缺譯 fallback 均按語系顯示；私有交易頁維持 noindex。Nuxt typecheck/build 與專用 Playwright 9/9 PASS；測試使用 loopback synthetic API fixture，沒有真 SQL／正式網域／搜尋引擎部署。詳見[Issue 17 驗證紀錄](../../../architecture/VERIFICATION.md#issue-17-三語購物旅程與語系網址2026-10-17)。下一項 Issue 18 的依賴已解除。

2026-10-17 Issue 18 已 resolved（本機 mock 範圍）：dedicated SQL harness 與 live API／SQL／Storefront Playwright 1/1 通過；涵蓋已付款出貨、guest 本人追蹤、UTC 節點／耗時、重送／亂序／例外恢復，以及未付款與無履約權限拒絕。真實 carrier、外部通知、營業日與 SLA 不在本機範圍。結案證據見 [Issue 18 驗證紀錄](../../../architecture/VERIFICATION.md#issue-18-live-apisqlstorefront-結案驗收2026-10-17)。下一項 Issue 19（退貨申請、客服與驗收）依賴已解除。

2026-10-17 Issue 19 已 resolved（本機 mock 範圍）：dedicated SQL harness 驗證 Return 分類、首次回應／人工升級 UTC 秒數、拒絕理由、未授權客服拒絕、拒絕後新 capability 重申請、活動案件重複申請阻擋、檢查庫存 movement 與原付款快照退款單位；live API／SQL／Storefront Playwright 1/1 通過。法定退貨期限、日曆／營業日及 SLA 保持待決；真實 carrier、通知與退款執行不在本票。詳見[Issue 19 驗證紀錄](../../../architecture/VERIFICATION.md#issue-19-退貨申請客服與驗收2026-10-17)。下一項 Issue 20「受控退款與財務核對」依賴已解除，尚未開始。

2026-10-17 Issue 20 已 resolved（本機 mock 範圍）：顧客本人訂單呈現伺服器退款狀態摘要，完整 Commerce SQL harness 與 live API／worker／SQL／Storefront 售後旅程均通過；人工核對唯讀且不變更帳務資料。自動排程、銀行核對、真實資金及正式稅務均不在本範圍。詳見[Issue 20 驗證紀錄](../../../architecture/VERIFICATION.md#issue-20-受控退款與財務核對2026-10-17)。下一項 Issue 21 依賴已解除。

2026-10-17 Issue 21 已 resolved（本機 mock 範圍）：既有 Commerce SQL 證據涵蓋 DEMO 憑證／調整唯一性、Unknown 重試與退款分離；本次新增 Storefront Playwright 驗證顧客本人訂單頁顯示憑證、同額退款調整與無稅務效力聲明，typecheck PASS。未修改 runtime/API/schema/授權；正式稅務與開票未驗。詳見[Issue 21 驗證紀錄](../../../architecture/VERIFICATION.md#issue-21-交易憑證與退款發票處理2026-10-17)。下一項 Issue 22 依賴已解除。

2026-10-17 進度補充：Issue 13 成功售後、拒絕／Unknown 真 API／SQL／Storefront 結果、API 暫時不可用與恢復旅程均已驗證；fresh dedicated SQL fixture 的 Commerce harness、SQL snapshot 與 synthetic cleanup 通過。Issue 13 本機 mock Work status 已 resolved，Issue 14 依賴解除，正式 provider sandbox 仍不在目前範圍。Anonymous session runtime contract 修正後 Release solution build 0 warnings／errors、Core.Tests 11/11；Storefront typecheck PASS；生成器依批准清單投影 26 個 runtime operation。Issue 13 詳細證據與限制見票券及[驗證紀錄](../../../architecture/VERIFICATION.md)。分類篩選因目前 Catalog 無分類契約暫回 422，詳見 Issue 10 resolution。M1-AC07／08／10 尚有部分情境未覆蓋，詳見各票與驗證紀錄。

Issue 13 後續：拒絕／Unknown 的 API／SQL／worker／Storefront 結果、API 暫時不可用與恢復均在新建 dedicated fixture 驗證；詳見票券與驗證紀錄。依 [票券作業規則](../../../agents/issue-tracker.md)，Issue 13 已 resolved，Issue 14 不再受其阻擋。

2026-10-17 Issue 14 已 resolved：fresh dedicated SQL fixture 完整 Commerce SQL harness 通過；live API／worker／SQL 驗證晚到成功維持 expired、客服 read scope 一致，以及 mock 補償失敗後 pending 清零、reservation 釋放；Storefront live Playwright 1/1 通過。詳細證據與 fixture 限制見[Issue 14](../../../issues/kohami/14-payment-races-and-compensation.md)及[驗證紀錄](../../../architecture/VERIFICATION.md)。

依序閱讀（只需這四份起手檔）：

1. [Issue 22](../../../issues/kohami/22-policies-and-support-entry.md)：Issue 22 本機 mock 範圍已 resolved。Issue 23 的報表、現有日誌清理與逐類暫定工程期限已完成／採用；其餘類別須待來源或通知／備份機制成立。之後建議先補帳號活動時間來源與 24 個月未活動通知／30 日寬限流程，再接配送／客服結案來源與備份生命週期；此為後續建議，尚未執行。
2. [批准範圍](technical-baseline/APPROVED_SCOPE.yaml)：目前只批准本機 core34／native4／12AC；完整候選含核心97及Phase2，未全部批准。
3. [逐項實作狀態](technical-baseline/IMPLEMENTATION_STATUS.yaml)：找未通過案例；歷史 PASS 不直接套用目前 candidate。
4. [操作手冊](LOCAL_DEMO_RUNBOOK.md)：已存在的命令、前置條件與尚無可執行命令的缺口。

開始執行下一張票前，先核對其需求、批准範圍、現行架構與環境；驗收狀態只依各票與具日期的驗證證據更新，不從歷史 PASS 推定新候選通過。

## 2. 已確認的交付與適用範圍

2026-10-15 使用者已選：**核心商店本機版 → 會員實付儲值本機版 → 換裝另版本機版 → 正式營運 → 實際營運觀測**。本機外部商務服務維持 mock；贈送金暫不發放／扣抵，保留日後分帳啟用方向。核心商店不等待換裝；Issue 22 驗收展示政策與客服，正式品牌／商家／條款由 Issue 07 和 production-launch/01 把關。

此順序適合既有模組化單體及交易邊界：先補真實 SQL／授權證據，再逐一交付旅程。Commerce 裁決訂單、庫存、付款、退款及會員帳本，前端只顯示；新規則不塞 Runtime，不為拆模組破壞原子一致性。現行責任見[MODULES](../../../architecture/MODULES.md)。

本文件確認規劃順序與使用者已選方向，沒有把後續契約、真資金或部署一併批准；每階段開始時只確認該階段尚缺的實質決策，不重問已採框架、mock 或非 AI 方向。登入強化選配見[身份安全基線](../identity/AUTH_SECURITY_PROFILE.md)。

## 3. 唯一執行順序與各版產品

| 階段 | 明確執行順序 | 這時能看到／操作的產品 | 本階段結束點 |
| --- | --- | --- | --- |
| S0 基線可執行 | 06 先續做環境與共同證據 → 04 → 05 → 回到 06 完成基線結案 | 既有 HTTPS Nuxt＋API／SQL、受保護登入、合成購物與付款結果可查；不是完整商品管理站 | 04／05／06 各自符合本機範圍；必要的 M0-AC01–11 補齊相應證據，未跑不填 PASS；M0-AC12 clean checkout／獨立 handoff 為選配改善 |
| S1 商品與購物車 | 08 → 09 → 10 → 11 | 後台建商品／SKU、發布與調庫存；顧客從首頁搜尋／篩選、選顏色尺寸、加車、確認全部費用；仍不能宣稱完整訂單旅程 | 商品詳規 M1-AC01–10 的對應案例；草稿／越權／缺貨／價變正反例 |
| S2 訂單與付款 | 12 → 13 → 14 → 15 → 16 → 17 | 訪客／會員 mock 結帳、成功或待確認、自己的訂單；後台可查異常／對帳；三語完整串接 | M2-AC01–12，真 SQL 不超賣／不重單，Unknown 不重付，晚到成功不復活原單 |
| S3 配送與售後 | 18 → 19 → 20 → 21 → 22 → 23 | 營運模擬配送、受理退貨／收件／驗收，具有效 Owner 權限與案件 scope 後退款；顧客查退款／憑證；客服與最小報表可用 | M3-AC01–12，真 API／SQL browser；退款、入庫、憑證及對帳分開 |
| S4 核心商店本機版 | 24 → 25 → 26 | 一個完整可操作的本機商店：三語／手機、後台商品庫存、購買、訂單、履約、售後、退款／對帳 | VALIDATION_PLAN 的 FINAL-CORE；依操作手冊可啟停並完整演示。這是第一個完整本機交付 |
| S5 會員實付儲值版 | member-wallet/01 → 02 → 03 → 04 → 05 | 合成可信入金→本人實付餘額／帳本→餘額扣抵購物→人工履約→退款／對帳；無真收款，贈送金未啟用 | FINAL-WALLET；所有適用 WALLET／FUL 案例，雙花／超賣／超退 |
| S6 換裝另版 | 27 → 28 → 29 → 30 → 31 → 32 → 33 → 34 → 35 → 36 → 37 | 先一個角色尺寸×SKU 三視圖實證，再擴四組／公告尺寸與合格 SKU；顧客換裝、收藏原版、依授權匯出；失效可回商品購物 | FINAL-FITTING；合法非 AI 素材、人工品質、權利撤銷／cache、裝置與版本驗收 |
| S7 正式啟用準備 | 07 → production-launch/01 → 明確的正式部署／收款啟用決策 | 真實品牌／商品及核准資金、人工配送、通知／憑證、權限、監控與復原；未批准前保持關閉正式入口 | FINAL-LAUNCH；上線票 resolved 也不替代實際啟用授權 |
| S8 營運後觀測 | 正式啟用後 → 38 | 以真實資料觀測每日對帳、出貨／客服時效、可用性與復原成本 | FINAL-OPS；完整量測期，樣本不足仍未驗證，不能阻擋先完成 S4 |

這是推薦的單線執行順序；不要求平行開發。07 的只讀商業資料蒐集可提早進行，但正式採購／申請不由本計畫授權。若要改成平行或提前啟用功能，另核對成本、consumer 與驗收範圍。

06 出現兩次代表「先恢復基線，再完成缺口結案」，不是重做兩次；04／05／06 的現有票依賴無循環。22 已依使用者決定只依 19，正式 07 不再卡本機 24／26；38 必須等正式啟用，不在本機驗收內。其餘票原 Blocked by 仍有效，以下序列已遵守。

## 4. 每張票的完整檔案與驗收輸入

表列為完整執行順序；已完成票券的 Work status 以各票為準。執行下一列前先讀該票與所屬詳規，不需一次讀所有 OpenAPI。

| 順序／階段 | 檔案 | 實作輸入／驗收來源 |
| --- | --- | --- |
| 01 | [06：確認商店技術方案與品質基準](../../../issues/kohami/06-commerce-technology-quality-baseline.md) | [基線 READINESS](technical-baseline/READINESS.md)、[mock 政策](../../../proposals/kohami/04-demo-commerce-policy.md)；M0-AC／DEMO |
| 02 | [04：建立模擬金流契約與情境驗證](../../../issues/kohami/04-payment-provider-contract-validation.md) | [基線 READINESS](technical-baseline/READINESS.md)、[mock 政策](../../../proposals/kohami/04-demo-commerce-policy.md)；M0-AC／DEMO |
| 03 | [05：建立模擬物流與交易憑證契約](../../../issues/kohami/05-logistics-invoice-contract-validation.md) | [基線 READINESS](technical-baseline/READINESS.md)、[mock 政策](../../../proposals/kohami/04-demo-commerce-policy.md)；M0-AC／DEMO |
| 04 | [08：從後台上架到顧客查看商品](../../../issues/kohami/08-publish-product-to-storefront.md) | [CATALOG_PLAN](../commerce/CATALOG_PLAN.md)、[畫面與三語](../experience/EXPERIENCE_DESIGN.md)；M1-AC |
| 05 | [09：維護可銷售庫存與缺貨狀態](../../../issues/kohami/09-sellable-stock-and-availability.md) | [CATALOG_PLAN](../commerce/CATALOG_PLAN.md)、[畫面與三語](../experience/EXPERIENCE_DESIGN.md)；M1-AC |
| 06 | [10：從首頁搜尋與篩選商品](../../../issues/kohami/10-home-search-filter-journey.md) | [CATALOG_PLAN](../commerce/CATALOG_PLAN.md)、[畫面與三語](../experience/EXPERIENCE_DESIGN.md)；M1-AC |
| 07 | [11：確認 SKU、購物車與完整費用](../../../issues/kohami/11-sku-cart-price-review.md) | [CATALOG_PLAN](../commerce/CATALOG_PLAN.md)、[畫面與三語](../experience/EXPERIENCE_DESIGN.md)；M1-AC |
| 08 | [12：訪客結帳與可靠庫存承諾](../../../issues/kohami/12-guest-checkout-stock-reservation.md) | [CHECKOUT_ORDER_PLAN](../commerce/CHECKOUT_ORDER_PLAN.md)、[訪客存取](../identity/GUEST_ORDER_ACCESS.md)；M2-AC |
| 09 | [13：完成付款並顯示可信結果](../../../issues/kohami/13-trusted-payment-order-results.md) | [CHECKOUT_ORDER_PLAN](../commerce/CHECKOUT_ORDER_PLAN.md)、[訪客存取](../identity/GUEST_ORDER_ACCESS.md)；M2-AC |
| 10 | [14：處理付款逾時、取消與晚到成功](../../../issues/kohami/14-payment-races-and-compensation.md) | [CHECKOUT_ORDER_PLAN](../commerce/CHECKOUT_ORDER_PLAN.md)、[訪客存取](../identity/GUEST_ORDER_ACCESS.md)；M2-AC |
| 11 | [15：每日對帳與付款異常處理](../../../issues/kohami/15-daily-reconciliation-payment-exceptions.md) | [CHECKOUT_ORDER_PLAN](../commerce/CHECKOUT_ORDER_PLAN.md)、[訪客存取](../identity/GUEST_ORDER_ACCESS.md)；M2-AC |
| 12 | [16：會員購物與受保護的訂單紀錄](../../../issues/kohami/16-member-checkout-private-orders.md) | [CHECKOUT_ORDER_PLAN](../commerce/CHECKOUT_ORDER_PLAN.md)、[訪客存取](../identity/GUEST_ORDER_ACCESS.md)；M2-AC |
| 13 | [17：三語購物旅程與可索引網址](../../../issues/kohami/17-three-locale-shopping-journey.md) | [CHECKOUT_ORDER_PLAN](../commerce/CHECKOUT_ORDER_PLAN.md)、[訪客存取](../identity/GUEST_ORDER_ACCESS.md)；M2-AC |
| 14 | [18：出貨與顧客配送追蹤](../../../issues/kohami/18-shipping-and-tracking.md) | [AFTERCARE_REFUND_PLAN](../commerce/AFTERCARE_REFUND_PLAN.md)、[對帳資料](../commerce/RECONCILIATION_DATA_PLAN.md)；M3-AC |
| 15 | [19：退貨申請、客服與驗收](../../../issues/kohami/19-return-support-inspection.md) | [AFTERCARE_REFUND_PLAN](../commerce/AFTERCARE_REFUND_PLAN.md)、[對帳資料](../commerce/RECONCILIATION_DATA_PLAN.md)；M3-AC |
| 16 | [20：受控退款與財務核對](../../../issues/kohami/20-controlled-refunds-reconciliation.md) | [AFTERCARE_REFUND_PLAN](../commerce/AFTERCARE_REFUND_PLAN.md)、[對帳資料](../commerce/RECONCILIATION_DATA_PLAN.md)；M3-AC |
| 17 | [21：交易憑證與退款發票處理](../../../issues/kohami/21-invoice-and-refund-adjustments.md) | [AFTERCARE_REFUND_PLAN](../commerce/AFTERCARE_REFUND_PLAN.md)、[對帳資料](../commerce/RECONCILIATION_DATA_PLAN.md)；M3-AC |
| 18 | [22：購物政策與客服入口](../../../issues/kohami/22-policies-and-support-entry.md) | [AFTERCARE_REFUND_PLAN](../commerce/AFTERCARE_REFUND_PLAN.md)、[對帳資料](../commerce/RECONCILIATION_DATA_PLAN.md)；M3-AC |
| 19 | [23：最小化報表與資料生命週期](../../../issues/kohami/23-minimal-reports-data-lifecycle.md) | [AFTERCARE_REFUND_PLAN](../commerce/AFTERCARE_REFUND_PLAN.md)、[對帳資料](../commerce/RECONCILIATION_DATA_PLAN.md)；M3-AC |
| 20 | [24：核心購物的手機、可及性與效能驗收](../../../issues/kohami/24-core-mobile-accessibility-performance.md) | [唯一驗收定義](VALIDATION_PLAN.md)、[操作手冊](LOCAL_DEMO_RUNBOOK.md)、[品質 Profile](../../../quality/QUALITY_PROFILE.yaml)；M4-AC／CHK／Q |
| 21 | [25：商務監測、安全與災難復原](../../../issues/kohami/25-commerce-security-monitoring-recovery.md) | [唯一驗收定義](VALIDATION_PLAN.md)、[操作手冊](LOCAL_DEMO_RUNBOOK.md)、[品質 Profile](../../../quality/QUALITY_PROFILE.yaml)；M4-AC／CHK／Q |
| 22 | [26：核心商店獨立交付與啟用審查](../../../issues/kohami/26-core-store-delivery-readiness.md) | [唯一驗收定義](VALIDATION_PLAN.md)、[操作手冊](LOCAL_DEMO_RUNBOOK.md)、[品質 Profile](../../../quality/QUALITY_PROFILE.yaml)；M4-AC／CHK／Q |
| S5-1 | [member-wallet/01：確認實付儲值規則、可信來源與契約](../../../issues/member-wallet/01-policy-and-contract-decisions.md) | [會員儲值詳規](../commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)；WALLET-AC／FUL-AC |
| S5-2 | [member-wallet/02：完成實付帳本、合成入金與本人餘額](../../../issues/member-wallet/02-paid-ledger-and-topup.md) | [會員儲值詳規](../commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)；WALLET-AC／FUL-AC |
| S5-3 | [member-wallet/03：完成會員餘額原子結帳與訂單](../../../issues/member-wallet/03-atomic-balance-checkout.md) | [會員儲值詳規](../commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)；WALLET-AC／FUL-AC |
| S5-4 | [member-wallet/04：完成實付退款、人工履約與帳本對帳](../../../issues/member-wallet/04-wallet-refund-and-reconciliation.md) | [會員儲值詳規](../commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)；WALLET-AC／FUL-AC |
| S5-5 | [member-wallet/05：驗收會員實付餘額閉環與復原](../../../issues/member-wallet/05-wallet-recovery-and-delivery.md) | [會員儲值詳規](../commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)；WALLET-AC／FUL-AC |
| S6-1 | [27：合法素材的最小換裝實證](../../../issues/kohami/27-licensed-fitting-pilot.md) | [換裝詳規](../fitting/FITTING_ASSET_OUTFIT_PLAN.md)、[實證計畫](../fitting/FITTING_PILOT_PLAN.md)；FIT／FAPI／AAPI／PILOT／REG／PNG 案例 |
| S6-2 | [28：確認換裝、收藏與匯出契約](../../../issues/kohami/28-fitting-outfit-export-contracts.md) | [換裝詳規](../fitting/FITTING_ASSET_OUTFIT_PLAN.md)、[實證計畫](../fitting/FITTING_PILOT_PLAN.md)；FIT／FAPI／AAPI／PILOT／REG／PNG 案例 |
| S6-3 | [29：從素材審核到首次三視圖換裝](../../../issues/kohami/29-review-to-first-three-view-fitting.md) | [換裝詳規](../fitting/FITTING_ASSET_OUTFIT_PLAN.md)、[實證計畫](../fitting/FITTING_PILOT_PLAN.md)；FIT／FAPI／AAPI／PILOT／REG／PNG 案例 |
| S6-4 | [30：跨頁比較並回到真實商品購買](../../../issues/kohami/30-fitting-session-to-purchase-fallback.md) | [換裝詳規](../fitting/FITTING_ASSET_OUTFIT_PLAN.md)、[實證計畫](../fitting/FITTING_PILOT_PLAN.md)；FIT／FAPI／AAPI／PILOT／REG／PNG 案例 |
| S6-5 | [31：獨立停用、授權撤銷與快取撤除](../../../issues/kohami/31-fitting-suspension-rights-cache-revocation.md) | [換裝詳規](../fitting/FITTING_ASSET_OUTFIT_PLAN.md)、[實證計畫](../fitting/FITTING_PILOT_PLAN.md)；FIT／FAPI／AAPI／PILOT／REG／PNG 案例 |
| S6-6 | [32：會員明確收藏與版本失效提示](../../../issues/kohami/32-explicit-saved-outfits-version-lifecycle.md) | [換裝詳規](../fitting/FITTING_ASSET_OUTFIT_PLAN.md)、[實證計畫](../fitting/FITTING_PILOT_PLAN.md)；FIT／FAPI／AAPI／PILOT／REG／PNG 案例 |
| S6-7 | [33：訪客與會員下載授權示意截圖](../../../issues/kohami/33-licensed-outfit-reference-export.md) | [換裝詳規](../fitting/FITTING_ASSET_OUTFIT_PLAN.md)、[實證計畫](../fitting/FITTING_PILOT_PLAN.md)；FIT／FAPI／AAPI／PILOT／REG／PNG 案例 |
| S6-8 | [34：四組角色與公告尺寸完整驗收](../../../issues/kohami/34-four-avatar-families-supported-sizes.md) | [換裝詳規](../fitting/FITTING_ASSET_OUTFIT_PLAN.md)、[實證計畫](../fitting/FITTING_PILOT_PLAN.md)；FIT／FAPI／AAPI／PILOT／REG／PNG 案例 |
| S6-9 | [35：首批 SKU 相容性與換裝資格公告](../../../issues/kohami/35-initial-sku-compatibility-publication.md) | [換裝詳規](../fitting/FITTING_ASSET_OUTFIT_PLAN.md)、[實證計畫](../fitting/FITTING_PILOT_PLAN.md)；FIT／FAPI／AAPI／PILOT／REG／PNG 案例 |
| S6-10 | [36：換裝效能、備援與資料邊界驗收](../../../issues/kohami/36-fitting-performance-fallback-data-validation.md) | [換裝詳規](../fitting/FITTING_ASSET_OUTFIT_PLAN.md)、[實證計畫](../fitting/FITTING_PILOT_PLAN.md)；FIT／FAPI／AAPI／PILOT／REG／PNG 案例 |
| S6-11 | [37：換裝啟用範圍與交付審查](../../../issues/kohami/37-fitting-enablement-readiness.md) | [換裝詳規](../fitting/FITTING_ASSET_OUTFIT_PLAN.md)、[實證計畫](../fitting/FITTING_PILOT_PLAN.md)；FIT／FAPI／AAPI／PILOT／REG／PNG 案例 |
| S7-1 | [07：查核品牌、商家與商品來源](../../../issues/kohami/07-brand-merchant-product-verification.md) | 真實商家／品牌／商品、用途／權利及正式條款查核 |
| S7-2 | [production-launch/01：正式啟用驗收](../../../issues/production-launch/01-go-live-readiness.md) | [驗收定義](VALIDATION_PLAN.md)；FINAL-LAUNCH |
| S8 | [38：以實際營運紀錄驗證服務目標](../../../issues/kohami/38-operational-service-target-evidence.md) | [驗收定義](VALIDATION_PLAN.md)；FINAL-OPS，正式營運後完整期間 |

政策輸入（只讀，無政策改動不重開）：[01 結帳庫存](../../../issues/kohami/01-checkout-inventory-rules.md)、[02 配送售後](../../../issues/kohami/02-fulfillment-aftercare-invoice-rules.md)、[03 身份資料](../../../issues/kohami/03-identity-permissions-data-policies.md)。未啟用選配：[member-wallet/06 贈送購物金](../../../issues/member-wallet/06-promotional-credit-policy-and-enablement.md)，不列入上述必要完成順序。

契約輸入只取目前這張票需要的操作：已批准基線使用 [runtime core34](../../../contracts/runtime/Core.openapi.json)；新核心與換裝對照 [canonical 候選](../contracts/API_CANONICAL.openapi.yaml)及[操作 map](../contracts/CANONICAL_OPERATION_MAP.yaml)，逐項確認 authority、DTO／錯誤、scope、consumer、SQL／事件影響。沒有整體一次生成或實作 131 操作的要求。

## 5. 每一階段怎麼結束

每票以清單＋可觀察成果結案；每版以 [VALIDATION_PLAN](VALIDATION_PLAN.md) 的 FINAL 閘門驗收。不要把「UI 看得到」「測試共 N 項」「OpenAPI 可解析」當作 SQL 一致性或完整交付。每次回報要包含目前票／下一張票、現在可操作的產品、已跑命令／candidate／fixture、未跑或失敗項目、需要決策的影響。

核心最終演示腳本、真 API 與 SQL、權限正反例、交易競爭、Unknown、三語／手機／可及性／效能驗收統一維護於驗收文件；獨立 clean-checkout restore/build 與第二位操作者 handoff、資料庫 restore／cutoff 及原生 OS 實證列為選配改善。此處不再複製檢查數值，避免雙重權威。具體啟停及現存命令只維護於操作手冊。

## 6. 已決與待決事項：至少兩方案

已決（本次使用者明確選擇）：

| ID | 決定 | 未選方案及取捨 |
| --- | --- | --- |
| EXEC-01 | 核心商店先獨立交付，換裝另版 | 不採「等換裝全部完成才交首版」，避免素材阻擋核心購物 |
| EXEC-02 | 首輪儲值只驗實付，暫不發放贈送金 | 不採「首輪同做兩類額度」，贈送金順序／期限／退貨規則留後續 |
| EXEC-03 | 22 驗展示，07＋正式啟用票驗正式商業條件 | 不採「07 完成才讓本機整站驗收」，也不把展示條款當正式審查 |
| EXEC-04 | 核心本機→會員實付儲值本機→換裝另版本機→正式營運 | 不採「先換裝再儲值」；未更動非 AI／無本人照片方向 |

下表是**後續需使用者決策的方案**，均未視為已採用；到所列時點提供證據再問，不要求現在一次回答。受影響工作保持待決，其他獨立票可繼續。

| ID／決策時點 | 方案 A（建議方式） | 方案 B | 不決定會阻擋什麼 |
| --- | --- | --- | --- |
| WAL-D01：member-wallet/01，正式入金規劃前 | 先本機合成入金驗證；正式版另審「店家核對外部入金後入帳」的可信證據、覆核與對帳 | 直接規劃受核准服務商的自動入金；前期需資格、契約、費用與可信通知／查詢 | 真資金入帳；兩方案都不能讓會員自行標已付款 |
| WAL-D02：正式退款規則採用前 | 保留原付款／入金來源關聯，取得適用規則及專業審查後決定去向；本機只模擬採用流程 | 優先回會員實付餘額，但必須另確認使用者權利及可退原來源情況，未審不得設為唯一去向 | 正式退款，不阻擋既有 mock 退款；不由本文件給法律分類結論 |
| WAL-D03：正式資金後台啟用前 | 入金確認／額度更正採雙人覆核，普通配送分權；成本較高、降低單人錯誤 | 單人操作加不可改帳稽核、限額／警示與獨立事後對帳；須明確接受剩餘風險 | 正式資金操作權限，不更動已採單 Owner mock 退款 |
| FIT-D01：28／34 前 | 保留四組角色方向；先一組尺寸×SKU pilot，僅公布合格尺寸與組合，通過再擴 | 先製作四組及所有候選尺寸再整批驗收；成本／返工較高 | 素材採購、範圍擴充與換裝公告，不阻擋核心與實付版 |
| FIT-D02：28／32 前 | 採經裝置／成本實證的可設定收藏 quota，超限提供刪除／替代另存，確認後採用數值 | 不設產品 quota，需先證明儲存／濫用／成本與資源限制可控 | 收藏啟用；20 組仍不是已採上限 |
| OPS-D01：正式啟用票前 | 先受控測試／預備環境→核對模式隔離／回復與完整驗收→分階段公開 | 一次完整公開；需在同一候選先取得全部啟用與回復證據、風險較集中 | 對外部署／收款；此文件不選付費平台或建立正式環境 |

贈送金後續若重開，由 member-wallet/06 提供「維持停用」與「採明確使用／到期／退款規則後啟用」至少兩方案；目前維持停用是已決狀態，不重問。.NET／Nuxt／SQL 與全 mock 亦不重新選型。

## 7. 文件分工、棄用與尚缺命令

本次將重複交接合併至本檔，原 PLANNING_HANDOFF.md 移除；將本機驗收／restore 合併至 VALIDATION_PLAN.md，原 LOCAL_ACCEPTANCE_PLAN.md 移除。PRODUCT_PLAN 只留產品目標與商業假設；R01–R20 移入 SPEC，其他畫面／領域／品質內容回各自詳規。各票共用規則只維護於 docs/agents/issue-tracker.md。

舊版「無程式／無 Git／框架未選／canonical 未建立」、不同人批准的 mock 退款、20–80 人日舊估算、所有命令 UNBOUND 等現行說法不再適用。原決策歷史與有日期的驗證不改寫為新 PASS；機器快照只作來源追溯，當前產品狀態見實作狀態。

Issue 06 的 HTTP/API／SQL 售後、browser UI 售後與 mock worker crash/restart／failure replay matrix 均有可重跑證據；交接文件已整理，獨立 clean-checkout restore/build 與另一位操作者重跑不再是結案條件。詳細結果見逐項狀態與具日期驗證紀錄。Clean checkout／handoff、資料庫 restore／cutoff 對帳、登入方式改善與原生 OS trust/user-presence 改善分別列於[clean checkout 選配](../options/CLEAN_CHECKOUT_REBUILD_HANDOFF_OPTION.md)、[資料庫 restore 選配](../options/ISOLATED_RESTORE_CUTOFF_OPTION.md)及[選配清單](../options)。會員餘額、換裝及正式啟用的新增產品命令尚無實作來源，須在各自工作票建立並驗收，不會只靠擴寫文件變成可執行網站。

Issue 06 依本機展示版範圍更新 S0–S4 低信心估算為 60–120 人日，S0 目前為 5–10 人日（人工費率待定），詳見[Issue 06](../../../issues/kohami/06-commerce-technology-quality-baseline.md)。此為剩餘工作分解估算，並非可靠日曆交期、總價或核准預算；實際速度、負載／主機量測與人工驗收紀錄出現後再重估，不以帳號使用率或測試數量推算。

## 規劃快照的穩定追蹤 ID

以下保留 `PLANNING_READINESS.yaml` 與 checker 使用的來源定位，不將原決策或證據狀態抹除。

| ID | 主題 | 現況／邊界 |
| --- | --- | --- |
| DEC-01 | 核心 wire／版本／權限整合 | 核心修正方向已採用；產品實作批准僅 core34 子集 |
| DEC-02 | 帳號驗證與敏感操作重新驗證 | 登入強化建議僅列於選配計畫；敏感操作依獨立授權規則 |
| DEC-03 | 核心 UX／三語／品質門檻 | 核心已採用；採用目標不代表品質實測通過 |
| DEC-04 | 換裝權限、scope 與 purpose | 未採用，影響 Phase2 |
| DEC-05 | 收藏上限與替代另存計數 | 未採用，20 組仍暫定 |
| DEC-06 | 匯入匯出 TTL、容量／並行／加工數值 | 未採用，須裝置與 decoder 實證 |
| DEC-07 | 公告尺寸、相容 SKU 與允許用途 | 未採用，須合法素材／品質／成本證據 |



| ID | 來源／工作群組 | 後續處置 |
| --- | --- | --- |
| NEXT-01 | canonical 與操作 consumer | 候選已建立；增量採用與實際 consumer 按工作包綁定 |
| NEXT-02 | key/cert 與 SQL 權限來源 | 核對現有來源與逐項基線證據，不把空模板當 runtime 狀態 |
| NEXT-03 | Preset／BodyProfile 與私有權利證據 | 詳規與模板已建立，Phase2 啟用前綁真來源 |
| NEXT-04 | PNG／畫布／decoder／隔離來源 | 候選設計已建立，仍需真素材與裝置實證 |
| NEXT-05 | 產品檢查與交付命令 | 部分已有產品證據；依目前實作狀態補未完成案例 |
