using RhythmHunter.RhythmDemo;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    public sealed class FightScenePresenter : MonoBehaviour
    {
        private static readonly Color Background = new(0.018f, 0.025f, 0.045f, 1f);
        private static readonly Color Cyan = new(0.2f, 0.92f, 1f, 1f);
        private static readonly Color Dim = new(0.12f, 0.2f, 0.28f, 1f);
        private static readonly Color Gold = new(1f, 0.68f, 0.16f, 1f);
        private static readonly Color Green = new(0.3f, 1f, 0.55f, 1f);
        private static readonly Color Red = new(1f, 0.25f, 0.3f, 1f);
        private static readonly Color Purple = new(0.72f, 0.42f, 1f, 1f);

        [Header("Dependencies")]
        [SerializeField] private FmodBeatClock beatClock;
        [SerializeField] private FmodRhythmJudge rhythmJudge;
        [SerializeField] private FightCombatController fight;

        [Header("Rhythm UI")]
        [SerializeField] private Text playbackText;
        [SerializeField] private Text cycleText;
        [SerializeField] private Text warningText;
        [SerializeField] private Text resultText;
        [SerializeField] private Text detailText;
        [SerializeField] private Text healthText;
        [SerializeField] private Text statisticsText;
        [SerializeField] private Image[] beatNodes;
        [SerializeField] private Slider beatProgress;
        [SerializeField] private Slider healthBar;

        [Header("Battlefield UI")]
        [SerializeField] private Image[] enemySlots;
        [SerializeField] private Image[] heroSlots;
        [SerializeField] private Image shieldEffect;
        [SerializeField] private Image screenFlash;

        private float resultTimer;
        private float shieldTimer;
        private float flashTimer;
        private int currentBeat;
        private int perfectCalls;
        private int missCalls;
        private int blockedAttacks;
        private int receivedAttacks;
        private int pulsingHero = -1;
        private float heroPulse;

        public void Configure(
            FmodBeatClock clock,
            FmodRhythmJudge judge,
            FightCombatController controller,
            Text playback,
            Text cycle,
            Text warning,
            Text result,
            Text detail,
            Text health,
            Text statistics,
            Image[] beats,
            Slider progress,
            Slider hpBar,
            Image[] enemies,
            Image[] heroes,
            Image shield,
            Image flash)
        {
            beatClock = clock;
            rhythmJudge = judge;
            fight = controller;
            playbackText = playback;
            cycleText = cycle;
            warningText = warning;
            resultText = result;
            detailText = detail;
            healthText = health;
            statisticsText = statistics;
            beatNodes = beats;
            beatProgress = progress;
            healthBar = hpBar;
            enemySlots = enemies;
            heroSlots = heroes;
            shieldEffect = shield;
            screenFlash = flash;
        }

        private void OnEnable()
        {
            if (fight == null)
                return;

            fight.FightBeat += OnFightBeat;
            fight.HeroCalled += OnHeroCalled;
            fight.EnemyAttackResolved += OnEnemyAttackResolved;
            fight.PartyHealthChanged += OnPartyHealthChanged;
            fight.BattleLost += OnBattleLost;
        }

        private void Start()
        {
            OnPartyHealthChanged(fight != null ? fight.PartyHp : 0, fight != null ? fight.MaxPartyHp : 1);
            SetResult("GET READY", Cyan, "Enemy attacks land on every fourth beat.", 2f);
            UpdateStatistics();

            if (shieldEffect != null)
                shieldEffect.color = new Color(Green.r, Green.g, Green.b, 0f);
            if (screenFlash != null)
                screenFlash.color = new Color(Red.r, Red.g, Red.b, 0f);
        }

        private void OnDisable()
        {
            if (fight == null)
                return;

            fight.FightBeat -= OnFightBeat;
            fight.HeroCalled -= OnHeroCalled;
            fight.EnemyAttackResolved -= OnEnemyAttackResolved;
            fight.PartyHealthChanged -= OnPartyHealthChanged;
            fight.BattleLost -= OnBattleLost;
        }

        private void Update()
        {
            UpdatePlaybackReadout();
            UpdateBeatProgress();
            UpdateFades();
            UpdateHeroPulse();
        }

        private void OnFightBeat(FmodBeatClock.BeatSnapshot beat)
        {
            currentBeat = beat.Beat;

            if (cycleText != null)
                cycleText.text = beat.Beat == 4
                    ? $"BAR {beat.Bar:00}  •  BEAT 4  •  HEAVY"
                    : $"BAR {beat.Bar:00}  •  BEAT {beat.Beat}/4";

            if (warningText != null)
            {
                warningText.text = beat.Beat switch
                {
                    1 => "ENEMY TARGETING  •  3 BEATS",
                    2 => "ATTACK IN 2 BEATS",
                    3 => "GUARD ON THE NEXT BEAT",
                    _ => "HEAVY BEAT  •  ENEMY ATTACK"
                };
                warningText.color = beat.Beat == 4 ? Gold : Color.white;
            }

            if (beatNodes != null)
            {
                for (int i = 0; i < beatNodes.Length; i++)
                {
                    if (beatNodes[i] == null)
                        continue;

                    bool active = i == beat.Beat - 1;
                    beatNodes[i].color = active ? (i == 3 ? Gold : Cyan) : Dim;
                    beatNodes[i].rectTransform.localScale = active
                        ? Vector3.one * (i == 3 ? 1.4f : 1.2f)
                        : Vector3.one;
                }
            }

            if (enemySlots != null && enemySlots.Length > 1 && enemySlots[1] != null)
                enemySlots[1].color = beat.Beat == 4 ? Red : new Color(0.52f, 0.18f, 0.22f, 1f);
        }

        private void OnHeroCalled(FightCombatController.HeroCallResult call)
        {
            pulsingHero = HeroIndex(call.Command);
            heroPulse = 1f;

            if (call.Command == FightInputRouter.HeroCommand.Ultimate)
            {
                SetResult("ULTIMATE RESERVED", Purple, "A / R • redesign in progress", 1.5f);
                return;
            }

            switch (call.RhythmResult.Judgement)
            {
                case FmodRhythmJudge.Grade.Perfect:
                    perfectCalls++;
                    if (call.SkillActivated)
                    {
                        SetResult("GUARD READY", Green, FormatDelta(call.RhythmResult.DeltaMs), 1.2f);
                    }
                    else if (call.IsHeavyBeat)
                    {
                        SetResult("SKILL PLACEHOLDER", Gold, call.Message, 1.2f);
                    }
                    else
                    {
                        SetResult("PERFECT CALL", Cyan, "Skill requires beat 4", 0.9f);
                    }
                    break;

                case FmodRhythmJudge.Grade.Miss:
                    missCalls++;
                    SetResult("MISS", Red, FormatDelta(call.RhythmResult.DeltaMs), 1.2f);
                    break;

                default:
                    SetResult("WAIT", Gold, call.Message, 1.2f);
                    break;
            }

            UpdateStatistics();
        }

        private void OnEnemyAttackResolved(FightCombatController.EnemyAttackResult attack)
        {
            if (attack.Blocked)
            {
                blockedAttacks++;
                shieldTimer = 0.8f;
                SetResult("BLOCKED", Green, "Tank absorbed the heavy attack", 1.4f);
            }
            else
            {
                receivedAttacks++;
                flashTimer = 0.55f;
                SetResult("PARTY HIT", Red, $"-{attack.Damage} HP • press X / Q on beat 4", 1.4f);
            }

            UpdateStatistics();
        }

        private void OnPartyHealthChanged(int current, int maximum)
        {
            if (healthText != null)
                healthText.text = $"PARTY HP   {current} / {maximum}";

            if (healthBar != null)
                healthBar.SetValueWithoutNotify(maximum > 0 ? (float)current / maximum : 0f);
        }

        private void OnBattleLost()
        {
            SetResult("DEFEAT", Red, "Stop Play Mode to reset the prototype", 999f);
            if (warningText != null)
                warningText.text = "BATTLE ENDED";
        }

        private void UpdatePlaybackReadout()
        {
            if (playbackText == null || beatClock == null)
                return;

            if (!string.IsNullOrEmpty(beatClock.LastError))
            {
                playbackText.text = $"FMOD ERROR  •  {beatClock.LastError}";
                playbackText.color = Red;
                return;
            }

            float bpm = beatClock.HasTimingAnchor ? beatClock.LatestBeat.Tempo : 0f;
            playbackText.text = beatClock.HasTimingAnchor
                ? $"FMOD LIVE  •  {bpm:0.##} BPM  •  PERFECT ±{rhythmJudge.PerfectWindowMs:0} ms  •  OFFSET {rhythmJudge.JudgementOffsetMs:+0;-0;0} ms"
                : "WAITING FOR FMOD BEAT CALLBACK...";
            playbackText.color = beatClock.HasTimingAnchor ? Cyan : Gold;
        }

        private void UpdateBeatProgress()
        {
            if (beatProgress == null || beatClock == null)
                return;

            if (beatClock.TryGetBeatPhase(out float phase))
                beatProgress.SetValueWithoutNotify(phase);
        }

        private void UpdateFades()
        {
            resultTimer = Mathf.Max(0f, resultTimer - Time.unscaledDeltaTime);
            float resultAlpha = resultTimer > 0f ? Mathf.Clamp01(resultTimer * 4f) : 0.28f;

            if (resultText != null)
            {
                Color color = resultText.color;
                color.a = resultAlpha;
                resultText.color = color;
            }

            if (detailText != null)
            {
                Color color = detailText.color;
                color.a = resultAlpha;
                detailText.color = color;
            }

            shieldTimer = Mathf.Max(0f, shieldTimer - Time.unscaledDeltaTime);
            if (shieldEffect != null)
            {
                float alpha = Mathf.Clamp01(shieldTimer * 3f) * 0.55f;
                shieldEffect.color = new Color(Green.r, Green.g, Green.b, alpha);
                shieldEffect.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.25f, alpha);
            }

            flashTimer = Mathf.Max(0f, flashTimer - Time.unscaledDeltaTime);
            if (screenFlash != null)
            {
                float alpha = Mathf.Clamp01(flashTimer * 4f) * 0.28f;
                screenFlash.color = new Color(Red.r, Red.g, Red.b, alpha);
            }
        }

        private void UpdateHeroPulse()
        {
            if (heroSlots == null)
                return;

            heroPulse = Mathf.MoveTowards(heroPulse, 0f, Time.unscaledDeltaTime * 3.5f);
            for (int i = 0; i < heroSlots.Length; i++)
            {
                if (heroSlots[i] == null)
                    continue;

                float scale = i == pulsingHero ? Mathf.Lerp(1f, 1.14f, heroPulse) : 1f;
                heroSlots[i].rectTransform.localScale = Vector3.one * scale;
            }
        }

        private void SetResult(string title, Color color, string detail, float seconds)
        {
            if (resultText != null)
            {
                resultText.text = title;
                resultText.color = color;
            }

            if (detailText != null)
            {
                detailText.text = detail;
                detailText.color = color;
            }

            resultTimer = seconds;
        }

        private void UpdateStatistics()
        {
            if (statisticsText == null)
                return;

            statisticsText.text =
                $"CALLS  PERFECT {perfectCalls:00}  MISS {missCalls:00}     " +
                $"DEFENSE  BLOCK {blockedAttacks:00}  HIT {receivedAttacks:00}";
        }

        private static int HeroIndex(FightInputRouter.HeroCommand command)
        {
            return command switch
            {
                FightInputRouter.HeroCommand.Tank => 0,
                FightInputRouter.HeroCommand.Support => 1,
                FightInputRouter.HeroCommand.Damage => 2,
                _ => -1
            };
        }

        private static string FormatDelta(double deltaMs)
        {
            string direction = deltaMs < 0.0 ? "EARLY" : "LATE";
            return $"{deltaMs:+0.0;-0.0;0.0} ms  {direction}";
        }
    }
}
