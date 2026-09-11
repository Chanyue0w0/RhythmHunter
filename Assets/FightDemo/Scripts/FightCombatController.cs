using System;
using System.Collections.Generic;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Runs the original fourth-beat guard prototype in FightScene and the front-hero
    /// gameplay validation rules in FightScene2.
    /// </summary>
    public sealed class FightCombatController : MonoBehaviour
    {
        [Serializable]
        public sealed class HeroBeatSettings
        {
            [SerializeField] private string heroLabel = "Hero";
            [SerializeField] private FightUnitSlot unitSlot;
            [SerializeField] private bool playerControlled;

            [Header("Attack Beat")]
            [Tooltip("Number of musical beats between automatic attacks. The player-controlled hero attacks from input instead.")]
            [SerializeField, Min(1)] private int attackIntervalBeats = 2;

            [Header("Mana")]
            [SerializeField, Min(1)] private int maxMana = 4;
            [SerializeField, Min(0)] private int startingMana;
            [Tooltip("Runtime mana. A successful light attack adds one point.")]
            [SerializeField, Min(0)] private int currentMana;

            [Header("Skill")]
            [SerializeField] private string skillName = "Beat Skill";
            [SerializeField, Min(0.1f)] private float lightAttackMultiplier = 1f;
            [SerializeField, Min(0.1f)] private float heavyAttackMultiplier = 1.75f;
            [SerializeField, Min(0.1f)] private float skillDamageMultiplier = 3f;
            [Tooltip("Runtime count used for gameplay validation.")]
            [SerializeField, Min(0)] private int skillActivationCount;
            [Tooltip("Global beat of the latest scheduled automatic attack.")]
            [SerializeField] private long lastAutomaticAttackGlobalBeat = -1;

            public HeroBeatSettings()
            {
            }

            public HeroBeatSettings(
                string label,
                bool isPlayerControlled,
                int intervalBeats,
                int manaCapacity,
                string abilityName,
                float skillMultiplier)
            {
                heroLabel = label;
                playerControlled = isPlayerControlled;
                attackIntervalBeats = Mathf.Max(1, intervalBeats);
                maxMana = Mathf.Max(1, manaCapacity);
                skillName = abilityName;
                skillDamageMultiplier = Mathf.Max(0.1f, skillMultiplier);
            }

            public string HeroLabel => heroLabel;
            public FightUnitSlot UnitSlot => unitSlot;
            public bool PlayerControlled => playerControlled;
            public int AttackIntervalBeats => unitSlot != null && unitSlot.HasCharacter
                ? unitSlot.AttackIntervalBeats
                : attackIntervalBeats;
            public int MaxMana => maxMana;
            public int CurrentMana => currentMana;
            public string SkillName => unitSlot?.CharacterDefinition != null
                ? unitSlot.CharacterDefinition.SkillName
                : skillName;
            public bool SkillReady => currentMana >= maxMana;
            public int SkillActivationCount => skillActivationCount;
            public long LastAutomaticAttackGlobalBeat => lastAutomaticAttackGlobalBeat;

            internal void BindUnitSlot(FightUnitSlot slot, bool resetRuntime)
            {
                unitSlot = slot;
                RefreshRuntimeValues(resetRuntime);
            }

            internal void RefreshRuntimeValues(bool resetRuntime)
            {
                if (unitSlot != null && unitSlot.HasCharacter)
                {
                    heroLabel = unitSlot.DisplayName;
                    attackIntervalBeats = unitSlot.AttackIntervalBeats;
                    maxMana = unitSlot.MaxMana;
                }

                attackIntervalBeats = Mathf.Max(1, attackIntervalBeats);
                maxMana = Mathf.Max(1, maxMana);
                startingMana = Mathf.Clamp(startingMana, 0, maxMana);
                currentMana = resetRuntime ? startingMana : Mathf.Clamp(currentMana, 0, maxMana);
                if (resetRuntime)
                {
                    skillActivationCount = 0;
                    lastAutomaticAttackGlobalBeat = -1;
                }
                lightAttackMultiplier = Mathf.Max(0.1f, lightAttackMultiplier);
                heavyAttackMultiplier = Mathf.Max(0.1f, heavyAttackMultiplier);
                skillDamageMultiplier = Mathf.Max(0.1f, skillDamageMultiplier);
            }

            internal bool IsScheduledAttackBeat(long globalBeat)
            {
                return !playerControlled && globalBeat >= 0 && (globalBeat + 1) % AttackIntervalBeats == 0;
            }

            internal int DamageFor(ActionType action)
            {
                if (unitSlot == null)
                    return 0;

                float multiplier = action switch
                {
                    ActionType.HeavyAttack => heavyAttackMultiplier,
                    ActionType.Skill => skillDamageMultiplier,
                    _ => lightAttackMultiplier
                };
                if (action == ActionType.Skill && unitSlot.SkillDamage > 0)
                    return unitSlot.SkillDamage;
                return Mathf.Max(1, Mathf.RoundToInt(unitSlot.AttackPower * multiplier));
            }

            internal void GainLightAttackMana()
            {
                currentMana = Mathf.Min(maxMana, currentMana + 1);
            }

            internal void ConsumeSkillMana()
            {
                currentMana = 0;
                skillActivationCount++;
            }

            internal void MarkAutomaticAttack(long globalBeat)
            {
                lastAutomaticAttackGlobalBeat = globalBeat;
            }
        }

        public enum ActionType
        {
            LightAttack,
            HeavyAttack,
            Guard,
            Skill
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
            public EnemyAttackResult(long globalBeat, int bar, bool blocked, int damage, int partyHp)
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
            public int Damage { get; }
            public int PartyHp { get; }
        }

        [Header("Dependencies")]
        [SerializeField] private FmodBeatClock beatClock;
        [SerializeField] private FmodRhythmJudge rhythmJudge;
        [SerializeField] private FightInputRouter inputRouter;
        [SerializeField] private FightRosterManager rosterManager;
        [SerializeField] private FightUnitSlot tankSlot;
        [SerializeField] private FightUnitSlot activeEnemySlot;

        [Header("Prototype Rules")]
        [SerializeField, Min(1)] private int maxPartyHp = 5;
        [SerializeField, Min(1)] private int enemyAttackDamage = 1;
        [SerializeField, Min(0f)] private float resolutionSafetyMs = 12f;

        [Header("FightScene2 - Front Hero Controls")]
        [Tooltip("Automatically activates in a scene named FightScene2. Disable this to compare against the original controls.")]
        [SerializeField] private bool enableFrontHeroModeInFightScene2 = true;
        [Tooltip("Disabled for the current gameplay test. Attacks still resolve and play feedback without changing HP.")]
        [SerializeField] private bool enableHealthSystemInFightScene2;
        [SerializeField, Min(1)] private int enemyAttackIntervalBeats = 4;
        [SerializeField] private HeroBeatSettings frontHero =
            new("Paladin - Player", true, 1, 4, "Radiant Smite", 3f);
        [SerializeField] private HeroBeatSettings secondHero =
            new("Bard - Auto", false, 2, 4, "Final Chorus", 2.5f);
        [SerializeField] private HeroBeatSettings thirdHero =
            new("Mage - Auto", false, 4, 3, "Arcane Burst", 3.5f);

        private readonly List<FightUnitSlot> fightScene2Enemies = new();
        private int rosterVersion;
        private long nextActionId;
        private int partyHp;
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
        private long lastEnemyAttackGlobalBeat = -1;

        public event Action<FmodBeatClock.BeatSnapshot> FightBeat;
        public event Action<HeroCallResult> HeroCalled;
        public event Action<EnemyAttackResult> EnemyAttackResolved;
        public event Action<int, int> PartyHealthChanged;
        public event Action BattleLost;

        public int PartyHp => partyHp;
        public int MaxPartyHp => maxPartyHp;
        public bool BattleEnded => battleEnded;
        public bool HasPendingEnemyAttack => pendingEnemyAttack;
        public int BlockedAttackCount => blockedAttackCount;
        public int ReceivedAttackCount => receivedAttackCount;
        public FightUnitSlot TankSlot => tankSlot;
        public FightUnitSlot ActiveEnemySlot => activeEnemySlot;
        public bool UsesFrontHeroControls => enableFrontHeroModeInFightScene2 && gameObject.scene.name == "FightScene2";
        public bool HealthSystemEnabled => !UsesFrontHeroControls || enableHealthSystemInFightScene2;
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
            int partyHealth = 5,
            int attackDamage = 1)
        {
            beatClock = clock;
            rhythmJudge = judge;
            inputRouter = router;
            tankSlot = tank;
            activeEnemySlot = enemy;
            maxPartyHp = tankSlot != null ? tankSlot.MaxHp : Mathf.Max(1, partyHealth);
            enemyAttackDamage = activeEnemySlot != null
                ? Mathf.Max(1, activeEnemySlot.AttackPower)
                : Mathf.Max(1, attackDamage);
            partyHp = maxPartyHp;
        }

        public void ConfigureRoster(FightRosterManager manager)
        {
            rosterManager = manager;
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
            if (beatClock != null)
                beatClock.Beat += OnBeat;

            if (inputRouter != null)
                inputRouter.CommandStarted += SubmitHeroCommand;

            if (rosterManager != null)
                rosterManager.RosterChanged += OnRosterChanged;
        }

        private void Start()
        {
            if (UsesFrontHeroControls)
            {
                RefreshFightScene2Labels();
                RefreshFightScene2HealthVisibility();
            }

            PartyHealthChanged?.Invoke(partyHp, maxPartyHp);
        }

        private void Update()
        {
            TryResolvePendingAttack();
        }

        private void OnDisable()
        {
            if (beatClock != null)
                beatClock.Beat -= OnBeat;

            if (inputRouter != null)
                inputRouter.CommandStarted -= SubmitHeroCommand;

            if (rosterManager != null)
                rosterManager.RosterChanged -= OnRosterChanged;
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

            FmodRhythmJudge.Result judgement = rhythmJudge.JudgeNow();
            if (judgement.Judgement != FmodRhythmJudge.Grade.Perfect)
            {
                HeroCalled?.Invoke(new HeroCallResult(command, judgement, false, false, judgement.Message));
                return;
            }

            switch (command)
            {
                case FightInputRouter.HeroCommand.Tank:
                    PerformHeroAttack(frontHero, ActionType.LightAttack, command, judgement);
                    break;
                case FightInputRouter.HeroCommand.Support:
                    PerformHeroAttack(frontHero, ActionType.HeavyAttack, command, judgement);
                    break;
                case FightInputRouter.HeroCommand.Damage:
                    // A valid guard entered during the short animation-driven attack window
                    // belongs to that pending attack beat even if nearest-beat rounding has
                    // already crossed the musical boundary.
                    guardedGlobalBeat = pendingEnemyAttack
                        ? pendingAttackGlobalBeat
                        : judgement.NearestBeat.GlobalBeat;
                    if (frontHero.UnitSlot != null &&
                        !frontHero.UnitSlot.PlayCombatAnimation(
                            FightCharacterCombatAnimator.CombatAnimation.Guard,
                            null,
                            frontHero.UnitSlot.PlayGuard,
                            null))
                    {
                        frontHero.UnitSlot.PlayGuard();
                    }
                    HeroCalled?.Invoke(new HeroCallResult(
                        command,
                        judgement,
                        false,
                        false,
                        $"Guard ready on beat {judgement.NearestBeat.Beat}."));
                    break;
            }
        }

        private void PerformHeroAttack(
            HeroBeatSettings hero,
            ActionType requestedAction,
            FightInputRouter.HeroCommand command,
            FmodRhythmJudge.Result judgement)
        {
            FightUnitSlot target = FindFrontLivingEnemy();
            if (hero?.UnitSlot == null || target == null)
                return;

            ActionType resolvedAction = hero.SkillReady ? ActionType.Skill : requestedAction;
            int damage = hero.DamageFor(resolvedAction);
            PlayAnimatedHeroAction(hero.UnitSlot, target, resolvedAction, damage);

            if (resolvedAction == ActionType.Skill)
                hero.ConsumeSkillMana();
            else if (resolvedAction == ActionType.LightAttack)
                hero.GainLightAttackMana();

            activeEnemySlot = FindFrontLivingEnemy();
            string actionName = resolvedAction == ActionType.Skill ? hero.SkillName : FormatAction(resolvedAction);
            string impact = HealthSystemEnabled ? $"dealt {damage}" : $"power {damage} (HP disabled)";
            HeroCalled?.Invoke(new HeroCallResult(
                command,
                judgement,
                requestedAction == ActionType.HeavyAttack,
                resolvedAction == ActionType.Skill,
                $"{actionName} {impact}. Mana {hero.CurrentMana}/{hero.MaxMana}."));
        }

        private void PerformAutomaticAttack(HeroBeatSettings hero, long globalBeat)
        {
            FightUnitSlot target = FindFrontLivingEnemy();
            if (hero?.UnitSlot == null || target == null || hero.UnitSlot.CurrentHp <= 0)
                return;

            ActionType action = hero.SkillReady ? ActionType.Skill : ActionType.LightAttack;
            int damage = hero.DamageFor(action);
            PlayAnimatedHeroAction(hero.UnitSlot, target, action, damage);

            if (action == ActionType.Skill)
                hero.ConsumeSkillMana();
            else
                hero.GainLightAttackMana();
            hero.MarkAutomaticAttack(globalBeat);

            activeEnemySlot = FindFrontLivingEnemy();
            string impact = HealthSystemEnabled ? $"dealt {damage}" : $"power {damage} (HP disabled)";
            Debug.Log(
                $"[FightScene2] {hero.HeroLabel}: {(action == ActionType.Skill ? hero.SkillName : "Light Attack")} " +
                $"{impact}. Mana {hero.CurrentMana}/{hero.MaxMana}.",
                this);
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
                if (secondHero.IsScheduledAttackBeat(beat.GlobalBeat))
                    PerformAutomaticAttack(secondHero, beat.GlobalBeat);
                if (thirdHero.IsScheduledAttackBeat(beat.GlobalBeat))
                    PerformAutomaticAttack(thirdHero, beat.GlobalBeat);

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
            pendingEnemyAnimationDriven = hasSequence;
            if (hasSequence)
            {
                int attackRosterVersion = pendingAttackRosterVersion;
                long actionId = pendingEnemyActionId;
                bool started = attacker.PlayCombatAnimation(
                    FightCharacterCombatAnimator.CombatAnimation.NormalAttack,
                    () => PlayEnemyAttackWarning(attackRosterVersion, actionId, attacker),
                    () => PlayEnemyAttackEffect(attackRosterVersion, actionId, attacker),
                    () => ResolvePendingEnemyAttack(attackRosterVersion, actionId, attacker));
                if (started)
                    return;

                pendingEnemyAnimationDriven = false;
            }

            attacker.PlayImmediateNormalAttack();
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

            bool blocked = guardedGlobalBeat == pendingAttackGlobalBeat;
            FightUnitSlot attacker = pendingEnemyAttacker != null ? pendingEnemyAttacker : activeEnemySlot;
            int configuredDamage = UsesFrontHeroControls && attacker != null
                ? Mathf.Max(1, attacker.AttackPower)
                : enemyAttackDamage;
            int damage = blocked || !HealthSystemEnabled ? 0 : configuredDamage;

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
                partyHp = tankSlot != null ? tankSlot.CurrentHp : Mathf.Max(0, partyHp - damage);
                if (!blocked && tankSlot != null)
                {
                    tankSlot.TakeDamage(damage);
                    tankSlot.PlayCombatAnimation(
                        FightCharacterCombatAnimator.CombatAnimation.Hit,
                        null,
                        null,
                        null);
                    partyHp = tankSlot.CurrentHp;
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
            lastEnemyAttackGlobalBeat = -1;

            maxPartyHp = tankSlot != null ? tankSlot.MaxHp : Mathf.Max(1, maxPartyHp);
            if (tankSlot != null)
                tankSlot.RestoreFullHealth();
            partyHp = maxPartyHp;
        }

        private void OnRosterChanged()
        {
            if (!UsesFrontHeroControls)
                return;

            RebuildRosterAndResetCombat();
            RefreshFightScene2Labels();
            RefreshFightScene2HealthVisibility();
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

        private void RefreshFightScene2Labels()
        {
            SetRoleLabel(frontHero.UnitSlot, "PLAYER  •  Q/X LIGHT  W/Y HEAVY  E/B GUARD");
            SetRoleLabel(secondHero.UnitSlot, $"AUTO  •  EVERY {secondHero.AttackIntervalBeats} BEATS");
            SetRoleLabel(thirdHero.UnitSlot, $"AUTO  •  EVERY {thirdHero.AttackIntervalBeats} BEATS");
        }

        private void RefreshFightScene2HealthVisibility()
        {
            FightUnitSlot[] slots = FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None);
            foreach (FightUnitSlot slot in slots)
            {
                if (slot.gameObject.scene == gameObject.scene)
                    slot.SetHealthDisplayVisible(enableHealthSystemInFightScene2);
            }

            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (Transform candidate in transforms)
            {
                if (candidate.gameObject.scene != gameObject.scene)
                    continue;

                if (candidate.name == "TankHealth" || candidate.name == "TankHealthBar")
                    candidate.gameObject.SetActive(enableHealthSystemInFightScene2);
            }
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
            ActionType action)
        {
            if (IsCurrentHero(expectedRosterVersion, attacker))
                PlayActionVisual(attacker, action);
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
                attacker.PlayImmediateNormalAttack();
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
            int damage)
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
            bool animated = attacker.PlayCombatAnimation(
                animation,
                () => PlayHeroAttackWarning(actionRosterVersion, attacker),
                () => PlayHeroActionVisual(actionRosterVersion, attacker, action),
                () => ApplyHeroDamage(actionRosterVersion, attacker, target, damage));
            if (animated)
                return;

            PlayActionVisual(attacker, action);
            ApplyHeroDamage(actionRosterVersion, attacker, target, damage);
        }

        private void ApplyHeroDamage(
            int expectedRosterVersion,
            FightUnitSlot attacker,
            FightUnitSlot target,
            int damage)
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

        private static void SetRoleLabel(FightUnitSlot slot, string value)
        {
            if (slot == null)
                return;

            TextMesh[] labels = slot.GetComponentsInChildren<TextMesh>(true);
            foreach (TextMesh label in labels)
            {
                if (label.name == "RoleAndInput")
                {
                    label.text = value;
                    return;
                }
            }
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
            frontHero?.RefreshRuntimeValues(false);
            secondHero?.RefreshRuntimeValues(false);
            thirdHero?.RefreshRuntimeValues(false);
        }
#endif
    }
}
