# Storm Lab 雷電特效實驗場

使用 Unity **6000.3.15f1**、URP **17.3.0** 和 Linear 色彩空間。場景為原創的室內放電測試台。從 Unity Hub 加入此專案，開啟 `Assets/StormLab/StormLab.unity`，按 **Play**。也可以從 Unity 選單選 **Storm Lab → Open test range**。

| 操作 | 用途 |
| --- | --- |
| 滑鼠左鍵／按住 | 發射到滑鼠指向的表面 |
| Space／按住 | 對測試台上的金屬靶發射 |
| 1 / 2 | 切換青藍、紅色電弧 |
| 3 / 4 | 分別切換電弧、衝擊波的 Shader Graph 與 HLSL |
| C | 開啟或停止手部蓄電預覽 |
| F | 播放蓄電 → 飛行 → 命中 |
| R | 停止蓄電流程，重置測試方塊、焦痕與射擊計數 |
| 右側 ON/OFF | 分別比較 Bloom、分支、折射、物理推力、鏡頭震動和焦痕 |

## 手部蓄電與三段流程

在左側 **Hand Charge** 面板按 **Preview [C]**，可以持續觀察白色分岔與向掌心聚集的火花。分別切換 **White branch arcs** 和 **Particle sparks**，每個開關只控制手部的對應效果，也適用於完整流程。

按 **Play chain [F]** 後，面板會依次顯示 **Charge → Travel → Impact**。蓄電時，電弧與火花同時播放。放電後，光點與拖尾沿掌心到測試靶的路徑飛行，到靶後觸發原有的命中特效。流程啟動時鎖定測試靶上的命中點。按 **R** 可中止流程並清除畫面中的效果。

| 想編輯的內容 | Unity 位置 |
| --- | --- |
| 電弧的長度與寬度 | Hierarchy → Discharge emitter → Hand Charge Fixture；選取 Hand Charge 元件。 |
| 侵蝕、噪點速度和白芯 | Project → Charge → **White Charge Ribbons** 材質。它使用 **Arc Erosion** Shader Graph，也供飛行拖尾使用。 |
| 火花的數量、方向與亮度 | Hand Charge Fixture → Open right gauntlet → Palm FX anchor → **Inward charge sparks**。調 Particle System 的 Emission、Shape、Start Speed 和 Color over Lifetime。球形發射器搭配負的 Start Speed 會把粒子往掌心帶。 |
| 各階段時間 | Project → Charge → **Hand Charge Sequence**。預設蓄電 1.45 秒、飛行 0.28 秒、命中階段 0.72 秒。 |

逕行打靶可繼續使用左鍵或 Space。測試機械手時，先用 Preview 比較每層，再完整播放，檢查出手和命中是否接得上。
## 從哪裡改參數

| 想研究的效果 | 編輯位置 | 可以先比較的參數 |
| --- | --- | --- |
| 主電弧的形狀 | Hierarchy → Arc generator | Vertices、Amplitude、Main Width。主弧預設 20 點，兩端固定。 |
| 分支與火花拖尾 | Arc generator、Impact feedback | 調整 Branches 和 Branch Lifetime。命中火花使用面向攝影機的 Trail Renderer，尾端寬度逐漸收細。 |
| 螢幕空間折射 | Impact feedback、Shaders/Shockwave.shader | Refraction 是螢幕 UV 的位移量。對準地板接縫或靶板邊緣，反覆開關 Scene refraction 比較。 |
| 物理與鏡頭 | Impact feedback | Radius、Explosion Impulse、Shake Amplitude。按 R 恢復靶台周圍的測試方塊。 |
| HDR 與泛光 | Global HDR volume、Shaders/Arc.shader | Bloom Threshold、Intensity；比較右側 HDR bloom 開關。 |
| 焦痕 | Generated/Scorch.png、Generated/ForwardRenderer.asset | URP DBuffer Decal Projector。打中可動方塊時，投影器會跟著剛體移動。 |

## 編輯與比較 Shader Graph

兩個效果預設使用 Shader Graph。遊戲右側的 **Arc shader [3]** 和 **Wave shader [4]** 可獨立切換 Graph 或原版 HLSL。按住 Space 反覆打同一個靶；比較折射時，可以把準星對到靶板邊緣或地板接縫。

從 Project 面板開啟 `Assets/StormLab/Shaders/Graphs`。雙擊 `.shadergraph` 開節點圖，選取同名 `.mat` 可在 Inspector 邊玩邊改數值。

| Graph | 在節點圖可追蹤的流程 | 材質上可即時調整的參數 |
| --- | --- | --- |
| `Arc Erosion` | UV0 X → 正反向的 Simple Noise → 噪點相乘 → 和 UV0 Y 的橫向遮罩合成 → Smoothstep 侵蝕 → HDR 白芯與色暈。 | **Erosion Bias** 越高越早碎裂，**Erosion Softness** 控制臨界過渡。另有快、慢兩層的 **Density / Speed** 和 **White Core Intensity**。 |
| `Shockwave Refraction` | UV0 → 圓環波前 → 正弦波帶 → 偏移 Screen Position → Scene Color；再疊上發光輪廓。 | **Refraction Gain** 設為 0 可保留輪廓並關掉 UV 位移。也可調 **Distortion Band Width**、**Ripple Frequency**、**Rim Glow**。 |

Blackboard 上有 `(script)` 的參數由場景腳本逐發或逐幀寫入，例如電弧生命週期 `_Erosion`、衝擊波 `_Age` 與基礎 `_Refraction`。調整材質時優先使用表中的參數。Graph 版電弧使用 Shader Graph 的 Simple Noise，和 HLSL 版的噪點分布會有些差異。火花拖尾使用原版電弧材質。

折射依賴 URP 的 **Opaque Texture**。衝擊波在透明電弧之前繪製，Scene Color 取樣的是背景中已繪製的不透明場景。
