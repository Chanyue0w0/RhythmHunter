using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    internal static class FightMenuUi
    {
        internal static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=size;return r;
        }
        internal static Text Label(Transform parent,string text,Vector2 pos,Vector2 size,Font font,int fontSize)
        {
            var t=Rect("Label",parent,pos,size).gameObject.AddComponent<Text>();t.font=font;t.fontSize=fontSize;t.text=text;
            t.color=Color.white;t.raycastTarget=false;t.alignment=TextAnchor.MiddleCenter;return t;
        }
        internal static Button Button(Transform parent,string text,Vector2 pos,Vector2 size,Font font,UnityEngine.Events.UnityAction action)
        {
            var r=Rect(text,parent,pos,size);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.08f,.22f,.3f);
            var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;button.navigation=new Navigation{mode=Navigation.Mode.None};
            var colors=button.colors;
            colors.selectedColor=new Color(1f,.8f,.3f); colors.highlightedColor=new Color(.65f,.9f,1f);
            colors.colorMultiplier=2; button.colors=colors;
            button.onClick.AddListener(action);Label(r,text,Vector2.zero,size,font,18);return button;
        }
    }
}
