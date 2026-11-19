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
        [Header("Beam: local +Z points to Boss")]
        [SerializeField] private GameObject beamPrefab;
        [SerializeField,Min(.1f)] private float beamScale=1.35f;
        [SerializeField] private bool beamVolumeEnabled=true;
        [SerializeField] private Shader beamVolumeShader;
        [SerializeField,Min(.01f)] private float beamVolumeRadius=.95f;
        [SerializeField] private int beamSortingOrder=20;
        [SerializeField] private Vector3 beamOffset=Vector3.zero;
        [SerializeField] private Vector3 beamRotationOffset=Vector3.zero;
        [SerializeField,Range(0,1)] private float weakBeamOpacity=.16f;
        [SerializeField,Min(.01f)] private float beamFadeBeats=1;
        public bool Active {get;private set;}
        public bool Fired {get;private set;}
        public int PressCount {get;private set;}
        public float Charge => Mathf.Clamp01(PressCount/(float)Mathf.Max(1,fullChargePresses));
        // Reward design pending: zero presses still wins; consume PressCount/Charge in future reward calculation.
        public bool LowParticipation => PressCount<fullChargePresses;
        public int CompletedPressCount {get;private set;}
        public float CompletedCharge {get;private set;}
        public bool Accepting => Active&&!Fired&&!dying&&CanRun;
        bool CanRun => fight&&fight.BossPhaseTwoActive&&!fight.BattleEnded&&!fight.DefeatSlowMotionActive&&!fight.IsPaused&&!fight.TimingCalibrationActive;
        bool dying;float deathBeats;double beat,lastPress=double.NegativeInfinity;
        Transform[] heroes;Vector3[] heroFrom;
        GameObject gem,beam,prompt;Vector3 gemBaseScale;
        Light gemLight;FightVfxOpacity beamOpacity;FightGemBeamVolume beamVolume;Vector3 promptBaseScale;
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
            rhythm.EnterGemFinale();
            var slots=new[]{fight.FrontHero.UnitSlot,fight.SecondHero.UnitSlot,fight.ThirdHero.UnitSlot};
            heroes=new Transform[3];heroFrom=new Vector3[3];
            for(int i=0;i<3;i++){heroes[i]=slots[i].ActorRoot;heroFrom[i]=heroes[i].position;}
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
            }
            Place();
        }
        public void Press()
        {
            if(!Accepting)return;
            PressCount++;lastPress=beat;
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
            if(!beamPrefab||!gem)return;
            var boss=rhythm.Boss;
            Vector3 destination=boss&&boss.ActorRoot?boss.ActorRoot.position:gem.transform.position+Vector3.forward*10;
            Vector3 origin=gem.transform.position+beamOffset;
            beam=Instantiate(beamPrefab,origin,Quaternion.LookRotation((destination-origin).normalized,Vector3.up)*Quaternion.Euler(beamRotationOffset));
            beam.name="FinaleGemUltRay_Runtime";PrepareEffects(beam);
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
            beamParticles=beam.GetComponentsInChildren<ParticleSystem>(true);
            foreach(var p in beamParticles){p.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);p.Play(false);}
        }
        public void BossDied()
        {
            if(!Active)return;
            // Death remains exclusively owned by the existing song-end execution.
            CompletedPressCount=PressCount;CompletedCharge=Charge;
            dying=true;deathBeats=0;if(prompt)prompt.SetActive(false);
        }
        void Place()
        {
            if(heroes==null)return;
            float gather=Mathf.SmoothStep(0,1,Mathf.Clamp01((float)(beat-startBeat)/Mathf.Max(.01f,gatherBeats)));
            for(int i=0;i<3;i++)if(heroes[i])heroes[i].position=Vector3.Lerp(heroFrom[i],Vector3.Lerp(heroFrom[i],heroFrom[1],gatherFraction),gather);
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
            float pulse=1+bounceAmount*Mathf.Sin(Mathf.Clamp01((float)(beat-lastPress)/Mathf.Max(.01f,bounceBeats))*Mathf.PI);
            gem.transform.localScale=gemBaseScale*pulse;
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
        }
        void Reset()
        {
            if(gem)Destroy(gem);if(beam)Destroy(beam);if(prompt)Destroy(prompt);
            foreach(var burst in bursts)if(burst.go)Destroy(burst.go);bursts.Clear();
            gem=beam=prompt=null;beamVolume=null;gemLight=null;beamOpacity=null;beamParticles=Array.Empty<ParticleSystem>();
            Active=Fired=dying=false;PressCount=0;heroes=null;lastPress=double.NegativeInfinity;
        }
        void OnDisable(){if(fight)fight.BossPhaseTwoStarted-=Reset;Reset();}
    }
}
