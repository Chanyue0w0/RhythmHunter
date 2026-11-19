using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using RhythmHunter.FightDemo;
namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightPhaseTwoFireValidation
    {
        const string SetupRequest="Temp/PhaseTwoFireSetup.request", PlayRequest="Temp/PhaseTwoFirePlay.request";
        static double deadline, readyAt, shotAt; static bool requested;
        static FightPhaseTwoFireValidation()=>EditorApplication.update+=Poll;
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(File.Exists("Temp/PhaseTwoFireOrder.request")&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete("Temp/PhaseTwoFireOrder.request");
                try
                {
                    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                    var fire=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FightBossPhaseTwoFire>(true)).Single();
                    var data=new SerializedObject(fire);
                    data.FindProperty("scaleRange").vector2Value=new Vector2(.45f,.7f);
                    data.FindProperty("fadeInSeconds").floatValue=1.2f;
                    data.FindProperty("sortingOrdersBehind").intValue=1;
                    data.ApplyModifiedProperties();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                    File.WriteAllText("Temp/PhaseTwoFireOrder.result","PASS: start scale .45, fade 1.2 seconds, stable within-group ordering.");
                }catch(Exception e){File.WriteAllText("Temp/PhaseTwoFireOrder.result","FAIL: "+e);}
            }
            if(File.Exists("Temp/PhaseTwoSizeTune.request")&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete("Temp/PhaseTwoSizeTune.request");
                try
                {
                    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                    var fight=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FightCombatController>(true)).Single();
                    var fire=new SerializedObject(fight.GetComponent<FightBossPhaseTwoFire>());
                    fire.FindProperty("scaleRange").vector2Value=new Vector2(.35f,.7f);
                    fire.FindProperty("offset").vector3Value=new Vector3(0,0,.1f);
                    fire.FindProperty("flameBaseLocalPoint").vector3Value=Vector3.zero;
                    fire.ApplyModifiedProperties();
                    var data=new SerializedObject(fight.GetComponent<FightBossPhaseTwoTransition>());
                    var targets=data.FindProperty("heroDestinations");
                    for(int j=0;j<targets.arraySize;j++)
                    {
                        var t=(Transform)targets.GetArrayElementAtIndex(j).objectReferenceValue;
                        Undo.RecordObject(t,"Lower heroes a little further");var p=t.localPosition;p.z=-4.6272853f;t.localPosition=p;EditorUtility.SetDirty(t);
                    }
                    const string path="Assets/FightDemo/Prefabs/VFX/BossRythmSlash.prefab";
                    var root=PrefabUtility.LoadPrefabContents(path);
                    try{root.transform.localScale=Vector3.one*.5f;PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                    File.WriteAllText("Temp/PhaseTwoSizeTune.result","PASS: fire .35~.7 pinned to feet; projectile scale .5; heroes further down by .3 world units.");
                }catch(Exception e){File.WriteAllText("Temp/PhaseTwoSizeTune.result","FAIL: "+e);}
            }
            if(File.Exists("Temp/PhaseTwoPresentationTune.request")&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete("Temp/PhaseTwoPresentationTune.request");
                try
                {
                    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                    var fight=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FightCombatController>(true)).Single();
                    var fire=new SerializedObject(fight.GetComponent<FightBossPhaseTwoFire>());
                    fire.FindProperty("offset").vector3Value=new Vector3(0,0,.1f);
                    fire.FindProperty("fadeInSeconds").floatValue=3;
                    fire.FindProperty("opacityRange").vector2Value=new Vector2(.12f,.7f);
                    fire.FindProperty("scaleRange").vector2Value=new Vector2(.75f,1.5f);
                    fire.ApplyModifiedProperties();
                    var transition=new SerializedObject(fight.GetComponent<FightBossPhaseTwoTransition>());
                    var targets=transition.FindProperty("heroDestinations");
                    for(int i=0;i<targets.arraySize;i++)
                    {
                        var t=(Transform)targets.GetArrayElementAtIndex(i).objectReferenceValue;
                        Undo.RecordObject(t,"Lower phase two hero destinations");var position=t.localPosition;position.z=-4.3272853f;t.localPosition=position;EditorUtility.SetDirty(t);
                    }
                    var focus=fight.GetComponent<RhythmHunter.FightDemo.Art2.PhaseTwoFocusBlur>();
                    if(!focus)focus=Undo.AddComponent<RhythmHunter.FightDemo.Art2.PhaseTwoFocusBlur>(fight.gameObject);
                    var data=new SerializedObject(focus);data.FindProperty("fight").objectReferenceValue=fight;
                    data.FindProperty("transition").objectReferenceValue=fight.GetComponent<FightBossPhaseTwoTransition>();
                    data.FindProperty("targetCamera").objectReferenceValue=fire.FindProperty("view").objectReferenceValue;
                    data.FindProperty("shader").objectReferenceValue=Shader.Find("Hidden/RhythmHunter/PhaseTwoFocusBlur");
                    data.ApplyModifiedProperties();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                    File.WriteAllText("Temp/PhaseTwoPresentationTune.result","PASS: lowered flame, 3-second fade, lower hero destinations, trapezoid blur configured in live scene.");
                }catch(Exception e){File.WriteAllText("Temp/PhaseTwoPresentationTune.result","FAIL: "+e);}
            }
            if(File.Exists(SetupRequest)&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(SetupRequest);
                try
                {
                    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                    if(!scene.IsValid()||!scene.isLoaded)throw new Exception("Open Art2 first.");
                    var fight=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FightCombatController>(true)).Single();
                    var effect=fight.GetComponent<FightBossPhaseTwoFire>();if(!effect)effect=Undo.AddComponent<FightBossPhaseTwoFire>(fight.gameObject);
                    var data=new SerializedObject(effect);
                    data.FindProperty("fight").objectReferenceValue=fight;
                    data.FindProperty("view").objectReferenceValue=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First(c=>c.name=="Art2_TestCamera");
                    data.FindProperty("firePrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/VFX/FiredEffect.prefab");
                    data.FindProperty("sortingOrdersBehind").intValue=0;
                    data.FindProperty("offset").vector3Value=new Vector3(0,.15f,.1f);
                    data.FindProperty("scaleRange").vector2Value=new Vector2(.75f,1.5f);
                    data.FindProperty("opacityRange").vector2Value=new Vector2(.25f,.85f);
                    data.ApplyModifiedProperties();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                    File.WriteAllText("Temp/PhaseTwoFireSetup.result","PASS: attached and saved live Art2 scene; camera, prefab and controller references assigned.");
                }catch(Exception e){File.WriteAllText("Temp/PhaseTwoFireSetup.result","FAIL: "+e);}
            }
            if(!File.Exists(PlayRequest))return;
            if(!EditorApplication.isPlaying){if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.isPlaying=true;return;}
            try
            {
                Application.runInBackground=true;
                if(deadline==0)deadline=EditorApplication.timeSinceStartup+90;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timed out waiting for actual phase-two fire.");
                var fight=UnityEngine.Object.FindFirstObjectByType<FightCombatController>();if(!fight||fight.AwaitingBattleStart)return;
                if(!requested){fight.GetComponent<FightBossStageDebug>().RequestPhaseTwoSkip();requested=true;return;}
                var fire=GameObject.Find("BossPhaseTwo_FiredEffect");
                if(!fire||!fight.GetComponent<FightBossPhaseTwoTransition>().Ready)return;
                if(readyAt==0)readyAt=EditorApplication.timeSinceStartup;
                if(EditorApplication.timeSinceStartup-readyAt<3 || fight.GetComponent<FightBossRhythmBattle>().LaunchedNotes<3)return;
                if(shotAt==0)shotAt=EditorApplication.timeSinceStartup;
                if(EditorApplication.timeSinceStartup-shotAt<.28)return;
                var ps=fire.GetComponentsInChildren<ParticleSystem>();if(ps.Any(p=>p.particleCount==0))return;
                var effect=fight.GetComponent<FightBossPhaseTwoFire>();
                var boss=fight.RosterManager.ActiveEnemies.First(e=>e&&e.BossPhaseOneProtected);
                var group=boss.ActorRoot.GetComponent<UnityEngine.Rendering.SortingGroup>();
                if(!group||!fire.transform.IsChildOf(group.transform))throw new Exception("Fire not inside Boss sorting group");
                foreach(var p in ps)if(p.GetComponent<ParticleSystemRenderer>().sortingOrder>=boss.CombatAnimator.TargetRenderer.sortingOrder)throw new Exception("Fire not behind Boss");
                var info="PASS: stable Boss/fire sorting group; phase-two fire spawned; particle counts="+string.Join(",",ps.Select(p=>p.particleCount))+"; scale="+fire.transform.localScale+"; position="+fire.transform.position+"; boss="+boss.ActorRoot.position;
                foreach(var p in ps)
                {
                    var r=p.GetComponent<ParticleSystemRenderer>();
                    var live=new ParticleSystem.Particle[p.particleCount];p.GetParticles(live);
                    info+="\n"+p.name+" bounds="+r.bounds+" order="+r.sortingOrder+" layer="+r.sortingLayerID+" queue="+r.sharedMaterial.renderQueue+" shader="+r.sharedMaterial.shader.name+" enabled="+r.enabled+" alpha="+p.colorOverLifetime.color.Evaluate(.5f).a+" color="+(live.Length>0?live[0].GetCurrentColor(p).ToString():"empty");
                }
                info+="\nBoss bounds="+boss.CombatAnimator.TargetRenderer.bounds+" order="+boss.CombatAnimator.TargetRenderer.sortingOrder;
                ScreenCapture.CaptureScreenshot("Temp/PhaseTwoFirePreview.png");
                Finish(info);
            }catch(Exception e){Finish("FAIL: "+e);}
        }
        static void Finish(string result){File.WriteAllText("Temp/PhaseTwoFirePlay.result",result);File.Delete(PlayRequest);deadline=readyAt=shotAt=0;requested=false;EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;}
    }
}
