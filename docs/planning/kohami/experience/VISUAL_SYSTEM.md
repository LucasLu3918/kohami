# 本機展示視覺提案

日期：2026-10-13｜READY_FOR_REVIEW｜不是已採用品牌規範

## 方向與範圍

承接既有日系服飾品牌與半寫實日系型錄角色方向。商店建議採留白、米白背景、深色文字、莓色重點；商品實拍／合法示意素材為主，避免視覺模擬暗示真人合身。KOHAMI名稱、商標及字標權利仍待查核，不建立虛構認證或物流合作標章。

此為可直接試作的設計提案，未產生完成畫稿、商標或素材授權。既有角色風格不代表以下商店色票已獲採用。

| Token／元件 | 建議值／行為 |
| --- | --- |
| VIS-01 色彩 | background #FAF7F2、surface #FFFFFF、text #262326、muted #5F5860、accent #83334F、border #D8D0D5；所有實際配對量測對比，不以色碼聲稱合規 |
| VIS-02 字體 | 使用具繁中／日文／拉丁覆蓋的系統sans-serif堆疊；本文16px起、行高1.6，標題依層級；不新增付費字型或外部追蹤請求 |
| VIS-03 間距 | 4px基礎，常用8/12/16/24/32；表單群組可辨，手機頁邊16px，桌面內容最大1200px |
| VIS-04 控制 | 常用點擊目標至少44×44 CSS px作UX建議；focus可見且不被固定列遮住，禁用有文字原因 |
| VIS-05 商品卡 | 圖片保留比例與尺寸避免位移，價格／SKU／缺貨文字清楚；換裝資格獨立標示 |
| VIS-06 按鈕 | 每區一個主要動作；危險操作附確認；loading仍保留label，後端冪等不依賴disabled |
| VIS-07 狀態 | badge＋文字＋下一步；待確認不能用紅色「失敗」，mock完成不使用「已入帳」 |
| VIS-08 表格 | 後台交易可掃讀；小螢幕改摘要卡或局部可辨橫向捲動，不讓整頁難以操作 |
| VIS-09 圖片 | 合法授權、用途、版本及替代文字；不能以生成／未審核素材取代真實商品準確性 |
| VIS-10 動態 | 非必要動態可關閉，遵循prefers-reduced-motion；不使用輪播／動畫阻擋購買 |

## 元件狀態與審閱方式

每個元件需設計 default／hover／focus／disabled／loading／error，表單另有valid與read-only。Checkout Summary、Order Status Timeline、Refund Confirmation、Demo Banner、Permission Denied、Empty State為共用元件，不在不同頁發明矛盾文案。

下一個可審閱成果建議為首頁、商品頁、結帳、訂單、管理退款五個桌面／手機線框與色彩樣式；本輪是文字線框與token提案，不聲稱視覺稿已完成。素材未備妥可用明示占位圖驗證布局，不能公告真實商品／換裝資格。追蹤 [畫面規劃](EXPERIENCE_DESIGN.md) SCR-01–09與 [品質規劃](../../../quality/QUALITY_PROFILE.yaml)。
