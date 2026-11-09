using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightDamageEdgeSetup
    {
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        static FightDamageEdgeSetup() => EditorApplication.update += Poll;
        static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
        static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void Poll()
        {
            if (SessionState.GetInt("DamageEdgePlayCheck", 0) != 0) { TickPlayCheck(); return; }
            const string request = "Temp/DamageEdgeSetup.request";
            if (File.Exists(request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating && File.ReadAllText(request).Trim() == "play-check")
            {
                File.Delete(request);
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetInt("DamageEdgePlayCheck", 1);
                SessionState.SetFloat("DamageEdgePlayCheckTime", (float)EditorApplication.timeSinceStartup);
                EditorApplication.isPlaying = true; return;
            }
            if (File.Exists(request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating && File.ReadAllText(request).Trim() == "inspect")
            {
                File.Delete(request);
                var rows = new System.Collections.Generic.List<string> { "Playing=" + EditorApplication.isPlaying };
                foreach (var feedback in UnityEngine.Object.FindObjectsByType<FightDamageEdgeFeedback>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var fight = (FightCombatController)feedback.GetType().GetField("fight", Flags).GetValue(feedback);
                    rows.Add($"Feedback enabled={feedback.enabled} active={feedback.gameObject.activeInHierarchy} fight={fight} hp={fight?.PartyHp} armor={fight?.PartyArmor} ended={fight?.BattleEnded} awaiting={fight?.AwaitingBattleStart}");
                    foreach (var graphic in feedback.GetComponentsInChildren<FightScreenEdgeGraphic>(true))
                    {
                        var renderer = graphic.GetComponent<CanvasRenderer>();
                        var mesh = renderer != null ? renderer.GetMesh() : null;
                        rows.Add($"Graphic active={graphic.gameObject.activeInHierarchy} color={graphic.color} rect={graphic.rectTransform.rect} pos={graphic.transform.position} scale={graphic.transform.lossyScale} canvas={graphic.canvas?.renderMode} pixelRect={graphic.canvas?.pixelRect} order={graphic.canvas?.sortingOrder} mesh={mesh?.vertexCount} culled={(renderer != null && renderer.cull)}");
                        
                    }
                }
                File.WriteAllLines("Temp/DamageEdgeInspection.result", rows); return;
            }
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(request); } catch (IOException) { return; }
            try { Run(); File.WriteAllText("Temp/DamageEdgeSetup.result", "PASS: armor blue, spillover HP red, zero/blocked damage silent, low-HP pulse and healing clear, result cleanup, no input interception; mesh centre transparent."); }
            catch (Exception e) { File.WriteAllText("Temp/DamageEdgeSetup.result", "FAIL: " + e); Debug.LogException(e); }
        }
        static void TickPlayCheck()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            int step = SessionState.GetInt("DamageEdgePlayCheck", 0);
            float elapsed = (float)EditorApplication.timeSinceStartup - SessionState.GetFloat("DamageEdgePlayCheckTime", 0);
            if (elapsed > 60) { SessionState.SetInt("DamageEdgePlayCheck", 0); EditorApplication.isPlaying = false; File.WriteAllText("Temp/DamageEdgePlayCheck.result", "FAIL: timed out"); return; }
            if (!EditorApplication.isPlaying) return;
            var feedback = UnityEngine.Object.FindFirstObjectByType<FightDamageEdgeFeedback>();
            var fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
            if (feedback == null || fight == null || fight.AwaitingBattleStart) return;
            if (step == 1)
            {
                Set(fight, "endBattleOnZeroHp", false);
                Set(fight, "partyHp", 4f); Set(fight, "partyArmor", 2f);
                Set(feedback, "hitFlashSeconds", 4f); // Test only: enough time to inspect a rendered frame.
                fight.ApplyPartyDamage(.5f, 0);
                SessionState.SetInt("DamageEdgePlayCheck", 2);
                SessionState.SetFloat("DamageEdgeCaptureTime", (float)EditorApplication.timeSinceStartup);
                EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
                return;
            }
            float sinceHit = (float)EditorApplication.timeSinceStartup - SessionState.GetFloat("DamageEdgeCaptureTime", 0);
            if (step == 2 && sinceHit > .25f)
            {
                var graphic = feedback.GetComponentInChildren<FightScreenEdgeGraphic>(true);
                var mesh = graphic != null ? graphic.canvasRenderer.GetMesh() : null;
                File.WriteAllText("Temp/DamageEdgePlayCheck.result", $"Graphic={graphic != null} active={graphic?.gameObject.activeInHierarchy} color={graphic?.color} rect={graphic?.rectTransform.rect} canvasRect={graphic?.canvas.pixelRect} mesh={mesh?.vertexCount} culled={graphic?.canvasRenderer.cull}");
                ScreenCapture.CaptureScreenshot("Temp/DamageEdgePlayCheck.png");
                SessionState.SetInt("DamageEdgePlayCheck", 3);
            }
            if (step == 3 && sinceHit > 1f)
            {
                SessionState.SetInt("DamageEdgePlayCheck", 0);
                EditorApplication.isPlaying = false;
            }
        }
        static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.path.EndsWith("Art2_FightScene.unity")) throw new Exception("Open Art2 outside Play Mode.");
            var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
            var feedback = fight.GetComponent<FightDamageEdgeFeedback>() ?? Undo.AddComponent<FightDamageEdgeFeedback>(fight.gameObject);
            var data = new SerializedObject(feedback);
            data.FindProperty("fight").objectReferenceValue = fight;
            data.ApplyModifiedProperties();
            Validate();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed");
        }
        static void Validate()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                fight.RosterManager.SpawnConfiguredRoster(); Call(fight, "RebuildRosterAndResetCombat");
                Set(fight.RosterManager, "<BattleStarted>k__BackingField", true);
                Set(fight, "endBattleOnZeroHp", false);
                var feedback = fight.GetComponent<FightDamageEdgeFeedback>() ?? fight.gameObject.AddComponent<FightDamageEdgeFeedback>();
                Set(feedback, "fight", fight); Call(feedback, "OnDisable"); Call(feedback, "OnEnable");
                FightScreenEdgeGraphic Graphic() => fight.GetComponentInChildren<FightScreenEdgeGraphic>(true);
                int events = 0; fight.PartyDamageTaken += (_, _) => events++;
                Set(fight, "partyHp", 4f); Set(fight, "partyArmor", 2f);
                fight.ApplyPartyDamage(0, 1);
                Check(events == 0 && Graphic() == null, "Blocked/zero damage created a flash");
                fight.ApplyPartyDamage(.5f, 1);
                var graphic = Graphic();
                Check(graphic != null && graphic.color.b > graphic.color.r && graphic.color.a > 0, "Armor damage did not flash blue");
                Check(graphic.canvasRenderer != null, "Edge graphic is missing its CanvasRenderer");
                Canvas.ForceUpdateCanvases();
                graphic.Rebuild(CanvasUpdate.PreRender);
                var drawnMesh = graphic.canvasRenderer.GetMesh();
                try
                {
                    Check(drawnMesh != null, "No rendered mesh");
                    Check(drawnMesh.vertexCount == 52 && drawnMesh.bounds.size.x > 0 && drawnMesh.bounds.size.y > 0,
                        "CanvasRenderer has no drawable full-screen border mesh");
                    Check(graphic.rectTransform.anchorMin == Vector2.zero && graphic.rectTransform.anchorMax == Vector2.one &&
                        graphic.rectTransform.offsetMin == Vector2.zero && graphic.rectTransform.offsetMax == Vector2.zero,
                        "Edge graphic does not stretch over the entire canvas");
                }
                finally { }
                fight.ApplyPartyDamage(2f, 2);
                Check(graphic.color.r > graphic.color.b && events == 2, "Damage spilling into HP must flash red");
                Check(!graphic.raycastTarget && graphic.canvas.GetComponent<GraphicRaycaster>() == null, "Flash intercepts input");
                Set(fight, "partyHp", 1f);
                Call(feedback, "Render", 1f);
                Check(graphic.gameObject.activeInHierarchy && graphic.color.g > .2f && graphic.color.a > 0 && graphic.color.a < .25f, "Low HP warning must be a softer red");
                Call(fight, "HealPartyHealth", fight.RosterManager.ActiveHeroes.First(), .5f);
                Call(feedback, "Render", 0f);
                Check(!graphic.gameObject.activeInHierarchy, "Healing above threshold did not clear warning");
                Set(fight, "partyHp", .5f); Call(feedback, "Render", 0f);
                Check(graphic.gameObject.activeInHierarchy, "Half-heart warning missing");
                Set(fight, "battleEnded", true); Call(feedback, "Render", 0f);
                Check(!graphic.gameObject.activeInHierarchy, "Warning remained behind completed results");
                graphic.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1920);
                graphic.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1080);
                using (var vertices = new VertexHelper())
                {
                    typeof(FightScreenEdgeGraphic).GetMethod("OnPopulateMesh", Flags, null,
                        new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { vertices });
                    Check(vertices.currentVertCount == 52, "Unexpected border geometry");
                    var vertex = new UIVertex(); vertices.PopulateUIVertex(ref vertex, 48);
                    Check(vertex.color.a == 0, "Inner edge is not transparent");
                }
                Call(feedback, "OnDisable"); Set(fight, "battleEnded", false);
                fight.ApplyPartyDamage(.5f, 3);
                Check(!graphic.gameObject.activeInHierarchy, "Disabled feedback still subscribed");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
