using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    // One non-interactive UI mesh; does not change exposure, camera settings or shared volumes.
    public sealed class FightGemFinaleGlare : MaskableGraphic
    {
        Vector2 origin; float charge, wash, glow, flash;
        public void Present(Vector2 center,float amount,float brightness,float strength,float whiteFlash)
        {
            origin=center;charge=amount;wash=brightness;glow=strength;flash=whiteFlash;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=GetPixelAdjustedRect();
            const int columns=32,rows=18;
            float aspect=rect.width/Mathf.Max(1,rect.height);
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {
                Vector2 uv=new(x/(float)columns,y/(float)rows);
                Vector2 delta=uv-origin;delta.x*=aspect;
                float halo=Mathf.Exp(-delta.sqrMagnitude*7)*glow*charge;
                float baseAlpha=1-(1-wash*charge)*(1-halo);
                float alpha=1-(1-baseAlpha)*(1-flash);
                var tint=Color.Lerp(new Color(.76f,.91f,1),Color.white,flash);
                tint.a=Mathf.Clamp01(alpha);
                mesh.AddVert(new Vector3(rect.xMin+uv.x*rect.width,rect.yMin+uv.y*rect.height),tint,uv);
                if(x<columns&&y<rows)
                {
                    int a=y*(columns+1)+x,b=a+columns+1;
                    mesh.AddTriangle(a,b,a+1);mesh.AddTriangle(a+1,b,b+1);
                }
            }
        }
    }
}
