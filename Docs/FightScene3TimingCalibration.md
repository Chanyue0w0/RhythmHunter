# FightScene3 跟拍校正

進入戰鬥後，點擊右側 `TIMING`，或按鍵盤 `F8`／手把 `Start`。
音樂繼續播放，戰鬥暫停；待結算的攻擊會取消，護甲恢復拍數暫停累計。

1. 先使用 `CUES: OFF`，聽著歌曲每拍按一次 Q/W/E 或 X/Y/B，任選同一個鍵即可。
2. 前 4 次是暖身，接著收集 32 個不同拍點的輸入。
3. `Measured habit` 是未套用個人補償的跟拍偏差中位數。`LATE` 表示較晚，`EARLY` 表示較早。
4. `Spread` 表示穩定程度；`Drift` 是最後 8 拍和最初 8 拍的中位數差。
5. 按 `PREVIEW` 試用，繼續跟拍，觀察 `COMPENSATED` 是否接近 0 ms。
6. 按 `SAVE` 保存；不保存而關閉會還原試用前的數值。`RESET` 清除此輸入裝置的保存值。
7. `CLOSE`／F8／Start／Escape 關閉，戰鬥在後續拍點恢復。

鍵盤與手把分開保存，跨歌曲沿用。切換輸入裝置會重新採樣。
結果包含個人習慣、輸入及音訊裝置延遲，不能視為純粹的反應速度。
更換耳機、藍牙／有線輸出等裝置後應重新校正。

## 判定與保存規則

- 不更改 FMOD Tempo Marker、BPM 或音樂速度。
- 使用 Input System 事件時間戳回推輸入時間，超過 250 ms 的過期輸入不採用。
- 個人習慣以毫秒保存，範圍 ±150 ms。晚按 +60 ms 代表從輸入時間扣除 60 ms。
- 基礎 Inspector `Judgement Offset Ms` 仍保留舊有正負號慣例；它與個人習慣值是不同項目。
- 拍點 UI 使用基礎 offset，個人輸入補償不移動 UI。
- 同一拍不能重複採樣；遠離拍點的輸入會略過。暖身後以 MAD 排除明顯離群值。
- 32 次中至少 24 次為一致樣本，穩定度須 ≤30 ms，前後漂移須 ≤30 ms，才可套用。
- 失去焦點、超過 5 秒未跟拍、切換輸入裝置或切換視覺提示模式會重新暖身。
- 若持續無法取得穩定結果，先檢查歌曲 Tempo Map；不要用個人 offset 補償歌曲的漸進變速。

校正值存於 Unity PlayerPrefs：`FightTiming.v1.Keyboard`、`FightTiming.v1.Gamepad`。
在 Hierarchy 選取 `FightDemoController`，查看 `FmodRhythmJudge` 的「個人跟拍校正」區塊，
可直接看到鍵盤、手把的保存值（停止 Play Mode 後也可查看）。播放時另顯示當前裝置、
個人補償、基礎 Offset 與有效判定 Offset，並即時更新。所有校正讀值為唯讀，不會在檢視時覆寫保存值。
一般攻擊結果也會顯示補償後的 EARLY／LATE 毫秒數，方便比較。

## 驗證

`Rhythm Hunter → Validate FightScene3 Equal Beats` 使用隔離場景，包含校正統計、正負方向、
重複輸入、漂移拒絕、試用取消、戰鬥暫停及介面渲染驗證，不會保存玩家校正偏好。
結果與介面圖輸出至 `Temp/FightTimingCalibrationValidation.result` 及
`Temp/FightTimingCalibration-preview.png`。
