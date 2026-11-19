using UnityEngine;
using UnityEngine.Rendering;
namespace RhythmHunter.FightDemo
{
    /// <summary>Actual world-space ribbons moving down a cone from the door to the viewing camera.</summary>
    public sealed class FightTunnelAirflow : MonoBehaviour
    {
        struct Streak { public float angle,t,radius,width,speed,alpha,presence;public bool retired; }
        readonly Streak[] streaks=new Streak[128];
        readonly Vector3[] vertices=new Vector3[512];
        readonly Color[] colors=new Color[512];
        readonly int[] indices=new int[768];
        readonly System.Random random=new System.Random(7531);
        Mesh mesh;Material material;
        float displayedLength=-1;
        float Next()=>(float)random.NextDouble();
        public void Initialize(Shader shader)
        {
            if(mesh)return;
            mesh=new Mesh{name="Tunnel airflow dynamic ribbons"};mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=gameObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            material=new Material(shader){name="Tunnel airflow instance"};renderer.sharedMaterial=material;
            renderer.sortingOrder=50;
            for(int i=0;i<128;i++) {int v=i*4,j=i*6;indices[j]=v;indices[j+1]=v+1;indices[j+2]=v+2;indices[j+3]=v;indices[j+4]=v+2;indices[j+5]=v+3;}
            ResetLines();
        }
        public void ResetLines()
        {
            displayedLength=-1;
            for(int i=0;i<128;i++)streaks[i]=new Streak{angle=Next()*Mathf.PI*2,t=Next()*.92f,radius=Mathf.Lerp(.65f,1.25f,Next()),width=Next(),speed=Mathf.Lerp(.8f,1.2f,Next()),alpha=Mathf.Lerp(.35f,1,Next())};
        }
        public static Vector3 Point(Vector3 origin,Vector3 camera,Vector3 radial,float t,float sourceRadius,float nearRadius)
            =>Vector3.LerpUnclamped(origin,camera,t)+radial*Mathf.LerpUnclamped(sourceRadius,nearRadius,t);
        public void Present(Camera camera,Vector3 origin,float travel,int count,float sourceRadius,float nearRadius,float length,Vector2 widths,Color tint,
            bool protect,float center,float protectedWidth,float doorWidth,float feather,bool snap,bool depth,float populationStep = 1,bool emit = true,float exitMargin=.08f,float lengthBlend=1)
        {
            if(!mesh||!material||!camera)return;
            count=Mathf.Clamp(count,0,128);
            displayedLength=displayedLength<0?length:Mathf.Lerp(displayedLength,length,Mathf.Clamp01(lengthBlend));
            Vector3 axis=(camera.transform.position-origin).normalized;
            Vector3 right=Vector3.ProjectOnPlane(camera.transform.right,axis).normalized;
            Vector3 up=Vector3.Cross(axis,right).normalized;
            if(Vector3.Dot(up,camera.transform.up)<0)up=-up;
            float distance=Mathf.Max(.01f,Vector3.Distance(origin,camera.transform.position));
            Vector3 door=camera.WorldToViewportPoint(origin);
            material.SetVector("_Protection",new Vector4(protect?1:0,center,protectedWidth,feather));
            material.SetVector("_Door",new Vector4(door.x,door.y,doorWidth,0));
            material.SetFloat("_PixelSnap",snap?1:0);material.SetFloat("_ZTest",(float)(depth?CompareFunction.LessEqual:CompareFunction.Always));
            for(int i=0;i<128;i++)
            {
                var line=streaks[i];
                if(emit)line.presence=Mathf.MoveTowards(line.presence,i<count?1:0,Mathf.Max(0,populationStep));
                line.t+=Mathf.Max(0,travel)*line.speed;

                streaks[i]=line;
                Vector3 radial=(right*Mathf.Cos(line.angle)+up*Mathf.Sin(line.angle))*line.radius;
                Vector3 head=Point(origin,camera.transform.position,radial,line.t,sourceRadius,nearRadius);
                Vector3 tail=Point(origin,camera.transform.position,radial,Mathf.Max(0,line.t-displayedLength/distance),sourceRadius,nearRadius);
                // Clip before constructing the camera-facing width. A head behind the camera
                // can flip the ribbon's side vector even while its tail is still visible.
                bool visible=ClipToNearPlane(camera,ref tail,ref head,out float tailAlpha,out float headAlpha);
                Vector3 side=visible?Vector3.Cross(camera.transform.position-(head+tail)*.5f,head-tail).normalized:Vector3.zero;
                side*=Mathf.Max(.001f,Mathf.Lerp(widths.x,widths.y,line.width))*.5f;
                bool outside=visible&&OutsideViewport(camera,tail-side,tail+side,head+side,head-side,exitMargin);
                // Recycle only after the whole ribbon exits. A fixed travel cap could
                // otherwise remove a long tail that is still on screen.
                if(!visible || (line.t>1&&outside))
                {
                    if(emit){line.t=0;line.angle=Next()*Mathf.PI*2;line.presence=0;}
                    else line.retired=true;
                    streaks[i]=line;
                    for(int k=0;k<4;k++)colors[i*4+k]=Color.clear;
                    continue;
                }
                int v=i*4;
                vertices[v]=transform.InverseTransformPoint(tail-side);vertices[v+1]=transform.InverseTransformPoint(tail+side);
                vertices[v+2]=transform.InverseTransformPoint(head+side);vertices[v+3]=transform.InverseTransformPoint(head-side);
                Color color=tint;color.a*=line.presence*line.alpha*Mathf.SmoothStep(0,1,Mathf.Clamp01(line.t/.1f))*(line.retired?0:1);
                colors[v]=colors[v+1]=new Color(color.r,color.g,color.b,color.a*tailAlpha);
                colors[v+2]=colors[v+3]=new Color(color.r,color.g,color.b,color.a*headAlpha);
            }
            mesh.Clear(false);mesh.SetVertices(vertices,0,512);mesh.SetColors(colors,0,512);mesh.SetIndices(indices,0,768,MeshTopology.Triangles,0);mesh.RecalculateBounds();
        }
        public static bool ClipToNearPlane(Camera camera,ref Vector3 tail,ref Vector3 head,out float tailAlpha,out float headAlpha)
        {
            tailAlpha=0;headAlpha=1;
            float near=camera.nearClipPlane+.001f;
            float a=Vector3.Dot(tail-camera.transform.position,camera.transform.forward);
            float b=Vector3.Dot(head-camera.transform.position,camera.transform.forward);
            if(a<=near&&b<=near)return false;
            if(a<near){float t=(near-a)/(b-a);tail=Vector3.Lerp(tail,head,t);tailAlpha=t;}
            else if(b<near){float t=(near-a)/(b-a);head=Vector3.Lerp(tail,head,t);headAlpha=t;}
            return true;
        }
        static bool OutsideViewport(Camera camera,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float margin)
        {
            a=camera.WorldToViewportPoint(a);b=camera.WorldToViewportPoint(b);c=camera.WorldToViewportPoint(c);d=camera.WorldToViewportPoint(d);
            return Mathf.Max(Mathf.Max(a.x,b.x),Mathf.Max(c.x,d.x))<-margin||Mathf.Min(Mathf.Min(a.x,b.x),Mathf.Min(c.x,d.x))>1+margin||
                Mathf.Max(Mathf.Max(a.y,b.y),Mathf.Max(c.y,d.y))<-margin||Mathf.Min(Mathf.Min(a.y,b.y),Mathf.Min(c.y,d.y))>1+margin;
        }
        void OnDestroy()
        {
            if(Application.isPlaying){if(mesh)Destroy(mesh);if(material)Destroy(material);}
            else{if(mesh)DestroyImmediate(mesh);if(material)DestroyImmediate(material);}
        }
    }
}
