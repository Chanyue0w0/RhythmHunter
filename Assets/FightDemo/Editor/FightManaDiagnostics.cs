using System.IO;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using RhythmHunter.RhythmDemo;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightManaDiagnostics
    {
        static double next;
        static FightManaDiagnostics() => EditorApplication.update += Snapshot;
        [MenuItem("Rhythm Hunter/Diagnostics/Start MP Capture")]
        static void StartCapture() => File.WriteAllText("Temp/ManaDiagnostics.capture", "capture");
        [MenuItem("Rhythm Hunter/Diagnostics/Stop MP Capture")]
        static void StopCapture() { if (File.Exists("Temp/ManaDiagnostics.capture")) File.Delete("Temp/ManaDiagnostics.capture"); }
        static object Read(object o, string n) => o?.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(o);
        static void Snapshot()
        {
            if (File.Exists("Temp/ManaValidation.request") && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                File.Delete("Temp/ManaValidation.request");
                try { Validate(); } catch (Exception e) { File.WriteAllText("Temp/ManaValidation.result", "FAIL: " + e); Debug.LogException(e); }
            }
            if (!File.Exists("Temp/ManaDiagnostics.capture") || EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling) return;
            next = EditorApplication.timeSinceStartup + 2;
            var log = new StringBuilder("Playing=" + EditorApplication.isPlaying + "\n");
            foreach (var fight in UnityEngine.Object.FindObjectsByType<FightCombatController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                log.AppendLine($"Scene={fight.gameObject.scene.name} active={fight.isActiveAndEnabled} MP={fight.TeamSkillGaugeExact}/{fight.TeamSkillGaugeMax} equal={fight.UsesEqualBeats} skill={fight.TeamSkillRunning} ultimate={fight.TeamUltimateRunning}");
                foreach (var n in new[] { "teamSkillGaugeGain", "lastGaugeBeat", "lastTeamSkillBeat", "lastTeamUltimateBeat", "latestCombatBeat", "pressureManaMultiplier" }) log.AppendLine(n + "=" + Read(fight, n));
            }
            foreach (var presenter in UnityEngine.Object.FindObjectsByType<FightScenePresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var fill = Read(presenter, "stoneFillImage") as Image;
                log.AppendLine($"Presenter={presenter.gameObject.scene.name} active={presenter.isActiveAndEnabled} fill={fill?.fillAmount} enabled={fill?.enabled} active={fill?.gameObject.activeInHierarchy} rect={fill?.rectTransform.rect} color={fill?.color} sprite={fill?.sprite?.name}");
            }
            File.WriteAllText("Temp/ManaDiagnostics.txt", log.ToString());
            if (EditorApplication.isPlaying) File.WriteAllText("Temp/ManaLastPlay.txt", log.ToString());
        }

        static void Validate()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            void Set(object o, string n, object v) => o.GetType().GetField(n, flags).SetValue(o, v);
            object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, flags).Invoke(o, args);
            void Check(bool value, string message) { if (!value) throw new Exception(message); }
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var fight = components.OfType<FightCombatController>().Single();
                var presenter = components.OfType<FightScenePresenter>().Single();
                foreach (var audio in components.OfType<FightAudioPlayer>()) Set(audio, "soundEnabled", false);
                Set(fight, "enableSuccessfulInputSound", false);
                fight.RosterManager.SpawnConfiguredRoster();
                Call(fight, "RebuildRosterAndResetCombat");
                Set(fight, "beatClock", null); Set(fight, "waveEntrance", null);
                Check(fight.TryBeginBattle(), "Cannot begin battle");
                Check(fight.TeamSkillGaugeExact == 0 && fight.TeamSkillGaugeMax == 90, "Expected scene MP 0/90");
                Call(presenter, "BuildEqualBeatTimeline");
                var fill = (Image)Read(presenter, "stoneFillImage");
                long beat = 100;
                float expected = 0;
                var report = new StringBuilder();
                foreach (var command in new[] { FightInputRouter.HeroCommand.Front, FightInputRouter.HeroCommand.Middle, FightInputRouter.HeroCommand.Back })
                {
                    var result = new FmodRhythmJudge.Result(FmodRhythmJudge.Grade.Perfect, 0, 1000, 1000,
                        new FmodBeatClock.NearestBeat(beat++, 26, 1, 1000, 0), false, "Validation");
                    Call(fight, "ApplyHeroJudgement", fight.GetHeroForCommand(command), command, result);
                    expected++;
                    Check(fight.TeamSkillGaugeExact == expected, command + " did not gain MP from successful call");
                    Call(presenter, "UpdateTimelineArtwork");
                    Check(fill.enabled && fill.fillAmount > 48f / 360f, command + " fill still hidden in transparent padding");
                    report.AppendLine(command + ": MP=" + fight.TeamSkillGaugeExact + "/90, image fill=" + fill.fillAmount);
                }
                Set(fight, "pressureRemaining", 20d);
                var pressureResult = new FmodRhythmJudge.Result(FmodRhythmJudge.Grade.Perfect, 0, 1000, 1000,
                    new FmodBeatClock.NearestBeat(beat++, 26, 1, 1000, 0), false, "Pressure validation");
                Call(fight, "ApplyHeroJudgement", fight.GetHeroForCommand(FightInputRouter.HeroCommand.Middle), FightInputRouter.HeroCommand.Middle, pressureResult);
                Check(fight.TeamSkillGaugeExact == 5, "Pressure call did not gain 2 MP");
                Call(presenter, "RenderStoneArtwork", 0f, 0d, false);
                Check(!fill.enabled && fill.fillAmount == 0, "Zero MP should not show a filled pixel");
                File.WriteAllText("Temp/ManaValidation.result", "PASS: real hero judgement/ability path grants 1 MP per hero and 2 under Pressure; first MP passes visible image bottom; zero MP hidden.\n" + report);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
