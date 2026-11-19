using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightBossGemFinaleSetup
    {
        static FightBossGemFinaleSetup()=>EditorApplication.update+=Poll;
        static void Poll()
        {
            const string energy="Temp/GemFinaleEnergy.request";
            if(File.Exists(energy)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)
            {
                File.Delete(energy);
                try
                {
                    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                    var finale=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FightBossGemFinale>(true)).Single();
                    var data=new SerializedObject(finale);
                    data.FindProperty("chargingShellSize").vector3Value=new Vector3(2.1f,2.8f,1.4f);
                    data.FindProperty("chargingLinkShader").objectReferenceValue=Shader.Find("RhythmHunter/Art2/GemEnergyLink");
                    data.FindProperty("chargingShellShader").objectReferenceValue=Shader.Find("RhythmHunter/Art2/GemChargingEnergy");
                    if(!data.FindProperty("chargingShellShader").objectReferenceValue)throw new Exception("Missing shell shader");
                    data.ApplyModifiedProperties();
                    if(FightBossGemFinale.CalculateBeamDamage(0,10,20,99999)!=0||FightBossGemFinale.CalculateBeamDamage(20,10,20,99999)!=200||FightBossGemFinale.CalculateBeamDamage(21,10,20,99999)!=99999)throw new Exception("Damage threshold");
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                    File.WriteAllText("Temp/GemFinaleEnergy.result","PASS: 0/20/21 presses = 0/200/99999; stronger bounce, charge shell assigned.");
                }catch(Exception e){File.WriteAllText("Temp/GemFinaleEnergy.result","FAIL: "+e);}
                return;
            }
            const string formation="Temp/GemFinaleFormation.request";
            if(File.Exists(formation)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)
            {
                File.Delete(formation);
                try
                {
                    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                    var finale=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FightBossGemFinale>(true)).Single();
                    var data=new SerializedObject(finale);
                    var offset=data.FindProperty("gemOffset").vector3Value;offset.z=2.2f;
                    data.FindProperty("gemOffset").vector3Value=offset;
                    data.FindProperty("sideHeroGatherOffset").vector3Value=new Vector3(0,0,1.2f);
                    data.ApplyModifiedProperties();
                    var speed=new SerializedObject(finale.GetComponent<FightPhaseTwoSpeedLines>());
                    speed.FindProperty("lowIntensityCount").intValue=42;
                    speed.FindProperty("highIntensityCount").intValue=128;
                    speed.FindProperty("countGrowthMultiplier").floatValue=1.6f;
                    speed.FindProperty("countFadeBeats").floatValue=.3f;
                    speed.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                    File.WriteAllText("Temp/GemFinaleFormation.result","PASS: density 42-128, growth 1.6, fade .3; side heroes +Z1.2; gem +Z2.2.");
                }catch(Exception e){File.WriteAllText("Temp/GemFinaleFormation.result","FAIL: "+e);}
                return;
            }
            const string digits="Temp/GemFinaleDigits.request";
            if(File.Exists(digits)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)
            {
                File.Delete(digits);
                try
                {
                    for(int i=0;i<10;i++)
                    {
                        string path=$"Assets/FightDemo/Prefabs/VFX/DamageNumbers/number_red/{i}.prefab";
                        string source=$"Assets/FightDemo/Arts/UI/戰鬥場景/數字/number_red/number_red_{i}.png";
                        var sprite=AssetDatabase.LoadAllAssetsAtPath(source).OfType<Sprite>().First();
                        var root=PrefabUtility.LoadPrefabContents(path);
                        try{root.GetComponentInChildren<SpriteRenderer>(true).sprite=sprite;PrefabUtility.SaveAsPrefabAsset(root,path);}
                        finally{PrefabUtility.UnloadPrefabContents(root);}
                        var saved=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<SpriteRenderer>(true).sprite;
                        if(AssetDatabase.GetAssetPath(saved)!=source)throw new Exception("Wrong digit source: "+i);
                    }
                    AssetDatabase.SaveAssets();
                    File.WriteAllText("Temp/GemFinaleDigits.result","PASS: all ten red digit prefabs reference new artwork outside old.");
                }catch(Exception e){File.WriteAllText("Temp/GemFinaleDigits.result","FAIL: "+e);}
                return;
            }
            const string tune="Temp/GemFinalePresentation.request";
            if(File.Exists(tune)&&EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying=false;
                return;
            }
            if(File.Exists(tune)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)
            {
                try
                {
                    File.Delete(tune);
                    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                    var finale=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FightBossGemFinale>(true)).Single();
                    var data=new SerializedObject(finale);
                    data.FindProperty("gemOffset").vector3Value=new Vector3(0,1.3f,1.4f);
                    data.FindProperty("gemEnterFromScreenTop").boolValue=true;
                    data.FindProperty("gemEntryViewportMargin").floatValue=.2f;
                    data.FindProperty("gemEntryOffset").vector3Value=new Vector3(0,0,7);
                    data.FindProperty("gemEntryBeats").floatValue=.45f;
                    data.FindProperty("promptSprite").objectReferenceValue=AssetDatabase.LoadAllAssetsAtPath("Assets/FightDemo/Arts/UI/old/ChatGPT Image 2025年12月9日 上午03_47_03.png").OfType<Sprite>().First();
                    data.FindProperty("promptHeight").floatValue=.65f;
                    data.FindProperty("promptOffset").vector3Value=new Vector3(0,1,0);
                    data.FindProperty("beamScale").floatValue=1.35f;
                    data.FindProperty("beamVolumeShader").objectReferenceValue=Shader.Find("RhythmHunter/Art2/GemBeamVolume");
                    data.FindProperty("beamVolumeEnabled").boolValue=true;
                    data.ApplyModifiedProperties();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                    File.WriteAllText("Temp/GemFinalePresentation.result","PASS: +Z gem arrival; A artwork; 1.35x beam with cylindrical volume and Boss overlay sorting.");
                }catch(Exception e){File.WriteAllText("Temp/GemFinalePresentation.result","FAIL: "+e);}
                return;
            }
            const string request="Temp/GemFinaleSetup.request";
            if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            try{File.Delete(request);}catch(IOException){return;}
            try
            {
                var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                if(!scene.isLoaded)throw new Exception("Open Art2 first.");
                var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
                var fight=all.Select(t=>t.GetComponent<FightCombatController>()).First(x=>x);
                var presenter=all.Select(t=>t.GetComponent<FightScenePresenter>()).First(x=>x);
                var presenterData=new SerializedObject(presenter);
                var clock=all.Select(t=>t.GetComponent<FmodBeatClock>()).First(x=>x);
                double tempo=FightOpeningTempoImporter.ReadTempo(clock.MusicEventPath);
                var markers=FightOpeningTempoImporter.ReadMarkers(clock.MusicEventPath);
                var start=markers.First(x=>x.name=="Normal_End").positionMs;
                var end=markers.First(x=>x.name=="Boss_End").positionMs;
                if(tempo<=0)throw new Exception("Invalid tempo");
                double endBeat=(end-start)*tempo/60000;
                if(endBeat<104)throw new Exception("Boss_End precedes beat 104: "+endBeat);
                var chart=AssetDatabase.LoadAssetAtPath<FightBossRhythmChart>("Assets/FightDemo/Levels/GoblinKing_PhaseTwo_Chart.asset");
                int removed=chart.notes.RemoveAll(n=>n.useFreeTime?(n.timeSeconds*1000+n.offsetMs)/(60000/tempo)>=95:n.arrivalBeat>=95);
                if(removed>0)EditorUtility.SetDirty(chart);
                const string basePath="Assets/FightDemo/Prefabs/VFX/";
                var ray=all.FirstOrDefault(t=>t.name=="GemUltRay");
                var rayPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(basePath+"GemUltRay.prefab");
                if(ray)
                {
                    var clone=UnityEngine.Object.Instantiate(ray.gameObject);clone.name="GemUltRay";clone.SetActive(true);
                    try
                    {
                        clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                        foreach(var e in clone.GetComponentsInChildren<CartoonFX.CFXR_Effect>(true))e.clearBehavior=CartoonFX.CFXR_Effect.ClearBehavior.None;
                        foreach(var ps in clone.GetComponentsInChildren<ParticleSystem>(true)){var main=ps.main;main.stopAction=ParticleSystemStopAction.None;}
                        rayPrefab=PrefabUtility.SaveAsPrefabAsset(clone,basePath+"GemUltRay.prefab");
                    }finally{UnityEngine.Object.DestroyImmediate(clone);}
                    ray.gameObject.SetActive(false); // Keep the editable scene sample; don't render a second beam in battle.
                }
                if(!rayPrefab)throw new Exception("Missing scene GemUltRay");
                var array=presenterData.FindProperty("stoneReadyFrames");
                var frames=Enumerable.Range(0,array.arraySize).Select(i=>(Sprite)array.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
                if(frames.Length==0||frames.Any(s=>!s))throw new Exception("Missing ready gem sprites");
                var gem=new GameObject("FinaleFullGem");GameObject gemPrefab;
                try
                {
                    var sprite=gem.AddComponent<SpriteRenderer>();sprite.sprite=frames[0];sprite.sortingOrder=50;
                    sprite.shadowCastingMode=ShadowCastingMode.Off;sprite.receiveShadows=false;
                    gem.AddComponent<SceneBeatSpriteAnimator>().Configure(null,frames,1);
                    gemPrefab=PrefabUtility.SaveAsPrefabAsset(gem,basePath+"FinaleFullGem.prefab");
                }finally{UnityEngine.Object.DestroyImmediate(gem);}
                var finale=fight.GetComponent<FightBossGemFinale>()??fight.gameObject.AddComponent<FightBossGemFinale>();
                var data=new SerializedObject(finale);
                data.FindProperty("fight").objectReferenceValue=fight;
                data.FindProperty("rhythm").objectReferenceValue=fight.GetComponent<FightBossRhythmBattle>();
                data.FindProperty("view").objectReferenceValue=all.Select(t=>t.GetComponent<Camera>()).First(c=>c&&c.name=="Art2_TestCamera");
                data.FindProperty("gemPrefab").objectReferenceValue=gemPrefab;
                data.FindProperty("beamPrefab").objectReferenceValue=rayPrefab;
                data.FindProperty("hitEffectPrefab").objectReferenceValue=presenterData.FindProperty("stoneSuccessEffectPrefab").objectReferenceValue;
                var frameData=data.FindProperty("gemFrames");frameData.arraySize=frames.Length;
                for(int i=0;i<frames.Length;i++)frameData.GetArrayElementAtIndex(i).objectReferenceValue=frames[i];
                data.ApplyModifiedProperties();
                // Let already emitted streaks leave while the finale fades them down.
                var speed=new SerializedObject(fight.GetComponent<FightPhaseTwoSpeedLines>());
                speed.FindProperty("fadeOutSeconds").floatValue=1;
                speed.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
                File.WriteAllText("Temp/GemFinaleSetup.result",$"PASS: endBeat={endBeat:F4}; tempo={tempo}; beam window={(endBeat-104)*60/tempo:F3}s; removed notes={removed}; gem/ray prefab and live scene saved.");
            }catch(Exception e){File.WriteAllText("Temp/GemFinaleSetup.result","FAIL: "+e);}
        }
    }
}
