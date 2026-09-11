using System.Collections.Generic;
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

        [Header("Screen Feedback")]
        [SerializeField] private Image screenFlash;

        private float resultTimer;
        private float flashTimer;
        private int currentBeat;
        private int perfectCalls;
        private int missCalls;
        private int blockedAttacks;
        private int receivedAttacks;
        private FightCombatController subscribedFight;
        private RectTransform pixelHeartRoot;
        private readonly List<Image[]> pixelHearts = new();
        private readonly List<bool[]> pixelHeartLeftHalves = new();

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
            Image flash)
        {
            Unsubscribe();
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
            screenFlash = flash;
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || fight == null)
                return;

            Unsubscribe();
            subscribedFight = fight;
            subscribedFight.FightBeat += OnFightBeat;
            subscribedFight.HeroCalled += OnHeroCalled;
            subscribedFight.EnemyAttackResolved += OnEnemyAttackResolved;
            subscribedFight.PartyHealthChanged += OnPartyHealthChanged;
            subscribedFight.BattleLost += OnBattleLost;
        }

        private void Start()
        {
            bool showHealth = fight == null || fight.HealthSystemEnabled;
            if (healthText != null)
                healthText.gameObject.SetActive(showHealth && (fight == null || !fight.UsesFrontHeroControls));
            if (healthBar != null)
                healthBar.gameObject.SetActive(showHealth && (fight == null || !fight.UsesFrontHeroControls));
            if (fight != null && fight.UsesFrontHeroControls)
                BuildPixelHeartUi(Mathf.CeilToInt(fight.MaxPartyHp));
            OnPartyHealthChanged(fight != null ? fight.PartyHp : 0f, fight != null ? fight.MaxPartyHp : 1f);
            SetResult(
                "GET READY",
                Cyan,
                fight != null && fight.UsesFrontHeroControls
                    ? "X/Y/B = Guard / Heal / Damage  •  Keyboard Q/W/E  •  Beat 4 = Skill"
                    : "Enemy attacks land on every fourth beat.",
                2f);
            UpdateStatistics();

            if (screenFlash != null)
                screenFlash.color = new Color(Red.r, Red.g, Red.b, 0f);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (subscribedFight == null)
                return;

            subscribedFight.FightBeat -= OnFightBeat;
            subscribedFight.HeroCalled -= OnHeroCalled;
            subscribedFight.EnemyAttackResolved -= OnEnemyAttackResolved;
            subscribedFight.PartyHealthChanged -= OnPartyHealthChanged;
            subscribedFight.BattleLost -= OnBattleLost;
            subscribedFight = null;
        }

        private void Update()
        {
            UpdatePlaybackReadout();
            UpdateBeatProgress();
            UpdateFades();
        }

        private void OnFightBeat(FmodBeatClock.BeatSnapshot beat)
        {
            currentBeat = beat.Beat;

            if (fight != null && fight.UsesFrontHeroControls)
            {
                bool enemyAttackBeat = fight.IsEnemyAttackBeat(beat.GlobalBeat);
                int beatsUntilEnemyAttack = fight.GetEnemyBeatsUntilAttack(beat.GlobalBeat);
                if (cycleText != null)
                    cycleText.text = $"BAR {beat.Bar:00}  •  BEAT {beat.Beat}/4";
                if (warningText != null)
                {
                    warningText.text = enemyAttackBeat
                        ? $"HEAVY BEAT  •  HERO SKILLS  •  ENEMY ATTACK  •  MANA {fight.EnemyCurrentMana}/{fight.EnemyMaxMana}"
                        : $"NORMAL ABILITIES  •  ENEMY ATTACK IN {beatsUntilEnemyAttack} BEAT{(beatsUntilEnemyAttack == 1 ? string.Empty : "S")}  •  " +
                          $"MANA {fight.EnemyCurrentMana}/{fight.EnemyMaxMana}";
                    warningText.color = enemyAttackBeat ? Gold : Color.white;
                }

                UpdateBeatNodes(beat.Beat);
                return;
            }

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

            UpdateBeatNodes(beat.Beat);
        }

        private void OnHeroCalled(FightCombatController.HeroCallResult call)
        {
            if (fight != null && fight.UsesFrontHeroControls)
            {
                if (call.RhythmResult.Judgement == FmodRhythmJudge.Grade.Perfect)
                {
                    perfectCalls++;
                    string title = call.SkillActivated ? "SKILL" : "NORMAL ABILITY";
                    SetResult(title, call.SkillActivated ? Gold : Green, call.Message, 1.2f);
                }
                else
                {
                    missCalls++;
                    SetResult("MISS", Red, FormatDelta(call.RhythmResult.DeltaMs), 1.2f);
                }

                UpdateStatistics();
                return;
            }

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
                SetResult(
                    "BLOCKED",
                    Green,
                    $"Enemy normal attack blocked • Mana {fight.EnemyCurrentMana}/{fight.EnemyMaxMana}",
                    1.4f);
            }
            else
            {
                receivedAttacks++;
                flashTimer = 0.55f;
                string guardHint = fight != null && fight.UsesFrontHeroControls
                    ? "beat 4 activates hero skills"
                    : "press X / Q on beat 4";
                SetResult(
                    "PARTY HIT",
                    Red,
                    $"Enemy normal attack • Mana {fight.EnemyCurrentMana}/{fight.EnemyMaxMana} • {guardHint}",
                    1.4f);
            }

            UpdateStatistics();
        }

        private void OnPartyHealthChanged(float current, float maximum)
        {
            if (healthText != null)
                healthText.text = $"PLAYER HP   {current:0.#} / {maximum:0.#}";

            if (healthBar != null)
                healthBar.SetValueWithoutNotify(maximum > 0f ? current / maximum : 0f);

            UpdatePixelHearts(current);
        }

        private void BuildPixelHeartUi(int heartCount)
        {
            if (pixelHeartRoot != null || healthText == null || healthText.canvas == null)
                return;

            GameObject root = new("PixelHeartHealth", typeof(RectTransform));
            pixelHeartRoot = root.GetComponent<RectTransform>();
            pixelHeartRoot.SetParent(healthText.canvas.transform, false);
            pixelHeartRoot.anchorMin = Vector2.one;
            pixelHeartRoot.anchorMax = Vector2.one;
            pixelHeartRoot.pivot = Vector2.one;
            pixelHeartRoot.anchoredPosition = new Vector2(-32f, -28f);
            pixelHeartRoot.sizeDelta = new Vector2(260f, 76f);

            GameObject labelObject = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(pixelHeartRoot, false);
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(1f, 1f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(0f, 22f);
            Text label = labelObject.GetComponent<Text>();
            label.font = healthText.font;
            label.fontSize = 15;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.UpperRight;
            label.color = Color.white;
            label.text = "PLAYER HP";

            string[] pattern =
            {
                ".XX.XX.",
                "XXXXXXX",
                "XXXXXXX",
                ".XXXXX.",
                "..XXX..",
                "...X..."
            };
            const float pixel = 7f;
            const float heartWidth = 49f;
            const float spacing = 9f;
            int count = Mathf.Max(1, heartCount);
            float totalWidth = count * heartWidth + (count - 1) * spacing;
            float startX = 260f - totalWidth;
            for (int heartIndex = 0; heartIndex < count; heartIndex++)
            {
                List<Image> pixels = new();
                List<bool> leftHalf = new();
                for (int y = 0; y < pattern.Length; y++)
                {
                    for (int x = 0; x < pattern[y].Length; x++)
                    {
                        if (pattern[y][x] != 'X')
                            continue;

                        GameObject pixelObject = new($"Heart{heartIndex + 1}_Pixel{x}_{y}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        RectTransform pixelRect = pixelObject.GetComponent<RectTransform>();
                        pixelRect.SetParent(pixelHeartRoot, false);
                        pixelRect.anchorMin = new Vector2(0f, 1f);
                        pixelRect.anchorMax = new Vector2(0f, 1f);
                        pixelRect.pivot = new Vector2(0f, 1f);
                        pixelRect.anchoredPosition = new Vector2(
                            startX + heartIndex * (heartWidth + spacing) + x * pixel,
                            -26f - y * pixel);
                        pixelRect.sizeDelta = new Vector2(pixel, pixel);
                        Image image = pixelObject.GetComponent<Image>();
                        image.raycastTarget = false;
                        pixels.Add(image);
                        leftHalf.Add(x <= 2);
                    }
                }

                pixelHearts.Add(pixels.ToArray());
                pixelHeartLeftHalves.Add(leftHalf.ToArray());
            }
        }

        private void UpdatePixelHearts(float current)
        {
            Color filled = new(1f, 0.16f, 0.24f, 1f);
            Color empty = new(0.2f, 0.05f, 0.08f, 0.9f);
            for (int heartIndex = 0; heartIndex < pixelHearts.Count; heartIndex++)
            {
                float remaining = current - heartIndex;
                bool full = remaining >= 1f;
                bool half = !full && remaining >= 0.5f;
                Image[] pixels = pixelHearts[heartIndex];
                bool[] leftHalf = pixelHeartLeftHalves[heartIndex];
                for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
                    pixels[pixelIndex].color = full || (half && leftHalf[pixelIndex]) ? filled : empty;
            }
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

            flashTimer = Mathf.Max(0f, flashTimer - Time.unscaledDeltaTime);
            if (screenFlash != null)
            {
                float alpha = Mathf.Clamp01(flashTimer * 4f) * 0.28f;
                screenFlash.color = new Color(Red.r, Red.g, Red.b, alpha);
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

            statisticsText.text = fight != null && fight.UsesFrontHeroControls
                ? $"PARTY INPUT  PERFECT {perfectCalls:00}  MISS {missCalls:00}     ENEMY HITS {receivedAttacks:00}"
                : $"CALLS  PERFECT {perfectCalls:00}  MISS {missCalls:00}     " +
                  $"DEFENSE  BLOCK {blockedAttacks:00}  HIT {receivedAttacks:00}";
        }

        private void UpdateBeatNodes(int beat)
        {
            if (beatNodes == null)
                return;

            for (int i = 0; i < beatNodes.Length; i++)
            {
                if (beatNodes[i] == null)
                    continue;

                bool active = i == beat - 1;
                beatNodes[i].color = active ? (i == 3 ? Gold : Cyan) : Dim;
                beatNodes[i].rectTransform.localScale = active
                    ? Vector3.one * (i == 3 ? 1.4f : 1.2f)
                    : Vector3.one;
            }
        }

        private static string FormatDelta(double deltaMs)
        {
            string direction = deltaMs < 0.0 ? "EARLY" : "LATE";
            return $"{deltaMs:+0.0;-0.0;0.0} ms  {direction}";
        }
    }
}
