# 沙織（KOHAMI Editorial Model 02）

此資料夾保存虛構成年模特「沙織」的候選、姿勢／表情與三視圖參考。伊織仍在 `kohami-editorial-model-01`，兩者身份素材不可混用。

## 狀態與用途

- D 已由使用者於 2026-10-26 正式定案為沙織唯一身份主圖。
- 其餘候選 A/B/C/E 只供姿勢、場景或歷史候選比對，不可作為沙織臉部身份來源。
- Qwen 姿勢圖與三視圖均依使用者要求納入參考集；按 manifest 限定各自用途，不是身份鎖定保證。
- `10-turnaround-three-view-qwen.png` 可用來對照正面、側面、背面輪廓和服裝一致性。
- 使用者已確認目前產圖機制可公開使用；素材仍保留原模型授權資訊，原始授權條款不因該確認而改寫。

## 後續生成

以 `references/01-identity-candidate-d.png` 作為 `image_1` 的正式身份來源；商品準確度需要來源商品照片作為 `image_2`。每次由身份參考重新編輯，不串接上一張成品。若身份差異明顯，拒收並沿用已定案的 D 身份主圖。

每張素材的 SHA-256 與 AIPS 執行紀錄見 `CHARACTER_ARTWORK_MANIFEST.yaml` 和 `generation-runs/2026-10-26/`。
