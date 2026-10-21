# 11: 確認 SKU、購物車與完整費用

**What to build:** 顧客明確選擇顏色／服裝尺寸，管理購物車，查看精確 TWD 金額、配送與優惠條件。

**Blocked by:** 09：維護可銷售庫存與缺貨狀態

**Status:** ready-for-agent
**Work status:** resolved

- [x] 顧客明選合成展示 SKU 後加入購物車，可修改數量、移除商品並核對顏色、服裝尺寸與商品資訊。
- [x] 以本機 mock 合同的整數 TWD、DEMO15 與服務端費用規則呈現商品小計、折扣、配送及最終金額，並驗算組成一致。
- [ ] 不存在、停用、缺貨或過期的 SKU／價格資料有清楚處理，不由展示狀態決定可購買性。
- [ ] 驗證新增、變更、拒絕與載入失敗旅程；購物車不宣稱已保留庫存或已完成訂單。

## 本機mock階段規劃補充（2026-10-13）

現行依 [M1詳規](../../planning/kohami/commerce/CATALOG_PLAN.md) 與 [三語內容契約](../../planning/kohami/experience/CONTENT_LOCALE_CONTRACT.md)規劃：商品／價格／量測／圖像可使用明示的合成資料與自有占位，不能宣稱真實商品來源／素材資格已查核。原完整產品／實物驗收保留為正式資料階段要求。商品可售、缺貨、下架與換裝資格分開；cart不保留庫存，Quote與15分鐘Reservation分開。

2026-10-17 起依本票執行本機 mock 範圍；完整商品／正式費用資料與正式付款仍依原階段界線處理。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：R01–R03／AC01–AC03；User Stories 5、13–16、19。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。

## Resolution

**Work status:** resolved（2026-10-17，本機 mock 交付範圍）。本機 mock 交付包含商品頁明選合成 SKU、購物車新增／修改／移除、DEMO15 優惠、配送選項及 server quote 的 TWD 明細。改價／庫存變動會保留購物車並要求重新檢視；購物車不保留庫存、不代表已成立訂單。

- `python3 scripts/foundation/run-issue06-aftercare-acceptance.py --cart-only`：dedicated synthetic SQL fixture 上 Playwright 4/4 PASS，覆蓋 SKU 選取、購物車操作、DEMO15 報價、無庫存保留、ETag／冪等、改價 `QUOTE_CHANGED` 及缺貨 `STOCK_CHANGED`。僅清除 runner 記錄的測試 cart／quote／idempotency rows，還原暫時庫存與 maintenance，保留 fixture。
- `npm --prefix apps/storefront run typecheck`、Storefront build、Commerce/API Release build 與驗收 runner／architecture tests 的最終結果記於[驗證紀錄](../../architecture/VERIFICATION.md)。
- 完成範圍是明確的本機 mock 子集。M1-AC06 在本次測試到的操作／版本／重送範圍通過；M1-AC07、AC08、AC10 僅部分覆蓋。未實際驗收不存在／停用 SKU、fee 變動、購物車載入失敗／恢復，以及完整 1590 固定分攤與 5 分鐘 Quote 邊界、三語、鍵盤／焦點可及性與效能；不代表正式商品、售價、運費、付款或資金處理。其餘情境依詳規繼續追蹤，不得從 Work status 推定 M1 全部通過。
- 下一張無阻擋工作為 Issue 12「訪客結帳與可靠庫存承諾」。

