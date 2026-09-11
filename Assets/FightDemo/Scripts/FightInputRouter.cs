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
        private InputAction subscribedTankAction;
        private InputAction subscribedSupportAction;
        private InputAction subscribedDamageAction;
        private InputAction subscribedUltimateAction;
        private bool enabledTankAction;
        private bool enabledSupportAction;
        private bool enabledDamageAction;
        private bool enabledUltimateAction;

        public event Action<HeroCommand> CommandStarted;

        public InputActionAsset FightControls => fightControls;
        public bool IsConfigured => tankAction != null && supportAction != null &&
                                    damageAction != null && ultimateAction != null;

        public void Configure(InputActionAsset controls)
        {
            Unsubscribe();
            fightControls = controls;
            CacheActions();
            if (isActiveAndEnabled)
                Subscribe();
        }

        private void Awake()
        {
            CacheActions();
        }

        private void OnEnable()
        {
            CacheActions();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void CacheActions()
        {
            tankAction = null;
            supportAction = null;
            damageAction = null;
            ultimateAction = null;

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

        private void Subscribe()
        {
            Unsubscribe();
            subscribedTankAction = tankAction;
            subscribedSupportAction = supportAction;
            subscribedDamageAction = damageAction;
            subscribedUltimateAction = ultimateAction;
            SubscribeAction(subscribedTankAction, OnTank, ref enabledTankAction);
            SubscribeAction(subscribedSupportAction, OnSupport, ref enabledSupportAction);
            SubscribeAction(subscribedDamageAction, OnDamage, ref enabledDamageAction);
            SubscribeAction(subscribedUltimateAction, OnUltimate, ref enabledUltimateAction);
        }

        private void Unsubscribe()
        {
            UnsubscribeAction(subscribedTankAction, OnTank, ref enabledTankAction);
            UnsubscribeAction(subscribedSupportAction, OnSupport, ref enabledSupportAction);
            UnsubscribeAction(subscribedDamageAction, OnDamage, ref enabledDamageAction);
            UnsubscribeAction(subscribedUltimateAction, OnUltimate, ref enabledUltimateAction);
            subscribedTankAction = null;
            subscribedSupportAction = null;
            subscribedDamageAction = null;
            subscribedUltimateAction = null;
        }

        private static void SubscribeAction(
            InputAction action,
            Action<InputAction.CallbackContext> callback,
            ref bool enabledByRouter)
        {
            enabledByRouter = false;
            if (action == null)
                return;

            action.started -= callback;
            action.started += callback;
            if (!action.enabled)
            {
                action.Enable();
                enabledByRouter = true;
            }
        }

        private static void UnsubscribeAction(
            InputAction action,
            Action<InputAction.CallbackContext> callback,
            ref bool enabledByRouter)
        {
            if (action != null)
            {
                action.started -= callback;
                if (enabledByRouter && action.enabled)
                    action.Disable();
            }

            enabledByRouter = false;
        }

        private void OnTank(InputAction.CallbackContext context) => CommandStarted?.Invoke(HeroCommand.Tank);
        private void OnSupport(InputAction.CallbackContext context) => CommandStarted?.Invoke(HeroCommand.Support);
        private void OnDamage(InputAction.CallbackContext context) => CommandStarted?.Invoke(HeroCommand.Damage);
        private void OnUltimate(InputAction.CallbackContext context) => CommandStarted?.Invoke(HeroCommand.Ultimate);
    }
}
