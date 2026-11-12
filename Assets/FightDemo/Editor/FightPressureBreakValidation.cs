using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightPressureBreakValidation
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
        static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
        static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, Flags).Invoke(o, a);
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        static FightPressureBreakValidation() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/PressureBreakValidation.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Run(); }
            catch (Exception e) { File.WriteAllText("Temp/PressureBreakValidation.result", "FAIL: " + e); Debug.LogException(e); }
        }

        [MenuItem("Rhythm Hunter/Levels/Validate Pressure Break and UI")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play first.");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            FightStageDefinition stage = null;
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var fight = components.OfType<FightCombatController>().Single();
                var presenter = components.OfType<FightScenePresenter>().Single();
                var roster = fight.RosterManager;
                stage = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<FightStageDefinition>("Assets/FightDemo/Levels/Stage01.asset"));
                var waves = (List<FightStageDefinition.Wave>)Get(stage, "waves");
                waves[0].pressure = new FightEnemyPressureSettings { enablePressurePhase = true };
                Check(roster.TryConfigureStage(stage, out var error), error);
                roster.SpawnConfiguredRoster();
                Call(fight, "RebuildRosterAndResetCombat");
                Set(fight, "beatClock", null); Set(fight, "waveEntrance", null);
                Set(fight, "startWithFullTeamSkill", false);
                Check(fight.TryBeginBattle(), "Begin test battle");
                Set(fight, "pressureDamagePerPair", 5f);
                Set(fight, "pressureBlocksPerPair", 5);
                Set(fight, "pressureReductionSegments", 20);
                Set(fight, "pressureGuardChipDamage", .5f);
                Set(fight, "refillManaAfterPressure", true);
                Set(fight, "blockFeverDuringPressure", true);
                Set(fight, "teamSkillGaugeGain", 1);
                float savedHp = fight.PartyHp, savedArmor = fight.PartyArmor;
                float Chip(float damage, bool blocked, float chip) => (float)Call(fight, "ResolveEnemyHitDamage", damage, blocked, chip);
                Check(Chip(2, false, .5f) == 2 && Chip(1, true, 0) == 0, "No bonus on unblocked hits; ordinary guard remains complete");
                Set(fight, "partyHp", 4f); Set(fight, "partyArmor", 3f);
                fight.ApplyPartyDamage(Chip(2, true, .5f), 0);
                Check(fight.PartyHp == 4 && fight.PartyArmor == 2.5f, "Pressure guard chip must hit armor first");
                Set(fight, "partyArmor", 0f);
                fight.ApplyPartyDamage(Chip(1, true, .5f), 0);
                Check(fight.PartyHp == 3.5f, "Pressure guard chip should reach HP only without armor");
                Set(fight, "partyHp", 4f); Set(fight, "partyArmor", 10f);
                Call(fight, "ApplyEnemyPartyDamage", .5f, true, -10L);
                Call(fight, "ApplyEnemyPartyDamage", .5f, true, -10L);
                Check(fight.PartyArmor == 9.5f, "Same-beat chip must apply once");
                Call(fight, "ApplyEnemyPartyDamage", 1f, false, -10L);
                Call(fight, "ApplyEnemyPartyDamage", .5f, true, -9L);
                Call(fight, "ApplyEnemyPartyDamage", .5f, true, -10L);
                Check(fight.PartyArmor == 8f, "Unblocked damage, next beat and late duplicate must remain correct");
                Call(fight, "ApplyEnemyPartyDamage", 0f, true, -8L);
                Call(fight, "ApplyEnemyPartyDamage", .5f, true, -8L);
                Check(fight.PartyArmor == 7.5f, "Zero damage must not consume allowance");
                Set(fight, "partyHp", savedHp); Set(fight, "partyArmor", savedArmor);
                Set(fight, "showEnemyDamageNumbers", false);
                var enemy = roster.ActiveEnemies[0];
                Set(enemy, "maxHp", 100f); enemy.RestoreFullHealth();
                long global = 0;
                void Beat()
                {
                    global++;
                    Set(fight, "latestCombatBeat", global);
                    Call(fight, "AdvanceNormalBattleBeat", new FmodBeatClock.BeatSnapshot(global, 1, 1, (int)global * 500, 120, 4, 4));
                }
                void Gain(long n, bool duplicate = false)
                {
                    var clock = components.OfType<FmodBeatClock>().First();
                    var judge = components.OfType<FmodRhythmJudge>().First();
                    Set(clock, "hasAnchor", true);
                    Set(clock, "latestBeat", new FmodBeatClock.BeatSnapshot(n, 1, 1, (int)n * 500, 120, 4, 4));
                    judge.Configure(clock, 120, 0);
                    if (!duplicate) judge.ResetDuplicateTracking();
                    var result = (FmodRhythmJudge.Result)Call(judge, "JudgeTimelinePosition", (int)n * 500);
                    Call(fight, "GainTeamSkill", result);
                }
                while (fight.WaveCombatBeatCount < 15) Beat();
                Check(fight.PressureWarningActive && !fight.PressurePhaseActive, "Five beat warning");
                enemy.TakeDamage(3); Call(fight, "RecordPressureBlock");
                Check((float)Get(fight, "pressureDamageProgress") == 0 && (int)Get(fight, "pressureBlockProgress") == 0, "Warning must not accumulate breaks");
                Gain(15); Check(fight.TeamSkillGaugeExact == .5f, "Ordinary input should grant .5 MP");
                Gain(15, true); Check(fight.TeamSkillGaugeExact == .5f, "Duplicate input granted MP");
                Set(presenter, "fight", fight); Set(presenter, "beatClock", null);
                Call(presenter, "EnsureUiGroups"); Call(presenter, "UpdatePressureBar");
                var root = (RectTransform)Get(presenter, "pressureBarRoot");
                Check(root == null || !root.gameObject.activeSelf, "Neither bar nor skull may appear five beats early");
                while (fight.WaveCombatBeatCount < 18) Beat();
                Call(presenter, "RenderPressureBar", 18.99d, true);
                Check(Get(presenter, "pressureBarRoot") == null || !((RectTransform)Get(presenter, "pressureBarRoot")).gameObject.activeSelf, "UI must stay hidden until final warning beat");
                Beat();
                Call(presenter, "RenderPressureBar", 19.05d, true);
                root = (RectTransform)Get(presenter, "pressureBarRoot");
                Check(root != null && root.name == "PlayerPressureDurationBar", "Player pressure bar missing");
                float semi = root.GetComponent<CanvasGroup>().alpha;
                Check(semi > 0 && semi < 1 && root.anchoredPosition == (Vector2)Get(presenter, "pressureBarOffset"), "Whole UI should quickly fade in without warning shake");
                Check(root.GetComponentsInChildren<Image>(true).Length == 6, "Expected track, trail, fill, two edges and icon");
                Call(presenter, "RenderPressureBar", 19.5d, true);
                Check(root.GetComponent<CanvasGroup>().alpha == 1, "Whole UI should finish quick reveal within final beat");
                var skull = (Image)Get(presenter, "pressureBarIcon");
                Check(skull.enabled && skull.rectTransform.sizeDelta == new Vector2(64, 64), "Scene skull must be twice the previous 32px size");
                Check(skull.rectTransform.pivot.y == 0 && skull.rectTransform.anchoredPosition.y == ((Vector2)Get(presenter, "pressureBarSize")).y * .5f, "Skull bottom must touch bar top without overlap");
                Beat();
                Check(fight.PressurePhaseRemainingBeats == 20 && fight.PressureVisiblePairs == 20 && fight.PressurePairBeats == 1, "Default twenty-segment pressure start");
                enemy.TakeDamage(4);
                Check(fight.PressureRemainingExact == 20, "Four damage must not reduce pressure");
                enemy.TakeDamage(1);
                Check(fight.PressureRemainingExact == 19, "Five damage must remove one beat");
                for (int i = 0; i < 4; i++) Call(fight, "RecordPressureBlock");
                Check(fight.PressureRemainingExact == 19, "Four blocks must not reduce pressure");
                Call(fight, "RecordPressureBlock");
                Check(fight.PressureRemainingExact == 18, "Five blocks must remove one beat");
                Set(fight, "pressureReductionSegments", 40);
                enemy.TakeDamage(5);
                Check(fight.PressureRemainingExact == 17.5f, "Configurable segments must support fractional reduction");
                // Retain the larger reduction fixture for trail, overkill and lifecycle regression below.
                Set(fight, "pressureReductionSegments", 5);
                Set(fight, "pressureDamagePerPair", 3f);
                Set(fight, "pressureBlocksPerPair", 3);
                Set(fight, "pressureRemaining", 20d);
                Set(fight, "<PressureBreakRevision>k__BackingField", 0);
                Set(fight, "<PressureRemovedBeats>k__BackingField", 0f);
                Call(presenter, "RenderPressureBar", 20.1d, true);
                Check(root.GetComponent<CanvasGroup>().alpha == 1 && root.anchoredPosition != (Vector2)Get(presenter, "pressureBarOffset"), "Active bar should be opaque and bounce");
                var fill = (Image)Get(presenter, "pressureFill");
                var trailImage = (Image)Get(presenter, "pressureTrail");
                float fullWidth = fill.rectTransform.sizeDelta.x;
                Call(presenter, "RenderPressureBar", 20.6d, true);
                Check(fill.rectTransform.sizeDelta.x < fullWidth && !trailImage.enabled, "Natural countdown must shrink smoothly without yellow trail");
                Gain(20); Check(fight.TeamSkillGaugeExact == 1.5f, "Pressure MP multiplier");
                Set(fight, "teamSkillGauge", fight.TeamSkillGaugeMax); Set(fight, "teamSkillGaugeFraction", 0f);
                Set(fight, "suspendFeverActivation", false);
                Set(fight, "teamUltimateCharge", fight.TeamUltimateRequiredCharges);
                Check(fight.FeverBlockedByPressure && !fight.TeamSkillReady && !fight.TryStartTeamSkill() && !fight.TryStartTeamUltimate(), "Pressure must block both Fever entry points even with full charge");
                Check(fight.TeamSkillGaugeExact == fight.TeamSkillGaugeMax, "Rejected activation consumed MP");
                Set(fight, "blockFeverDuringPressure", false);
                Check(fight.TeamSkillReady, "Pressure lock must be configurable");
                Set(fight, "blockFeverDuringPressure", true); Set(fight, "suspendFeverActivation", true);
                Set(fight, "teamSkillGauge", 1); Set(fight, "teamSkillGaugeFraction", .5f);
                Beat(); Beat();
                enemy.TakeDamage(2); Check(fight.PressurePhaseRemainingBeats == 18, "Partial damage reduced pressure");
                enemy.TakeDamage(1); Check(fight.PressurePhaseRemainingBeats == 14 && fight.PressureVisiblePairs == 4, "18 minus 4 must equal 14");
                Call(presenter, "RenderPressureBar", 22.5d, true);
                float beforeSlide = fill.rectTransform.sizeDelta.x;
                Call(presenter, "RenderPressureBar", 22.7d, true);
                var left = (Image)Get(presenter, "pressureLeftEdge");
                var right = (Image)Get(presenter, "pressureRightEdge");
                Check(fill.rectTransform.sizeDelta.x < beforeSlide && Mathf.Abs(left.rectTransform.anchoredPosition.x + right.rectTransform.anchoredPosition.x) < .001f, "Rapid reduction must move both edges symmetrically");
                Check(trailImage.enabled && Mathf.Abs(trailImage.rectTransform.sizeDelta.x - fill.rectTransform.sizeDelta.x - 72) < .01f, "Yellow trail must show exactly four of twenty beats (72px)");
                Call(presenter, "RenderPressureBar", 23.05d, true);
                Check(Mathf.Abs(root.anchoredPosition.x) > 1, "Player break must strongly shake on next beat");
                Call(fight, "RecordPressureBlock"); Call(fight, "RecordPressureBlock");
                Check(fight.PressurePhaseRemainingBeats == 14, "Only two blocks reduced pressure");
                Call(fight, "RecordPressureBlock"); Check(fight.PressurePhaseRemainingBeats == 10, "Three blocks should remove one pair");
                enemy.TakeDamage(7); Check(fight.PressurePhaseRemainingBeats == 2 && (float)Get(fight, "pressureDamageProgress") == 1, "Multi-threshold damage/remainder");
                Check(fight.ScheduleEnemyHits(enemy, new[] { 1, 1, 1 }, 1, true, true), "Schedule launched projectile");
                Check(fight.ScheduleEnemyHits(enemy, new[] { 2 }, 1), "Schedule melee");
                enemy.TakeDamage(2);
                Check(!fight.PressurePhaseActive && fight.PressureRestActive && fight.PressureNextStartBeat == 62, "Early end/rest/restart delay");
                Check(fight.TeamSkillGaugeExact == fight.TeamSkillGaugeMax && !fight.FeverBlockedByPressure, "Early clear must refill all MP and release Fever lock");
                Check(fight.ScheduledEnemyHitCount == 3, "End must retain projectiles and cancel melee");
                Call(presenter, "RenderPressureBar", 23.1d, true);
                Call(presenter, "RenderPressureBar", 23.4d, true);
                Check(root.gameObject.activeSelf && fill.rectTransform.sizeDelta.x == 0 && trailImage.enabled, "Final reduction must retain yellow trail after gameplay ends");
                Call(presenter, "RenderPressureBar", 25.2d, true);
                Check(root.gameObject.activeSelf && trailImage.color.a < 1, "Yellow trail should fade only after hold duration");
                Call(presenter, "RenderPressureBar", 25.7d, true);
                Check(!root.gameObject.activeSelf, "Completed final trail should hide bar");
                Check(!(bool)Call(fight, "EnemyCadenceReady", enemy, global), "Rest allowed new attack");
                Check(!fight.ScheduleEnemyHits(enemy, new[] { 1 }, 1), "Rest accepted scheduled attack");
                Gain(23); Check(fight.TeamSkillGaugeExact == fight.TeamSkillGaugeMax, "Reward MP must stay capped");
                float beforeArmor = fight.PartyArmor, beforeHp = fight.PartyHp;
                int beforeBlocks = fight.BlockedAttackCount;
                Set(fight, "playSuccessfulBlockSound", false);
                ((HashSet<long>)Get(fight, "guardedBeats")).Add(global + 1);
                Beat();
                Call(fight, "ResolveEnemyCombatWork", (int)global * 500 + 300);
                Check(fight.ScheduledEnemyHitCount == 0 && fight.PartyArmor + fight.PartyHp == beforeArmor + beforeHp - .5f, "Pressure projectile should retain .5 guard chip during rest");

                Check(fight.BlockedAttackCount == beforeBlocks + 3 && fight.PartyHp == beforeHp, "Guard chip still counts as a successful block and consumes armor first");
                while (fight.WaveCombatBeatCount < 41) Beat();
                Check(fight.PressureRestActive, "Rest ended before 20 beats");
                Beat(); Check(!fight.PressureRestActive && !fight.PressurePhaseActive, "Rest exit must begin separate wait");
                while (fight.WaveCombatBeatCount < 57) Beat();
                Check(fight.PressureWarningActive, "Repeat warning missing");
                while (fight.WaveCombatBeatCount < 62) Beat();
                Check(fight.PressurePhaseActive && fight.PressureRemainingExact == 20, "Repeat full duration");
                Set(fight, "nextTeamSkillBeat", 0L); Beat();
                Check(fight.PressureRemainingExact == 20, "Team Skill must freeze pressure");
                Set(fight, "nextTeamSkillBeat", long.MaxValue);
                Set(fight, "teamSkillGauge", 2); Set(fight, "teamSkillGaugeFraction", .5f);
                while (fight.WaveCombatBeatCount < 82) Beat();
                Check(fight.PressureRestActive && fight.PressureNextStartBeat == 122, "Natural end/repeat");
                Check(fight.TeamSkillGaugeExact == fight.TeamSkillGaugeMax, "Natural clear must refill MP including fractional remainder");
                Set(fight, "teamSkillGauge", 1);
                Call(fight, "EndPressurePhase");
                Check(fight.TeamSkillGaugeExact == 1, "Reward must not repeat for the same phase");
                Call(fight, "ResetWavePressure");
                Check(!fight.PressureRestActive && !fight.PressurePhaseActive && fight.PressureNextStartBeat == 20, "Wave reset retained state");
                while (fight.WaveCombatBeatCount < 19) Beat();
                Set(fight, "nextTeamSkillBeat", 0L);
                for (int i = 0; i < 8; i++) Beat();
                Check(fight.WaveCombatBeatCount == 19 && !fight.PressurePhaseActive, "Prior Fever must defer Pressure trigger");
                Set(fight, "nextTeamSkillBeat", long.MaxValue);
                Beat(); Check(fight.PressurePhaseActive, "Pressure should resume remaining countdown after Fever");
                Set(fight, "refillManaAfterPressure", false);
                Call(fight, "EndPressurePhase");
                Check(fight.TeamSkillGaugeExact == 1, "Reward toggle must disable refill");
                Set(fight, "refillManaAfterPressure", true); Set(fight, "pressureRewardPaid", false);
                Set(fight, "partyHp", 0f);
                Call(fight, "EndPressurePhase");
                Check(fight.TeamSkillGaugeExact == 1, "Defeated party must not earn survival reward");
                File.WriteAllText("Temp/PressureBreakValidation.result", "PASS: natural/early pressure reward, one-shot/toggle/death guards; Pressure Fever lock and prior-Fever deferral; whole UI one-beat quick warning; 64px skull anchored above bar; three same-beat projectiles only chip once but count three blocks; unblocked/next-beat/late callback/zero-damage cases; pressure guard chip, armor first, HP overflow, no unblocked bonus, normal block unchanged; five damage/five blocks; twenty default segments; configurable fractional reduction; warning fade/no shake; opaque beat bounce; continuous countdown; symmetric edges; rapid reduction; exact yellow trail amount; delayed fade and final-break cleanup; next-beat strong shake; .5/1 MP and duplicate rejection; 18->14; damage/block thresholds and remainder; early/natural end; projectile retained and dealt damage during rest; 20 beat rest + fresh start delay; TeamSkill freeze; wave reset.");
            }
            finally
            {
                if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
