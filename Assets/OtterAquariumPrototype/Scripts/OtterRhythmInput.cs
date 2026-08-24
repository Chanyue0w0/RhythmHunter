using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmHunter.OtterAquariumPrototype
{
    public sealed class OtterRhythmInput : MonoBehaviour
    {
        [SerializeField] private OtterRhythmLevelRunner levelRunner;

        private InputAction tapAction;

        public void Configure(OtterRhythmLevelRunner runner)
        {
            levelRunner = runner;
        }

        private void Awake()
        {
            tapAction = new InputAction("OtterShellTap", InputActionType.Button);
            tapAction.AddBinding("<Keyboard>/space");
            tapAction.AddBinding("<Keyboard>/enter");
            tapAction.AddBinding("<Mouse>/leftButton");
            tapAction.AddBinding("<Gamepad>/buttonSouth");
        }

        private void OnEnable()
        {
            if (tapAction == null)
                return;
            tapAction.performed += OnTap;
            tapAction.Enable();
        }

        private void OnDisable()
        {
            if (tapAction == null)
                return;
            tapAction.performed -= OnTap;
            tapAction.Disable();
        }

        private void OnDestroy()
        {
            tapAction?.Dispose();
        }

        private void OnTap(InputAction.CallbackContext context)
        {
            levelRunner?.SubmitInput();
        }
    }
}
