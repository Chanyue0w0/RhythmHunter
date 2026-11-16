using UnityEngine;

namespace RhythmHunter.FightDemo
{
    // A one-shot adapter; frame timing remains owned by the existing combat animator.
    [DefaultExecutionOrder(260), RequireComponent(typeof(FightCharacterCombatAnimator))]
    public sealed class FightBeatSpriteEffect : MonoBehaviour
    {
        [SerializeField] private string animationName = "Howl";
        [SerializeField] private bool drawInFrontOfCaster;
        [SerializeField, Min(.001f)] private float foregroundClearance = .05f;
        private SpriteRenderer caster;
        private Camera view;
        private Vector3 depthOffset;
        private float depthScale = 1;
        public void BindCaster(SpriteRenderer renderer)
        {
            caster=renderer;
            if(!drawInFrontOfCaster || !caster) return;
            var cameraRig=FindFirstObjectByType<Art2.Art2CameraController>();
            view=cameraRig ? cameraRig.GetComponent<Camera>() : Camera.main;
        }
        private FightCharacterCombatAnimator animator;
        private FightCombatController fight;
        private void Start()
        {
            animator=GetComponent<FightCharacterCombatAnimator>();
            fight=FindFirstObjectByType<FightCombatController>();
            animator.BindBeatSource(fight);
            var sequence=animator.FindSequence(animationName);
            if(sequence==null) { Destroy(gameObject); return; }
            animator.PlayVisualSequence(animator.TargetRenderer,sequence,false);
        }
        private void Update()
        {
            if(!animator) return;
            animator.SetPlaybackPaused(fight && (fight.IsPaused || fight.TimingCalibrationActive));
            if(animator.VisualSequenceComplete) Destroy(gameObject);
        }
        private void LateUpdate()
        {
            if(!drawInFrontOfCaster || !caster || !view || !animator || !animator.TargetRenderer) return;
            transform.position-=depthOffset;
            transform.localScale/=depthScale;
            depthScale=1;
            var renderer=animator.TargetRenderer;
            renderer.sortingLayerID=caster.sortingLayerID;
            renderer.sortingOrder=Mathf.Max(renderer.sortingOrder,caster.sortingOrder+1);
            var forward=view.transform.forward;
            var absolute=new Vector3(Mathf.Abs(forward.x),Mathf.Abs(forward.y),Mathf.Abs(forward.z));
            // Move along the viewing axis, not upwards: keep the same screen position
            // while placing the complete effect in front of the caster's depth mask.
            float nearest=Vector3.Dot(caster.bounds.center,forward)-Vector3.Dot(caster.bounds.extents,absolute);
            float farthest=Vector3.Dot(renderer.bounds.center,forward)+Vector3.Dot(renderer.bounds.extents,absolute);
            float shift=Mathf.Max(0,farthest-nearest+foregroundClearance);
            depthOffset=-forward*shift;
            if(!view.orthographic && shift>0)
            {
                float cameraDepth=Vector3.Dot(view.transform.position,forward);
                depthScale=Mathf.Clamp((nearest-cameraDepth-foregroundClearance)/Mathf.Max(.001f,farthest-cameraDepth),.01f,1);
                // Perspective: scale about the camera so depth changes do not enlarge
                // the image or move it away from the authored screen-space anchor.
                depthOffset=(view.transform.position-transform.position)*(1-depthScale);
                transform.localScale*=depthScale;
            }
            transform.position+=depthOffset;
        }
    }
}
