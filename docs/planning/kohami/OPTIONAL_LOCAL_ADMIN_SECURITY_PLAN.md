# 可選本機管理安全擴充評估

日期：2026-10-15。狀態：**DEFERRED_OPTION；未決定是否擴充，未授權後續實作。**

## 選配範圍

未來可重新評估本機管理員初始化／復原、原生操作者信任，以及依賴管理員身分的售後 API／SQL 瀏覽器驗收。這些項目已從 Issue 01／02 與目前活動規劃移出，不是目前 M0 或 issue 的驗收前置條件。

候選細節保存在[Owner 初始化與復原選項](options/OWNER_BOOTSTRAP_RECOVERY_OPTION.md)及[安全操作 profile 選項](options/SECURITY_OPERATIONS_PROFILE_OPTION.yaml)，僅供未來評估，不代表採用或可執行。

## 目前維持的決議與證據

- 已採用的後台登入方式仍為 Email／密碼＋TOTP；本次不改登入、敏感操作重新驗證、授權或資料庫維護政策。
- 使用者已明確將 Owner bootstrap／recovery 及三項 native action 自已批准 M0 範圍移除；現行 M0 為 core34、native4、12 項 acceptance。其他 Issue 01／02 業務規則不變，顧客購物流程仍可由其既有驗收獨立判定。
- Issue 02 的售後 UI mock journey 與專用 SQL harness 已通過；真實 API／SQL browser journey 尚未執行，不記為通過。被中止的隔離 fixture 已清理。
- Owner 初始化／復原的既有程式碼不因本選項而刪除或視為已驗收；未來若重開範圍，須另行確認產品目標、安全邊界與驗收。

## 未來重開條件

只有在使用者明確決定評估或擴充時，才重新確認目標環境與操作者、帳號初始化與復原威脅、MFA／近期 proof、稽核與資料隔離、實作範圍及真實驗收需求；之後另行取得產品與實作核准。
