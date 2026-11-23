using UnityEngine;

namespace NoggiDemo
{
    public sealed class PerlicaOrbitComparison : MonoBehaviour
    {
        [SerializeField] Camera leftCamera;
        [SerializeField] Camera rightCamera;
        [SerializeField] Transform leftSubject;
        [SerializeField] Transform rightSubject;

        const float YawLimit = 38f;
        const float HalfWidth = .203f;
        const float HalfHeight = .225f;
        const float Distance = 1.4f;
        static readonly Vector3 AimOffset = new(0f, 1.532f, -.02f);

        bool sweeping = true;
        float phase;
        float yaw;

        void OnEnable()
        {
            FitViewport(leftCamera);
            FitViewport(rightCamera);
            SetYaw(0f);
        }

        void LateUpdate()
        {
            FitViewport(leftCamera);
            FitViewport(rightCamera);
            if (sweeping)
                phase = Mathf.Repeat(phase + Time.unscaledDeltaTime * (Mathf.PI * 2f / 7f), Mathf.PI * 2f);
            SetYaw(sweeping ? Mathf.Sin(phase) * YawLimit : yaw);
        }

        public void SetYaw(float degrees)
        {
            yaw = Mathf.Clamp(degrees, -YawLimit, YawLimit);
            Vector3 view = Quaternion.Euler(0f, -yaw, 0f) * new Vector3(0f, .032f, -Distance);
            Quaternion orientation = Quaternion.LookRotation(-view, Vector3.up);
            Apply(leftCamera, leftSubject, view, orientation);
            Apply(rightCamera, rightSubject, view, orientation);
        }

        static void FitViewport(Camera camera)
        {
            if (camera.pixelWidth > 0 && camera.pixelHeight > 0)
                camera.aspect = (float)camera.pixelWidth / camera.pixelHeight;
        }

        static void Apply(Camera camera, Transform subject, Vector3 view, Quaternion orientation)
        {
            camera.transform.SetPositionAndRotation(subject.position + AimOffset + view, orientation);
            camera.orthographicSize = Mathf.Max(HalfHeight, HalfWidth / camera.aspect);
        }

        void OnGUI()
        {
            const float height = 33f;
            float width = Mathf.Min(500f, Screen.width - 24f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * .5f, Screen.height - height - 6f, width, height), GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(sweeping ? "Pause" : "Sweep", GUILayout.Width(62f)))
            {
                sweeping = !sweeping;
                if (sweeping)
                    phase = Mathf.Asin(yaw / YawLimit);
            }
            if (GUILayout.Button("Front", GUILayout.Width(52f)))
            {
                sweeping = false;
                SetYaw(0f);
            }
            float next = GUILayout.HorizontalSlider(yaw, -YawLimit, YawLimit);
            if (!Mathf.Approximately(next, yaw))
            {
                sweeping = false;
                SetYaw(next);
            }
            GUILayout.Label($"{yaw:+0;-0;0}°", GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
