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
    public sealed partial class FightCombatController : MonoBehaviour
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
            FrontHero,
            EqualBeat
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
        [Header("Battle Testing")]
        [Tooltip("In prepared party battles, end combat when shared HP reaches zero. Disable to keep testing at 0 HP. Does not restart an already ended battle.")]
        [SerializeField] private bool endBattleOnZeroHp = true;
        public bool EndBattleOnZeroHp => endBattleOnZeroHp;
        public FmodBeatClock BeatClock => beatClock;

        [Header("Battle Entrance")]
        [SerializeField] private bool autoPlayBattleEntrance;
        [Tooltip("開場先播放普通 Idle 與喝酒；關閉時跳過這段演出，保留音樂延遲與角色滑入。下次進場生效。")]
        [SerializeField] private bool playOpeningCharacterAnimations = true;
        [Tooltip("Seconds before music starts, independent of Opening Idle and Drink. Battle controls unlock after the entrance animation and music start.")]
        [UnityEngine.Serialization.FormerlySerializedAs("minimumEntranceSeconds")]
        [SerializeField, Min(0f)] private float musicStartDelaySeconds = 0f;
        public bool AutoPlayBattleEntrance => autoPlayBattleEntrance;
        private float entranceElapsed;
        private bool entrancePending;
        private bool entranceMusicStarted, entranceDrinking;
        private double entranceIdleDuration;
        private FightWaveEntrance waveEntrance;
        public bool WaveEntranceRunning => waveEntrance != null && waveEntrance.IsRunning;
        public bool WaveTransitionActive => AwaitingNextWave || WaveEntranceRunning;
        public bool BattleEntranceRunning => entrancePending || WaveEntranceRunning;

        private void BeginBattleEntrance()
        {
            if (!autoPlayBattleEntrance || !AwaitingBattleStart) return;
            entrancePending = true;
            entranceElapsed = 0;
            entranceMusicStarted = entranceDrinking = false;
            beatClock?.SetAutomaticPlayback(false);
            beatClock?.PrepareOpeningTempo();
            if (musicStartDelaySeconds <= 0f) StartEntranceMusic();
            entranceIdleDuration = 0;
            if (!playOpeningCharacterAnimations) return;
            foreach (var hero in rosterManager.ActiveHeroes)
            {
                hero.CombatAnimator?.BeginOpeningIdle();
                if (hero.CombatAnimator != null)
                    entranceIdleDuration = Math.Max(entranceIdleDuration,
                        hero.CombatAnimator.GetSequenceDurationSeconds(FightCharacterCombatAnimator.CombatAnimation.OpeningIdle));
            }
        }

        private void StartEntranceMusic()
        {
            if (entranceMusicStarted) return;
            beatClock?.RestartMusic();
            entranceMusicStarted = true;
        }

        private void UpdateBattleEntrance()
        {
            if (!entrancePending || IsPaused || TimingCalibrationActive) return;
            entranceElapsed += UnityEngine.Time.deltaTime;
            if (!entranceMusicStarted && entranceElapsed >= musicStartDelaySeconds) StartEntranceMusic();
            if (!playOpeningCharacterAnimations)
            {
                if (entranceMusicStarted) CompleteBattleEntrance();
                return;
            }
            if (!entranceDrinking && entranceElapsed >= entranceIdleDuration)
            {
                entranceDrinking = true;
                foreach (var hero in rosterManager.ActiveHeroes)
                    hero.CombatAnimator?.PlayDrink(returnToOpeningIdle: true);
            }
            if (!entranceDrinking || !entranceMusicStarted) return;
            foreach (var hero in rosterManager.ActiveHeroes)
                if (hero.CombatAnimator != null && hero.CombatAnimator.IsPlaying &&
                    hero.CombatAnimator.ActiveAnimation == FightCharacterCombatAnimator.CombatAnimation.Drink) return;
            CompleteBattleEntrance();
        }

        private void CompleteBattleEntrance()
        {
            if (IsPaused || TimingCalibrationActive || WaveEntranceRunning) return;
            entrancePending = false;
            if (TryBeginBattle())
            {
                foreach (var hero in rosterManager.ActiveHeroes) hero.CombatAnimator?.EndOpeningPresentation();
            }
            else Debug.LogWarning("Battle entrance could not start: check all three hero slots.", this);
        }

        [Header("Successful Input Audio")]
        [Tooltip("Play once when a character input passes rhythm judgement, including preparation and guard inputs.")]
        [SerializeField] private bool enableSuccessfulInputSound = true;
        [SerializeField] private string successfulInputSoundEventPath = "event:/SoundEffects/BeatTapping";
        [SerializeField, Range(0f, 1f)] private float successfulInputSoundVolume = 0.5f;

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
        private float partyArmor;
        private float maxPartyArmor;
        private long latestCombatBeat = -1;
        private bool pendingEnemyAttack;
        private bool pendingEnemyWindup;
        private long pendingEnemyHitCombatBeat;
        private bool pendingEnemyAnimationDriven;
        private FightUnitSlot pendingEnemyAttacker;
        private int pendingAttackRosterVersion = -1;
        private long pendingEnemyActionId = long.MinValue;
        private long pendingAttackGlobalBeat = long.MinValue;
        private int pendingAttackBar;
        private int pendingAttackTimelineMs;
        private long guardedGlobalBeat = long.MinValue;
        private readonly HashSet<long> guardedBeats = new();
        private bool battleEnded;
        private int blockedAttackCount;
        private int receivedAttackCount;
        private float totalHealingReceived;
        private long lastEnemyAttackGlobalBeat = -1;
        private long calibrationStartBeat;
        private long resumeAfterCalibrationBeat = -1;
        public bool TimingCalibrationActive { get; private set; }
        public bool IsPaused => beatClock != null && beatClock.IsPaused;

        public void SetTimingCalibrationActive(bool active)
        {
            if (TimingCalibrationActive == active) return;
            if (active) ResetTeamSkill();
            TimingCalibrationActive = active;
            pendingEnemyAttack = false;
            pendingEnemyAnimationDriven = false;
            pendingEnemyAttacker = null;
            pendingEnemyActionId = long.MinValue;
            guardedBeats.Clear();
            guardedGlobalBeat = long.MinValue;
            if (active) calibrationStartBeat = latestCombatBeat;
            else
            {
                resumeAfterCalibrationBeat = latestCombatBeat;
            }
        }

        public event Action<FmodBeatClock.BeatSnapshot> FightBeat;
        public event Action<HeroCallResult> HeroCalled;
        public event Action<EnemyAttackResult> EnemyAttackResolved;
        public event Action<float, float> PartyHealthChanged;
        public event Action BattleLost;
        public event Action RosterRebuilt;

        public float PartyHp => partyHp;
        public float MaxPartyHp => maxPartyHp;
        public float PartyArmor => partyArmor;
        public float MaxPartyArmor => maxPartyArmor;
        public int ArmorRecoveryBeatsRemaining => 0;
        public bool BattleEnded => battleEnded;
        public bool UsesBattlePreparation => UsesEqualBeats && rosterManager != null && rosterManager.RequiresBattlePreparation;
        public bool AwaitingBattleStart => UsesBattlePreparation && !rosterManager.BattleStarted;
        public bool NaturalArmorRecoveryEnabled => false;

        public bool TryBeginBattle()
        {
            if (BattleEntranceRunning || !AwaitingBattleStart || IsPaused || TimingCalibrationActive || !rosterManager.TryLockFormation()) return false;
            ResetBattleStateForRoster();
            rhythmJudge?.ResetDuplicateTracking();
            if (startWithFullTeamSkill)
            {
                teamSkillGauge = TeamSkillGaugeMax;
                TeamSkillStatus = "READY  A / R";
            }
            PartyHealthChanged?.Invoke(partyHp, maxPartyHp);
            if (beatClock != null && !beatClock.IsPlaying) beatClock.RestartMusic();
            waveEntrance?.BeginFirstWave();
            return true;
        }
        public bool HasPendingEnemyAttack => pendingEnemyAttack;
        public int BlockedAttackCount => blockedAttackCount;
        public int ReceivedAttackCount => receivedAttackCount;
        public float TotalHealingReceived => totalHealingReceived;
        public FightUnitSlot TankSlot => tankSlot;
        public FightUnitSlot ActiveEnemySlot => activeEnemySlot;
        public CombatMode Mode => combatMode;
        public bool UsesEqualBeats => combatMode == CombatMode.EqualBeat;
        public bool UsesFrontHeroControls => combatMode == CombatMode.FrontHero || UsesEqualBeats;
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
            if (combatMode != mode) ResetTeamSkill();
            combatMode = mode;
        }

        public ActionType GetHeroActionForBeat(int beatInBar)
        {
            return !UsesEqualBeats && beatInBar == 4 ? ActionType.Skill : ActionType.LightAttack;
        }

        public void SetFrontHeroHealthSystemEnabled(bool enabled)
        {
            enableHealthSystemInFrontHeroMode = enabled;
        }

        public int GetEnemyBeatsUntilAttack(long globalBeat)
        {
            if (BossActive) return BossBeatsUntilAttack(globalBeat);
            return GetBeatsUntilScheduledAttack(globalBeat - enemyPausedBeats, EnemyAttackIntervalBeats);
        }

        public bool IsEnemyAttackBeat(long globalBeat)
        {
            if (AwaitingBattleStart) return false;
            if (BossActive) return IsBossAttackBeat(globalBeat);
            return IsScheduledAttackBeat(globalBeat - enemyPausedBeats, EnemyAttackIntervalBeats);
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
            waveEntrance = GetComponent<FightWaveEntrance>();
            EnsureStatusIconPool();
            EnsureDamageNumberPool();
            if (autoPlayBattleEntrance) beatClock?.SetAutomaticPlayback(false);
            if (UsesFrontHeroControls)
                RebuildRosterAndResetCombat();

            if (!UsesEqualBeats)
                maxPartyHp = tankSlot != null ? tankSlot.MaxHp : maxPartyHp;
            partyHp = maxPartyHp;
            if (tankSlot != null)
                tankSlot.RestoreFullHealth();
        }

        private void OnEnable()
        {
            FightSceneNaturalLighting.Ensure(this);
            FightEnvironmentController.EnsureLegacy(this);
            SubscribeDependencies();
        }

        private void Start()
        {
            BeginBattleEntrance();
            PartyHealthChanged?.Invoke(partyHp, maxPartyHp);
        }

        private void Update()
        {
            UpdateBattleEntrance();
            UpdateSkillVisualPause();
            UpdateNormalImpacts();
            UpdateSwordStreaks();
            if (TimingCalibrationActive || IsPaused || AwaitingBattleStart || battleEnded) return;

            UpdateStageDeadline();
            if (battleEnded) return;

            UpdateTeamSkillBurst();
            AdvanceSkillVisualLifetimes(Time.deltaTime);
            AdvanceSlimeDetonations(Time.deltaTime);
            CheckWaveCompletion();
            UpdateBasicAbilityProgress();
            if (WaveEntranceRunning) return;
            TryResolvePendingAttack();
            if (beatClock != null && beatClock.TryGetTimelinePositionMs(out int impactTime)) ResolveEnemyCombatWork(impactTime);
        }

        private void OnDisable()
        {
            ResetSwordCharges();
            ResetDamageNumberPool();
            entrancePending = false;
            ResetTeamSkill();
            CancelEnemyCombatWork();
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
            if (battleEnded || AwaitingBattleStart || TimingCalibrationActive || IsPaused ||
                (UsesEqualBeats && latestCombatBeat <= resumeAfterCalibrationBeat))
                return;

            if (command == FightInputRouter.HeroCommand.TeamSkill)
            {
                if (UsesEqualBeats && rhythmJudge != null && inputRouter != null) rhythmJudge.SetInputProfile(inputRouter.CurrentInputProfile);
                if (WaveTransitionActive || suspendFeverActivation)
                {
                    if (rhythmJudge == null) return;
                    var result = rhythmJudge.JudgeInput(inputRouter != null ? inputRouter.CurrentInputAgeMs : 0);
                    ApplyTransitionRhythmJudgement(result);
                    return;
                }
                if (UsesEqualBeats) { if (TeamSkillRunning) SubmitTeamSkillBurst(); else TryStartTeamSkill(); return; }
                command = FightInputRouter.HeroCommand.Ultimate;
            }
            if (TeamSkillRunning) return;
            if (UsesEqualBeats && command == FightInputRouter.HeroCommand.Ultimate)
            {
                if (suspendFeverActivation) return;
                TryStartTeamUltimate();
                return;
            }
            if (UsesFrontHeroControls)
            {
                // A remains routed separately so future on-beat encouragement can be
                // introduced without consuming the shared Basic judgement slot.
                if (TeamSkillRunning) return;
                SubmitFrontHeroCommand(command);
                return;
            }

            SubmitLegacyHeroCommand(command);
        }

        private void ApplyTransitionRhythmJudgement(FmodRhythmJudge.Result result)
        {
            PlaySuccessfulInputSound(result);
            HeroCalled?.Invoke(new HeroCallResult(FightInputRouter.HeroCommand.TeamSkill, result, false, false,
                "Rhythm only — Fever not activated"));
        }

        private void SubmitFrontHeroCommand(FightInputRouter.HeroCommand command)
        {
            if (command == FightInputRouter.HeroCommand.Ultimate || rhythmJudge == null)
                return;

            HeroBeatSettings hero = GetHeroForCommand(command);
            if (hero?.UnitSlot == null)
                return;

            if (UsesEqualBeats && inputRouter != null) rhythmJudge.SetInputProfile(inputRouter.CurrentInputProfile);
            FmodRhythmJudge.Result judgement = UsesEqualBeats
                ? rhythmJudge.JudgeInput(inputRouter != null ? inputRouter.CurrentInputAgeMs : 0)
                : rhythmJudge.JudgeNow();
            ApplyHeroJudgement(hero, command, judgement);
        }

        private void ApplyHeroJudgement(HeroBeatSettings hero, FightInputRouter.HeroCommand command,
            FmodRhythmJudge.Result judgement)
        {
            bool perfect = judgement.Judgement == FmodRhythmJudge.Grade.Perfect;
            TrackAimedShotJudgement(hero.UnitSlot, judgement);
            TrackSwordJudgement(hero.UnitSlot, judgement);
            hero.UnitSlot.PlayInputFeedback(perfect, GetSwordChargeStage(hero.UnitSlot) == 3);
            if (!perfect)
            {
                HeroCalled?.Invoke(new HeroCallResult(command, judgement, false, false, judgement.Message));
                return;
            }

            PlaySuccessfulInputSound(judgement);
            ActionType action = GetHeroActionForBeat(judgement.NearestBeat.Beat);
            bool heavyBeat = action == ActionType.Skill;
            PerformHeroAction(
                hero,
                action,
                command,
                judgement,
                heavyBeat);
        }

        public HeroBeatSettings GetHeroForCommand(FightInputRouter.HeroCommand command)
        {
            return command switch
            {
                FightInputRouter.HeroCommand.Front => frontHero,
                FightInputRouter.HeroCommand.Middle => secondHero,
                FightInputRouter.HeroCommand.Back => thirdHero,
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
            if (behavior == FightCharacterDefinition.AbilityBehavior.Unavailable)
            {
                HeroCalled?.Invoke(new HeroCallResult(command, judgement, false, false,
                    $"{hero.HeroLabel}: ability not available in this checkpoint."));
                return;
            }
            float power = skill ? actor.SkillPower : actor.AttackPower;
            if (!skill)
                power = actor.NormalAbilityPower;
            if (!skill && UsesEqualBeats && IsAttackCall(behavior)) TriggerAimedFollowUp(actor);
            string result = PerformConfiguredAbility(
                hero,
                action,
                behavior,
                power,
                judgement.NearestBeat.GlobalBeat);


            activeEnemySlot = FindFrontLivingEnemy();
            if (!skill && UsesEqualBeats)
            {
                ApplyAbilityArmorRecovery(actor.CharacterDefinition, false);
                GainTeamSkill(judgement);
            }
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
                : UsesEqualBeats ? FightCharacterCombatAnimator.CombatAnimation.NormalAttack
                    : FightCharacterCombatAnimator.CombatAnimation.LightAttack;
            Action playEffect = skill
                ? () => actor.PlaySkillAttackAt(actor.CastEffectAnchor)
                : () => actor.PlayLightAttackAt(actor.CastEffectAnchor);
            string abilityName = skill ? hero.SkillName : actor.CharacterDefinition != null
                ? actor.CharacterDefinition.BasicAbilityName : "Basic Ability";

            // Accepted early inputs use their judged beat's state as well.
            AdvanceBossState(globalBeat);
            // Preparation inputs are not attacks and must not build Boss Break.
            if (!skill && behavior == FightCharacterDefinition.AbilityBehavior.AimedShot)
                return PerformAimedShot(actor, power, globalBeat);
            if (actor.CharacterDefinition != null && !WaveTransitionActive)
                ApplyBossBreak(skill ? actor.CharacterDefinition.SkillBreakPower : actor.CharacterDefinition.BasicBreakPower, globalBeat);

            if (!skill && UsesEqualBeats && actor.CharacterDefinition != null && actor.CharacterDefinition.EnableSwordCharge &&
                behavior == FightCharacterDefinition.AbilityBehavior.DamageFront)
                return PerformSwordAttack(actor, power, globalBeat);

            switch (behavior)
            {
                case FightCharacterDefinition.AbilityBehavior.BreakingSlash:
                case FightCharacterDefinition.AbilityBehavior.EnergySurge:
                case FightCharacterDefinition.AbilityBehavior.FormationBreaker:
                case FightCharacterDefinition.AbilityBehavior.SlimeFragments:
                case FightCharacterDefinition.AbilityBehavior.SmallBombs:
                    return PerformPartySkill(actor, behavior, power);
                case FightCharacterDefinition.AbilityBehavior.Unavailable:
                    return $"{abilityName} is not available in this checkpoint";
                case FightCharacterDefinition.AbilityBehavior.RestoreArmor:
                    PlayAnimatedHeroUtility(actor, animation, playEffect, null);
                    return $"{abilityName} restores shared armor according to its configured recovery";
                case FightCharacterDefinition.AbilityBehavior.Guard:
                    ArmGuard(globalBeat, skill ? 1 : actor.CharacterDefinition?.BasicGuardBeats ?? 1);
                    PlayAnimatedHeroUtility(
                        actor,
                        UsesEqualBeats ? animation : FightCharacterCombatAnimator.CombatAnimation.Guard,
                        () => PlayGuardProjectileVisual(actor, skill, UsesEqualBeats ? tankSlot : actor),
                        null);
                    return UsesEqualBeats ? $"{abilityName} blocks enemy damage on this beat only"
                        : $"{abilityName} blocks all damage from the next enemy attack";

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
                    ArmGuard(globalBeat, skill ? 1 : actor.CharacterDefinition?.BasicGuardBeats ?? 1);
                    actor.PlayGuardAt(skill, UsesEqualBeats ? tankSlot : null);
                    FightUnitSlot guardedTarget = FindFrontLivingEnemy();
                    if (guardedTarget != null)
                        PlayAnimatedHeroAction(actor, guardedTarget, action, power);
                    return UsesEqualBeats
                        ? $"{abilityName} guards this beat and deals {power:0.#} damage to the front enemy"
                        : $"{abilityName} blocks all damage and deals {power:0.#} damage to the front enemy";

                default:
                    FightUnitSlot target = FindFrontLivingEnemy();
                    if (target == null)
                    {
                        if (WaveTransitionActive) PlayTransitionAttackWithoutTarget(actor, animation);
                        return $"{abilityName} found no target";
                    }
                    float effectivePower = ModifyBossDamage(target, power);
                    PlayAnimatedHeroAction(actor, target, action, power);
                    return $"{abilityName} deals {effectivePower:0.#} damage to the front enemy";
            }
        }

        private void ArmGuard(long inputGlobalBeat, int durationBeats = 1)
        {
            if (UsesEqualBeats)
            {
                // Preserve adjacent accepted beats independently, even when a late attack
                // resolution overlaps an early input for the following beat.
                for (int offset = 0; offset < Mathf.Max(1, durationBeats); offset++)
                    guardedBeats.Add(inputGlobalBeat + offset);
                return;
            }
            guardedGlobalBeat = pendingEnemyAttack
                ? pendingAttackGlobalBeat
                : inputGlobalBeat + GetEnemyBeatsUntilAttack(inputGlobalBeat);
        }

        private void PlayTransitionAttackWithoutTarget(FightUnitSlot actor, FightCharacterCombatAnimator.CombatAnimation animation)
        {
            actor.PlayCombatAnimation(animation, null, null, null,
                effect => { if (effect.location == FightCharacterCombatAnimator.EffectLocation.Self) actor.PlayFrameEffect(effect); });
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
            PlaySuccessfulInputSound(judgement);
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

        private void PlaySuccessfulInputSound(FmodRhythmJudge.Result judgement)
        {
            if (!enableSuccessfulInputSound || judgement.Judgement != FmodRhythmJudge.Grade.Perfect ||
                judgement.DuplicateBeat || string.IsNullOrWhiteSpace(successfulInputSoundEventPath))
                return;

            AudioPlayer.Play(successfulInputSoundEventPath, successfulInputSoundVolume, transform.position);
        }
        private void OnBeat(FmodBeatClock.BeatSnapshot beat)
        {
            if (IsPaused) return;

            latestCombatBeat = beat.GlobalBeat;
            if (TimingCalibrationActive || battleEnded) return;
            UpdateStageDeadline();
            if (battleEnded) return;
            waveEntrance?.RefreshProgress();
            if (WaveEntranceRunning)
            {
                if (!AwaitingBattleStart) enemyPausedBeats++;
                FightBeat?.Invoke(beat);
                return;
            }
            if (AwaitingBattleStart)
            {
                FightBeat?.Invoke(beat);
                return;
            }
            AdvanceEnemyPause(beat.GlobalBeat);
            AdvanceBossState(beat.GlobalBeat);
            AdvanceTeamSkill(beat.GlobalBeat);
            AdvanceTeamUltimate(beat.GlobalBeat);
            if (UsesBattlePreparation && AdvanceWaveBoundary()) return;
            AdvanceNormalBattleBeat(beat);
            if (UsesEqualBeats)
                guardedBeats.RemoveWhere(guardBeat => guardBeat < beat.GlobalBeat &&
                    (!pendingEnemyAttack || guardBeat != pendingAttackGlobalBeat) &&
                    !enemyHits.Exists(hit => hit.hitBeat == guardBeat));
            FightBeat?.Invoke(beat);

            if (battleEnded || AwaitingNextWave)
                return;

            if (EnemyActionsPaused) return;

            if (UsesFrontHeroControls)
            {
                activeEnemySlot = FindFrontLivingEnemy();
                // Authored enemies act from every formation position. Front selection
                // determines player targets, not which enemies are allowed to attack.
                StartAuthoredEnemyTurns(beat.GlobalBeat);
                if (!PressureRestActive && !pendingEnemyAttack && lastEnemyQueuedBeat != beat.GlobalBeat && activeEnemySlot != null && IsEnemyAttackBeat(beat.GlobalBeat) &&
                    (activeEnemySlot.CharacterDefinition == null || (BossActive && activeEnemySlot == bossActor)) &&
                    !enemyHits.Exists(hit => hit.source == activeEnemySlot && !hit.launched) &&
                    (!BossActive || lastBossAttackBeat != beat.GlobalBeat))
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
            if (activeEnemySlot != null && activeEnemySlot.IsStunned) return;
            lastEnemyQueuedBeat = beat.GlobalBeat;
            if (TryStartAuthoredEnemyAttack(activeEnemySlot)) return;
            pendingBossDamage = BossAttackDamage(beat.GlobalBeat);
            if (BossActive) lastBossAttackBeat = beat.GlobalBeat;
            pendingEnemyActionId = ++nextActionId;
            pendingAttackRosterVersion = rosterVersion;
            pendingEnemyAttack = true;
            pendingAttackGlobalBeat = beat.GlobalBeat;
            pendingAttackBar = beat.Bar;
            pendingAttackTimelineMs = beat.TimelinePositionMs;
            pendingEnemyAttacker = activeEnemySlot;
            pendingEnemyGuardChip = CurrentPressureGuardChip;
            pendingEnemyAnimationDriven = false;
            int windup = activeEnemySlot != null && activeEnemySlot.CharacterDefinition != null
                ? activeEnemySlot.CharacterDefinition.AttackWindupBeats : 0;
            pendingEnemyWindup = windup > 0;
            pendingEnemyHitCombatBeat = NormalBattleBeatCount + windup;
            pendingAttackGlobalBeat += windup;
            if (beatClock != null) pendingAttackTimelineMs += Mathf.RoundToInt(windup * (float)beatClock.MillisecondsPerBeat);
        }

        private void StartEnemyAttackAnimation()
        {
            if (!pendingEnemyAttack || !UsesFrontHeroControls || pendingEnemyAttacker == null)
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
            if (EnemyActionsPaused) return;
            if (!pendingEnemyAttack || pendingEnemyWindup || pendingEnemyAnimationDriven || beatClock == null || rhythmJudge == null ||
                !beatClock.TryGetTimelinePositionMs(out int timelineMs))
            {
                return;
            }

            float lateWindowMs = Mathf.Max(0f, rhythmJudge.PerfectWindowMs - rhythmJudge.JudgementOffsetMs);
            bool guarded = UsesEqualBeats ? guardedBeats.Contains(pendingAttackGlobalBeat)
                : guardedGlobalBeat == pendingAttackGlobalBeat;
            if (timelineMs < pendingAttackTimelineMs ||
                (!guarded && timelineMs < pendingAttackTimelineMs + lateWindowMs + resolutionSafetyMs))
                return;

            ResolvePendingEnemyAttack(pendingAttackRosterVersion, pendingEnemyActionId, pendingEnemyAttacker);
        }

        private void ResolvePendingEnemyAttack(
            int expectedRosterVersion,
            long expectedActionId,
            FightUnitSlot expectedAttacker)
        {
            if (IsPaused || EnemyActionsPaused || pendingEnemyWindup) return;
            if (!IsCurrentPendingEnemyAttack(expectedRosterVersion, expectedActionId, expectedAttacker))
                return;

            bool blocked = UsesEqualBeats ? guardedBeats.Contains(pendingAttackGlobalBeat)
                : guardedGlobalBeat == pendingAttackGlobalBeat;
            // Only unresolved guards need the remaining late-input window.
            if (pendingEnemyAnimationDriven && !blocked && !HasEnemyResolutionWindowElapsed())
            {
                pendingEnemyAnimationDriven = false;
                return;
            }

            FightUnitSlot attacker = pendingEnemyAttacker != null ? pendingEnemyAttacker : activeEnemySlot;
            float configuredDamage = UsesFrontHeroControls && attacker != null
                ? Mathf.Max(0.5f, attacker.AttackPower)
                : enemyAttackDamage;
            if (pendingBossDamage > 0) configuredDamage = pendingBossDamage;
            float damage = ResolveEnemyHitDamage(configuredDamage, blocked, pendingEnemyGuardChip);

            if (blocked)
            {
                blockedAttackCount++;
                PlaySuccessfulBlockFeedback();
            }
            else
                receivedAttackCount++;

            // Enemy skills are intentionally controlled elsewhere. Scheduled enemy turns
            // always remain normal attacks, but still bank one mana up to the prefab limit.
            if (UsesFrontHeroControls && attacker != null)
                attacker.GainMana();
            lastEnemyAttackGlobalBeat = pendingAttackGlobalBeat;

            if (HealthSystemEnabled)
            {
                if (UsesEqualBeats)
                    damage = ApplyEnemyPartyDamage(damage, blocked, pendingAttackGlobalBeat);
                else
                    partyHp = Mathf.Max(0f, partyHp - damage);
                if (damage > 0 && attacker != null && tankSlot != null)
                    attacker.PlayOnHitEffect(false, tankSlot.ImpactEffectAnchor);
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
            if (blocked) RecordPressureBlock();
            pendingAttackRosterVersion = -1;
            pendingEnemyActionId = long.MinValue;
            EnemyAttackResolved?.Invoke(new EnemyAttackResult(
                pendingAttackGlobalBeat,
                pendingAttackBar,
                blocked,
                damage,
                partyHp));
            PartyHealthChanged?.Invoke(partyHp, maxPartyHp);

            if (blocked && BossActive && attacker == bossActor && CurrentBossPhase == BossPhase.Rage)
                ApplyBossBreak(BossPattern.successfulGuardBreakPower, pendingAttackGlobalBeat);
            pendingBossDamage = 0;

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
            frontHero.BindUnitSlot(HeroAtPosition(heroes, FightRosterManager.PartyPosition.Front), true);
            secondHero.BindUnitSlot(HeroAtPosition(heroes, FightRosterManager.PartyPosition.Middle), true);
            thirdHero.BindUnitSlot(HeroAtPosition(heroes, FightRosterManager.PartyPosition.Back), true);
            tankSlot = frontHero.UnitSlot;
            activeEnemySlot = FindFrontLivingEnemy();
            enemyAttackIntervalBeats = Mathf.Max(1, enemyAttackIntervalBeats);
            ResetBattleStateForRoster();
        }

        private FightUnitSlot HeroAtPosition(List<FightUnitSlot> heroes, FightRosterManager.PartyPosition position)
        {
            if (!UsesEqualBeats)
                return SlotAt(heroes, (int)position);
            if (rosterManager != null)
                return rosterManager.GetHeroAtPosition(position);
            return heroes.Find(slot => slot.SlotIndex == (int)position);
        }

        private void ResetBattleStateForRoster()
        {
            ResetTeamSkill();
            pendingEnemyAttack = false;
            pendingEnemyAnimationDriven = false;
            pendingEnemyAttacker = null;
            pendingAttackRosterVersion = -1;
            pendingEnemyActionId = long.MinValue;
            pendingAttackGlobalBeat = long.MinValue;
            pendingAttackBar = 0;
            pendingAttackTimelineMs = 0;
            guardedGlobalBeat = long.MinValue;
            guardedBeats.Clear();
            battleEnded = false;
            BattleWon = AwaitingNextWave = false;
            CurrentWave = 1;
            ResetStageDeadline();
            ResetWavePressure();
            NormalBattleBeatCount = 0;
            waveWaitBeatsRemaining = 0;
            lastWaveWaitBeat = long.MinValue;
            lastNormalBattleBeat = long.MinValue;
            ClearEnemyScheduledActions();
            ClearBattleEffects();
            blockedAttackCount = 0;
            receivedAttackCount = 0;
            totalHealingReceived = 0f;
            lastEnemyAttackGlobalBeat = -1;

            maxPartyHp = UsesEqualBeats
                ? (frontHero.UnitSlot != null ? frontHero.UnitSlot.MaxHp : 0f)
                  + (secondHero.UnitSlot != null ? secondHero.UnitSlot.MaxHp : 0f)
                  + (thirdHero.UnitSlot != null ? thirdHero.UnitSlot.MaxHp : 0f)
                : tankSlot != null ? tankSlot.MaxHp : Mathf.Max(0.5f, maxPartyHp);
            maxPartyArmor = UsesEqualBeats && tankSlot != null && tankSlot.CharacterDefinition != null
                ? ArmorForDefense(tankSlot.CharacterDefinition.Defense) + (enableCharacterPassives ? tankSlot.CharacterDefinition.FrontlineDefenseBonus : 0) : 0f;
            partyArmor = maxPartyArmor;
            latestCombatBeat = beatClock != null && beatClock.HasTimingAnchor ? beatClock.LatestBeat.GlobalBeat : -1;
            if (tankSlot != null)
                tankSlot.RestoreFullHealth();
            partyHp = maxPartyHp;
        }

        public float ArmorForDefense(int defense)
        {
            return Mathf.Max(0, defense);
        }

        public float ApplyPartyDamage(float amount, long globalBeat)
        {
            if (!UsesEqualBeats || !HealthSystemEnabled || battleEnded || AwaitingBattleStart || amount <= 0f)
                return 0f;
            float damage = Mathf.Max(0f, Mathf.Round(amount * 2f) * 0.5f);
            if (damage <= 0f)
                return 0f;
            latestCombatBeat = Math.Max(latestCombatBeat, globalBeat);
            float armorDamage = Mathf.Min(partyArmor, damage);
            partyArmor -= armorDamage;
            float hpDamage = Mathf.Min(partyHp, damage - armorDamage);
            partyHp -= hpDamage;
            if (armorDamage > 0 || hpDamage > 0) PartyDamageTaken?.Invoke(armorDamage, hpDamage);
            PartyHealthChanged?.Invoke(partyHp, maxPartyHp);
            if (UsesBattlePreparation && endBattleOnZeroHp && partyHp <= 0) FinishBattle(false);
            return armorDamage + hpDamage;
        }

        // Actual resource loss, emitted only by damage resolution (never healing/formation changes).
        public event Action<float, float> PartyDamageTaken;

        // Compatibility entry point for existing validation tools. Time never restores armor.
        public void AdvanceArmorRecovery(long globalBeat) { }

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
                if (slot != null && slot.HasCharacter && slot.CurrentHp > 0)
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
            // This visual belongs to the already resolved hit, including a lethal hit.
            // Only a subsequent attack selects the next living front enemy.
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
            foreach (FightUnitSlot target in fightScene2Enemies.ToArray())
            {
                if (!IsCurrentHero(expectedRosterVersion, attacker)) return;
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

            // A transition shot stays harmless even if its animation/projectile lands after entry finishes.
            bool transitionBlocked = WaveTransitionActive && damage > 0;
            if (transitionBlocked) damage = 0;

            FightCharacterCombatAnimator.CombatAnimation animation = action switch
            {
                ActionType.HeavyAttack => FightCharacterCombatAnimator.CombatAnimation.HeavyAttack,
                ActionType.Skill => FightCharacterCombatAnimator.CombatAnimation.Skill,
                _ => UsesEqualBeats ? FightCharacterCombatAnimator.CombatAnimation.NormalAttack
                    : FightCharacterCombatAnimator.CombatAnimation.LightAttack
            };
            int actionRosterVersion = rosterVersion;
            var projectile = action != ActionType.Skill && attacker.NormalAttackEffectPrefab != null
                ? attacker.NormalAttackEffectPrefab.GetComponent<FightEffectSettings>() : null;
            if (projectile != null && projectile.flyToTarget && beatClock != null)
            {
                bool launched = false;
                Action launch = () =>
                {
                    if (launched || !IsCurrentHeroAction(actionRosterVersion, attacker, target) || !CanHitBurstTarget(target)) return;
                    launched = true;
                    var origin = attacker.CastEffectAnchor;
                    var position = origin.position + (target.ImpactEffectAnchor.position - origin.position).normalized * projectile.forwardSpawnDistance;
                    var instance = Instantiate(attacker.NormalAttackEffectPrefab, position,
                        origin.rotation * Quaternion.Euler(0, 0, attacker.NormalAttackEffectPrefab.transform.eulerAngles.z));
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
                    instance.AddComponent<FightBeatProjectile>().Initialize(this, target.ImpactEffectAnchor, projectile.flightBeats,
                        () => !battleEnded && IsCurrentHeroAction(actionRosterVersion, attacker, target) && CanHitBurstTarget(target),
                        () =>
                        {
                            float hp = target.CurrentHp;
                            if (transitionBlocked) target.ShowTransitionImmuneHit();
                            ApplyHeroDamage(actionRosterVersion, attacker, target, damage, false);
                            if (IsCurrentHeroAction(actionRosterVersion, attacker, target) && target.CurrentHp < hp &&
                                attacker.CharacterDefinition != null && attacker.CharacterDefinition.BasicAppliesBleeding)
                                target.ApplyBleeding(attacker.CharacterDefinition.BleedDamage, attacker.CharacterDefinition.BleedBeats);
                        });
                };
                if (!attacker.PlayCombatAnimation(animation,
                    () => PlayHeroAttackWarning(actionRosterVersion, attacker), launch, null,
                    effect => { if (IsCurrentHeroAction(actionRosterVersion, attacker, target)) attacker.PlayFrameEffect(effect, target.ImpactEffectAnchor); })) launch();
                return;
            }
            if (transitionBlocked && IsCurrentHeroAction(actionRosterVersion, attacker, target)) target.ShowTransitionImmuneHit();
            if (resolvingTeamSkillFrame && action == ActionType.Skill)
            {
                PlayHeroActionVisual(actionRosterVersion, attacker, target, action);
                ApplyHeroDamage(actionRosterVersion, attacker, target, damage, action == ActionType.Skill);
                return;
            }
            // Equal-beat normal hits and Aimed projectiles share one song-beat impact delay.
            // Particle lifetime never decides gameplay timing; legacy skills retain their path.
            if (UsesEqualBeats && action != ActionType.Skill && !transitionBlocked)
                ScheduleNormalImpact(attacker, target, () => ApplyHeroDamage(actionRosterVersion, attacker, target, damage, false));
            else ApplyHeroDamage(actionRosterVersion, attacker, target, damage, action == ActionType.Skill);
            bool animated = attacker.PlayCombatAnimation(
                animation,
                () => PlayHeroAttackWarning(actionRosterVersion, attacker),
                () => PlayHeroActionVisual(actionRosterVersion, attacker, target, action),
                null,
                effect =>
                {
                    if (IsCurrentHeroAction(actionRosterVersion, attacker, target))
                        attacker.PlayFrameEffect(effect, target.ImpactEffectAnchor);
                });
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
            var effectTargets = fightScene2Enemies.FindAll(enemy => enemy != null && enemy.HasCharacter && enemy.CurrentHp > 0).ToArray();
            if (resolvingTeamSkillFrame && action == ActionType.Skill)
            {
                PlayHeroAreaVisual(actionRosterVersion, attacker, action);
                ApplyAreaDamage(actionRosterVersion, attacker, damage);
                return;
            }
            if (UsesEqualBeats && action != ActionType.Skill && !WaveTransitionActive)
            {
                foreach (var target in effectTargets)
                    ScheduleNormalImpact(attacker, target, () => ApplyHeroDamage(actionRosterVersion, attacker, target, damage, false));
            }
            else ApplyAreaDamage(actionRosterVersion, attacker, damage);
            bool animated = attacker.PlayCombatAnimation(
                animation,
                () => PlayHeroAttackWarning(actionRosterVersion, attacker),
                () => PlayHeroAreaVisual(actionRosterVersion, attacker, action),
                null,
                effect =>
                {
                    if (!IsCurrentHero(actionRosterVersion, attacker)) return;
                    if (effect.location == FightCharacterCombatAnimator.EffectLocation.Self)
                        attacker.PlayFrameEffect(effect);
                    else
                        foreach (var target in effectTargets)
                            if (IsCurrentHeroAction(actionRosterVersion, attacker, target))
                                attacker.PlayFrameEffect(effect, target.ImpactEffectAnchor);
                });
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
            if (resolvingTeamSkillFrame)
            {
                guardedEffect();
                guardedResolution();
                return;
            }
            bool animated = actor.PlayCombatAnimation(
                animation,
                () => PlayHeroAttackWarning(actionRosterVersion, actor),
                guardedEffect,
                guardedResolution,
                effect =>
                {
                    if (IsCurrentHero(actionRosterVersion, actor)) actor.PlayFrameEffect(effect);
                });
            if (animated)
                return;

            guardedEffect();
            guardedResolution();
        }

        private void HealPartyHealth(FightUnitSlot healer, float amount)
        {
            if (healer == null || amount <= 0f || (UsesEqualBeats && battleEnded))
                return;

            float previous = partyHp;
            partyHp = Mathf.Min(maxPartyHp, partyHp + QuantizeCombatValue(amount));
            totalHealingReceived += partyHp - previous;
            PartyHealthChanged?.Invoke(partyHp, maxPartyHp);
            Debug.Log($"[FightParty] {healer.DisplayName} healed {partyHp - previous:0.#} HP.", this);
        }

        private void ApplyAreaDamage(int expectedRosterVersion, FightUnitSlot attacker, float damage)
        {
            if (!IsCurrentHero(expectedRosterVersion, attacker) || !HealthSystemEnabled || damage <= 0)
                return;
            if (WaveTransitionActive)
            {
                foreach (var enemy in fightScene2Enemies)
                    if (enemy != null && enemy.CurrentHp > 0) enemy.ShowTransitionImmuneHit();
                return;
            }

            foreach (FightUnitSlot target in fightScene2Enemies.ToArray())
            {
                if (!IsCurrentHero(expectedRosterVersion, attacker)) return;
                if (!CanHitBurstTarget(target))
                    continue;
                bool wasAlive = target.CurrentHp > 0;
                float dealt = target.TakeDamage(ModifyBossDamage(target, damage));
                if (dealt > 0 || IsBurstRetainedTarget(target) && ModifyBossDamage(target, damage) > 0)
                    attacker.PlayOnHitEffect(true, target.ImpactEffectAnchor);
                if (wasAlive) CancelDefeatedEnemyAttack(target);
            }
            activeEnemySlot = FindFrontLivingEnemy();
        }

        private void ApplyHeroDamage(
            int expectedRosterVersion,
            FightUnitSlot attacker,
            FightUnitSlot target,
            float damage,
            bool skill = true,
            bool aimedFollowUp = false,
            GameObject onHitOverride = null,
            GameObject additionalOnHitEffect = null)
        {
            if (!IsCurrentHeroAction(expectedRosterVersion, attacker, target) || damage <= 0)
                return;
            if (WaveTransitionActive) { target.ShowTransitionImmuneHit(); return; }

            if (HealthSystemEnabled && CanHitBurstTarget(target))
            {
                bool wasAlive = target.CurrentHp > 0;
                float dealt = target.TakeDamage(ModifyBossDamage(target, damage));
                if (!aimedFollowUp && target.CurrentHp > 0) target.NotifyAimedDirectHit(attacker);
                if (dealt > 0 || IsBurstRetainedTarget(target) && ModifyBossDamage(target, damage) > 0)
                {
                    if (onHitOverride != null)
                        attacker.EffectPlayer.SpawnConfiguredAbility(onHitOverride, target.ImpactEffectAnchor, 0,
                            FightAttackEffect.VisualStyle.Light, Color.white);
                    else attacker.PlayOnHitEffect(skill, target.ImpactEffectAnchor);
                    if (additionalOnHitEffect != null)
                        attacker.EffectPlayer.SpawnConfiguredAbility(additionalOnHitEffect, target.ImpactEffectAnchor, 0,
                            FightAttackEffect.VisualStyle.Light, Color.white);
                }
                if (wasAlive) CancelDefeatedEnemyAttack(target);
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
