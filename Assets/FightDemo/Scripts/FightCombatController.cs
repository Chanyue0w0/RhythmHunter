using System;
using System.Collections.Generic;
using RhythmHunter.RhythmDemo;
using UnityEngine;
using UnityEngine.Serialization;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Runs the original fourth-beat guard prototype in FightScene and the three-hero
    /// party controls used by the later fight scenes.
    /// </summary>
    public sealed class FightCombatController : MonoBehaviour
    {
        [Serializable]
        public sealed class HeroBeatSettings
        {
            [SerializeField] private string heroLabel = "Hero";
            [SerializeField] private FightUnitSlot unitSlot;

            [Tooltip("Runtime count used for gameplay validation.")]
            [SerializeField, Min(0)] private int skillActivationCount;

            public HeroBeatSettings()
            {
            }

            public HeroBeatSettings(string label)
            {
                heroLabel = label;
            }

            public string HeroLabel => heroLabel;
            public FightUnitSlot UnitSlot => unitSlot;
            public int AttackIntervalBeats => unitSlot != null && unitSlot.HasCharacter
                ? unitSlot.AttackIntervalBeats
                : 1;
            public string SkillName => unitSlot?.CharacterDefinition != null
                ? unitSlot.CharacterDefinition.SkillName
                : "Beat Skill";
            public int SkillActivationCount => skillActivationCount;

            internal void BindUnitSlot(FightUnitSlot slot, bool resetRuntime)
            {
                unitSlot = slot;
                RefreshRuntimeValues(resetRuntime);
            }

            internal void RefreshRuntimeValues(bool resetRuntime)
            {
                if (unitSlot != null && unitSlot.HasCharacter)
                    heroLabel = unitSlot.DisplayName;

                if (resetRuntime)
                {
                    unitSlot?.ResetMana();
                    skillActivationCount = 0;
                }
            }

            internal void RecordSkillActivation()
            {
                skillActivationCount++;
            }
        }

        public enum ActionType
        {
            LightAttack,
            HeavyAttack,
            Guard,
            Skill
        }

        public enum CombatMode
        {
            LegacyFourthBeatGuard,
            FrontHero
        }

        public readonly struct HeroCallResult
        {
            public HeroCallResult(
                FightInputRouter.HeroCommand command,
                FmodRhythmJudge.Result rhythmResult,
                bool isHeavyBeat,
                bool skillActivated,
                string message)
            {
                Command = command;
                RhythmResult = rhythmResult;
                IsHeavyBeat = isHeavyBeat;
                SkillActivated = skillActivated;
                Message = message;
            }

            public FightInputRouter.HeroCommand Command { get; }
            public FmodRhythmJudge.Result RhythmResult { get; }
            public bool IsHeavyBeat { get; }
            public bool SkillActivated { get; }
            public string Message { get; }
        }

        public readonly struct EnemyAttackResult
        {
            public EnemyAttackResult(long globalBeat, int bar, bool blocked, float damage, float partyHp)
            {
                GlobalBeat = globalBeat;
                Bar = bar;
                Blocked = blocked;
                Damage = damage;
                PartyHp = partyHp;
            }

            public long GlobalBeat { get; }
            public int Bar { get; }
            public bool Blocked { get; }
            public float Damage { get; }
            public float PartyHp { get; }
        }

        [Header("Dependencies")]
        [SerializeField] private FmodBeatClock beatClock;
        [SerializeField] private FmodRhythmJudge rhythmJudge;
        [SerializeField] private FightInputRouter inputRouter;
        [SerializeField] private FightRosterManager rosterManager;
        [SerializeField] private FightUnitSlot tankSlot;
        [SerializeField] private FightUnitSlot activeEnemySlot;

        [Header("Prototype Rules")]
        [SerializeField, Min(0.5f)] private float maxPartyHp = 4f;
        [SerializeField, Min(0.5f)] private float enemyAttackDamage = 1f;
        [SerializeField, Min(0f)] private float resolutionSafetyMs = 12f;

        [Header("Combat Mode")]
        [FormerlySerializedAs("enableFrontHeroModeInFightScene2")]
        [SerializeField] private CombatMode combatMode;
        [Tooltip("Controls shared player HP in FrontHero mode. Reaching zero does not end the gameplay session.")]
        [FormerlySerializedAs("enableHealthSystemInFightScene2")]
        [SerializeField] private bool enableHealthSystemInFrontHeroMode;
        [SerializeField, Min(1)] private int enemyAttackIntervalBeats = 4;
        [SerializeField] private HeroBeatSettings frontHero =
            new("Hero 1 - Player");
        [SerializeField] private HeroBeatSettings secondHero =
            new("Hero 2 - Player");
        [SerializeField] private HeroBeatSettings thirdHero =
            new("Hero 3 - Player");

        private readonly List<FightUnitSlot> fightScene2Enemies = new();
        private FmodBeatClock subscribedBeatClock;
        private FightInputRouter subscribedInputRouter;
        private FightRosterManager subscribedRosterManager;
        private int rosterVersion;
        private long nextActionId;
        private float partyHp;
        private bool pendingEnemyAttack;
        private bool pendingEnemyAnimationDriven;
        private FightUnitSlot pendingEnemyAttacker;
        private int pendingAttackRosterVersion = -1;
        private long pendingEnemyActionId = long.MinValue;
        private long pendingAttackGlobalBeat = long.MinValue;
        private int pendingAttackBar;
        private int pendingAttackTimelineMs;
        private long guardedGlobalBeat = long.MinValue;
        private bool battleEnded;
        private int blockedAttackCount;
        private int receivedAttackCount;
        private float totalHealingReceived;
        private long lastEnemyAttackGlobalBeat = -1;

        public event Action<FmodBeatClock.BeatSnapshot> FightBeat;
        public event Action<HeroCallResult> HeroCalled;
        public event Action<EnemyAttackResult> EnemyAttackResolved;
        public event Action<float, float> PartyHealthChanged;
        public event Action BattleLost;
        public event Action RosterRebuilt;

        public float PartyHp => partyHp;
        public float MaxPartyHp => maxPartyHp;
        public bool BattleEnded => battleEnded;
        public bool HasPendingEnemyAttack => pendingEnemyAttack;
        public int BlockedAttackCount => blockedAttackCount;
        public int ReceivedAttackCount => receivedAttackCount;
        public float TotalHealingReceived => totalHealingReceived;
        public FightUnitSlot TankSlot => tankSlot;
        public FightUnitSlot ActiveEnemySlot => activeEnemySlot;
        public CombatMode Mode => combatMode;
        public bool UsesFrontHeroControls => combatMode == CombatMode.FrontHero;
        public bool HealthSystemEnabled => !UsesFrontHeroControls || enableHealthSystemInFrontHeroMode;
        public int EnemyAttackIntervalBeats => UsesFrontHeroControls && activeEnemySlot != null
            ? activeEnemySlot.AttackIntervalBeats
            : enemyAttackIntervalBeats;
        public int EnemyCurrentMana => activeEnemySlot != null ? activeEnemySlot.CurrentMana : 0;
        public int EnemyMaxMana => activeEnemySlot != null ? activeEnemySlot.MaxMana : 0;
        public long LastEnemyAttackGlobalBeat => lastEnemyAttackGlobalBeat;
        public FightRosterManager RosterManager => rosterManager;
        public HeroBeatSettings FrontHero => frontHero;
        public HeroBeatSettings SecondHero => secondHero;
        public HeroBeatSettings ThirdHero => thirdHero;

        public void Configure(
            FmodBeatClock clock,
            FmodRhythmJudge judge,
            FightInputRouter router,
            FightUnitSlot tank = null,
            FightUnitSlot enemy = null,
            float partyHealth = 4f,
            float attackDamage = 1f)
        {
            UnsubscribeDependencies();
            beatClock = clock;
            rhythmJudge = judge;
            inputRouter = router;
            tankSlot = tank;
            activeEnemySlot = enemy;
            maxPartyHp = tankSlot != null ? tankSlot.MaxHp : Mathf.Max(0.5f, partyHealth);
            enemyAttackDamage = activeEnemySlot != null
                ? Mathf.Max(0.5f, activeEnemySlot.AttackPower)
                : Mathf.Max(0.5f, attackDamage);
            partyHp = maxPartyHp;
            SubscribeDependencies();
        }

        public void ConfigureRoster(FightRosterManager manager)
        {
            UnsubscribeDependencies();
            rosterManager = manager;
            SubscribeDependencies();
        }

        public void SetCombatMode(CombatMode mode)
        {
            combatMode = mode;
        }

        public void SetFrontHeroHealthSystemEnabled(bool enabled)
        {
            enableHealthSystemInFrontHeroMode = enabled;
        }

        public int GetEnemyBeatsUntilAttack(long globalBeat)
        {
            return GetBeatsUntilScheduledAttack(globalBeat, EnemyAttackIntervalBeats);
        }

        public bool IsEnemyAttackBeat(long globalBeat)
        {
            return IsScheduledAttackBeat(globalBeat, EnemyAttackIntervalBeats);
        }

        public bool IsScheduledAttackBeat(long globalBeat, int attackInterval)
        {
            int interval = Mathf.Max(1, attackInterval);
            return globalBeat >= 0 && (globalBeat + 1) % interval == 0;
        }

        public int GetBeatsUntilScheduledAttack(long globalBeat, int attackInterval)
        {
            int interval = Mathf.Max(1, attackInterval);
            if (globalBeat < 0)
                return interval;

            long songBeatNumber = globalBeat + 1;
            return (int)((interval - songBeatNumber % interval) % interval);
        }

        private void Awake()
        {
            if (UsesFrontHeroControls)
                RebuildRosterAndResetCombat();

            maxPartyHp = tankSlot != null ? tankSlot.MaxHp : maxPartyHp;
            partyHp = maxPartyHp;
            if (tankSlot != null)
                tankSlot.RestoreFullHealth();
        }

        private void OnEnable()
        {
            SubscribeDependencies();
        }

        private void Start()
        {
            PartyHealthChanged?.Invoke(partyHp, maxPartyHp);
        }

        private void Update()
        {
            TryResolvePendingAttack();
        }

        private void OnDisable()
        {
            UnsubscribeDependencies();
        }

        private void SubscribeDependencies()
        {
            if (!isActiveAndEnabled)
                return;

            UnsubscribeDependencies();
            subscribedBeatClock = beatClock;
            subscribedInputRouter = inputRouter;
            subscribedRosterManager = rosterManager;
            if (subscribedBeatClock != null)
                subscribedBeatClock.Beat += OnBeat;
            if (subscribedInputRouter != null)
                subscribedInputRouter.CommandStarted += SubmitHeroCommand;
            if (subscribedRosterManager != null)
                subscribedRosterManager.RosterChanged += OnRosterChanged;
        }

        private void UnsubscribeDependencies()
        {
            if (subscribedBeatClock != null)
                subscribedBeatClock.Beat -= OnBeat;
            if (subscribedInputRouter != null)
                subscribedInputRouter.CommandStarted -= SubmitHeroCommand;
            if (subscribedRosterManager != null)
                subscribedRosterManager.RosterChanged -= OnRosterChanged;
            subscribedBeatClock = null;
            subscribedInputRouter = null;
            subscribedRosterManager = null;
        }

        public void SubmitHeroCommand(FightInputRouter.HeroCommand command)
        {
            if (battleEnded)
                return;

            if (UsesFrontHeroControls)
            {
                SubmitFrontHeroCommand(command);
                return;
            }

            SubmitLegacyHeroCommand(command);
        }

        private void SubmitFrontHeroCommand(FightInputRouter.HeroCommand command)
        {
            if (command == FightInputRouter.HeroCommand.Ultimate || rhythmJudge == null)
                return;

            HeroBeatSettings hero = HeroFor(command);
            if (hero?.UnitSlot == null)
                return;

            FmodRhythmJudge.Result judgement = rhythmJudge.JudgeNow();
            bool perfect = judgement.Judgement == FmodRhythmJudge.Grade.Perfect;
            hero.UnitSlot.PlayInputFeedback(perfect);
            if (!perfect)
            {
                HeroCalled?.Invoke(new HeroCallResult(command, judgement, false, false, judgement.Message));
                return;
            }

            bool heavyBeat = judgement.NearestBeat.Beat == 4;
            PerformHeroAction(
                hero,
                heavyBeat ? ActionType.Skill : ActionType.LightAttack,
                command,
                judgement,
                heavyBeat);
        }

        private HeroBeatSettings HeroFor(FightInputRouter.HeroCommand command)
        {
            return command switch
            {
                FightInputRouter.HeroCommand.Tank => frontHero,
                FightInputRouter.HeroCommand.Support => secondHero,
                FightInputRouter.HeroCommand.Damage => thirdHero,
                _ => null
            };
        }

        private void PerformHeroAction(
            HeroBeatSettings hero,
            ActionType action,
            FightInputRouter.HeroCommand command,
            FmodRhythmJudge.Result judgement,
            bool isHeavyBeat)
        {
            FightUnitSlot actor = hero?.UnitSlot;
            if (actor == null)
                return;

            bool skill = action == ActionType.Skill;
            if (skill)
                hero.RecordSkillActivation();
            FightCharacterDefinition.AbilityBehavior behavior = skill
                ? actor.SkillBehavior
                : actor.NormalAbilityBehavior;
            float power = skill ? actor.SkillPower : actor.AttackPower;
            if (!skill)
                power = actor.NormalAbilityPower;
            string result = PerformConfiguredAbility(
                hero,
                action,
                behavior,
                power,
                judgement.NearestBeat.GlobalBeat);

            activeEnemySlot = FindFrontLivingEnemy();
            HeroCalled?.Invoke(new HeroCallResult(
                command,
                judgement,
                isHeavyBeat,
                action == ActionType.Skill,
                $"{hero.HeroLabel}: {result}."));
        }

        private string PerformConfiguredAbility(
            HeroBeatSettings hero,
            ActionType action,
            FightCharacterDefinition.AbilityBehavior behavior,
            float power,
            long globalBeat)
        {
            FightUnitSlot actor = hero.UnitSlot;
            bool skill = action == ActionType.Skill;
            power = QuantizeCombatValue(power);
            FightCharacterCombatAnimator.CombatAnimation animation = skill
                ? FightCharacterCombatAnimator.CombatAnimation.Skill
                : FightCharacterCombatAnimator.CombatAnimation.LightAttack;
            Action playEffect = skill
                ? () => actor.PlaySkillAttackAt(actor.CastEffectAnchor)
                : () => actor.PlayLightAttackAt(actor.CastEffectAnchor);
            string abilityName = skill ? hero.SkillName : "Normal Ability";

            switch (behavior)
            {
                case FightCharacterDefinition.AbilityBehavior.Guard:
                    ArmGuardForNextEnemyAttack(globalBeat);
                    PlayAnimatedHeroUtility(
                        actor,
                        FightCharacterCombatAnimator.CombatAnimation.Guard,
                        () => actor.PlayGuardAt(skill),
                        null);
                    return $"{abilityName} blocks all damage from the next enemy attack";

                case FightCharacterDefinition.AbilityBehavior.HealParty:
                    // Gameplay resolves from the accepted beat now. Animation and VFX
                    // callbacks are presentation-only and cannot delay healing.
                    HealPartyHealth(actor, power);
                    PlayAnimatedHeroUtility(
                        actor,
                        animation,
                        playEffect,
                        null);
                    return $"{abilityName} restores {power:0.#} HP";

                case FightCharacterDefinition.AbilityBehavior.DamageAll:
                    PlayAnimatedHeroAreaAction(actor, animation, action, power);
                    return $"{abilityName} deals {power:0.#} damage to every enemy";

                case FightCharacterDefinition.AbilityBehavior.GuardAndDamageFront:
                    ArmGuardForNextEnemyAttack(globalBeat);
                    actor.PlayGuardAt(skill);
                    FightUnitSlot guardedTarget = FindFrontLivingEnemy();
                    if (guardedTarget != null)
                        PlayAnimatedHeroAction(actor, guardedTarget, action, power);
                    return $"{abilityName} blocks all damage and deals {power:0.#} damage to the front enemy";

                default:
                    FightUnitSlot target = FindFrontLivingEnemy();
                    if (target == null)
                        return $"{abilityName} found no target";
                    PlayAnimatedHeroAction(actor, target, action, power);
                    return $"{abilityName} deals {power:0.#} damage to the front enemy";
            }
        }

        private void ArmGuardForNextEnemyAttack(long inputGlobalBeat)
        {
            guardedGlobalBeat = pendingEnemyAttack
                ? pendingAttackGlobalBeat
                : inputGlobalBeat + GetEnemyBeatsUntilAttack(inputGlobalBeat);
        }

        private void SubmitLegacyHeroCommand(FightInputRouter.HeroCommand command)
        {
            if (command == FightInputRouter.HeroCommand.Ultimate)
            {
                HeroCalled?.Invoke(new HeroCallResult(
                    command,
                    default,
                    false,
                    false,
                    "Ultimate (A / R) is reserved for the redesign."));
                return;
            }

            if (rhythmJudge == null)
                return;

            FmodRhythmJudge.Result judgement = rhythmJudge.JudgeNow();
            bool perfect = judgement.Judgement == FmodRhythmJudge.Grade.Perfect;
            bool heavyBeat = perfect && judgement.NearestBeat.Beat == 4;
            bool skillActivated = false;
            string message;

            if (!perfect)
            {
                message = judgement.Message;
            }
            else if (!heavyBeat)
            {
                message = "Perfect call. Skills activate only on beat 4.";
            }
            else
            {
                switch (command)
                {
                    case FightInputRouter.HeroCommand.Tank:
                        guardedGlobalBeat = judgement.NearestBeat.GlobalBeat;
                        skillActivated = true;
                        message = "Tank Guard activated.";
                        break;
                    case FightInputRouter.HeroCommand.Support:
                        message = "Support skill placeholder.";
                        break;
                    case FightInputRouter.HeroCommand.Damage:
                        message = "Damage skill placeholder.";
                        break;
                    default:
                        message = string.Empty;
                        break;
                }
            }

            HeroCalled?.Invoke(new HeroCallResult(command, judgement, heavyBeat, skillActivated, message));
        }

        private void OnBeat(FmodBeatClock.BeatSnapshot beat)
        {
            FightBeat?.Invoke(beat);

            if (battleEnded)
                return;

            if (UsesFrontHeroControls)
            {
                activeEnemySlot = FindFrontLivingEnemy();
                if (activeEnemySlot != null && IsEnemyAttackBeat(beat.GlobalBeat))
                {
                    QueueEnemyAttack(beat);
                    StartEnemyAttackAnimation();
                }

                return;
            }

            if (beat.Beat == 4)
                QueueEnemyAttack(beat);
        }

        private void QueueEnemyAttack(FmodBeatClock.BeatSnapshot beat)
        {
            pendingEnemyActionId = ++nextActionId;
            pendingAttackRosterVersion = rosterVersion;
            pendingEnemyAttack = true;
            pendingAttackGlobalBeat = beat.GlobalBeat;
            pendingAttackBar = beat.Bar;
            pendingAttackTimelineMs = beat.TimelinePositionMs;
            pendingEnemyAttacker = activeEnemySlot;
            pendingEnemyAnimationDriven = false;
        }

        private void StartEnemyAttackAnimation()
        {
            if (!UsesFrontHeroControls || pendingEnemyAttacker == null)
                return;

            FightUnitSlot attacker = pendingEnemyAttacker;
            bool hasSequence = attacker.CombatAnimator != null &&
                               attacker.CombatAnimator.HasSequence(
                                   FightCharacterCombatAnimator.CombatAnimation.NormalAttack);
            // Enemy damage is resolved by the beat/judgement window in Update. The
            // animation damage frame remains available to artists but has no gameplay authority.
            pendingEnemyAnimationDriven = false;
            if (hasSequence)
            {
                int attackRosterVersion = pendingAttackRosterVersion;
                long actionId = pendingEnemyActionId;
                bool started = attacker.PlayCombatAnimation(
                    FightCharacterCombatAnimator.CombatAnimation.NormalAttack,
                    () => PlayEnemyAttackWarning(attackRosterVersion, actionId, attacker),
                    () => PlayEnemyAttackEffect(attackRosterVersion, actionId, attacker),
                    null);
                if (started)
                    return;
            }

            attacker.PlayLightAttackAt(tankSlot != null ? tankSlot.ImpactEffectAnchor : null);
        }

        private void TryResolvePendingAttack()
        {
            if (!pendingEnemyAttack || pendingEnemyAnimationDriven || beatClock == null || rhythmJudge == null ||
                !beatClock.TryGetTimelinePositionMs(out int timelineMs))
            {
                return;
            }

            float lateWindowMs = Mathf.Max(0f, rhythmJudge.PerfectWindowMs - rhythmJudge.JudgementOffsetMs);
            if (timelineMs < pendingAttackTimelineMs + lateWindowMs + resolutionSafetyMs)
                return;

            ResolvePendingEnemyAttack(pendingAttackRosterVersion, pendingEnemyActionId, pendingEnemyAttacker);
        }

        private void ResolvePendingEnemyAttack(
            int expectedRosterVersion,
            long expectedActionId,
            FightUnitSlot expectedAttacker)
        {
            if (!IsCurrentPendingEnemyAttack(expectedRosterVersion, expectedActionId, expectedAttacker))
                return;

            // The attack animation may reach its damage frame before the late half of
            // the Perfect window closes. Keep the pending action alive so a valid
            // fourth-beat guard is accepted across the complete judgement window.
            if (pendingEnemyAnimationDriven && !HasEnemyResolutionWindowElapsed())
            {
                pendingEnemyAnimationDriven = false;
                return;
            }

            bool blocked = guardedGlobalBeat == pendingAttackGlobalBeat;
            FightUnitSlot attacker = pendingEnemyAttacker != null ? pendingEnemyAttacker : activeEnemySlot;
            float configuredDamage = UsesFrontHeroControls && attacker != null
                ? Mathf.Max(0.5f, attacker.AttackPower)
                : enemyAttackDamage;
            float damage = blocked || !HealthSystemEnabled ? 0f : QuantizeCombatValue(configuredDamage);

            if (blocked)
                blockedAttackCount++;
            else
                receivedAttackCount++;

            // Enemy skills are intentionally controlled elsewhere. Scheduled enemy turns
            // always remain normal attacks, but still bank one mana up to the prefab limit.
            if (UsesFrontHeroControls && attacker != null)
                attacker.GainMana();
            lastEnemyAttackGlobalBeat = pendingAttackGlobalBeat;

            if (HealthSystemEnabled)
            {
                partyHp = Mathf.Max(0f, partyHp - damage);
                if (!blocked && tankSlot != null)
                {
                    tankSlot.PlayCombatAnimation(
                        FightCharacterCombatAnimator.CombatAnimation.Hit,
                        null,
                        null,
                        null);
                }
            }
            else
            {
                partyHp = maxPartyHp;
            }

            pendingEnemyAttack = false;
            pendingEnemyAnimationDriven = false;
            pendingEnemyAttacker = null;
            pendingAttackRosterVersion = -1;
            pendingEnemyActionId = long.MinValue;
            EnemyAttackResolved?.Invoke(new EnemyAttackResult(
                pendingAttackGlobalBeat,
                pendingAttackBar,
                blocked,
                damage,
                partyHp));
            PartyHealthChanged?.Invoke(partyHp, maxPartyHp);

            // FightScene2 is an open-ended gameplay lab. HP never ends the session,
            // even if the health toggle is temporarily enabled for comparison.
            if (UsesFrontHeroControls)
                return;

            if (partyHp > 0)
                return;

            battleEnded = true;
            BattleLost?.Invoke();
        }

        private bool HasEnemyResolutionWindowElapsed()
        {
            if (beatClock == null || rhythmJudge == null ||
                !beatClock.TryGetTimelinePositionMs(out int timelineMs))
            {
                return true;
            }

            float lateWindowMs = Mathf.Max(0f, rhythmJudge.PerfectWindowMs - rhythmJudge.JudgementOffsetMs);
            return timelineMs >= pendingAttackTimelineMs + lateWindowMs + resolutionSafetyMs;
        }

        private void RebuildRosterAndResetCombat()
        {
            List<FightUnitSlot> heroes = new();
            fightScene2Enemies.Clear();

            if (rosterManager != null)
            {
                heroes.AddRange(rosterManager.ActiveHeroes);
                fightScene2Enemies.AddRange(rosterManager.ActiveEnemies);
            }
            else
            {
                FightUnitSlot[] allSlots = FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None);
                foreach (FightUnitSlot slot in allSlots)
                {
                    if (slot.gameObject.scene != gameObject.scene || !slot.HasCharacter)
                        continue;

                    if (slot.Team == FightUnitSlot.UnitTeam.Hero)
                        heroes.Add(slot);
                    else
                        fightScene2Enemies.Add(slot);
                }
            }

            heroes.Sort((left, right) => left.SlotIndex.CompareTo(right.SlotIndex));
            fightScene2Enemies.Sort((left, right) => right.SlotIndex.CompareTo(left.SlotIndex));

            rosterVersion++;
            frontHero.BindUnitSlot(SlotAt(heroes, 0), true);
            secondHero.BindUnitSlot(SlotAt(heroes, 1), true);
            thirdHero.BindUnitSlot(SlotAt(heroes, 2), true);
            tankSlot = frontHero.UnitSlot;
            activeEnemySlot = FindFrontLivingEnemy();
            enemyAttackIntervalBeats = Mathf.Max(1, enemyAttackIntervalBeats);
            ResetBattleStateForRoster();
        }

        private void ResetBattleStateForRoster()
        {
            pendingEnemyAttack = false;
            pendingEnemyAnimationDriven = false;
            pendingEnemyAttacker = null;
            pendingAttackRosterVersion = -1;
            pendingEnemyActionId = long.MinValue;
            pendingAttackGlobalBeat = long.MinValue;
            pendingAttackBar = 0;
            pendingAttackTimelineMs = 0;
            guardedGlobalBeat = long.MinValue;
            battleEnded = false;
            blockedAttackCount = 0;
            receivedAttackCount = 0;
            totalHealingReceived = 0f;
            lastEnemyAttackGlobalBeat = -1;

            maxPartyHp = tankSlot != null ? tankSlot.MaxHp : Mathf.Max(0.5f, maxPartyHp);
            if (tankSlot != null)
                tankSlot.RestoreFullHealth();
            partyHp = maxPartyHp;
        }

        private void OnRosterChanged()
        {
            if (!UsesFrontHeroControls)
                return;

            RebuildRosterAndResetCombat();
            RosterRebuilt?.Invoke();
            PartyHealthChanged?.Invoke(partyHp, maxPartyHp);
        }

        private FightUnitSlot FindFrontLivingEnemy()
        {
            for (int i = 0; i < fightScene2Enemies.Count; i++)
            {
                FightUnitSlot slot = fightScene2Enemies[i];
                if (slot != null && slot.CurrentHp > 0)
                    return slot;
            }

            return null;
        }

        private static void PlayActionVisual(FightUnitSlot slot, ActionType action)
        {
            if (slot == null)
                return;

            switch (action)
            {
                case ActionType.HeavyAttack:
                    slot.PlayHeavyAttack();
                    break;
                case ActionType.Skill:
                    slot.PlaySkillAttack();
                    break;
                default:
                    slot.PlayLightAttack();
                    break;
            }
        }

        private void PlayHeroAttackWarning(int expectedRosterVersion, FightUnitSlot attacker)
        {
            if (IsCurrentHero(expectedRosterVersion, attacker))
                attacker.PlayAttackFrameWarning(false);
        }

        private void PlayHeroActionVisual(
            int expectedRosterVersion,
            FightUnitSlot attacker,
            FightUnitSlot target,
            ActionType action)
        {
            if (!IsCurrentHeroAction(expectedRosterVersion, attacker, target))
                return;

            if (action == ActionType.Skill)
                attacker.PlaySkillAttackAt(target.ImpactEffectAnchor);
            else
                attacker.PlayLightAttackAt(target.ImpactEffectAnchor);
        }

        private void PlayHeroAreaVisual(
            int expectedRosterVersion,
            FightUnitSlot attacker,
            ActionType action)
        {
            if (!IsCurrentHero(expectedRosterVersion, attacker))
                return;

            List<Transform> anchors = new();
            foreach (FightUnitSlot target in fightScene2Enemies)
            {
                if (target != null && target.HasCharacter)
                    anchors.Add(target.ImpactEffectAnchor);
            }

            if (action == ActionType.Skill)
                attacker.PlaySkillAttackAt(anchors.ToArray());
            else
                attacker.PlayLightAttackAt(anchors.ToArray());
        }

        private void PlayEnemyAttackWarning(
            int expectedRosterVersion,
            long expectedActionId,
            FightUnitSlot attacker)
        {
            if (IsCurrentPendingEnemyAttack(expectedRosterVersion, expectedActionId, attacker))
                attacker.PlayAttackFrameWarning(true);
        }

        private void PlayEnemyAttackEffect(
            int expectedRosterVersion,
            long expectedActionId,
            FightUnitSlot attacker)
        {
            if (IsCurrentPendingEnemyAttack(expectedRosterVersion, expectedActionId, attacker))
                attacker.PlayLightAttackAt(tankSlot != null ? tankSlot.ImpactEffectAnchor : null);
        }

        private bool IsCurrentHeroAction(
            int expectedRosterVersion,
            FightUnitSlot attacker,
            FightUnitSlot target)
        {
            return IsCurrentHero(expectedRosterVersion, attacker) &&
                   target != null &&
                   target.HasCharacter &&
                   fightScene2Enemies.Contains(target);
        }

        private bool IsCurrentHero(int expectedRosterVersion, FightUnitSlot attacker)
        {
            return expectedRosterVersion == rosterVersion &&
                   attacker != null &&
                   attacker.HasCharacter &&
                   (frontHero.UnitSlot == attacker ||
                    secondHero.UnitSlot == attacker ||
                    thirdHero.UnitSlot == attacker);
        }

        private bool IsCurrentPendingEnemyAttack(
            int expectedRosterVersion,
            long expectedActionId,
            FightUnitSlot expectedAttacker)
        {
            if (!pendingEnemyAttack ||
                expectedRosterVersion != rosterVersion ||
                expectedRosterVersion != pendingAttackRosterVersion ||
                expectedActionId != pendingEnemyActionId ||
                expectedAttacker != pendingEnemyAttacker)
            {
                return false;
            }

            return !UsesFrontHeroControls ||
                   (expectedAttacker != null &&
                    expectedAttacker.HasCharacter &&
                    fightScene2Enemies.Contains(expectedAttacker));
        }

        private void PlayAnimatedHeroAction(
            FightUnitSlot attacker,
            FightUnitSlot target,
            ActionType action,
            float damage)
        {
            if (attacker == null)
                return;

            FightCharacterCombatAnimator.CombatAnimation animation = action switch
            {
                ActionType.HeavyAttack => FightCharacterCombatAnimator.CombatAnimation.HeavyAttack,
                ActionType.Skill => FightCharacterCombatAnimator.CombatAnimation.Skill,
                _ => FightCharacterCombatAnimator.CombatAnimation.LightAttack
            };
            int actionRosterVersion = rosterVersion;
            // The rhythm judgement owns damage timing. Resolve before starting any
            // animation so projectile speed, effect lifetime, and frame events are visual-only.
            ApplyHeroDamage(actionRosterVersion, attacker, target, damage);
            bool animated = attacker.PlayCombatAnimation(
                animation,
                () => PlayHeroAttackWarning(actionRosterVersion, attacker),
                () => PlayHeroActionVisual(actionRosterVersion, attacker, target, action),
                null);
            if (animated)
                return;

            PlayHeroActionVisual(actionRosterVersion, attacker, target, action);
        }

        private void PlayAnimatedHeroAreaAction(
            FightUnitSlot attacker,
            FightCharacterCombatAnimator.CombatAnimation animation,
            ActionType action,
            float damage)
        {
            int actionRosterVersion = rosterVersion;
            ApplyAreaDamage(actionRosterVersion, attacker, damage);
            bool animated = attacker.PlayCombatAnimation(
                animation,
                () => PlayHeroAttackWarning(actionRosterVersion, attacker),
                () => PlayHeroAreaVisual(actionRosterVersion, attacker, action),
                null);
            if (animated)
                return;

            PlayHeroAreaVisual(actionRosterVersion, attacker, action);
        }

        private void PlayAnimatedHeroUtility(
            FightUnitSlot actor,
            FightCharacterCombatAnimator.CombatAnimation animation,
            Action onEffect,
            Action onResolve)
        {
            if (actor == null)
                return;

            int actionRosterVersion = rosterVersion;
            Action guardedEffect = () =>
            {
                if (IsCurrentHero(actionRosterVersion, actor))
                    onEffect?.Invoke();
            };
            Action guardedResolution = () =>
            {
                if (IsCurrentHero(actionRosterVersion, actor))
                    onResolve?.Invoke();
            };
            bool animated = actor.PlayCombatAnimation(
                animation,
                () => PlayHeroAttackWarning(actionRosterVersion, actor),
                guardedEffect,
                guardedResolution);
            if (animated)
                return;

            guardedEffect();
            guardedResolution();
        }

        private void HealPartyHealth(FightUnitSlot healer, float amount)
        {
            if (healer == null || amount <= 0f)
                return;

            float previous = partyHp;
            partyHp = Mathf.Min(maxPartyHp, partyHp + QuantizeCombatValue(amount));
            totalHealingReceived += partyHp - previous;
            PartyHealthChanged?.Invoke(partyHp, maxPartyHp);
            Debug.Log($"[FightParty] {healer.DisplayName} healed {partyHp - previous:0.#} HP.", this);
        }

        private void ApplyAreaDamage(int expectedRosterVersion, FightUnitSlot attacker, float damage)
        {
            if (!IsCurrentHero(expectedRosterVersion, attacker) || !HealthSystemEnabled)
                return;

            foreach (FightUnitSlot target in fightScene2Enemies)
            {
                if (target == null || !target.HasCharacter || target.CurrentHp <= 0f)
                    continue;
                target.TakeDamage(damage);
                target.PlayCombatAnimation(
                    FightCharacterCombatAnimator.CombatAnimation.Hit,
                    null,
                    null,
                    null);
            }
            activeEnemySlot = FindFrontLivingEnemy();
        }

        private void ApplyHeroDamage(
            int expectedRosterVersion,
            FightUnitSlot attacker,
            FightUnitSlot target,
            float damage)
        {
            if (!IsCurrentHeroAction(expectedRosterVersion, attacker, target))
                return;

            if (HealthSystemEnabled && target != null && target.HasCharacter)
            {
                target.TakeDamage(damage);
                target.PlayCombatAnimation(
                    FightCharacterCombatAnimator.CombatAnimation.Hit,
                    null,
                    null,
                    null);
            }
            activeEnemySlot = FindFrontLivingEnemy();
        }

        private static float QuantizeCombatValue(float value)
        {
            return value <= 0f ? 0f : Mathf.Max(0.5f, Mathf.Round(value * 2f) * 0.5f);
        }

        private static FightUnitSlot SlotAt(IReadOnlyList<FightUnitSlot> slots, int index)
        {
            return slots != null && index >= 0 && index < slots.Count ? slots[index] : null;
        }

        private static string FormatAction(ActionType action)
        {
            return action switch
            {
                ActionType.HeavyAttack => "Heavy Attack",
                ActionType.Guard => "Guard",
                ActionType.Skill => "Skill",
                _ => "Light Attack"
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            enemyAttackIntervalBeats = Mathf.Max(1, enemyAttackIntervalBeats);
            maxPartyHp = QuantizeCombatValue(maxPartyHp);
            enemyAttackDamage = QuantizeCombatValue(enemyAttackDamage);
            frontHero?.RefreshRuntimeValues(false);
            secondHero?.RefreshRuntimeValues(false);
            thirdHero?.RefreshRuntimeValues(false);
        }
#endif
    }
}
