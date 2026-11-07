using System;
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
    public static class FightTimelineArtValidation
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Get(object obj, string name) => obj.GetType().GetField(name, Flags).GetValue(obj);
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
        static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
        static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        static FightTimelineArtValidation() => EditorApplication.update += Requested;
        static void Requested()
        {
            const string file = "Temp/TimelineArt.request";
            if (!File.Exists(file) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(file);
            try { Run(); File.WriteAllText("Temp/TimelineArt.result", "PASS: timeline geometry/count, fill, five frames, shake/reset, miss, subscriptions."); }
            catch (Exception ex) { File.WriteAllText("Temp/TimelineArt.result", "FAIL: " + ex); Debug.LogException(ex); }
        }
        [MenuItem("Rhythm Hunter/Validate Timeline Artwork")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            try
            {
                var presenter = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightScenePresenter>(true)).Single();
                var fight = (FightCombatController)Get(presenter, "fight");
                Set(fight.RosterManager, "requireBattlePreparation", false);
                fight.RosterManager.SpawnConfiguredRoster();
                Call(fight, "RebuildRosterAndResetCombat");
                Call(presenter, "BuildEqualBeatTimeline");
                Set(presenter, "showDeveloperUi", false);
                Call(presenter, "ApplyVisibilityControls");
                var playerRoot = (RectTransform)Get(presenter, "playerUiRoot");
                var developerRoot = (RectTransform)Get(presenter, "developerUiRoot");
                var timeline = (RectTransform)Get(presenter, "timelineRoot");
                Check(playerRoot.gameObject.activeInHierarchy && timeline.gameObject.activeInHierarchy &&
                    !developerRoot.gameObject.activeInHierarchy, "Developer UI must not hide the player timeline.");
                Check(timeline.parent == playerRoot, "Player timeline must not inherit legacy debug hierarchy.");
                var points = (Image[])Get(presenter, "approachingPoints");
                var bar = (Sprite)Get(presenter, "timelineBarSprite");
                int preview = Mathf.Clamp((int)Get(presenter, "previewBeats"), 1, 8);
                Check(points.Length == preview * 4, "Original point pool count.");
                Check(preview == 5, "Five anchors per side.");
                for (int i = 0; i < 5; i++)
                {
                    float start = (float)Call(presenter, "TimelinePointDistance", i, 0f, 5, false);
                    float hold = (float)Call(presenter, "TimelinePointDistance", i, .7f, 5, false);
                    float end = (float)Call(presenter, "TimelinePointDistance", i, 1f, 5, false);
                    float next = i == 0 ? 0 : (float)Call(presenter, "TimelinePointDistance", i - 1, 0f, 5, false);
                    Check(Mathf.Abs(hold - Mathf.Lerp(start, end, .7f)) < .001f && Mathf.Abs(end - next) < .001f, "Constant-speed movement and continuous beat boundaries.");
                    float burst = (float)Call(presenter, "TimelinePointDistance", i * 2 + 1, 0f, 10, true);
                    Check(Mathf.Abs(start - burst) < .001f, "Half-beat points preserve whole-beat anchors.");
                }
                var fill = (Image)Get(presenter, "stoneFillImage");
                long baseTick = ((FmodBeatClock)Get(presenter, "beatClock")).LatestBeat.GlobalBeat * 2;
                Call(presenter, "ResetConsumedTimelinePoints");
                Call(presenter, "RenderEqualBeatTimeline", 250d, 0d, 500d);
                Call(presenter, "ConsumeTimelinePoint", baseTick + 2);
                Check(!points[0].enabled && !points[1].enabled && points[2].enabled, "Early success hides only its pair.");
                Call(presenter, "RenderEqualBeatTimeline", 300d, 0d, 500d);
                Check(!points[0].enabled && !points[1].enabled, "Consumed points must not reappear next frame.");
                Call(presenter, "ConsumeTimelinePoint", baseTick + 2);
                var consumed = (System.Collections.Generic.HashSet<long>)Get(presenter, "consumedTimelineTicks");
                Check(consumed.Count == 1, "Duplicate success must not retrigger feedback.");
                Call(presenter, "RenderEqualBeatTimeline", 510d, 0d, 500d);
                Check(points[0].enabled && points[1].enabled, "Late input must not consume the next beat.");
                Call(presenter, "OnTeamSkillPointAccepted", baseTick / 2d + .5);
                Check(consumed.Contains(baseTick + 1), "Half-beat success retains its exact half-beat identity.");
                Call(presenter, "ResetConsumedTimelinePoints");
                Check(consumed.Count == 0, "New battle/roster resets consumed points.");
                var center = (Image)Get(presenter, "timelineCenter");
                Vector2 originalSize = (Vector2)Get(presenter, "stoneArtSize");
                Set(presenter, "stoneArtSize", new Vector2(84, 108));
                Set(presenter, "timelineBarHeight", 60f);
                Call(presenter, "UpdateTimelineArtwork");
                Check(center.rectTransform.sizeDelta == new Vector2(84, 108) &&
                    ((Image)Get(presenter, "timelineTrack")).rectTransform.sizeDelta.y == 60, "Live artwork sizing.");
                Set(presenter, "stoneArtSize", originalSize);
                Set(presenter, "timelineBarHeight", 0f);
                Set(fight, "teamSkillGauge", fight.TeamSkillGaugeMax / 2);
                Call(presenter, "UpdateTimelineArtwork");
                Check(Mathf.Abs(fill.fillAmount - .5f) < .001f && fill.fillOrigin == (int)Image.OriginVertical.Bottom, "Shared MP fills bottom to top.");
                var frames = (Sprite[])Get(presenter, "stoneReadyFrames");
                Check(frames.Length == 5 && frames.All(s => s != null), "Five ready sprites assigned.");
                for (int i = 0; i < 5; i++)
                {
                    Call(presenter, "RenderStoneArtwork", 1f, i * .2 + .01, true);
                    Check(center.sprite == frames[i] && !fill.enabled, "Ready animation frame order.");
                }
                Call(presenter, "RenderStoneArtwork", 0f, 1d, true);
                Check(fill.enabled && fill.fillAmount == 0 && center.sprite == (Sprite)Get(presenter, "stoneEmptySprite"), "Consumption returns to empty stone.");
                Call(presenter, "RenderStoneBounce", 0d);
                Check(center.rectTransform.localScale.x > 1 && center.rectTransform.anchoredPosition == Vector2.zero, "Successful input expands without positional drift.");
                Call(presenter, "RenderStoneBounce", .75d);
                Check(center.rectTransform.localScale.x < 1, "Bounce contracts slightly before settling.");
                Call(presenter, "RenderStoneBounce", 1d);
                Check(center.rectTransform.localScale == Vector3.one && center.rectTransform.anchoredPosition == Vector2.zero, "Bounce restores size and origin.");
                Check(Vector3.Distance(timeline.localScale, Vector3.one * 1.9f) < .001f && timeline.anchoredPosition.y == 150, "Art2 persists overall scale and bottom offset.");
                var miss = new FmodRhythmJudge.Result(FmodRhythmJudge.Grade.Miss, 300, 0, 0, default, false, "Miss");
                double beforeMiss = (double)Get(presenter, "stoneHitTimelineMs");
                Call(presenter, "OnHeroCalled", new FightCombatController.HeroCallResult(FightInputRouter.HeroCommand.Front, miss, false, false, "Miss"));
                Check((double)Get(presenter, "stoneHitTimelineMs") == beforeMiss, "Miss must not trigger feedback.");
                Call(presenter, "Subscribe"); Call(presenter, "Subscribe");
                var handlers = (Action)Get(fight, "TeamSkillBeatAccepted");
                Check(handlers.GetInvocationList().Count(h => ReferenceEquals(h.Target, presenter)) == 1, "No duplicate burst feedback subscription.");
                Call(presenter, "Unsubscribe");
                handlers = (Action)Get(fight, "TeamSkillBeatAccepted");
                Check(handlers == null || handlers.GetInvocationList().All(h => !ReferenceEquals(h.Target, presenter)), "Feedback unsubscribes.");
                var canvas = ((Text)Get(presenter, "healthText")).canvas;
                Call(presenter, "RenderEqualBeatTimeline", 250d, 0d, 500d);
                Call(presenter, "RenderStoneArtwork", .5f, 11d, true);
                typeof(FightScene3BeatValidation).GetMethod("CaptureHud", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { canvas, "Temp/TimelineArt-preview.png" });
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
