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

## 1. 角色資料、共用資源、戰前編隊（已確認、已 push）

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
- 已 push 至 `origin/FightPartyControl`，提交 `a044a3d`。下列第 1 關能力開放狀態為當時紀錄，目前狀態以第 2 關為準。

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

## 2. 普通能力與輸入（已確認、已 push）

使用者已確認並授權進入第 3 關；提交 `bc32b55` 已 push 至 `origin/FightPartyControl`。以下保留本關完成時的驗證範圍。

- 戰士即時斬擊；法師一拍多次格擋及早晚判定窗口。
- 射手連續三拍準備、漏拍／錯拍／同拍重複、進度提示。
- 各成功普通能力输入 +1 MP；射手準備失敗不退款。

### 已實作

- 戰士成功判定當下對目前存活首位造成基礎 1 HP，仍由節拍判定掌握傷害時機。
- 法師的 Guard 在該判定拍內不會被首次命中消耗；同拍多次符合規則的命中都被擋住，下一拍不沿用。動畫／VFX 顯示於實際前衛。
- 射手三次連續成功輸入依序準備、瞄準、射擊；第三次重新尋找存活首位，基礎傷害 1，射擊後歸零。中斷與被動增傷仍留在第 3 關。
- 同拍重複輸入不推進、不加 MP，也不清掉本拍已取得的準備；射手錯拍會清零。下一拍被其他角色成功使用時，因既有共用判定規則，射手進度清零。
- 無輸入的漏拍依 FMOD 時間軸、BPM、個人校準與晚側窗口關閉判斷，不在 OnBeat 當下提前清零。使用既有判定安全邊界，不新增音樂時鐘。
- 每次準備／瞄準／射擊成功各 +1 MP；失敗不退還已取得的 MP；法師沒擋到攻擊仍 +1；重複命中不額外產 MP；封頂 60。
- 基本進度 HUD 顯示位置按鍵與 `1/3 PREPARING`、`2/3 AIMING`，格擋有效拍顯示 `GUARD ACTIVE`。
- Team Skill 暫停時不清準備；恢復後第一個普通拍可接續。已驗證暫停入口的模擬呼叫，完整 Team Skill 演出仍待第 3／4 關驗收。
- 無存活目標、戰鬥結束旗標、停用、校準與重建陣容會清準備；已提供 `ResetBasicAbilityProgress()` 給後續 Wave 清理使用，尚未宣稱換波流程完成。

### 第 2 關驗證

- Unity 編譯成功；獨立 Preview Scene 使用**正式 FmodRhythmJudge** 的確定性驗證通過 **91 項檢查**。
- 包含早／晚邊界、同拍三次格擋、相鄰格擋、射手兩輪射擊、同拍連按、錯拍、漏拍、換角色、死亡後換目標、沒有目標、MP 封頂與失敗不退款。
- 測試涵蓋 60／120／150 BPM，以及 -90／0／+90 ms 判定偏移；沒有使用替代音樂時鐘。
- UI 預覽：`Temp/FightPartyBasic-preview.png`；結果：`Temp/FightPartyBasicValidation.result`。
- 不包含實體手把／實際 FMOD 音訊延遲操作、敵人正式技能、中斷增傷、新 Team Skill、Wave 與正式射手美術。

### 使用者操作驗收

1. FightScene3 進 Play Mode，先採預設戰士／法師／射手順序並點 `START BATTLE`。
2. 拍點按 Q／X：首位立即受傷，MP +1。
3. 拍點按 W／Y：MP +1；該拍敵方命中被格擋。單一敵人目前未提供正式連斬；同拍多次命中已由獨立驗證排程確認。
4. 連續三拍各按一次 E／B：依序顯示 1/3、2/3、射擊並歸零，MP 共 +3。
5. 試同拍連按、第二拍不按、第二拍錯拍、或改按另一角色；確認不會提前射擊、進度依規則清理，已取得 MP 保留。
6. 可停止 Play Mode 後交換編隊再開戰，確認射手提示與能力跟著新的 X／Y／B 位置走。

自動驗證入口：`Rhythm Hunter > Validate Party Checkpoint 2 - Basic Abilities`，或 `Temp/FightPartyBasicValidation.request` 寫入 `run`。

### 第 2 關資料／掛點／檔案

- `FightCharacterDefinition` 新增 `Consecutive Input Beats`（射手 3）與 `Basic Guard Beats`（法師 1）；普通傷害維持使用 `Normal Ability Power`。
- `SkeletonGunner.prefab` 已將 Basic 設為 `AimedShot`，`EnergyMage.prefab` 明確設定格擋一拍。
- `FightCombatController.BasicAbilities.cs` 管理準備、錯拍、到期及清理；既有 controller 傷害入口、EnemyPause、TeamSkill 重設接入此狀態。
- `FightUnitSlot.PlayGuardAt` 支援將效果指向實際前衛，能力 Prefab 仍由施法角色決定。
- `FightDefenseHud` 顯示準備進度與格擋；`FightPartyBasicValidation` 提供本關驗證。
- 仍沿用暫代劍士外觀與既有施法／命中特效掛點；正式準備、瞄準、燧發槍射擊動畫和槍口掛點未在本輪製作。

## 3. 脆弱、中斷、Team Skill 與被動（已確認）

- 非疊加脆弱，僅有效傷害消耗，每名敵人獨立判定。
- 中斷結果由行為系統提供，射手只在成功時增加同次傷害。
- 逐角色即時決定技能模式；法師效果結算後滿甲。
- 足夠 HP 的三敵測試：戰法射 14/0/0、戰射法 8/4/4。

### 已實作與驗證

- 演出時間已依使用者指定：每角最多 4 拍；目前戰士、法師、射手各 2 拍。第 1 拍分別為跳躍／舉手／舉手瞄準的準備拍，第 2 拍斬擊／能量爆破／射擊才結算技能及法師被動。正式準備動畫仍待素材掛接，目前提供 PREPARING 提示。
- 三人於 N、N+2、N+4 開始，效果於 N+1、N+3、N+5 結算，N+6 恢復普通戰鬥；共 6 拍，120 BPM 為 3 秒，另加發動後等待下一拍的時間。敵人在準備與結算期間皆保持 Team Skill 暫停。
- 2 拍修訂已編譯；測試已改成每拍推進，新增準備拍零傷害、第二拍唯一結算及完整保留末拍的檢查。首次重跑遇到 Play Mode 與 Preview Scene 不相容；回到編輯模式後本關通過 **110 項檢查**，兩種編隊傷害維持不變。下方 83 項為修改拍數前紀錄。
- 脆弱附著於個別敵人；重複施加不疊加，下一次正傷害加倍並立即移除。HUD 顯示 `◆ x2`，消耗後清除。零傷害、現有 Boss 完全減傷不消耗；保留既有 Boss 減傷先結算，再套用脆弱的流程。
- 戰士先造成 2 傷害，再對原目標仍存活時施加脆弱；可消耗舊脆弱後重新施加，斬殺不轉移狀態。
- 法師輪到施放才尋找最前方脆弱敵人，集中基礎 4，否則全體各 2。自身技能結算後補滿共用 DEF，位置與模式不影響被動，不補 HP。
- 射手技能只檢查當時首位是否脆弱；單體 4 或全體各 2，選定後不因死亡切換模式，也不附加普攻中斷。
- 射手普通第三拍向既有敵人行為排程要求中斷；成功才將同發傷害由 1 改為 2，再由共用傷害入口套用脆弱。待機、已結算收招及不可中斷皆不加成，不產生第二次傷害。
- 中斷涵蓋既有待結算攻擊與測試 Boss 的進行中攻擊／蓄力／連續攻擊階段；取消尚未執行的段落，不解除整體 Team Skill 暫停、不刪除已發射物。未替正式哥布林配置能力。
- 每位技能讀取前一位結算後的敵人狀態；死亡後重新找合法目標。全體傷害使用當次目標快照，陣容失效時停止，避免重入事件把舊連鎖復活。完整 Wave 換波保護仍在第 4 關。
- 新 EnergyMage Prefab 繼承的舊 `Basic Break Power`／`Skill Break Power` 已歸零，避免額外觸發未列入規格的 Boss Break；原版法師 Prefab 保留。
- Unity 編譯成功；`FightPartySkillValidation` 通過 **83 項檢查**，前兩關重跑 **170 + 91 項**亦通過。
- 實際逐拍連鎖驗證：戰士→法師→射手 **14/0/0**；戰士→射手→法師 **8/4/4**。兩者法師結算後滿甲、HP 不變；另驗證法師前衛仍觸發補甲。
- 包含脆弱刷新／零傷害／死亡、集中優先序、模式固定、同發中斷增傷、重複中斷、無目標、停用清理、陣容重入取消與 HUD 狀態。結果在 `Temp/FightPartySkillValidation.result`，已檢視 `Temp/FightPartySkill-preview.png`。
- Preview Scene 渲染時出現 URP 重複 Global Light 訊息；預覽與開啟中的 FightScene3 光源並存，未改動正式場景光源。本輪不宣稱 Console 完全無訊息，也不將確定性測試視為實際 FMOD／手把驗證。

### 使用者操作驗收

1. Unity 選單 `Rhythm Hunter > Validate Party Checkpoint 3 - Skills and Fragile` 可重跑本關；亦可在編輯模式將 `run` 寫入 `Temp/FightPartySkillValidation.request`。
2. FightScene3 開戰後，以成功普通能力累積至 60 MP，再按 A／R。依序觀察戰士施加 `◆ x2`、下一位技能消耗脆弱與法師補滿 DEF；期間 Basic 停止。
3. 停止 Play Mode，再交換成戰士→射手→法師重新開戰，確認技能依新的位置順序施放。
4. 精確的兩組傷害表由三名各 100 HP、無額外防禦的測試敵人驗證。正式 FightScene3 仍使用既有測試 Boss，受其防禦、血量與階段影響，不保證畫面上直接重現傷害表。
5. 射手第三拍命中進行中的可中斷攻擊，確認提示 `INTERRUPTED`；待機命中不應顯示成功中斷。新敵人完整行為替身與飛行攻擊測試留待第 4 關。

### Inspector、掛點與主要檔案

- 三份 `LongswordWarrior`／`EnergyMage`／`SkeletonGunner` Prefab 已設定 `Skill Behavior`、`Skill Power`。戰士為 `BreakingSlash`／2；法師為 `EnergySurge`／2，`Conditional Skill Power` 4；射手為 `FormationBreaker`／4，條件強度 2。
- 三者 `Team Skill Performance Beats` 均為 2（限制 1–4），新增 `Team Skill Effect Beat Offset` 均為 1，即第二拍結算；偏移以 0 起算且不能超出演出長度。舊角色預設偏移 0 保留首拍結算。
- 法師 `Restore Armor After Team Skill` 開啟；射手 `Basic Interrupts Enemy` 開啟、`Successful Interrupt Damage Bonus` 1。敵人資料新增 `Attack Interruptible`，預設開啟。
- `FightCombatController.PartySkills.cs` 集中三個技能模式；`EnemyPause.cs` 提供有結果的中斷入口；`BasicAbilities.cs` 接入同發增傷；`TeamSkill.cs` 處理施放後被動與失效檢查。
- `FightUnitSlot.cs` 管理脆弱及傷害消耗；`FightCharacterDefinition.cs` 保存能力參數；`FightCombatController.cs` 接線與全體目標快照；`FightDefenseHud.cs`、`FightFormationPanel.cs` 更新狀態與說明。
- `Editor/FightPartySkillValidation.cs` 為本關獨立驗證入口。沒有另建音樂時鐘或大型被動框架。
- 仍使用既有 Cast／Impact 掛點和暫代劍士演出。正式脆弱特效掛點、法師能量波、骷髏準備／瞄準／燧發槍口動畫特效尚缺；目前以 HUD 圖示及文字提供可辨識狀態。

使用者已確認第 3 關及三人各 2 拍演出，授權 push。修訂後技能 110 項、編隊 170 項、普通能力 91 項檢查通過。跨 Wave 保留 MP 已是確認規則，實際換波、飛行攻擊凍結、強化第零拍與勝敗清理尚不能視為本關已完成。

## 4. 暫停、Wave 與生命週期（已確認）

使用者已確認本關並授權 push，接續第 5 關。

- 敵人行為／已發射攻擊戰鬥進度凍結；中斷不刪除已發射物。
- 強化第零拍；射手進度凍結，正常拍恢復判定。
- 清 Wave 回滿 DEF、保留 HP/MP；整場不能換位；剩餘 Team Skill 不跨 Wave。
- 勝敗、無目標、停用與重入清理；預告／連斬／蓄力／延遲命中／盾窗口測試替身。

### 本關實作

- 保留現有 FMOD 時鐘與 Team Skill 排程。普通戰鬥新增 `NormalBattleBeatCount`／`NormalBattleBeat`：演出及等待期間不推進，恢復普通戰鬥的第一拍才推進一次；重複回呼不多扣拍。未新增玩家增傷技能或另一個音樂時鐘，後續按拍強化應使用此入口。
- 既有敵人動畫、一般攻擊倒數及 Boss 狀態期限維持暫停；已生效格擋的拍點也平移，避免表演期間提前失效。表演中施加的 Boss Break 保留完整期限。
- `ScheduleEnemyHits` 提供敵人行為接線入口：從目前普通戰鬥拍數排定單段、連段、蓄力後命中或已發射的延遲攻擊。這些測試行為沒有配置到正式哥布林 Prefab。
- 可中斷行為取消尚未執行段落；`launchedProjectile` 命中排程獨立於發射者，中斷發射者不刪除已發射攻擊。命中須等現有晚側判定窗口與安全邊界關閉，同拍成功格擋適用該拍所有符合條件的命中。
- 既有 `FightAttackEffect`／`FightGuardEffect` 與新 `FightBattleEffect` 接入敵方表演暫停、全局暫停及換波清理。ParticleSystem 與 Animator 暫停後恢復原狀態；效果仍為視覺，不靠動畫事件或飛行到達時機造成傷害。
- `FightRosterManager.Additional Waves` 定義初始敵方之後的波次。換波只生成敵人；原英雄、HP、MP、編隊鎖定全部保留，DEF 在清波時補滿，射手準備與舊敵人排程／狀態／效果清空。
- 最後一名敵人死亡時標記清波；當前 Team Skill 在空戰場完成餘下演出及法師被動，再於完整演出結束的拍點換波。新敵人不承受前波剩餘技能，生成拍不安排敵人攻擊。
- FightScene3 共用 HP 歸零觸發一次敗北；清完最後一波觸發勝利。兩者停止後續輸入、技能與攻擊，清理暫停／排程／演出回呼；HUD 顯示結果。舊場景未啟用 Battle Preparation 的零 HP 實驗模式保留。

### Inspector 與操作驗收

1. 開啟 FightScene3，`FightDemoController > Fight Roster Manager > Enemy Prefabs` 仍是第一波。
2. `Additional Waves` 每個元素代表後續一波，`Enemies` 依原敵方槽位規則填入最多三名現有敵人 Prefab。沒有新增英雄欄位，不能藉換波更改隊形。
3. 原 FightScene3 敵人配置不變，未自行新增正式關卡或哥布林能力；未設定 Additional Waves 時，清完原敵人即勝利。可在編輯模式增加測試波次後進 Play Mode 驗收。
4. 蓄積部分 MP 並損失 DEF／HP，再清波：確認回滿 DEF，HP／MP 保留，角色不重生，編隊仍鎖住，HUD 的 Wave 編號更新。
5. 用 Team Skill 清波：待剩餘演出完成才出現下一波，下一波滿血。受到致命傷害後應顯示 DEFEAT，清完最後一波顯示 VICTORY；重開場景進行新戰鬥。
6. `Rhythm Hunter > Validate Party Checkpoint 4 - Lifecycle`，或編輯模式寫入 `Temp/FightPartyLifecycleValidation.request` 為 `run`，執行確定性測試。

測試用敗北開關：`FightDemoController > Fight Combat Controller > Battle Testing > End Battle On Zero Hp`。目前依使用者要求，FightScene3 設為關閉：HP 仍正常扣到 0，但不觸發敗北，普通能力、MP 與 Team Skill 可继续操作；勝利與換波規則不變。勾選後恢復零血量敗北。若在 0 HP 時重新勾選，下一次戰鬥檢查會觸發敗北；已結束的戰鬥不會因取消勾選自動復活，需要重開 Play Mode。非 Play Mode 修改並存場景才會保留設定。

### 主要檔案與限制

- Unity 編譯成功；第 4 關 **31 項檢查通過**，前 3 關回歸 **170 + 91 + 110 項通過**，合計 402 項。第 4 關結果在 `Temp/FightPartyLifecycleValidation.result`；涵蓋既有盾窗與 Break 期限、表演中的格擋保留，以及避免新敵人排程與舊普通攻擊同時出招。
- 新增 `FightCombatController.BattleLifecycle.cs`、`FightCombatController.EnemyActions.cs`、`FightBattleEffect.cs`，分別管理換波／終局、可中斷段落及延遲命中、視覺暫停與清理。
- 修改 RosterManager、CombatController、EnemyPause、TeamSkill／TeamUltimate 入口防護、UnitSlot／UnitEffects、AttackEffect／GuardEffect、DefenseHud／ScenePresenter。沒有修改角色或正式敵人 Prefab、FightScene3 場景配置、音樂、判定窗口及快取。
- 驗證沿用前關 Preview Scene 的中立測試敌人，以及既有測試 Boss 的固定護甲／Break 窗口；不等於完成下一階段正式敌人的技能設計。
- 正式外部 VFX 若自帶獨立移動／自毀腳本，仍需第 5 關逐資產檢查並接入暫停契約；本次保證內建效果及已接管的粒子／Animator，尚未驗證所有第三方特效。
- 本關測試未使用真實 FMOD 音訊／手把。Preview Scene 仍可能出現先前記錄的 URP 重複 Global Light 訊息，不能宣稱完整實機或正式美術驗收完成。

## 5. 完整回歸與演出（程式／自動驗證已確認，正式素材待補）

- 實際 FMOD、鍵盤／手把與早晚输入操作。
- 狀態／技能／編隊 UI、正式角色素材與特效掛點檢視。
- 更新失效的舊測試假設（5 HP、回血、自然回甲、零 HP 繼續玩）。
- 分列自動驗證、實際操作與未驗證項目，經確認後才 push。

### 已驗證

- 第 4 關已 push：`f4d3202`。前四關確定性檢查共 402 項通過。
- `FightScene3BeatValidation` 已移除 5 HP、自然回甲、回血及零血量繼續操作的舊斷言，改為當前 4 HP／戰士前衛 DEF 3／明確開戰。多 BPM 節拍 UI、敵我血甲顯示、開發面板與校準回歸通過。
- 舊 `FightTeamSkillValidation.request` 入口改為執行目前的技能／生命週期套件；不再用 30 MP、4+4+4 或超過 4 拍的舊技能資料驗證新角色。
- `FightTeamSkillLiveValidation` 使用真正的 FMOD 音樂播放及模擬 Input System 手把：確認 **121 BPM、60 MP、三人各 2 拍、14 傷害、法師滿甲而 HP 不變**，A／RB+A 路由、暫停與恢復通過。原有 Ultimate 僅做相容性回歸，沒有重新設計。
- `FightPartyInputLiveValidation` 使用真正的 FMOD 時鐘及模擬鍵盤／手把，依序送出 Q/W/E 與 X/Y/B，跨早晚窗口完成兩輪三拍射擊。實測十筆有效輸入偏差約 **-58.9 至 +78.0 ms**，共 **10 MP、4 傷害**；沒有以假判定結果代替普通輸入。
- 這兩份 live 測試只在 Play Mode 調整測試敵人的 HP／攻擊間隔，不保存场景，不寫入個人校準檔；结束移除模拟裝置、解除事件並恢復 Play Mode 起始場景。
- 已檢視防禦 HUD 預覽與 Team Skill 完成截圖。FightScene3 序列化初始提示仍寫著 Guard／Heal／Damage，已修正為 Front／Middle／Back，避免進場誤導玩家。

### 素材與掛點狀態

使用者已確認三位角色正式演出素材尚未完成，因此本關不宣稱正式美術整合完成，也不自行製作替代正式素材。

| 角色 | 已有 | 待素材完成後接入 |
| --- | --- | --- |
| 戰士 | 兩拍排程、第二拍斬擊、Cast／Impact 掛點 | 第一拍跳躍、第二拍斬擊正式圖組與效果 |
| 法師 | 一拍格擋、兩拍技能、第二拍爆破與補甲 | 正式外觀、舉手／爆破動畫、防護罩及能量波特效 |
| 射手 | 1/3、2/3 HUD、三拍普攻、兩拍技能 | 骷髏燧發槍外觀、準備／瞄準／射擊圖組與槍口掛點 |

- 三份資料 Prefab 仍共用 `HeroSwordsman_Visual`；角色專用 `Normal Ability Effect Prefab`／`Skill Effect Prefab` 目前未配置，使用既有後備演出。
- 替換素材後須逐項確認自帶移動、自毀、粒子和 Animator 是否遵守 `FightBattleEffect` 暫停；目前不存在「所有外部特效都已通過」的驗證結論。
- 脆弱目前有 HUD `◆ x2`，角色身上的正式狀態特效仍待掛點與素材。

### 重跑與尚待人工驗收

- 編輯模式執行 `Rhythm Hunter > Validate FightScene3 Equal Beats` 檢查 UI／校準；寫入 `Temp/FightTeamSkillValidation.request` 的 `run` 重跑技能／生命週期。
- 將 `run` 寫入 `Temp/FightTeamSkillLiveValidation.request` 或 `Temp/FightPartyInputLiveValidation.request`，一次只執行一個。工具會自動進入 Play Mode 並在結束後退出。
- 結果為對應 `.result`；截圖：`Temp/FightDefenseHud-preview.png`、`Temp/TeamSkill-complete.png`、`Temp/PartyInput-live.png`。
- 尚待：實體鍵盤／手把、耳機／喇叭延遲、真人聽拍與操作手感、正式動畫／特效素材，以及敵人正式配置後的難度平衡。
- Unity 開發環境的 FMOD Debug Overlay 會覆蓋部分左上敵人資訊；Preview Scene 仍有既有重複 Global Light 訊息。它們沒有讓本輪數值或 live 測試失敗，但截圖不作最終美術驗收。

第 5 關的程式與可自動化項目已完成，使用者已授權連同零血量敗北開關一起 push；正式素材掛接與人工操作驗收仍待後續，未列為全部完成。加入敗北開關後，生命週期套件通過 35 項檢查。
