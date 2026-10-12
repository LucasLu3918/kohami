# 票 06 技術實證方案 v0.1

日期：2026-10-13｜Status: PROPOSED｜僅官方文件查核，實證 NOT_EXECUTED。

適配性：兩個商務核心均有條件適合。01–03 政策已採用；以下僅文件查核與工程推論，實證皆 NOT_EXECUTED，不構成選型或部署批准。

| 候選 | 官方能力／KOHAMI 缺口 |
| --- | --- |
| Medusa v2 | [Reservation](https://docs.medusajs.com/resources/commerce-modules/inventory/reservations-lifecycle) 原生於下單建立，取消釋放；15 分鐘付款前保留須客製。[Workflow locks](https://docs.medusajs.com/learn/fundamentals/workflows/locks) 可保護競爭。[付款回呼](https://docs.medusajs.com/resources/commerce-modules/payment/webhook-events) 可能自動完成購物車，須攔截到期後成功，避免恢復訂單。[MFA](https://docs.medusajs.com/resources/commerce-modules/auth/mfa) 已有 TOTP／復原碼，但須另驗證後台強制註冊。[Cloud](https://docs.medusajs.com/cloud/pricing) 將 RBAC 列 Enterprise；自架可用範圍與授權未證實。 |
| Vendure | 原生 [RBAC](https://docs.vendure.io/current/core/core-concepts/permissions) 與自訂權限較貼近四角色。[庫存](https://docs.vendure.io/current/core/core-concepts/stock-control) 預設付款授權／結算才分配，15 分鐘保留須調整策略及到期工作。[v3.7.4](https://docs.vendure.io/changelog?focus=v3.7.4) 加入 StockLevel row lock／不足事件；SQLite 無 row lock。MFA 整合仍待證明。 |
| Next.js | [App Router](https://nextjs.org/docs/app/guides/internationalization) 支援語系路由，[Metadata](https://nextjs.org/docs/app/api-reference/functions/generate-metadata) 可產生 canonical／hreflang；商品翻譯、三語 URL 與 SEO 規則由專案維護。 |
| Nuxt | [i18n SEO](https://i18n.nuxtjs.org/docs/guide/seo) 可產生 lang、canonical、hreflang；需設定 locale 與 baseUrl。三語實際 HTML／商品 slug 仍須查驗，模組加入升級維護責任。 |

兩核心均不能以退款 API 取代雙人覆核：需持久化提案／核准人、拒絕同人、自動重送去重及防止直接 API 繞過。Medusa [payment provider](https://docs.medusajs.com/resources/references/payment/provider) 與 Vendure [shipping](https://docs.vendure.io/current/core/user-guide/settings/shipping-methods) 提供擴充接縫；台灣金流、物流、發票的驗簽／退款／對帳能力仍待供應商沙盒，沒有已驗證現成支援結論。

有條件建議：優先實證 Vendure＋PostgreSQL，理由為原生 RBAC 較符合已採權限；若 MFA 或庫存保留改造過大，再比較 Medusa 的授權／客製總成本。前台由維護者技能決定：熟 React 選 Next.js，熟 Vue 選 Nuxt；現無團隊技能證據，不能據 SEO 選出勝者。先採單一核心與 worker 的候選部署，資料儲存、備份與主機另待批准；不推定月費可在 NT$8,000 內。

最小實證：鎖定版本，以 1 SKU／1 庫存、可控時鐘與模擬支付跑雙人競購、同鍵重送、15 分鐘到期／重啟、回呼重複／亂序／延遲成功及付款未知；查庫存與付款嘗試持久資料。再驗證四角色越權、未完成 MFA、同人退款及原生退款入口繞過；三語商品頁確認無 JS 的 HTML、canonical／hreflang／sitemap，檢查改價不覆寫訂單價格快照與 TWD 分攤。最後以合成資料備份復原；真供應商能力由票 04–05 補證。

## 本輪建議的實證範圍

建議先用 **Next.js＋Vendure＋PostgreSQL** 做本機最小實證；這是供確認的候選路線，不是已批准的最終產品架構。Vendure 優先來自已查核的權限接縫；Next.js 是尚無團隊框架偏好時的工程預設，並不主張比 Nuxt 更快或更能達成三語 SEO。若已有 Vue 維護者，採 Nuxt＋Vendure＋PostgreSQL 是合理替代。若 Vendure 的 MFA／保留改造證據不符，再比較 Medusa 的整體授權與改造成本。

實證限合成商品／帳號／支付事件，本機環境、一個真實可測的核心與必要背景工作；不買雲端、不申請服務、不正式收款、不導入 AI 或本人照片。實際部署供應商、費率、維護人力與正式開發範圍待證據另審，無法保證既有 NT$8,000／月目標。

## 品質與證據門檻

| 邊界 | 通過條件／驗證方式 | 本輪證據 |
| --- | --- | --- |
| 庫存與重送 | 1 SKU 最後一件並行只承諾一次；同操作重送不重建；15 分鐘到期後重啟、取消／成功競爭不超賣。以公開請求與持久資料核對 | NOT_EXECUTED |
| 金額／歷史 | TWD 分攤總額守恆；付款期與已付款後改價不覆寫快照；晚到不恢復訂單 | NOT_EXECUTED |
| 身份／退款 | 四角色正反向、後台 MFA、降權／登出撤銷、禁止同人退款及原生入口繞過；瀏覽器旅程＋必要並行邊界 | NOT_EXECUTED |
| 三語／可及性 | 合成商品三語可索引 HTML 與 canonical／hreflang；切換保留 SKU／購物車；鍵盤、焦點、錯誤提示可完成購物 | NOT_EXECUTED |
| 效能 | N01 的 LCP／INP／CLS 規劃目標須依頁面、裝置／網路及樣本量測；本機 smoke 不證明真實使用者 P75 | NOT_EXECUTED |
| 復原／成本 | 合成資料還原後對帳與刪除限制維持；N04 RPO／RTO 及 N03 每月可用性須正式環境證據。先記實證耗時與主機／儲存需求，再估服務、維護、素材總成本 | NOT_EXECUTED |

候選確認後鎖定可重現版本與實際 package manifest，才建立執行命令；目前沒有產品測試工具或 package.json，不提供看似可執行的假命令。真供應商事件與能力須票 04／05 沙盒，合成事件不替代它們；換裝品質、權利與支援尺寸仍須人工及實際素材證據。

## 待確認

請選擇先實證的候選：Next.js＋Vendure＋PostgreSQL（建議），或 Nuxt＋Vendure＋PostgreSQL。接受候選只批准上述本機實證；最終架構採用及正式部署需實測後再確認。票 06 仍未 resolved，票 08 尚未解除依賴。
