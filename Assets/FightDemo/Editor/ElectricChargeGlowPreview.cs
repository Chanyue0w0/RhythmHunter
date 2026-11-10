using System;
using System.IO;
using System.Linq;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    // Editor-only, disposable visual copy: never changes the source effect's timing or scene data.
    public sealed class ElectricChargeGlowPreview : EditorWindow
    {
        const string Path = "Assets/FightDemo/Prefabs/Skills/Mage/ElectricChargeGlow.prefab";
        GameObject source, copy;
        ParticleSystem[] particles;
        bool loop = true;
        float time = .075f;
        double previous;
        SceneView view;
        bool oldRefresh, oldParticles;

        [MenuItem("Rhythm Hunter/VFX/Preview Electric Charge Glow")]
        public static void Open()
        {
            var window = GetWindow<ElectricChargeGlowPreview>("Electric Charge Preview");
            window.minSize = new Vector2(390, 210);
            window.FindSource(); window.Show();
        }
        void OnEnable()
        {
            previous = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
        }
        void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload -= Clear;
            Clear();
        }
        void OnPlayMode(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingEditMode) Clear(); }
        void FindSource()
        {
            Clear();
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == Path) source = stage.prefabContentsRoot;
            else
            {
                var candidate = Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(p =>
                    !EditorUtility.IsPersistent(p) && p.gameObject.scene == SceneManager.GetActiveScene() && p.name == "ElectricChargeGlowVFX" && (p.gameObject.hideFlags & HideFlags.DontSave) == 0);
                source = candidate != null ? (candidate.transform.parent != null ? candidate.transform.parent.gameObject : candidate.gameObject) : null;
            }
            if (source == null) return;
            copy = Instantiate(source); copy.name = "ElectricChargeGlow_EditorPreview";
            SceneManager.MoveGameObjectToScene(copy, source.scene);
            foreach (var t in copy.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.hideFlags = HideFlags.HideAndDontSave;
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }
            foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true)) if (script != null) DestroyImmediate(script);
            particles = copy.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var p in particles) { var main = p.main; main.stopAction = ParticleSystemStopAction.None; p.useAutoRandomSeed = false; p.randomSeed = 1; }
            view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                oldRefresh = view.sceneViewState.alwaysRefresh; oldParticles = view.sceneViewState.showParticleSystems;
                view.sceneViewState.alwaysRefresh = true; view.sceneViewState.showParticleSystems = true;
            }
            time = .075f; previous = EditorApplication.timeSinceStartup; Simulate();
        }
        void Clear()
        {
            if (copy != null) DestroyImmediate(copy);
            copy = null; particles = null;
            if (view != null) { view.sceneViewState.alwaysRefresh = oldRefresh; view.sceneViewState.showParticleSystems = oldParticles; view.Repaint(); }
            view = null;
        }
        void Tick()
        {
            double now = EditorApplication.timeSinceStartup, delta = now - previous; previous = now;
            if (EditorApplication.isPlayingOrWillChangePlaymode || copy == null) return;
            if (source == null) { Clear(); return; }
            copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            copy.transform.localScale = source.transform.lossyScale;
            if (loop) time = (time + (float)delta) % .6f;
            Simulate();
        }
        void Simulate()
        {
            if (particles == null) return;
            foreach (var p in particles) if (p != null) { p.Simulate(time, false, true, false); p.Pause(false); }
            SceneView.RepaintAll(); Repaint();
        }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("原特效只在 0／0.25 秒閃光，各持續 0.15 秒。此視窗建立不儲存的預覽副本；不改遊戲播放與場景設定。關閉視窗即移除。", MessageType.Info);
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("場景來源", source, typeof(GameObject), true);
            loop = EditorGUILayout.Toggle("循環預覽", loop);
            EditorGUI.BeginChangeCheck(); time = EditorGUILayout.Slider("定格秒數", time, 0, .5f);
            if (EditorGUI.EndChangeCheck()) { loop = false; Simulate(); }
            if (GUILayout.Button("定格在最亮的 0.075 秒")) { loop = false; time = .075f; Simulate(); }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("尋找／重新載入場景中的特效")) FindSource();
            if (source == null) EditorGUILayout.LabelField("請將 ElectricChargeGlow 放進場景，或開啟其 Prefab Mode。");
        }

    }
}
