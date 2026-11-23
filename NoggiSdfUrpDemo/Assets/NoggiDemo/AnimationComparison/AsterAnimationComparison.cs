using UnityEngine;

namespace NoggiDemo
{
    public sealed class AsterAnimationComparison : MonoBehaviour
    {
        [SerializeField] GameObject smoothActor;
        [SerializeField] GameObject steppedActor;
        [SerializeField] AnimationClip smoothClip;
        [SerializeField] AnimationClip steppedClip;

        bool playing = true;
        float position;

        void OnEnable() => Sample(0f);

        void Update()
        {
            if (playing)
                Sample(Mathf.Repeat(position + Time.unscaledDeltaTime, smoothClip.length));
        }

        void Sample(float time)
        {
            position = time;
            smoothClip.SampleAnimation(smoothActor, time);
            steppedClip.SampleAnimation(steppedActor, time);
        }

        void OnGUI()
        {
            const float height = 33f;
            float width = Mathf.Min(510f, Screen.width - 24f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * .5f, Screen.height - height - 6f, width, height), GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(playing ? "Pause" : "Play", GUILayout.Width(55f)))
            {
                if (!playing && position >= smoothClip.length)
                    Sample(0f);
                playing = !playing;
            }
            if (GUILayout.Button("Replay", GUILayout.Width(60f)))
            {
                Sample(0f);
                playing = true;
            }
            float next = GUILayout.HorizontalSlider(position, 0f, smoothClip.length);
            if (!Mathf.Approximately(next, position))
            {
                playing = false;
                Sample(next);
            }
            GUILayout.Label($"{position:0.00} s", GUILayout.Width(52f));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
