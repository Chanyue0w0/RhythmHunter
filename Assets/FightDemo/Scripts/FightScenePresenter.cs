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

        [Header("Equal Beat Timeline")]
        [SerializeField, Min(100f)] private float timelineWidth = 840f;
        [SerializeField] private float timelineBottomOffset = 76f;
        [SerializeField, Range(1, 8)] private int previewBeats = 3;
        [SerializeField, Min(4f)] private float beatPointSize = 14f;
        [SerializeField] private Color beatPointColor = new(0.2f, 0.92f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float centerFlashStrength = 0.8f;

        private RectTransform timelineRoot;
        private Image timelineCenter;
        private Image[] approachingPoints;
        private FightTimingCalibration timingCalibration;
        private bool EqualBeats => fight != null && fight.UsesEqualBeats;

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
            if (EqualBeats)
            {
                BuildEqualBeatTimeline();
                if (healthText != null && healthText.canvas != null)
                {
                    gameObject.AddComponent<FightDefenseHud>().Configure(fight, healthText.canvas, healthText.font);
                    rhythmJudge.EnablePersonalCalibration();
                    timingCalibration = gameObject.AddComponent<FightTimingCalibration>();
                    timingCalibration.Configure(fight, beatClock, rhythmJudge,
                        fight.GetComponent<FightInputRouter>(), healthText.canvas, healthText.font);
                }
            }
            bool showHealth = fight == null || fight.HealthSystemEnabled;
            if (healthText != null)
                healthText.gameObject.SetActive(showHealth && (fight == null || !fight.UsesFrontHeroControls));
            if (healthBar != null)
                healthBar.gameObject.SetActive(showHealth && (fight == null || !fight.UsesFrontHeroControls));
            if (fight != null && fight.UsesFrontHeroControls && !EqualBeats)
                BuildPixelHeartUi(Mathf.CeilToInt(fight.MaxPartyHp));
            OnPartyHealthChanged(fight != null ? fight.PartyHp : 0f, fight != null ? fight.MaxPartyHp : 1f);
            SetResult(
                "GET READY",
                Cyan,
                EqualBeats ? "X/Y/B = Basic Ability  •  Keyboard Q/W/E  •  Every beat is equal"
                : fight != null && fight.UsesFrontHeroControls
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
            UpdateEqualBeatTimeline();
            UpdateFades();
        }

        private void OnFightBeat(FmodBeatClock.BeatSnapshot beat)
        {
            currentBeat = beat.Beat;

            if (EqualBeats)
            {
                if (fight.BattleEnded)
                {
                    if (warningText != null)
                        warningText.text = "BATTLE ENDED";
                    return;
                }
                if (warningText != null)
                {
                    int remaining = fight.GetEnemyBeatsUntilAttack(beat.GlobalBeat);
                    warningText.text = remaining == 0 ? "ENEMY ATTACK  •  GUARD THIS BEAT"
                        : $"ENEMY ATTACK IN {remaining} BEAT{(remaining == 1 ? string.Empty : "S")}";
                    warningText.color = remaining == 0 ? Gold : Color.white;
                }
                return;
            }

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
                    string title = call.SkillActivated ? "SKILL" : EqualBeats ? "BASIC ABILITY" : "NORMAL ABILITY";
                    SetResult(title, call.SkillActivated ? Gold : Green,
                        call.Message + "  |  " + FormatDelta(call.RhythmResult.DeltaMs), 1.2f);
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
            if (EqualBeats && fight.BattleEnded)
            {
                OnBattleLost();
                return;
            }
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
                string guardHint = EqualBeats ? "X/Y/B = Basic Ability on every beat"
                    : fight != null && fight.UsesFrontHeroControls
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
            if (EqualBeats)
                return;
            if (beatProgress == null || beatClock == null)
                return;

            if (beatClock.TryGetBeatPhase(out float phase))
                beatProgress.SetValueWithoutNotify(phase);
        }

        private void BuildEqualBeatTimeline()
        {
            if (timelineRoot != null || beatProgress == null)
                return;

            // Keep the new HUD under the same optional bottom-UI group as the old HUD.
            Transform oldPanel = beatProgress.transform.parent;
            Transform parent = oldPanel.parent;
            oldPanel.gameObject.SetActive(false);
            Text legend = parent.Find("InputLegend")?.GetComponent<Text>();
            if (legend != null)
                legend.text = "FRONT X/Q   •   MIDDLE Y/W   •   BACK B/E   •   BASIC ON EVERY BEAT";
            GameObject root = new("EqualBeatTimeline", typeof(RectTransform));
            timelineRoot = root.GetComponent<RectTransform>();
            timelineRoot.SetParent(parent, false);
            timelineRoot.anchorMin = timelineRoot.anchorMax = new Vector2(0.5f, 0f);
            timelineRoot.anchoredPosition = new Vector2(0f, timelineBottomOffset);
            timelineRoot.sizeDelta = new Vector2(timelineWidth, 64f);
            if (statisticsText != null)
            {
                statisticsText.transform.SetParent(timelineRoot, false);
                statisticsText.rectTransform.anchorMin = statisticsText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                statisticsText.rectTransform.anchoredPosition = new Vector2(0f, -32f);
                statisticsText.rectTransform.sizeDelta = new Vector2(timelineWidth, 22f);
                statisticsText.alignment = TextAnchor.MiddleCenter;
            }
            CreateTimelineImage("Track", new Vector2(timelineWidth, 2f), Dim);
            CreateTimelineImage("CenterFrame", new Vector2(32f, 44f), beatPointColor);
            timelineCenter = CreateTimelineImage("Center", new Vector2(26f, 38f), Background);
            approachingPoints = new Image[Mathf.Clamp(previewBeats, 1, 8) * 2];
            for (int i = 0; i < approachingPoints.Length; i++)
            {
                approachingPoints[i] = CreateTimelineImage($"BeatPoint_{i}", Vector2.one * beatPointSize, beatPointColor);
                approachingPoints[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                approachingPoints[i].enabled = false;
            }
            // Enemy countdown remains independent of the player's equal-beat rhythm.
            if (warningText != null)
            {
                warningText.transform.SetParent(timelineRoot, false);
                warningText.rectTransform.anchorMin = warningText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                warningText.rectTransform.anchoredPosition = new Vector2(0f, 48f);
                warningText.rectTransform.sizeDelta = new Vector2(timelineWidth, 30f);
                warningText.alignment = TextAnchor.MiddleCenter;
                warningText.text = "WAITING FOR MUSIC";
            }
        }

        private Image CreateTimelineImage(string objectName, Vector2 size, Color color)
        {
            GameObject item = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Image graphic = item.GetComponent<Image>();
            graphic.rectTransform.SetParent(timelineRoot, false);
            graphic.rectTransform.sizeDelta = size;
            graphic.color = color;
            graphic.raycastTarget = false;
            return graphic;
        }

        private void UpdateEqualBeatTimeline()
        {
            if (!EqualBeats || timelineRoot == null)
                return;

            bool ready = beatClock != null && beatClock.IsPlaying && beatClock.HasTimingAnchor
                && beatClock.MillisecondsPerBeat > 0;
            int timelineMs = 0;
            ready = ready && beatClock.TryGetTimelinePositionMs(out timelineMs);
            bool hideCues = timingCalibration != null && timingCalibration.HideRhythmCues;
            foreach (Image point in approachingPoints)
                point.enabled = ready && !hideCues;
            if (!ready)
            {
                timelineCenter.color = Background;
                timelineCenter.rectTransform.localScale = Vector3.one;
                if (warningText != null)
                    warningText.text = "WAITING FOR MUSIC";
                return;
            }

            if (timingCalibration != null && timingCalibration.IsOpen && warningText != null)
                warningText.text = "CALIBRATION  |  FOLLOW THE MUSIC";
            if (hideCues)
            {
                timelineCenter.color = Background;
                timelineCenter.rectTransform.localScale = Vector3.one;
                return;
            }

            // Use the same calibrated time as JudgeNow; no accumulated deltaTime drift.
            // Personal input compensation must not move the musical visual target.
            double evaluatedMs = timelineMs + (rhythmJudge != null ? rhythmJudge.VisualOffsetMs : 0f);
            RenderEqualBeatTimeline(evaluatedMs, beatClock.LatestBeat.TimelinePositionMs, beatClock.MillisecondsPerBeat);
        }

        private void RenderEqualBeatTimeline(double evaluatedMs, double anchorMs, double intervalMs)
        {
            double beatPosition = (evaluatedMs - anchorMs) / intervalMs;
            float phase = (float)(beatPosition - System.Math.Floor(beatPosition));
            int count = approachingPoints.Length / 2;
            for (int i = 0; i < count; i++)
            {
                float distance = (i + 1f - phase) / count * timelineWidth * 0.5f;
                approachingPoints[i * 2].rectTransform.anchoredPosition = new Vector2(-distance, 0f);
                approachingPoints[i * 2 + 1].rectTransform.anchoredPosition = new Vector2(distance, 0f);
            }
            // Timeline-driven pulse freezes with paused audio and resets with a new anchor.
            float pulse = Mathf.Clamp01(1f - (float)(phase * intervalMs / 110.0));
            timelineCenter.color = Color.Lerp(Background, beatPointColor, pulse * centerFlashStrength);
            timelineCenter.rectTransform.localScale = Vector3.one * (1f + pulse * 0.12f);
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
