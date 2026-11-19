using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using RhythmHunter.FightDemo;
using RhythmHunter.FightDemo.Art2;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightForestEnvironmentSetup
    {
        const string Folder="Assets/FightDemo/Art2Prototype";
        const string PrefabPath="Assets/FightDemo/Prefabs/ArtBattle/ForestEnvironment_2_5D.prefab";
        static FightForestEnvironmentSetup()=>EditorApplication.update+=Poll;
        static Material MaterialAsset(string name, string shader)
        {
            var path=Folder+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(material,path);}
            return material;
        }
        static Rect OriginalCrop(SpriteRenderer original)
        {
            var rect=original.sprite.rect;
            // The two rock groups use their authored canvas spacing, not the scrolling tile crop.
            if(original.name=="Background_05")return rect;
            var scroller=original.GetComponent<LoopingBackgroundScroller>();
            if(scroller)
            {
                var data=new SerializedObject(scroller);
                int left=data.FindProperty("cropLeftPixels").intValue,right=data.FindProperty("cropRightPixels").intValue;
                rect.x+=left;rect.width-=left+right;
            }
            return rect;
        }
        static Sprite CroppedArtwork(SpriteRenderer original)
        {
            string path=Folder+"/Forest_"+original.name+".asset";
            var created=Sprite.Create(original.sprite.texture,OriginalCrop(original),Vector2.one*.5f,original.sprite.pixelsPerUnit,0,SpriteMeshType.FullRect);
            created.name="Forest_"+original.name;
            var saved=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(saved){EditorUtility.CopySerialized(created,saved);UnityEngine.Object.DestroyImmediate(created);EditorUtility.SetDirty(saved);return saved;}
            AssetDatabase.CreateAsset(created,path);return created;
        }
        static Mesh RoadMesh(Camera view)
        {
            // Bake the original road composition onto XZ rather than stretching a small texture crop.
            const int rows=32,columns=8;
            var points=new Vector3[(rows+1)*(columns+1)];var uv=new Vector2[points.Length];var triangles=new int[rows*columns*6];
            var plane=new Plane(Vector3.up,Vector3.zero);int tri=0;
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {
                float u=(float)x/columns,v=(float)y/rows*(400f/720f);
                var ray=view.ViewportPointToRay(new Vector3(u,v,0));
                if(!plane.Raycast(ray,out float distance))throw new Exception("Forest road camera does not face ground");
                int at=y*(columns+1)+x;points[at]=ray.GetPoint(distance);uv[at]=new Vector2(u,v);
                if(x==columns||y==rows)continue;
                triangles[tri++]=at;triangles[tri++]=at+columns+1;triangles[tri++]=at+1;
                triangles[tri++]=at+1;triangles[tri++]=at+columns+1;triangles[tri++]=at+columns+2;
            }
            var mesh=new Mesh{name="ForestRoad_ReferenceComposition",vertices=points,uv=uv,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Folder+"/ForestRoad.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static void Poll()
        {
            const string request="Temp/ForestEnvironmentSetup.request";
            if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            File.Delete(request);
            try{Build();File.WriteAllText("Temp/ForestEnvironmentSetup.result","PASS: Forest 2.5D prefab, lighting/floor bindings, Tutorial Chapter01 Stage01 selected and previewed; Cave round-trip preserved.");}
            catch(Exception e){File.WriteAllText("Temp/ForestEnvironmentSetup.result","FAIL: "+e);}
        }
        static void Build()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var planner=all.Select(t=>t.GetComponent<FightLevelPlanner>()).Single(p=>p);
            var selector=planner.GetComponent<FightEnvironmentSelector>();
            var view=all.Select(t=>t.GetComponent<Art2CameraController>()).Single(p=>p).GetComponent<Camera>();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/ArtBattle/ForestEnvironment.prefab");
            var sprites=source.GetComponentsInChildren<SpriteRenderer>(true);
            var cards=MaterialAsset("ForestSpriteCard","RhythmHunter/Art2/SpriteCardLit");
            cards.SetFloat("_AmbientStrength",1f);cards.SetFloat("_LightInfluence",0f);cards.SetColor("_AmbientTint",Color.white);
            var ground=MaterialAsset("ForestGround","Universal Render Pipeline/Lit");
            var roadSource=sprites.First(s=>s.name=="Background_04");var road=roadSource.sprite.texture;var roadRect=OriginalCrop(roadSource);
            ground.SetTexture("_BaseMap",road);ground.SetTextureScale("_BaseMap",new Vector2(roadRect.width/road.width,roadRect.height/road.height));ground.SetTextureOffset("_BaseMap",new Vector2(roadRect.x/road.width,roadRect.y/road.height));
            ground.SetFloat("_Smoothness",0);ground.SetFloat("_Cull",0);ground.SetColor("_BaseColor",Color.white);
            var root=new GameObject("ForestEnvironment_2_5D");
            GameObject prefab;
            try
            {
                var floor=new GameObject("ForestGround_XZ");floor.transform.SetParent(root.transform,false);
                floor.AddComponent<MeshFilter>().sharedMesh=RoadMesh(view);floor.AddComponent<MeshRenderer>().sharedMaterial=ground;
                string[] layers={"Background_10","Background_09","Background_08","Background_07","Background_06","Background_05","Background_03","Background_02","Background_01","Background_00"};
                for(int i=0;i<layers.Length;i++)
                {
                    var original=sprites.FirstOrDefault(s=>s.name==layers[i]);if(!original||!original.sprite)continue;
                    var go=new GameObject(layers[i]);go.transform.SetParent(root.transform,false);
                    var sprite=go.AddComponent<SpriteRenderer>();sprite.sprite=CroppedArtwork(original);sprite.sharedMaterial=cards;
                    sprite.sortingOrder=-50+i;sprite.shadowCastingMode=ShadowCastingMode.Off;
                    // Far layers sit behind the battle. Grass meets the road; foreground frames it.
                    float depth=i<6?28-i*.3f:(i<8?13.3f:8f);
                    go.transform.position=view.ViewportToWorldPoint(new Vector3(.5f,.5f,depth));go.transform.rotation=view.transform.rotation;
                    float height=2*depth*Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f);
                    go.transform.localScale=new Vector3(height*view.aspect/sprite.sprite.bounds.size.x,height/sprite.sprite.bounds.size.y,1);
                    if(layers[i]=="Background_07"||layers[i]=="Background_00")
                    {
                        // Frame the outer edges as in the original composition, leaving the arena clear.
                        var size=go.transform.localScale;size.x*=layers[i]=="Background_07"?1.4f:1.5f;go.transform.localScale=size;
                    }
                    if(layers[i]=="Background_09")
                    {
                        var cloud=go.AddComponent<LoopingBackgroundScroller>();var config=new SerializedObject(cloud);
                        config.FindProperty("sourceRenderer").objectReferenceValue=sprite;
                        config.FindProperty("unitsPerSecond").floatValue=.12f;config.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                foreach(var original in sprites.Where(s=>s.name.StartsWith("Grass_")))
                {
                    var go=new GameObject(original.name);go.transform.SetParent(root.transform,false);
                    var sr=go.AddComponent<SpriteRenderer>();sr.sprite=original.sprite;sr.sharedMaterial=cards;
                    go.transform.position=new Vector3(original.transform.localPosition.x*1.5f,.1f,original.transform.localPosition.x<0?4.5f:5.5f);
                    go.transform.rotation=view.transform.rotation;go.transform.localScale=Vector3.one*1.5f;
                    // Legacy grass sprites contain canvas padding; retain as optional dressing.
                    go.SetActive(false);
                }
                var sun=new GameObject("ForestSun_AdjustMe").AddComponent<Light>();sun.transform.SetParent(root.transform,false);
                sun.type=LightType.Directional;sun.color=Color.white;sun.intensity=1f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.45f;
                sun.transform.rotation=Quaternion.Euler(50,-30,0);
                prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            var instance=all.FirstOrDefault(t=>t.name=="ForestEnvironment_2_5D");
            var forestRoot=instance?instance.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
            var data=new SerializedObject(selector);var bindings=data.FindProperty("bindings");
            var forest=AssetDatabase.LoadAssetAtPath<FightEnvironmentDefinition>("Assets/FightDemo/Levels/Environment_Forest.asset");
            var cave=AssetDatabase.LoadAssetAtPath<FightEnvironmentDefinition>("Assets/FightDemo/Levels/Environment_Cave.asset");
            var floorRecovery=all.Select(t=>t.GetComponent<Art2FloorOcclusion>()).Single(p=>p);
            // Floor recovery is shared by both environments, not part of Cave fog.
            if(floorRecovery.transform.parent!=planner.transform)
                Undo.SetTransformParent(floorRecovery.transform,planner.transform,"Share environment floor recovery");
            var actor=all.Select(t=>t.GetComponent<Art2ActorPresentation>()).Single(p=>p);
            var pressure=all.Select(t=>t.GetComponent<FightPressureVisuals>()).Single(p=>p);
            void References(SerializedProperty array,UnityEngine.Object[] objects){array.arraySize=objects.Length;for(int i=0;i<objects.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=objects[i];}
            var caveBinding=bindings.GetArrayElementAtIndex(0);
            // Pressure presentation lives on the old light object. Keep that host alive
            // and toggle only its Cave light/adapter so Forest still receives Pressure VFX.
            var lightHost=all.Single(t=>t.name=="ShadowTestLight_MoveMe").gameObject;
            var rootsProperty=caveBinding.FindPropertyRelative("roots");
            var rootFlags=caveBinding.FindPropertyRelative("rootEnabled");
            for(int i=rootsProperty.arraySize-1;i>=0;i--)
                if(rootsProperty.GetArrayElementAtIndex(i).objectReferenceValue==lightHost)
                {
                    rootsProperty.DeleteArrayElementAtIndex(i);
                    if(i<rootsProperty.arraySize&&rootsProperty.GetArrayElementAtIndex(i).objectReferenceValue==null)rootsProperty.DeleteArrayElementAtIndex(i);
                    rootFlags.DeleteArrayElementAtIndex(i);
                }
            var control=caveBinding.FindPropertyRelative("controllers");var controlFlags=caveBinding.FindPropertyRelative("controllerEnabled");
            foreach(var extra in new Behaviour[]{lightHost.GetComponent<Light>(),lightHost.GetComponent<Art2ShadowTestLight>()})
            {
                if(!extra)continue;bool found=false;for(int i=0;i<control.arraySize;i++)found|=control.GetArrayElementAtIndex(i).objectReferenceValue==extra;
                if(found)continue;int at=control.arraySize;control.arraySize++;controlFlags.arraySize=control.arraySize;
                control.GetArrayElementAtIndex(at).objectReferenceValue=extra;controlFlags.GetArrayElementAtIndex(at).boolValue=true;
            }
            lightHost.SetActive(true);
            if(!caveBinding.FindPropertyRelative("mainLight").objectReferenceValue)
            {
                caveBinding.FindPropertyRelative("mainLight").objectReferenceValue=all.Single(t=>t.name=="ShadowTestLight_MoveMe").GetComponent<Light>();
                References(caveBinding.FindPropertyRelative("floors"),floorRecovery.floors);
                References(caveBinding.FindPropertyRelative("occluders"),floorRecovery.sceneryOccluders);
            }
            var binding=bindings.GetArrayElementAtIndex(1);binding.FindPropertyRelative("ready").boolValue=true;
            References(binding.FindPropertyRelative("roots"),new UnityEngine.Object[]{forestRoot});
            binding.FindPropertyRelative("rootEnabled").arraySize=1;binding.FindPropertyRelative("rootEnabled").GetArrayElementAtIndex(0).boolValue=true;
            binding.FindPropertyRelative("mainLight").objectReferenceValue=forestRoot.GetComponentInChildren<Light>(true);
            References(binding.FindPropertyRelative("floors"),new[]{forestRoot.transform.Find("ForestGround_XZ").GetComponent<Renderer>()});
            References(binding.FindPropertyRelative("occluders"),forestRoot.GetComponentsInChildren<SpriteRenderer>(true));
            data.FindProperty("actorPresentation").objectReferenceValue=actor;data.FindProperty("pressureVisuals").objectReferenceValue=pressure;data.FindProperty("floorOcclusion").objectReferenceValue=floorRecovery;data.ApplyModifiedProperties();
            const string stagePath="Assets/FightDemo/Levels/Chapter01_Stage01_Tutorial.asset";
            var stage=AssetDatabase.LoadAssetAtPath<FightStageDefinition>(stagePath);
            if(!stage)
            {
                stage=ScriptableObject.CreateInstance<FightStageDefinition>();AssetDatabase.CreateAsset(stage,stagePath);
                var stageData=new SerializedObject(stage);stageData.FindProperty("stageId").stringValue="Chapter01_Stage01";
                stageData.FindProperty("displayName").stringValue="Chapter 1 - Stage 1 - Forest Tutorial";stageData.FindProperty("tutorial").boolValue=true;
                stageData.FindProperty("tutorialHero").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/ArtBattle/LongSwordWarrior_Weapon.prefab").GetComponent<FightCharacterDefinition>();
                stageData.FindProperty("musicEventPath").stringValue="event:/Ritual Slam _120_3";
                var waves=stageData.FindProperty("waves");waves.arraySize=1;var wave=waves.GetArrayElementAtIndex(0);
                wave.FindPropertyRelative("name").stringValue="Forest Preview";
                wave.FindPropertyRelative("front").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Normal.prefab").GetComponent<FightCharacterDefinition>();
                wave.FindPropertyRelative("pressure.enablePressurePhase").boolValue=false;
                stageData.ApplyModifiedProperties();
            }
            if(!stage.Validate(out string error))throw new Exception(error);
            var chapter=AssetDatabase.LoadAssetAtPath<FightChapterDefinition>("Assets/FightDemo/Levels/Chapter01.asset");
            var chapterData=new SerializedObject(chapter);var stages=chapterData.FindProperty("stages");if(stages.arraySize==0)stages.arraySize=1;
            stages.GetArrayElementAtIndex(0).objectReferenceValue=stage;chapterData.ApplyModifiedProperties();
            var plan=new SerializedObject(planner);plan.FindProperty("chapter").objectReferenceValue=chapter;plan.FindProperty("stageIndex").intValue=0;plan.ApplyModifiedProperties();
            if(!selector.TryApply(cave,out error)||!selector.TryApply(forest,out error))throw new Exception(error);
            if(planner.SelectedEnvironment!=forest||!forestRoot.activeSelf)throw new Exception("Forest selection failed");
            // Scene-level references changed by the preview must survive the next editor session.
            EditorUtility.SetDirty(actor);EditorUtility.SetDirty(pressure);EditorUtility.SetDirty(floorRecovery);
            EditorUtility.SetDirty(cards);EditorUtility.SetDirty(ground);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Selection.activeGameObject=forestRoot;
        }
    }
}
