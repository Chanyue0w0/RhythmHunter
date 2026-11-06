using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmHunter.FightDemo.Art2
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class Art2CameraController : MonoBehaviour
    {
        [SerializeField] private bool perspective = true;
        [SerializeField, Range(30, 60)] private float pitch = 45;
        [SerializeField, Range(20, 70)] private float fieldOfView = 40;
        [SerializeField, Min(1)] private float framingHalfHeight = 5.35f;
        [SerializeField] private Vector3 focus = Vector3.zero;
        [SerializeField, Min(0)] private float panSpeed = 3;
        [SerializeField, Min(0)] private float zoomSpeed = 0.15f;
        [SerializeField] private Vector2 zoomLimits = new Vector2(3.5f, 8);
        [SerializeField] private bool enableKeyboardAndWheel = true;

        private Camera view;
        private Vector3 initialFocus;
        private float initialPitch, initialHeight, initialFov;
        private bool initialPerspective;

        private void Awake()
        {
            view = GetComponent<Camera>();
            initialFocus = focus;
            initialPitch = pitch;
            initialHeight = framingHalfHeight;
            initialFov = fieldOfView;
            initialPerspective = perspective;
            ApplyView();
        }

        private void OnValidate()
        {
            pitch = Mathf.Clamp(pitch, 30, 60);
            fieldOfView = Mathf.Clamp(fieldOfView, 20, 70);
            zoomLimits.x = Mathf.Max(1, zoomLimits.x);
            zoomLimits.y = Mathf.Max(zoomLimits.x, zoomLimits.y);
            framingHalfHeight = Mathf.Clamp(framingHalfHeight, zoomLimits.x, zoomLimits.y);
        }

        private void LateUpdate()
        {
            if (enableKeyboardAndWheel && Application.isFocused)
            {
                var keys = Keyboard.current;
                if (keys != null)
                {
                    var move = new Vector3((keys.rightArrowKey.isPressed ? 1 : 0) - (keys.leftArrowKey.isPressed ? 1 : 0),
                        0, (keys.upArrowKey.isPressed ? 1 : 0) - (keys.downArrowKey.isPressed ? 1 : 0));
                    focus += Vector3.ClampMagnitude(move, 1) * (panSpeed * Time.unscaledDeltaTime);
                    if (keys.pKey.wasPressedThisFrame) perspective = !perspective;
                    if (keys.homeKey.wasPressedThisFrame) ResetView();
                }
                if (Mouse.current != null)
                {
                    float scroll = Mouse.current.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f)
                        framingHalfHeight = Mathf.Clamp(framingHalfHeight - Mathf.Sign(scroll) * zoomSpeed,
                            zoomLimits.x, zoomLimits.y);
                }
            }
            ApplyView();
        }

        [ContextMenu("Reset View (Play Mode)")]
        public void ResetView()
        {
            if (!Application.isPlaying) return;
            focus = initialFocus;
            pitch = initialPitch;
            framingHalfHeight = initialHeight;
            fieldOfView = initialFov;
            perspective = initialPerspective;
            ApplyView();
        }

        private void ApplyView()
        {
            if (!view) view = GetComponent<Camera>();
            view.orthographic = !perspective;
            view.orthographicSize = framingHalfHeight;
            view.fieldOfView = fieldOfView;
            transform.rotation = Quaternion.Euler(pitch, 0, 0);
            // Equal vertical framing at the focus plane in either projection.
            float distance = framingHalfHeight / Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            transform.position = focus - transform.forward * distance;
            view.transparencySortMode = UnityEngine.TransparencySortMode.CustomAxis;
            view.transparencySortAxis = transform.forward;
        }
    }
}
