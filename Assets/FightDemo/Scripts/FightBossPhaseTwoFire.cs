using UnityEngine;
using UnityEngine.Rendering;
namespace RhythmHunter.FightDemo
{
    /// <summary>Presentation only; follows phase-two music progress without changing combat.</summary>
    [DisallowMultipleComponent]
    public sealed class FightBossPhaseTwoFire : MonoBehaviour
    {
        [SerializeField] private FightCombatController fight;
        [SerializeField] private Camera view;
        [SerializeField] private GameObject firePrefab;
        [SerializeField] private bool effectEnabled=true;
        [Tooltip("Offset from Boss: X along camera right, Y world height, Z camera depth. Fire stays upright in world space.")]
        [SerializeField] private Vector3 offset=new(0,0,.1f);
        [SerializeField] private Vector2 scaleRange=new(.45f,.7f);
        [SerializeField] private Vector2 opacityRange=new(.12f,.7f);
        [SerializeField] private AnimationCurve growth=AnimationCurve.EaseInOut(0,0,1,1);
        [SerializeField,Min(.01f)] private float fadeInSeconds=1.2f;
        [Tooltip("Within the Boss sorting group, keep the flame behind its sprite without moving it behind the cave.")]
        [SerializeField,Min(1)] private int sortingOrdersBehind=1;
        [Tooltip("Art2 cave cards use order 0. Keep the Boss group just above them; its internal flame stays behind the Boss.")]
        [SerializeField] private int bossGroupOrder=1;
        SortingGroup ownedSortingGroup;
        [Tooltip("Local point on the VFX that should stay at the Boss feet as the flame grows.")]
        [SerializeField] private Vector3 flameBaseLocalPoint;
        Transform bossFoot;
        FightBossPhaseTwoTransition transition;
        GameObject instance; FightVfxOpacity opacity; FightUnitSlot boss; Vector3 baseScale;
        ParticleSystem[] particles; float fade; bool ended,paused;
        void OnEnable()
        {
            if(!fight)fight=GetComponent<FightCombatController>();
            transition=GetComponent<FightBossPhaseTwoTransition>();
            if(fight){fight.BossPhaseTwoStarted+=Begin;fight.BossPhaseTwoMusicEnded+=End;}
        }
        void Begin(){Clear();ended=false;}
        void End(){ended=true;Clear();}
        void LateUpdate()
        {
            if(!fight||!effectEnabled||!fight.BossPhaseTwoActive||fight.BattleEnded||fight.DefeatSlowMotionActive||ended){Clear();return;}
            if(!view||!firePrefab)return;
            if(!transition || (transition.Phase!=FightBossPhaseTwoTransition.TransitionPhase.HeroesJump && !transition.Ready))return;
            if(!boss)foreach(var enemy in fight.RosterManager.ActiveEnemies)if(enemy&&enemy.BossPhaseOneProtected){boss=enemy;break;}
            if(!boss||!boss.ActorRoot){Clear();return;}
            if(!bossFoot && boss.ActorInstance)bossFoot=boss.ActorInstance.transform.Find("FootAnchor");
            if(!instance)
            {
                var bossSprite=boss.CombatAnimator?boss.CombatAnimator.TargetRenderer:null;
                var group=boss.ActorRoot.GetComponent<SortingGroup>();
                if(!group)
                {
                    group=boss.ActorRoot.gameObject.AddComponent<SortingGroup>();ownedSortingGroup=group;
                    if(bossSprite){group.sortingLayerID=bossSprite.sortingLayerID;group.sortingOrder=bossGroupOrder;}
                }
                instance=Instantiate(firePrefab); instance.name="BossPhaseTwo_FiredEffect";
                instance.transform.SetParent(boss.ActorRoot,true);
                baseScale=firePrefab.transform.localScale;opacity=instance.GetComponent<FightVfxOpacity>();
                if(!opacity)opacity=instance.AddComponent<FightVfxOpacity>();
                particles=instance.GetComponentsInChildren<ParticleSystem>(true);
                foreach(var p in particles){var m=p.main;m.scalingMode=ParticleSystemScalingMode.Hierarchy;m.simulationSpace=ParticleSystemSimulationSpace.Local;}
                opacity.SetOpacity(0);fade=0;
            }
            bool freeze=fight.IsPaused||fight.TimingCalibrationActive;
            if(freeze!=paused){foreach(var p in particles)if(p){if(freeze)p.Pause(false);else p.Play(false);}paused=freeze;}
            if(freeze)return;
            float progress=1-fight.BossPhaseTwoSecondsRemaining/Mathf.Max(.01f,fight.BossPhaseTwoDurationSeconds);
            float t=Mathf.Clamp01(growth.Evaluate(Mathf.Clamp01(progress)));
            fade=Mathf.MoveTowards(fade,1,Time.unscaledDeltaTime/Mathf.Max(.01f,fadeInSeconds));
            instance.transform.rotation=firePrefab.transform.rotation;
            var worldScale=baseScale*Mathf.Max(.01f,Mathf.Lerp(scaleRange.x,scaleRange.y,t));
            var parentScale=instance.transform.parent.lossyScale;
            instance.transform.localScale=new Vector3(worldScale.x/Mathf.Max(.0001f,Mathf.Abs(parentScale.x)),worldScale.y/Mathf.Max(.0001f,Mathf.Abs(parentScale.y)),worldScale.z/Mathf.Max(.0001f,Mathf.Abs(parentScale.z)));
            var feet=bossFoot?bossFoot.position:boss.ActorRoot.position;
            instance.transform.position=feet+view.transform.right*offset.x+Vector3.up*offset.y+view.transform.forward*offset.z
                -instance.transform.TransformVector(flameBaseLocalPoint);
            opacity.SetOpacity(Mathf.Lerp(opacityRange.x,opacityRange.y,t)*fade);
            var sprite=boss.CombatAnimator?boss.CombatAnimator.TargetRenderer:null;
            if(sprite)foreach(var p in particles)if(p&&p.TryGetComponent<ParticleSystemRenderer>(out var r)){r.sortingLayerID=sprite.sortingLayerID;r.sortingOrder=sprite.sortingOrder-Mathf.Max(1,sortingOrdersBehind);}
        }
        void Clear(){if(instance)Destroy(instance);if(ownedSortingGroup)Destroy(ownedSortingGroup);ownedSortingGroup=null;instance=null;opacity=null;boss=null;bossFoot=null;particles=null;fade=0;paused=false;}
        void OnDisable(){if(fight){fight.BossPhaseTwoStarted-=Begin;fight.BossPhaseTwoMusicEnded-=End;}Clear();}
    }
}
