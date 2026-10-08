# FightScene3 Team Skill

> FightPartyControl 正在分關卡改造三人隊伍。FightScene3 已換成戰士／法師／射手資料，需求 60 MP，Team Skill 期間封鎖 Basic；第 3 關已接入破勢斬、能量波動、破陣彈、脆弱與法師滿甲被動，待使用者驗收。技能條件在每位角色實際施放時判定。下列舊職業、30 MP 與演出期間可操作 Basic 的敘述是歷史版本，最新規則、Inspector 與驗收請看 [三人隊伍驗收關卡](FightPartyImplementationCheckpoints.md)。跨 Wave／飛行攻擊／完整時間凍結仍待第 4 關。

## 操作

以下為舊版操作紀錄。目前三位新角色均為 2 拍：首拍準備、第二拍結算；每角上限 4 拍，完整連鎖共 6 拍。新角色的設定與驗證狀態以三人隊伍驗收文件為準。

- 成功踩拍使用 X／Y／B Basic（鍵盤 Q／W／E）增加全隊共用量表。
- 預設每次 +1 魔力；需求是目前隊伍三名角色的需求魔力加總。三名正式英雄各需 10 點，因此全隊需求 30 點；Miss、重複拍點、空槽不增加魔力。
- 滿後按手把 **A** 或鍵盤 **R**。發動鍵不需要踩拍，也不消耗該拍的 Basic 判定。
- 魔力立即歸零，從下一個 FMOD 拍點開始，依 **Front → Middle → Back** 施放各角色 Team Skill。每名角色擁有獨立的固定表演拍數，預設每人 4 拍。
- 例如首位在拍點 N 開始，則中位在 N+4、末位在 N+8 開始；末位演出持續至 N+12 才完成整段連鎖，共 12 拍。
- 連鎖期間仍可操作 Basic，但不增加魔力；到最後一名角色的完整表演時段結束後才能重新蓄力。
- 空角色位置會跳過；編隊變更、進入校準或停用戰鬥控制器會取消連鎖並清空量表。
- Start／Esc／F8 暫停音樂時連鎖一併暫停，繼續後接著施放。
- Team Skill 成功啟動（包括等待下一拍）起，敵人動畫保持原畫格與小數進度，攻擊結算、一般攻擊排程、Boss 狀態倒數也暫停。音樂、我方演出與我方護甲恢復照常進行。
- 最後一名角色表演完整結束後，未被打斷的攻擊動畫從原進度繼續。待結算攻擊移至恢復當拍，保留完整踩拍防禦窗口，不立即補算過期傷害，也不補發停住期間的攻擊。
- 普通受擊不會替換凍結畫格。打斷技能／Break／擊倒敵人會取消該次攻擊與動畫回呼；恢復後不會重播。Team Skill 中造成的 Break，其完整拍數從恢復後開始計算。
- 新技能可呼叫 `FightCombatController.InterruptEnemyAttack(enemySlot)` 中斷指定敵人；中斷不會解除 Team Skill 的整體凍結。
- 完整完成一次連鎖增加 1 格 Ultimate 充能，預設 3 格可按 **RB＋A**（鍵盤 Shift＋R）發動全隊大招。大招不消耗 Team Skill 魔力，詳見 `FightScene3TeamUltimate.md`。

## 調整參數

開啟角色 Prefab → **Fight Character Definition → Team Skill / Legacy Fourth-Beat Skill**：

| 欄位 | 預設 | 用途 |
| --- | --- | --- |
| Team Skill Mana Cost | 10 | 此角色對全隊需求魔力的貢獻 |
| Team Skill Performance Beats | 4 | 此角色技能表演的固定拍數 |

三個正式英雄位於 `Assets/FightDemo/Prefabs/ArtBattle/Swordsman_Paladin.prefab`、`Swordsman_Bard.prefab`、`Swordsman_Mage.prefab`。例如需求改成 10／12／15，隊伍需求即為 37；拍數改成 2／5／3，依編隊順序演出共 10 拍。空槽不貢獻魔力或表演時間。

每次 Basic 的魔力增加量仍在 `FightDemoController → Fight Combat Controller → Team Skill (Equal Beat) → Team Skill Gauge Gain`，預設 1。全隊不再另外設定固定需求值或共用技能間隔。

這些參數不改變 FMOD BPM。右上方顯示目前／需求魔力、READY、等待下一拍、當前角色剩餘表演拍數與技能效果。表演拍數在啟動連鎖時固定，暫停期間不流逝。

各角色技能沿用角色 Prefab 的 **Fight Character Definition**：`Skill Name`、`Skill Behavior`、`Skill Power`、`Skill Effect Prefab`。位置順序由 Fight Roster Manager 決定，不綁定職業。

目前正式角色資料：

| 角色 | Team Skill | 效果 |
| --- | --- | --- |
| 聖騎士 | Aegis Counter | 當拍 Guard＋攻擊前方敵人，技能強度 1 |
| 吟遊詩人 | Healing Chorus | 全隊共用 HP 恢復 1，不補護甲 |
| 法師 | Arcane Burst | 對所有存活敵人造成 1 傷害 |

目前技能效果在各角色表演開始的拍點結算，動作素材沿用現有圖組；固定表演時段供後續正式演出接入，沒有新增動作素材。Boss 狀態已實作，詳見 `FightScene3Boss.md`；Buff／Debuff 與 Fever 尚未實作。

## 驗證

- `Temp/FightTeamSkillValidation.request` 寫入 `run`：量表、重複輸入、順序、空槽與取消規則。
- `Temp/FightTeamSkillLiveValidation.request` 寫入 `run`：虛擬手把 A／RB＋A 路由、實際 FMOD 連鎖、暫停／繼續與畫面擷取。
- 既有 `FightScene3BeatValidation` 用於 HP、護甲、Guard、陣形、校準與節拍 UI 回歸。
- `FightEnemyPauseValidation` 驗證畫格／小數進度、傷害／倒數暫停、正常恢復、中斷、Break 與清理；`FightEnemyPauseLiveValidation` 使用實際 FMOD 驗證中途停住攻擊、全域暫停重疊與技能打斷。
