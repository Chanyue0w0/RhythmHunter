using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemoEditor
{
    // Runs against an isolated scene copy; never enters Play Mode or saves the user's scene.
    [InitializeOnLoad]
    public static class FightScene3BeatValidation
    {
        private const string Request = "Temp/FightScene3BeatValidation.request";
        private const string Result = "Temp/FightScene3BeatValidation.result";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        static FightScene3BeatValidation()
        {
            EditorApplication.update += CheckRequest;
        }

        private static void CheckRequest()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;
            File.Delete(Request);
            Run();
        }

        [MenuItem("Rhythm Hunter/Validate FightScene3 Equal Beats")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/FightScene3.unity");
            bool passed = false;
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var fight = components.OfType<FightCombatController>().Single();
                var hud = components.OfType<FightScenePresenter>().Single();
                Require(fight.UsesEqualBeats && fight.UsesFrontHeroControls, "FightScene3 must use equal-beat party controls.");
                for (int beat = 1; beat <= 8; beat++)
                    Require(fight.GetHeroActionForBeat(beat) == FightCombatController.ActionType.LightAttack, "Every musical beat must select Basic.");
                fight.SetCombatMode(FightCombatController.CombatMode.FrontHero);
                Require(fight.GetHeroActionForBeat(4) == FightCombatController.ActionType.Skill, "Older scenes must retain fourth-beat skills.");
                fight.SetCombatMode(FightCombatController.CombatMode.EqualBeat);

                Invoke(hud, "BuildEqualBeatTimeline");
                var points = Field<Image[]>(hud, "approachingPoints");
                var center = Field<Image>(hud, "timelineCenter");
                var root = Field<RectTransform>(hud, "timelineRoot");
                Require(points.Length == 6 && root.anchorMin == new Vector2(0.5f, 0f), "Expected three pairs anchored at bottom center.");
                Require(!Field<Slider>(hud, "beatProgress").gameObject.activeInHierarchy, "Old four-beat HUD must be hidden.");
                Invoke(hud, "BuildEqualBeatTimeline");
                Require(Field<RectTransform>(hud, "timelineRoot") == root, "HUD must not be duplicated.");

                foreach (double interval in new[] { 1000.0, 500.0, 333.333 })
                {
                    // Sweep across multiple bars, including negative phase (early input offsets).
                    for (int beat = -1; beat < 9; beat++)
                    {
                        Invoke(hud, "RenderEqualBeatTimeline", 2000 + (beat + 0.1) * interval, 2000.0, interval);
                        float initial = Mathf.Abs(points[0].rectTransform.anchoredPosition.x);
                        Invoke(hud, "RenderEqualBeatTimeline", 2000 + (beat + 0.9) * interval, 2000.0, interval);
                        Require(Mathf.Abs(points[0].rectTransform.anchoredPosition.x) < initial, "Points must move inward throughout each beat.");
                        for (int pair = 0; pair < points.Length; pair += 2)
                            Require(Mathf.Approximately(points[pair].rectTransform.anchoredPosition.x,
                                -points[pair + 1].rectTransform.anchoredPosition.x), "Left/right points must arrive together.");
                        Invoke(hud, "RenderEqualBeatTimeline", 2000 + (beat + 0.99999) * interval, 2000.0, interval);
                        Require(Mathf.Abs(points[0].rectTransform.anchoredPosition.x) < 0.01f, "Points must reach the center immediately before the beat rollover.");
                    }
                }
                Invoke(hud, "RenderEqualBeatTimeline", 2000.0, 2000.0, 500.0);
                Require(center.rectTransform.localScale.x > 1f, "Center must pulse at the beat.");
                Invoke(hud, "UpdateEqualBeatTimeline");
                Require(points.All(point => !point.enabled), "No moving points before music is ready.");
                Require(center.rectTransform.localScale == Vector3.one, "Waiting state must clear the pulse.");
                Require(root.GetComponentsInChildren<Image>().All(graphic => !graphic.raycastTarget), "Timeline must not intercept input.");
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    Invoke(hud, "RenderEqualBeatTimeline", 2250.0, 2000.0, 500.0);
                    foreach (Image point in points)
                        point.enabled = true;
                    var camera = scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<Camera>(true)).First();
                    Capture(root.GetComponentInParent<Canvas>(), camera, 1280, 720);
                    Capture(root.GetComponentInParent<Canvas>(), camera, 1024, 768);
                }
                File.WriteAllText(Result, "PASS: scene mode; all-beat Basic selection; legacy fourth-beat skills; paired inward motion across bars/tempos; center arrival/pulse; waiting state; idempotent HUD; nonblocking UI.\nLive FMOD/input and visual playtesting are separate checks.");
                Debug.Log("FIGHT_SCENE3_BEAT_VALIDATION_PASS");
                passed = true;
            }
            catch (Exception exception)
            {
                File.WriteAllText(Result, "FAIL: " + exception);
                Debug.LogException(exception);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            if (Application.isBatchMode)
                EditorApplication.Exit(passed ? 0 : 1);
        }

        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);

        private static void Capture(Canvas canvas, Camera camera, int width, int height)
        {
            var target = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes($"Temp/FightScene3Beat-{width}x{height}.png", texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
        private static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
