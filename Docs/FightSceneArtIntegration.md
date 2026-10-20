# FightScene 美術循環整合

## FightScene3 角色生成位置

`FightDemoController → FightRosterManager` 指定角色 Prefab；執行時由 `FightUnitSlot` 生成在各位置的 `ActorRoot` 下，實例名稱為 `角色Prefab名稱 (Runtime)`。
玩家位置為 `HeroSlot_Front`、`HeroSlot_Middle`、`HeroSlot_Rear`；敵方對應 `EnemySlot_Front`、`EnemySlot_Middle`、`EnemySlot_Rear`。名稱代表位置，不代表職業，戰前交換仍沿用原生成點引用。
六組舊 `PrototypePlaceholder`（含 Body、Core、PrefabLabel）已移除。敵方暫時沒有指派 Prefab 的位置仍保留給編隊與後續 Wave，執行時由 `ClearCharacter()` 關閉。

## 角色逐張圖片拍數

`FightSpriteImportDefaults` 為 Assets 下首次匯入、尚無匯入設定的 Sprite 圖片設定 Single + Full Rect。既有 .meta、Reimport、已製作的 Multiple 圖集與非 Sprite 紋理不會被覆寫；新素材請等 Unity 編譯完成後再匯入。若連同 .meta 搬入，會保留來源設定，須在 Inspector 手動選 Single / Full Rect 並 Apply。

角色動畫使用原圖完整畫布與透明邊界，不以不透明像素自動裁切。`hero` 目前 10 張圖均為 776×528，Sprite Rect 統一為 (0,0,776,528)，Mesh Type 為 Full Rect，中心 Pivot；保留既有 Multiple 單切片的 GUID／internalID，避免 Prefab 引用失效。新加入的單張動畫可使用 Single + Full Rect；若沿用 Multiple，切片需涵蓋完整畫布，不使用 Automatic Slice 裁邊。

新增可選的 `Opening Idle`（開場清醒待機）與 `Drink`（喝酒）分類，均支援逐張拍數。原 `Idle` 作為酒醉後主要待機；Normal／TeamSkill 等使用酒醉版戰鬥素材。未提供新素材時不自動複製其他圖片。
FightScene3 開場不再試播靜音事件等待 Beat 回呼。Editor 依目前音樂 event GUID 讀取 ProjectFMOD 下對應事件 XML 的第一個 Tempo Marker；建置場景時將 BPM 與 event path 一併保存至 FmodBeatClock，執行檔不需讀取 FMOD 專案檔。來源缺失時明確回報；建置時不允許缺少開場 BPM 的場景悄悄使用預設值。正式音樂仍在喝酒結束後從頭播放，保留 AHDSR 開頭淡入。
Opening Idle 播放一輪，採全隊最長序列時間後同步開始 Drink。喝酒依每張圖片原本設定的拍數播放，不縮放、不截斷；喝完後回到清醒 Opening Idle 持續循環，開場至少 3 秒且全員喝完後才啟動音樂與戰鬥，同時切換酒醉主要 Idle。不要求第二輪清醒 Idle 播完，不停留喝酒末幀，也不提前切換酒醉 Idle。Minimum Entrance Seconds 可調整最低開場秒數；動畫超過此時間時不會被截斷。BPM 取自目前載入 Bank 的歌曲節拍；更改 FMOD Tempo Marker 後必須重建 Bank。
逐張拍數未開啟的序列仍按原 FPS；要以圖片拍數播放，須勾選「逐張設定拍數」。缺少 Opening Idle 或 Drink 的角色不會阻塞其他人。開場期間不接受普通能力或編隊操作，Wave 切換不重播。關閉 Auto Play Battle Entrance 保留手動編隊流程。
FMOD 淡入：音樂 Event 的 Master Track 音量加入 AHDSR，Initial=-∞、Attack 為所需淡入秒數、Peak/Sustain=0 dB、Hold/Decay=0，再 Build Bank。不要為了淡入移動音檔起點或 Tempo Marker。

LongswordWarrior 的 Normal 已接入 `hero/attack/attack_1～4`；第 4 張觸發 `Assets/FightDemo/Prefabs/VFX/SwordHit.prefab`，已指定於戰士的 Normal Ability Effect Prefab。生成位置為這次命中敵人的 ImpactEffectAnchor，即使該次傷害致死也不將特效轉移至其他敵人；下一次攻擊才重新尋找存活首位。Wave 切換後舊回呼失效。特效只負責畫面，不額外結算傷害。
TeamSkill 已接入 `hero/skill/attack_5～7`，暫不啟用 Warning、攻擊特效或舊流程傷害事件。兩組先保留 10 FPS；技能的實際傷害仍由戰鬥節拍結算。
敵人死亡不移動其他敵人的 Transform、不壓縮隊形；首位代表依前到後順序找到的第一個存活且有角色的 Slot。後續攻擊依此繼續往後找，不能跨 Wave 沿用舊動畫回呼。

角色視覺 Prefab 的 `FightCharacterCombatAnimator` 統一控制待機與戰鬥序列。

- Inspector 分為 `Idle`（待機）、`Normal`（普通能力）、`TeamSkill`（隊伍技能）、`GetHurt`（受傷）、`Die`（死亡）五個主分類；Guard、Heavy Attack、舊版 Light Attack 保留於其他動作區。
- 展開分類，每個「圖片 1／2／3」區塊內直接指定 Sprite、持續拍數，以及進入這張圖片時是否發出 Warning／攻擊特效／舊流程傷害事件。待機沒有戰鬥事件欄位。拍數模式關閉時仍沿用整套動畫的 FPS／循環速度。
- 使用「新增圖片」「上移」「下移」「刪除圖片」管理圖片；排序會同步搬移拍數與事件，刪除圖片會解除它的事件。每種事件沿用一套動畫一次的規則，勾選另一張會移動該事件，不會新增多次攻擊。
- 下方顯示單次正向總拍數，Ping Pong 的返回行程另外計算。尚無資料的分類可用「新增此類動畫」建立，新建序列預設使用拍數模式。
- Idle 拍數模式不使用 Cycles Per Beat，但保留 Ping Pong 與 Phase Offset。
- 顯示名稱保留原 enum 數值（NormalAttack=0、Skill=4、Hit=5、Death=6），既有 Prefab 不需搬移。FightScene3 普通攻擊改用 Normal 分類，舊場景仍保留 Light Attack。Die 分類只是提供素材設定，不新增死亡觸發流程。
- 支援 0.25、0.5、1、1.5 等拍數；最小 0.01 拍，缺少的項目預設 0.5 拍。新增圖片時 Inspector 驗證會補齊拍數欄位。
- 例如三張圖填 0.5、0.25、1.25，總長為 2 拍；120 BPM 時分別為 0.25、0.125、0.625 秒。
- 預設不啟用新模式，因此現有 Prefab 保持原 FPS／待機循環設定。戰鬥動畫沿用戰鬥節拍提供的 tempo 與暫停機制；沒有節拍來源時才使用 Fallback BPM。
- 事件內部保留原圖片索引資料，Inspector 以圖片內勾選框編輯；既有 Prefab 不需搬移。事件必須由播放端傳入對應 callback 才產生效果。隊伍模式的實際傷害與 Team Skill 總拍數仍由戰鬥系統決定，調整圖片不會自動修改技能時程；請使技能圖片總長及效果圖片起點配合角色技能的演出／效果拍數。
- 目前三名新角色共用視覺 Prefab。要分別設定圖片，先複製各自的視覺 Prefab 再接至各角色資料，避免修改共用資產影響三人。

## 閃現原因

`ArtTest` (`6b4c557`) 的 `LoopingBackgroundScroller` 使用左右副本接續背景。
合併時背景貼圖、匯入設定與捲動程式沒有被改寫；但是原本的副本建立方式只設定
Sprite 與共用材質，沒有明確綁定材質的 `_MainTex`。

在目前 Unity 6.3 / URP 環境的實際畫面中，原圖有材質參數區塊，動態副本沒有。
副本的 Sprite、Bounds、座標、enabled、isVisible 都可以正常，但實際仍不顯示。
因此原圖每次循環換位都像消失後重新生成。單純檢查物件數量與間距無法抓到此問題。

修正會保留來源的材質參數，並替每個副本明確指定自己的 Sprite texture。
這同時涵蓋背景、雲、石頭與草地切片，不依賴編輯器或特定電腦曾經建立過的材質狀態。

## 其他整合修正

- 循環間距固定；可捲動的圖層僅做垂直節拍縮放，避免水平接縫變動。
- 草地副本只複製視覺物件，跟隨來源姿態，不複製獨立的動畫行為。
- `Background_05` 石頭也納入統一節拍控制，避免遺留動畫在暫停後繼續縮放。
- 捲動與 BeatBounce 使用遊戲時間，可隨暫停停止。
- 尊重美術場景既有的全域光源；新建光源先設定種類再啟用，避免 URP 重複全域光源錯誤。

## 驗證方式

Unity 選單 **Rhythm Hunter → Validate Art Background Motion**。
測試透過 Play Mode 起始場景覆寫開啟 FightScene，結束後恢復使用者的場景設定，
不保存場景、Build Settings 或個人校正值。

測試除了檢查貼圖綁定、循環間距、草地同步與暫停，還會逐層把捲動位置設在循環邊界
前後，擷取實際畫面並比較像素。所有 11 層都必須通過，不能只以座標正確判定成功。
結果：`Temp/FightArtMotionValidation.result`；畫面：`Temp/ArtWrap-Background_XX-before.png`
與 `Temp/ArtWrap-Background_XX-after.png`。另以 FightScene3 的既有戰鬥測試確認玩法未回退。

本機 ArtTest 原捲動程式的對照不等於美術電腦上的整個執行環境；不應據此宣稱已驗證對方電腦。

本次反向驗證：暫時載入 ArtTest 原版捲動程式後，同一檢查在
`Background_08 (Loop Copy Left)` 偵測到缺少貼圖綁定而失敗；結果留於
`Temp/ArtTestTextureBindingRegression.result`。恢復修正版後，11 層循環邊界的
畫面像素比對皆通過。這確認檢查能攔截本次缺陷，而非只確認物件存在。
