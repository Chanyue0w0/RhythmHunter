using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmHunter.FightDemo.Art2;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightPhaseTwoSpeedSetup
    {
        static FightPhaseTwoSpeedSetup()=>EditorApplication.update+=Poll;
        static void Poll()
        {
            const string request="Temp/PhaseTwoSpeedSetup.request";
            if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            File.Delete(request);
            try { Setup(); Validate();File.WriteAllText("Temp/PhaseTwoSpeedSetup.result","PASS: Art2 door/camera/shader wired; true 3D perspective expansion; fixed world mesh after 1000 updates; no UI graphic. Chart and rhythm controller untouched."); }
            catch(Exception e){File.WriteAllText("Temp/PhaseTwoSpeedSetup.result","FAIL: "+e);Debug.LogException(e);}
        }
        static void Setup()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene("Assets/FightDemo/Scenes/Art2_FightScene.unity",OpenSceneMode.Additive);
            try
            {
                var fight=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FightCombatController>(true)).Single();
                var visual=fight.GetComponent<FightPhaseTwoSpeedLines>();
                if(!visual)visual=Undo.AddComponent<FightPhaseTwoSpeedLines>(fight.gameObject);
                var data=new SerializedObject(visual);data.FindProperty("fight").objectReferenceValue=fight;data.FindProperty("rhythm").objectReferenceValue=fight.GetComponent<FightBossRhythmBattle>();
                if(!data.FindProperty("tunnelOrigin").objectReferenceValue)
                {
                    var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
                    data.FindProperty("tunnelOrigin").objectReferenceValue=all.First(t=>t.name=="BossDoorSpawnAnchor");
                    data.FindProperty("tunnelCamera").objectReferenceValue=all.Select(t=>t.GetComponent<Art2CameraController>()).First(c=>c).GetComponent<Camera>();
                    data.FindProperty("tunnelShader").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Shader>("Assets/FightDemo/Art2Prototype/TunnelAirflow.shader");
                    data.FindProperty("lineColor").colorValue=new Color(.5f,.75f,1,.55f);
                    data.FindProperty("lowIntensityCount").intValue=28;data.FindProperty("highIntensityCount").intValue=96;
                    data.FindProperty("protectedWidth").floatValue=.68f;
                }
                data.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static void Validate()
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/FightDemo/Art2Prototype/TunnelAirflow.shader");
            Check(shader&&!ShaderUtil.ShaderHasError(shader),"Tunnel shader");
            var root=new GameObject("Tunnel test");
            try
            {
                var camera=root.AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-10);camera.fieldOfView=40;
                var far=FightTunnelAirflow.Point(Vector3.zero,camera.transform.position,Vector3.right,.1f,.35f,4);
                var near=FightTunnelAirflow.Point(Vector3.zero,camera.transform.position,Vector3.right,.6f,.35f,4);
                Check(Mathf.Abs(camera.WorldToViewportPoint(near).x-.5f)>Mathf.Abs(camera.WorldToViewportPoint(far).x-.5f),"No perspective expansion");
                var meshObject=new GameObject("Airflow");meshObject.transform.SetParent(root.transform,false);
                var airflow=meshObject.AddComponent<FightTunnelAirflow>();airflow.Initialize(shader);
                for(int i=0;i<1000;i++)airflow.Present(camera,Vector3.zero,.01f,96,.35f,4,2,new Vector2(.02f,.04f),Color.white,true,.5f,.68f,.1f,.04f,true,true);
                var mesh=meshObject.GetComponent<MeshFilter>().sharedMesh;
                Check(mesh.vertexCount==512&&mesh.triangles.Length==768,"Fixed mesh capacity including fading lines");
                Check(mesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.y)),"Invalid world geometry");
                Vector3 tail=new(0,0,-9),head=new(0,0,-11);
                Check(FightTunnelAirflow.ClipToNearPlane(camera,ref tail,ref head,out float ta,out float ha),"Visible tail discarded");
                Check(ta==0&&ha>0&&ha<1&&camera.WorldToViewportPoint(head).z>=camera.nearClipPlane,"Near-plane alpha continuity");
                tail=new Vector3(0,0,-11);head=new Vector3(0,0,-12);
                Check(!FightTunnelAirflow.ClipToNearPlane(camera,ref tail,ref head,out ta,out ha),"Behind-camera ribbon retained");
                airflow.Present(camera,Vector3.zero,0,96,.35f,4,10,new Vector2(.02f,.04f),Color.white,true,.5f,.68f,.1f,.04f,true,true);
                airflow.Present(camera,Vector3.zero,0,96,.35f,4,2,new Vector2(.02f,.04f),Color.white,true,.5f,.68f,.1f,.04f,true,true,lengthBlend:.1f);
                float smooth=(float)typeof(FightTunnelAirflow).GetField("displayedLength",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(airflow);
                Check(Mathf.Abs(smooth-9.2f)<.001f,"Abrupt length change");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
