# 訪客訂單、單次動作與會員歸戶存取提案

日期：2026-10-13｜READY_FOR_REVIEW｜不是已通過安全驗收

沿用[身份政策](../../../.scratch/kohami/proposals/03-identity-permissions-data.md)與[AUTH_SECURITY_PROFILE](AUTH_SECURITY_PROFILE.md)。mock只替代投遞，不把Email文字或本機地址當授權證明；後台MFA選擇與本流程無關，不能用guest proof取得Owner權限。

## 1. 能力範圍

| 類型 | 可做 | 明確不可做 |
| --- | --- | --- |
| checkout.read | 本筆非PII付款／保留結果与總額 | 完整Order、聯絡、歷史列表、取消、return、claim |
| checkout.pay | 原有效Reservation內發起該筆付款命令 | 偽成功、Unknown再付、完整Order或取消 |
| guest.read | 已驗證原Email的單笔Order及退款／DEMO憑證查閱，聯絡遮罩 | 列所有單、改Email／地址／退款去向、取消、歸戶 |
| guest.action(cancel/return) | 原單單次指定命令 | 其他動作／單、重用成第二次操作 |
| guest.action(claim)＋member | 明確歸戶該單且尚未屬別人 | Email同名批次歸戶、替他人會員取得單 |
| member | 本人Order／適當狀態內操作 | 另一member/guest Order，或後台退款權限 |

## 2. 請求、投遞與確認

OP-21輸入OrderId＋原聯絡Email＋intent，固定統一受理回應；不存在／不匹配不洩漏資料，也不向任意輸入管道發包含訂單存在的通知。對匹配資料由server發challenge至原交易合成persona；request回瀏覽器nonce binding，不直接回proof/token。

Email驗證／重設與guest動作challenge候選有效15分鐘，重送讓前枚失效；MFA challenge5分鐘屬另一用途，不套所有Email challenge。這些時間延用原政策建議，相關配置分開並待實證。GET只呈現確認，不消耗token，避免郵件掃描器完成操作；OP-22 explicit POST驗challenge、dataset/Order/intent/browser nonce、expiry、single-use、SecurityVersion。

證明成功read→guest.read session，閒置15分鐘／最長30分鐘候選，過期回受理入口，不重建付款；動作proof只授預定command的一次能力，不能順便升read／其他權限。若要同時查閱，明示另申read或在安全審閱後新增契約，不由實作者隨意擴權。

## 3. 去重、版本與歸戶

動作command先驗當前合法capability，再辨識原Idempotency-Key/RequestHash／資源；成功消耗與command／Audit同交易。網路回應丟失的合法原operation可查原結果，不能借已消耗cap重新發不同payload或目標。舊If-Match回412，沒有業務commit時不假稱動作完成；重新驗版本／範圍仍須有效授權。

OP-38具有效member session＋同原Order的claim proof，鎖Order所有權一次判斷；已屬另一member拒絕（不透露那位身份），同member同操作重送返回原關聯。登入／註冊／改Email都不自動claim；原GuestContact快照與舊交易內容不被新memberEmail覆寫。

能力與proof不放URL query／永久分享網址／日誌；若Email展示連結帶一次proof，須受控fragment或其他不進server/access log的方式，再explicit POST交換，confirmation頁no-store且不載第三方資源。具體安全傳遞方式是工程提案，實作前明確驗證Referrer／history／日誌與token撤銷；不能把秘密綁在公開API examples。

## 4. 合成收件匣與撤銷

本機測試收件匣GET /demo/inbox只對指定persona capability或受控test harness開放；持有Email字串不是收件匣權。persona由本機操作者／測試初始化受控配置，不接受customer api任意發全站persona能力。可以示範同一帳號／guest讀碼與確認，但不聲稱真實Email控制權已驗證或可正式上線。

撤銷能力／停用帳號／換角色撤相關SecurityVersion與scope，新操作拒絕；已發起交易結果仍由system追蹤，不能因guest session過期丟付款事件。取消訂單本身不撤銷原guest.read售後查閱權；它仍依原有效期與能力撤銷判斷。登出／切persona不殘留私有SSR payload；confirmation／Order HTML no-store/noindex，狀態頁自身不能當登入憑證。

## 5. 可驗收反例

猜OrderId＋Email不讀資料；mail scanner GET不consume；錯／過期／重送前token、不同nonce/intent/dataset、修改OrderId、過期session、已用cancel能力拿去claim、member A讀B或用B proof都拒絕。合法read不grant操作；合法原重送不再取消／建return；claim競爭僅一個owner。

這些是計畫，不以本機mock信件／文件讀取宣稱Email、session或授權實作通過。真正驗證綁[M2-AC07/08](M2_CHECKOUT_ORDER_PLAN.md)與OP-21/22/38。
