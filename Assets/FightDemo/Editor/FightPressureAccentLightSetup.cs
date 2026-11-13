using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightPressureAccentLightSetup
    {
        const string ObjectName = "PressureRedAccentLight_AdjustMe";
        static FightPressureAccentLightSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/PressureAccentLight.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Configure(); }
            catch (Exception e) { File.WriteAllText("Temp/PressureAccentLight.result", "FAIL: " + e); Debug.LogException(e); }
        }

        [MenuItem("Rhythm Hunter/Art2/Configure Pressure Red Accent Light")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play before saving scene lighting.");
            var scene = SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            if (!scene.isLoaded) throw new Exception("Open Art2_FightScene first.");
            var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var controller = objects.Select(t => t.GetComponent<FightCombatController>()).First(c => c != null);
            var host = objects.Single(t => t.name == "Art2_2_5D_Prototype");
            var existing = objects.FirstOrDefault(t => t.name == ObjectName);
            var sun = RenderSettings.sun;
            if (sun == null) throw new Exception("Expected existing explicit main sun light; do not replace the main-light shadow source.");
            Color sunColor = sun.color;
            float sunIntensity = sun.intensity;
            Quaternion sunRotation = sun.transform.rotation;
            GameObject go;
            if (existing == null)
            {
                go = new GameObject(ObjectName);
                Undo.RegisterCreatedObjectUndo(go, "Create Pressure red accent light");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.SetParent(host, false);
                go.transform.position = new Vector3(-5, 8, -4);
                go.transform.rotation = Quaternion.Euler(50, -35, 0);
                var light = Undo.AddComponent<Light>(go);
                light.type = LightType.Directional;
                light.color = new Color(1, .12f, .075f);
                light.intensity = 1.8f;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForcePixel;
                light.cullingMask = -1;
                light.useColorTemperature = false;
                Undo.AddComponent<UniversalAdditionalLightData>(go);
            }
            else go = existing.gameObject;
            var control = go.GetComponent<FightPressureAccentLight>() ?? Undo.AddComponent<FightPressureAccentLight>(go);
            Undo.RecordObject(control, "Bind Pressure red accent light");
            control.Configure(controller);
            Validate(control);
            if (RenderSettings.sun != sun || sun.color != sunColor || sun.intensity != sunIntensity || sun.transform.rotation != sunRotation)
                throw new Exception("Main sun was unexpectedly changed.");
            EditorUtility.SetDirty(control);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            SceneView.RepaintAll();
            File.WriteAllText("Temp/PressureAccentLight.result", "PASS: Pressure accent light bound to shared warning fade; half/full/fade-out intensity and restoration verified; preview controls and runtime gating verified; existing main sun preserved. User light tuning preserved.");
        }

        static void Validate(FightPressureAccentLight control)
        {
            var light = control.GetComponent<Light>();
            bool preview = control.previewInEditor, enabled = control.enablePressureLight;
            try
            {
                control.enablePressureLight = true;
                control.previewInEditor = false; control.RefreshLight();
                if (light.enabled) throw new Exception("Preview off did not switch off light.");
                control.previewInEditor = true; control.RefreshLight();
                if (!light.enabled) throw new Exception("Preview on did not illuminate.");
                var method = typeof(FightPressureAccentLight).GetMethod("ShouldIlluminate", BindingFlags.NonPublic | BindingFlags.Static);
                bool Gate(bool feature, bool playing, bool editPreview, bool pressure) => (bool)method.Invoke(null, new object[] { feature, playing, editPreview, pressure });
                if (Gate(true, true, true, false) || !Gate(true, true, false, true) || Gate(false, true, true, true))
                    throw new Exception("Pressure gating failed.");
                var apply = typeof(FightPressureAccentLight).GetMethod("ApplyLightState", BindingFlags.NonPublic | BindingFlags.Instance);
                float full = light.intensity;
                void Blend(float value) => apply.Invoke(control, new object[] { true, value });
                Blend(.5f);
                if (!light.enabled || !Mathf.Approximately(light.intensity, full * .5f)) throw new Exception("Half fade failed.");
                Blend(1);
                if (!Mathf.Approximately(light.intensity, full)) throw new Exception("Full fade failed.");
                Blend(.25f);
                if (!Mathf.Approximately(light.intensity, full * .25f)) throw new Exception("Fade-out failed.");
                Blend(0);
                if (light.enabled || !Mathf.Approximately(light.intensity, full)) throw new Exception("Intensity restoration failed.");
                control.enablePressureLight = false; control.RefreshLight();
                if (light.enabled) throw new Exception("Master switch failed.");
            }
            finally { control.previewInEditor = preview; control.enablePressureLight = enabled; control.RefreshLight(); }
        }
    }
}
