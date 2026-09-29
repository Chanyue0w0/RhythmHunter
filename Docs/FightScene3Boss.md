# FightScene3 測試 Boss

FightScene3 的敵方第三格已改用 `Assets/FightDemo/Prefabs/ArtBattle/Goblin_TestBoss.prefab`，沿用 Killer Goblin 正式素材與現有動畫，獨立 HP 60。一般哥布林 Prefab 不啟用此狀態機，舊 FightScene 玩法也不受影響。

## 預設流程

所有時間使用 FMOD 拍點，沒有獨立 BPM 或秒數計時器。

| 狀態 | 時間 | 效果／操作 |
| --- | --- | --- |
| Neutral | 4 拍 | 補血、累積魔力；提示下一次攻擊 |
| Attack | 1 拍 | 普通攻擊 1；聖騎士同拍 Guard 可擋下 |
| Armor | 4 拍 | 每次受傷減少 0.5；破防能力累積至 3 可進 Break |
| Charge | 4 拍 | 最後一拍造成 2 傷害；累積 3 破防可打斷，也可同拍 Guard |
| Vulnerable | 4 拍 | 每次受傷增加 0.5 |
| Rage | 2 拍預告＋6 拍攻擊 | 演出第 3、5、7 拍普攻，第 8 拍重擊 3；破防能力及成功 Guard 累積至 3 可進 Break |
| Break | 4 拍 | 取消未結算攻擊、停止新攻擊、每次受傷增加 0.5，結束回 Neutral |

未打斷 Rage 時，重擊後回 Neutral。破防進度每個新狀態重新計算；沒有強迫角色輪替或 Basic 冷卻。

## 角色互動

- 法師：Basic 增加 1 破防，Team Skill 增加 2。第三次破防先進 Break，再結算該次傷害，享有 Break 加成。
- 聖騎士：維持同拍 Guard；在 Rage **實際成功擋下攻擊**才增加 1 破防。只按 Guard、不遇到攻擊不會增加。
- 吟遊詩人：維持補血，讓玩家能在安全階段恢復。
- Team Skill／Ultimate 傷害也受 Armor、Vulnerable、Break 影響。Ultimate 仍是每個敵人一次傷害。
- 提前踩拍依判定歸屬拍套用狀態；攻擊仍在 FMOD 回呼後、完整晚判定窗口結束才結算。

## Inspector

選取 **Goblin_TestBoss Prefab → Fight Character Definition → Test Boss Pattern (Equal Beat Only) → Boss Pattern**：開關、各階段拍數、破防門檻、減傷、易傷加成、蓄力／狂暴重擊傷害、成功 Guard 破防量。

角色 Prefab 的 **Boss Interaction → Basic Break Power / Skill Break Power** 可設定破防能力；數值 0 表示沒有破防效果。

更換 Boss：FightScene3 的 FightRosterManager → Enemy Prefabs 第三格。場上位置沿用 Enemy Spawn Slots 對應物件的 Transform。

暫停會凍結 Boss 與音樂。重生編隊、校準、切換模式會重設流程；死亡 Boss 不再行動。Break 事件已提供給後續 Fever 接入，本次尚未加入半拍 Fever。

## 驗證

`Temp/FightBossValidation.request` 寫入 `run` 可執行狀態／能力互動測試；`Temp/FightBossLiveValidation.request` 則測試實際 FMOD、暫停與畫面。結果及截圖寫入 Temp。實機 Boss 測試注入成功能力判定；手把輸入另由 Team Skill 實機測試覆蓋。
