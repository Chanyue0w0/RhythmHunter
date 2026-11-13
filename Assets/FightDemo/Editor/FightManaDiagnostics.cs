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
            if (File.Exists("Temp/CountdownPlay.request") && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                File.Delete("Temp/CountdownPlay.request");
                EditorApplication.isPlaying = true;
            }
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
                log.AppendLine($"Deadline enabled={fight.StageDeadlineEnabled} awaiting={fight.AwaitingBattleStart} ended={fight.BattleEnded} limit={fight.StageTimeLimitSeconds} remaining={fight.StageSecondsRemaining} expired={fight.StageTimeExpired}");
                foreach (var n in new[] { "teamSkillGaugeGain", "lastGaugeBeat", "lastTeamSkillBeat", "lastTeamUltimateBeat", "latestCombatBeat", "pressureManaMultiplier" }) log.AppendLine(n + "=" + Read(fight, n));
            }
            foreach (var presenter in UnityEngine.Object.FindObjectsByType<FightScenePresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var fill = Read(presenter, "stoneFillImage") as Image;
                log.AppendLine($"Countdown configured={Read(presenter, "showStageCountdownLine")} screen={Screen.width}x{Screen.height}");
                var line = Read(presenter, "stageCountdownRoot") as RectTransform;
                log.AppendLine("Countdown root=" + (line == null ? "MISSING" : line.rect.ToString()));
                for (Transform t = line; t != null; t = t.parent)
                {
                    log.AppendLine($"Ancestor {t.name} active={t.gameObject.activeInHierarchy} scale={t.lossyScale} rect={(t as RectTransform)?.rect}");
                    var canvas = t.GetComponent<Canvas>();
                    if (canvas != null) log.AppendLine($"Canvas enabled={canvas.enabled} mode={canvas.renderMode} order={canvas.sortingOrder} scale={canvas.scaleFactor}");
                }
                var countdownFill = Read(presenter, "stageCountdownFill") as Image;
                if (countdownFill != null) log.AppendLine($"Countdown fill enabled={countdownFill.enabled} color={countdownFill.color} rect={countdownFill.rectTransform.rect} culled={countdownFill.canvasRenderer.cull}");
                if (line != null)
                {
                    var corners = new Vector3[4]; line.GetWorldCorners(corners);
                    log.AppendLine("Countdown corners=" + string.Join(",", corners.Select(c => c.ToString())));
                    if (EditorApplication.isPlaying && File.Exists("Temp/CountdownScreenshot.request"))
                    {
                        File.Delete("Temp/CountdownScreenshot.request");
                        ScreenCapture.CaptureScreenshot("Temp/CountdownGame.png");
                    }
                }
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
                Set(fight, "suspendFeverActivation", true); // Explicit fixture; scene may enable Fever.
                Call(presenter, "BuildEqualBeatTimeline");
                var fill = (Image)Read(presenter, "stoneFillImage");
                var chain = (Image)Read(presenter, "stoneLockImage");
                Check(chain != null && chain.sprite != null && chain.enabled, "Suspended Fever chain missing");
                Check(chain.transform.parent == fill.transform.parent && chain.transform.GetSiblingIndex() > fill.transform.GetSiblingIndex(), "Chain must follow stone above fill");
                Set(fight, "suspendFeverActivation", false);
                Set(fight, "latestCombatBeat", 10L);
                Call(presenter, "UpdateTimelineArtwork");
                Check(!chain.enabled && !fight.TryStartTeamSkill(), "Empty MP alone must not show chain or allow Fever");
                Set(fight, "pressureRemaining", 20d);
                Set(fight, "teamSkillGauge", fight.TeamSkillGaugeMax);
                Call(presenter, "UpdateTimelineArtwork");
                Check(chain.enabled && !fight.TryStartTeamSkill(), "Pressure must lock full MP Fever");
                Set(fight, "pressureRemaining", 0d);
                Call(presenter, "UpdateTimelineArtwork");
                Check(!chain.enabled, "Chain did not clear when Pressure ended");
                Set(fight, "teamSkillGauge", 0);
                Set(fight, "suspendFeverActivation", true);
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
                File.WriteAllText("Temp/ManaValidation.result", "PASS: Fever chain follows stone above fill, locks suspended/Pressure with full MP, hides for insufficient MP alone and on Pressure exit; real hero judgement grants 1 MP and 2 under Pressure; first MP visible; zero MP hidden.\n" + report);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
