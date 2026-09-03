using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmHunter.RhythmArena
{
    public sealed class PlayerCombatController : MonoBehaviour
    {
        public enum ActionType
        {
            None,
            QuickSlash,
            HeavySlash,
            BreakStrike,
            Guard
        }

        [Serializable]
        public struct ActionDefinition
        {
            public float durationBeats;
            public int perfectDamage;
            public int goodDamage;
            public int offbeatDamage;
        }

        [Header("Dependencies")]
        [SerializeField] private RhythmClock rhythmClock;
        [SerializeField] private CombatResolver combatResolver;
        [SerializeField] private EnemyPatternController enemyPattern;

        [Header("Fixed Action Knowledge")]
        [SerializeField] private ActionDefinition quickSlash = new()
        {
            durationBeats = 1f,
            perfectDamage = 12,
            goodDamage = 10,
            offbeatDamage = 8
        };
        [SerializeField] private ActionDefinition heavySlash = new()
        {
            durationBeats = 2f,
            perfectDamage = 30,
            goodDamage = 25,
            offbeatDamage = 20
        };
        [SerializeField] private ActionDefinition breakStrike = new()
        {
            durationBeats = 1.5f,
            perfectDamage = 5,
            goodDamage = 5,
            offbeatDamage = 5
        };
        [SerializeField] private ActionDefinition guard = new()
        {
            durationBeats = 0.5f,
            perfectDamage = 0,
            goodDamage = 0,
            offbeatDamage = 0
        };
        [SerializeField, Range(0.01f, 0.49f)] private float perfectGuardWindowBeats = 0.10f;

        [Header("World Feedback")]
        [SerializeField] private Transform heroVisual;
        [SerializeField] private GameObject guardShield;

        private InputAction quickAction;
        private InputAction heavyAction;
        private InputAction breakAction;
        private InputAction guardAction;
        private Vector3 heroRestPosition;
        private ActionType currentAction;
        private RhythmClock.TimingGrade currentGrade;
        private double actionStartBeat;
        private double actionEndBeat;
        private double guardWindowStart = double.NegativeInfinity;
        private double guardWindowEnd = double.NegativeInfinity;

        public bool IsBusy => currentAction != ActionType.None;
        public ActionType CurrentAction => currentAction;
        public RhythmClock.TimingGrade CurrentGrade => currentGrade;
        public double ActionStartBeat => actionStartBeat;
        public double ActionEndBeat => actionEndBeat;

        private void Awake()
        {
            heroRestPosition = heroVisual != null ? heroVisual.localPosition : Vector3.zero;
            CreateInputActions();
        }

        private void OnEnable()
        {
            BindInput(quickAction, OnQuick);
            BindInput(heavyAction, OnHeavy);
            BindInput(breakAction, OnBreak);
            BindInput(guardAction, OnGuard);
        }

        private void OnDisable()
        {
            UnbindInput(quickAction, OnQuick);
            UnbindInput(heavyAction, OnHeavy);
            UnbindInput(breakAction, OnBreak);
            UnbindInput(guardAction, OnGuard);
        }

        private void OnDestroy()
        {
            quickAction?.Dispose();
            heavyAction?.Dispose();
            breakAction?.Dispose();
            guardAction?.Dispose();
        }

        private void Update()
        {
            if (!IsBusy || rhythmClock == null || rhythmClock.AbsoluteBeatTime < actionEndBeat)
                return;

            ResolveCurrentAction();
        }

        public void Configure(
            RhythmClock clock,
            CombatResolver resolver,
            EnemyPatternController pattern,
            Transform visual,
            GameObject shield)
        {
            rhythmClock = clock;
            combatResolver = resolver;
            enemyPattern = pattern;
            heroVisual = visual;
            guardShield = shield;
            heroRestPosition = heroVisual != null ? heroVisual.localPosition : Vector3.zero;
        }

        public bool TryStartAction(ActionType actionType)
        {
            if (IsBusy || actionType == ActionType.None || rhythmClock == null || !rhythmClock.IsReady ||
                combatResolver == null || !combatResolver.CombatActive)
            {
                return false;
            }

            ActionDefinition definition = GetDefinition(actionType);
            currentAction = actionType;
            currentGrade = rhythmClock.JudgeNow();
            actionStartBeat = rhythmClock.AbsoluteBeatTime;
            actionEndBeat = actionStartBeat + definition.durationBeats;

            if (actionType == ActionType.Guard)
            {
                guardWindowStart = actionStartBeat;
                guardWindowEnd = actionEndBeat;
                if (guardShield != null)
                    guardShield.SetActive(true);
            }

            combatResolver.ShowMessage($"{GetActionLabel(actionType)}  {currentGrade.ToString().ToUpperInvariant()}");
            StartCoroutine(PlayActionFeedback(actionType, definition.durationBeats * rhythmClock.BeatDurationSeconds));
            return true;
        }

        public bool TryGuardAgainst(double attackBeat, out bool perfectParry)
        {
            bool insideGuard = attackBeat >= guardWindowStart - 0.001 && attackBeat <= guardWindowEnd + 0.001;
            perfectParry = insideGuard && Math.Abs(attackBeat - guardWindowStart) <= perfectGuardWindowBeats;
            return insideGuard;
        }

        public void ResetPlayer()
        {
            StopAllCoroutines();
            currentAction = ActionType.None;
            guardWindowStart = double.NegativeInfinity;
            guardWindowEnd = double.NegativeInfinity;
            if (heroVisual != null)
                heroVisual.localPosition = heroRestPosition;
            if (guardShield != null)
                guardShield.SetActive(false);
        }

        private void ResolveCurrentAction()
        {
            ActionType resolvedAction = currentAction;
            RhythmClock.TimingGrade resolvedGrade = currentGrade;
            currentAction = ActionType.None;

            if (guardShield != null)
                guardShield.SetActive(false);

            if (resolvedAction == ActionType.Guard)
                return;

            ActionDefinition definition = GetDefinition(resolvedAction);
            int damage = resolvedGrade switch
            {
                RhythmClock.TimingGrade.Perfect => definition.perfectDamage,
                RhythmClock.TimingGrade.Good => definition.goodDamage,
                _ => definition.offbeatDamage
            };

            combatResolver.DamageEnemy(damage, resolvedAction, resolvedGrade);
            if (!combatResolver.CombatActive || resolvedAction != ActionType.BreakStrike)
                return;

            float delay = resolvedGrade switch
            {
                RhythmClock.TimingGrade.Perfect => 1f,
                RhythmClock.TimingGrade.Good => 0.5f,
                _ => 0f
            };

            if (delay > 0f)
            {
                enemyPattern.ShiftNextAttack(delay);
                combatResolver.ShowMessage($"BREAK {resolvedGrade.ToString().ToUpperInvariant()}  NEXT ATTACK +{delay:0.0} BEAT");
            }
        }

        private IEnumerator PlayActionFeedback(ActionType actionType, float durationSeconds)
        {
            if (heroVisual == null)
                yield break;

            Vector3 start = heroRestPosition;
            float windupRatio = actionType == ActionType.HeavySlash ? 0.55f : 0.2f;
            float lunge = actionType == ActionType.QuickSlash ? 0.35f : actionType == ActionType.HeavySlash ? 0.55f : 0.2f;
            float elapsed = 0f;

            while (elapsed < durationSeconds)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, durationSeconds));

                if (actionType == ActionType.BreakStrike)
                {
                    float pulse = 1f + Mathf.Sin(normalized * Mathf.PI) * 0.18f;
                    heroVisual.localScale = Vector3.one * pulse;
                }
                else if (actionType != ActionType.Guard)
                {
                    float move = normalized < windupRatio
                        ? -0.08f * normalized / windupRatio
                        : lunge * Mathf.Sin((normalized - windupRatio) / (1f - windupRatio) * Mathf.PI);
                    heroVisual.localPosition = start + Vector3.left * move;
                }

                yield return null;
            }

            heroVisual.localPosition = start;
            heroVisual.localScale = Vector3.one;
        }

        private ActionDefinition GetDefinition(ActionType actionType)
        {
            return actionType switch
            {
                ActionType.QuickSlash => quickSlash,
                ActionType.HeavySlash => heavySlash,
                ActionType.BreakStrike => breakStrike,
                ActionType.Guard => guard,
                _ => default
            };
        }

        private void CreateInputActions()
        {
            quickAction = CreateAction("Quick Slash", "<Keyboard>/j", "<Gamepad>/buttonWest");
            heavyAction = CreateAction("Heavy Slash", "<Keyboard>/k", "<Gamepad>/buttonNorth");
            breakAction = CreateAction("Break Strike", "<Keyboard>/l", "<Gamepad>/buttonEast");
            guardAction = CreateAction("Guard", "<Keyboard>/space", "<Gamepad>/buttonSouth");
        }

        private static InputAction CreateAction(string name, string keyboardBinding, string gamepadBinding)
        {
            InputAction action = new(name, InputActionType.Button);
            action.AddBinding(keyboardBinding);
            action.AddBinding(gamepadBinding);
            return action;
        }

        private static void BindInput(InputAction action, Action<InputAction.CallbackContext> callback)
        {
            if (action == null)
                return;
            action.performed += callback;
            action.Enable();
        }

        private static void UnbindInput(InputAction action, Action<InputAction.CallbackContext> callback)
        {
            if (action == null)
                return;
            action.performed -= callback;
            action.Disable();
        }

        private void OnQuick(InputAction.CallbackContext _) => TryStartAction(ActionType.QuickSlash);
        private void OnHeavy(InputAction.CallbackContext _) => TryStartAction(ActionType.HeavySlash);
        private void OnBreak(InputAction.CallbackContext _) => TryStartAction(ActionType.BreakStrike);
        private void OnGuard(InputAction.CallbackContext _) => TryStartAction(ActionType.Guard);

        private static string GetActionLabel(ActionType actionType)
        {
            return actionType switch
            {
                ActionType.QuickSlash => "QUICK SLASH  [1 BEAT]",
                ActionType.HeavySlash => "HEAVY SLASH  [2 BEATS]",
                ActionType.BreakStrike => "BREAK STRIKE  [1.5 BEATS]",
                ActionType.Guard => "GUARD  [0.5 BEAT]",
                _ => string.Empty
            };
        }
    }
}
