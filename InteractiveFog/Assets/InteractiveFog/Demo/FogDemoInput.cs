using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace InteractiveFog.Demo
{
    /// <summary>Works with either the Input System package or the legacy Input Manager.</summary>
    public static class FogDemoInput
    {
#if ENABLE_INPUT_SYSTEM
        static bool K(Key k) { var kb = Keyboard.current; return kb != null && kb[k].isPressed; }
        static bool KD(Key k) { var kb = Keyboard.current; return kb != null && kb[k].wasPressedThisFrame; }

        public static Vector2 Move() => new Vector2(
            (K(Key.D) || K(Key.RightArrow) ? 1 : 0) - (K(Key.A) || K(Key.LeftArrow) ? 1 : 0),
            (K(Key.W) || K(Key.UpArrow) ? 1 : 0) - (K(Key.S) || K(Key.DownArrow) ? 1 : 0));
        public static bool ToggleStir() => KD(Key.V);
        public static bool Reset() => KD(Key.R);
        public static bool MouseHeld() => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public static Vector2 MousePosition() => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static float Scroll() => Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
        public static Vector2 Move() => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        public static bool ToggleStir() => Input.GetKeyDown(KeyCode.V);
        public static bool Reset() => Input.GetKeyDown(KeyCode.R);
        public static bool MouseHeld() => Input.GetMouseButton(0);
        public static Vector2 MousePosition() => Input.mousePosition;
        public static float Scroll() => Input.mouseScrollDelta.y;
#endif
    }
}
