# 本機 mock 操作與展示手冊提案

2026-10-13｜READY_FOR_REVIEW｜操作規格，產品命令尚未綁定／執行

## 1. 操作前與啟停

確認位於kohami工作區、指定candidate／mock dataset／loopback目標，SQL專用資源與私有設定可用且無對外商務endpoint。核連接埠衝突、磁碟與記憶體餘裕，不擅自停用占用程序。秘密只交後端私有設定；Nuxt public config、HTML、網路或文件不得含SQL密碼與敏感token。

現有SQL環境真命令見[SQL README](../../../infra/sql-server/README.md)：`./infra/sql-server/manage.sh status`／`check`只驗該環境；`start`／`stop`會改執行狀態，`restart-probe`會停啟容器，只有實際操作範圍授權時執行。本輪不執行。stop保留volume，禁止全域prune。

產品建立後需填命令登錄：依鎖定SDK／package lock安裝→SQL健康→schema相容性檢查→明確migration／合成seed→API／worker→Nuxt SSR→健康與核心readiness→本機瀏覽器。migration／seed不因普通start自動對未知DB執行。API存活但DB／身份／mock outbox不可用時不顯示整站ready。

關閉先停止新寫入入口、停接worker新派送、記未決並有限等待正在執行工作、再停Nuxt/API/worker；SQL是否stop按操作需求且保留資料。未知結果持久化，不能把shutdown當Failed。新啟動核原expiry與未決，不能重設15分鐘或重發退款。

## 2. 合成展示腳本

每情境使用不同case/order，或明確preview重建合成dataset；不為下一幕任意改上一幕金融終態。mock結果選擇只在授權測試入口，不能由顧客request任意指定成功；展示用測試clock不可公開暴露。證據需求仍依[M4驗收](M4_LOCAL_ACCEPTANCE_PLAN.md)。

| 展示步驟 | 可見結果／觀察點 |
| --- | --- |
| 三語商品與cart | 3商品6SKU、缺貨／下架拒絕、相同SKU保留、費用版本與mock shipping |
| guest成功付款 | 原1590快照，Processing→Succeeded與DEMO憑證；完整訂單需單筆guest.read |
| 未知／到期 | Unknown只查原操作；15分鐘邊界由固定clock測，晚到成功不復活Order，建獨立補償 |
| 會員與權限 | A/B不能跨單；guest.read不能取消，guest.action另驗證；Support不能執行退款 |
| 配送與Return | 受控配送示意，Received／Inspection／可再售Movement分開且去重 |
| 正常部分退款 | 原1590、已退552、Unknown553、可用485，本案只選425；退款成功不再加庫存 |
| 補償／shipping-only | 獨立額外collection退回不占原商品slots；另合格原運費案件可只退剩60 |
| 憑證與對帳 | 退款成功／憑證未決各自顯示，來源去重與差異工作台不硬改成功 |
| 重啟／restore | 在隔離演練目標展示持久化未知、撤權與台帳一致，不影響原DB |

沒有選定MFA或受控mock情境入口尚未實作時，相關展示標BLOCKED，不能以靜態Owner標示或直接SQL改值代替。local通知收件匣也須persona/scope授權，token不貼入展示紀錄，驗證流程同產品規格。

## 3. 遇到問題的處置

連線／版本錯誤先停寫入、核目標與私有設定；不輸出connection string秘密。Unknown查原Operation、source ledger與差異，不另建付款／退款；Outbox backlog查租約與持久結果，不手改成功。撤權先於Started則拒派送，已Started由系統追結果。庫存／金額不平先保存最小狀態證據與停受影響command，不overwrite帳務。

reset必preview並核Owner／demo.reset／近期MFA、dataset版本／archive策略；只重建指定合成資料，留業務Audit及ResetAudit。restore細則見M4，第一個可行動作是確認隔離target及dispatch禁用，不覆蓋原DB。版本回復需Schema相容性與資料保存策略；沒有可驗證rollback就採forward-fix規劃，不drop重建掩蓋失敗。

## 4. 待綁定命令與交付清單

API/worker、Nuxt SSR、migration、seed、reset、backup/restore、完整contract/SQL/E2E/a11y/performance檢查命令目前均UNBOUND；由實作時真檔案與版本填入[M4證據模板](M4_EVIDENCE_TEMPLATE.yaml)。每條命令標目標、是否改資料、先決條件、預期輸出及安全停止方式；不在本規劃中虛構腳本。

交付者示範後，另一操作者照文件完成乾淨重建、成功／拒絕／未知各一旅程，記疑點與限制再判交付。建議角色為實作者、驗收覆核者、使用者；不填真人／備援姓名或要求退款雙人核准。正式運費、供應商、營運人員、法定分類、換裝尺寸／權利與對外主機持續延期。
