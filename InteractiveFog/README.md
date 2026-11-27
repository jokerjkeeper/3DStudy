# InteractiveFog

Unity 6（6000.3.15f1）+ URP 17.3（RenderGraph）的可交互霧氣。參考 Godot 復刻《時之笛》霧氣的做法：2D Stable Fluids 驅動密度場，全螢幕 raymarch 畫出貼著地形的體積霧。

## 開啟

1. Unity Hub → Add → 選 `D:\unity\3D\InteractiveFog`，用 6000.3.15f1 開啟。
2. 第一次匯入完成後，`InteractiveFogSetup` 會自動執行：建立 `Assets/InteractiveFog/Settings/FogURP.asset`（含 InteractiveFog Renderer Feature），把它設到 Graphics 和所有 Quality 等級，並產生 `Assets/InteractiveFog/Demo/InteractiveFogDemo.unity`。
3. 打開 Demo 場景按 Play。如果自動設定沒有跑，從選單執行 **Tools → Interactive Fog → Setup URP + Build Demo Scene**。

操作：WASD／方向鍵移動、左鍵拖曳攪霧、滾輪縮放、V 切換 Stir、R 重置。

## 結構

| 檔案 | 作用 |
| --- | --- |
| `Shaders/FluidSim.compute` | Stable Fluids：外力 → 散度 → Jacobi 壓力 → 投影 → 平流；加上渦度約束（Stir）與 curl-noise 風場。網格 256²，RG/R Half。 |
| `Runtime/FogSimulation.cs` | 每幀派發 compute、管理 ping-pong RT，並把密度圖、地形高度圖、太陽方向等設成全域 shader 參數。 |
| `Runtime/FogInteractor.cs` | 掛在角色、技能、爆炸上即可推開霧。參數：半徑、外推、跟隨速度、挖空速度。最多 16 個。 |
| `Runtime/InteractiveFogFeature.cs` | URP RenderGraph Renderer Feature：讀相機顏色和深度，執行 raymarch 後寫回。 |
| `Shaders/InteractiveFogRaymarch.shader` | 霧層跟著 Terrain 高度圖起伏，厚度由流體密度決定，加 3D value noise 細節；Beer-Lambert 加兩點自陰影。 |
| `Demo/*` | 示範用的角色移動、跟隨相機、滑鼠攪霧與 IMGUI 參數面板。 |

## 接到自己的場景

1. 確認使用中的 URP Renderer 已加入 `InteractiveFogFeature`。
2. 場景放一個 `FogSimulation`，指定 `FluidSim.compute` 和 Terrain（沒有 Terrain 時用 Flat Ground Y）。模擬範圍以它的位置為中心，邊長是 `worldSize`。
3. 會推霧的物件加上 `FogInteractor`。

## 效能注意（Steam Deck）

- 模擬成本很小（256² 約 30 次 dispatch）。主要成本在全解析度 raymarch：每個像素最多 `raySteps` 步，每步 3 次密度取樣。
- 下一步可以做的優化：霧改成半解析度渲染再做 bilateral upsample，加上 temporal 累積後，`raySteps` 可以降到 24 左右；自陰影改成預先算好的 2D 光照圖。
