using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    [DisallowMultipleComponent]
    public sealed class FightBossRhythmBattle : MonoBehaviour
    {
        [SerializeField] private FightCombatController fight;
        [SerializeField] private FightBossPhaseTwoTransition transition;
        [SerializeField] private Camera view;
        [SerializeField] private FightBossRhythmChart chart;
        [Header("Boss animation names: Left / Middle / Right")]
        [SerializeField] private string[] chopAnimations = { "Chop_Left", "Chop_Middle", "Chop_Right" };
        [SerializeField] private string chopIdle = "Chop_Idle";
        [Header("Prototype VFX (pooled)")]
        [SerializeField] private FightBossRhythmVisual slashPrefab;
        [SerializeField] private FightBossRhythmVisual guardPrefab;
        [SerializeField] private Vector3 targetOffset = new(0, .75f, 0);
        [SerializeField, Min(.1f)] private float guardDurationBeats = .5f;
        [Tooltip("Song milliseconds after an incorrect press. 0 disables input lock; does not change arrival times.")]
        [SerializeField, Min(0)] private float missLockMilliseconds = 80;
        [SerializeField, Min(1f)] private float missDamage = 1;
        [Header("Phase Two Playtesting")]
        [Tooltip("測試用：僅 Boss 二階段保留至少 2 HP，護甲與 Miss 照常結算。正式測試死亡流程前請取消勾選。目前依測試需求預設開啟。")]
        [SerializeField] private bool preventPlayerDeathForTesting = true;
        public bool PreventPlayerDeathForTesting => preventPlayerDeathForTesting;
        public enum HitSoundMode { Immediate, AlignToArrival }
        [Header("Phase Two Hit Audio (existing successful-input event)")]
        [SerializeField] private HitSoundMode hitSoundMode = HitSoundMode.AlignToArrival;
        [Tooltip("Song milliseconds. Positive plays later, negative earlier. Does not move notes or judgement.")]
        [SerializeField] private float hitSoundOffsetMs;
        [Tooltip("Minimum real-time lead for music timeline scheduling; shorter lead uses immediate feedback.")]
        [SerializeField, Min(0)] private float minimumSoundLeadMs = 25;
        public double LastInputErrorMs { get; private set; }
        public double MeanInputErrorMs => errorCount > 0 ? errorSum / errorCount : 0;
        public double InputDeviationMs => errorCount > 0 ? Math.Sqrt(Math.Max(0,errorSquareSum/errorCount-MeanInputErrorMs*MeanInputErrorMs)) : 0;
        public int InputSampleCount => errorCount;
        private int errorCount;
        private double errorSum, errorSquareSum;
        [Header("Victory presentation")]
        [SerializeField] private GameObject victoryExplosion;
        [SerializeField, Min(.1f)] private float victoryWaitSeconds = 1.2f;
        public int Hits { get; private set; }
        public int MissedNotes { get; private set; }
        public int BadInputs { get; private set; }
        public int SkippedTransitionNotes { get; private set; }
        public string LastJudgement { get; private set; } = "";
        public string ChartError { get; private set; }
        public double CurrentBeat { get; private set; }
        public int CreatedVisuals { get; private set; }
        public int LaunchedNotes { get; private set; }
        public float LaunchProgress => Mathf.Clamp01((LaunchedNotes - 1f) / Mathf.Max(1, notes.Count - SkippedTransitionNotes - 1));
        public bool Running => started && !outro && fight && fight.BossPhaseTwoActive && !fight.BattleEnded && !fight.DefeatSlowMotionActive;
        public event Action<int, bool, double> NoteResolved;
        private sealed class Note
        {
            public double beat;
            public int lane;
            public FightBossRhythmChart.Note source;
            public bool animationStarted, accepted, resolved, skipped;
            public FightBossRhythmVisual visual;
        }
        private readonly List<Note> notes = new();
        private readonly Stack<FightBossRhythmVisual> slashPool = new(), guardPool = new();
        private readonly List<(FightBossRhythmVisual visual, int lane, double end, bool success)> guards = new();
        private readonly List<FightBossRhythmVisual> allVisuals = new();
        private readonly FightCharacterCombatAnimator.Sequence[] animations = new FightCharacterCombatAnimator.Sequence[3];
        private readonly double[] animationLeadBeats = new double[3];
        private Note displayedAttack;
        private readonly FightUnitSlot[] heroes = new FightUnitSlot[3];
        private FightUnitSlot boss;
        private bool started, originKnown, readySeen, outro, animationPlaying;
        private double originBeat, lockedUntil = double.NegativeInfinity;
        private float outroElapsed;
        private void OnEnable()
        {
            if (!fight) fight = GetComponent<FightCombatController>();
            if (!transition) transition = GetComponent<FightBossPhaseTwoTransition>();
            if (fight) { fight.BossPhaseTwoStarted += Begin; fight.BossPhaseTwoMusicEnded += BeginOutro; }
        }
        private void Begin()
        {
            Clear(); Hits = MissedNotes = BadInputs = SkippedTransitionNotes = 0; LastJudgement = "";
            ChartError = null;
            errorCount=0;errorSum=errorSquareSum=LastInputErrorMs=0;
            fight.AudioPlayer.PrepareBeatSounds(fight.BossHitSoundPath);
            string error = null;
            if (!chart || !chart.Validate(out error))
            { ChartError = chart ? error : "Assign a Boss rhythm chart."; Debug.LogError(ChartError, this); return; }
            if (!slashPrefab || !guardPrefab || !transition || !view)
            { ChartError = "Assign transition, camera, slash and guard prefabs."; Debug.LogError(ChartError, this); return; }
            boss = fight.RosterManager.ActiveEnemies.FirstOrDefault(x => x && x.BossPhaseOneProtected);
            if (!boss) { ChartError = "Protected Boss missing."; Debug.LogError(ChartError, this); return; }
            heroes[0] = fight.FrontHero.UnitSlot; heroes[1] = fight.SecondHero.UnitSlot; heroes[2] = fight.ThirdHero.UnitSlot;
            foreach (var entry in chart.notes) notes.Add(new Note { source = entry, beat = entry.arrivalBeat, lane = (int)entry.lane });
            for (int lane = 0; lane < 3; lane++)
            {
                var source = boss.CombatAnimator.FindSequence(chopAnimations[lane]);
                if (source == null || source.Frames.Count == 0) { ChartError = "Missing animation: " + chopAnimations[lane]; continue; }
                // Playback copy only: dense chart phrases may fit a chop into one beat, without changing authored data.
                var copy = new FightCharacterCombatAnimator.Sequence();
                copy.Configure(source.Animation, source.Frames, source.FramesPerSecond, -1, -1, -1);
                float total = Enumerable.Range(0, source.Frames.Count).Sum(i => source.GetFrameBeats(i));
                copy.ConfigureFrameBeats(Enumerable.Range(0, source.Frames.Count).Select(i => source.GetFrameBeats(i) / Mathf.Max(.01f, total) * chart.animationBeats));
                animations[lane] = copy;
                // First marked frame is the release point; unmarked legacy sequences release at the end.
                int releaseFrame = Enumerable.Range(0, source.Frames.Count).Where(source.PlaysAttackEffectAt).DefaultIfEmpty(source.Frames.Count).First();
                animationLeadBeats[lane] = Enumerable.Range(0, releaseFrame).Sum(i => (double)copy.GetFrameBeats(i));
            }
            if (ChartError != null) { Debug.LogError(ChartError, this); notes.Clear(); return; }
            started = true;
            Prewarm(false); Prewarm(true);
        }
        private bool ReadBeat(out double beat)
        {
            beat = 0; var clock = fight.BeatClock;
            if (!clock || clock.BossSectionJumpInProgress || !clock.HasTimingAnchor ||
                !clock.TryGetTimelinePositionMs(out int continuous) || !clock.TryGetRawTimelinePositionMs(out int raw) ||
                !clock.TryGetMarkerPosition("Normal_End", out int start)) return false;
            double global = clock.LatestBeat.GlobalBeat + (continuous - clock.LatestBeat.TimelinePositionMs) / clock.MillisecondsPerBeat;
            if (!originKnown)
            {
                originBeat = global - (raw - start) / clock.MillisecondsPerBeat;
                // FMOD markers use integer milliseconds; remove sub-millisecond marker rounding on a whole beat.
                if (Math.Abs(originBeat - Math.Round(originBeat)) < .02) originBeat = Math.Round(originBeat);
                originKnown = true;
            }
            beat = global - originBeat;
            foreach (var note in notes)
                note.beat = note.source.EffectiveBeat(clock.MillisecondsPerBeat, beat, raw - start);
            notes.Sort((a, b) => a.beat.CompareTo(b.beat));
            return true;
        }
        private void Update()
        {
            if (!started) return;
            if (!fight || !fight.BossPhaseTwoActive || fight.BattleEnded || fight.DefeatSlowMotionActive) { Clear(); return; }
            foreach (var visual in allVisuals) if (visual && visual.gameObject.activeSelf) visual.SetPaused(fight.IsPaused || fight.TimingCalibrationActive);
            if (fight.IsPaused || fight.TimingCalibrationActive) return;
            if (outro)
            {
                outroElapsed += Time.deltaTime;
                if (outroElapsed >= victoryWaitSeconds && (!boss || !boss.DeathPresentationPending)) fight.CompleteBossRhythmBattle();
                return;
            }
            if (!ReadBeat(out double beat)) return;
            CurrentBeat = beat;
            boss.SetBossSurvivalHealth(1 - fight.BossPhaseTwoSecondsRemaining / Mathf.Max(.01f, fight.BossPhaseTwoDurationSeconds));
            if (!transition.Ready) return;
            if (!readySeen)
            {
                readySeen = true;
                if (fight.BeatClock.TryGetRawTimelinePositionMs(out int raw) && fight.BeatClock.TryGetMarkerPosition("Normal_End", out int start) &&
                    !chart.ValidateTiming(fight.BeatClock.MillisecondsPerBeat, beat, raw - start, fight.BossRhythmJudge.PerfectWindowMs, out string timingError))
                { Debug.LogWarning("[Boss chart] " + timingError, this); }
                foreach (var note in notes) if (note.beat - chart.travelBeats - animationLeadBeats[note.lane] < beat)
                { note.resolved = note.skipped = true; SkippedTransitionNotes++; }
            }
            if (animationPlaying && boss.CombatAnimator.VisualSequenceComplete)
            { boss.CombatAnimator.InterruptPlayback(); boss.CombatAnimator.SetIdleOverride(chopIdle); animationPlaying = false; }
            AdvanceNotes(beat);
            if (animationPlaying && displayedAttack != null)
                boss.CombatAnimator.SampleVisualSequenceBeats(beat - (displayedAttack.beat - chart.travelBeats - animationLeadBeats[displayedAttack.lane]));
            for (int i = guards.Count - 1; i >= 0; i--)
            {
                var guard = guards[i]; float remaining = (float)(guard.end - beat);
                if (remaining <= 0) { Return(guard.visual, true); guards.RemoveAt(i); continue; }
                guard.visual.Present(Target(guard.lane), view.transform.rotation, 1 + .15f * Mathf.Sin(remaining / guardDurationBeats * Mathf.PI),
                    guard.success ? new Color(.25f, .9f, 1, Mathf.Clamp01(remaining / guardDurationBeats))
                        : new Color(1, .2f, .12f, Mathf.Clamp01(remaining / guardDurationBeats)));
            }
        }
        private void AdvanceNotes(double beat)
        {
            double window = fight.BossRhythmJudge.PerfectWindowMs / fight.BeatClock.MillisecondsPerBeat;
            foreach (var note in notes)
            {
                if (note.resolved) continue;
                if (note.beat - chart.travelBeats - animationLeadBeats[note.lane] > beat) continue;
                if (!note.animationStarted)
                {
                    note.animationStarted = true;
                    if (beat < note.beat - chart.travelBeats + .01)
                    { boss.CombatAnimator.PlayVisualSequence(boss.CombatAnimator.TargetRenderer, animations[note.lane], false, true); displayedAttack = note; animationPlaying = true; }
                }
                if (beat >= note.beat - chart.travelBeats && note.visual == null)
                { note.visual = Rent(false); LaunchedNotes++; }
                if (note.visual)
                {
                    var origin = boss.CastEffectAnchor ? boss.CastEffectAnchor.position : boss.ActorRoot.position;
                    float t = Mathf.Clamp01((float)((beat - (note.beat - chart.travelBeats)) / chart.travelBeats));
                    note.visual.PresentFlight(Vector3.Lerp(origin, Target(note.lane), t), Target(note.lane)-origin, view.transform.rotation, 1,
                        note.accepted ? new Color(1, .85f, .25f, 1) : new Color(1, .2f, .12f, 1));
                }
                if (note.accepted && beat >= note.beat) Resolve(note, true);
                // Delay only failure settlement through the same calibrated late-input window; visual arrival stays exact.
                else if (!note.accepted && beat > note.beat + window + Math.Max(0, -fight.BossRhythmJudge.JudgementOffsetMs) / fight.BeatClock.MillisecondsPerBeat)
                    Resolve(note, false);
                if (fight.DefeatSlowMotionActive || fight.BattleEnded) break;
            }
        }
        public void Submit(FightInputRouter.HeroCommand command, double inputAgeMs, string profile)
        {
            int lane = (int)command;
            if (!Running || !transition.Ready || lane < 0 || lane > 2 || fight.IsPaused || fight.TimingCalibrationActive || !originKnown) return;
            var judge = fight.BossRhythmJudge; judge.SetInputProfile(profile);
            if (!ReadBeat(out double now)) return;
            CurrentBeat = now;
            if (!judge.TryGetEvaluatedBeat(inputAgeMs, out double global)) return;
            JudgeLane(lane, global - originBeat);
        }
        private void JudgeLane(int lane, double beat)
        {
            if (beat < lockedUntil) return;
            double window = fight.BossRhythmJudge.PerfectWindowMs / fight.BeatClock.MillisecondsPerBeat;
            var note = FindNearestNote(lane, beat, window);
            var guard = Rent(true);
            guards.Add((guard, lane, Math.Max(CurrentBeat, note != null ? note.beat : CurrentBeat) + guardDurationBeats, note != null));
            guard.Present(Target(lane), view.transform.rotation, 1, note != null ? Color.cyan : Color.red);
            if (note == null)
            { BadInputs++; LastJudgement = "MISS"; lockedUntil = beat + missLockMilliseconds / fight.BeatClock.MillisecondsPerBeat; heroes[lane]?.PlayInputFeedback(false); return; }
            note.accepted = true; LastJudgement = "BLOCK";
            LastInputErrorMs=(beat-note.beat)*fight.BeatClock.MillisecondsPerBeat;
            errorCount++;errorSum+=LastInputErrorMs;errorSquareSum+=LastInputErrorMs*LastInputErrorMs;
            if(fight.BeatClock.TryGetTimelinePositionMs(out int nowMs))
                fight.AudioPlayer.PlayBeatSound(fight.BossHitSoundPath,fight.BossHitSoundVolume,
                    nowMs+(note.beat-CurrentBeat)*fight.BeatClock.MillisecondsPerBeat+hitSoundOffsetMs,
                    hitSoundMode==HitSoundMode.AlignToArrival,minimumSoundLeadMs);
            // Successful input owns audio; impact resolves at arrival without replaying the sound.
            if (CurrentBeat >= note.beat) Resolve(note, true);
        }
        private Note FindNearestNote(int lane, double beat, double window)
        {
            Note best = null;
            double bestDistance = double.PositiveInfinity;
            foreach (var candidate in notes)
            {
                if (candidate.resolved || candidate.accepted || candidate.lane != lane) continue;
                double distance = Math.Abs(candidate.beat - beat);
                if (distance > window + 1e-6) continue;
                if (best == null || distance < bestDistance - 1e-9 ||
                    (Math.Abs(distance - bestDistance) <= 1e-9 && candidate.beat < best.beat))
                { best = candidate; bestDistance = distance; }
            }
            return best;
        }
        private void Resolve(Note note, bool blocked)
        {
            if (note.resolved) return;
            note.resolved = true; if (note.visual) { Return(note.visual, false); note.visual = null; }
            if (blocked) { Hits++; fight.ReportBossGuard(heroes[note.lane]); }
            else
            {
                MissedNotes++; LastJudgement = "MISS";
                fight.ApplyPartyDamage(missDamage, (long)Math.Round(originBeat + note.beat));
                heroes[note.lane]?.PlayInputFeedback(false);
            }
            NoteResolved?.Invoke(note.lane, blocked, note.beat);
        }
        private Vector3 Target(int lane)
        {
            var actor = heroes[lane];
            var foot = actor && actor.ActorInstance ? actor.ActorInstance.transform.Find("FootAnchor") : null;
            return (foot ? foot.position : actor ? actor.transform.position : transform.position) + targetOffset;
        }
        private FightBossRhythmVisual Rent(bool guard)
        {
            var pool = guard ? guardPool : slashPool;
            FightBossRhythmVisual visual;
            if (pool.Count > 0) visual = pool.Pop();
            else { visual = Instantiate(guard ? guardPrefab : slashPrefab, transform); allVisuals.Add(visual); CreatedVisuals++; }
            visual.gameObject.SetActive(true); return visual;
        }
        private void Prewarm(bool guard)
        {
            var pool = guard ? guardPool : slashPool;
            while (pool.Count < 8)
            {
                var visual = Instantiate(guard ? guardPrefab : slashPrefab, transform);
                allVisuals.Add(visual); CreatedVisuals++; Return(visual, guard);
            }
        }
        private void Return(FightBossRhythmVisual visual, bool guard)
        { if (!visual) return; visual.gameObject.SetActive(false); (guard ? guardPool : slashPool).Push(visual); }
        private void BeginOutro()
        {
            if (!started || outro || fight.BattleEnded || fight.DefeatSlowMotionActive) return;
            if (ReadBeat(out double beat)) { CurrentBeat = beat; AdvanceNotes(beat); }
            if (fight.DefeatSlowMotionActive || fight.BattleEnded) return;
            outro = true; outroElapsed = 0; ClearVisuals();
            if (boss)
            {
                if (victoryExplosion)
                {
                    var effect = Instantiate(victoryExplosion, boss.ActorRoot.position, victoryExplosion.transform.rotation);
                    (effect.GetComponent<FightBattleEffect>() ?? effect.AddComponent<FightBattleEffect>()).Configure(fight, false, 5);
                }
                boss.ReleaseBossPhaseOneProtection(); boss.TakeDamage(boss.CurrentHp + boss.MaxHp);
            }
        }
        private void ClearVisuals()
        {
            foreach (var note in notes) if (note.visual) { Return(note.visual, false); note.visual = null; }
            foreach (var guard in guards) Return(guard.visual, true); guards.Clear();
        }
        private void Clear()
        {
            if(fight)fight.AudioPlayer.StopBeatSounds();
            ClearVisuals(); notes.Clear(); started = originKnown = readySeen = outro = animationPlaying = false;
            lockedUntil = double.NegativeInfinity; CurrentBeat = 0; LaunchedNotes = 0; displayedAttack = null;
        }
        private void OnDisable()
        {
            if (fight) { fight.BossPhaseTwoStarted -= Begin; fight.BossPhaseTwoMusicEnded -= BeginOutro; }
            Clear(); foreach (var visual in allVisuals) if (visual) Destroy(visual.gameObject);
            allVisuals.Clear(); slashPool.Clear(); guardPool.Clear();
        }
    }
}
