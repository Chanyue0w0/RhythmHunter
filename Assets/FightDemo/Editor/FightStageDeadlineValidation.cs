using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightStageDeadlineValidation
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static FightStageDeadlineValidation() => EditorApplication.update += Poll;
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Poll()
        {
            const string request = "Temp/StageDeadlineValidation.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(request); } catch (IOException) { return; }
            try { Validate(); File.WriteAllText("Temp/StageDeadlineValidation.result", "PASS: deadline boundary, current-Wave-only overtime, death presentation wait, no next Wave, treasure/gap expiry, Fever deferral, defeat priority, reset and partial result statistics; final-combat-only policy rejects early combat/gaps/Fever and ignores trailing treasure."); }
            catch (Exception e) { File.WriteAllText("Temp/StageDeadlineValidation.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Diagnostics/Validate Stage Deadline")]
        public static void Validate()
        {
            foreach (string scenario in new[] { "combat", "gap", "treasure", "fever", "defeat", "early", "restricted_early", "restricted_gap", "restricted_last" })
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                FightStageDefinition stage = null;
                try
                {
                    var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                    var fight = all.OfType<FightCombatController>().Single();
                    var results = all.OfType<FightStageResults>().Single();
                    var presenter = all.OfType<FightScenePresenter>().Single();
                    // Preview scenes do not run the clock's Awake metadata resolution.
                    Set(results.BeatClock, "openingTempo", 120f);
                    foreach (var sound in all.OfType<FightAudioPlayer>()) Set(sound, "soundEnabled", false);
                    stage = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<FightStageDefinition>("Assets/FightDemo/Levels/Stage01.asset"));
                    Set(stage, "useNormalEndDeadline", true);
                    Set(stage, "overtimeOnlyOnFinalCombatWave", scenario.StartsWith("restricted_"));
                    if (scenario == "restricted_last")
                    {
                        var waves = (System.Collections.Generic.List<FightStageDefinition.Wave>)typeof(FightStageDefinition).GetField("waves", Flags).GetValue(stage);
                        waves.Add(new FightStageDefinition.Wave { kind = FightStageDefinition.WaveKind.Treasure, front = stage.Waves[0].front });
                    }
                    if (scenario == "treasure") stage.Waves[0].kind = FightStageDefinition.WaveKind.Treasure;
                    Check(fight.RosterManager.TryConfigureStage(stage, out string error), error);
                    fight.RosterManager.SpawnConfiguredRoster();
                    Call(fight, "RebuildRosterAndResetCombat");
                    Set(fight, "beatClock", null); Set(fight, "waveEntrance", null);
                    Check(fight.TryBeginBattle(), "Battle did not start");
                    Set(fight, "stageDeadlineMs", 63471);
                    if (scenario == "combat")
                    {
                        Call(fight, "EvaluateStageDeadline", 0);
                        Call(presenter, "UpdateStageCountdownLine");
                        var line = (RectTransform)typeof(FightScenePresenter).GetField("stageCountdownRoot", Flags).GetValue(presenter);
                        Check(line != null && line.gameObject.activeSelf && line.parent.name == "PlayerUI", "Countdown missing from player UI");
                        var fill = line.Find("RemainingTime").GetComponent<UnityEngine.UI.Image>();
                        Check(fill.rectTransform.anchorMin.x == 0 && fill.rectTransform.anchorMax.x == 1 && !fill.raycastTarget, "Full countdown geometry/input blocking wrong");
                        Call(fight, "EvaluateStageDeadline", 31735);
                        foreach (float width in new[] { 1280f, 1920f, 2560f })
                        {
                            ((RectTransform)line.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                            Call(presenter, "UpdateStageCountdownLine");
                            Check(Mathf.Abs(line.rect.width - width) < .1f && Mathf.Abs(fill.rectTransform.rect.width - width * .5f) < .1f, "Countdown did not stretch/shrink symmetrically");
                        }
                        Set(presenter, "showStageCountdownLine", false); Call(presenter, "UpdateStageCountdownLine");
                        Check(!line.gameObject.activeSelf, "Countdown toggle failed");
                        Set(presenter, "showStageCountdownLine", true); Call(presenter, "UpdateStageCountdownLine");
                        Check(line.gameObject.activeSelf && line.childCount == 1, "Countdown toggle recreated children");
                    }
                    int clears = 0; fight.WaveCleared += _ => clears++;
                    Call(fight, "EvaluateStageDeadline", 63470);
                    Check(!fight.StageTimeExpired && !fight.BattleEnded, scenario + " expired early");
                    if (scenario == "early")
                    {
                        Call(fight, "FinishBattle", true);
                        Check(fight.BattleWon && !fight.StageTimeExpired, "Early victory failed");
                        continue;
                    }
                    if (scenario == "gap") Set(fight, "<AwaitingNextWave>k__BackingField", true);
                    if (scenario == "restricted_gap") Set(fight, "<AwaitingNextWave>k__BackingField", true);
                    if (scenario == "restricted_early") Set(fight, "nextTeamSkillBeat", 999L);
                    if (scenario == "restricted_last") Set(fight, "<CurrentWave>k__BackingField", 2);
                    if (scenario == "fever") Set(fight, "nextTeamSkillBeat", 999L);
                    if (scenario == "defeat") { Set(fight, "endBattleOnZeroHp", true); Set(fight, "partyHp", 0f); }
                    Call(fight, "EvaluateStageDeadline", 63471);
                    Check(fight.StageTimeExpired, "Did not latch deadline");
                    if (scenario.StartsWith("restricted_"))
                    {
                        if (scenario == "restricted_last")
                            Check(!fight.BattleEnded && fight.StageOvertimeActive && fight.RosterManager.LastCombatWaveIndex == 1, "Trailing treasure incorrectly prevented last-combat overtime");
                        else
                            Check(fight.BattleEnded && !fight.BattleWon && !fight.StageOvertimeActive, "Earlier combat/gap must immediately defeat, including active Fever");
                        continue;
                    }
                    if (scenario == "gap" || scenario == "treasure")
                        Check(fight.BattleEnded && fight.BattleWon && fight.CurrentWave == 1, scenario + " did not settle immediately");
                    else if (scenario == "defeat") Check(fight.BattleEnded && !fight.BattleWon, "Zero HP must defeat");
                    else
                    {
                        if (scenario == "fever")
                        {
                            Check(!fight.StageOvertimeActive && !fight.BattleEnded, "Interrupted running Fever");
                            Set(fight, "nextTeamSkillBeat", long.MaxValue);
                            Call(fight, "EvaluateStageDeadline", 64000);
                        }
                        Check(fight.StageOvertimeActive && !fight.BattleEnded && fight.TeamSkillActivationBlocked, "Combat overtime not active/locked");
                        if (scenario == "combat")
                        {
                            Call(presenter, "UpdateStageCountdownLine");
                            var fill = (UnityEngine.UI.Image)typeof(FightScenePresenter).GetField("stageCountdownFill", Flags).GetValue(presenter);
                            Check(!fill.enabled && fill.rectTransform.anchorMin.x == .5f && fill.rectTransform.anchorMax.x == .5f, "Overtime countdown refilled");
                        }
                        foreach (var enemy in fight.RosterManager.ActiveEnemies) enemy.TakeDamage(10000);
                        Call(fight, "EvaluateStageDeadline", 65000);
                        Check(!fight.BattleEnded, "Skipped death presentation");
                        foreach (var enemy in fight.RosterManager.ActiveEnemies)
                        {
                            if (enemy.CombatAnimator != null) Call(enemy.CombatAnimator, "AdvancePlayback", 2f);
                            Call(enemy, "LateUpdate");
                        }
                        Call(fight, "EvaluateStageDeadline", 66000);
                        Check(fight.BattleEnded && fight.BattleWon && fight.CurrentWave == 1 && clears == 1, "Overtime spawned next Wave or missed clear");
                        Call(presenter, "UpdateStageCountdownLine");
                        if (scenario == "combat")
                        {
                            var line = (RectTransform)typeof(FightScenePresenter).GetField("stageCountdownRoot", Flags).GetValue(presenter);
                            Check(!line.gameObject.activeSelf, "Countdown remained visible over results");
                        }
                        Call(results, "Finish", true);
                        Check(results.ClearedWaves < results.TotalWaves, "Partial victory reported all Waves cleared");
                    }
                    if (scenario == "defeat")
                    {
                        Call(presenter, "BuildEqualBeatTimeline");
                        Set(presenter, "defeatUiStarted", false);
                        Call(presenter, "BeginDefeatUi");
                        var stone = (UnityEngine.UI.Image)typeof(FightScenePresenter).GetField("timelineCenter", Flags).GetValue(presenter);
                        var points = (UnityEngine.UI.Image[])typeof(FightScenePresenter).GetField("approachingPoints", Flags).GetValue(presenter);
                        var hud = (Canvas)typeof(FightScenePresenter).GetField("playerUiCanvas", Flags).GetValue(presenter);
                        float startY = stone.rectTransform.anchoredPosition.y;
                        Call(presenter, "AdvanceDefeatUi", .1f);
                        Check(stone.rectTransform.anchoredPosition.y > startY && hud.gameObject.activeSelf, "Stone did not jump or HUD disappeared early");
                        Call(presenter, "AdvanceDefeatUi", .7f);
                        Check(stone.rectTransform.anchoredPosition.y < startY && points.All(p => p.color.a == 0) && hud.gameObject.activeSelf, "Stone fall / beat fade / retained HUD failed");
                        Call(results, "Finish", false);
                        var panel = all.OfType<FightVictoryPanel>().Single();
                        Call(panel, "Show");
                        var group = (CanvasGroup)typeof(FightVictoryPanel).GetField("presentationGroup", Flags).GetValue(panel);
                        float delay = (float)typeof(FightVictoryPanel).GetField("presentationDelay", Flags).GetValue(panel);
                        Check(delay > 0 && group.alpha == 0 && !group.interactable && fight.BattleEnded, "Defeat must lock combat before displaying results");
                        Call(panel, "AdvancePresentation", delay * .5f);
                        Check(group.alpha == 0 && !group.interactable, "Defeat appeared before two beats");
                        Call(panel, "AdvancePresentation", delay * .5f);
                        Call(panel, "AdvancePresentation", .175f);
                        Check(group.alpha > 0 && group.alpha < 1 && !group.interactable, "Defeat fade or input lock failed");
                        Call(panel, "AdvancePresentation", .2f);
                        Check(group.alpha == 1 && group.interactable, "Defeat fade did not finish");
                        Check(!hud.gameObject.activeSelf, "HUD remained after defeat panel finished appearing");
                    }
                    Call(fight, "ResetStageDeadline");
                    Check(!fight.StageTimeExpired && !fight.StageOvertimeActive && fight.StageMusicSeconds == 0, "Timer reset failed");
                }
                finally { if (stage != null) UnityEngine.Object.DestroyImmediate(stage); EditorSceneManager.ClosePreviewScene(scene); }
            }
        }
    }
}
