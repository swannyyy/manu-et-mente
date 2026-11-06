// SPDX-License-Identifier: MPL-2.0
using System;
using UnityEngine;
using UnityEngine.UI;

// UI units are scaled by height; safe-area anchors are refreshed after rotation/resizing.
public static class UXUI
{
    public static readonly Color Ink = new Color(.055f,.09f,.115f);
    public static readonly Color Surface = new Color(.09f,.15f,.18f);
    public static readonly Color Ivory = new Color(.96f,.92f,.82f);
    public static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    public static RectTransform Canvas(string name, int order)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720,1280); scaler.matchWidthOrHeight = 1;
        var safe = Panel(go.transform,"Safe area",Color.clear);
        safe.gameObject.AddComponent<UXSafeArea>();
        return safe;
    }
    public static RectTransform Panel(Transform parent,string name,Color color)
    {
        var go = new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
        var r = (RectTransform)go.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        var im = go.GetComponent<Image>(); im.color = color; im.raycastTarget = color.a > 0;
        return r;
    }
    public static void Place(RectTransform r,float y,float height,float margin=24)
    {
        r.anchorMin = new Vector2(0,1); r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f,1);
        r.offsetMin = new Vector2(margin,-y-height); r.offsetMax = new Vector2(-margin,-y);
    }
    public static Text Label(Transform parent,string value,float y,float height=52,int size=28)
    {
        var go = new GameObject(value,typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
        var t = go.GetComponent<Text>(); t.font = Font; t.fontSize = size; t.color = Ivory;
        t.alignment = TextAnchor.MiddleCenter; t.text = value; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
        Place(t.rectTransform,y,height); return t;
    }
    public static Button Button(Transform parent,string title,float y,Action action,bool primary=false)
    {
        var r = Panel(parent,title,primary ? new Color(.2f,.4f,.36f) : new Color(.15f,.24f,.28f));
        Place(r,y,66);
        var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = r.GetComponent<Image>();
        var colors = b.colors; colors.highlightedColor = new Color(.8f,.9f,.85f); colors.pressedColor = new Color(.6f,.75f,.7f);
        colors.disabledColor = new Color(.4f,.4f,.4f,.5f); b.colors = colors;
        var label = Label(r,title,0,66,27); Place(label.rectTransform,0,66,8);
        b.onClick.AddListener(()=>{ SoundManager.Instance?.PlayButtonClick(); action(); }); return b;
    }
    public static InputField Input(Transform parent,string hint,float y,int limit)
    {
        var r = Panel(parent,hint,new Color(.17f,.23f,.26f)); Place(r,y,70);
        var input = r.gameObject.AddComponent<InputField>(); input.targetGraphic = r.GetComponent<Image>();
        var text = Label(r,"",0,70,28); text.supportRichText = false;
        var placeholder = Label(r,hint,0,70,26); placeholder.color = new Color(.7f,.75f,.74f);
        input.textComponent = text; input.placeholder = placeholder; input.characterLimit = limit;
        input.lineType = InputField.LineType.SingleLine; return input;
    }
    public static RectTransform Modal(Transform parent,string title)
    {
        var modal = Panel(parent,title,new Color(.025f,.045f,.06f,.99f));
        modal.SetAsLastSibling(); Label(modal,title,30,65,34); return modal;
    }
    public static void Rules(Transform parent,Action close)
    {
        var p = Modal(parent,"How to win");
        Label(p,"Win with any one of these combinations:",110,60,26);
        Label(p,"Hand + Mind in the center D ring\n\nHand or Mind + 3 Pawns in D\n\nHand or Mind in D + 4 Pawns in C/c\n\nCapture both enemy commanders (Hand and Mind)",180,420,28);
        Label(p,"Tap a piece to see legal moves. Mint rings are moves; segmented coral rings are captures. Scholars and Sages can rotate their ring pair instead of moving.\n\nDiagonal pieces may need an enabler deeper in the board. Select a piece to see its ability.",630,280,26);
        Button(p,"Back",970,()=>{UnityEngine.Object.Destroy(p.gameObject); close?.Invoke();});
    }
}
public class UXSafeArea : MonoBehaviour
{
    void LateUpdate()
    {
        var r=(RectTransform)transform; var a=Screen.safeArea;
        if(Screen.width<1 || Screen.height<1)return;
        r.anchorMin=new Vector2(a.xMin/Screen.width,a.yMin/Screen.height);
        r.anchorMax=new Vector2(a.xMax/Screen.width,a.yMax/Screen.height);
        r.offsetMin=r.offsetMax=Vector2.zero;
    }
}
