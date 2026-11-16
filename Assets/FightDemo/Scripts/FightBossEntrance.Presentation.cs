using System.Collections.Generic;
using UnityEngine;
using RhythmHunter.FightDemo.Art2;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightBossEntrance
    {
        [SerializeField] private Shader doorFanShader;
        private readonly HashSet<Entry> entryIdleRestored = new();
        private MaterialPropertyBlock silhouetteBlock;
        private static readonly int SilhouetteId = Shader.PropertyToID("_EntranceSilhouette");
        private GameObject doorLightingRoot;
        private Light entranceSpot;
        private MeshRenderer entranceFan;
        private Mesh fanMesh;
        private Material fanMaterial;
        private double shakeStartBeat = -1;
        private Art2CameraController cameraController;
        private Vector3 fanDimensions = new Vector3(-1,-1,-1);

        private void ApplySilhouette(Entry entry, bool black)
        {
            if(entry == null) return;
            var renderer = entry.slot && entry.slot.ActorInstance ? entry.slot.CombatAnimator?.TargetRenderer : null;
            if (!renderer) return;
            silhouetteBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(silhouetteBlock);
            silhouetteBlock.SetFloat(SilhouetteId, black ? 1 : 0);
            renderer.SetPropertyBlock(silhouetteBlock);
        }

        private void CreateDoorLighting()
        {
            doorLightingRoot = new GameObject("BossEntranceDoorLighting_Runtime");
            doorLightingRoot.transform.SetParent(transform, false);
            var lightObject = new GameObject("DoorSpotLight");
            lightObject.transform.SetParent(doorLightingRoot.transform, false);
            entranceSpot = lightObject.AddComponent<Light>();
            entranceSpot.type = LightType.Spot;
            entranceSpot.shadows = LightShadows.None;
            if (!doorFanShader) return;
            var fan = new GameObject("DoorGroundFan");
            fan.transform.SetParent(doorLightingRoot.transform, false);
            fanMesh = new Mesh { name = "Boss door fan" };
            fanMesh.vertices = new[] {new Vector3(-.5f,0,0), new Vector3(.5f,0,0), new Vector3(-.5f,0,1), new Vector3(.5f,0,1)};
            fanMesh.uv = new[] {new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1)};
            fanMesh.triangles = new[] {0,2,1,1,2,3};
            fanMesh.RecalculateBounds();
            fan.AddComponent<MeshFilter>().sharedMesh = fanMesh;
            entranceFan = fan.AddComponent<MeshRenderer>();
            entranceFan.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            entranceFan.receiveShadows = false;
            fanMaterial = new Material(doorFanShader);
            entranceFan.sharedMaterial = fanMaterial;
        }

        private void UpdatePresentation(double beat)
        {
            var s = Settings;
            double elapsed = beat-startBeat;
            double jumpAt = s.waitBeforeDoorBeats+s.doorOpenBeats+s.openingIdleBeats;
            float lightWeight = s.doorLightEnabled ? Mathf.Clamp01((float)((elapsed-s.waitBeforeDoorBeats)/Mathf.Max(.01f,s.doorOpenBeats))) : 0;
            if(elapsed>=jumpAt) lightWeight *= 1-Mathf.Clamp01((float)((elapsed-jumpAt)/Mathf.Max(.01f,s.doorLightFadeOutBeats)));
            if(lightWeight>0 && !doorLightingRoot) CreateDoorLighting();
            if(doorLightingRoot)
            {
                doorLightingRoot.SetActive(lightWeight>0);
                Vector3 origin = doorSpawnAnchor ? doorSpawnAnchor.position : transform.position;
                entranceSpot.transform.SetPositionAndRotation(origin+s.doorLightOffset,Quaternion.Euler(s.doorLightEuler));
                entranceSpot.color=s.doorLightColor;
                entranceSpot.intensity=s.doorLightIntensity*lightWeight;
                entranceSpot.range=s.doorLightRange;
                entranceSpot.spotAngle=s.doorLightAngle;
                entranceSpot.innerSpotAngle=s.doorLightAngle*.75f;
                if(entranceFan)
                {
                    entranceFan.transform.SetPositionAndRotation(new Vector3(origin.x,s.fanGroundY,origin.z),Quaternion.Euler(0,s.fanYaw,0));
                    entranceFan.transform.localScale=Vector3.one;
                    var dimensions=new Vector3(s.fanStartWidth,s.fanEndWidth,s.fanLength);
                    if(fanDimensions!=dimensions)
                    {
                        fanDimensions=dimensions;
                        fanMesh.vertices=new[] {new Vector3(-s.fanStartWidth*.5f,0,0),new Vector3(s.fanStartWidth*.5f,0,0),new Vector3(-s.fanEndWidth*.5f,0,s.fanLength),new Vector3(s.fanEndWidth*.5f,0,s.fanLength)};
                        fanMesh.RecalculateBounds();
                    }
                    fanMaterial.SetColor("_Color",s.doorLightColor*(s.fanBrightness*lightWeight));
                }
            }
            if(!cameraController && view) cameraController=view.GetComponent<Art2CameraController>();
            if(!cameraController) return;
            double shakeElapsed=beat-shakeStartBeat;
            Vector2 offset=Vector2.zero;
            if(s.landingShakeEnabled && shakeStartBeat>=0 && shakeElapsed>=0 && shakeElapsed<s.landingShakeBeats)
            {
                float weight=Mathf.Pow(1-(float)shakeElapsed/Mathf.Max(.01f,s.landingShakeBeats),s.landingShakeDecay);
                float phase=(float)shakeElapsed*Mathf.PI*2*s.landingShakeCyclesPerBeat;
                offset=Vector2.Scale(s.landingShakeAmplitude,new Vector2(Mathf.Sin(phase),Mathf.Cos(phase*1.23f)))*weight;
            }
            cameraController.SetBossEntranceShake(offset);
        }

        private void ClearPresentation()
        {
            foreach(var entry in entries) ApplySilhouette(entry,false);
            if(cameraController) cameraController.SetBossEntranceShake(Vector2.zero);
            if(doorLightingRoot) Destroy(doorLightingRoot);
            if(fanMesh) Destroy(fanMesh);
            if(fanMaterial) Destroy(fanMaterial);
        }
    }
}
