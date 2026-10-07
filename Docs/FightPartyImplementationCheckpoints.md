# FightPartyControl 三人隊伍驗收關卡

工作場景為 FightScene3，延伸既有 EqualBeat、FMOD 時鐘、傷害與 Prefab 架構。
每一關完成後先交使用者驗收；未獲確認不 push，也不進入下一關。

## 已確認的方向

- 三人共用 HP、DEF、MP；HP 2+1+1=4，前衛戰士 DEF 2+1=3，其他前衛 DEF 1。
- MP 貢獻各 20，共 60；採用進場 0、上限 60、施放消耗 60 的預設，跨 Wave 保留。
- 現行全隊共用判定器每拍只接受一次成功 Basic，故每拍最多 +1 MP。
- Team Skill 期間停止普通能力输入。之後可能加入拍點按 A 鼓勵隊伍，本階段不實作其治療、補甲或其他效益。
- 既有 Team Ultimate 原樣保留，未來是否改為多次 Team Skill 後的強化版本另議。
- 不實作三種哥布林與哥布林王正式能力。

## 1. 角色資料、共用資源、戰前編隊（本輪）

- 沿用現有角色 Prefab 結構，建立 LongswordWarrior、EnergyMage、SkeletonGunner 資料 Prefab。
- 先保留共用劍士外觀；正式法師與骷髏燧發槍素材在後續掛點驗收接入，不宣稱本輪美術完成。
- 三對位置均可交換，角色位置、文字／按鍵、Basic 資料、Team Skill actor 順序與數值同步。
- 每次從資料重算戰士前衛 +1，不累加；進場 MP 0/60。
- 明確按 Start Battle 前不進行戰鬥，之後所有公開換隊／重新生成入口都不能改動編隊。
- 此場景取消 DEF 自然恢復；普通傷害先扣唯一共用 DEF，再扣唯一共用 HP。
- 不就緒能力以 Unavailable 明確標示：本輪戰士使用既有即時單體傷害、法師使用既有格擋；射手與三個 Team Skill 尚未開放，沒有沿用舊補血。
- 本轮不宣稱已完成三拍射擊、整拍多次格擋、Wave 或零血量結束流程。

驗收：六種排列；至少十次反覆交換戰士前衛；HP 4；DEF 3/1/1；MP 0/60；傷害溢出；英雄個別資料不扣血；開戰鎖定；UI 重建無重複。

### 第 1 關目前結果

- Unity 編譯成功；`FightPartyPreparationValidation` 在獨立 Preview Scene 通過 **170 項 assertions**。
- 已自動檢查六種排列、20 次反覆交換、HP/DEF/MP 資料、能力隨位置更新、開戰前阻擋输入／敵人行動／傷害、面板按鈕、重複建構、開戰鎖定與繞過公開 API 的嘗試、共用傷害與 Team Skill 普通輸入封鎖。
- 已檢視 `Temp/FightPartyPreparation-preview.png`：面板無重疊或截字。這是 UI 渲染預覽，不代表正式角色美術或實際 FMOD 操作驗證。
- 完整結果在 `Temp/FightPartyPreparationValidation.result`。
- 使用者已確認第 1 關，授權 push 並進入第 2 關；不將此確認當作已完成實際 FMOD／手把自動驗證。

### 使用者操作驗收

1. 開啟 `Assets/FightDemo/Scenes/FightScene3.unity`，進入 Play Mode。
2. 點選 `Front / Middle`、`Front / Back`、`Middle / Back`，確認角色名稱／位置與 X/Q、Y/W、B/E 一起更新。
3. 戰士前衛時顯示 HP 4、DEF 3、MP 0/60；法師或射手前衛時 HP 4、DEF 1、MP 0/60。
4. 反覆交換戰士前衛，確認 DEF 不會超過 3。開始前輸入普通能力不會觸發戰鬥或取得 MP。
5. 點 `START BATTLE`，編隊面板收起並出現 `FORMATION LOCKED`；Roster Inspector 的英雄位置欄位鎖住。
6. 目前戰士普通斬擊可用，法師仍是既有單次格擋版本；射手與 Team Skill 標示 pending。完整能力留待下一關，不以這一關判定能力完成。

重跑自動檢查：Unity 選單 `Rhythm Hunter > Validate Party Checkpoint 1 - Preparation`，或將 `run` 寫入 `Temp/FightPartyPreparationValidation.request`（編輯模式）。此檢查不覆寫使用者開啟的場景。

### Inspector 與 Prefab

- `FightScene3 > FightDemoController > Fight Roster Manager > Require Battle Preparation` 已勾選。此場景使用正式戰前鎖定與無自然回甲規則；舊測試場景維持原行為。
- `Hero Prefabs` 已配置 `ArtBattle/LongswordWarrior`、`EnergyMage`、`SkeletonGunner`，依序為戰士、法師、射手。
- `Fight Character Definition`：`Max Hp`、`Defense`、`Team Skill Mana Cost` 分別是角色資料來源。新欄位 `Frontline Defense Bonus` 僅戰士為 1，其他為 0。
- 既有 `Cast Effect Anchor`／`Impact Effect Anchor` 隨角色 Prefab 保留；正式法師防護罩、骷髏準備／瞄準／燧發槍口及脆弱狀態掛點尚待能力與美術關卡接入。
- 新增 `FightFormationPanel` 由既有 Presenter 初始化，不需另掛 Canvas 或第二個時鐘。

### 第 1 關主要變更檔案

- `Assets/FightDemo/Scripts/FightRosterManager.cs`、`FightCombatController.cs`：戰前入口、編隊鎖定、前衛護甲修正與共用資源規則。
- `FightCharacterDefinition.cs`：前衛加成及未開放能力的明確資料值，既有 enum 序號保持不變。
- `FightFormationPanel.cs`、`FightScenePresenter.cs`、`FightDefenseHud.cs`、`Editor/FightRosterManagerEditor.cs`：面板、資源文字與 Inspector 鎖定。
- `FightCombatController.TeamSkill.cs`：暫未完成技能的發動防護；`FightCombatController.TeamUltimate.cs` 僅補開戰前的發動防護，未重新設計大招。
- 三份新角色 Prefab 與 `.meta`、`FightScene3.unity`：新資料接線，保留原三份 Swordsman Prefab 供舊流程使用。
- `Editor/FightPartyPreparationValidation.cs`：獨立場景的自動驗證與 UI 預覽。

舊 `FightScene3BeatValidation` 等套件包含 5 HP、自然回甲、舊職業補血與零血量继续等歷史假設，不能拿它們的既有 PASS 當作本次新規格已通過。完整套件的遷移與重跑列在第 5 關。

## 2. 普通能力與輸入

- 戰士即時斬擊；法師一拍多次格擋及早晚判定窗口。
- 射手連續三拍準備、漏拍／錯拍／同拍重複、進度提示。
- 各成功普通能力输入 +1 MP；射手準備失敗不退款。

## 3. 脆弱、中斷、Team Skill 與被動

- 非疊加脆弱，僅有效傷害消耗，每名敵人獨立判定。
- 中斷結果由行為系統提供，射手只在成功時增加同次傷害。
- 逐角色即時決定技能模式；法師效果結算後滿甲。
- 足夠 HP 的三敵測試：戰法射 14/0/0、戰射法 8/4/4。

## 4. 暫停、Wave 與生命週期

- 敵人行為／已發射攻擊戰鬥進度凍結；中斷不刪除已發射物。
- 強化第零拍；射手進度凍結，正常拍恢復判定。
- 清 Wave 回滿 DEF、保留 HP/MP；整場不能換位；剩餘 Team Skill 不跨 Wave。
- 勝敗、無目標、停用與重入清理；預告／連斬／蓄力／延遲命中／盾窗口測試替身。

## 5. 完整回歸與演出

- 實際 FMOD、鍵盤／手把與早晚输入操作。
- 狀態／技能／編隊 UI、正式角色素材與特效掛點檢視。
- 更新失效的舊測試假設（5 HP、回血、自然回甲、零 HP 繼續玩）。
- 分列自動驗證、實際操作與未驗證項目，經確認後才 push。
