# 商品與交易三語內容契約

日期：2026-10-13｜READY_FOR_REVIEW｜翻譯與細節是提案，未對外發布

## 1. 語言、欄位與發布

語系固定zh-TW／ja／en，路由前綴已採用。商品identity、skuId、colorId、GarmentSize、庫存、TWD、PriceSnapshot／operation不因切語言改變；只改內容呈現。字型與對比沿[視覺提案](VISUAL_SYSTEM.md)，不新增外部追蹤或付費字型。

可翻譯欄位：name、description、material／care、量測欄位label、color label、media alt、購物車／訂單商品展示名、政策／錯誤文案。不可翻譯成不同資料：Qty、實際量測值與cm單位、幣別、金額、SKU、原單位分攤與交易狀態。

商品內容來源欄位：ProductId／Locale／ContentRevision／Draft/Published／ReviewedAt／ReviewedBy（合成角色）。M1 fixture三語全部備齊，動態內容仍須人工語言校對；不宣稱日文／英文已由母語審查。

缺少某語譯文時：首版規劃fallback至zh-TW，頁面清楚標「以下部分內容以繁體中文顯示」。只逐項替代內容，locale URL／UI仍原語；不得用空字串靜默補齊並標全部翻譯完成。SSR需lang屬性與語言片段標示；完整核心三語驗收仍需補全主要商品／操作／結果文案，fallback只容錯，不代替完成條件。

## 2. 穩定文案key

| key | zh-TW | ja | en |
| --- | --- | --- | --- |
| demo.notice | 展示環境，無實際交易 | デモ環境です。実際の取引はありません | Demo environment; no real transactions |
| sku.choose | 請選擇顏色與尺碼 | 色とサイズを選んでください | Select a color and size |
| cart.notReserved | 購物車尚未保留庫存 | カートでは在庫を確保していません | Cart items are not reserved |
| product.soldOut | 缺貨，可查看商品資訊 | 在庫切れ。商品情報は閲覧できます | Out of stock; item details remain available |
| media.placeholder | 示意占位，非實物照片 | 実物写真ではない仮の画像 | Placeholder; not a product photograph |
| fee.demo | 示意運費，非正式報價 | 配送料はデモ用の仮データです | Demonstration shipping fee; not a real quote |
| quote.changed | 費用或庫存已變更，請重新確認 | 料金または在庫が変更されました。再確認してください | Costs or stock changed; please review again |
| content.fallback | 部分內容以繁體中文顯示 | 一部の内容は繁体字中国語で表示されます | Some content is shown in Traditional Chinese |
| fitting.unavailable | 換裝素材尚未驗證，請參考照片與尺寸資料 | 着せ替え素材は未確認です。画像と寸法を参照してください | Fitting assets are unverified; see images and measurements |

這些是審閱草案，沿用[既有交易狀態文案](EXPERIENCE_DESIGN.md)，不在本表創第二套退款／付款狀態。所有錯誤以server code＋參數查key，不回原始SQL／secret；amount以TWD整元格式，不用locale浮點格式改計算。

## 3. 管理維護與驗收

每次內容改版保留原公開Revision，發布／下架進Audit；同商品三語可以不同revision，但public回應標來源／fallback狀態。固定商品slug與ProductId，不因改名重新建立SKU。quote快照保存交易內容／語言來源；歷史Order展示原交易值，後續改翻譯不覆寫已成立單據內容。

相同篩選在不同語系以穩定colorId／GarmentSize比對；q可針對目前語言已發布內容搜索，fallback欄位可被檢索但必須標來源。按Locale/Revision選資料並以server allowlist校驗，不信任任意資料欄位排序或raw query。

驗收：完整fixture所有product/color三語key相同／非空，所有數字與SKU跨語一致；fallback／翻譯draft不洩漏私有來源；切語系保留cart；中文／日文長標籤與英文長字不溢出手機控制。原M1資料皆合成，不是商品真實量測或正式條款審查。
