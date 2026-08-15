using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmHunter.PirateOceanPrototype
{
    /// <summary>
    /// Switches between the ship combat shot and the sea-monster wide shot by
    /// changing Cinemachine camera priorities. The public methods are intended
    /// to be called by the future battle and boss-introduction flow.
    /// </summary>
    public sealed class PirateBossCameraController : MonoBehaviour
    {
        private const int LivePriority = 20;
        private const int StandbyPriority = 10;

        [Header("Cinemachine References")]
        [SerializeField] private CinemachineBrain brain;
        [SerializeField] private CinemachineCamera shipCombatCamera;
        [SerializeField] private CinemachineCamera bossWideCamera;

        [Header("Transition")]
        [SerializeField, Min(0f)] private float blendDuration = 2.5f;
        [SerializeField] private CinemachineBlendDefinition.Styles blendStyle =
            CinemachineBlendDefinition.Styles.EaseInOut;
        [SerializeField] private bool startWithBossView;

        [Header("Prototype Input")]
        [Tooltip("Allows the B key to toggle shots while testing this prototype scene.")]
        [SerializeField] private bool enableKeyboardToggle = true;

        [SerializeField, HideInInspector] private bool bossViewActive;

        public CinemachineBrain Brain => brain;
        public CinemachineCamera ShipCombatCamera => shipCombatCamera;
        public CinemachineCamera BossWideCamera => bossWideCamera;
        public bool BossViewActive => bossViewActive;
        public float BlendDuration => blendDuration;
        public bool IsBlending => brain != null && brain.IsBlending;

        public void Configure(
            CinemachineBrain targetBrain,
            CinemachineCamera combatCamera,
            CinemachineCamera wideCamera)
        {
            brain = targetBrain;
            shipCombatCamera = combatCamera;
            bossWideCamera = wideCamera;
            ApplyBlendSettings();
            SetBossView(startWithBossView);
        }

        private void Awake()
        {
            ApplyBlendSettings();
            SetBossView(startWithBossView);
        }

        private void Update()
        {
            if (!enableKeyboardToggle || Keyboard.current == null)
                return;

            if (Keyboard.current.bKey.wasPressedThisFrame)
                ToggleView();
        }

        private void OnValidate()
        {
            blendDuration = Mathf.Max(0f, blendDuration);
            ApplyBlendSettings();
        }

        [ContextMenu("Show Ship Combat View")]
        public void ShowShipCombatView()
        {
            SetBossView(false);
        }

        [ContextMenu("Show Boss Wide View")]
        public void ShowBossWideView()
        {
            SetBossView(true);
        }

        [ContextMenu("Toggle Ship / Boss View")]
        public void ToggleView()
        {
            SetBossView(!bossViewActive);
        }

        public void SetBossView(bool showBoss)
        {
            if (shipCombatCamera == null || bossWideCamera == null)
                return;

            bossViewActive = showBoss;
            shipCombatCamera.Priority = showBoss ? StandbyPriority : LivePriority;
            bossWideCamera.Priority = showBoss ? LivePriority : StandbyPriority;

            if (showBoss)
                bossWideCamera.Prioritize();
            else
                shipCombatCamera.Prioritize();
        }

        public void SetBlendDuration(float seconds)
        {
            blendDuration = Mathf.Max(0f, seconds);
            ApplyBlendSettings();
        }

        public void SetKeyboardToggleEnabled(bool enabled)
        {
            enableKeyboardToggle = enabled;
        }

        private void ApplyBlendSettings()
        {
            if (brain != null)
                brain.DefaultBlend = new CinemachineBlendDefinition(blendStyle, blendDuration);
        }
    }
}
