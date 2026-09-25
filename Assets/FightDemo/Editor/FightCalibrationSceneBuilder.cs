using System;
using System.IO;
using System.Linq;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightCalibrationSceneBuilder
    {
        public const string ScenePath="Assets/FightDemo/Scenes/FightCalibrationScene.unity";
        public const string BattlePath="Assets/FightDemo/Scenes/FightScene3.unity";
        const string Request="Temp/FightCalibrationSceneBuild.request";
        static FightCalibrationSceneBuilder(){EditorApplication.update+=CheckRequest;}
        static void CheckRequest()
        {
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)return;
            File.Delete(Request);
            try { Build();File.WriteAllText("Temp/FightCalibrationSceneBuild.result","PASS: calibration scene created and both scenes enabled in Build Settings."); }
            catch(Exception e){File.WriteAllText("Temp/FightCalibrationSceneBuild.result","FAIL: "+e);Debug.LogException(e);}
        }

        [MenuItem("Rhythm Hunter/Create Calibration Scene")]
        public static void Build()
        {
            if(File.Exists(ScenePath)){EnsureBuildEntries();return;}
            Scene previous=SceneManager.GetActiveScene();
            Scene source=EditorSceneManager.OpenPreviewScene(BattlePath);
            Scene scene=default;
            try
            {
                var objects=source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var originalClock=objects.OfType<FmodBeatClock>().Single();
                var originalJudge=objects.OfType<FmodRhythmJudge>().Single();
                scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var camera=new GameObject("Main Camera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.025f,.05f);
                camera.transform.position=new Vector3(0,0,-10);
                var canvas=new GameObject("CalibrationCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
                canvas.renderMode=RenderMode.ScreenSpaceOverlay;
                var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
                new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                var controller=new GameObject("CalibrationController");
                var clock=controller.AddComponent<FmodBeatClock>();EditorUtility.CopySerialized(originalClock,clock);
                var judge=controller.AddComponent<FmodRhythmJudge>();
                judge.Configure(clock,originalJudge.PerfectWindowMs,originalJudge.VisualOffsetMs);
                var router=controller.AddComponent<FightInputRouter>();router.Configure(AssetDatabase.LoadAssetAtPath<InputActionAsset>(FightSceneBuilder.InputActionsPath));
                controller.AddComponent<FightCalibrationScene>().Configure(clock,judge,router,canvas,Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
                if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Could not save calibration scene.");
                EnsureBuildEntries();
                // Migration copies existing values, never overwrites the original PlayerPrefs.
                var saved=RhythmCalibrationStore.Read();
                File.WriteAllText("Temp/CalibrationMigrationValues.json",JsonUtility.ToJson(saved,true));
            }
            finally
            {
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                if(scene.IsValid())EditorSceneManager.CloseScene(scene,true);
                EditorSceneManager.ClosePreviewScene(source);
            }
        }

        static void EnsureBuildEntries()
        {
            EditorBuildSettingsScene[] Add(EditorBuildSettingsScene[] current)
            {
                var scenes=current.ToList();
                foreach(string path in new[]{ScenePath,BattlePath})
                {
                    var entry=scenes.FirstOrDefault(s=>s.path==path);
                    if(entry!=null)entry.enabled=true;else scenes.Add(new EditorBuildSettingsScene(path,true));
                }
                return scenes.ToArray();
            }
            // Unity 6 may use a platform Build Profile instead of the versioned global list.
            EditorBuildSettings.globalScenes=Add(EditorBuildSettings.globalScenes);
            EditorBuildSettings.scenes=Add(EditorBuildSettings.scenes);
            AssetDatabase.SaveAssets();
        }
    }
}
