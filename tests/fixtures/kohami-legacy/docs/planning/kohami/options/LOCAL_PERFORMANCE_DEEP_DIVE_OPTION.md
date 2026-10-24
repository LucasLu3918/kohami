# 本機深度效能診斷選配

狀態：`DEFERRED_OPTION`｜不阻擋本機環境啟動、API／SQL 連線或商品資料載入。

## 範圍

本文件收納超出本機服務基本可用性的效能工作：三語多路由 LCP／CLS 冷暖快取矩陣、不同成功／錯誤互動的固定樣本、裝置／CPU／網路條件比較、API P95 壓測，以及至少 30 分鐘的資源剖析和發現可重現瓶頸後的產品優化。既有 Issue24 lab 數據及其限制仍是歷史證據，不因移出必要驗收而刪除或改稱正式通過。

本機核心必要檢查只確認 API／SQL 已依其 readiness 契約就緒、Storefront 能載入三語商品資料，以及變更涉及的必要交易／授權行為。效能數值不得取代這些檢查，也不作本機服務可啟動門檻。

## 若日後採用

1. 固定候選 SHA、頁面／語系、瀏覽器與版本、OS／硬體、viewport、CPU／網路條件、冷暖快取、樣本數及量測期間。
2. 分別記錄 LCP、CLS、操作到 UI 穩定時間及瀏覽器 Event Timing 原始樣本；Event Timing 門檻或子集不得標為 INP，也不推估缺失樣本。
3. 記錄重現命令、原始樣本位置、計算方式、失敗／排除樣本與限制。localhost synthetic／lab 結果只作工程診斷，不宣稱正式 field Core Web Vitals 或 SLA。
4. 若需要正式 field INP、CrUX 或 SLA，先依[正式網域與搜尋曝光選配](PUBLIC_DOMAIN_SEO_OPTION.md)處理公開網域及資料治理前置；本選配本身不授權部署或收集訪客資料。

## 既有證據

Issue24 的三語桌面 LCP／CLS、互動延遲、鍵盤及版面紀錄保留於[Issue24 票券](../../../issues/kohami/24-core-mobile-accessibility-performance.md)與[驗證紀錄](../../../architecture/VERIFICATION.md)。哪些項目已完成、哪些未驗證，依其原始日期與範圍判讀；本文件不擴大其結論。
