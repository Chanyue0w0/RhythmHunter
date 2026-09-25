# 校正場景與戰鬥暫停

## 校正

開啟 `Assets/FightDemo/Scenes/FightCalibrationScene.unity`，進入 Play Mode。
此場景只播放校正音樂，不執行戰鬥。音樂設定複製自建立場景當時的 FightScene3；
之後可在 `CalibrationController → FmodBeatClock` 更換歌曲。

1. 先用 `CUES: OFF`，聽著歌曲每拍按一次 Q/W/E 或 X/Y/B，任選同一個鍵。
2. 前 4 次暖身，接著收集 32 個不同拍點的輸入。
3. `Measured habit` 為未套用補償的跟拍偏差中位數：LATE 晚按、EARLY 早按。
4. `Spread` 為穩定程度；`Drift` 為最後 8 拍與最初 8 拍的中位數差。
5. `PREVIEW` 試用後，繼續跟拍觀察 `COMPENSATED` 是否接近 0 ms。
6. `SAVE` 保存；`RESET` 將目前裝置的保存值歸零。鍵盤與手把分開保存。
7. `ENTER BATTLE` 進入 FightScene3，戰鬥直接讀取已保存數值。未保存的試用不會帶入戰鬥。
8. 歌曲播完或想重新開始時，按 `REPLAY MUSIC`。

此數值包含習慣與裝置延遲，不是純粹的反應速度；換耳機／輸出裝置後應重新校正。
結果若持續漂移，先檢查 Tempo Map，不要用個人 offset 補償歌曲變速。

## 戰鬥暫停

FightScene3 不再提供校正面板。按手把 Start、鍵盤 Escape／F8，或右側 PAUSE 按鈕，
會暫停遊戲與 FMOD 音樂；再次按鍵或點 RESUME 從原位置繼續。
角色動畫、特效、戰鬥輸入、扣血與護甲恢復會暫停，待結算攻擊保留至恢復後處理。
音樂不會因暫停而重新起算拍數。

## 共用保存檔

主要保存位置為 `Application.persistentDataPath/rhythm-calibration.json`。
目前 Windows 專案對應 `%USERPROFILE%/AppData/LocalLow/LootingLimeGameStudio/OtterHero/`。
兩個場景使用同一份檔案，不需手動搬移。

- 第一次讀取會從既有 `FightTiming.v1.Keyboard`／`FightTiming.v1.Gamepad` PlayerPrefs 原樣搬入。
- 舊 PlayerPrefs 不刪除；日後 SAVE 同步更新舊格式，以利相容舊版。
- `rhythm-calibration.legacy-backup.json` 保留首次搬移的值。
- 每次覆寫會將上一版保留為 `rhythm-calibration.json.bak`。
- 檔案無法讀取或版本不支援時保留原檔、回退舊 PlayerPrefs，並顯示錯誤，不直接覆寫。
- 寫入失敗會顯示錯誤，不會宣稱 SAVE 成功。

選取 FightScene3 的 `FightDemoController` 或校正場景的 `CalibrationController`，
查看 `FmodRhythmJudge` Inspector，可看到兩種裝置的保存值、檔案位置與播放時實際套用值。
正值代表習慣晚按；有效判定 Offset = Inspector 基礎 Offset − 個人補償。
個人補償不移動音樂或 UI 拍點。

## 驗證

- `Rhythm Hunter → Validate FightScene3 Equal Beats`：隔離場景的戰鬥與校正回歸檢查。
- `Rhythm Hunter → Validate Calibration To Battle And Pause`：短暫進入 Play Mode，驗證跨場景讀取、
  音樂實例、暫停與恢復；結束後還原原本編輯場景，不保存或重設校正值。
- 新場景與 FightScene3 均已加入全域及目前 Build Profile 的場景清單，既有啟動場景順序不變。
