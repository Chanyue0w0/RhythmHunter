using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RhythmHunter.FightDemo
{
    // Presentation and free-press finale. Song beat ownership stays with FightBossRhythmBattle.
    [DefaultExecutionOrder(90), DisallowMultipleComponent]
    public sealed class FightBossGemFinale : MonoBehaviour
    {
        [SerializeField] private FightCombatController fight;
        [SerializeField] private FightBossRhythmBattle rhythm;
        [SerializeField] private Camera view;
        [Header("Song section beats")]
        [SerializeField] private bool finaleEnabled=true;
        [SerializeField,Min(0)] private float startBeat=95;
        [SerializeField,Min(0)] private float fireBeat=104;
        [SerializeField,Min(.01f)] private float gatherBeats=1;
        [SerializeField,Range(0,1)] private float gatherFraction=.35f;
        [Tooltip("Additional world-space movement for the two side heroes during gem charging. +Z moves toward the Boss.")]
        [SerializeField] private Vector3 sideHeroGatherOffset=new(0,0,1.2f);
        [Header("World-space full gem")]
        [SerializeField] private GameObject gemPrefab;
        [SerializeField] private Sprite[] gemFrames;
        [SerializeField,Min(.01f)] private float gemLoopBeats=1;
        [SerializeField] private Vector3 gemOffset=new(0,1.3f,1.1f);
        [SerializeField,Min(.01f)] private float gemHeight=1.25f;
        [Tooltip("World-space offset from the landing point; +Z starts toward the distant door.")]
        [SerializeField] private Vector3 gemEntryOffset=new(0,0,7);
        [Tooltip("Enter vertically from above the camera viewport, at the landing point's camera depth.")]
        [SerializeField] private bool gemEnterFromScreenTop=true;
        [SerializeField,Min(.05f)] private float gemEntryViewportMargin=.2f;
        [SerializeField,Min(.01f)] private float gemEntryBeats=.45f;
        [SerializeField,Range(0,1)] private float bounceAmount=.18f;
        [SerializeField,Min(.01f)] private float bounceBeats=.25f;
        [Header("Free A presses (future reward input; no reward formula yet)")]
        [SerializeField,Min(1)] private int fullChargePresses=32;
        [SerializeField,Min(0)] private int damagePerPress=10;
        [SerializeField,Min(0)] private int perfectPressThreshold=20;
        [SerializeField,Min(1)] private int perfectDamage=99999;
        [Header("Charging energy shell")]
        [SerializeField] private bool chargingShellEnabled=true;
        [SerializeField] private Shader chargingShellShader;
        [SerializeField] private Vector3 chargingShellSize=new(2.1f,2.8f,1.4f);
        [SerializeField,Range(0,1)] private float chargingShellOpacity=.24f;
        [Tooltip("Offset from each hero's feet in camera right/up/forward coordinates.")]
        [SerializeField] private Vector3 chargingShellCenterOffset=new(0,1.2f,0);
        [SerializeField] private Shader chargingLinkShader;
        [SerializeField,Min(.001f)] private float chargingLinkWidth=.045f;
        [SerializeField] private Color chargingLinkColor=new(.2f,.7f,1,.65f);
        [SerializeField] private Sprite promptSprite;
        [SerializeField,Min(.01f)] private float promptHeight=.55f;
        [SerializeField] private Vector3 promptOffset=new(0,1.15f,0);
        [SerializeField,Range(0,.5f)] private float promptPulseAmount=.18f;
        [SerializeField,Min(.01f)] private float promptPulseBeats=.3f;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField,Min(.01f)] private float hitEffectLifetime=.5f;
        [SerializeField,Min(.01f)] private float hitEffectScale=.65f;
        [Header("Blue gem light (no shadows)")]
        [SerializeField] private Color gemLightColor=new(.15f,.5f,1,1);
        [SerializeField] private Vector2 lightIntensity=new(5,35);
        [SerializeField,Min(.1f)] private float lightRange=16;
        [Header("Camera glare (presentation only)")]
        [SerializeField] private bool screenGlareEnabled=true;
        [SerializeField,Range(0,1)] private float screenBrightness=.32f;
        [SerializeField,Range(0,1)] private float gemGlareStrength=.5f;
        [SerializeField] private bool fireFlashEnabled=true;
        [SerializeField,Range(0,1)] private float fireFlashStrength=.95f;
        [SerializeField,Min(.01f)] private float fireFlashLeadBeats=.18f;
        [SerializeField,Min(.01f)] private float fireFlashFadeBeats=.35f;
        [Header("Beam: local +Z points to Boss")]
        [SerializeField] private GameObject beamPrefab;
        [SerializeField,Min(.1f)] private float beamScale=1.35f;
        [SerializeField] private bool beamVolumeEnabled=true;
        [Tooltip("Suppress the authored Sparks around long streaks on this instance only.")]
        [SerializeField] private bool suppressBeamLongSparks=true;
        [SerializeField] private Shader beamVolumeShader;
        [SerializeField,Min(.01f)] private float beamVolumeRadius=.95f;
        [SerializeField] private int beamSortingOrder=20;
        [SerializeField] private Vector3 beamOffset=Vector3.zero;
        [SerializeField] private Vector3 beamRotationOffset=Vector3.zero;
        [SerializeField,Range(0,1)] private float weakBeamOpacity=.16f;
        [SerializeField,Min(.01f)] private float beamFadeBeats=1;
        [Header("Beam camera shake")]
        [SerializeField] private bool beamCameraShakeEnabled=true;
        [SerializeField] private Vector2 beamCameraShakeAmplitude=new(.045f,.035f);
        [SerializeField,Min(.1f)] private float beamCameraShakeCyclesPerBeat=4;
        Art2.Art2CameraController finaleCamera;
        [Header("Boss beam impact (visual only)")]
        [SerializeField] private bool bossImpactShakeEnabled=true;
        [SerializeField,Min(0)] private float bossImpactShakeAmount=.24f;
        [SerializeField,Range(.01f,1)] private float bossImpactShakeBeats=.4f;
        [SerializeField] private bool bossImpactFlashEnabled=true;
        [SerializeField,Range(0,1)] private float bossImpactFlashStrength=.7f;
        [SerializeField,Min(.001f)] private float bossImpactFlashSeconds=.08f;
        Transform impactRoot;Vector3 impactPosition;FightHitFlashController impactFlash;int lastImpactBeat=int.MinValue;
        public bool Active {get;private set;}
        public bool Fired {get;private set;}
        public int PressCount {get;private set;}
        public bool IsPerfect => PressCount>perfectPressThreshold;
        public int BeamDamage => CalculateBeamDamage(PressCount,damagePerPress,perfectPressThreshold,perfectDamage);
        public static int CalculateBeamDamage(int presses,int perPress,int threshold,int maximum)
            => presses>threshold?maximum:(int)Math.Min(int.MaxValue,(long)Math.Max(0,presses)*Math.Max(0,perPress));
        public float Charge => Mathf.Clamp01(PressCount/(float)Mathf.Max(1,fullChargePresses));
        // Reward design pending: zero presses still wins; consume PressCount/Charge in future reward calculation.
        public bool LowParticipation => PressCount<fullChargePresses;
        public int CompletedPressCount {get;private set;}
        public float CompletedCharge {get;private set;}
        public bool Accepting => Active&&!Fired&&!dying&&CanRun;
        bool CanRun => fight&&fight.BossPhaseTwoActive&&!fight.BattleEnded&&!fight.DefeatSlowMotionActive&&!fight.IsPaused&&!fight.TimingCalibrationActive;
        bool dying;float deathBeats;double beat,lastPress=double.NegativeInfinity;
        Transform[] heroes;Vector3[] heroFrom,heroFootFrom;
        GameObject gem,beam,prompt;Vector3 gemBaseScale;
        Light gemLight;FightVfxOpacity beamOpacity;FightGemBeamVolume beamVolume;Vector3 promptBaseScale;
        FightGemFinaleGlare glare;
        UnityEngine.UI.Image gemAboveGlare,promptAboveGlare;
        UnityEngine.UI.Text perfectLabel;
        GameObject chargingShell;Material chargingShellMaterial;double perfectAt=double.NegativeInfinity;
        Transform[] heroShells;LineRenderer[] chargingLinks;Material chargingLinkMaterial;
        int lastDamageBeat=int.MinValue;
        public int BeamDamagePulses {get;private set;}
        public bool HasDisplayedBeamDamageThisBeat => lastDamageBeat==(int)Math.Floor(beat);
        sealed class Burst {public GameObject go;public ParticleSystem[] particles;public float remaining;}
        readonly List<Burst> bursts=new();
        ParticleSystem[] beamParticles=Array.Empty<ParticleSystem>();
        void OnEnable()
        {
            if(!fight)fight=GetComponent<FightCombatController>();
            if(!rhythm)rhythm=GetComponent<FightBossRhythmBattle>();
            if(fight)fight.BossPhaseTwoStarted+=Reset;
        }
        public bool WillReplaceNote(double arrival)=>finaleEnabled&&arrival>=startBeat;
        public bool Advance(double currentBeat)
        {
            if(!CanRun||!finaleEnabled)return Active;
            beat=currentBeat;
            if(!Active && beat>=startBeat && GetComponent<FightBossPhaseTwoTransition>().Ready)Begin();
            if(Active&&!Fired&&beat>=fireBeat)Fire();
            return Active;
        }
        void Begin()
        {
            Active=true;PressCount=0;CompletedPressCount=0;CompletedCharge=0;Fired=dying=false;deathBeats=0;
            lastDamageBeat=int.MinValue;
            BeamDamagePulses=0;
            perfectAt=double.NegativeInfinity;
            rhythm.EnterGemFinale();
            var slots=new[]{fight.FrontHero.UnitSlot,fight.SecondHero.UnitSlot,fight.ThirdHero.UnitSlot};
            heroes=new Transform[3];heroFrom=new Vector3[3];heroFootFrom=new Vector3[3];
            for(int i=0;i<3;i++)
            {
                heroes[i]=slots[i].ActorRoot;heroFrom[i]=heroes[i].position;
                var foot=slots[i].ActorInstance.transform.Find("FootAnchor");
                heroFootFrom[i]=foot?foot.position:heroFrom[i];
            }
            finaleCamera=view?view.GetComponent<Art2.Art2CameraController>():null;
            if(gemPrefab)
            {
                gem=Instantiate(gemPrefab,transform);gem.name="FinaleWorldGem_Runtime";
                var sprite=gem.GetComponentInChildren<SpriteRenderer>();
                if(sprite)
                {
                    (sprite.GetComponent<SceneBeatSpriteAnimator>()??sprite.gameObject.AddComponent<SceneBeatSpriteAnimator>()).Configure(fight.BeatClock,gemFrames,gemLoopBeats);
                    gemBaseScale=Vector3.one*gemHeight/Mathf.Max(.01f,sprite.sprite?sprite.sprite.bounds.size.y:1);
                    gem.transform.localScale=gemBaseScale;
                }
                else gemBaseScale=gem.transform.localScale;
                gemLight=new GameObject("FinaleGemBlueLight").AddComponent<Light>();gemLight.transform.SetParent(gem.transform,false);
                gemLight.type=LightType.Point;gemLight.shadows=LightShadows.None;gemLight.color=gemLightColor;gemLight.range=lightRange;
                prompt=new GameObject("Finale_A_Prompt");prompt.transform.SetParent(transform,false);
                if(promptSprite){var icon=prompt.AddComponent<SpriteRenderer>();icon.sprite=promptSprite;icon.sortingOrder=60;prompt.transform.localScale=Vector3.one*promptHeight/Mathf.Max(.01f,promptSprite.bounds.size.y);}
                else
                {
                    var text=prompt.AddComponent<TextMesh>();text.text="A";text.fontSize=64;text.characterSize=.045f;
                    text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=Color.white;
                    text.GetComponent<MeshRenderer>().sortingOrder=60;
                }
                promptBaseScale=prompt.transform.localScale;
                if(chargingShellEnabled&&chargingShellShader)
                {
                    chargingShell=new GameObject("FinaleChargingEnergy");
                    chargingShell.transform.SetParent(transform,false);
                    chargingShellMaterial=new Material(chargingShellShader);
                    if(chargingLinkShader)chargingLinkMaterial=new Material(chargingLinkShader);
                    heroShells=new Transform[3];chargingLinks=new LineRenderer[3];
                    for(int i=0;i<3;i++)
                    {
                        var shell=GameObject.CreatePrimitive(PrimitiveType.Sphere);shell.name="HeroEnergy_"+i;shell.transform.SetParent(chargingShell.transform,false);
                        var collider=shell.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
                        var renderer=shell.GetComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                        renderer.sharedMaterial=chargingShellMaterial;heroShells[i]=shell.transform;
                        if(chargingLinkMaterial)
                        {
                            var link=new GameObject("GemEnergyLink_"+i).AddComponent<LineRenderer>();link.transform.SetParent(chargingShell.transform,false);
                            link.sharedMaterial=chargingLinkMaterial;link.positionCount=2;link.useWorldSpace=true;link.alignment=LineAlignment.View;
                            link.shadowCastingMode=ShadowCastingMode.Off;link.receiveShadows=false;link.numCapVertices=3;chargingLinks[i]=link;
                        }
                    }
                }
            }
            Place();
        }
        public void Press()
        {
            if(!Accepting)return;
            PressCount++;lastPress=beat;
            if(PressCount==perfectPressThreshold+1)perfectAt=beat;
            fight.AudioPlayer.PlayBeatSound(fight.BossHitSoundPath,fight.BossHitSoundVolume,0,false,0);
            if(!gem||!hitEffectPrefab)return;
            Burst burst=bursts.Find(b=>!b.go.activeSelf);
            if(burst==null)
            {
                var go=Instantiate(hitEffectPrefab,transform);go.name="FinaleGemHit_Pooled";
                PrepareEffects(go);burst=new Burst{go=go,particles=go.GetComponentsInChildren<ParticleSystem>(true)};bursts.Add(burst);
            }
            burst.go.transform.SetPositionAndRotation(gem.transform.position+(view?view.transform.forward:Vector3.forward)*.05f,view?view.transform.rotation:Quaternion.identity);
            burst.go.transform.localScale=hitEffectPrefab.transform.localScale*hitEffectScale;
            burst.remaining=hitEffectLifetime;burst.go.SetActive(true);
            foreach(var p in burst.particles){p.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);p.Play(false);}
        }
        static void PrepareEffects(GameObject root)
        {
            foreach(var effect in root.GetComponentsInChildren<CartoonFX.CFXR_Effect>(true))effect.clearBehavior=CartoonFX.CFXR_Effect.ClearBehavior.None;
            foreach(var p in root.GetComponentsInChildren<ParticleSystem>(true)){var m=p.main;m.stopAction=ParticleSystemStopAction.None;}
        }
        void Fire()
        {
            Fired=true;if(prompt)prompt.SetActive(false);
            if(chargingShell)chargingShell.SetActive(false);
            if(!beamPrefab||!gem)return;
            var boss=rhythm.Boss;
            if(boss&&boss.ActorRoot)
            {
                impactRoot=boss.ActorRoot;impactPosition=impactRoot.position;lastImpactBeat=int.MinValue;
                impactFlash=boss.GetComponentInChildren<FightHitFlashController>(true);
                if(!impactFlash&&boss.CombatAnimator&&boss.CombatAnimator.TargetRenderer)
                {
                    impactFlash=boss.gameObject.AddComponent<FightHitFlashController>();
                    impactFlash.Configure(new[]{boss.CombatAnimator.TargetRenderer},view);
                }
            }
            Vector3 destination=boss&&boss.ActorRoot?boss.ActorRoot.position:gem.transform.position+Vector3.forward*10;
            Vector3 origin=gem.transform.position+beamOffset;
            beam=Instantiate(beamPrefab,origin,Quaternion.LookRotation((destination-origin).normalized,Vector3.up)*Quaternion.Euler(beamRotationOffset));
            beam.name="FinaleGemUltRay_Runtime";PrepareEffects(beam);
            if(suppressBeamLongSparks)
                foreach(var particle in beam.GetComponentsInChildren<ParticleSystem>(true))
                    if(particle.name=="Sparks around")particle.gameObject.SetActive(false);
            // Preserve the authored geometry: nonuniform hierarchy scaling changes Unity's
            // stretched billboard length and pivot, so weak charge is represented by radiance.
            beam.transform.localScale=beamPrefab.transform.localScale*beamScale;
            var group=beam.GetComponent<SortingGroup>();if(!group)group=beam.AddComponent<SortingGroup>();
            group.sortingOrder=beamSortingOrder;
            if(boss&&boss.CombatAnimator&&boss.CombatAnimator.TargetRenderer)group.sortingLayerID=boss.CombatAnimator.TargetRenderer.sortingLayerID;
            foreach(var r in beam.GetComponentsInChildren<ParticleSystemRenderer>(true))r.sortingOrder=0;
            if(beamVolumeEnabled&&beamVolumeShader)
            {
                beamVolume=beam.AddComponent<FightGemBeamVolume>();
                beamVolume.Initialize(beamVolumeShader,Vector3.Distance(origin,destination)/Mathf.Max(.01f,beamScale)+1,beamVolumeRadius);
            }
            beamOpacity=beam.GetComponent<FightVfxOpacity>()??beam.AddComponent<FightVfxOpacity>();
            beamOpacity.SetOpacity(Mathf.Lerp(weakBeamOpacity,1,Charge));
            // Disable soft intersection fading on these per-instance materials so the Boss
            // silhouette cannot punch a hole through the final beam.
            foreach(var r in beam.GetComponentsInChildren<ParticleSystemRenderer>(true))foreach(var mat in r.sharedMaterials)
                if(mat){mat.DisableKeyword("_FADING_ON");if(mat.HasProperty("_UseSP"))mat.SetFloat("_UseSP",0);}
            (beam.GetComponent<Art2.Art2EffectPresentation>()??beam.AddComponent<Art2.Art2EffectPresentation>()).ConfigureCinematicOverlay();
            beamParticles=beam.GetComponentsInChildren<ParticleSystem>(false);
            foreach(var p in beamParticles){p.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);p.Play(false);}
        }
        public void BossDied()
        {
            ClearBossImpact();
            if(!Active)return;
            // Death remains exclusively owned by the existing song-end execution.
            CompletedPressCount=PressCount;CompletedCharge=Charge;
            dying=true;deathBeats=0;if(prompt)prompt.SetActive(false);
        }
        void Place()
        {
            if(heroes==null)return;
            float gather=Mathf.SmoothStep(0,1,Mathf.Clamp01((float)(beat-startBeat)/Mathf.Max(.01f,gatherBeats)));
            for(int i=0;i<3;i++)if(heroes[i])
            {
                // Different characters have different root-to-foot offsets. Gather their
                // feet on XZ, preserving each actor's grounded height and authored pivot.
                Vector3 targetFoot=Vector3.Lerp(heroFootFrom[i],heroFootFrom[1],gatherFraction);
                targetFoot.y=heroFootFrom[i].y;
                Vector3 target=targetFoot-(heroFootFrom[i]-heroFrom[i]);
                if(i!=1)target+=sideHeroGatherOffset;
                heroes[i].position=Vector3.Lerp(heroFrom[i],target,gather);
            }
            if(!gem)return;
            float enter=Mathf.Clamp01((float)(beat-startBeat)/Mathf.Max(.01f,gemEntryBeats));
            Vector3 landing=heroFrom[1]+gemOffset;
            Vector3 entry=landing+gemEntryOffset;
            if(gemEnterFromScreenTop&&view)
            {
                Vector3 screen=view.WorldToViewportPoint(landing);
                if(screen.z>0)
                {
                    screen.y=1+gemEntryViewportMargin;
                    entry=view.ViewportToWorldPoint(screen);
                }
            }
            gem.transform.position=Vector3.Lerp(landing,entry,Mathf.Pow(1-enter,3));
            if(view)gem.transform.rotation=view.transform.rotation;
            float bounceT=Mathf.Clamp01((float)(beat-lastPress)/Mathf.Max(.01f,bounceBeats));
            float pulse=1+bounceAmount*Mathf.Cos(bounceT*Mathf.PI*2)*Mathf.Pow(1-bounceT,2);
            gem.transform.localScale=gemBaseScale*pulse;
            if(chargingShell)
            {
                chargingShell.SetActive(chargingShellEnabled&&!Fired&&!dying);
                float opacity=chargingShellOpacity*Mathf.Clamp01((float)(beat-startBeat)/.5f);
                chargingShellMaterial.SetVector("_Charge",new Vector4((float)beat,opacity,Charge,0));
                Quaternion facing=view?view.transform.rotation:Quaternion.identity;
                for(int i=0;i<3;i++)
                {
                    if(!heroes[i])continue;
                    Vector3 foot=heroes[i].position+heroFootFrom[i]-heroFrom[i];
                    Vector3 center=foot+facing*chargingShellCenterOffset;
                    heroShells[i].SetPositionAndRotation(center,facing);heroShells[i].localScale=chargingShellSize;
                    var link=chargingLinks[i];if(!link)continue;
                    link.SetPosition(0,center);link.SetPosition(1,gem.transform.position);
                    link.widthMultiplier=chargingLinkWidth;
                    Color tint=chargingLinkColor;tint.a*=Mathf.Clamp01((float)(beat-startBeat)/.5f);
                    link.startColor=link.endColor=tint;
                }
            }
            float fade=dying?1-Mathf.Clamp01(deathBeats/Mathf.Max(.01f,beamFadeBeats)):1;
            gemLight.intensity=Mathf.Lerp(lightIntensity.x,lightIntensity.y,Charge)*fade;gemLight.color=gemLightColor;gemLight.range=lightRange;
            if(prompt)
            {
                double phase=beat-Math.Floor(beat);
                float pulseA=1+promptPulseAmount*Mathf.Sin(Mathf.Clamp01((float)phase/Mathf.Max(.01f,promptPulseBeats))*Mathf.PI);
                prompt.transform.position=gem.transform.position+promptOffset;prompt.transform.localScale=promptBaseScale*pulseA;
                if(view)prompt.transform.rotation=view.transform.rotation;
            }
            if(beamVolume)beamVolume.Present((float)beat,Mathf.Lerp(weakBeamOpacity,1,Charge)*fade);
        }
        void LateUpdate()
        {
            if(!Active)return;
            if(!fight||!fight.BossPhaseTwoActive||fight.BattleEnded||fight.DefeatSlowMotionActive){Reset();return;}
            bool paused=fight.IsPaused||fight.TimingCalibrationActive;
            foreach(var p in beamParticles)if(p){if(paused&&p.isPlaying)p.Pause(false);else if(!paused&&p.isPaused)p.Play(false);}
            foreach(var burst in bursts)
            {
                if(!burst.go.activeSelf)continue;
                foreach(var p in burst.particles)if(p){if(paused&&p.isPlaying)p.Pause(false);else if(!paused&&p.isPaused)p.Play(false);}
                if(!paused){burst.remaining-=Time.unscaledDeltaTime;if(burst.remaining<=0)burst.go.SetActive(false);}
            }
            if(paused)return;
            if(dying)
            {
                // Music has stopped; finish one beat using the final tempo, while respecting pause.
                deathBeats+=Time.unscaledDeltaTime*1000f/Mathf.Max(1,(float)fight.BeatClock.MillisecondsPerBeat);
                if(beamOpacity)beamOpacity.SetOpacity(Mathf.Lerp(weakBeamOpacity,1,Charge)*(1-Mathf.Clamp01(deathBeats/Mathf.Max(.01f,beamFadeBeats))));
                if(deathBeats>=beamFadeBeats&&beam){Destroy(beam);beam=null;beamParticles=Array.Empty<ParticleSystem>();}
            }
            Place();
            PresentGlare();
            PresentBossImpact();
            if(finaleCamera)
            {
                float weight=Fired&&beam&&beamCameraShakeEnabled?1:0;
                if(dying)weight*=1-Mathf.Clamp01(deathBeats/Mathf.Max(.01f,beamFadeBeats));
                float phase=(float)(beat-fireBeat+deathBeats)*Mathf.PI*2*beamCameraShakeCyclesPerBeat;
                finaleCamera.SetGemFinaleShake(Vector2.Scale(beamCameraShakeAmplitude,new Vector2(Mathf.Sin(phase),Mathf.Sin(phase*1.27f+.6f)))*weight);
            }
        }
        void PresentBossImpact()
        {
            if(!Fired||dying||!impactRoot)return;
            int current=(int)Math.Floor(beat-fireBeat);
            if(current!=lastImpactBeat)
            {
                lastImpactBeat=current;
                if(rhythm.Boss&&rhythm.Boss.CurrentHp>0)
                {
                    // Phase two HP/death belongs to the song timeline. The sustained beam
                    // displays an overkill hit each beat without executing the Boss early.
                    if(BeamDamage>0)fight.ShowEnemyDamageNumber(rhythm.Boss,BeamDamage);
                    BeamDamagePulses++;
                    lastDamageBeat=(int)Math.Floor(beat);
                }
                if(bossImpactFlashEnabled&&impactFlash)impactFlash.TriggerPresentationFlash(bossImpactFlashStrength,bossImpactFlashSeconds);
            }
            float phase=(float)(beat-fireBeat-current);
            float t=Mathf.Clamp01(phase/Mathf.Max(.01f,bossImpactShakeBeats));
            Vector3 right=view?view.transform.right:Vector3.right;
            impactRoot.position=impactPosition+right*(bossImpactShakeEnabled?Mathf.Sin(t*Mathf.PI*6)*(1-t)*bossImpactShakeAmount:0);
        }
        void ClearBossImpact()
        {
            if(impactRoot)impactRoot.position=impactPosition;
            if(impactFlash)impactFlash.ClearFlash();
            impactRoot=null;impactFlash=null;lastImpactBeat=int.MinValue;
        }
        void PresentGlare()
        {
            if(!glare)
            {
                var root=new GameObject("FinaleCameraGlare",typeof(RectTransform),typeof(Canvas));
                root.transform.SetParent(transform,false);
                var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=90;
                var graphic=new GameObject("Glare",typeof(RectTransform),typeof(CanvasRenderer),typeof(FightGemFinaleGlare));
                graphic.transform.SetParent(root.transform,false);glare=graphic.GetComponent<FightGemFinaleGlare>();
                var rect=glare.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                glare.raycastTarget=false;
                gemAboveGlare=CreateForegroundSprite(root.transform,"GemAboveGlare");
                promptAboveGlare=CreateForegroundSprite(root.transform,"PromptAboveGlare");
                var label=new GameObject("FinalePerfect",typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Text));
                label.transform.SetParent(root.transform,false);perfectLabel=label.GetComponent<UnityEngine.UI.Text>();
                perfectLabel.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");perfectLabel.text="Perfect";
                perfectLabel.fontStyle=FontStyle.Bold;perfectLabel.fontSize=48;perfectLabel.alignment=TextAnchor.MiddleCenter;
                perfectLabel.color=new Color(1,.76f,.15f);perfectLabel.raycastTarget=false;
                var labelRect=perfectLabel.rectTransform;labelRect.anchorMin=new Vector2(.7f,.45f);labelRect.anchorMax=new Vector2(.97f,.65f);labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
                var outline=label.AddComponent<UnityEngine.UI.Outline>();outline.effectColor=new Color(.22f,.09f,0,.85f);outline.effectDistance=new Vector2(2,-2);
            }
            if(!glare)return;
            float fade=dying?1-Mathf.Clamp01(deathBeats/Mathf.Max(.01f,beamFadeBeats)):1;
            float flash=0;
            if(fireFlashEnabled&&!dying)
            {
                double time=beat-fireBeat;
                flash=time<0?Mathf.Clamp01(1+(float)time/fireFlashLeadBeats):1-Mathf.Clamp01((float)time/fireFlashFadeBeats);
                flash*=fireFlashStrength;
            }
            Vector3 center=gem&&view?view.WorldToViewportPoint(gem.transform.position):new Vector3(.5f,.5f,1);
            // Keep the charge glow briefly behind the beam, then release it so play remains readable.
            float release=Fired?1-Mathf.Clamp01((float)(beat-fireBeat)/2):1;
            glare.Present(center,screenGlareEnabled?Charge*release*fade:0,screenBrightness,gemGlareStrength,flash);
            SyncForegroundSprite(gemAboveGlare,gem);
            SyncForegroundSprite(promptAboveGlare,prompt);
            perfectLabel.enabled=IsPerfect&&!dying;
            float pop=1+.3f*Mathf.Pow(1-Mathf.Clamp01((float)(beat-perfectAt)/.5f),2);
            perfectLabel.rectTransform.localScale=Vector3.one*pop;
        }
        static UnityEngine.UI.Image CreateForegroundSprite(Transform parent,string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent,false);
            var image=go.GetComponent<UnityEngine.UI.Image>();image.raycastTarget=false;return image;
        }
        void SyncForegroundSprite(UnityEngine.UI.Image image,GameObject source)
        {
            if(!image)return;
            var renderer=source?source.GetComponentInChildren<SpriteRenderer>():null;
            bool visible=renderer&&renderer.enabled&&renderer.gameObject.activeInHierarchy&&renderer.sprite&&view;
            image.enabled=visible;if(!visible)return;
            Bounds b=renderer.sprite.bounds;
            Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
            for(int i=0;i<4;i++)
            {
                Vector3 p=b.center+new Vector3((i&1)==0?-b.extents.x:b.extents.x,(i&2)==0?-b.extents.y:b.extents.y,0);
                var v=view.WorldToViewportPoint(renderer.transform.TransformPoint(p));
                if(v.z<=0){image.enabled=false;return;}
                min=Vector2.Min(min,v);max=Vector2.Max(max,v);
            }
            image.sprite=renderer.sprite;image.color=renderer.color;
            var rect=image.rectTransform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
        void Reset()
        {
            if(finaleCamera)finaleCamera.SetGemFinaleShake(Vector2.zero);finaleCamera=null;
            ClearBossImpact();
            if(glare)Destroy(glare.transform.parent.gameObject);glare=null;
            gemAboveGlare=promptAboveGlare=null;
            perfectLabel=null;
            if(chargingShell)Destroy(chargingShell);if(chargingShellMaterial)Destroy(chargingShellMaterial);
            chargingShell=null;chargingShellMaterial=null;
            if(chargingLinkMaterial)Destroy(chargingLinkMaterial);chargingLinkMaterial=null;heroShells=null;chargingLinks=null;
            if(gem)Destroy(gem);if(beam)Destroy(beam);if(prompt)Destroy(prompt);
            foreach(var burst in bursts)if(burst.go)Destroy(burst.go);bursts.Clear();
            gem=beam=prompt=null;beamVolume=null;gemLight=null;beamOpacity=null;beamParticles=Array.Empty<ParticleSystem>();
            Active=Fired=dying=false;PressCount=0;heroes=null;lastPress=double.NegativeInfinity;
        }
        void OnDisable(){if(fight)fight.BossPhaseTwoStarted-=Reset;Reset();}
    }
}
