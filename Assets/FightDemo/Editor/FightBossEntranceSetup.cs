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
            const string focusRequest="Temp/Art2CinematicFocusSetup.request";
            if(File.Exists(focusRequest) && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                File.Delete(focusRequest);
                try
                {
                    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                    if(!scene.isLoaded)throw new Exception("Open Art2 scene first");
                    var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
                    var camera=all.Select(t=>t.GetComponent<RhythmHunter.FightDemo.Art2.Art2CameraController>()).First(x=>x);
                    var focus=camera.GetComponent<RhythmHunter.FightDemo.Art2.Art2CinematicFocus>()??Undo.AddComponent<RhythmHunter.FightDemo.Art2.Art2CinematicFocus>(camera.gameObject);
                    var intro=all.Select(t=>t.GetComponent<FightBossEntrance>()).First(x=>x);
                    Set(focus,"roster",intro.GetComponent<FightRosterManager>());Set(intro,"cinematicFocus",focus);
                    var walls=all.Select(t=>t.GetComponent<RhythmHunter.FightDemo.Art2.Art2FocusWall>()).Where(x=>x).ToArray();
                    var so=new SerializedObject(focus);var list=so.FindProperty("distantWalls");list.arraySize=walls.Length;
                    for(int i=0;i<walls.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=walls[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
                    File.WriteAllText("Temp/Art2CinematicFocusSetup.result","PASS: Art2_TestCamera cinematic focus wired to entrance and "+walls.Length+" distant walls. Default blur=1.2, background blur retention=0, fog retention=1.");
                }
                catch(Exception e){File.WriteAllText("Temp/Art2CinematicFocusSetup.result","FAIL: "+e);}
            }
            const string foregroundRequest="Temp/FightBossHowlForeground.request";
            if(File.Exists(foregroundRequest) && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                File.Delete(foregroundRequest);
                try
                {
                    const string path="Assets/FightDemo/Prefabs/VFX/howl_VFX.prefab";
                    var root=PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var player=root.GetComponent<FightBeatSpriteEffect>();
                        var so=new SerializedObject(player);so.FindProperty("drawInFrontOfCaster").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
                        var presentation=root.GetComponent<RhythmHunter.FightDemo.Art2.Art2EffectPresentation>()??root.AddComponent<RhythmHunter.FightDemo.Art2.Art2EffectPresentation>();
                        so=new SerializedObject(presentation);so.FindProperty("ignoreFloorOcclusion").boolValue=true;so.FindProperty("faceSpritesToCamera").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(root,path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                    AssetDatabase.SaveAssets();File.WriteAllText("Temp/FightBossHowlForeground.result","PASS: howl camera-facing foreground depth and post-character floor-recovery rendering enabled.");
                }
                catch(Exception e){File.WriteAllText("Temp/FightBossHowlForeground.result","FAIL: "+e);}
            }
            const string flightRequest="Temp/FightBossFlightSetup.request";
            if(File.Exists(flightRequest) && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                File.Delete(flightRequest);
                try { ConfigureFlight(); } catch(Exception e) { File.WriteAllText("Temp/FightBossFlightSetup.result","FAIL: "+e); }
            }
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
        [MenuItem("Rhythm Hunter/Boss/Configure Flight And Howl")]
        static void ConfigureFlight()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            if(!scene.isLoaded) throw new Exception("Open Art2 scene first");
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var source=all.FirstOrDefault(t=>t.name.Equals("howl_VFX",StringComparison.OrdinalIgnoreCase));
            if(!source) throw new Exception("Scene howl_VFX missing; please save/restore authored scene object.");
            string effectPath="Assets/FightDemo/Prefabs/VFX/howl_VFX.prefab";
            var copy=UnityEngine.Object.Instantiate(source.gameObject);
            GameObject effectPrefab;
            try
            {
                copy.name="howl_VFX";copy.transform.position=Vector3.zero;copy.SetActive(true);
                var renderer=copy.GetComponentInChildren<SpriteRenderer>(true);
                if(!renderer) throw new Exception("howl_VFX requires a SpriteRenderer");
                var sprites=new Sprite[3];
                for(int i=0;i<3;i++)
                {
                    string path="Assets/FightDemo/Arts/敵人/Goblin_King/jump/jump_VFX_"+(i+2)+".png";
                    sprites[i]=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
                    if(!sprites[i]) throw new Exception("Sprite missing: "+path);
                }
                renderer.sprite=sprites[0];renderer.sortingOrder=Math.Max(100,renderer.sortingOrder);
                var animator=copy.GetComponent<FightCharacterCombatAnimator>()??copy.AddComponent<FightCharacterCombatAnimator>();
                Set(animator,"targetRenderer",renderer);
                var sequence=animator.FindSequence("Howl")??animator.AddCustomSequence("Howl");
                sequence.Configure(FightCharacterCombatAnimator.CombatAnimation.NormalAttack,sprites,16,-1,-1,-1);
                sequence.ConfigureFrameBeats(new[]{.33f,.33f,.34f});
                if(!copy.GetComponent<FightBeatSpriteEffect>())copy.AddComponent<FightBeatSpriteEffect>();
                var settings=copy.GetComponent<FightEffectSettings>()??copy.AddComponent<FightEffectSettings>();
                settings.spawnOrientation=FightEffectSettings.SpawnOrientation.Prefab;
                effectPrefab=PrefabUtility.SaveAsPrefabAsset(copy,effectPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
            string kingPath="Assets/FightDemo/Prefabs/ArtBattle/Goblin_King.prefab";
            var king=PrefabUtility.LoadPrefabContents(kingPath);
            string report="";
            try
            {
                var animator=king.GetComponentInChildren<FightCharacterCombatAnimator>(true);
                var so=new SerializedObject(animator);var sequences=so.FindProperty("sequences");
                SerializedProperty landing=null;
                for(int i=0;i<sequences.arraySize;i++)
                    if(sequences.GetArrayElementAtIndex(i).FindPropertyRelative("animationName").stringValue=="Landing") landing=sequences.GetArrayElementAtIndex(i);
                if(landing==null)throw new Exception("Landing missing");
                var frames=landing.FindPropertyRelative("frameEffects");
                while(frames.arraySize<4) { int at=frames.arraySize;frames.InsertArrayElementAtIndex(at);var f=frames.GetArrayElementAtIndex(at);f.FindPropertyRelative("effects").ClearArray();f.FindPropertyRelative("playAttackEffect").boolValue=false;f.FindPropertyRelative("playWarning").boolValue=false; }
                var effects=frames.GetArrayElementAtIndex(3).FindPropertyRelative("effects");
                bool exists=false;for(int i=0;i<effects.arraySize;i++)if(effects.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue==effectPrefab)exists=true;
                if(!exists)
                {
                    int at=effects.arraySize;effects.InsertArrayElementAtIndex(at);var e=effects.GetArrayElementAtIndex(at);
                    e.FindPropertyRelative("prefab").objectReferenceValue=effectPrefab;
                    e.FindPropertyRelative("soundEventPath").stringValue="";
                    e.FindPropertyRelative("location").enumValueIndex=0;e.FindPropertyRelative("selfAnchor").objectReferenceValue=null;
                    e.FindPropertyRelative("positionOffset").vector3Value=Vector3.zero;e.FindPropertyRelative("rotationOffset").vector3Value=Vector3.zero;
                    e.FindPropertyRelative("followAnchor").boolValue=false;e.FindPropertyRelative("limitLifetime").boolValue=false;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                foreach(string name in new[]{"Jump","Flying","Landing"})
                {
                    var seq=animator.FindSequence(name);if(seq==null||seq.Frames.Count==0)throw new Exception(name+" missing");
                    double beats=0;for(int i=0;i<seq.Frames.Count;i++)beats+=seq.GetFrameBeats(i);
                    report+=name+": "+seq.Frames.Count+" frames, "+beats+" beats. ";
                }
                PrefabUtility.SaveAsPrefabAsset(king,kingPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(king); }
            var planner=all.Select(t=>t.GetComponent<FightLevelPlanner>()).First(x=>x);
            planner.SelectedStage.BossEntrance.doorExplosionEnabled=false;
            EditorUtility.SetDirty(planner.SelectedStage);
            source.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/FightBossFlightSetup.result","PASS: howl source copied, 0.33/0.33/0.34 beat animation; Landing frame 4 attached; door explosion disabled. "+report);
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
