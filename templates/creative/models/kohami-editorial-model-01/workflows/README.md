# Garment Recolor 商品圖流程

本流程只產生商品單品色彩預覽；模特穿搭素材維持獨立，不混入商品圖片集。

## 每色產出步驟

1. 選取同一 SKU／版本的原始商品圖與衣物遮罩。確認兩者尺寸一致、遮罩白色區域只涵蓋衣物；避免沿用其他 SKU 上次的節點選取狀態。
2. 在 `Garment_Recolor_Reusable` 執行 `GarmentPaletteRemap`，固定 `harmony_strength=1.0`、`saturation_boost=1.0`、`light_garment_fix=true`、`merge_threshold=12`。不可將 harmony strength 設為 0；否則顏色亮度／飽和度可能偏離目標。
3. 每個顏色完成後，確認輸出仍是同一件商品、輪廓／布料紋理保持、主色接近色號，再繼續其他商品。
4. 上架前比對 PNG 內的 workflow metadata：原始圖 hash、遮罩檔名、目標色與參數；核對輸出尺寸及 SHA-256，並將資料記入 `ASSET_MANIFEST.json`。
5. 圖片放在對應 SKU／色號路徑。顏色選擇只切換該商品的單品圖；模特穿搭圖留在穿搭區。

這個色彩映射會保留商品原始輪廓及布料明暗紋理，輸出仍須逐張目視確認；不得以未核對的批次結果取代來源與遮罩驗證。
