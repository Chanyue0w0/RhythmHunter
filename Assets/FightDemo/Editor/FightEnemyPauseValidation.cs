using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightEnemyPauseValidation
    {
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        const string Request = "Temp/FightEnemyPauseValidation.request";
        static FightEnemyPauseValidation() => EditorApplication.update += Tick;
        public static void RunBatch()
        {
            if (Application.isBatchMode) EditorSceneManager.OpenScene("Assets/FightDemo/Scenes/FightScene3.unity");
            File.WriteAllText(Request, "run"); Tick();
            FightScene3BeatValidation.Run();
            File.WriteAllText("Temp/FightBossValidation.request", "run");
            typeof(FightBossValidation).GetMethod("Tick", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            File.WriteAllText("Temp/FightTeamSkillValidation.request", "run");
            typeof(FightTeamSkillValidation).GetMethod("Tick", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            foreach (string name in new[] { "FightEnemyPauseValidation", "FightScene3BeatValidation", "FightBossValidation", "FightTeamSkillValidation" })
                if (!File.ReadAllText("Temp/" + name + ".result").StartsWith("PASS")) throw new Exception(name + " failed; see Temp result.");
        }
        static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
        static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, Flags).GetValue(o);
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
            {
                if (active.isDirty) return; // Never replace an unsaved user scene.
                EditorSceneManager.OpenScene("Assets/FightDemo/Scenes/FightScene3.unity");
            }
            File.Delete(Request);
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/FightScene3.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                void Beat(int n) => Call(fight, "OnBeat", new FmodBeatClock.BeatSnapshot(n, 1 + n / 4, 1 + n % 4, n * 500, 120, 4, 4));
                void Resolve()
                {
                    if (fight.HasPendingEnemyAttack) Call(fight, "ResolvePendingEnemyAttack", Field<int>(fight, "pendingAttackRosterVersion"),
                        Field<long>(fight, "pendingEnemyActionId"), Field<FightUnitSlot>(fight, "pendingEnemyAttacker"));
                }
                void Reset() { fight.RosterManager.SpawnConfiguredRoster(); Call(fight, "RebuildRosterAndResetCombat"); Beat(0); }
                void StartSkill()
                {
                    typeof(FightCombatController).GetField("teamSkillGauge", Flags).SetValue(fight, fight.TeamSkillGaugeMax);
                    Check(fight.TryStartTeamSkill(), "Team Skill must activate.");
                }
                Reset(); for (int n = 1; n <= 4; n++) Beat(n);
                Check(fight.HasPendingEnemyAttack, "An attack is pending before freeze.");
                var animator = fight.ActiveEnemySlot.CombatAnimator;
                Call(animator, "AdvancePlayback", .08f);
                int frame = animator.ActiveFrame; var sprite = animator.TargetRenderer.sprite;
                float remainder = Field<float>(animator, "frameElapsed");
                int effectEvents = animator.AttackEffectEventCount, damageEvents = animator.DamageEventCount;
                float hp = fight.PartyHp, armor = fight.PartyArmor;
                StartSkill();
                for (int n = 5; n <= 16; n++)
                {
                    Beat(n); Resolve(); Call(animator, "AdvancePlayback", 2f);
                    Check(fight.EnemyActionsPaused && animator.PlaybackPaused, "Freeze covers all three full performances.");
                    Check(animator.ActiveFrame == frame && animator.TargetRenderer.sprite == sprite && Field<float>(animator, "frameElapsed") == remainder, "Exact frame and fractional progress remain held despite hits.");
                    Check(fight.PartyHp == hp && fight.PartyArmor == armor && fight.HasPendingEnemyAttack, "Enemy damage remains suspended.");
                    Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Attack && fight.BossBeatsRemaining == 1, "Boss countdown is frozen.");
                    Check(animator.AttackEffectEventCount == effectEvents && animator.DamageEventCount == damageEvents, "No animation callback may fire while frozen.");
                }
                Beat(17); Check(!fight.EnemyActionsPaused && !animator.PlaybackPaused && animator.IsPlaying && animator.ActiveFrame == frame, "Resume retains the exact old attack.");
                Call(animator, "AdvancePlayback", .1f); Check(animator.ActiveFrame > frame, "Resume advances the existing sequence."); Resolve();
                Check(!fight.HasPendingEnemyAttack, "Deferred attack resolves once.");

                Reset(); for (int n = 1; n <= 4; n++) Beat(n); animator = fight.ActiveEnemySlot.CombatAnimator;
                StartSkill(); sprite = animator.TargetRenderer.sprite;
                effectEvents = animator.AttackEffectEventCount; damageEvents = animator.DamageEventCount;
                fight.InterruptEnemyAttack(fight.ActiveEnemySlot);
                Check(!animator.IsPlaying && animator.PlaybackPaused && animator.TargetRenderer.sprite == sprite && !fight.HasPendingEnemyAttack, "Interruption discards attack but preserves held sprite.");
                for (int n = 5; n <= 17; n++) Beat(n);
                Call(animator, "AdvancePlayback", 10f);
                Check(!animator.IsPlaying && !fight.HasPendingEnemyAttack && animator.AttackEffectEventCount == effectEvents && animator.DamageEventCount == damageEvents, "Interrupted attack and callbacks cannot revive.");

                Reset(); for (int n = 1; n <= 9; n++) { Beat(n); Resolve(); }
                Call(fight, "ApplyBossBreak", 1, 9L); StartSkill();
                for (int n = 10; n <= 21; n++) Beat(n);
                Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Break && fight.BossBeatsRemaining == 4 && fight.EnemyActionsPaused, "Mage Team Skill can break a frozen charge; Break timer stays frozen.");
                Beat(22); Check(!fight.EnemyActionsPaused && fight.BossBeatsRemaining == 4 && !fight.HasPendingEnemyAttack, "Break starts counting only after Team Skill.");
                for (int n = 23; n <= 25; n++) Beat(n);
                Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Break, "No lost Break beats.");
                Beat(26); Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Neutral, "Full Break then Neutral.");

                Reset(); fight.ActiveEnemySlot.CharacterDefinition.BossPattern.enabled = false;
                typeof(FightUnitSlot).GetField("attackIntervalBeats", Flags).SetValue(fight.ActiveEnemySlot, 4);
                for (int n = 1; n <= 3; n++) Beat(n);
                Check(fight.HasPendingEnemyAttack, "Regular enemy schedules normally.");
                StartSkill(); fight.InterruptEnemyAttack(fight.ActiveEnemySlot);
                for (int n = 4; n <= 19; n++) { Beat(n); Check(!fight.HasPendingEnemyAttack, "Interrupted ordinary attack must not be requeued on the resume beat."); }
                Beat(20); Check(fight.HasPendingEnemyAttack, "Regular enemy resumes on the next scheduled attack, without catch-up.");

                StartSkill(); fight.SetTimingCalibrationActive(true);
                Check(!fight.EnemyActionsPaused && fight.RosterManager.ActiveEnemies.All(e => !e.CombatAnimator.PlaybackPaused), "Calibration releases all animators.");
                fight.SetTimingCalibrationActive(false); Reset(); StartSkill(); fight.RosterManager.SpawnConfiguredRoster(); Call(fight, "RebuildRosterAndResetCombat");
                Check(!fight.EnemyActionsPaused && fight.RosterManager.ActiveEnemies.All(e => !e.CombatAnimator.PlaybackPaused), "Roster reset cannot leak pause state.");
                Reset(); animator = fight.ActiveEnemySlot.CombatAnimator;
                Call(animator, "AdvancePlayback", 0f); sprite = animator.TargetRenderer.sprite;
                animator.SetPlaybackPaused(true);
                Call(animator, "OnBeat", new FmodBeatClock.BeatSnapshot(8, 3, 1, 4000, 120, 4, 4));
                animator.SetPlaybackPaused(false); Call(animator, "AdvancePlayback", 0f);
                Check(animator.TargetRenderer.sprite == sprite && Field<double>(animator, "idlePausedBeats") >= 7.99,
                    "Idle also retains its phase instead of jumping over paused beats.");
                File.WriteAllText("Temp/FightEnemyPauseValidation.result", "PASS: exact frame and fractional progress, 12-beat hold, frozen damage/state/callbacks, resume once, interruption without callback flush, Mage Break during freeze, full Break after resume, calibration/roster cleanup.");
            }
            catch (Exception e) { File.WriteAllText("Temp/FightEnemyPauseValidation.result", "FAIL: " + e); Debug.LogException(e); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
