# CMO_TW

《Command: Modern Operations（現代行動：指揮）》完整正體中文化修補程式與本地化工程。

基於 .NET `AppDomainManager` 機制加載，不修改遊戲 `Command.exe` 主程式檔案。

---

<!-- downloads:begin -->
## 下载

| 通道 | 适合谁 | 版本 | 下载 |
|---|---|---|---|
| **稳定版** | 想安稳玩、不想折腾 | **`4.2.1`**（维护者人工确认） | [安装器](https://github.com/mj8724/CMO_CN/releases/download/v4.2.1/CMO_Chinese_Setup_v4.2.1.exe) · [绿色包](https://github.com/mj8724/CMO_CN/releases/download/v4.2.1/CMO_Chinese_Pack_4.2.1.zip) |
| **测试版** | 想第一时间拿到最新修复 | **`4.2.6`**（本页登记版本） | [安装器](https://github.com/mj8724/CMO_CN/releases/download/v4.2.6/CMO_Chinese_Setup_v4.2.6.exe) · [绿色包](https://github.com/mj8724/CMO_CN/releases/download/v4.2.6/CMO_Chinese_Pack_4.2.6.zip) |

> **稳定版**链接固定指向 `v4.2.1`，只有维护者确认新版本稳定后才会提升。
> **测试版**链接固定指向本页标注的 `v4.2.6`，可能包含尚未充分验证的修复。
> 两个下载都绑定明确版本；最近一次发布与全部历史版本见 [Releases](https://github.com/mj8724/CMO_CN/releases)。
>
> 不确定选哪个？**选稳定版**。想帮忙验证修复，再用测试版。

### 绿色包首次启用（不是只解压即生效）

把 ZIP 解压到独立临时目录（不要先覆盖游戏目录），退出游戏后在该目录运行包内同版本安装器：

```powershell
& ".\CMO_Chinese_Setup_v4.2.6.exe" --install "<游戏根目录>" --silent
```

稳定版绿色包使用它对应的 `CMO_Chinese_Setup_v4.2.1.exe` 执行相同参数；历史 ZIP 若未附安装器，须从同一 tag 另行下载该版本安装器到临时目录，不要混用版本。
安装器统一部署内嵌载荷、合并目标自己的官方 runtime/bindingRedirects、保留首次 `.cmo_bak`/`.orig` 备份与玩家设置，并登记安装状态。
`--adopt "<游戏根目录>" --silent` 仅登记已有完整载荷，不代替首次 apply；不要改名/覆盖 `Command.exe.config.patch` 或其他旧备份。
<!-- downloads:end -->

---

## 版本與適配

- **適配的遊戲內部版本：`1900.24`** —— 即遊戲標題裏的 `Command: Modern Operations v1.10 - Build 1900.24`
  （倉庫根目錄 `GAME_BUILD` 是它的單一來源，`verify_all` 會校驗各處一致）。
- 本修補程式自身的版本號見倉庫根目錄 `VERSION`：安裝包檔名、安裝器標題、更新助手提示都會顯示它。
- **遊戲每次更新（構建號變化）都可能讓本修補程式失效**（遊戲會重新命名內部混淆成員、改動介面文本）。
  此時本倉庫**次版本號 +1、修訂位歸零**（例：4.1.8 → **4.2.0**）並重新適配；判斷是否匹配，就看上行的構建號與你遊戲標題裏的 Build 號是否一致。

---

## 包含內容

- **介面與選單**：主介面、頂部工具欄、全部 10 大主選單及多級子選單、右鍵選單。
- **業務視窗**：航空作業（Air Ops）、就緒飛機（Ready Aircraft）、作戰條令（RoE）、任務編輯器、戰鬥序列（OoB）、推演選項等。
- **裝備資料庫**：飛機、艦船、潛艦、地面設施、武器、感測器等 8 大類別的常規參數、戰術能力、工作頻段及武器掛載包線。
- **教程與戰情訊息**：涵蓋全部 45 個官方教程（飛行、多域打擊、對地打擊、水面、潛艦、反潛、電子戰、空中加油及馬克大叔教程），教程操作指引中的按鍵（F1~F12 等）與按鈕名稱已對齊。
- **地圖地名**：全球 321 處國家/海域及 4,287 座城市的地名正體中文化。

---

## 實機截圖

### 教程戰情訊息與就緒飛機視窗
![教程訊息與就緒飛機](evidence/live/tutorials/17_message_2_and_ready_aircraft_side_by_side.png)

### 航空作業 (Air Ops) 視窗
![航空作業](evidence/live/tutorials/08_air_ops_ramat_david_fixed.png)

### 資料庫檢視器（外掛武器與掛載包線）
![資料庫檢視器](evidence/live/tutorials/07_db_viewer_weapons_translated.png)

### 戰場主介面
![戰場主介面](evidence/live/tutorials/10_right_panel_flying_frame1.png)

---

## 安裝與使用

### 1. 使用安裝器（推薦）

下載發佈包中的 `CMO_Chinese_Setup.exe`：

- **圖形界面**：連按兩下運行，選擇遊戲安裝目錄，點擊「一鍵安裝正體中文化」。如需恢復英文原版，點擊「還原英文原版」。
- **命令列靜默安裝**：
  ```cmd
  CMO_Chinese_Setup.exe /install "遊戲目錄路徑"
  ```
- **命令列靜默卸載**：
  ```cmd
  CMO_Chinese_Setup.exe /uninstall "遊戲目錄路徑"
  ```

### 2. 手動安裝

將發佈包解壓後複製到遊戲根目錄（覆蓋同名檔案前請先備份原檔案）：
- `CMOLocalizer.dll` -> 遊戲根目錄
- `CMOLocalizer.json` -> 遊戲根目錄
- `Command.exe.config` -> 遊戲根目錄（新增了 AppDomainManager 引導設定）
- `DB/Templates/*.html` -> 覆蓋到 `DB/Templates/` 目錄
- `Symbols/Menu/*.png` -> 覆蓋到 `Symbols/Menu/` 目錄

---

## 從源碼構建

需要 Windows 環境，並安裝 .NET Framework 4.8 與 Python 3.10+。

```bash
# 1. 生成詞庫
python scripts/build_db_enums.py
python scripts/build_dict.py

# 2. 編譯引擎與安裝器
bash scripts/build.sh

# 3. 運行校驗門禁
python scripts/verify_all.py
```

編譯產物位於 `build/` 目錄下：
- `build/CMOLocalizer.dll`
- `build/CMO_Chinese_Setup.exe`

---

## 目錄結構

```text
├── src/
│   ├── engine/       # 本地化引擎源碼 (CMOLocalizer.cs)
│   └── installer/    # 一鍵安裝器源碼 (CMO_Chinese_Setup.cs)
├── dict/
│   ├── CMOLocalizer.json     # 運行時主詞庫
│   ├── glossary.json         # 核心軍語術語表
│   ├── brief_translations.json # 簡報與教程譯文庫
│   └── src/                  # 各模塊源詞庫 (選單/航空作業/資料庫枚舉等)
├── assets/           # 正體中文化後的資料庫 HTML 模板
├── scripts/          # 構建、打包、校驗與部署腳本
├── docs/             # 術語表、實測報告與變更記錄
└── evidence/live/    # 實機測試截圖
```

---

## 說明

本項目僅包含正體中文化修補程式相關源碼、詞庫與資源，不包含遊戲本體或任何商業檔案。請在支持正版遊戲的前提下使用。

## 版權與開放原始碼

本專案依據 MIT License 開放原始碼，原始專案倉庫為 [mj8724/CMO_CN](https://github.com/mj8724/CMO_CN)，本專案倉庫為 [NekoDream/CMO_TW](https://github.com/NekoDream/CMO_TW)。安裝器內建「版權與開放原始碼宣告」視窗，載明原作者倉庫與完整授權條款。

---

## 常見問題（FAQ）

**Q：Steam 校驗遊戲檔案後正體中文化不見了？**
A：Steam 的「驗證遊戲檔案完整性」或遊戲更新會還原它管理的檔案（含地圖/簡報/按鈕圖）。
重新運行一次 `CMO_Chinese_Setup.exe` 即可恢復；綠色包用戶重新解壓覆蓋同樣有效。

**Q：地圖/工具欄按鈕還是英文？**
A：請確認裝的是最新版安裝包。裝好後地圖標籤由引擎內置的本地中文瓦片服務實時生成，瀏覽任何區域都是繁體中文。

**Q：怎麼更新到新版本？**
A：本專案已精簡移除背景自動更新與常駐托盤，無任何後台遙測與常駐進程。需要更新時，直接下載最新版安裝包覆蓋安裝即可。

**Q：怎麼徹底還原英文？**
A：執行 `CMO_Chinese_Setup.exe` → 「一鍵還原原版英文」。還原會自動清除在地化設定並還原官方原版配置。

**Q：裝了新版後發現不合適，能退回上一版嗎？**
A：能。安裝/升級時會自動把安裝包緩存到本機，安裝器上的「回退到上一版本 vX」按鈕或
更新助手托盤選單的「回退到上一版本…」可以直接離線回退（回退目標必須是本地緩存過的版本）。
命令列等價寫法：

```
CMO_Chinese_Setup.exe --rollback "<游戏目录>" --silent
CMO_Chinese_Setup.exe --list-versions "<游戏目录>"      # 檢視已裝版本與本地緩存版本
```

**Q：綠色包（ZIP）怎麼用？裝了之後狀態顯示對嗎？**
A：把壓縮包裏的檔案解壓覆蓋到遊戲目錄即可。爲了讓安裝狀態、更新與回退都能正常工作，
解壓後請**至少運行一次同包的 `CMO_Chinese_Setup.exe` 或 `CMO_Updater.exe`**：它們會自動
**登記安裝狀態**（讀取實際部署的檔案並寫入清單，無需重新覆蓋檔案）；也可以手動執行：

```
CMO_Chinese_Setup.exe --adopt "<游戏目录>"
```

未登記也不影響正體中文化生效，只是安裝器會顯示「已安裝正體中文化修補程式（已登記當前狀態）」。

**Q：檢查更新時提示「發現可回退版本」是什麼意思？**
A：只有當你本機**已經緩存過**那個版本的安裝包時纔會出現該提示（用於回退）；
若你正在跑一個比線上更新（未發佈）的版本且本機沒有緩存，檢查更新會靜默處理，不會打擾你。
