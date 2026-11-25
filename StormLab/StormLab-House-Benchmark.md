# StormLab 房屋渲染效能測試

2026-11-23。Unity 6000.3.15f1、URP、Windows Development Player、Direct3D 12、RTX 3060、i5-12400。渲染目標為 1920 × 1080。每組暖機 180 幀，再取 240 幀中位數。房屋使用相同 4K 材質，每棟 15,948 個三角形。拆件組包含屋頂、牆體、木構飾條和窗組。

## 這次要測什麼

同一款房子建到 64／256 棟時，比較整棟單一 Mesh 與拆件後的渲染提交量，再分別測 Static Batching、Forward+ 和 GPU Resident Drawer。另一部分比較 2K 與 4K 貼圖的記憶體用量和一般距離／近拍的畫質。

- **Static Batching（A3）** 用 `StaticBatchingUtility.Combine` 將不移動的拆件合批。A2 與 A3 的差值用來觀察這一步減少多少 Draw Calls。
- **GPU Instancing** 讓多份相同 Mesh 與材質共用一批繪製。此次沒有為材質的 **Enable GPU Instancing** 或手動呼叫 `Graphics.RenderMeshInstanced` 另設測組。
- **GPU Resident Drawer（A5）** 是 Unity 6 URP 的自動實例化路徑。Unity 經由 `BatchRendererGroup` 為相容的 Mesh Renderer 批次提交圖形，適合重複使用相同屋頂、窗框等網格的場景。本測試比較 A4 和 A5 以觀察開啟它後的 Draw Calls 變化。 [Unity URP GPU Resident Drawer 說明](https://docs.unity3d.com/cn/current/Manual/urp/reduce-rendering-work-on-cpu.html)

## Draw Calls 與 SetPass

| 組別 | 配置 | 64 棟 Draw Calls | 256 棟 Draw Calls | 64／256 棟 SetPass |
| --- | --- | ---: | ---: | ---: |
| A1 | 整棟單一 Mesh，Forward | 130 | 512.5 | 5／5 |
| A2 | 每棟拆四件，Forward | 514 | 1,892 | 6／11 |
| A3 | 每棟拆四件，Forward + Static Batching | 57 | 178 | 6／11 |
| A4 | 每棟拆四件，Forward+ | 513 | 1,904.5 | 6／11 |
| A5 | 每棟拆四件，Forward+ + GPU Resident Drawer | 10 | 10 | 4／4 |

64 棟時，A3 相對 A2 減少 88.9% Draw Calls，且 256 個 Renderer 都回報屬於 Static Batch。256 棟時，A3 相對 A2 減少 90.6%。A5 相對同為 Forward+ 的 A4 分別減少 98.1% 和 99.5%，兩個規模皆回報 GPU Resident Drawer 已初始化。

各組幀時間中位數落在 16.663–16.671 ms。這輪可比較繪製提交量，無法據此量化 FPS 或 GPU 速度增益。256 棟時各組實際提交的三角形數也有差異，約 745 萬至 817 萬，解讀時間差時須考慮這點。

## Frame Debugger

64 棟 Development Player 的獨立擷取中，A2、A3 和 A4 的主光陰影顯示為 SRPBatch。A2 與 A4 的首個陰影事件各為 256 個 Draw Calls，A3 為 22 個。A5 的主光陰影與不透明物件事件都顯示為 HybridBatch。其中陰影事件包含 256 個 instances、4 個 Draw Calls。這些是個別擷取事件的數字，整幀的中位數請看上表。

## 2K 與 4K 貼圖

Base Color、Normal、Mask 都以 BC7 匯入。以下為三張貼圖加總。

| 版本 | 每張 mip 層數 | BC7 完整 mip 鏈 | Unity 回報 runtime size |
| --- | ---: | ---: | ---: |
| 原始／4K | 13 | 64.00 MiB | 64.13 MiB |
| 2K | 12 | 16.00 MiB | 16.13 MiB |

2K 的 BC7 mip 資料量約為 4K 的四分之一。一般鏡頭下兩版差異不明顯，近拍在瓦片、木紋與牆面細節較容易看出差別。完整比較圖為同構圖、同解析度的四張 PNG。

舊版執行檔輸出的 textures.csv 將 mip 索引傳入深度參數，導致 mip_payload_bytes 有誤。上表用每級 mip 的 BC7 區塊數重算。Unity 回報的 runtime size 沿用原始量測，與實際 GPU 駐留量是不同指標。

## 本機驗收檔案

- `Builds/HouseBenchmarkResults/performance-player-2026-11-23.csv` 是上表的 Development Player 原始資料，從當晚的 `player.log` 還原。
- `Builds/HouseBenchmarkResults/performance-editor-2026-11-24.csv` 保存 2026-11-24 的 Unity Editor 重跑資料。該次 A5 在 64 與 256 棟都回報 `grd_initialized=False`，應和 Development Player 的結果分開讀。
- 同一資料夾還有 `textures.csv`、四張貼圖比較圖，以及 A2–A5 的 Frame Debugger TSV。獨立執行檔位於 `Builds/HouseBenchmark/HouseBenchmark.exe`。

## 批次測試版的啟動方式

直接開啟 `HouseBenchmark.exe` 會進入自動量測。攝像機畫到 1920 × 1080 的離屏 RenderTexture，視窗在測試期間會顯示黑畫面。兩種房屋數量各測五組，每組暖機 180 幀並取樣 240 幀。以 60 FPS 計算，光是這些測量就約 70 秒。輸出結果後 Player 會自動退出。結果目錄中的 `complete.txt` 是完成標記。

預設相對輸出路徑是 `Builds/HouseBenchmarkResults`，以啟動程式時的工作目錄為準。從執行檔資料夾啟動會寫到 `Builds/HouseBenchmark/Builds/HouseBenchmarkResults`。2026-11-24 15:48 的 Development Player 重跑已在此目錄寫出十筆結果與完成標記。

從 StormLab 專案根目錄下指令。在 PowerShell 執行以下命令可停在 A3 的 64 棟預覽，方便查看畫面或 Frame Debugger。

```powershell
.\Builds\HouseBenchmark\HouseBenchmark.exe -house-hold 3 64
```

重新量測時可指定獨立目錄保留每次輸出。

```powershell
.\Builds\HouseBenchmark\HouseBenchmark.exe -house-output "D:\unity\3D\StormLab\Builds\HouseBenchmarkResults\Player-New"
```
