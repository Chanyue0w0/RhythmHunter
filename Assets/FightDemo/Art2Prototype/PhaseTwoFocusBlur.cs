using UnityEngine;
namespace RhythmHunter.FightDemo.Art2
{
    [DisallowMultipleComponent]
    public sealed class PhaseTwoFocusBlur : MonoBehaviour
    {
        public static PhaseTwoFocusBlur Active { get; private set; }
        [SerializeField] private FightCombatController fight;
        [SerializeField] private FightBossPhaseTwoTransition transition;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Shader shader;
        [SerializeField] private bool blurEnabled=true;
        [SerializeField] private bool darkenOutsideEnabled=true;
        [SerializeField,Range(0,1)] private float outsideDarkness=.45f;
        [SerializeField,Range(0,12)] private float blurPixels=4;
        [SerializeField,Range(0,1)] private float strength=.85f;
        [SerializeField,Range(0,1)] private float centerX=.5f;
        [SerializeField,Range(0,1)] private float topWidth=.28f;
        [SerializeField,Range(0,1)] private float bottomWidth=.7f;
        [SerializeField,Range(0,1)] private float topY=.82f;
        [SerializeField,Range(0,1)] private float bottomY=.12f;
        [SerializeField,Range(.001f,.25f)] private float feather=.08f;
        [SerializeField,Min(.01f)] private float fadeSeconds=.7f;
        Material material;float weight;
        void OnEnable(){Active=this;if(!fight)fight=GetComponent<FightCombatController>();if(!transition)transition=GetComponent<FightBossPhaseTwoTransition>();}
        void Update()
        {
            if(fight&&(fight.IsPaused||fight.TimingCalibrationActive))return;
            bool active=(blurEnabled||darkenOutsideEnabled)&&fight&&fight.BossPhaseTwoActive&&!fight.BattleEnded&&!fight.DefeatSlowMotionActive&&transition&&transition.Ready;
            weight=Mathf.MoveTowards(weight,active?1:0,Time.unscaledDeltaTime/Mathf.Max(.01f,fadeSeconds));
        }
        public bool TryMaterial(Camera camera,out Material result)
        {
            result=null;if(!isActiveAndEnabled||camera!=targetCamera||weight<=0||!shader)return false;
            if(!material)material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            material.SetVector("_FocusShape",new Vector4(centerX,topWidth,bottomWidth,feather));
            material.SetVector("_FocusBlur",new Vector4(topY,bottomY,blurPixels,blurEnabled?strength*weight:0));
            material.SetFloat("_OutsideDarkness",darkenOutsideEnabled?outsideDarkness*weight:0);
            result=material;return true;
        }
        void OnDisable(){if(Active==this)Active=null;weight=0;}
        void OnDestroy(){if(material)Destroy(material);}
    }
}
