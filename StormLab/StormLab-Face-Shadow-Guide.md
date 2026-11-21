# StormLab｜臉部 SDF 與瀏海投影

## 開啟 Windows 版

將整個 ZIP 解壓縮，執行其中的 **StormLab.exe**。執行檔、StormLab_Data 和同層 DLL 請放在同一資料夾。啟動場景是 **Cyberpunk City**。Windows x64 版使用 Unity 6000.3.15f1 與 URP 17.3.0 建置。

在 Unity 編輯器中，開啟 **Assets → StormLab → City → CyberpunkCity**，再按 Play。若場景正在播放，先退出 Play，等 Unity 匯入更新，再重新播放。如果右側面板隱藏了，按 **Tab** 顯示。

## 在 Play 調整四個滑桿

右側 **EFFECT CONTROLS** 面板有兩組設定。開關與滑桿即時生效，兩組可以各自調整。

| 面板 | Strength 強度 | Softness 柔邊 |
| --- | --- | --- |
| **Face SDF** | 控制亮色與陰影色的混合。0% 等於沒有 SDF 陰影，100% 使用材質的完整陰影色。 | 控制 SDF 明暗交界的漸層寬度。低值接近硬邊，高值過渡較寬。 |
| **Bangs shadow** | 控制髮片投在額頭和上半臉的陰影濃度。0% 時看不見投影。 | 控制對髮片深度取樣的範圍。低值保留清晰的髮尖輪廓，高值會把邊緣揉開。 |

預設是 **Face SDF 100% 強度、約 12% 柔邊**，以及 **Bangs shadow 72% 強度、約 22% 柔邊**。面板顯示四捨五入後的百分比。滑桿在效果關閉時也能預先調整，重新開啟時會使用目前的值。

### 做出圖 2 那種只開瀏海投影

1. 在 **EFFECT CONTROLS** 把 **Face SDF** 切成 OFF，將 **Bangs shadow** 切成 ON。
2. 以瀏海投影 **Strength 80%**、**Softness 15–25%** 起步。想保留清楚的髮尖輪廓就降低柔邊，想讓眼睛上方自然過渡就稍微增加柔邊。
3. 按 **T** 進入調燈模式，按 **L** 選 **Twilight key**。方向燈模式下可用 **WASD** 改變俯仰與左右角度。先從臉的前上方照，觀察投影落在額頭和眉上。調燈面板的 **RESET** 可以把選中的燈恢復到場景初始姿態。
4. 若位置已經適合，只調強度和柔邊。陰影的位置和形狀由光向、髮片與臉部的相對位置決定。

要比較純 SDF，就開啟 Face SDF 並關閉 Bangs shadow。**F8** 是 StormLab 原有的 SDF 遮罩偵錯模式。頭頂出現 **FACE SDF MASK [F8]** 時，再按 F8 回正常畫面。退出 Play 後，面板操作會回到場景與材質的初始值。

## 在 Unity Inspector 找到改動

### Perlica 範例專案

1. 開啟 **PerlicaLighting**，在 Hierarchy 展開 **Perlica**，點選 **face.002**。
2. 在右側 Inspector 往上捲到元件區，展開 **Bangs Shadow Projection**。你的截圖上方的 **Capture Radius、Strength、Depth Bias** 就是這個元件的欄位。更上方可以看到 **Caster** 指到髮網格 **hair**，**Receiver** 指回臉部 Renderer，**Receiver Slot** 指定皮膚材質槽。
3. 同一個物件的 **Skinned Mesh Renderer → Materials** 可查看各個槽。展開 **Perlica - Face SDF**，就會看到 **Noggi SDF Toolkit/Face (URP)**、臉部 SDF 貼圖、亮暗色和唇部光澤。這是把臉部 SDF 和瀏海深度合成到皮膚的材質。
4. **Eye Shadow、Mouth、Iris、Lashes、Sclera** 是臉網格上其他表面的材質。**Eye Shadow** 的透明設定控制眼瞼與虹膜上方的遮色。瀏海投影的來源和範圍由上方的 **Bangs Shadow Projection** 元件決定。

範例專案的檔案在 **Assets/NoggiSdfTemplate**。臉部 Shader 是 **Shaders/FaceSdf.shader**，瀏海元件是 **Runtime/BangsShadowProjection.cs**，投影讀取與擷取程式是 **Shaders/BangsProjection.hlsl** 和 **Shaders/BangsDepth.shader**。

### StormLab 專案

1. 開啟 **CyberpunkCity**，在 Hierarchy 依序展開 **Perlica • target** 和底下的模型，選 **face.002**。
2. 查看 **Bangs Shadow Projection**。StormLab 的 **Caster** 指到城市場景實際顯示的動畫髮網格，**Receiver** 是臉部的皮膚槽。點 Inspector 的物件欄位可跳到對應 Renderer。
3. 要改瀏海效果的**預設值**，先退出 Play，在該元件調整 **Strength**、**Softness**，再存場景。
4. 臉部 SDF 的預設值在 Project 面板 **Assets/model/Perlica-Materials/face.perlica.mat**。它使用 **StormLab/Perlica Toon**。材質的 **Face Shadow Strength** 和 **Face Shadow Softness** 是 SDF 初始值。材質柔邊的實際範圍是 0–0.18，對應 HUD 0–100%。
5. Hierarchy 的 **Storm Lab controls** 上有 **Storm Controls** 元件。其 **Face Sdf Target** 與 **Bangs Target** 連到角色設定，右側面板透過它們切換效果。

StormLab 的主要檔案都在 **Assets/StormLab**。

| 要看什麼 | 檔案 |
| --- | --- |
| 右側面板的開關與四個滑桿 | StormControls.cs |
| 將 SDF 開關、強度與柔邊寫入皮膚材質槽 | City/CityTargetMotion.cs |
| SDF 門檻、臉部陰影色、瀏海陰影合成 | Shaders/PerlicaToon.shader |
| 用主方向燈擷取額頭前的髮片 | City/BangsShadowProjection.cs、Shaders/BangsDepth.shader |
| 投影深度取樣、比較與柔邊 | Shaders/BangsProjection.hlsl |

## 兩種陰影如何一起工作

**Face SDF** 用頭部相對於主光的方向讀取臉部的距離遮罩，決定左右分色。**Bangs shadow** 把額頭前方的髮片擷取成沿主光方向的深度圖。對同一條光線，位於髮片後方的皮膚才接收投影。StormLab 的皮膚 Shader 依序套用這兩層效果。髮片姿勢或主光角度改變時，投影形狀也會跟著變。

## 用 Unity 自己建置

在 **File → Build Profiles** 選 Windows、x86_64。將 **CyberpunkCity** 加入場景清單並排第一，然後 Build。這份發行版以 Cyberpunk City 啟動，也包含專案原先啟用的 StormLab 和 LanternQuay 場景。

