using System;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Minimal four-beat combat loop. The enemy telegraphs on beats 1-3 and attacks on beat 4.
    /// A Perfect tank command on that heavy beat blocks the attack.
    /// </summary>
    public sealed class FightCombatController : MonoBehaviour
    {
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

        [Header("Prototype Rules")]
        [SerializeField, Min(1)] private int maxPartyHp = 5;
        [SerializeField, Min(1)] private int enemyAttackDamage = 1;
        [SerializeField, Min(0f)] private float resolutionSafetyMs = 12f;

        private int partyHp;
        private bool pendingEnemyAttack;
        private long pendingAttackGlobalBeat = long.MinValue;
        private int pendingAttackBar;
        private int pendingAttackTimelineMs;
        private long guardedGlobalBeat = long.MinValue;
        private bool battleEnded;
        private int blockedAttackCount;
        private int receivedAttackCount;

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

        public void Configure(
            FmodBeatClock clock,
            FmodRhythmJudge judge,
            FightInputRouter router,
            int partyHealth = 5,
            int attackDamage = 1)
        {
            beatClock = clock;
            rhythmJudge = judge;
            inputRouter = router;
            maxPartyHp = Mathf.Max(1, partyHealth);
            enemyAttackDamage = Mathf.Max(1, attackDamage);
            partyHp = maxPartyHp;
        }

        private void Awake()
        {
            partyHp = maxPartyHp;
        }

        private void OnEnable()
        {
            if (beatClock != null)
                beatClock.Beat += OnBeat;

            if (inputRouter != null)
                inputRouter.CommandStarted += SubmitHeroCommand;
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
            if (beatClock != null)
                beatClock.Beat -= OnBeat;

            if (inputRouter != null)
                inputRouter.CommandStarted -= SubmitHeroCommand;
        }

        public void SubmitHeroCommand(FightInputRouter.HeroCommand command)
        {
            if (battleEnded)
                return;

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

            HeroCalled?.Invoke(new HeroCallResult(
                command,
                judgement,
                heavyBeat,
                skillActivated,
                message));
        }

        private void OnBeat(FmodBeatClock.BeatSnapshot beat)
        {
            FightBeat?.Invoke(beat);

            if (battleEnded || beat.Beat != 4)
                return;

            // Resolve after the late side of the Perfect window, so early and late inputs are fair.
            pendingEnemyAttack = true;
            pendingAttackGlobalBeat = beat.GlobalBeat;
            pendingAttackBar = beat.Bar;
            pendingAttackTimelineMs = beat.TimelinePositionMs;
        }

        private void TryResolvePendingAttack()
        {
            if (!pendingEnemyAttack || beatClock == null || rhythmJudge == null ||
                !beatClock.TryGetTimelinePositionMs(out int timelineMs))
            {
                return;
            }

            float lateWindowMs = Mathf.Max(
                0f,
                rhythmJudge.PerfectWindowMs - rhythmJudge.JudgementOffsetMs);

            if (timelineMs < pendingAttackTimelineMs + lateWindowMs + resolutionSafetyMs)
                return;

            bool blocked = guardedGlobalBeat == pendingAttackGlobalBeat;
            int damage = blocked ? 0 : enemyAttackDamage;

            if (blocked)
                blockedAttackCount++;
            else
                receivedAttackCount++;

            partyHp = Mathf.Max(0, partyHp - damage);
            pendingEnemyAttack = false;

            EnemyAttackResolved?.Invoke(new EnemyAttackResult(
                pendingAttackGlobalBeat,
                pendingAttackBar,
                blocked,
                damage,
                partyHp));

            PartyHealthChanged?.Invoke(partyHp, maxPartyHp);

            if (partyHp > 0)
                return;

            battleEnded = true;
            BattleLost?.Invoke();
        }
    }
}
