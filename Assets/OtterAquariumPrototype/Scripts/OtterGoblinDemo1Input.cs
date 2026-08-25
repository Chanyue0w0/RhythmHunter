using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototype
{
    public sealed class OtterGoblinDemo1Input : MonoBehaviour
    {
        [SerializeField] private OtterGoblinDemo1Runner runner;

        private InputAction defendAction;
        private InputAction restartAction;

        public void Configure(OtterGoblinDemo1Runner configuredRunner)
        {
            runner = configuredRunner;
        }

        private void Awake()
        {
            defendAction = new InputAction("Demo1Defend", InputActionType.Button);
            defendAction.AddBinding("<Keyboard>/space");
            defendAction.AddBinding("<Keyboard>/enter");
            defendAction.AddBinding("<Mouse>/leftButton");
            defendAction.AddBinding("<Gamepad>/buttonSouth");

            restartAction = new InputAction("Demo1Restart", InputActionType.Button);
            restartAction.AddBinding("<Keyboard>/r");
        }

        private void OnEnable()
        {
            defendAction.performed += OnDefend;
            restartAction.performed += OnRestart;
            defendAction.Enable();
            restartAction.Enable();
        }

        private void OnDisable()
        {
            defendAction.performed -= OnDefend;
            restartAction.performed -= OnRestart;
            defendAction.Disable();
            restartAction.Disable();
        }

        private void OnDestroy()
        {
            defendAction?.Dispose();
            restartAction?.Dispose();
        }

        private void OnDefend(InputAction.CallbackContext context)
        {
            runner?.SubmitInput();
        }

        private void OnRestart(InputAction.CallbackContext context)
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid())
                SceneManager.LoadScene(active.buildIndex >= 0 ? active.buildIndex : 0);
        }
    }
}
