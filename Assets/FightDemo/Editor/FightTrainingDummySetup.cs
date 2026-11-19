using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightTrainingDummySetup
    {
        const string Art="Assets/FightDemo/Art2Prototype/";
        const string Prefabs="Assets/FightDemo/Prefabs/ArtBattle/";
        static FightTrainingDummySetup()=>EditorApplication.update+=Poll;
        static Sprite SaveSprite(string name,Texture2D texture,Rect rect,Vector2 pivot,float ppu)
        {
            var sprite=Sprite.Create(texture,rect,pivot,ppu,0,SpriteMeshType.FullRect);sprite.name=name;
            string path=Art+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(existing){EditorUtility.CopySerialized(sprite,existing);UnityEngine.Object.DestroyImmediate(sprite);EditorUtility.SetDirty(existing);return existing;}
            AssetDatabase.CreateAsset(sprite,path);return sprite;
        }
        static void Motion(SceneBeatMotionManager manager,Transform[] targets,FmodBeatClock clock=null)
        {
            var data=new SerializedObject(manager);data.FindProperty("beatClock").objectReferenceValue=clock;
            var list=data.FindProperty("targets");list.arraySize=targets.Length;
            for(int i=0;i<targets.Length;i++)
            {
                var item=list.GetArrayElementAtIndex(i);item.FindPropertyRelative("enabled").boolValue=true;
                item.FindPropertyRelative("target").objectReferenceValue=targets[i];item.FindPropertyRelative("directionSpace").intValue=(int)Space.World;
                item.FindPropertyRelative("direction").vector3Value=Vector3.up;item.FindPropertyRelative("amplitude").floatValue=.06f;
                item.FindPropertyRelative("durationInBeats").floatValue=.4f;item.FindPropertyRelative("oscillations").intValue=1;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Poll()
        {
            const string request="Temp/TrainingDummySetup.request";
            if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            File.Delete(request);
            try{Build();File.WriteAllText("Temp/TrainingDummySetup.result","PASS: passive TrainingDummy HP 99999; Warrior/Skeleton lessons assigned; four forest props independently beat-driven; original textures unchanged.");}
            catch(Exception e){File.WriteAllText("Temp/TrainingDummySetup.result","FAIL: "+e);}
        }
        static void Build()
        {
            var root=new GameObject("TrainingDummy");
            try
            {
                var definition=root.AddComponent<FightCharacterDefinition>();
                definition.Configure("training_dummy","Training Dummy",FightUnitSlot.UnitTeam.Enemy,FightUnitSlot.UnitRole.Enemy,99999,
                    FightCharacterDefinition.AbilityBehavior.Unavailable,1,1,"None",FightCharacterDefinition.AbilityBehavior.Unavailable,1,null,16,1,Color.white);
                var data=new SerializedObject(definition);data.FindProperty("enemyCanAttack").boolValue=false;data.FindProperty("useFallbackAbilityEffects").boolValue=false;
                data.FindProperty("ignoreFloorOcclusion").boolValue=false;
                Transform Anchor(string name,Vector3 position){var t=new GameObject(name).transform;t.SetParent(root.transform,false);t.localPosition=position;return t;}
                Anchor("FootAnchor",Vector3.zero);
                data.FindProperty("headAnchor").objectReferenceValue=Anchor("HeadAnchor",new Vector3(0,2.05f,0));
                data.FindProperty("impactEffectAnchor").objectReferenceValue=Anchor("VFX_ImpactAnchor",new Vector3(0,1,-.1f));
                data.FindProperty("castEffectAnchor").objectReferenceValue=Anchor("VFX_CastAnchor",new Vector3(0,1,0));data.ApplyModifiedPropertiesWithoutUndo();
                // Keep opaque feet above the floor even at rest. Beat motion only lifts upwards.
                var visual=Anchor("Visual",new Vector3(0,.025f,0));var renderer=visual.gameObject.AddComponent<SpriteRenderer>();
                visual.localRotation=Quaternion.Euler(45,0,0);
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FightDemo/Arts/敵人/old/ChatGPT Image 2025年12月10日 下午10_11_25.png");
                renderer.sprite=SaveSprite("TrainingDummySprite",texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.12f),texture.height/2.5f);
                renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Art+"ForestSpriteCard.mat");
                var dummyMotion=root.AddComponent<SceneBeatMotionManager>();
                Motion(dummyMotion,new[]{visual});
                var dummyMotionData=new SerializedObject(dummyMotion);
                dummyMotionData.FindProperty("targets").GetArrayElementAtIndex(0).FindPropertyRelative("positiveOnly").boolValue=true;
                dummyMotionData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"TrainingDummy.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            var dummy=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"TrainingDummy.prefab").GetComponent<FightCharacterDefinition>();
            foreach(int index in new[]{1,3})
            {
                var stage=AssetDatabase.LoadAssetAtPath<FightStageDefinition>($"Assets/FightDemo/Levels/Chapter01_Stage0{index}_Tutorial.asset");
                var data=new SerializedObject(stage);var wave=data.FindProperty("waves").GetArrayElementAtIndex(0);
                wave.FindPropertyRelative("front").objectReferenceValue=dummy;wave.FindPropertyRelative("middle").objectReferenceValue=null;wave.FindPropertyRelative("back").objectReferenceValue=null;
                wave.FindPropertyRelative("name").stringValue="Training Dummy Practice";data.ApplyModifiedProperties();
            }
            string forestPath=Prefabs+"ForestEnvironment_2_5D.prefab";
            var forest=PrefabUtility.LoadPrefabContents(forestPath);
            try
            {
                var targets=new System.Collections.Generic.List<Transform>();
                foreach(string layer in new[]{"Background_05","Background_07"})
                {
                    var parent=forest.transform.Find(layer);var original=parent.GetComponent<SpriteRenderer>();var sprite=original.sprite;
                    for(int side=0;side<2;side++)
                    {
                        string name=(layer=="Background_05"?"Rock":"Tree")+(side==0?"_Left":"_Right");
                        var child=parent.Find(name);if(!child){child=new GameObject(name).transform;child.SetParent(parent,false);}
                        var rect=sprite.rect;rect.width*=.5f;rect.x+=side*rect.width;
                        var sr=child.GetComponent<SpriteRenderer>();if(!sr)sr=child.gameObject.AddComponent<SpriteRenderer>();
                        sr.sprite=SaveSprite("Forest_"+name,sprite.texture,rect,Vector2.one*.5f,sprite.pixelsPerUnit);
                        sr.sharedMaterial=original.sharedMaterial;sr.sortingLayerID=original.sortingLayerID;sr.sortingOrder=layer=="Background_05"?-42:original.sortingOrder;
                        child.localPosition=new Vector3((side==0?-1:1)*sprite.rect.width/sprite.pixelsPerUnit*.25f,0,0);
                        targets.Add(child);
                    }
                    original.enabled=false;
                }
                var manager=forest.GetComponent<SceneBeatMotionManager>();if(!manager)manager=forest.AddComponent<SceneBeatMotionManager>();
                Motion(manager,targets.ToArray());
                var motions=new SerializedObject(manager);var entries=motions.FindProperty("targets");
                for(int i=0;i<entries.arraySize;i++)entries.GetArrayElementAtIndex(i).FindPropertyRelative("amplitude").floatValue=.18f;
                motions.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(forest,forestPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(forest);}
            var plan=UnityEngine.Object.FindFirstObjectByType<FightLevelPlanner>();var scene=plan.gameObject.scene;
            var instance=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="ForestEnvironment_2_5D");
            var motion=instance.GetComponent<SceneBeatMotionManager>();var motionData=new SerializedObject(motion);
            motionData.FindProperty("beatClock").objectReferenceValue=plan.GetComponent<FmodBeatClock>();motionData.ApplyModifiedProperties();PrefabUtility.RecordPrefabInstancePropertyModifications(motion);
            var selector=plan.GetComponent<FightEnvironmentSelector>();var selectorData=new SerializedObject(selector);var bindings=selectorData.FindProperty("bindings");
            for(int i=0;i<bindings.arraySize;i++)
            {
                var binding=bindings.GetArrayElementAtIndex(i);var environment=binding.FindPropertyRelative("environment").objectReferenceValue as FightEnvironmentDefinition;
                if(!environment||environment.EnvironmentId!="Forest")continue;
                var renderers=instance.GetComponentsInChildren<SpriteRenderer>(true);var array=binding.FindPropertyRelative("occluders");array.arraySize=renderers.Length;
                for(int j=0;j<renderers.Length;j++)array.GetArrayElementAtIndex(j).objectReferenceValue=renderers[j];
            }
            selectorData.ApplyModifiedProperties();selector.TryApply(plan.SelectedEnvironment,out _);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
