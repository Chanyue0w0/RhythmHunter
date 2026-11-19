using UnityEngine;
using UnityEngine.Rendering;
namespace RhythmHunter.FightDemo
{
    // Actual cylindrical geometry around the authored particle core, not a camera-facing card.
    public sealed class FightGemBeamVolume : MonoBehaviour
    {
        Mesh mesh;Material material;GameObject shell;
        public void Initialize(Shader shader,float length,float radius)
        {
            const int sides=24,rings=9;
            var vertices=new Vector3[(sides+1)*rings];var uv=new Vector2[vertices.Length];
            var triangles=new int[(rings-1)*sides*6];int index=0;
            for(int j=0;j<rings;j++)for(int i=0;i<=sides;i++)
            {
                float t=j/(float)(rings-1),angle=i/(float)sides*Mathf.PI*2;
                float width=radius*Mathf.Lerp(.55f,1,t);
                int v=j*(sides+1)+i;vertices[v]=new Vector3(Mathf.Cos(angle)*width,Mathf.Sin(angle)*width,t*length);uv[v]=new Vector2(i/(float)sides,t);
                if(j<rings-1&&i<sides){int n=v+sides+1;triangles[index++]=v;triangles[index++]=v+1;triangles[index++]=n;triangles[index++]=v+1;triangles[index++]=n+1;triangles[index++]=n;}
            }
            mesh=new Mesh{name="Finale beam cylindrical volume"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            shell=new GameObject("BeamVolume3D");shell.transform.SetParent(transform,false);
            shell.AddComponent<MeshFilter>().sharedMesh=mesh;var r=shell.AddComponent<MeshRenderer>();
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.sortingOrder=-1;
            material=new Material(shader);r.sharedMaterial=material;
        }
        public void Present(float beat,float opacity){if(material)material.SetVector("_Motion",new Vector4(beat,opacity,0,0));}
        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);if(shell)Destroy(shell);}
    }
}
