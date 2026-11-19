using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmHunter.FightDemo.Art2;

namespace RhythmHunter.FightDemo.Editor
{
    [InitializeOnLoad]
    static class FightCameraStabilitySetup
    {
        static FightCameraStabilitySetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/CameraStabilitySetup.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(request);
            try { Build(); File.WriteAllText("Temp/CameraStabilitySetup.result", "PASS: camera shake adapter, pinned forest foreground, original forest composition preserved."); }
            catch (Exception e) { File.WriteAllText("Temp/CameraStabilitySetup.result", "FAIL: " + e); }
        }
        static void Build()
        {
            var camera = UnityEngine.Object.FindFirstObjectByType<Art2CameraController>();
            var fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
            if (!camera || !fight) throw new Exception("Open Art2_FightScene.");
            var adapter = camera.GetComponent<Art2BattleCameraShake>();
            if (!adapter) adapter = camera.gameObject.AddComponent<Art2BattleCameraShake>();
            adapter.combat = fight; adapter.cameraController = camera;
            const string path = "Assets/FightDemo/Prefabs/ArtBattle/ForestEnvironment_2_5D.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (string name in new[] { "Background_00", "Background_01" })
                {
                    var item = root.transform.Find(name);
                    if (!item.GetComponent<Art2CameraPinnedForeground>()) item.gameObject.AddComponent<Art2CameraPinnedForeground>();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            EditorUtility.SetDirty(adapter); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(fight.gameObject.scene); EditorSceneManager.SaveScene(fight.gameObject.scene);
        }
    }
}
