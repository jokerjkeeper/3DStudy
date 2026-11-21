# Noggi SDF · Unity URP 範例

使用 Unity 6000.3.15f1、URP 17.3.0 和 Unity Toon Shader 0.16.1-preview。這是獨立專案。

## 在 Unity 測試

1. 用 Unity Hub 加入此資料夾，選擇 Unity 6000.3.15f1。
2. 開啟 Assets/NoggiDemo/NoggiLighting.unity，按 Play。
3. 按 Left、Front、Right 或拉動 Main light azimuth。Sweep main light 會持續轉動光源。
4. 切換 Inverted-hull outlines，可同時比較臉部反殼和外套的 Unity Toon Shader 描邊。

也可以在編輯模式旋轉 Main Directional Light，從 Game 視窗看陰影。Preview/UnityURP.png 是實際渲染的對照。

## 接自己的角色

- 身體使用 Toon/Toon（Unity Toon Shader）。可從 Generated/Jacket - Unity Toon Shader.mat 複製一份材質。
- 臉部複製 Generated/Face - Noggi SDF.mat。Assets/NoggiDemo/Textures 裡的 4096 × 4096 PNG 直接取自 Noggi v2.1 的 Shader.blend，維持 Linear、Clamp、無壓縮。
- 臉部 Shader 讀 UV0，將 URP 主光投影到頭部本地水平面。此範例臉朝本地 -Z。若角色的頭部骨架前向不同，需改 FaceSdf.shader 中的前向投影或對齊臉網格座標。左右 UV 慣例相反時，可以切換 Invert UV direction。
- 臉部反殼是同一張網格的第二個 Renderer，使用 Face Outline 材質。它沿法線外擴並剔除正面。帶骨架的角色應讓臉和反殼共用對應的蒙皮與骨架。
- 範例的臉部陰影由 SDF 貼圖與主光方位決定。頭髮投影等場景遮擋未接入這個臉部材質。

## Bforartists 示範

開啟 Bforartists/Noggi_SDF_Suzanne.blend。這份副本把包內的 SDF 材質分配到 Suzanne 的臉部面，使用正面投影對齊 UV，並加入包內的反殼 Geometry Nodes。

- 時間軸第 1、41、81 幀分別是 -55°、正面、+55°。
- 在 Outliner 顯示或隱藏 Outline | toggle in Outliner 可比較反殼。F12 渲染時使用物件的渲染開關。
- 若換成自己的模型，先把臉部分配到 SDF 材質，再對齊臉部 UV；反殼厚度需依模型大小微調。
- Preview/Bforartists.png 是這份副本的實際渲染。原始下載的 Shader.blend 未被改寫。

[Unity URP 自訂 shader 讀取主光](https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/shaders/writing-custom-shaders-urp/use-built-in-shader-methods/lighting)
[Unity Toon Shader](https://github.com/Unity-Technologies/com.unity.toonshader)
[Noggi's 3D Anime Starter Kit](https://noggi3d.gumroad.com/l/starter-kit)

## Suzanne FBX 對照

離開 Play Mode 後，開啟 Assets/NoggiDemo/SuzanneLighting.unity，再按 Play。原本的 NoggiLighting 場景是另一個角色，仍可獨立開啟。

Assets/NoggiDemo/Models/Suzanne_Noggi.fbx 來自 Bforartists 的 Suzanne 示範。匯出使用已對齊的臉部 UV 作為 UV0，並保留臉部及其餘表面的兩個材質槽。FBX 的材質重新對應到專案裡的 Unity 材質，SDF 材質共用原來的 sdf_texture_generic.png。描邊在 Unity 使用同一份 FBX 網格做反殼。

在新場景按 Left、Front、Right 比較光向，用 Inverted-hull outlines 開關描邊。Preview/SuzanneUnityURP.png 是實際渲染結果。


## Perlica 與可重複使用的模板

在同一個專案開啟 Assets/PerlicaSdfDemo/PerlicaLighting.unity，看原角色 UV、SDF4 遮罩與反殼描邊。可攜版是專案根目錄的 NoggiSdfTemplate-URP.unitypackage。此專案已內建模板，直接使用 Tools → Noggi SDF → Character Setup。換自己的 FBX 時，請參考 Assets/NoggiSdfTemplate/使用說明.md。

