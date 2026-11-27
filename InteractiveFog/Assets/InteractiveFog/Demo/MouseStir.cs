using UnityEngine;

namespace InteractiveFog.Demo
{
    /// <summary>Drag with the left mouse button to stir the fog.</summary>
    [RequireComponent(typeof(Camera))]
    public class MouseStir : MonoBehaviour
    {
        public FogInteractor interactor;
        public LayerMask mask = ~0;
        Camera _cam;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (interactor) interactor.enabled = false;
        }

        void Update()
        {
            if (interactor == null) return;
            Vector2 mp = FogDemoInput.MousePosition();
            bool overUI = FogDemoUI.PanelRect.Contains(new Vector2(mp.x, Screen.height - mp.y));
            if (FogDemoInput.MouseHeld() && !overUI &&
                Physics.Raycast(_cam.ScreenPointToRay(mp), out var hit, 500f, mask))
            {
                interactor.transform.position = hit.point;
                if (!interactor.enabled) interactor.enabled = true;
            }
            else if (interactor.enabled) interactor.enabled = false;
        }
    }
}
