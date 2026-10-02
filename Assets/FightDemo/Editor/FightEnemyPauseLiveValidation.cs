using System;
using System.IO;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightEnemyPauseLiveValidation
    {
        const string Request = "Temp/FightEnemyPauseLiveValidation.request", Result = "Temp/FightEnemyPauseLiveValidation.result";
        const string Running = "EnemyPauseLive.Running", Previous = "EnemyPauseLive.Previous";
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        static FightCombatController fight;
        static FightCharacterCombatAnimator animator;
        static Sprite sprite;
        static int phase, frame, effects, damageEvents, frozenPosition;
        static float frameRemainder, hp;
        static long startBeat;
        static double deadline, checkpoint;
        static FightEnemyPauseLiveValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredEditMode) return;
                if (SessionState.GetBool(Running, false)) Finish("FAIL: interrupted");
                if (Application.isBatchMode && File.Exists(Result))
                    EditorApplication.Exit(File.ReadAllText(Result).Contains("PASS:") ? 0 : 1);
            };
        }
        public static void RunBatch() => File.WriteAllText(Request, "run");
        static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
        static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, Flags).GetValue(o);
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void Start(FmodBeatClock clock)
        {
            animator = fight.ActiveEnemySlot.CombatAnimator;
            Call(animator, "AdvancePlayback", .08f);
            frame = animator.ActiveFrame; sprite = animator.TargetRenderer.sprite;
            frameRemainder = Field<float>(animator, "frameElapsed");
            effects = animator.AttackEffectEventCount; damageEvents = animator.DamageEventCount;
            hp = fight.PartyHp; startBeat = clock.LatestBeat.GlobalBeat;
            typeof(FightCombatController).GetField("teamSkillGauge", Flags).SetValue(fight, fight.TeamSkillGaugeMax);
            Check(fight.TryStartTeamSkill() && fight.EnemyActionsPaused, "Team Skill freezes immediately.");
        }
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling) return;
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetString(Previous, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                SessionState.SetBool(Running, true);
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/FightDemo/Scenes/FightScene3.unity");
                File.WriteAllText(Result, "Starting live enemy suspension validation.\n"); EditorApplication.isPlaying = true; return;
            }
            try
            {
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 90;
                Check(EditorApplication.timeSinceStartup < deadline, "Timed out.");
                Application.runInBackground = true;
                var clock = UnityEngine.Object.FindFirstObjectByType<FmodBeatClock>();
                if (clock == null || clock.ReceivedBeatCount < 1) return;
                FMODUnity.RuntimeManager.CoreSystem.mixerResume();
                fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                var pause = UnityEngine.Object.FindFirstObjectByType<FightPauseController>();
                if (phase == 0 && fight.HasPendingEnemyAttack && fight.CurrentBossPhase == FightCombatController.BossPhase.Attack)
                {
                    Start(clock); phase = 1;
                }
                if ((phase == 1 || phase == 2 || phase == 3 || phase == 4) && fight.EnemyActionsPaused)
                {
                    Check(animator.PlaybackPaused && animator.ActiveFrame == frame && animator.TargetRenderer.sprite == sprite,
                        "Enemy must keep the exact attack frame through the whole Team Skill.");
                    Check(Field<float>(animator, "frameElapsed") == frameRemainder && animator.AttackEffectEventCount == effects && animator.DamageEventCount == damageEvents,
                        "Fractional animation time and all callbacks must stop.");
                    Check(fight.PartyHp == hp && fight.BossBeatsRemaining == 1, "Damage and boss countdown must stop.");
                }
                if (phase == 1 && clock.LatestBeat.GlobalBeat >= startBeat + 2)
                {
                    ScreenCapture.CaptureScreenshot("Temp/EnemyPause-held.png");
                    pause.SetPaused(true); phase = 2; checkpoint = EditorApplication.timeSinceStartup + .3;
                }
                if (phase == 2 && EditorApplication.timeSinceStartup >= checkpoint)
                {
                    clock.TryGetTimelinePositionMs(out frozenPosition); phase = 3; checkpoint = EditorApplication.timeSinceStartup + 1;
                }
                if (phase == 3 && EditorApplication.timeSinceStartup >= checkpoint)
                {
                    clock.TryGetTimelinePositionMs(out int now); Check(Math.Abs(now - frozenPosition) <= 1, "Global pause freezes FMOD too.");
                    pause.SetPaused(false); phase = 4;
                }
                if (phase == 4 && !fight.TeamSkillRunning)
                {
                    Check(!animator.PlaybackPaused && clock.LatestBeat.GlobalBeat >= startBeat + 13, "Full 12-beat performance releases the animator.");
                    Check(animator.ActiveFrame >= frame, "The same attack resumes instead of restarting.");
                    phase = 5;
                }
                if (phase == 5 && fight.HasPendingEnemyAttack && fight.CurrentBossPhase == FightCombatController.BossPhase.Charge)
                {
                    // One setup point, followed by Mage's actual Team Skill (+2), must
                    // interrupt this suspended charge and discard the old attack.
                    Call(fight, "ApplyBossBreak", 1, clock.LatestBeat.GlobalBeat);
                    Start(clock); phase = 6;
                }
                if (phase == 6)
                {
                    if (fight.EnemyActionsPaused)
                    {
                        Check(animator.TargetRenderer.sprite == sprite && animator.PlaybackPaused, "Even an interrupted enemy remains visually frozen.");
                        if (fight.CurrentBossPhase == FightCombatController.BossPhase.Break)
                            Check(!animator.IsPlaying && !fight.HasPendingEnemyAttack, "Mage interrupt discards the suspended attack.");
                    }
                    else
                    {
                        Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Break && fight.BossBeatsRemaining == 4 && !fight.HasPendingEnemyAttack,
                            "Interrupted attack cannot resume and Break retains all four beats.");
                        Check(Mathf.Abs(clock.LatestBeat.Tempo - 121.15f) < .01f, "FMOD tempo unchanged.");
                        Finish("PASS: live FMOD advances during 12-beat enemy freeze; exact mid-attack sprite/frame/fraction/callback hold, global pause overlap, resume same attack, Mage Team Skill interrupts second suspended charge, full Break remains afterward.");
                    }
                }
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        static void Finish(string message)
        {
            File.AppendAllText(Result, message + "\n"); File.Delete(Request);
            var pause = UnityEngine.Object.FindFirstObjectByType<FightPauseController>(); if (pause != null) pause.SetPaused(false);
            string previous = SessionState.GetString(Previous, "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool(Running, false); EditorApplication.isPlaying = false;
            phase = 0; deadline = 0; fight = null; animator = null;
        }
    }
}
