using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmHunter.FightDemo;
using RhythmHunter.FightDemo.Art2;
using RhythmHunter.RhythmDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightWaveEntranceSetup
    {
        static FightWaveEntranceSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/WaveEntranceSetup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(request); } catch (IOException) { return; }
            try { Run(); } catch (Exception e) { File.WriteAllText("Temp/WaveEntranceSetup.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Levels/Configure Art2 Wave Entrance")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.path.EndsWith("Art2_FightScene.unity") || EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Open Art2 outside Play Mode.");
            var roots = scene.GetRootGameObjects();
            var fight = roots.SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
            var roster = roots.SelectMany(r => r.GetComponentsInChildren<FightRosterManager>(true)).Single();
            var clock = roots.SelectMany(r => r.GetComponentsInChildren<FmodBeatClock>(true)).Single();
            var camera = roots.SelectMany(r => r.GetComponentsInChildren<Art2CameraController>(true)).Single().GetComponent<Camera>();
            ValidateProjection();
            ValidateSidesAndRestore();
            var entrance = fight.GetComponent<FightWaveEntrance>();
            if (!entrance) entrance = Undo.AddComponent<FightWaveEntrance>(fight.gameObject);
            Undo.RecordObject(entrance, "Configure one-beat wave entrance");
            var data = new SerializedObject(entrance);
            data.FindProperty("fight").objectReferenceValue = fight;
            data.FindProperty("roster").objectReferenceValue = roster;
            data.FindProperty("beatClock").objectReferenceValue = clock;
            data.FindProperty("view").objectReferenceValue = camera;
            data.FindProperty("slideEnabled").boolValue = true;
            data.FindProperty("durationBeats").floatValue = 1;
            data.FindProperty("hopOnArrival").boolValue = true;
            data.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed");
            File.WriteAllText("Temp/WaveEntranceSetup.result", "PASS: Art2 one-beat entrance with arrival hop configured; six initial actors / later enemies only; arrival hops once, cancellation never hops, input counters unchanged; roots restored; orthographic/perspective bounds validated.");
        }
        private static void ValidateSidesAndRestore()
        {
            var root = new GameObject("Entrance State Validation") { hideFlags = HideFlags.HideAndDontSave };
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            try
            {
                var camera = root.AddComponent<Camera>(); camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = 6; camera.aspect = 16f / 9;
                // Keep the test root still; the camera gets its own transform.
                var cameraObject = new GameObject("Validation Camera"); cameraObject.transform.SetParent(root.transform);
                var view = cameraObject.AddComponent<Camera>(); view.enabled = false; view.CopyFrom(camera);
                view.transform.SetPositionAndRotation(new Vector3(0,10,-10), Quaternion.Euler(45,0,0));
                var roster = root.AddComponent<FightRosterManager>();
                var clock = root.AddComponent<FmodBeatClock>();
                var entrance = root.AddComponent<FightWaveEntrance>();
                var data = new SerializedObject(entrance);
                data.FindProperty("view").objectReferenceValue = view;
                data.FindProperty("roster").objectReferenceValue = roster;
                data.FindProperty("beatClock").objectReferenceValue = clock;
                data.ApplyModifiedPropertiesWithoutUndo();
                var actorRoots = new Transform[6]; var slotRoots = new Transform[6]; var original = new Vector3[6];
                for (int i = 0; i < 6; i++)
                {
                    var obj = new GameObject("Slot " + i); obj.transform.SetParent(root.transform);
                    obj.transform.localPosition = new Vector3(i < 3 ? 3 + i : -3 - (i - 3), 0, 0);
                    slotRoots[i] = obj.transform; original[i] = obj.transform.position;
                    var slot = obj.AddComponent<FightUnitSlot>();
                    var visual = new GameObject("ActorRoot"); visual.transform.SetParent(obj.transform, false); actorRoots[i] = visual.transform;
                    var actor = new GameObject("Actor"); actor.transform.SetParent(visual.transform, false);
                    typeof(FightUnitSlot).GetField("actorInstance", flags).SetValue(slot, actor);
                    var slotData = new SerializedObject(slot); slotData.FindProperty("actorRoot").objectReferenceValue = visual.transform; slotData.ApplyModifiedPropertiesWithoutUndo();
                    var list = (System.Collections.Generic.List<FightUnitSlot>)typeof(FightRosterManager).GetField(i < 3 ? "activeHeroes" : "activeEnemies", flags).GetValue(roster);
                    list.Add(slot);
                }
                var begin = typeof(FightWaveEntrance).GetMethod("Begin", flags);
                var clear = typeof(FightWaveEntrance).GetMethod("Clear", flags);
                begin.Invoke(entrance, new object[] { true });
                for (int i = 0; i < 6; i++)
                    if (actorRoots[i].localPosition.sqrMagnitude < .01f || slotRoots[i].position != original[i]) throw new Exception("Initial entrance did not move only visual roots");
                clear.Invoke(entrance, null);
                if (actorRoots.Any(t => t.localPosition.sqrMagnitude > .000001f) || entrance.IsRunning) throw new Exception("Cancelled entrance left offsets");
                begin.Invoke(entrance, new object[] { false });
                for (int i = 0; i < 6; i++)
                    if (i < 3 ? actorRoots[i].localPosition.sqrMagnitude > .000001f : actorRoots[i].localPosition.sqrMagnitude < .01f)
                        throw new Exception("Later wave moved wrong side");
                clear.Invoke(entrance, null);
                var slots = slotRoots.Select(t => t.GetComponent<FightUnitSlot>()).ToArray();
                if (slots.Any(s => s.ArrivalHopPlayCount != 0)) throw new Exception("Cancelled entrance triggered a hop");
                var complete = typeof(FightWaveEntrance).GetMethod("CompleteArrival", flags);
                begin.Invoke(entrance, new object[] { true });
                complete.Invoke(entrance, null);
                complete.Invoke(entrance, null);
                if (slots.Any(s => s.ArrivalHopPlayCount != 1) || entrance.IsRunning || actorRoots.Any(t => t.localPosition.sqrMagnitude > .000001f))
                    throw new Exception("Initial arrival did not restore roots and hop exactly once");
                begin.Invoke(entrance, new object[] { false });
                complete.Invoke(entrance, null);
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i].ArrivalHopPlayCount != (i < 3 ? 1 : 2) || slots[i].OnBeatFeedbackPlayCount != 0 || slots[i].MissFeedbackPlayCount != 0)
                        throw new Exception("Arrival affected wrong side or counted as player input");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void ValidateProjection()
        {
            var root = new GameObject("Entrance Projection Validation") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camera = root.AddComponent<Camera>(); camera.enabled = false;
                camera.transform.SetPositionAndRotation(new Vector3(0, 10, -10), Quaternion.Euler(45, 0, 0));
                camera.aspect = 16f / 9; camera.orthographicSize = 6; camera.fieldOfView = 40;
                var method = typeof(FightWaveEntrance).GetMethod("CalculateStartOffset", BindingFlags.NonPublic | BindingFlags.Static);
                foreach (bool ortho in new[] { true, false })
                foreach (float x in new[] { -6f, 0, 6f })
                foreach (bool right in new[] { true, false })
                {
                    camera.orthographic = ortho;
                    var origin = new Vector3(x, 0, 0);
                    var bounds = new Bounds(origin + Vector3.up, new Vector3(3, 2, 1));
                    var offset = (Vector3)method.Invoke(null, new object[] { camera, bounds, origin, right, .08f });
                    if (Mathf.Abs(offset.y) > .00001f) throw new Exception("Entrance left the ground plane");
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = bounds.center + offset + Vector3.Scale(bounds.extents,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        float px = camera.WorldToViewportPoint(corner).x;
                        if (right ? px < 1.079f : px > -.079f) throw new Exception("Entrance card is still on screen");
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
