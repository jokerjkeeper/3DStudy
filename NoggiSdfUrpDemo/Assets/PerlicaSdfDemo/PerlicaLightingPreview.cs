using UnityEngine;
using NoggiSdfToolkit;

namespace NoggiDemo
{
    [AddComponentMenu("")]
    public sealed class PerlicaLightingPreview : MonoBehaviour
    {
        [SerializeField] Light mainLight;
        [SerializeField] GameObject[] outlines;
        [SerializeField] float angle = -65;
        [SerializeField] bool ink = true;
        [SerializeField] BangsShadowProjection bangs;

        [SerializeField] GameObject soloCamera;
        [SerializeField] GameObject soloModel;
        [SerializeField] GameObject outlineComparison;

        FaceSdfBasis face;

        public void SetOutlineComparison(bool enabled)
        {
            if (!outlineComparison) return;
            if (enabled)
            {
                soloCamera.SetActive(false);
                soloModel.SetActive(false);
                outlineComparison.SetActive(true);
            }
            else
            {
                outlineComparison.SetActive(false);
                soloModel.SetActive(true);
                soloCamera.SetActive(true);
            }
        }

        void Awake() => CacheFaceBasis();

        public void BindBangs(BangsShadowProjection projection)
        {
            bangs = projection;
            CacheFaceBasis();
        }

        void CacheFaceBasis() => face = bangs ? bangs.GetComponent<FaceSdfBasis>() : null;

        public void Bind(Light light, GameObject[] hulls)
        {
            mainLight = light;
            outlines = hulls;
            Apply(angle, ink);
        }

        public void Apply(float degrees, bool showOutlines)
        {
            angle = degrees;
            ink = showOutlines;
            float radians = degrees * Mathf.Deg2Rad;
            Vector3 towardLight = new(Mathf.Sin(radians), .27f, -Mathf.Cos(radians));
            mainLight.transform.rotation = Quaternion.LookRotation(-towardLight);
            foreach (GameObject hull in outlines)
                hull.SetActive(ink);
        }

        void OnGUI()
        {
            if (outlineComparison && outlineComparison.activeSelf)
            {
                if (GUI.Button(new Rect(12, 12, 112, 27), "Solo preview"))
                    SetOutlineComparison(false);
                return;
            }

            GUILayout.BeginArea(new Rect(18, 18, 230, 350), GUI.skin.box);
            if (outlineComparison && GUILayout.Button("Outline A/B"))
            {
                SetOutlineComparison(true);
                GUILayout.EndArea();
                return;
            }
            GUILayout.Label("PERLICA / FACE SDF");
            GUILayout.Label($"Main light  {angle:+0;-0;0}°");
            float value = GUILayout.HorizontalSlider(angle, -150, 150);
            if (!Mathf.Approximately(value, angle)) Apply(value, ink);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Left")) Apply(-65, ink);
            if (GUILayout.Button("Front")) Apply(0, ink);
            if (GUILayout.Button("Right")) Apply(65, ink);
            GUILayout.EndHorizontal();
            bool visible = GUILayout.Toggle(ink, " Inverted-hull outlines");
            if (visible != ink) Apply(angle, visible);
            if (face)
            {
                face.SdfEnabled = GUILayout.Toggle(face.SdfEnabled, " Face SDF");
                face.Strength = PercentSlider("Face SDF strength", face.Strength);
            }
            if (bangs)
            {
                bool enabled = GUILayout.Toggle(bangs.ProjectionEnabled, " Bangs shadow");
                if (enabled != bangs.ProjectionEnabled) bangs.ProjectionEnabled = enabled;
                bangs.Strength = PercentSlider("Bangs strength", bangs.Strength);
                bangs.Softness = PercentSlider("Bangs softness", bangs.Softness);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("0% softness")) bangs.Softness = 0f;
                if (GUILayout.Button("85% softness")) bangs.Softness = .85f;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        static float PercentSlider(string label, float value)
        {
            GUILayout.Label($"{label}  {value:0%}");
            return GUILayout.HorizontalSlider(value, 0f, 1f);
        }
    }
}
