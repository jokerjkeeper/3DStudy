# StormLab 渲染技術筆記

StormLab 在同一套 Unity 6.3 URP 裡使用寫實材質、卡通著色、像素角色和電弧特效。這份筆記從場景裡能看見的結果連到材質、貼圖、Shader 和渲染設定，方便逐項比較。

## PBR、NPR、URP 與 SDF

| 名稱 | 作用 | 在 StormLab 的例子 |
| --- | --- | --- |
| PBR（Physically Based Rendering） | 用底色、金屬度、粗糙度或光滑度等參數描述表面如何反射光。 | 城市 GLB、Ember 的 glTF 材質，以及測試場中使用 URP/Lit 的金屬、地面和其他模型部件。 |
| NPR（Non-Photorealistic Rendering） | 為了美術風格改寫受光規則，例如把明暗分成色帶、用固定色調表現陰影。這裡把需求中的 NBR 理解為 NPR。 | Perlica 的卡通臉部陰影、髮絲高光和 Lantern Quay 的像素角色。 |
| URP（Universal Render Pipeline） | 決定場景如何繪製，包括燈光、陰影、深度、不透明畫面拷貝、透明物和後處理。 | PBR 與 NPR 材質都在 URP 上運作。專案使用 URP 17.3.0、Linear 色彩空間和 Forward Renderer。 |
| SDF（Signed Distance Field，有號距離場） | 用數值場表達邊界。這裡用的是角色臉部專用的預先製作貼圖，由光向決定在哪個值切出明暗邊界。 | Perlica 臉部取樣原始 SDF4，不同來光方向會把陰影推向另一側。 |

## PBR 材質與模型交付

| 主題 | 專案位置與觀察方式 |
| --- | --- |
| URP/Lit 與材質反射 | `Assets/StormLab/Generated`、`Grove` 和 `Battle` 有金屬、布料、石材與發光材質。選材質看 Inspector 的 Base Map、Metallic、Smoothness、Normal Map 和 Emission，再在同一盞燈下比較。 |
| glTF PBR | 城市場景和 Ember 都由 glTFast 6.21.0 匯入 GLB。glTF 的 metallic-roughness 材質記錄底色、金屬度、粗糙度、法線、遮蔽或自發光所用的圖片與參數。Ember 的 GLB 實際內嵌 12 張圖，匯入時會產生 mipmap。 |
| glTF、GLB、FBX 和 PMX | `.gltf` 常搭配外部 `.bin` 和圖片。GLB 能把相同的資料封裝成單檔，也允許外部引用。Perlica 的原始 Blender 檔封裝了圖片，但匯出的 FBX 有材質槽而沒有貼圖參照，因此 Unity 材質是在 `Assets/model/Perlica-Materials` 重新綁定。PMX 是 MMD 資產交換背景，通常另外附圖並用路徑引用。 |
| 色彩空間與取樣 | 底色與自發光圖通常走 sRGB 轉換，法線與一般資料貼圖依各自編碼設定。Perlica 的 SDF4 依原 Blender 設定保留 sRGB 取樣，運算使用解碼後的 RGB。不要在調閾值的同時切換這張圖的 sRGB，否則會一起改變場值。 |

參考 [Unity URP Lit 材質](https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/built-in/shaders-in-universalrp/reference/lit-shader) · [Khronos glTF 2.0](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html) · [MMD Tools](https://github.com/MMD-Blender/blender_mmd_tools)

## Perlica 臉部和頭髮

1. **臉部入口**是 `Assets/model/Perlica-Materials/face.perlica.mat`，使用 `Assets/StormLab/Shaders/PerlicaToon.shader` 的 Face SDF 分支。底色顯示皮膚，SDF4 決定哪一塊皮膚使用陰影色。眼白、虹膜、睫毛和嘴有獨立材質。
2. **光向和場值**。Shader 把 URP 主光投影到頭部左右與前方，依來光側取樣原 UV 或鏡像 UV。原 Blender 貼圖的解碼為 `B + G × R`。Shader 比較這個場值和光向閾值，再用帶像素寬度的平滑過渡混合亮面與陰影。城市的 `CityTargetMotion` 依蒙皮綁定位姿，把臉部朝向更新到當前頭骨。
3. **觀察與調整**。在 CyberpunkCity 的 Play Mode 用右鍵拖曳轉向、WASD 靠近、Q/E 調高度。按 F8 看黑白遮罩，白色為亮面，黑色為陰影，畫面上方會顯示遮罩模式。正常材質用低對比膚色陰影和較寬的過渡。Inspector 的 **Face Shadow Bias** 調覆蓋範圍，**Face Shadow Softness** 調過渡，**Face Shadow Tint** 調陰影色，**Face Preview** 可看原始距離場。按 T 進入燈光模式，選 **Twilight key** 後用 A / D 轉動主光，可以直接看陰影換邊。只轉鏡頭會改變觀看角度，主光相對頭部的方向仍由燈光與頭部姿勢決定。
4. **髮絲高光**使用同一 Shader 的頭髮分支。它從視角空間法線取樣 `hair_s.png` 球面貼圖，再結合 `T_actor_pelica_hair_01_P` 的 G 通道和 **Hair Highlight Strength**。移動鏡頭時高光會跟著觀看方向變化。

參考 [Unity 自訂 URP Shader 的光照方法](https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/shaders/writing-custom-shaders-urp/use-built-in-shader-methods/lighting)

## URP、光照與畫面品質

| 技術 | 專案怎麼用 |
| --- | --- |
| Forward、線性色彩與 HDR | `Generated/StormURP.asset` 開啟 HDR、不透明貼圖及深度貼圖。Forward Renderer 設定在 `Generated/ForwardRenderer.asset`。城市用冷色方向光、青色與洋紅色局部補光、線性霧和實時主光陰影。 |
| Volume 後處理 | 城市 `City Lighting` 使用 Neutral 色調映射和 Bloom，室內測試場使用 ACES 與 Bloom，Lantern Quay 的 `Quay Lighting` 也有景深。Bloom 把超過亮度門檻的能量擴成光暈。測試面板可切換 HDR bloom。 |
| SMAA、Render Scale 和取樣 | 城市鏡頭使用 SMAA，URP Render Scale 為 1，MSAA 關閉。Game 視窗的 Scale 是編輯器預覽倍率。遠處角色的細節用真正的鏡頭位移查看。Lantern Quay 的像素角色使用 Point 取樣與 alpha clipping，保留有意設計的像素邊緣。 |
| DBuffer Decal | URP Renderer Feature 讓 `DecalProjector` 把焦痕投影到命中表面。由 `ImpactFeedback.cs` 重複使用投影器，命中可動物體時焦痕會跟著走。 |
| Renderer Feature 與 Render Graph | `Grove/BodyCurrent/BodyCurrentFeature.cs` 在不透明繪製後，將角色遮罩及 Fresnel 值寫入 R8G8 遮罩，再用螢幕空間邊緣取樣和加法混合畫出輪廓。此通道僅在有啟用貼身電流的 Game 鏡頭執行，並使用單重取樣深度附件。 |

參考 [Unity URP Render Graph](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/universal-render-pipeline/customizing-urp/render-graph) · [Unity URP 抗鋸齒](https://docs.unity.com/en-us/engine/6000.0/manual/render-pipelines/universal-render-pipeline/anti-aliasing)

## 電弧、折射與動作

| 技術 | 專案檔案與比較方式 |
| --- | --- |
| Shader Graph 和 HLSL | `Shaders/Graphs/Arc Erosion.shadergraph` 與 `Shaders/Arc.shader` 提供電弧的噪聲侵蝕、HDR 白芯和色暈。按 3 比較兩種實作。 |
| 螢幕空間折射 | `Shaders/Graphs/Shockwave Refraction.shadergraph` 與 `Shaders/Shockwave.shader` 用圓環波前偏移螢幕 UV，取樣 URP Opaque Texture。按 4 比較，觀察同一條路面標線在波前通過時的扭曲。 |
| Particle System、線段與拖尾 | `HandCharge.cs` 控制向掌心回流的 Particle System 和分岔 Line Renderer。`ArcFlash.cs` 生成主弧和分支，`ImpactFeedback.cs` 用 Trail Renderer 畫沿方向散出的火花。 |
| 放電亮芯、光圈與餘火 | `DischargeLayers.cs` 備妥固定容量的動態網格，依階段繪製面向鏡頭或貼合命中面的四邊形。`Resources/StormDischarge.shader` 用 UV 程序形狀和加法混合畫光斑、短光線和破碎光圈，配合 Bloom。 |
| 流程、角色姿勢與命中 | `VfxFlow.cs` 儲存節點和圖層時間，`ChargeSequence.cs` 播放蓄力、飛行、命中。`CityDuelPose.cs` 驅動 Ember 的手臂及施法姿勢，`CityTargetMotion.cs` 驅動 Perlica 的蒙皮骨架。命中結合射線偵測、實時閃光、焦痕、物理推力及鏡頭晃動。按 F 播放完整流程。 |
