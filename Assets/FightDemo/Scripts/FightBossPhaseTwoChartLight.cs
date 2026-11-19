using UnityEngine;
using UnityEngine.Rendering;
namespace RhythmHunter.FightDemo
{
    [DisallowMultipleComponent]
    public sealed class FightBossPhaseTwoChartLight : MonoBehaviour
    {
        [SerializeField] private FightCombatController fight;
        [SerializeField] private FightBossPhaseTwoTransition transition;
        [SerializeField] private Transform origin;
        [SerializeField] private Transform[] heroDestinations;
        [SerializeField] private Shader fanShader;
        [Header("Phase two chart spotlight (no shadows)")]
        [SerializeField] private bool lightingEnabled=true;
        [SerializeField] private bool spotEnabled=true;
        [SerializeField] private bool groundFanEnabled=true;
        [SerializeField] private Color lightColor=new(.65f,.8f,1,1);
        [SerializeField,Min(0)] private float intensity=2;
        [SerializeField,Min(.1f)] private float lightHeight=4;
        [SerializeField,Range(1,170)] private float spotAngle=65;
        [SerializeField,Min(1)] private float minimumRange=18;
        [Header("Ground chart area")]
        [SerializeField,Min(0)] private float extraLengthPastHeroes=3;
        [SerializeField,Min(1)] private float minimumLength=12;
        [SerializeField,Min(.1f)] private float startWidth=1.8f;
        [SerializeField,Min(.1f)] private float endWidth=9;
        [SerializeField,Min(0)] private float fanBrightness=.18f;
        [SerializeField] private float groundY=.025f;
        [SerializeField,Min(.01f)] private float fadeSeconds=.6f;
        [Header("Lane separators")]
        [SerializeField] private bool separatorsEnabled=true;
        [SerializeField] private Shader separatorShader;
        [SerializeField,Range(0,1)] private float separatorOpacity=.45f;
        [SerializeField,Min(.001f)] private float separatorWidth=.055f;
        [SerializeField,Min(0)] private float separatorStartSpread=.15f;
        MeshRenderer separators;Mesh separatorMesh;Material separatorMaterial;
        readonly Vector3[] separatorVertices=new Vector3[8];
        readonly float[] heroOffsets=new float[3];
        Light spot;MeshRenderer fan;Mesh mesh;Material material;GameObject visuals;
        readonly Vector3[] vertices=new Vector3[4];
        [SerializeField,Min(.01f)] private float extensionSeconds=.35f;
        [Header("Fire illumination")]
        [SerializeField] private bool fireFlickerEnabled=true;
        [SerializeField,Range(0,1)] private float fireFlickerAmount=.18f;
        [SerializeField,Min(0)] private float fireFlickerSpeed=3;
        [SerializeField] private Color fireWarmColor=new(1,.58f,.25f,1);
        [SerializeField,Range(0,1)] private float fireColorVariation=.2f;
        float fireTime;
        float extension;float weight;bool ended;
        void OnEnable(){if(fight){fight.BossPhaseTwoStarted+=Begin;fight.BossPhaseTwoMusicEnded+=End;}}
        void Begin(){ended=false;weight=0;extension=0;fireTime=0;}
        void End(){ended=true;}
        void CreateVisuals()
        {
            visuals=new GameObject("PhaseTwoChartLight_Runtime");visuals.transform.SetParent(transform,false);
            var lamp=new GameObject("BossChartSpotLight_NoShadows");lamp.transform.SetParent(visuals.transform,false);
            spot=lamp.AddComponent<Light>();spot.type=LightType.Spot;spot.shadows=LightShadows.None;
            if(separatorShader)
            {
                var lines=new GameObject("ChartLaneSeparators_NoShadows");lines.transform.SetParent(visuals.transform,false);
                separatorMesh=new Mesh{name="Chart lane separators"};separatorMesh.vertices=separatorVertices;
                separatorMesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one,Vector2.zero,Vector2.right,Vector2.up,Vector2.one};
                separatorMesh.triangles=new[]{0,2,1,1,2,3,4,6,5,5,6,7};
                lines.AddComponent<MeshFilter>().sharedMesh=separatorMesh;separators=lines.AddComponent<MeshRenderer>();
                separators.shadowCastingMode=ShadowCastingMode.Off;separators.receiveShadows=false;
                separatorMaterial=new Material(separatorShader);separators.sharedMaterial=separatorMaterial;
            }
            if(!fanShader)return;
            var plane=new GameObject("ChartGroundFan_NoShadows");plane.transform.SetParent(visuals.transform,false);
            mesh=new Mesh{name="Phase two chart fan"};mesh.vertices=vertices;
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};mesh.triangles=new[]{0,2,1,1,2,3};
            plane.AddComponent<MeshFilter>().sharedMesh=mesh;fan=plane.AddComponent<MeshRenderer>();
            fan.shadowCastingMode=ShadowCastingMode.Off;fan.receiveShadows=false;
            material=new Material(fanShader);fan.sharedMaterial=material;
        }
        void LateUpdate()
        {
            if(!fight||!transition||!origin)return;
            if(fight.IsPaused||fight.TimingCalibrationActive)return;
            bool settled=transition.Ready;
            bool active=lightingEnabled&&fight.BossPhaseTwoActive&&settled&&!ended&&!fight.BattleEnded&&!fight.DefeatSlowMotionActive;
            weight=Mathf.MoveTowards(weight,active?1:0,Time.unscaledDeltaTime/Mathf.Max(.01f,fadeSeconds));
            if(active)extension=Mathf.MoveTowards(extension,1,Time.unscaledDeltaTime/Mathf.Max(.01f,extensionSeconds));
            if(weight<=0){extension=0;if(visuals)visuals.SetActive(false);return;}
            if(!visuals)CreateVisuals();visuals.SetActive(true);
            fireTime+=Time.unscaledDeltaTime*fireFlickerSpeed;
            float noise=Mathf.PerlinNoise(fireTime,7.3f);
            float fireGain=fireFlickerEnabled?1+(noise*2-1)*fireFlickerAmount:1;
            Color fireColor=fireFlickerEnabled?Color.Lerp(lightColor,fireWarmColor,noise*fireColorVariation):lightColor;
            Vector3 target=Vector3.zero;int count=0;
            if(heroDestinations!=null)foreach(var t in heroDestinations)if(t){target+=t.position;count++;}
            target=count>0?target/count:origin.position+Vector3.back*10;
            Vector3 from=new(origin.position.x,groundY,origin.position.z);
            Vector3 direction=Vector3.ProjectOnPlane(target-from,Vector3.up).normalized;
            if(direction.sqrMagnitude<.01f)direction=Vector3.back;
            float furthest=Vector3.Dot(target-from,direction);
            if(heroDestinations!=null)foreach(var t in heroDestinations)if(t)furthest=Mathf.Max(furthest,Vector3.Dot(t.position-from,direction));
            float length=Mathf.Max(minimumLength,furthest+extraLengthPastHeroes);
            float reveal=Mathf.SmoothStep(0,1,extension);
            float shownLength=Mathf.Max(.001f,length*reveal);
            float shownWidth=Mathf.Lerp(startWidth,endWidth,reveal);
            spot.transform.position=from+Vector3.up*lightHeight;
            spot.transform.rotation=Quaternion.LookRotation(from+direction*shownLength*.65f-spot.transform.position,Vector3.up);
            spot.enabled=spotEnabled;spot.shadows=LightShadows.None;spot.color=fireColor;spot.intensity=intensity*weight*fireGain;
            spot.range=Mathf.Lerp(lightHeight,Mathf.Max(minimumRange,Mathf.Sqrt(length*length+lightHeight*lightHeight)+2),reveal);
            spot.spotAngle=spotAngle;spot.innerSpotAngle=spotAngle*.8f;
            if(separators)
            {
                separators.enabled=separatorsEnabled;
                separators.transform.SetPositionAndRotation(from+Vector3.up*.005f,Quaternion.LookRotation(direction,Vector3.up));
                Vector3 right=Vector3.Cross(Vector3.up,direction);
                int valid=0;
                if(heroDestinations!=null)foreach(var t in heroDestinations)if(t&&valid<3)heroOffsets[valid++]=Vector3.Dot(t.position-from,right);
                if(valid==3)System.Array.Sort(heroOffsets);
                for(int i=0;i<2;i++)
                {
                    float start=(i==0?-1:1)*separatorStartSpread;
                    float middle=valid==3?(heroOffsets[i]+heroOffsets[i+1])*.5f:(i==0?-1:1)*endWidth/6;
                    float end=start+(middle-start)*shownLength/Mathf.Max(.1f,Vector3.Dot(target-from,direction));
                    float half=separatorWidth*.5f;int v=i*4;
                    separatorVertices[v]=new(start-half,0,0);separatorVertices[v+1]=new(start+half,0,0);
                    separatorVertices[v+2]=new(end-half,0,shownLength);separatorVertices[v+3]=new(end+half,0,shownLength);
                }
                separatorMesh.vertices=separatorVertices;separatorMesh.RecalculateBounds();
                separatorMaterial.SetColor("_Color",new Color(0,0,0,separatorOpacity*weight));
            }
            if(fan)
            {
                fan.enabled=groundFanEnabled;fan.transform.SetPositionAndRotation(from,Quaternion.LookRotation(direction,Vector3.up));
                vertices[0]=new(-startWidth*.5f,0,0);vertices[1]=new(startWidth*.5f,0,0);
                vertices[2]=new(-shownWidth*.5f,0,shownLength);vertices[3]=new(shownWidth*.5f,0,shownLength);
                mesh.vertices=vertices;mesh.RecalculateBounds();
                material.SetColor("_Color",fireColor*(fanBrightness*weight*fireGain));
                material.SetVector("_FireMotion",new Vector4(fireTime,fireFlickerEnabled?fireFlickerAmount:0,0,0));
                material.SetVector("_DoorCone",Vector4.zero);
                // Do not match any scene light: this unshadowed ground overlay only marks the chart area.
                material.SetVector("_DoorLightPosition",new Vector4(100000,100000,100000,0));
            }
        }
        void OnDisable()
        {
            if(fight){fight.BossPhaseTwoStarted-=Begin;fight.BossPhaseTwoMusicEnded-=End;}
            weight=0;if(visuals)Destroy(visuals);if(mesh)Destroy(mesh);if(material)Destroy(material);
            if(separatorMesh)Destroy(separatorMesh);if(separatorMaterial)Destroy(separatorMaterial);
            separatorMesh=null;separatorMaterial=null;separators=null;
            visuals=null;mesh=null;material=null;
        }
    }
}
