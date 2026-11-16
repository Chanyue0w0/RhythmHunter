using System.Collections.Generic;
using UnityEngine;
using RhythmHunter.FightDemo.Art2;
using UnityEngine.Rendering.Universal;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightBossEntrance
    {
        [SerializeField] private Shader doorFanShader;
        [SerializeField] private Transform doorExplosionAnchor;
        [SerializeField] private Transform doorFocusAnchor;
        private GameObject entranceSmoke;
        private ParticleSystem[] smokeParticles;
        private float smokeDuration, smokeSimulated;
        private double smokeStartBeat=-1;
        private double lastPresentationBeat;
        private float bossReveal=1;
        private double DoorHoldBeats => System.Math.Max(Settings.bossRevealBeats,System.Math.Max(Settings.openingIdleBeats,Settings.doorExplosion ? Settings.doorExplosionBeats : 0));
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
        private const uint DoorRenderingLayer = 1u << 7;
        private readonly Dictionary<Renderer,uint> doorRendererMasks = new();
        private int doorLayerActorCount = -1;

        private void ConfigureDoorLayers(bool active)
        {
            if(!active)
            {
                foreach(var pair in doorRendererMasks)
                    if(pair.Key) pair.Key.renderingLayerMask=pair.Value;
                doorRendererMasks.Clear();
                doorLayerActorCount=-1;
                return;
            }
            int count=roster.ActiveHeroes.Count+roster.ActiveEnemies.Count;
            if(doorLayerActorCount==count) return;
            doorLayerActorCount=count;
            foreach(var sceneRoot in gameObject.scene.GetRootGameObjects())
                foreach(var renderer in sceneRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if(!doorRendererMasks.ContainsKey(renderer)) doorRendererMasks.Add(renderer,renderer.renderingLayerMask);
                    bool hero=false;
                    foreach(var slot in roster.ActiveHeroes)
                        if(slot && renderer.transform.IsChildOf(slot.transform)) { hero=true;break; }
                    renderer.renderingLayerMask=hero ? doorRendererMasks[renderer]&~DoorRenderingLayer : doorRendererMasks[renderer]|DoorRenderingLayer;
                }
        }

        private void ApplySilhouette(Entry entry, bool black)
        {
            if(entry == null) return;
            var renderer = entry.slot && entry.slot.ActorInstance ? entry.slot.CombatAnimator?.TargetRenderer : null;
            if (!renderer) return;
            silhouetteBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(silhouetteBlock);
            silhouetteBlock.SetFloat(SilhouetteId, black ? 1 : 0);
            silhouetteBlock.SetFloat("_EntranceReveal", bossReveal);
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
            lastPresentationBeat=beat;
            var s = Settings;
            double elapsed = beat-startBeat;
            double jumpAt = s.waitBeforeDoorBeats+s.doorOpenBeats+DoorHoldBeats;
            UpdateDoorExplosion(beat);
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
                entranceSpot.shadows=s.doorLightCastShadows ? LightShadows.Soft : LightShadows.None;
                entranceSpot.shadowStrength=s.doorLightShadowStrength;
                entranceSpot.shadowBias=s.doorLightShadowBias;
                entranceSpot.shadowNormalBias=s.doorLightShadowNormalBias;
                var lightData=entranceSpot.GetUniversalAdditionalLightData();
                lightData.customShadowLayers=false;
                lightData.renderingLayers=s.doorLightExcludeHeroes ? DoorRenderingLayer : 1u;
                ConfigureDoorLayers(lightWeight>0 && s.doorLightExcludeHeroes);
                if(entranceFan)
                {
                    Vector3 fanOrigin=s.fanFollowsDoorLight ? entranceSpot.transform.position : origin;
                    entranceFan.transform.SetPositionAndRotation(new Vector3(fanOrigin.x,s.fanGroundY,fanOrigin.z),Quaternion.Euler(0,s.fanFollowsDoorLight ? 0 : s.fanYaw,0));
                    entranceFan.transform.localScale=Vector3.one;
                    var dimensions=s.fanFollowsDoorLight ? new Vector3(s.doorLightRange*2,s.doorLightRange*2,-s.doorLightRange) : new Vector3(s.fanStartWidth,s.fanEndWidth,s.fanLength);
                    if(fanDimensions!=dimensions)
                    {
                        fanDimensions=dimensions;
                        float near=s.fanFollowsDoorLight ? -s.doorLightRange : 0;
                        float far=s.fanFollowsDoorLight ? s.doorLightRange : s.fanLength;
                        fanMesh.vertices=new[] {new Vector3(-dimensions.x*.5f,0,near),new Vector3(dimensions.x*.5f,0,near),new Vector3(-dimensions.y*.5f,0,far),new Vector3(dimensions.y*.5f,0,far)};
                        fanMesh.RecalculateBounds();
                    }
                    fanMaterial.SetColor("_Color",s.doorLightColor*(s.fanBrightness*lightWeight));
                    fanMaterial.SetVector("_DoorLightPosition",entranceSpot.transform.position);
                    fanMaterial.SetVector("_DoorLightForward",entranceSpot.transform.forward);
                    fanMaterial.SetVector("_DoorCone",new Vector4(s.doorLightRange,Mathf.Cos(s.doorLightAngle*.5f*Mathf.Deg2Rad),Mathf.Cos(entranceSpot.innerSpotAngle*.5f*Mathf.Deg2Rad),s.fanFollowsDoorLight ? 1 : 0));
                }
            }
            if(!cameraController && view) cameraController=view.GetComponent<Art2CameraController>();
            if(!cameraController) return;
            float focusProgress=Mathf.SmoothStep(0,1,Mathf.Clamp01((float)((elapsed-s.waitBeforeDoorBeats+s.doorFocusLeadBeats)/Mathf.Max(.01f,s.doorFocusLeadBeats))));
            if(elapsed>=jumpAt)focusProgress*=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((float)((elapsed-jumpAt)/Mathf.Max(.01f,s.doorFocusReturnBeats))));
            if(!s.doorFocusEnabled)focusProgress=0;
            var focusTarget=doorFocusAnchor ? doorFocusAnchor.position : doorSpawnAnchor ? doorSpawnAnchor.position : transform.position;
            cameraController.SetBossEntranceFocus(focusTarget+s.doorFocusOffset,s.doorFocusWeight*focusProgress,s.doorFocusZoom*focusProgress);
            double shakeElapsed=beat-shakeStartBeat;
            Vector2 offset=Vector2.zero;
            double doorShakeTime=elapsed-s.waitBeforeDoorBeats;
            if(s.doorShakeEnabled && doorShakeTime>=0 && doorShakeTime<s.doorShakeBeats)
            {
                float weight=Mathf.Pow(1-(float)doorShakeTime/Mathf.Max(.01f,s.doorShakeBeats),s.doorShakeDecay);
                float phase=(float)doorShakeTime*Mathf.PI*2*s.doorShakeCyclesPerBeat;
                offset=Vector2.Scale(s.doorShakeAmplitude,new Vector2(Mathf.Sin(phase),Mathf.Cos(phase*1.23f)))*weight;
            }
            if(s.landingShakeEnabled && shakeStartBeat>=0 && shakeElapsed>=0 && shakeElapsed<s.landingShakeBeats)
            {
                float weight=Mathf.Pow(1-(float)shakeElapsed/Mathf.Max(.01f,s.landingShakeBeats),s.landingShakeDecay);
                float phase=(float)shakeElapsed*Mathf.PI*2*s.landingShakeCyclesPerBeat;
                offset+=Vector2.Scale(s.landingShakeAmplitude,new Vector2(Mathf.Sin(phase),Mathf.Cos(phase*1.23f)))*weight;
            }
            cameraController.SetBossEntranceShake(offset);
        }

        private void ClearPresentation()
        {
            bossReveal=1;
            if(entranceSmoke) Destroy(entranceSmoke);
            ConfigureDoorLayers(false);
            foreach(var entry in entries) ApplySilhouette(entry,false);
            if(cameraController) { cameraController.SetBossEntranceShake(Vector2.zero); cameraController.SetBossEntranceFocus(Vector3.zero,0,0); }
            if(doorLightingRoot) Destroy(doorLightingRoot);
            if(fanMesh) Destroy(fanMesh);
            if(fanMaterial) Destroy(fanMaterial);
        }

        private void SpawnDoorExplosion(double beat)
        {
            var s=Settings;
            smokeStartBeat=beat;bossReveal=0;
            if(!s.doorExplosion) return;
            var anchor=doorExplosionAnchor ? doorExplosionAnchor : doorSpawnAnchor;
            entranceSmoke=Instantiate(s.doorExplosion,(anchor ? anchor.position : transform.position)+s.doorExplosionOffset,s.doorExplosion.transform.rotation);
            entranceSmoke.name="BossDoorSmoke_Runtime";
            entranceSmoke.transform.localScale*=s.doorExplosionScale;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(entranceSmoke,gameObject.scene);
            var effectSettings=entranceSmoke.GetComponent<FightEffectSettings>();
            if(effectSettings) effectSettings.destroyAfterParticles=false; // This entrance owns the beat-driven completion.
            var owner=entranceSmoke.GetComponent<FightBattleEffect>() ?? entranceSmoke.AddComponent<FightBattleEffect>();
            owner.Configure(fight,false,0);
            smokeParticles=entranceSmoke.GetComponentsInChildren<ParticleSystem>(true);
            smokeDuration=.01f;
            foreach(var particle in smokeParticles)
            {
                var main=particle.main;
                main.loop=false;main.stopAction=ParticleSystemStopAction.None;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                smokeDuration=Mathf.Max(smokeDuration,main.startDelay.constantMax+main.duration+main.startLifetime.constantMax);
                particle.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Simulate(0,false,true,false);
            }
            smokeStartBeat=beat;smokeSimulated=0;bossReveal=0;
            UpdateDoorExplosion(beat);
        }

        private void UpdateDoorExplosion(double beat)
        {
            if(smokeStartBeat<0) return;
            float progress=Mathf.Clamp01((float)((beat-smokeStartBeat)/Mathf.Max(.1f,Settings.doorExplosionBeats)));
            bossReveal=Mathf.Clamp01((float)((beat-smokeStartBeat)/Mathf.Max(.01f,Settings.bossRevealBeats)));
            if(!entranceSmoke) return;
            float target=Mathf.Max(.01f,progress*smokeDuration);
            foreach(var particle in smokeParticles)
                if(particle) particle.Simulate(Mathf.Max(0,target-smokeSimulated),false,false,false);
            smokeSimulated=target;
            if(progress>=1) { entranceSmoke.GetComponent<FightBattleEffect>().Cancel(); entranceSmoke=null; }
        }

        private void SpawnLandingExplosion(Entry entry)
        {
            var s=Settings;
            if(!s.landingExplosionEnabled || !s.doorExplosion) return;
            var effect=Instantiate(s.doorExplosion,entry.destination+entry.footOffset+s.landingExplosionOffset,s.doorExplosion.transform.rotation);
            effect.name="BossLandingSmoke_Runtime";
            effect.transform.localScale*=s.landingExplosionScale;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effect,gameObject.scene);
            var owner=effect.GetComponent<FightBattleEffect>() ?? effect.AddComponent<FightBattleEffect>();
            owner.Configure(fight,false,5);
        }
    }
}
