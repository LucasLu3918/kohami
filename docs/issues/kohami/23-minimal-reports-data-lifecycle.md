# 23: 最小化報表與資料生命週期

**What to build:** Analyst 只取得授權且最小化的營運資訊；保存、刪除及敏感存取依政策執行並可稽核。

**Blocked by:** 20：受控退款與財務核對

**Status:** ready-for-agent

**Work status:** claimed

- [x] Analyst 從後台取得已授權的最小營運報表，不能藉查詢或匯出取得超出必要用途的個人／交易資料。
- [ ] 依確認政策執行帳號、訂單、客服與日誌保存／刪除；依法應保留的交易證據及對帳一致性不被破壞。
- [ ] 敏感操作與授權存取可稽核，日誌／報表不暴露秘密，資料不用於本人身體或健康推斷。
- [ ] 驗證授權成功、越權拒絕、保存期限邊界及處理失敗；不擴充 Phase 3 的完整銷售轉換分析。

## M3詳規追蹤（2026-10-13）

參照[M3售後退款流程與驗收](../../planning/kohami/commerce/AFTERCARE_REFUND_PLAN.md)及[對帳／報表／資料生命週期](../../planning/kohami/commerce/RECONCILIATION_DATA_PLAN.md)。本次只補齊規劃、來源額度與靜態示意；本票產品驗收NOT_EXECUTED，原依賴與工作完成狀態維持。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：R07、R18／AC07、AC18；User Stories 50、78–82。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。



## Resolution（2026-10-18；最小報表與現有日誌生命週期子範圍）

已完成並驗收 Analyst 最小唯讀聚合報表子範圍；Issue 23 整體維持 `claimed`，不視為結案。後台 GET `/api/v1/admin/reports` 僅允許 `reports.read` 並固定檢查 `Report:operations`；API 只回傳合成資料按 UTC 訂單建立日及收款類型彙總的訂單數、已收、成功退款、待處理、可用、淨收及待補償訂單數。日期範圍必填、順序正確且最多 31 日。回應不包含個人資料、訂單／collection ID、SKU 或列級明細；Storefront 不提供匯出。畫面與 API 均標明資料是本機合成 mock、非正式財務報表，收退款依訂單日期分桶而非入帳日期。

- SQL acceptance 使用 dedicated `KohamiM0` 的合成訂單與 collections，驗證正確權限 scope、未知 Report scope 拒絕、聚合欄位、非法／超長日期拒絕、無列級識別資料，以及請求後清理合成資料。
- Playwright 驗證摘要畫面、UTC／mock 限制說明與 403 安全錯誤；Storefront typecheck、API／Commerce build 與專用 SQL fixture 證據記錄於[架構驗證紀錄](../../architecture/VERIFICATION.md)。
- 本次依使用者核准的暫定工程期限實作報表讀取持久稽核及現有 `m0.SecurityAudit`／`m0.AuthFailures` 清理：報表與安全稽核保存一年，登入失敗紀錄保存 90 日。報表稽核只含固定 `demo_m0`、可解析 actor、UTC 時間、授權結果與有效日期範圍；不記報表內容、IP、session、Email 或交易識別。API 只有 INSERT 權限；worker 透過固定 SQL 程序按 UTC 日執行冪等清理，失敗可重試。
- 帳號最近活動時間來源已從歷史 session 最新 `AuthenticatedAt` 回填，之後由成功登入及有效 session 請求更新 UTC `LastActivityAt`，最多每 5 分鐘寫入一次；不保存路徑、請求內容或逐次活動。沒有 session 歷史的舊帳號維持 `NULL`／未知。
- 2026-10-18 使用者採用帳號連續 24 個月未活動後寄送已驗證 Email 通知，通知後 30 日仍未活動即停用登入並保留資料。新增 worker 僅處理 `EmailConfirmed=1` 且活動時間已知的帳號；登入時效立即由 `Status='Suspended'` 與遞增 `SecurityVersion` 生效，撤銷既有 sessions／proofs，保護最後一位有效 Owner。狀態停用不刪除帳號、訂單、collection、付款／退款、憑證、地址、客服或備份資料。
- 本機沒有外部郵件供應商；此 mock 把信件寫入權限為使用者專屬的私有 inbox 證據檔。30 日計時從私有證據檔成功建立時開始，不能視為真實外寄郵件或正式營運郵件送達證明；正式郵件供應商仍待接入。郵件證據與 lifecycle audit 一年後清理。可由現有事件證明結案的配送與拒絕退貨資料已依期限去識別並保留最小摘要；其他客服來源、一般診斷／備份類別仍待來源或機制成立，會計／稅務分類待正式確認。其餘驗收條件未完成，Issue 23 維持 `claimed`，不視為結案。
- 新增版本化 SQL migration `0009_issue23_retention_audit`；既有 0001–0008 來源未改。未變更 API／OpenAPI 合約或事件。
- Impact Graph 全域 API／data／events 覆蓋仍為 partial／unknown；已人工檢視現有 Storefront proxy、Commerce 路由、Report scope resolver、角色權限規則與 SQL schema，未宣稱全域圖完整。

## Resolution（2026-10-18；配送與客服最小摘要保留子範圍）

依使用者採用的「去識別並保留最小摘要」決定，worker 以 UTC 結案事件作起算點：已配送／退回的 Shipment 結案 180 日後移除操作者、原因及顯示通知細節，只保留訂單關聯、狀態、時間線與期限處理標記；明確拒絕且滿 1 年的 Return 移除負責人、自由文字、SKU、單位識別碼與檢查細節，保留訂單／品項關聯、數量、狀態及時間線摘要。恰在期限邊界的紀錄保留，重跑冪等。

- 起算時間只接受最後一筆與目前終態相符且可解析的 UTC 歷史事件。狀態未終結、歷史缺少可信結案節點、已檢查但未有明確結案、或有退款／庫存移動證據關聯的退貨均保留原資料；不推定售後案件已結束。
- 不改寫 Order、Collection、退款／付款／發票、RefundCase、RefundOperation、InventoryMovement 或其他稽核證據；未改 API／OpenAPI 契約與事件。期限標記與 `retention_redacted` 擁有者保留最小化狀態，避免清理後重複處理。
- 專用 SQL Server 2025 loopback fixture 套用 migrations 0001–0011 並執行完整 Commerce SQL harness；新增 13 項生命周期 assertions 通過，細節見[架構驗證紀錄](../../architecture/VERIFICATION.md#issue-23-配送與客服資料去識別2026-10-18)。專用 fixture、合成資料及暫存 checkout 均已清除，既有本機展示資料庫未重設。
- 此項只覆蓋目前能由資料明確證明結案的 Shipment 與被拒 Return。客服其他資料來源、已檢查退貨之結案流程、診斷日誌、備份生命週期及會計／稅務分類仍未完成，因此 Issue 23 維持 `claimed`。
