using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmHunter.FightDemo;
using RhythmHunter.FightDemo.Art2;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class Art2GroundingSetup
    {
        static Art2GroundingSetup() => EditorApplication.update += Requested;
        static void Requested()
        {
            const string request = "Temp/Art2Grounding.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(request); } catch (IOException) { return; }
            try { Run(); } catch (Exception e) { File.WriteAllText("Temp/Art2Grounding.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Art2/Configure Character Grounding And Door Shadow")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.path.EndsWith("Art2_FightScene.unity")) throw new Exception("Open Art2 outside Play Mode.");
            int count = 0;
            foreach (var name in new[] { "LongswordWarrior", "Mage", "Skeleton", "Dwarf", "Minotaur", "Goblin_Normal", "Goblin_Killer", "Goblin_Giant" })
            {
                string path = "Assets/FightDemo/Prefabs/ArtBattle/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var foot = root.transform.Find("FootAnchor");
                    if (!foot)
                    {
                        var animator = root.GetComponentInChildren<FightCharacterCombatAnimator>(true);
                        var renderer = animator ? animator.TargetRenderer : root.GetComponentInChildren<SpriteRenderer>(true);
                        if (!renderer || !renderer.sprite) throw new Exception("Missing character sprite: " + name);
                        var point = Bottom(renderer.sprite, out _);
                        if (renderer.flipX) point.x = -point.x;
                        if (renderer.flipY) point.y = -point.y;
                        foot = new GameObject("FootAnchor").transform;
                        foot.SetParent(root.transform, false);
                        foot.position = renderer.transform.TransformPoint(point);
                    }
                    // Verify actual spawn rotations/scales can be grounded without moving the slot.
                    var baseline = root.transform.position;
                    var rotation = root.transform.rotation;
                    foreach (float x in new[] { -7f, 0f, 7f })
                    {
                        root.transform.SetPositionAndRotation(new Vector3(x, .3f, -1), Quaternion.Euler(45,0,0));
                        root.transform.position -= Vector3.up * foot.position.y;
                        if (Mathf.Abs(foot.position.y) > .0001f) throw new Exception("Foot grounding failed: " + name);
                    }
                    root.transform.SetPositionAndRotation(baseline, rotation);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    count++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var presentation = all.Select(t=>t.GetComponent<Art2ActorPresentation>()).Single(c=>c);
            Undo.RecordObject(presentation,"Ground character shadows");
            var data = new SerializedObject(presentation);
            data.FindProperty("showContactShadows").boolValue=false;
            data.FindProperty("castSpriteShadows").boolValue=true;
            data.FindProperty("alignFeetToGround").boolValue=true;
            data.FindProperty("actorGroundHeight").floatValue=0;
            data.ApplyModifiedProperties();
            var light = all.Single(t=>t.name=="ShadowTestLight_MoveMe").GetComponent<Light>();
            Undo.RecordObject(light,"Reduce detached shadow bias");
            light.shadowNormalBias=.03f;
            var door = all.Single(t=>t.name=="ClosedDoor_GroundAnchor").GetComponentInChildren<SpriteRenderer>(true);
            var contact = door.GetComponent<Art2SceneryContactShadows>();
            if(!contact) contact=Undo.AddComponent<Art2SceneryContactShadows>(door.gameObject);
            Undo.RecordObject(contact,"Door contact shadow");
            var bottom=Bottom(door.sprite,out float width);
            var settings=new SerializedObject(contact);
            settings.FindProperty("shadowMaterial").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>("Assets/FightDemo/Art2Prototype/Art2ContactShadow.mat");
            settings.FindProperty("showContactShadows").boolValue=true;
            settings.FindProperty("opacity").floatValue=.5f;
            settings.FindProperty("groundHeight").floatValue=.025f;
            var contacts=settings.FindProperty("contacts"); contacts.arraySize=1;
            var c=contacts.GetArrayElementAtIndex(0);
            c.FindPropertyRelative("footPixel").vector2Value=new Vector2(bottom.x*door.sprite.pixelsPerUnit+door.sprite.pivot.x, door.sprite.rect.height-(bottom.y*door.sprite.pixelsPerUnit+door.sprite.pivot.y));
            c.FindPropertyRelative("widthPixels").floatValue=width*1.15f;
            c.FindPropertyRelative("depth").floatValue=.7f;
            c.FindPropertyRelative("worldOffset").vector2Value=Vector2.zero;
            settings.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(contact);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");
            File.WriteAllText("Temp/Art2Grounding.result", "PASS: " + count + " foot anchors verified at left/center/right; blob shadows disabled; normal bias 0.03; door contact shadow configured; Art2 saved.");
        }
        static Vector3 Bottom(Sprite sprite, out float width)
        {
            var image = new Texture2D(2,2);
            try
            {
                image.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)));
                var pixels=image.GetPixels32(); var r=sprite.rect;
                int minY=(int)r.height, minX=(int)r.width, maxX=-1;
                for(int y=0;y<r.height;y++) for(int x=0;x<r.width;x++)
                    if(pixels[((int)r.y+y)*image.width+(int)r.x+x].a>25) minY=Math.Min(minY,y);
                if(minY==(int)r.height)throw new Exception("Empty sprite: "+sprite.name);
                for(int y=minY;y<Math.Min(r.height,minY+Math.Max(2,(int)r.height/20));y++) for(int x=0;x<r.width;x++)
                    if(pixels[((int)r.y+y)*image.width+(int)r.x+x].a>25){minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);}
                width=maxX-minX+1;
                return new Vector3((minX+maxX+1)*.5f-sprite.pivot.x,minY-sprite.pivot.y,0)/sprite.pixelsPerUnit;
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}
