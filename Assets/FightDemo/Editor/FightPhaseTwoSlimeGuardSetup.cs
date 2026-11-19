using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmHunter.FightDemo;
namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightPhaseTwoSlimeGuardSetup
    {
        static FightPhaseTwoSlimeGuardSetup()=>EditorApplication.update+=Poll;
        static void Poll()
        {
            const string request="Temp/PhaseTwoSlimeGuard.request";
            if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isUpdating||EditorApplication.isCompiling)return;
            File.Delete(request);
            try
            {
                var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                var battle=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FightBossRhythmBattle>(true)).Single();
                var mage=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/ArtBattle/Mage.prefab");
                var definition=mage.GetComponent<FightCharacterDefinition>();
                var source=(GameObject)new SerializedObject(definition).FindProperty("normalAbilityEffectPrefab").objectReferenceValue;
                if(!source)throw new Exception("Mage has no normal ability effect");
                const string path="Assets/FightDemo/Prefabs/VFX/BossPhaseTwo_SlimeGuard.prefab";
                var root=new GameObject("BossPhaseTwo_SlimeGuard");GameObject prefab;
                try
                {
                    root.AddComponent<FightBossRhythmVisual>();
                    var child=(GameObject)PrefabUtility.InstantiatePrefab(source);child.transform.SetParent(root.transform,false);
                    // The source has a phase-one emitter offset (-.4,-2,0). Only this
                    // nested instance uses a body-centred emitter for the back-facing party.
                    var emitter=child.transform.Find("VFX_Shield_01_Color_Loop_Static");
                    if(!emitter)throw new Exception("Missing shield emitter anchor");
                    emitter.localPosition=Vector3.zero;PrefabUtility.RecordPrefabInstancePropertyModifications(emitter);
                    foreach(var presentation in child.GetComponentsInChildren<RhythmHunter.FightDemo.Art2.Art2EffectPresentation>(true))
                    {
                        // Particle bounds expand during playback; ground-lifting those bounds
                        // would move this body-attached shield away from its owner.
                        var settings=new SerializedObject(presentation);
                        settings.FindProperty("keepAboveGround").boolValue=false;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(presentation);
                    }
                    prefab=PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
                var data=new SerializedObject(battle);data.FindProperty("guardPrefab").objectReferenceValue=prefab.GetComponent<FightBossRhythmVisual>();
                data.FindProperty("guardAtHeroCastAnchor").boolValue=true;data.ApplyModifiedProperties();
                data.Update();data.FindProperty("guardAtHeroBodyCenter").boolValue=true;data.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                var nested=prefab.GetComponentsInChildren<ParticleSystem>(true);
                if(nested.Length==0)throw new Exception("Missing shield particles");
                File.WriteAllText("Temp/PhaseTwoSlimeGuard.result","PASS: pooled phase-two guard wraps "+AssetDatabase.GetAssetPath(source)+"; cast anchor enabled; particles="+nested.Length);
            }
            catch(Exception e){File.WriteAllText("Temp/PhaseTwoSlimeGuard.result","FAIL: "+e);}
        }
    }
}
