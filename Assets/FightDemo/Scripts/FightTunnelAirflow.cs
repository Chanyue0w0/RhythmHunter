using UnityEngine;
using UnityEngine.Rendering;
namespace RhythmHunter.FightDemo
{
    /// <summary>Actual world-space ribbons moving down a cone from the door to the viewing camera.</summary>
    public sealed class FightTunnelAirflow : MonoBehaviour
    {
        struct Streak { public float angle,t,radius,width,speed,alpha,presence; }
        readonly Streak[] streaks=new Streak[128];
        readonly Vector3[] vertices=new Vector3[512];
        readonly Color[] colors=new Color[512];
        readonly int[] indices=new int[768];
        readonly System.Random random=new System.Random(7531);
        Mesh mesh;Material material;
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
            for(int i=0;i<128;i++)streaks[i]=new Streak{angle=Next()*Mathf.PI*2,t=Next()*.92f,radius=Mathf.Lerp(.65f,1.25f,Next()),width=Next(),speed=Mathf.Lerp(.8f,1.2f,Next()),alpha=Mathf.Lerp(.35f,1,Next())};
        }
        public static Vector3 Point(Vector3 origin,Vector3 camera,Vector3 radial,float t,float sourceRadius,float nearRadius)
            =>Vector3.Lerp(origin,camera,t)+radial*Mathf.Lerp(sourceRadius,nearRadius,t);
        public void Present(Camera camera,Vector3 origin,float travel,int count,float sourceRadius,float nearRadius,float length,Vector2 widths,Color tint,
            bool protect,float center,float protectedWidth,float doorWidth,float feather,bool snap,bool depth,float populationStep = 1)
        {
            if(!mesh||!material||!camera)return;
            count=Mathf.Clamp(count,0,128);
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
                line.presence=Mathf.MoveTowards(line.presence,i<count?1:0,Mathf.Max(0,populationStep));
                line.t+=Mathf.Max(0,travel)*line.speed;
                if(line.t>.94f){line.t%=.94f;line.angle=Next()*Mathf.PI*2;}
                streaks[i]=line;
                Vector3 radial=(right*Mathf.Cos(line.angle)+up*Mathf.Sin(line.angle))*line.radius;
                Vector3 head=Point(origin,camera.transform.position,radial,line.t,sourceRadius,nearRadius);
                Vector3 tail=Point(origin,camera.transform.position,radial,Mathf.Max(0,line.t-length/distance),sourceRadius,nearRadius);
                Vector3 side=Vector3.Cross(camera.transform.position-(head+tail)*.5f,head-tail).normalized;
                side*=Mathf.Max(.001f,Mathf.Lerp(widths.x,widths.y,line.width))*.5f;
                int v=i*4;
                vertices[v]=transform.InverseTransformPoint(tail-side);vertices[v+1]=transform.InverseTransformPoint(tail+side);
                vertices[v+2]=transform.InverseTransformPoint(head+side);vertices[v+3]=transform.InverseTransformPoint(head-side);
                Color color=tint;color.a*=line.presence*line.alpha*Mathf.SmoothStep(0,1,Mathf.Clamp01(line.t/.1f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((.94f-line.t)/.24f));
                colors[v]=colors[v+1]=new Color(color.r,color.g,color.b,0);colors[v+2]=colors[v+3]=color;
            }
            mesh.Clear(false);mesh.SetVertices(vertices,0,512);mesh.SetColors(colors,0,512);mesh.SetIndices(indices,0,768,MeshTopology.Triangles,0);mesh.RecalculateBounds();
        }
        void OnDestroy()
        {
            if(Application.isPlaying){if(mesh)Destroy(mesh);if(material)Destroy(material);}
            else{if(mesh)DestroyImmediate(mesh);if(material)DestroyImmediate(material);}
        }
    }
}
