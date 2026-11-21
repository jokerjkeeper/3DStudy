using UnityEngine;

namespace BodyCurrent
{
    public sealed class DemoMotion : MonoBehaviour
    {
        public Transform chest;
        public Transform head;
        public Transform leftArm;
        public Transform rightArm;
        public Transform leftElbow;
        public Transform rightElbow;
        public GameObject arcs;
        public BodyCurrentFeature feature;

        enum LightingMode { Combined, Outline, Fresnel }
        LightingMode mode;
        bool moving = true;
        bool showArcs = true;
        float poseTime;
        float defaultOutline;
        float defaultFresnel;

        void Awake()
        {
            defaultOutline = feature.outlinePixels;
            defaultFresnel = feature.fresnelStrength;
            ApplyMode();
        }

        void Update()
        {
            if (moving)
                poseTime += Time.deltaTime;
            SamplePose(poseTime);
        }

        void OnDestroy()
        {
            if (!feature)
                return;
            feature.outlinePixels = defaultOutline;
            feature.fresnelStrength = defaultFresnel;
        }

        public void SamplePose(float time)
        {
            float swing = Mathf.Sin(time * 1.45f);
            float lift = 0.5f + 0.5f * Mathf.Sin(time * 1.75f);
            chest.localRotation = Quaternion.Euler(4, -7 + swing * 12f, swing * 5f);
            head.localRotation = Quaternion.Euler(-3, 10 - swing * 12f, -3);
            leftArm.localRotation = Quaternion.Euler(-9 - lift * 27f, 1, -12 - lift * 16f);
            rightArm.localRotation = Quaternion.Euler(8 - lift * 20f, -8, 13 + lift * 29f);
            leftElbow.localRotation = Quaternion.Euler(26 + lift * 28f, 0, 4);
            rightElbow.localRotation = Quaternion.Euler(22 + lift * 38f, 0, -6);
        }

        void ApplyMode()
        {
            feature.outlinePixels = mode == LightingMode.Fresnel ? 0 : defaultOutline;
            feature.fresnelStrength = mode == LightingMode.Outline ? 0 : defaultFresnel;
        }

        void OnGUI()
        {
            const float width = 340;
            GUILayout.BeginArea(new Rect(18, 18, width, 92), GUI.skin.box);
            int choice = GUILayout.Toolbar((int)mode, new[] { "Combined", "Mask outline", "Fresnel" });
            if (choice != (int)mode)
            {
                mode = (LightingMode)choice;
                ApplyMode();
            }

            GUILayout.BeginHorizontal();
            showArcs = GUILayout.Toggle(showArcs, "Shoulder arcs");
            moving = GUILayout.Toggle(moving, "Animate pose");
            GUILayout.EndHorizontal();
            if (arcs.activeSelf != showArcs)
                arcs.SetActive(showArcs);
            GUILayout.EndArea();
        }
    }
}
