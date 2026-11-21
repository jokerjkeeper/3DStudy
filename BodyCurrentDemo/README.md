# 貼身電流範本

用 Unity Hub 加入 `D:\unity\3D\BodyCurrentDemo`，使用 Unity **6000.3.15f1** 開啟。選 **Body Current → Open Demo Scene**，再按 **Play**。場景也可以從 `Assets/BodyCurrent/Demo/BodyCurrentDemo.unity` 開啟。

| 預覽選項 | 畫面 |
| --- | --- |
| Combined | 遮罩描邊加上表面 Fresnel |
| Mask outline | 只看角色剪影外側的藍線 |
| Fresnel | 只看角色表面的邊緣光 |
| Shoulder arcs | 顯示或隱藏肩膀和上臂的電弧 |
| Animate pose | 播放或暫停抬手、轉身。暫停時仍可觀察電弧跳動 |

## 套到自己的角色

1. 在 URP Forward Renderer 資產上加入 `BodyCurrentFeature`，指定 `CharacterMask.shader` 和 `Composite.shader`，並把角色的不透明渲染器放到指定的 Layer。範本使用 Unity 6 URP Render Graph，相機關閉 MSAA。
2. 用 `Outline Pixels` 調藍邊寬度，用 `Fresnel Strength` 調表面光。描邊取自已通過相機深度測試的角色遮罩。
3. 在胸骨和上臂骨下各加一個空物件，作為 `BodyArc` 的起點和終點。把上臂骨指定為 `Character Space`。複製場景的其中一條電弧即可沿用 Line Renderer 和材質。
4. 用 `Arch Direction`、`Arch Height` 和 `Jitter` 調整電弧離開皮膚的方向、距離和折點偏移。端點會在 LateUpdate 跟隨骨骼，中間折點獨立跳動。

角色使用由關節驅動的硬殼零件示範。電弧端點採用骨骼錨點。若你的肩部蒙皮變形較大，可以把錨點換成跟隨蒙皮表面的實作。

