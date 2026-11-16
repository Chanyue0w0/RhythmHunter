using System;
using System.IO;
using System.Linq;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightBossEntranceSetup
    {
        const string Request="Temp/FightBossEntranceSetup.request";
        static FightBossEntranceSetup() => EditorApplication.update+=Poll;
        static void Poll()
        {
            const string revealRequest="Temp/FightBossRevealSetup.request";
            if(File.Exists(revealRequest) && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                File.Delete(revealRequest);
                try { ConfigureReveal(); } catch(Exception e) { File.WriteAllText("Temp/FightBossRevealSetup.result","FAIL: "+e); }
            }
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            File.Delete(Request);
            try { Configure(); }
            catch(Exception e) { File.WriteAllText("Temp/FightBossEntranceSetup.result","FAIL: "+e); }
        }
        [MenuItem("Rhythm Hunter/Boss/Configure Art2 Boss Entrance")]
        static void Configure()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            if(!scene.isLoaded) throw new Exception("Open Art2_FightScene in Edit mode first.");
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var planner=all.Select(t=>t.GetComponent<FightLevelPlanner>()).FirstOrDefault(x=>x!=null);
            if(planner==null||planner.SelectedStage==null)throw new Exception("Selected Stage missing");
            var host=planner.gameObject;
            var view=all.Select(t=>t.GetComponent<RhythmHunter.FightDemo.Art2.Art2CameraController>()).First(x=>x!=null).GetComponent<Camera>();
            var gate=all.FirstOrDefault(t=>t.name=="ClosedDoor_GroundAnchor");
            if(!gate)throw new Exception("ClosedDoor_GroundAnchor missing");
            var sprite=gate.GetComponentInChildren<SpriteRenderer>(true);
            if(!sprite)throw new Exception("Door sprite missing");
            var door=gate.GetComponent<FightBossDoorAnimation>()??Undo.AddComponent<FightBossDoorAnimation>(gate.gameObject);
            var animator=gate.GetComponent<FightCharacterCombatAnimator>()??Undo.AddComponent<FightCharacterCombatAnimator>(gate.gameObject);
            var anchor=gate.Find("BossDoorSpawnAnchor");
            if(!anchor)
            {
                var go=new GameObject("BossDoorSpawnAnchor");Undo.RegisterCreatedObjectUndo(go,"Boss door anchor");
                anchor=go.transform;anchor.SetParent(gate,false);
            }
            var intro=host.GetComponent<FightBossEntrance>()??Undo.AddComponent<FightBossEntrance>(host);
            Set(door,"doorRenderer",sprite);Set(door,"animator",animator);
            Set(animator,"targetRenderer",sprite);Set(animator,"beatSource",host.GetComponent<FightCombatController>());
            Set(intro,"planner",planner);Set(intro,"roster",host.GetComponent<FightRosterManager>());
            Set(intro,"fight",host.GetComponent<FightCombatController>());Set(intro,"beatClock",host.GetComponent<FmodBeatClock>());
            Set(intro,"view",view);Set(intro,"door",door);Set(intro,"doorSpawnAnchor",anchor);
            Set(intro,"doorFanShader",AssetDatabase.LoadAssetAtPath<Shader>("Assets/FightDemo/Art2Prototype/BossDoorFan.shader"));
            var stage=new SerializedObject(planner.SelectedStage);stage.FindProperty("bossStage").boolValue=true;
            var config=stage.FindProperty("bossEntrance");config.FindPropertyRelative("enabled").boolValue=true;
            config.FindPropertyRelative("landingEffect").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/VFX/GroundSmoke.prefab");
            stage.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/FightBossEntranceSetup.result","PASS: selected Stage BossStage enabled; shared entrance, door animator and spawn anchor wired; GroundSmoke assigned. Door openingFrames empty pending artwork. Stage="+AssetDatabase.GetAssetPath(planner.SelectedStage));
        }
        static void Set(UnityEngine.Object target,string name,UnityEngine.Object value)
        { var so=new SerializedObject(target);so.FindProperty(name).objectReferenceValue=value;so.ApplyModifiedProperties(); }

        [MenuItem("Rhythm Hunter/Boss/Configure Door Smoke Reveal")]
        static void ConfigureReveal()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            if(!scene.isLoaded)throw new Exception("Open Art2_FightScene first");
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var source=all.FirstOrDefault(t=>t.name=="SmokeExplosion_Dark");
            if(!source)throw new Exception("Scene SmokeExplosion_Dark not found");
            var intro=all.Select(t=>t.GetComponent<FightBossEntrance>()).First(x=>x);
            var planner=intro.GetComponent<FightLevelPlanner>();
            var door=all.First(t=>t.name=="ClosedDoor_GroundAnchor");
            string path="Assets/FightDemo/Prefabs/VFX/SmokeExplosion_Dark.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!prefab)
            {
                var copy=UnityEngine.Object.Instantiate(source.gameObject);
                copy.name="SmokeExplosion_Dark";copy.transform.position=Vector3.zero;copy.SetActive(true);
                try
                {
                    var settings=copy.GetComponent<FightEffectSettings>()??copy.AddComponent<FightEffectSettings>();
                    settings.destroyAfterParticles=true;
                    foreach(var particles in copy.GetComponentsInChildren<ParticleSystem>(true))
                    { var main=particles.main;main.loop=false;main.stopAction=ParticleSystemStopAction.None;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate; }
                    foreach(var renderer in copy.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    { renderer.sortingOrder=Mathf.Max(renderer.sortingOrder,100); renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; }
                    prefab=PrefabUtility.SaveAsPrefabAsset(copy,path);
                }
                finally { UnityEngine.Object.DestroyImmediate(copy); }
            }
            var contents=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var controller in contents.GetComponentsInChildren<CartoonFX.CFXR_Effect>(true))
                { controller.clearBehavior=CartoonFX.CFXR_Effect.ClearBehavior.None; controller.cameraShake.enabled=false; }
                PrefabUtility.SaveAsPrefabAsset(contents,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            var anchor=door.Find("BossDoorSmokeAnchor");
            if(!anchor)
            { var go=new GameObject("BossDoorSmokeAnchor");Undo.RegisterCreatedObjectUndo(go,"Boss smoke anchor");anchor=go.transform;anchor.SetParent(door,false);anchor.position=source.position; }
            Set(intro,"doorExplosionAnchor",anchor);Set(intro,"doorFocusAnchor",door);
            var stage=new SerializedObject(planner.SelectedStage);
            stage.FindProperty("bossEntrance").FindPropertyRelative("doorExplosion").objectReferenceValue=prefab;
            stage.ApplyModifiedProperties();EditorUtility.SetDirty(planner.SelectedStage);
            Undo.RecordObject(source.gameObject,"Disable placed smoke preview");source.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/FightBossRevealSetup.result","PASS: smoke Prefab saved and linked; authored scene smoke retained inactive; smoke anchor preserves scene position.\n"+string.Join("\n",prefab.GetComponentsInChildren<ParticleSystem>(true).Select(p=>$"{p.name}: duration={p.main.duration}, lifetime={p.main.startLifetime.constantMax}, delay={p.main.startDelay.constantMax}")));
        }
    }
}
