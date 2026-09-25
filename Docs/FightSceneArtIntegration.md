# FightScene 美術循環整合

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
