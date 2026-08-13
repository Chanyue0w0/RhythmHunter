using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Converts the semantic actions in Beats and Bard's FightControl asset into hero commands.
    /// Physical keyboard and gamepad bindings remain owned by the Input Action Asset.
    /// </summary>
    public sealed class FightInputRouter : MonoBehaviour
    {
        public enum HeroCommand
        {
            Tank,
            Support,
            Damage,
            Ultimate
        }

        private const string ActionMapName = "Abilities";
        private const string TankActionName = "Character1Attack";
        private const string SupportActionName = "Character2Attack";
        private const string DamageActionName = "Character3Attack";
        private const string UltimateActionName = "FeverUlt";

        [SerializeField] private InputActionAsset fightControls;

        private InputAction tankAction;
        private InputAction supportAction;
        private InputAction damageAction;
        private InputAction ultimateAction;

        public event Action<HeroCommand> CommandStarted;

        public InputActionAsset FightControls => fightControls;
        public bool IsConfigured => tankAction != null && supportAction != null &&
                                    damageAction != null && ultimateAction != null;

        public void Configure(InputActionAsset controls)
        {
            fightControls = controls;
            CacheActions();
        }

        private void Awake()
        {
            CacheActions();
        }

        private void OnEnable()
        {
            CacheActions();
            Subscribe(true);
        }

        private void OnDisable()
        {
            Subscribe(false);
        }

        private void CacheActions()
        {
            if (fightControls == null)
                return;

            InputActionMap map = fightControls.FindActionMap(ActionMapName, false);
            if (map == null)
            {
                Debug.LogError($"[FightInputRouter] Missing action map '{ActionMapName}'.", this);
                return;
            }

            tankAction = map.FindAction(TankActionName, false);
            supportAction = map.FindAction(SupportActionName, false);
            damageAction = map.FindAction(DamageActionName, false);
            ultimateAction = map.FindAction(UltimateActionName, false);

            if (!IsConfigured)
                Debug.LogError("[FightInputRouter] FightControl action names do not match Beats and Bard.", this);
        }

        private void Subscribe(bool subscribe)
        {
            if (!IsConfigured)
                return;

            if (subscribe)
            {
                tankAction.started += OnTank;
                supportAction.started += OnSupport;
                damageAction.started += OnDamage;
                ultimateAction.started += OnUltimate;
                tankAction.Enable();
                supportAction.Enable();
                damageAction.Enable();
                ultimateAction.Enable();
            }
            else
            {
                tankAction.started -= OnTank;
                supportAction.started -= OnSupport;
                damageAction.started -= OnDamage;
                ultimateAction.started -= OnUltimate;
                tankAction.Disable();
                supportAction.Disable();
                damageAction.Disable();
                ultimateAction.Disable();
            }
        }

        private void OnTank(InputAction.CallbackContext context) => CommandStarted?.Invoke(HeroCommand.Tank);
        private void OnSupport(InputAction.CallbackContext context) => CommandStarted?.Invoke(HeroCommand.Support);
        private void OnDamage(InputAction.CallbackContext context) => CommandStarted?.Invoke(HeroCommand.Damage);
        private void OnUltimate(InputAction.CallbackContext context) => CommandStarted?.Invoke(HeroCommand.Ultimate);
    }
}
