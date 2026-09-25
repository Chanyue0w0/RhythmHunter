# FightScene3 正式美術與 Prefab 設定

## 場景與戰鬥資料

開啟 `Assets/FightDemo/Scenes/FightScene3.unity`。保留 EqualBeat 戰鬥、原輸入、敵人血量、開發者數值介面、暫停與個人校準讀取。音樂仍由原本唯一的 FMOD Beat Clock 播放 `event:/Ritual Slam _120_3`；環境與角色接收其戰鬥拍點事件，不另設 BPM。

聖騎士／吟遊詩人／法師仍是三個獨立角色資料：HP 3／1／1、DEF 3／1／1，基本能力與技能沿用原設定。三人共用正式勇者劍士外觀。

## 角色 Prefab

資料夾：`Assets/FightDemo/Prefabs/ArtBattle/`。

| Prefab | 用途 |
| --- | --- |
| Swordsman_Paladin | 聖騎士資料＋勇者劍士外觀 |
| Swordsman_Bard | 吟遊詩人資料＋勇者劍士外觀 |
| Swordsman_Mage | 法師資料＋勇者劍士外觀 |
| Goblin_Giant | 正式巨型哥布林，可放入敵人陣容 |
| Goblin_Killer | 正式刺客哥布林，可放入敵人陣容 |
| HeroSwordsman_Visual | 三職業共用的外觀、待機和動作圖組 |
| GoblinGiant_Visual / GoblinKiller_Visual | 各哥布林共用外觀 |

選取 `FightDemoController`，在 **Fight Roster Manager** 的 `Hero Prefabs`／`Enemy Prefabs` 指定角色資料 Prefab。不要把 `_Visual` Prefab 放進陣容欄位。

陣容目前保留整合前的空槽配置與一名敵人；兩種正式哥布林都已備妥。敵方數值以整合前已配置的敵人為基礎，尚未另外調整兩種哥布林的平衡。

英雄陣列順序是 **0＝首位、1＝中位、2＝末位**。敵方沿用原架構：目前 **2＝前方 GoblinShield 槽、1＝中間 GoblinMercenary 槽、0＝後方 GoblinMage 槽**，戰鬥以 Slot Index 由大至小選擇存活敵人。實際生成位置由同一元件對應的 `Hero Spawn Slots`／`Enemy Spawn Slots` 參照決定；槽位的舊職業名稱不限制能放入哪個 Prefab。

調整數值：開啟上述角色 Prefab 的 **Fight Character Definition**。

調整外觀尺寸／圖組：開啟對應 `_Visual` Prefab，選 `Visual` 子物件，修改 Transform Scale 或 **Fight Character Combat Animator**。目前正式素材只有三張待機圖，動作圖組先共用這套圖，配合原有戰鬥特效；各動作可獨立替換新圖。傷害仍由戰鬥判定處理。

## 調整生成位置

1. 停止 Play Mode，在 Hierarchy 展開 `BattlefieldWorld`。
2. 選 `HeroSlot_Paladin`、`HeroSlot_Bard`、`HeroSlot_Mage`，或三個 `EnemySlot_...`。
3. 用 Move 工具拖曳，或修改槽位的 **Transform Position X／Y**；槽位位置代表腳底，角色、血條和特效會一起移動。
4. Scene 視窗開啟 **Gizmos**，可以看到 Spawn 圓圈與槽位名稱，包括目前空著的槽位。
5. 儲存場景後再 Play。執行期間修改的場景位置通常不會保留。

只想微調角色、不移動血條時，可改槽位下 `ActorRoot (Assign Prefab Here)` 的 Local Position，或 Fight Unit Slot 的 `Actor Local Offset`。角色特效發射點在角色資料 Prefab 的 `VFX_CastAnchor`／`VFX_ImpactAnchor`。

## 環境動態與光影

Hierarchy：`BattlefieldWorld > ForestEnvironment`，Inspector：**Fight Environment Controller**。

| 欄位 | 效果 |
| --- | --- |
| Motion Enabled | 環境動態總開關；關閉時捲動停在原位、節拍縮放回到基準 |
| Scrolling Enabled | 單獨停止背景平移，保留節拍動畫 |
| Direction | Right 向右／Left 向左；切換保留當前位置 |
| Speed Multiplier | 全局速度倍率，0 停止平移，1 為原美術速度 |
| Beat Pulse Enabled | 單獨開關環境節拍縮放 |
| Pulse Strength | 節拍縮放強度，1 為原美術強度 |
| Layers | 各背景層的捲動開關、速度倍率及反向設定 |
| Pulses | 明確綁定的草地、石頭、背景與角色陰影節拍設定 |

各背景的基礎速度仍在其 **Looping Background Scroller > Units Per Second**；最終速度是基礎速度 × 全局倍率 × 該層倍率。操作環境開關不會暫停戰鬥或音樂。Start／Esc／F8 的遊戲暫停則會一起凍結音樂、角色與環境。

環境 Prefab 是 `ForestEnvironment.prefab`。其下 `Warm Sun Light 2D` 控制主光，`Warm Window Light` 控制補光，`Natural Light Post Processing` 使用原美術 Volume Profile。正式角色共用 `BattleCharacterLit.mat`，陣容重新生成後仍會受光。

背景保留先前循環修復：固定接縫週期、草地副本只複製畫面、明確 Sprite 貼圖綁定。不要用停用／重啟各 Scroller 元件來代替環境控制開關，因為 Scroller 的原始生命週期會重設捲動位置。

## 驗證入口

- `Rhythm Hunter > Validate FightScene3 Equal Beats`：數值、護甲、零血量輸入、陣形、HUD 與校準回歸。
- `Temp/FightArtMotionValidation.request` 寫入 `FightScene3`：整合場景 Play Mode、FMOD 121.15 BPM、正式角色外觀、重新生成受光、11 層接縫影像比較、暫停與方向開關。
- 同一 request 寫入 `run`：驗證原美術 FightScene。

整合工具只供首次遷移使用，偵測到已有環境 Prefab 時會拒絕覆寫，以保留之後的手動美術調整。
