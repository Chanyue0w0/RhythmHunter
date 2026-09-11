# FightDemo 程式整理規畫（待確認）

範圍：`FightSingleControl` 分支、`Assets/FightDemo`。以下非動畫項目尚未實作，需使用者確認後才開始。

## 1. 隊伍重建與戰鬥狀態重設（優先）

現況：`FightCombatController.HeroBeatSettings.Initialize` 只有在 `unitSlot == null` 時才接受新 slot；`OnRosterChanged` 重建清單時，舊 slot 若仍存在，就可能繼續被當成前排。清空或更換陣容時，也沒有一起清除待結算攻擊、防禦拍點等狀態。

方案：區分「綁定新陣容」與「校正現有數值」，明確更新三位英雄的 slot；建立統一的戰鬥重設流程，清除舊陣容的待結算動作。攻擊攜帶陣容版本或動作識別碼，忽略舊角色的延遲回呼。

驗收：戰鬥中替換前排、清空第一格、只保留一位英雄、連續重生；控制與傷害必須落在新陣容，舊攻擊不得影響新角色。

## 2. 輸入與事件訂閱生命週期（優先）

現況：`FightInputRouter.Configure` 換資產時只重新取得 action，沒有先解除旧 action 訂閱並綁定新 action。若缺少任一 action，`Subscribe(false)` 也會因 `IsConfigured` 為 false 而跳過清理。多個 Presenter／Controller 的 Configure 同樣假設依賴只在啟用前設定。

方案：保存實際已訂閱的來源；重新設定前完整解除舊來源，再依啟用狀態綁定新來源。逐一清理 action，避免部分配置失敗留下回呼。釐清共用 InputActionAsset 的 Enable／Disable 所有權。

驗收：執行中更換輸入資產、缺少單一 action、反覆啟用停用元件；每次輸入只能產生一次命令，舊資產不再回呼。

## 3. 統一角色戰鬥狀態與規則來源

現況：英雄魔力保存在 `HeroBeatSettings`，敵人魔力保存在 `FightUnitSlot`，而 slot 同樣提供英雄可存取的 `CurrentMana`。攻擊週期、技能資料也存在 Prefab 與 Controller fallback 兩套設定。模式由場景名稱 `FightScene2` 判定。

方案：Prefab 保存初始數值，單一角色 runtime state 保存 HP／魔力；Controller 負責決定動作與套用規則。用明確模式設定取代場景名稱判定，並保留目前兩個場景的既有行為與數值。

驗收：輕攻集魔、滿魔技能消耗、敵人只集魔不自動施法、血量開關、場景改名；UI、技能判定與角色狀態必須一致。

## 4. 縮小 Controller 與 Slot 職責，整理顯示與特效

現況：`FightCombatController` 同時處理規則、角色選擇與名稱搜尋式 UI 更新；`FightUnitSlot` 同時負責數值、生成、世界血條和特效生成。血條顯示會搜尋全場物件，特效每次建立／銷毀。

方案：先將 UI 引用明確綁定到 Presenter，依陣容變化更新快取；將特效生成抽成專用元件。物件池等效能調整先量測再決定，避免只為抽象而增加架構。拆分時保留既有動畫影格回呼與戰鬥結果。

驗收：更名 UI 物件不影響顯示；少於三人的陣容、反覆切換血量顯示和連續攻擊均正常；比較整理前後的特效數量與配置成本。

## 建議順序

先處理 1、2 的可重現生命週期問題，再進行 3 的資料整理，最後執行 4。每階段分開檢查與提交；不擴展至其他 Demo，也不改動遊戲平衡。
