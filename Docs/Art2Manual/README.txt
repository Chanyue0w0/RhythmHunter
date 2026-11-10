Art2_FightScene 手冊來源
日期：2026-11-10

文件輸出：output/pdf/Art2_FightScene_Manual_zh-TW.pdf
場景快照：output/pdf/Art2_FightScene_snapshot.json

manual_pages.json 是繁體中文章節內容。
build_manual.py 讀取專案現況，重建 PDF、書籤、目錄與場景設定附錄。
check_manual.py 驗證文字、字型、書籤與頁面，建立供視覺檢查的聯絡表。

依賴：Python 3、reportlab、pypdf、Pillow；Windows 微軟正黑體 msjh.ttc / msjhbd.ttc。
視覺檢查另需 Poppler pdftoppm。這些不是 Unity runtime 相依。

從專案根執行：
python Docs/Art2Manual/build_manual.py
pdftoppm -scale-to 1100 -png output/pdf/Art2_FightScene_Manual_zh-TW.pdf tmp/pdfs/final
python Docs/Art2Manual/check_manual.py

重建會讀取當時已儲存的場景與 Prefab，無法讀取 Unity 尚未存檔的修改。
文字章節須人工核對；重建程式不會自動把新企劃改寫進正文。
不修改 Unity 場景、Prefab、材質或腳本。無自動 commit / push。
