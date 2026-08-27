using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototype
{
    public sealed class OtterGoblinDemo1Input : MonoBehaviour
    {
        [SerializeField] private OtterGoblinDemo1Runner runner;
        [SerializeField] private OtterGoblinDemo1Presenter presenter;

        private InputAction defendAction;
        private InputAction restartAction;
        private InputAction toggleDiagnosticHudAction;

        public void Configure(
            OtterGoblinDemo1Runner configuredRunner,
            OtterGoblinDemo1Presenter configuredPresenter = null)
        {
            runner = configuredRunner;
            presenter = configuredPresenter;
        }

        private void Awake()
        {
            if (presenter == null)
                presenter = GetComponent<OtterGoblinDemo1Presenter>();

            defendAction = new InputAction("Demo1Defend", InputActionType.Button);
            defendAction.AddBinding("<Keyboard>/space");
            defendAction.AddBinding("<Keyboard>/enter");
            defendAction.AddBinding("<Mouse>/leftButton");
            defendAction.AddBinding("<Gamepad>/buttonSouth");

            restartAction = new InputAction("Demo1Restart", InputActionType.Button);
            restartAction.AddBinding("<Keyboard>/r");

            toggleDiagnosticHudAction = new InputAction("Demo1ToggleDiagnosticHud", InputActionType.Button);
            toggleDiagnosticHudAction.AddBinding("<Keyboard>/h");
        }

        private void OnEnable()
        {
            defendAction.performed += OnDefend;
            restartAction.performed += OnRestart;
            toggleDiagnosticHudAction.performed += OnToggleDiagnosticHud;
            defendAction.Enable();
            restartAction.Enable();
            toggleDiagnosticHudAction.Enable();
        }

        private void OnDisable()
        {
            defendAction.performed -= OnDefend;
            restartAction.performed -= OnRestart;
            toggleDiagnosticHudAction.performed -= OnToggleDiagnosticHud;
            defendAction.Disable();
            restartAction.Disable();
            toggleDiagnosticHudAction.Disable();
        }

        private void OnDestroy()
        {
            defendAction?.Dispose();
            restartAction?.Dispose();
            toggleDiagnosticHudAction?.Dispose();
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

        private void OnToggleDiagnosticHud(InputAction.CallbackContext context)
        {
            presenter?.ToggleDiagnosticHud();
        }
    }
}
