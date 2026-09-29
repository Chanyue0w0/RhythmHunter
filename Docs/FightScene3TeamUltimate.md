# FightScene3 Team Ultimate

## 使用方式

1. 成功 Basic 累積共用魔力；全隊需求仍為角色加總，三名正式英雄共 30 點。
2. 滿魔力後按 A／R 發動 Team Skill。三名角色各自的完整表演時段都結束後，Ultimate 增加 1 格充能。
3. 預設累積 3 格後，右側顯示 `RB + A / SHIFT + R`。
4. 先按住 RB 再按 A，或按住 Shift 再按 R。發動不必踩拍，從下一個 FMOD 拍點開始全隊演出。
5. 預設演出 4 拍，第 4 拍對所有存活敵人各造成 3 點傷害，至下一拍才結束演出。

普通 A／R 只發動 Team Skill；RB＋A／Shift＋R 只發動 Ultimate。大招未就緒時不會退回誤放 Team Skill。

## 資源與戰鬥規則

- Ultimate 是全隊技能；傷害每個敵人只結算一份，不按參演英雄數量倍增。
- 完成一次 Team Skill 只增加一格充能。中途取消／更換編隊不給充能，充能不超過上限。
- 施放 Ultimate 立即消耗全部 Ultimate 充能，不消耗已儲存的 Team Skill 魔力。
- Team Skill 與 Ultimate 不可重疊施放。
- Ultimate 表演期間仍能操作 Basic，但不增加共用魔力；表演完整結束後恢復蓄力。敵人與音樂持續運作，不額外給予無敵。
- 暫停會凍結 FMOD、技能時段與傷害結算，繼續後接續原有進度。
- 更換編隊、進入校準、切換戰鬥模式或停用戰鬥控制器，會取消演出並重設兩套資源。

## Inspector 入口

`FightScene3 → FightDemoController → Fight Combat Controller → Team Ultimate (Equal Beat)`：

| 欄位 | 預設 | 意義 |
| --- | --- | --- |
| Team Ultimate Required Charges | 3 | 所需完整 Team Skill 次數，可設 1～8 |
| Team Ultimate Performance Beats | 4 | 全隊大招的表演拍數 |
| Team Ultimate Impact Beat | 4 | 第幾拍結算傷害，從 1 起算，不會超過表演拍數 |
| Team Ultimate Damage | 3 | 對每名敵人的傷害，按 0.5 單位計算 |

大招設定在啟動時固定，不受演出中 Inspector 改值影響。實際時間跟隨 FMOD BPM，不另設計時器。

## 畫面與演出

右側共用魔力量表下方新增小型菱形充能圖示，以及就緒、等待、演出剩餘拍數的文字。

目前演出使用現有角色素材：全隊同步集氣動作／光效，第 4 拍同步重擊特效與短暫金色畫面提示。傷害依 FMOD 拍點判定，與動畫、投射物速度無關；尚未新增正式專用大招圖組。

## 驗證

- `Temp/FightTeamSkillValidation.request` 寫入 `run`：涵蓋 Team Skill、3 次完整連鎖充能、封頂、互斥、資源分離、全體傷害、重複回呼及中斷取消。
- `Temp/FightTeamSkillLiveValidation.request` 寫入 `run`：Play Mode 模擬手把 A／RB＋A、實際 FMOD、充能 UI、Team Skill 與 Ultimate 暫停／繼續及傷害時機。結果與截圖在 Temp。
