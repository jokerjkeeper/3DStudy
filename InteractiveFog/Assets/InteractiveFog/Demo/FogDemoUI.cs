using UnityEngine;

namespace InteractiveFog.Demo
{
    /// <summary>Minimal IMGUI panel to tweak the fog at runtime.</summary>
    public class FogDemoUI : MonoBehaviour
    {
        public FogSimulation sim;
        public static Rect PanelRect = new Rect(12, 12, 280, 300);

        float _acc, _fps;
        int _frames;

        void Update()
        {
            if (sim == null) return;
            if (FogDemoInput.ToggleStir()) sim.stir = !sim.stir;
            if (FogDemoInput.Reset()) sim.ResetFields();
            _acc += Time.unscaledDeltaTime; _frames++;
            if (_acc > 0.5f) { _fps = _frames / _acc; _acc = 0; _frames = 0; }
        }

        void OnGUI()
        {
            if (sim == null) return;
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label(sim.stir ? "<b>Stable Fluids + Stir</b>" : "<b>Stable Fluids</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 15 });
            sim.stir = GUILayout.Toggle(sim.stir, " Stir 渦度約束 (V)");
            GUILayout.Label($"渦度強度 {sim.vorticity:0.0}");
            sim.vorticity = GUILayout.HorizontalSlider(sim.vorticity, 0f, 10f);
            sim.curlWind = GUILayout.Toggle(sim.curlWind, " Curl-noise 環境風");
            GUILayout.Label($"霧濃度 {sim.density:0.0}");
            sim.density = GUILayout.HorizontalSlider(sim.density, 0.4f, 4f);
            GUILayout.Label($"Ray steps {sim.raySteps}");
            sim.raySteps = Mathf.RoundToInt(GUILayout.HorizontalSlider(sim.raySteps, 16, 96));
            if (GUILayout.Button("重置霧氣 (R)")) sim.ResetFields();
            GUILayout.Label($"{_fps:0} fps · grid {sim.resolution}²");
            GUILayout.Label("WASD 移動 · 左鍵拖曳攪霧 · 滾輪縮放");
            GUILayout.EndArea();
        }
    }
}
