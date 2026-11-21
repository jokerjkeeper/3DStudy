using UnityEngine;

namespace NoggiDemo
{
    public sealed class LightingControls : MonoBehaviour
    {
        [SerializeField] Light keyLight;
        [SerializeField] Renderer body;
        [SerializeField] Material bodyWithOutline;
        [SerializeField] Material bodyWithoutOutline;
        [SerializeField] Renderer faceOutline;
        [SerializeField, Range(-150, 150)] float lightAngle = -45f;
        [SerializeField] bool outlines = true;

        float sweepStart;
        bool sweeping;

        public void Configure(Light light, Renderer bodyRenderer, Material inked, Material clean, Renderer faceHull)
        {
            keyLight = light;
            body = bodyRenderer;
            bodyWithOutline = inked;
            bodyWithoutOutline = clean;
            faceOutline = faceHull;
            SetLighting(lightAngle, outlines);
        }

        public void SetLighting(float degrees, bool showOutlines)
        {
            lightAngle = degrees;
            outlines = showOutlines;
            float radians = degrees * Mathf.Deg2Rad;
            // A directional light shines along its Transform.forward.
            Vector3 towardLight = new(Mathf.Sin(radians), 0.28f, -Mathf.Cos(radians));
            keyLight.transform.rotation = Quaternion.LookRotation(-towardLight, Vector3.up);
            body.sharedMaterial = outlines ? bodyWithOutline : bodyWithoutOutline;
            faceOutline.enabled = outlines;
        }

        void Update()
        {
            if (sweeping)
                SetLighting(Mathf.Sin((Time.unscaledTime - sweepStart) * 1.25f) * 115f, outlines);
        }

        void OnGUI()
        {
            const float width = 270f;
            float controlScale = Mathf.Clamp(Screen.width / 1000f, 0.75f, 1.3f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(controlScale, controlScale, 1));
            GUILayout.BeginArea(new Rect(21, 21, width, 182), GUI.skin.box);
            GUILayout.Label("NOGGI / URP LIGHTING STUDY");
            GUILayout.Space(7);
            GUILayout.Label($"Main light azimuth  {lightAngle:+0;-0;0}°");
            float next = GUILayout.HorizontalSlider(lightAngle, -150f, 150f);
            if (!Mathf.Approximately(next, lightAngle))
                ChooseAngle(next);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Left")) ChooseAngle(-60);
            if (GUILayout.Button("Front")) ChooseAngle(0);
            if (GUILayout.Button("Right")) ChooseAngle(60);
            GUILayout.EndHorizontal();
            GUILayout.Space(7);
            bool nextOutline = GUILayout.Toggle(outlines, "  Inverted-hull outlines");
            if (nextOutline != outlines) SetLighting(lightAngle, nextOutline);
            if (GUILayout.Button(sweeping ? "Pause light sweep" : "Sweep main light"))
            {
                sweeping = !sweeping;
                sweepStart = Time.unscaledTime;
            }
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        void ChooseAngle(float angle)
        {
            sweeping = false;
            SetLighting(angle, outlines);
        }
    }
}

