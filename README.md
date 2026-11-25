# 3DStudy

Unity 3D 測試專案。每個專案保留自己的 Assets、Packages 和 ProjectSettings，可用 Unity Hub 分別加入。

| 專案 | 內容 |
| --- | --- |
| [StormLab](StormLab/) | URP 特效測試場。Cyberpunk City 可獨立調整 Perlica 的臉部 SDF 與瀏海投影。 |
| [NoggiSdfUrpDemo](NoggiSdfUrpDemo/) | 臉部 SDF、即時瀏海投影和反殼描邊模板，附 Suzanne 與 Perlica 測試場景。 |
| [BodyCurrentDemo](BodyCurrentDemo/) | 角色表面電流與接地效果測試。 |
| [StormLabGraphVerification](StormLabGraphVerification/) | StormLab 的 Shader Graph 和特效比較、驗證素材。 |

## 技術筆記與操作說明

- [StormLab 房屋渲染效能修訂報告](StormLab/revision/HouseBenchmark.md) — Unity Play 操作、房屋合批、透明遮擋與 2K／4K 比較。
- [StormLab 渲染技術筆記](StormLab/Rendering.md) — PBR、NPR、URP、臉部 SDF，以及電弧和折射的做法。
- [StormLab 臉部 SDF 與瀏海投影操作說明](StormLab/StormLab-Face-Shadow-Guide.md) — Play Mode 的陰影開關、強度與柔邊滑桿、Inspector 設定和 Windows 建置。
- [Noggi SDF 模板使用說明](NoggiSdfUrpDemo/Assets/NoggiSdfTemplate/%E4%BD%BF%E7%94%A8%E8%AA%AA%E6%98%8E.md) — 將臉部 SDF、UV、反殼描邊和瀏海投影套到自己的角色 FBX。

## 加入後續測試專案

把每個 Unity 專案放成根目錄下的一個資料夾，提交 Assets（含 .meta）、Packages 和 ProjectSettings。Unity 快取、機器本機設定、Player 建置和發行 ZIP 依根目錄的忽略規則留在本地。

StormLab 的城市 GLB 透過 Git LFS 下載。新環境請先安裝 Git LFS。若已下載倉庫但模型仍是指標檔，在倉庫根目錄執行 git lfs pull。

