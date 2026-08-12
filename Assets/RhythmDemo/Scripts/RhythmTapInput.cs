using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmHunter.RhythmDemo
{
    public sealed class RhythmTapInput : MonoBehaviour
    {
        [SerializeField] private FmodRhythmJudge rhythmJudge;

        private InputAction tapAction;

        public void Configure(FmodRhythmJudge judge)
        {
            rhythmJudge = judge;
        }

        private void Awake()
        {
            tapAction = new InputAction("RhythmTap", InputActionType.Button);
            tapAction.AddBinding("<Mouse>/leftButton");
            tapAction.AddBinding("<Keyboard>/space");
            tapAction.AddBinding("<Keyboard>/enter");
            tapAction.AddBinding("<Gamepad>/buttonSouth");
        }

        private void OnEnable()
        {
            if (tapAction == null)
                return;

            tapAction.performed += OnTapPerformed;
            tapAction.Enable();
        }

        private void OnDisable()
        {
            if (tapAction == null)
                return;

            tapAction.performed -= OnTapPerformed;
            tapAction.Disable();
        }

        private void OnDestroy()
        {
            tapAction?.Dispose();
        }

        private void OnTapPerformed(InputAction.CallbackContext context)
        {
            rhythmJudge?.JudgeNow();
        }
    }
}
