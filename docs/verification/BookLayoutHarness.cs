// 执行真实BookView布局；轻量Unity替身用于几何/交互控制流及GDI预览。
// 不模拟IL2CPP、CanvasScaler、字体光栅、滚轮或实际游戏渲染。
using System;
using MOD_YtxSQ1.Services;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using MOD_YtxSQ1.UI;

namespace UnityEngine
{
    public enum KeyCode { Return,KeypadEnter,Escape,Tab }
    public static class Input { public static string compositionString=""; public static bool MouseDown,Submit,Escape; public static Vector2 mousePosition;public static bool GetMouseButtonDown(int button) { return MouseDown; } public static bool GetKeyDown(KeyCode key) { return Submit && key==KeyCode.Return || Escape && key==KeyCode.Escape; } }
    public class Canvas : Component { public Camera worldCamera; }
    public class CanvasGroup : Component { public float alpha; public bool interactable,blocksRaycasts; }
    public static class RectTransformUtility
    {
        public static bool Inside=true;public static bool RectangleContainsScreenPoint(RectTransform rect,Vector2 point,Camera camera) { return Inside; }
    }
    public class Object
    {
        public static GameObject Instantiate(GameObject source,Transform parent,bool world)
        {
            var clone=FakeRes.MakeField();clone.transform.SetParent(parent,false);
            clone.GetComponent<UnityEngine.UI.InputField>().onValueChanged=source.GetComponent<UnityEngine.UI.InputField>().onValueChanged;
            return clone;
        }
        public static void Destroy(Object obj)
        {
            var texture=obj as Texture2D;
            if(texture!=null && texture.Pixels!=null) { texture.Pixels=null;Texture2D.LiveProcedural--; }
            var go = obj as GameObject;
            if (go != null) ModelRenderTexture.ReleaseNative(go);
            if (go != null) { go.activeInHierarchy = false; if (go.transform.parent != null) go.transform.parent.Children.Remove(go.transform); }
        }
    }
    public struct Vector2
    {
        public float x, y; public Vector2(float a, float b) { x = a; y = b; }
        public static Vector2 zero { get { return new Vector2(); } }
        public static Vector2 one { get { return new Vector2(1, 1); } }
    }
    public struct Vector3 { public float x,y,z;public Vector3(float a,float b,float c) { x=a;y=b;z=c; } public static Vector3 one { get { return new Vector3(1,1,1); } } public static Vector3 operator *(Vector3 value,float amount) { return new Vector3(value.x*amount,value.y*amount,value.z*amount); } }
    public struct Bounds
    {
        public Vector3 center,size;public Bounds(Vector3 c,Vector3 s) { center=c;size=s; }
        public Vector3 extents { get { return size*.5f; } }
        public void Encapsulate(Bounds b)
        {
            float left=Math.Min(center.x-extents.x,b.center.x-b.extents.x),right=Math.Max(center.x+extents.x,b.center.x+b.extents.x);
            float bottom=Math.Min(center.y-extents.y,b.center.y-b.extents.y),top=Math.Max(center.y+extents.y,b.center.y+b.extents.y);
            center=new Vector3((left+right)/2,(bottom+top)/2,center.z);size=new Vector3(right-left,top-bottom,size.z);
        }
    }
    public class Renderer : Component { public bool enabled=true;public Bounds bounds; }
    public class Camera : Component { public bool orthographic;public float orthographicSize,aspect; }
    public struct Rect
    {
        public float x,y,width,height;
        public Rect(float a,float b,float c,float d) { x=a;y=b;width=c;height=d; }
        public Vector2 size { get { return new Vector2(width,height); } }
    }
    public struct Color
    {
        public float r,g,b,a; public Color(float r,float g,float b,float a) { this.r=r;this.g=g;this.b=b;this.a=a; }
        public static Color white { get { return new Color(1,1,1,1); } }
    }
    public static class Mathf { public static float Min(float a,float b) { return Math.Min(a,b); } public static float Max(float a,float b) { return Math.Max(a,b); } }
    public static class Time { public static float unscaledTime; public static int frameCount; }
    public static class Debug
    {
        public static void Log(string value) { }
        public static void LogWarning(string value) { }
        public static void LogError(string value) { throw new Exception(value); }
    }
    public class Component : Object
    {
        public GameObject gameObject; public Transform transform { get { return gameObject.transform; } }
        public T GetComponent<T>() where T:class { return gameObject.GetComponent<T>(); }
        public T GetComponentInParent<T>() where T:class { var own=GetComponent<T>();return own ?? (transform.parent==null ? null : transform.parent.GetComponentInParent<T>()); }
        public T GetComponentInChildren<T>() where T:class { return GetComponentsInChildren<T>(true).FirstOrDefault(); }
        public T[] GetComponentsInChildren<T>(bool inactive) where T:class
        {
            var list = new List<T>(); var own = GetComponent<T>(); if (own != null) list.Add(own);
            foreach (var child in transform.Children) list.AddRange(child.GetComponentsInChildren<T>(inactive));
            return list.ToArray();
        }
    }
    public class GameObject : Object
    {
        public static string ThrowDeactivateName;
        public string name; public int layer; public bool activeInHierarchy=true; public RectTransform transform;
        public bool activeSelf { get { return activeInHierarchy; } }
        public readonly List<Component> Components = new List<Component>();
        public GameObject(string name) { this.name=name; transform=new RectTransform { gameObject=this }; Components.Add(transform); }
        public T AddComponent<T>() where T:Component,new()
        {
            if (typeof(T)==typeof(RectTransform)) return transform as T;
            var item=new T { gameObject=this }; Components.Add(item); return item;
        }
        public T GetComponent<T>() where T:class { return Components.OfType<T>().FirstOrDefault(); }
        public T[] GetComponentsInChildren<T>(bool inactive) where T:class { return transform.GetComponentsInChildren<T>(inactive); }
        public T GetComponentInChildren<T>(bool inactive) where T:class { return GetComponentsInChildren<T>(inactive).FirstOrDefault(); }
        public void SetActive(bool active) { if (!active && name == ThrowDeactivateName) throw new Exception("injected deactivate failure"); activeInHierarchy=active; }
    }
    public class Transform : Component
    {
        public Vector3 position;public Transform parent; public readonly List<Transform> Children=new List<Transform>();
        public bool IsChildOf(Transform ancestor){for(var p=parent;p!=null;p=p.parent)if(p==ancestor)return true;return false;} public int childCount { get { return Children.Count; } }
        public Transform GetChild(int index) { return Children[index]; }
        public void SetParent(Transform value,bool world) { parent=value; value.Children.Add(this); }
        public void SetAsFirstSibling() { parent.Children.Remove(this); parent.Children.Insert(0,this); }
    }
    public class RectTransform : Transform
    {
        public Vector2 anchorMin,anchorMax,pivot,anchoredPosition,sizeDelta,offsetMin,offsetMax;
        public Vector3 localScale;
        public Rect rect { get { return new Rect(0,0,sizeDelta.x,sizeDelta.y); } }
    }
    public class Font : Object
    {
        public static Font CreateDynamicFontFromOSFont(string name,int size) { return new Font(); }
        public bool HasCharacter(char value) { return true; }
    }
    public enum TextureFormat { RGBA32 }
    public class Texture2D : Object
    {
        public string Path; public Color[] Pixels; public int Width,Height;public static int LiveProcedural;
        public Texture2D() { }
        public Texture2D(int width,int height,TextureFormat format,bool mipmap) { Width=width;Height=height;LiveProcedural++; }
        public void SetPixels(Color[] pixels) { Pixels=pixels; }
        public void Apply() { }
    }
    public enum TextAnchor { MiddleLeft,MiddleCenter,MiddleRight,UpperLeft }
    public enum HorizontalWrapMode { Wrap,Overflow }
    public enum VerticalWrapMode { Truncate }
}
namespace HarmonyLib
{
    public class Harmony { public static int Patches;public Harmony(string id) { } public void Patch(object method,HarmonyMethod prefix) { Patches++; } public void UnpatchSelf() { Patches=0; } }
    public class HarmonyMethod { public HarmonyMethod(Type type,string method) { } }
    public static class AccessTools { public static object Method(Type type,string name,Type[] parameters) { return new object(); } }
}
namespace UnityEngine.Events
{
    public class UnityAction
    {
        public Action Callback; public static explicit operator UnityAction(Action value) { return new UnityAction { Callback=value }; }
    }
    public class UnityAction<T>
    {
        public Action<T> Callback; public static explicit operator UnityAction<T>(Action<T> value) { return new UnityAction<T> { Callback=value }; }
    }
    public class UnityEvent : UnityEngine.UI.ClickEvent { }
}
namespace UnityEngine.UI
{
    using UnityEngine;
    public class Graphic : Component { public bool enabled=true,raycastTarget; public Color color; public T TryCast<T>() where T:class { return this as T; } public RectTransform rectTransform { get { return (RectTransform)transform; } } }
    public class Image : Graphic { public enum Type{Simple,Sliced}public Type type;public bool preserveAspect;public Object sprite; }
    public class RawImage : Graphic { public Texture2D texture; public Rect uvRect; }
    public class Text : Graphic
    {
        public Font font; public int fontSize; public string text; public TextAnchor alignment;
        public bool supportRichText; public HorizontalWrapMode horizontalOverflow; public VerticalWrapMode verticalOverflow;
        public bool resizeTextForBestFit; public int resizeTextMinSize,resizeTextMaxSize;
        public bool alignByGeometry;public float lineSpacing=1f;
        public float preferredHeight { get { return Math.Max(24, (text.Length / Math.Max(1,(int)(rectTransform.sizeDelta.x / Math.Max(1,fontSize))) + text.Split('\n').Length) * fontSize * 1.2f); } }
        public float preferredWidth { get { return (text??"").Length*fontSize*.55f; } }
    }
    public struct ColorBlock { public Color normalColor,highlightedColor,pressedColor,selectedColor; }
    public class ClickEvent
    {
        private readonly List<UnityEngine.Events.UnityAction> listeners=new List<UnityEngine.Events.UnityAction>();
        public void AddListener(UnityEngine.Events.UnityAction value) { listeners.Add(value); }
        public void Invoke() { foreach(var listener in listeners) listener.Callback(); }
    }
    public class Button : Component { public Navigation navigation; public bool interactable=true; public Graphic targetGraphic; public ColorBlock colors; public ClickEvent onClick=new ClickEvent(); }
    public class ValueEvent<T>
    {
        private readonly List<UnityEngine.Events.UnityAction<T>> listeners=new List<UnityEngine.Events.UnityAction<T>>();
        public void AddListener(UnityEngine.Events.UnityAction<T> value) { listeners.Add(value); }
        public void Invoke(T value) { foreach(var listener in listeners) listener.Callback(value); }
    }
    public class InputField : Component { public RectTransform caretRectTrans;public object m_CachedInputRenderer,m_InputTextCache;
        public class OnChangeEvent : ValueEvent<string> { }
        public class SubmitEvent : ValueEvent<string> { }
        public bool shouldActivateOnSelect;public object onValidateInput;public Navigation navigation;
        public SubmitEvent onEndEdit=new SubmitEvent();
        public enum ContentType { Standard } public enum LineType { SingleLine } public enum CharacterValidation { None }
        public ContentType contentType;public LineType lineType;public CharacterValidation characterValidation;
        public bool readOnly,interactable,customCaretColor,isFocused;public Color caretColor;public int caretWidth;
        public Graphic targetGraphic;
        public Text textComponent; public Graphic placeholder; public int characterLimit;
        public OnChangeEvent onValueChanged=new OnChangeEvent();
        private string input="";
        public string text { get { return input; } set { input=value; placeholder.enabled=value.Length==0; onValueChanged.Invoke(value); textComponent.text=value; } }
        public void SetTextWithoutNotify(string query) { input=query;textComponent.text=query;if(placeholder!=null)placeholder.enabled=query.Length==0; }
        public int selectionAnchorPosition,selectionFocusPosition;
        public void ActivateInputField() { isFocused=true; }
        public void AssignPositioningIfNeeded() { }
        public void UpdateLabel() { }
        public void OnUpdateSelected(UnityEngine.EventSystems.BaseEventData data)
        { if(isFocused && !readOnly) { text+=UnityEngine.EventSystems.EventSystem.Pending;UnityEngine.EventSystems.EventSystem.Pending=""; } }
    }
    public struct Navigation { public enum Mode { None } public Mode mode; }
    public class RectMask2D : Component { }
    public class ScrollRect : Component
    {
        public enum MovementType { Clamped }
        public RectTransform viewport,content; public bool horizontal,vertical,inertia; public MovementType movementType; public float scrollSensitivity; public Scrollbar verticalScrollbar;
        public float verticalNormalizedPosition=1; public ValueEvent<Vector2> onValueChanged=new ValueEvent<Vector2>();
    }
    public class Scrollbar : Component
    {
        public enum Direction { BottomToTop }
        public RectTransform handleRect; public Graphic targetGraphic; public Direction direction;
    }
}
namespace UnityEngine.EventSystems
{
    public class EventTrigger : UnityEngine.Component { public bool enabled=true; }
    public class EventSystem { public static EventSystem current;public static string Pending="";public UnityEngine.GameObject currentSelectedGameObject; }
    public class BaseEventData { public BaseEventData(EventSystem events) { } }
}
// 仅验证视图与原生悬停API的绑定/释放控制流，不模拟真实游戏提示窗渲染。
public sealed class UIEventListener : UnityEngine.Component
{
    public bool isMouseEnter;
    public UnityEngine.Events.UnityEvent onMouseEnter,onMouseExit;
    public void OnPointerEnter(object data) { isMouseEnter=true;onMouseEnter.Invoke(); }
    public void OnPointerExit(object data) { isMouseEnter=false;onMouseExit.Invoke(); }
}
public sealed class UITextInfoLong : UnityEngine.Component
{
    public UITextInfoLong()
    {
        gameObject=new UnityEngine.GameObject("native window");
        var label=gameObject.AddComponent<UnityEngine.UI.Text>(); label.font=new UnityEngine.Font(); label.text="原生字体来源";
        var button=new UnityEngine.GameObject("G:btnOK");button.transform.SetParent(gameObject.transform,false);button.AddComponent<UnityEngine.UI.Button>();
        var inactive=new UnityEngine.GameObject("native inactive");inactive.transform.SetParent(gameObject.transform,false);inactive.AddComponent<UnityEngine.UI.Button>();inactive.SetActive(false);
    }
}
public class UIType { public string uiName="NativeInput"; public static readonly UIType MapMain=new UIType(); public static readonly UIType ModTableInputStringLong=new UIType(); public static readonly UIType ModWorkshopUpload=new UIType(); }
public sealed class LayoutUI
{
    public UnityEngine.Component canvas;public UIModTableInputStringLong Editor;
    public object GetUI(UIType type) { return Editor; }
    public T OpenUI<T>(UIType type) where T:class { Editor=new UIModTableInputStringLong();return Editor as T; }
}
public static class SpriteTool { public static UnityEngine.Object GetLuckBG(ConfRoleCreateFeatureItem item) { return new UnityEngine.Object(); } }
public sealed class UIModTableInputStringLong
{
    public string Value;public int Limit;private Il2CppSystem.Action<string> callback;
    public void InitData(string title,string value,Il2CppSystem.Action<string> action,int max) { Value=value;Limit=max;callback=action; }
    public void Submit(string query) { g.ui.Editor=null;callback.Callback(query); }
    public void Cancel() { g.ui.Editor=null; }
}
public static partial class g { public static LayoutUI ui;public static FakeRes res=new FakeRes(); }
public class FakeRes
{
    public static UnityEngine.GameObject MakeField()
    {
        var go=new UnityEngine.GameObject("native field");go.AddComponent<UnityEngine.UI.Image>();
        var input=go.AddComponent<UnityEngine.UI.InputField>();
        input.shouldActivateOnSelect=false;input.onValidateInput=new object();
        go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var text=new UnityEngine.GameObject("input text");text.transform.SetParent(go.transform,false);input.textComponent=text.AddComponent<UnityEngine.UI.Text>();
        var placeholder=new UnityEngine.GameObject("placeholder");placeholder.transform.SetParent(go.transform,false);input.placeholder=placeholder.AddComponent<UnityEngine.UI.Text>();
        return go;
    }
    public T Load<T>(string path) where T:class
    {
        if(path!="UI/NativeInput") throw new Exception("incorrect native UI resource path");
        var source=MakeField();source.GetComponent<UnityEngine.UI.InputField>().onValueChanged.AddListener((UnityEngine.Events.UnityAction<string>)(new System.Action<string>(_=>{throw new Exception("prototype listener was not replaced");})));
        return source as T;
    }
}
namespace TMPro
{
    public enum TextAlignmentOptions { MidlineLeft }
    public class TMP_Text : UnityEngine.UI.Graphic { public string text;public float fontSize;public bool enableWordWrapping,enableAutoSizing;public TextAlignmentOptions alignment; }
    public class TMP_InputField : UnityEngine.Component
    {
        public enum ContentType { Standard } public enum LineType { SingleLine } public enum CharacterValidation { None }
        public class OnChangeEvent : UnityEngine.UI.ValueEvent<string> { }
        public class SubmitEvent : UnityEngine.UI.ValueEvent<string> { }
        public class SelectionEvent : UnityEngine.UI.ValueEvent<string> { }
        public OnChangeEvent onValueChanged=new OnChangeEvent();public SubmitEvent onSubmit,onEndEdit;public SelectionEvent onSelect,onDeselect;
        public ContentType contentType;public LineType lineType;public CharacterValidation characterValidation;
        public bool readOnly,interactable;public int characterLimit;public TMP_Text textComponent;
        public UnityEngine.RectTransform textViewport;public UnityEngine.UI.Graphic placeholder;public UnityEngine.UI.Scrollbar verticalScrollbar;
        private string value="";public string text { get { return value; } set { SetTextWithoutNotify(value);onValueChanged.Invoke(value); } }
        public void SetTextWithoutNotify(string query) { value=query;textComponent.text=query; }
    }
}
public sealed partial class ConfDramaNpc
{
    public static int LastNativeId;
    public int CreateTexture(int id,UnityEngine.UI.RawImage image,UnityEngine.Vector2 offset,float size,PortraitModelData unused,Action<UnityEngine.GameObject> callback)
    {
        if(!Portraits.ContainsKey(id)) throw new InvalidOperationException("native avatar missing");
        LastNativeId=id;
        image.enabled=true;
        if(id==8101)
        {
            image.enabled=false;
            new UnityEngine.GameObject("native prefab").transform.SetParent(image.transform,false);
            return 2;
        }
        image.texture=new UnityEngine.Texture2D();
        ModelRenderTexture.NativeOwners[image.gameObject]=new ModelRenderTexture(new UnityEngine.GameObject("native portrait"),image,offset);
        return 1;
    }
}
public sealed class PortraitModel
{
    public UnityEngine.GameObject root;
    public static PortraitModelData LastData;
    public static bool LastDynamic;
    public static PortraitModel CreateModel(WorldUnitBase unit,bool async,bool dynamic,object callback)
    {
        LastData=null;LastDynamic=dynamic;
        return MakeModel();
    }
    public static PortraitModel CreateModel(PortraitModelData data,bool async,bool dynamic,object callback)
    {
        LastData=data;LastDynamic=dynamic;
        return MakeModel();
    }
    private static PortraitModel MakeModel()
    {
        var root=new UnityEngine.GameObject("portrait model");
        root.AddComponent<UnityEngine.Renderer>().bounds=new UnityEngine.Bounds(new UnityEngine.Vector3(80,240,0),new UnityEngine.Vector3(220,500,1));
        return new PortraitModel { root=root };
    }
}
public sealed class ModelRenderTexture
{
    public static int Active;
    public static readonly Dictionary<UnityEngine.GameObject,ModelRenderTexture> NativeOwners=new Dictionary<UnityEngine.GameObject,ModelRenderTexture>();
    public static void ReleaseNative(UnityEngine.GameObject owner) { ModelRenderTexture render; if(NativeOwners.TryGetValue(owner,out render)) { NativeOwners.Remove(owner);render.Destroy(); } }
    public static UnityEngine.Camera LastCamera;public UnityEngine.Camera camera;
    public ModelRenderTexture(UnityEngine.GameObject model,UnityEngine.UI.RawImage image,UnityEngine.Vector2 offset)
    { Active++;camera=new UnityEngine.GameObject("camera").AddComponent<UnityEngine.Camera>();camera.transform.position=new UnityEngine.Vector3(0,0,-1000);LastCamera=camera; }
    public void Destroy() { Active--; }
}
namespace MOD_YtxSQ1.UI
{
    internal sealed class PaperSkin : IDisposable
    {
        internal static PaperSkin Acquire() { return new PaperSkin(); }
        public static string ArtDirectory;
        public UnityEngine.Texture2D Paper=new UnityEngine.Texture2D(), Brush=new UnityEngine.Texture2D();
        public UnityEngine.Texture2D Landscape=new UnityEngine.Texture2D { Path=Path.Combine(ArtDirectory,"InkLandscape-v1.png") };
        public UnityEngine.Texture2D TitleBrush=new UnityEngine.Texture2D { Path=Path.Combine(ArtDirectory,"TitleBrush-v1.png") };
        public void Dispose() { }
    }
}
internal static class BookLayoutHarness
{
    private static IEnumerable<UnityEngine.Transform> Walk(UnityEngine.Transform node)
    {
        yield return node;
        foreach(var child in node.Children) foreach(var descendant in Walk(child)) yield return descendant;
    }
    private static System.Drawing.Color Color(UnityEngine.Color c) { return System.Drawing.Color.FromArgb((int)(c.a*255),(int)(c.r*255),(int)(c.g*255),(int)(c.b*255)); }
    private static void Paint(Graphics graphics,UnityEngine.RectTransform node,float x,float y,bool root)
    {
        if (!node.gameObject.activeInHierarchy) return;
        if (!root) { x+=node.anchoredPosition.x; y-=node.anchoredPosition.y; }
        var rect=new RectangleF(x,y,node.sizeDelta.x,node.sizeDelta.y);
        foreach(var graphic in node.gameObject.Components.OfType<UnityEngine.UI.Graphic>())
        {
            if (!graphic.enabled) continue;
            var text=graphic as UnityEngine.UI.Text;
            var texture=graphic as UnityEngine.UI.RawImage;
            if (text!=null)
            {
                // 近似Unity的单行BestFit，仅用于模拟预览，不宣称与游戏字形度量一致。
                var fontSize=text.fontSize;
                if(text.resizeTextForBestFit && text.horizontalOverflow==UnityEngine.HorizontalWrapMode.Overflow)
                    while(fontSize>text.resizeTextMinSize)
                    {
                        using(var candidate=new System.Drawing.Font("楷体",fontSize,FontStyle.Regular,GraphicsUnit.Pixel))
                            if(graphics.MeasureString(text.text,candidate).Width<=rect.Width) break;
                        fontSize--;
                    }
                using(var font=new System.Drawing.Font("楷体",fontSize,FontStyle.Regular,GraphicsUnit.Pixel))
                using(var brush=new SolidBrush(Color(text.color)))
                using(var format=new StringFormat { FormatFlags=text.horizontalOverflow==UnityEngine.HorizontalWrapMode.Overflow?StringFormatFlags.NoWrap:0, LineAlignment=StringAlignment.Center, Alignment=text.alignment==UnityEngine.TextAnchor.MiddleCenter?StringAlignment.Center:text.alignment==UnityEngine.TextAnchor.MiddleRight?StringAlignment.Far:StringAlignment.Near })
                    graphics.DrawString(text.text,font,brush,rect,format);
            }
            else if(texture!=null)
            {
                if(texture.texture==null) continue;
                if(texture.texture.Path!=null)
                {
                    using(var bitmap=System.Drawing.Image.FromFile(texture.texture.Path))
                    {
                        var uv=texture.uvRect;
                        var source=new RectangleF(uv.x*bitmap.Width,(1-uv.y-uv.height)*bitmap.Height,uv.width*bitmap.Width,uv.height*bitmap.Height);
                        graphics.DrawImage(bitmap,rect,source,GraphicsUnit.Pixel);
                    }
                }
                else if(texture.texture.Pixels!=null)
                {
                    var data=texture.texture;
                    using(var bitmap=new Bitmap(data.Width,data.Height))
                    {
                        for(int py=0;py<data.Height;py++) for(int px=0;px<data.Width;px++) bitmap.SetPixel(px,py,Color(data.Pixels[py*data.Width+px]));
                        graphics.DrawImage(bitmap,rect);
                    }
                }
                else using(var brush=new SolidBrush(System.Drawing.Color.FromArgb(242,29,32,29)))
                    graphics.FillPolygon(brush,new[] {new PointF(x+5,y+5),new PointF(x+rect.Width-12,y+2),new PointF(x+rect.Width,y+rect.Height/2),new PointF(x+rect.Width-7,y+rect.Height-4),new PointF(x+3,y+rect.Height-2),new PointF(x,y+rect.Height/2)});
            }
            else
            {
                var paintColor=graphic.color;
                var button=node.GetComponent<UnityEngine.UI.Button>();
                if(button!=null && button.targetGraphic==graphic)
                {
                    var tint=button.colors.normalColor;
                    paintColor=new UnityEngine.Color(paintColor.r*tint.r,paintColor.g*tint.g,paintColor.b*tint.b,paintColor.a*tint.a);
                }
                using(var brush=new SolidBrush(Color(paintColor))) graphics.FillRectangle(brush,rect);
            }
        }
        var previous=graphics.Save();
        if(node.GetComponent<UnityEngine.UI.RectMask2D>()!=null) graphics.SetClip(rect);
        foreach(var child in node.Children) Paint(graphics,(UnityEngine.RectTransform)child,x,y,false);
        graphics.Restore(previous);
    }
    private static void Check(bool value,string description) { if(!value) throw new Exception("FAIL: "+description); Console.WriteLine("PASS: "+description); }
    public static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception ex) { Console.WriteLine("FAIL TYPE: " + ex.GetType().Name); Console.WriteLine("FAIL MESSAGE: " + ex.Message); Environment.ExitCode=1; }
    }
    private static void Run(string[] args)
    {
        var rightTip=BookView.FateTipBounds(new UnityEngine.Rect(10,20,200,32),280,90,new UnityEngine.Vector2(1000,500));
        var leftTip=BookView.FateTipBounds(new UnityEngine.Rect(800,20,180,32),280,90,new UnityEngine.Vector2(1000,500));
        var bottomTip=BookView.FateTipBounds(new UnityEngine.Rect(10,450,200,32),280,90,new UnityEngine.Vector2(1000,500));
        var belowTip=BookView.FateTipBounds(new UnityEngine.Rect(20,20,360,32),280,90,new UnityEngine.Vector2(420,500));
        Check(rightTip.x==218 && rightTip.y==20,"tooltip sits beside row with eight unit gap");
        Check(leftTip.x+leftTip.width==792,"tooltip switches to left near right edge");
        Check(bottomTip.y+bottomTip.height==492,"tooltip stays within bottom boundary");
        Check(belowTip.y==60,"very wide row places tooltip below when neither side fits");
        var testArea=new UnityEngine.GameObject("area").transform;
        var testViewport=new UnityEngine.GameObject("viewport").transform;testViewport.SetParent(testArea,false);testViewport.anchoredPosition=new UnityEngine.Vector2(40,-100);
        var testPage=new UnityEngine.GameObject("scroll page").transform;testPage.SetParent(testViewport,false);testPage.anchoredPosition=new UnityEngine.Vector2(0,36);
        var testRow=new UnityEngine.GameObject("row").transform;testRow.SetParent(testPage,false);testRow.anchoredPosition=new UnityEngine.Vector2(5,-72);testRow.sizeDelta=new UnityEngine.Vector2(200,32);
        var beforeScroll=BookView.FateRowBounds(testRow,testArea);testPage.anchoredPosition=new UnityEngine.Vector2(0,72);
        var afterScroll=BookView.FateRowBounds(testRow,testArea);
        Check(beforeScroll.x==45 && beforeScroll.y==136 && afterScroll.y==100,"real anchored hierarchy includes scroll displacement");
        PaperSkin.ArtDirectory=args[0];
        g.world.playerUnit.data.unitData.propertyData.Name="示例修士";
        for(int i=0;i<300;i++)
        {
            var npc=new WorldUnitBase();npc.data.unitData.unitID="npc"+i;
            npc.data.unitData.propertyData.Name="修士"+i.ToString("D3");npc.data.dynUnitData.attack.Stored=300+i;
            g.world.unit.Npcs.Add(npc);
        }
        var hiddenNpc=new WorldUnitBase { isHidden=true };hiddenNpc.data.unitData.unitID="hidden";
        hiddenNpc.data.unitData.propertyData.Name="隐藏修士";g.world.unit.Npcs.Add(hiddenNpc);
        var deadNpc=new WorldUnitBase { isDie=true };deadNpc.data.unitData.unitID="dead";g.world.unit.Npcs.Add(deadNpc);
        g.world.unit.Npcs.Add(g.world.unit.Npcs[0]);
        // 合成身份与配置只验证真实服务的分流/释放，不证明原生DLC配置或贴图正确。
        for(int i=0;i<5;i++)
        {
            var flower=new WorldUnitBase();flower.data.unitData.unitID="flower"+i;
            flower.data.unitData.propertyData.Name="特殊人物"+i;
            g.world.unit.Npcs.Add(flower);
            g.conf.dramaNpc.Ids[flower.data.unitData.unitID]=8100+i;
            g.conf.dramaNpc.Portraits[8100+i]=new PortraitModelData { body=9100+i };
        }
        System.Collections.Generic.List<CharacterEntry> roster;string rosterReason;
        Check(CharacterRoster.TryRead(out roster,out rosterReason) && roster.Count==307 && roster.Any(e=>e.Id=="hidden") && !roster.Any(e=>e.Id=="dead"),"roster includes hidden living units, filters dead and deduplicates IDs");
        Check(CharacterReader.Resolve("hidden")==hiddenNpc,"listed hidden target resolves for attribute and portrait reads");
        float[][] sizes={new[]{1920f,1200f},new[]{1600f,900f},new[]{1366f,768f},new[]{960f,720f}};
        for(int f=0;f<2000;f++)g.conf.roleCreateFeature.Items[20000+f]=new FateItem{type=2,name="bulk"+f,tips="0"};
        for(int s=0;s<sizes.Length;s++)
        {
            g.world.playerUnit.data.dynUnitData.hp.Stored=123;g.world.playerUnit.data.dynUnitData.hpMax.Stored=456;
            g.world.playerUnit.data.unitData.propertyData.hobby=new[]{201,202,203};g.world.playerUnit.data.unitData.propertyData.inTrait=1;
            g.world.playerUnit.data.dynUnitData.attack.Stored=77;
            g.world.playerUnit.data.unitData.propertyData.bornLuck=new[]{new DataUnit.LuckData{id=1},new DataUnit.LuckData{id=2},new DataUnit.LuckData{id=3}};
            g.world.playerUnit.data.unitData.propertyData.addLuck=new List<DataUnit.LuckData>{new DataUnit.LuckData{id=4}};
            string[] previewNames={"聪明谢顶","倾国倾城","天三才","陷入瓶颈"};
            for(int f=0;f<4;f++)
            {
                g.conf.roleCreateFeature.Items[f+1]=new FateItem{level=f==0?4:f==1?6:f==2?5:-1,type=f==3?2:1,name="preview"+f,tips="0"};
                ConfLocalText.Texts["preview"+f]=previewNames[f];
            }
            g.conf.roleCreateFeature.Items[9001]=new FateItem{level=2,type=3,name="catalog9001",tips="0"};ConfLocalText.Texts["catalog9001"]="未拥有气运";
            string[] heavenNames={"噬魂剑","阴阳眼","莲花剑意"};
            for(int h=0;h<3;h++){g.conf.roleCreateFeature.Items[2804+h]=new FateItem{level=6,type=1,name="heaven"+h,tips="0"};ConfLocalText.Texts["heaven"+h]=heavenNames[h];}
            var canvas=new UnityEngine.GameObject("canvas");canvas.transform.sizeDelta=new UnityEngine.Vector2(sizes[s][0],sizes[s][1]);g.ui=new LayoutUI { canvas=canvas.transform };
            bool closeRequested=false;
            var window=new UITextInfoLong();var bookView=new BookView();bookView.Build(window,()=>{closeRequested=true;});
            var appearance=window.GetComponentInChildren<UnityEngine.CanvasGroup>();
            Check(appearance!=null && appearance.alpha==0 && !appearance.interactable && appearance.blocksRaycasts,"opening starts transparent with blocked actions at "+sizes[s][0]);
            UnityEngine.Time.unscaledTime+=.08f;bookView.RefreshCharacter();
            Check(appearance.alpha>.4f && appearance.alpha<.6f && !appearance.interactable,"opening progresses before one-second data refresh at "+sizes[s][0]);
            UnityEngine.Time.unscaledTime+=.09f;bookView.RefreshCharacter();
            Check(appearance.alpha==1 && appearance.interactable,"opening completes and enables actions at "+sizes[s][0]);
            var originalButton=window.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(button=>button.gameObject.name=="G:btnOK");
            var inactiveButton=window.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(button=>button.gameObject.name=="native inactive");
            Check(!originalButton.gameObject.activeSelf && !originalButton.interactable,"native confirm button is inactive rather than only invisible");
            var book=(UnityEngine.RectTransform)Walk(window.transform).First(t=>t.gameObject.name=="BHML_秘录外框");
            var texts=book.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            string[] expectedTabs={"属性","道具","功法","关系","功能","批量","保存"};
            var tabs=Walk(book).Where(t=>t.gameObject.name.StartsWith("BHML_卷目") && t.gameObject.name!="BHML_卷目区").ToArray();
            Check(tabs.Length==7 && tabs.Select(t=>t.GetComponentInChildren<UnityEngine.UI.Text>().text).SequenceEqual(expectedTabs),"seven reference navigation entries in order without sponsor tab");
            var closeButton=Walk(book).First(t=>t.gameObject.name=="BHML_关闭").GetComponent<UnityEngine.UI.Button>();
            Check(closeButton.targetGraphic is UnityEngine.UI.RawImage && ((UnityEngine.UI.RawImage)closeButton.targetGraphic).texture.Pixels!=null && closeButton.GetComponentInChildren<UnityEngine.UI.Text>().text=="×","round framed x close control exists");
            Check(texts.Any(t=>t.text=="示例修士") && texts.Any(t=>t.text=="16 / 60"),"real BookView reads player and age at "+sizes[s][0]);
            bool allLabels=true;
            foreach(var group in MOD_YtxSQ1.Services.CharacterSnapshot.Labels) foreach(var label in group)
                allLabels &= texts.Count(t=>t.text==label)==1;
            Check(allLabels,"all 46 labels exist exactly once at "+sizes[s][0]);
            var cells=Walk(book).Where(t=>t.gameObject.name.StartsWith("BHML_属性格_")).Cast<UnityEngine.RectTransform>().ToArray();
            Check(cells.Length==46 && cells.All(c=>c.sizeDelta.y>=26 && c.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length>=3),"46 framed editable fields have room for label and value at "+sizes[s][0]);
            Check(texts.Any(t=>t.text=="玉器 · 书籍") && texts.Any(t=>t.text=="无私 · 义气 · 忠贞"),"reference hobby and personality values bind to view");
            Check(Walk(book).Any(t=>t.gameObject.name=="BHML_气运检索") && Walk(book).Count(t=>t.gameObject.name.StartsWith("BHML_快捷_"))==7,"fourth fate heading is a registered global search and library action is enabled");
            var restore=Walk(book).First(t=>t.gameObject.name=="BHML_快捷_满状态");
            Check(Walk(restore).Any(t=>t.gameObject.name=="BHML_金线上") && Walk(restore).Any(t=>t.gameObject.name=="BHML_金线右"),"full status action has a quiet gold inset frame");
            var dots=Walk(book).Where(t=>t.gameObject.name.StartsWith("BHML_说明点_")).ToArray();
            Check(dots.Length==6 && dots.All(t=>t.GetComponent<UnityEngine.UI.Text>().raycastTarget && t.GetComponent<UnityEngine.UI.Button>()!=null) && Walk(book).Where(t=>t.gameObject.name.StartsWith("BHML_快捷_")).All(t=>t.GetComponent<UIEventListener>()==null),"six overlay help diamonds receive hover and clicks independently from action bodies");
            Check(!Walk(restore).Any(t=>t.gameObject.name.StartsWith("BHML_说明点_")),"full status has no help mark or hover hint");
            var helpDot=dots.First(); var hintedButton=helpDot.parent;
            var beforeHp=g.world.playerUnit.data.dynUnitData.hp.Stored; helpDot.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Check(g.world.playerUnit.data.dynUnitData.hp.Stored==beforeHp,"clicking help dot does not run restore action");
            helpDot.GetComponent<UIEventListener>().OnPointerEnter(null);
            Check(Walk(book).Any(t=>t.gameObject.name=="BHML_气运介绍"),"target column action hover converts sibling coordinates safely");
            var actionTip=(UnityEngine.RectTransform)Walk(book).First(t=>t.gameObject.name=="BHML_气运介绍");
            var restoreBounds=BookView.FateRowBounds((UnityEngine.RectTransform)hintedButton,(UnityEngine.RectTransform)book);
            Check(actionTip.parent==book && Math.Abs(actionTip.anchoredPosition.x-restoreBounds.x)<1 && Math.Abs(-actionTip.anchoredPosition.y-restoreBounds.y)>restoreBounds.height,"action hint stays adjacent to its button in book coordinates");
            using(var bitmap=new Bitmap((int)book.sizeDelta.x,(int)book.sizeDelta.y))
            using(var graphics=Graphics.FromImage(bitmap))
            { graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;Paint(graphics,book,0,0,true);bitmap.Save(Path.Combine(args[1],"action-tip-07-33-layout-"+(int)sizes[s][0]+".png"),ImageFormat.Png); }
            helpDot.GetComponent<UIEventListener>().OnPointerExit(null);UnityEngine.Time.unscaledTime+=.2f;bookView.RefreshCharacter();
            Check(Walk(book).Count(t=>t.gameObject.name.StartsWith("BHML_气运行_0_"))==3 && Walk(book).First(t=>t.gameObject.name=="BHML_气运行_0_2").GetComponentInChildren<UnityEngine.UI.Text>().text=="天三才","all three born entries render as individual rows");
            var ownedRows=Walk(book).Where(t=>t.gameObject.name.StartsWith("BHML_气运行_")).ToArray();
            Check(ownedRows.All(t=>t.GetComponent<UnityEngine.UI.Image>().sprite==null && t.GetComponentInChildren<UnityEngine.UI.Text>().rectTransform.anchoredPosition.x==34) && ownedRows.SelectMany(t=>t.GetComponentsInChildren<UnityEngine.UI.Text>(true)).Any(t=>t.text=="仙"),"owned fates share custom grade backgrounds, crisp rank labels and reserved delete space");
            var bornColumn=Walk(book).First(t=>t.gameObject.name=="BHML_气运条目视口_0").GetComponent<UnityEngine.UI.ScrollRect>();
            var thirdBorn=(UnityEngine.RectTransform)Walk(book).First(t=>t.gameObject.name=="BHML_气运行_0_2");
            Check(-thirdBorn.anchoredPosition.y+thirdBorn.sizeDelta.y<=bornColumn.viewport.sizeDelta.y+.1f,"three ordinary born rows fit without scrolling at "+sizes[s][0]);
            Check(Walk(book).Count(t=>t.gameObject.name.StartsWith("BHML_移除气运_"))==4 && Walk(book).Where(t=>t.gameObject.name.StartsWith("BHML_移除气运_")).All(t=>t.GetComponent<UnityEngine.UI.Button>().interactable),"owned row x entries provide native removal actions");
            Check(!Walk(book).Any(t=>t.gameObject.name.StartsWith("BHML_查看气运_")) && !texts.Any(t=>t.text.Contains("点击查看")),"large summary buttons and count prompt removed");
            var globalSearch=Walk(book).First(t=>t.gameObject.name=="BHML_气运检索").GetComponentInChildren<UnityEngine.UI.InputField>();
            globalSearch.text="未拥有";
            Check(Walk(book).Any(t=>t.gameObject.name=="BHML_气运候选"&&t.GetComponentInChildren<UnityEngine.UI.Text>().text=="[逆] 未拥有气运"),"global search includes unowned configuration entries with category prefix");
            var candidate=Walk(book).First(t=>t.gameObject.name=="BHML_气运候选");
            Check(candidate.GetComponent<UnityEngine.UI.Image>().sprite==null && candidate.GetComponent<UnityEngine.UI.Image>().color.g>candidate.GetComponent<UnityEngine.UI.Image>().color.r && candidate.GetComponentInChildren<UnityEngine.UI.Text>().rectTransform.anchoredPosition.x==34,"candidate uses a sprite-free grade color and consistently inset title");
            globalSearch.text="莲花剑意";
            Check(candidate.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.text=="天") && Walk(candidate).Any(t=>t.gameObject.name=="BHML_天级纹饰") && candidate.GetComponent<UnityEngine.UI.Image>().color.r>candidate.GetComponent<UnityEngine.UI.Image>().color.g,"special talent uses heaven rank with amber gold and engraved frame");
            globalSearch.text="未拥有";
            Check(!Walk(candidate).First(t=>t.gameObject.name=="BHML_天级纹饰").gameObject.activeSelf && candidate.GetComponent<UnityEngine.UI.Image>().color.g>candidate.GetComponent<UnityEngine.UI.Image>().color.r,"pooled tile clears heaven ornament when reused for ordinary fate");
            Check(dots.All(t=>t.GetComponent<UnityEngine.UI.Text>().text=="◆" && t.parent.GetComponentInChildren<UnityEngine.UI.Text>().rectTransform.anchoredPosition.x==0),"help diamond overlays corner without shifting button title");
            Walk(book).First(t=>t.gameObject.name=="BHML_气运列表").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            var library=Walk(book).First(t=>t.gameObject.name=="BHML_气运图鉴");
            Check(Walk(library).Count(t=>t.gameObject.name=="BHML_气运候选")<128&&Walk(library).First(t=>t.gameObject.name=="BHML_图鉴长页").GetComponent<UnityEngine.RectTransform>().sizeDelta.y>10000,"thousands of catalog records use a bounded visible tile pool");
            Walk(library).First(t=>t.gameObject.name=="BHML_图鉴检索").GetComponentInChildren<UnityEngine.UI.InputField>().text="未拥有";
            Walk(library).First(t=>t.gameObject.name=="BHML_气运候选").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Walk(library).First(t=>t.gameObject.name=="BHML_确认气运选择").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Check(!Walk(book).Any(t=>t.gameObject.name=="BHML_气运图鉴")&&globalSearch.text==""&&Walk(book).Any(t=>t.gameObject.name=="BHML_气运候选"&&t.GetComponentInChildren<UnityEngine.UI.Text>().text=="[逆] 未拥有气运")&&g.world.playerUnit.data.unitData.propertyData.addLuck.Count==1,"library confirms selection into candidate panel without automatically adding luck");
            foreach(var panelName in MOD_YtxSQ1.Services.CharacterSnapshot.GroupNames)
            {
                var panel=Walk(book).First(t=>t.gameObject.name=="BHML_"+panelName);
                var panelCells=cells.Where(c=>c.parent==panel).ToArray();
                bool noOverlap=true;
                for(int a=0;a<panelCells.Length;a++) for(int b=a+1;b<panelCells.Length;b++)
                {
                    var ca=panelCells[a];var cb=panelCells[b];
                    noOverlap &= ca.anchoredPosition.x+ca.sizeDelta.x<=cb.anchoredPosition.x+.1f || cb.anchoredPosition.x+cb.sizeDelta.x<=ca.anchoredPosition.x+.1f ||
                        -ca.anchoredPosition.y+ca.sizeDelta.y<=-cb.anchoredPosition.y+.1f || -cb.anchoredPosition.y+cb.sizeDelta.y<=-ca.anchoredPosition.y+.1f;
                }
                Check(noOverlap,"attribute cells do not overlap in "+panelName+" at "+sizes[s][0]);
            }
            var hpValue=Walk(book).First(t=>t.gameObject.name=="BHML_编辑值_0:4:0").GetComponent<UnityEngine.UI.Button>();
            hpValue.onClick.Invoke();
            var editor=Walk(book).First(t=>t.gameObject.name=="BHML_属性输入_0:4:0").GetComponent<UnityEngine.UI.InputField>();
            Check(editor.text=="123" && editor.selectionAnchorPosition==0 && editor.selectionFocusPosition==3,"current resource opens only its own value and selects it");
            Check(!hpValue.GetComponentInChildren<UnityEngine.UI.Text>().enabled && editor.GetComponent<UnityEngine.RectTransform>().sizeDelta.x<60,"inline edit hides old digits and fits its text width");
            editor.text="123456";
            Check(editor.GetComponent<UnityEngine.RectTransform>().sizeDelta.x>=editor.textComponent.preferredWidth+4,"inline width measures new value before native label refresh and contains all digits");
            editor.text="100";editor.onEndEdit.Invoke("100");
            Check(g.world.playerUnit.data.dynUnitData.hp.Stored==123 && hpValue.GetComponentInChildren<UnityEngine.UI.Text>().text=="100" && Walk(book).First(t=>t.gameObject.name=="BHML_编辑值_0:4:1").GetComponentInChildren<UnityEngine.UI.Text>().text=="456","current resource draft changes only selected component and never writes game data");
            g.world.playerUnit.data.dynUnitData.hp.Stored=99;bookView.RefreshCharacter(true);
            Check(hpValue.GetComponentInChildren<UnityEngine.UI.Text>().text=="100","automatic snapshot refresh preserves pending draft");
            g.world.playerUnit.data.dynUnitData.hp.Stored=123;
            bookView.RefreshCharacter(true);
            var hpMax=Walk(book).First(t=>t.gameObject.name=="BHML_编辑值_0:4:1").GetComponent<UnityEngine.UI.Button>();hpMax.onClick.Invoke();
            editor=Walk(book).First(t=>t.gameObject.name=="BHML_属性输入_0:4:1").GetComponent<UnityEngine.UI.InputField>();
            Check(editor.text=="456","maximum opens its independent raw value without slash or current component");
            editor.text="90";editor.onEndEdit.Invoke("90");
            Check(hpValue.GetComponentInChildren<UnityEngine.UI.Text>().text=="90" && hpMax.GetComponentInChildren<UnityEngine.UI.Text>().text=="90" && g.world.playerUnit.data.dynUnitData.hpMax.Stored==456,"lower maximum explicitly stages clamped current without game writes");
            hpValue.onClick.Invoke();editor=Walk(book).First(t=>t.gameObject.name=="BHML_属性输入_0:4:0").GetComponent<UnityEngine.UI.InputField>();
            editor.text="2147483648";editor.onEndEdit.Invoke(editor.text);
            Check(editor.gameObject.activeSelf && hpValue.GetComponentInChildren<UnityEngine.UI.Text>().text=="90","overflow keeps input visible and preserves existing draft");
            UnityEngine.Time.frameCount++;UnityEngine.Input.Escape=true;bookView.RefreshCharacter();UnityEngine.Input.Escape=false;
            Check(!Walk(book).Any(t=>t.gameObject.name.StartsWith("BHML_属性输入_")) && MOD_YtxSQ1.UI.WindowInputScope.Registered.Count==2 && hpValue.GetComponentInChildren<UnityEngine.UI.Text>().enabled,"Escape cancels numeric input, restores digits and keeps fate search registered");
            var hobby=Walk(book).First(t=>t.gameObject.name=="BHML_编辑值_0:10:1").GetComponent<UnityEngine.UI.Button>();hobby.onClick.Invoke();
            Walk(book).First(t=>t.gameObject.name=="BHML_选项_204").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Check(hobby.GetComponentInChildren<UnityEngine.UI.Text>().text=="图册" && Walk(book).First(t=>t.gameObject.name=="BHML_编辑值_0:10:0").GetComponentInChildren<UnityEngine.UI.Text>().text=="玉器" && g.world.playerUnit.data.unitData.propertyData.hobby[1]==202,"hobby dropdown changes only its bound slot draft");
            var inner=Walk(book).First(t=>t.gameObject.name=="BHML_编辑值_1:10:0").GetComponent<UnityEngine.UI.Button>();inner.onClick.Invoke();
            Check(Walk(book).Any(t=>t.gameObject.name=="BHML_选项_4") && !Walk(book).Any(t=>t.gameObject.name=="BHML_选项_2"),"trait choices filter by real slot config type rather than guessed type numbers");
            g.conf.roleCreateCharacter.GetItem(4).xdash_54sd="待人宽厚的性格介绍";
            Walk(book).First(t=>t.gameObject.name=="BHML_选项_4").GetComponent<UIEventListener>().OnPointerEnter(null);
            Check(Walk(book).First(t=>t.gameObject.name=="BHML_气运介绍").GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.text=="待人宽厚的性格介绍"),"trait list hover uses actual localized configuration description");
            Walk(book).First(t=>t.gameObject.name=="BHML_选项_4").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Check(!Walk(book).Any(t=>t.gameObject.name=="BHML_气运介绍"),"choosing personality disposes associated floating introduction");
            Check(inner.GetComponentInChildren<UnityEngine.UI.Text>().text=="内在四" && g.world.playerUnit.data.unitData.propertyData.inTrait==1,"trait draft remains isolated from actual personality");
            Walk(book).First(t=>t.gameObject.name=="BHML_卷目6").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Check(Walk(book).Any(t=>t.gameObject.name=="BHML_属性格_体力") && MOD_YtxSQ1.Services.CharacterSave.Busy && g.world.playerUnit.data.dynUnitData.hp.Stored==90,"save applies drafts on current page and waits for real save callback");
            g.data.Complete(true);MOD_YtxSQ1.Services.CharacterSave.Poll();
            Check(!MOD_YtxSQ1.Services.CharacterSave.Busy && book.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.text=="修改已保存"),"save success is displayed only after callback");
            MOD_YtxSQ1.Services.CharacterEditSession.Shared.Clear();bookView.RefreshCharacter(true);
            var scroll=Walk(book).First(t=>t.gameObject.name=="BHML_属性视口").GetComponent<UnityEngine.UI.ScrollRect>();
            Check(scroll!=null && scroll.viewport.GetComponent<UnityEngine.UI.RectMask2D>()!=null && (!scroll.vertical || scroll.verticalScrollbar!=null),"long page has clipping and scrollbar at "+sizes[s][0]);
            var targetPanel=(UnityEngine.RectTransform)Walk(book).First(t=>t.gameObject.name=="BHML_对象栏");
            Check(Math.Abs(targetPanel.sizeDelta.y-(-scroll.viewport.anchoredPosition.y+scroll.viewport.sizeDelta.y))<.1f,"left panel and right viewport share bottom boundary at "+sizes[s][0]);
            if(!scroll.vertical) Check(Math.Abs(scroll.content.sizeDelta.y-scroll.viewport.sizeDelta.y)<.1f,"supplemental panel reaches shared bottom without extra blank strip");
            if(scroll.content.sizeDelta.x>=1180)
            {
                var frame=(UnityEngine.RectTransform)Walk(book).First(t=>t.gameObject.name=="BHML_属性总框");
                var supplement=(UnityEngine.RectTransform)Walk(book).First(t=>t.gameObject.name=="BHML_补充属性");
                Check(-frame.anchoredPosition.y+frame.sizeDelta.y<=-supplement.anchoredPosition.y,"supplemental attributes follow reference frame");
                var panels=MOD_YtxSQ1.Services.CharacterSnapshot.GroupNames.Take(5).Select(n=>(UnityEngine.RectTransform)Walk(book).First(t=>t.gameObject.name=="BHML_"+n)).ToArray();
                Check(panels.All(p=>p.parent==frame && p.anchoredPosition.y==panels[0].anchoredPosition.y && p.sizeDelta.y==panels[0].sizeDelta.y),"five panels share top and bottom boundaries");
            }
            globalSearch.text="莲花剑意";
            using(var bitmap=new Bitmap((int)book.sizeDelta.x,(int)book.sizeDelta.y))
            using(var graphics=Graphics.FromImage(bitmap))
            {
                graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                Paint(graphics,book,0,0,true);
                bitmap.Save(Path.Combine(args[1],"role-07-33-layout-"+(int)sizes[s][0]+".png"),ImageFormat.Png);
            }
            globalSearch.text="";
            var original=window.GetComponent<UnityEngine.UI.Text>();
            var rows=Walk(book).Where(t=>t.gameObject.name.StartsWith("BHML_修士行")).ToArray();
            Check(rows.Length<12 && rows.Length>0,"300 NPCs use a fixed row pool");
            var search=book.GetComponentInChildren<UnityEngine.UI.InputField>();
            Check(PortraitModel.LastData==null && !PortraitModel.LastDynamic,"ordinary player keeps static portrait path");
            var camera=ModelRenderTexture.LastCamera;
            Check(camera.transform.position.x==80 && camera.transform.position.y>240 && camera.transform.position.z==-1000 &&
                camera.transform.position.y-camera.orthographicSize<=-10 && camera.transform.position.y+camera.orthographicSize>=490 && camera.orthographicSize*camera.aspect>=110,"portrait shifts down while full bounds remain in camera view");
            Check(search!=null && search.textComponent!=null && search.interactable && !search.readOnly,"inline editable native InputField retains text components");
            Check(search.shouldActivateOnSelect && search.onValidateInput==null && !search.GetComponent<UnityEngine.EventSystems.EventTrigger>().enabled,"clone enables editing and clears inherited trigger and validation");
            search.text=" 修士299 ";
            Check(g.ui.Editor==null && rows[0].GetComponentInChildren<UnityEngine.UI.Text>().text=="修士299","typing filters immediately without opening a dialog or invoking prototype callbacks");
            rows[0].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Check(book.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.text=="599") && ModelRenderTexture.Active==1,"search selects matching NPC and replaces portrait resource");
            if(s==0)
            {
                for(int i=0;i<5;i++)
                {
                    search.text="特殊人物"+i;
                    rows[0].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Check(ConfDramaNpc.LastNativeId==8100+i && ModelRenderTexture.Active==(i==1?0:1),"special identity "+i+" uses native avatar/prefab path without requesting assembled data or duplicating renderer");
                }
                g.conf.dramaNpc.Ids["flower4"]=8204;
                g.conf.dramaNpc.Portraits[8204]=new PortraitModelData { body=9204 };
                bookView.RefreshCharacter(true);
                Check(ConfDramaNpc.LastNativeId==8204 && ModelRenderTexture.Active==1,"same selected unit refreshes native avatar when drama identity changes");
                g.conf.dramaNpc.Ids["flower4"]=8104;
                var missing=g.conf.dramaNpc.Portraits[8104];g.conf.dramaNpc.Portraits.Remove(8104);
                rows[0].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Check(ModelRenderTexture.Active==0 && book.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.text=="立绘暂不可用"),"missing special config releases old portrait instead of displaying unrelated ordinary model");
                Check(!Walk(book).First(t=>t.gameObject.name=="BHML_人物立绘").GetComponent<UnityEngine.UI.RawImage>().enabled && !Walk(book).Any(t=>t.gameObject.name=="BHML_特殊人物立绘"),"failure hides empty white RawImage and destroys native portrait host");
                g.conf.dramaNpc.Portraits[8104]=missing;
                search.text="修士299";rows[0].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Check(PortraitModel.LastData==null && !PortraitModel.LastDynamic && ModelRenderTexture.Active==1,"special failure can return to ordinary portrait without leaking renderer");
            }
            var itemButton=Walk(book).First(t=>t.gameObject.name=="BHML_卷目1").GetComponent<UnityEngine.UI.Button>();
            itemButton.onClick.Invoke();
            Check(!Walk(book).First(t=>t.gameObject.name=="BHML_对象栏").gameObject.activeInHierarchy && ModelRenderTexture.Active==0,"other module hides target panel and releases portrait");
            Check(book.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.text=="功能尚未开放") && !book.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.text=="寿命"),"module switch removes old attribute page");
            Walk(book).First(t=>t.gameObject.name=="BHML_卷目0").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            search.text="";
            Check(rows[0].GetComponentInChildren<UnityEngine.UI.Text>().text=="自己 · 示例修士","clearing inline text immediately restores full roster");
            search.text="示例修士";
            rows[0].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            g.world.playerUnit.data.dynUnitData.attack.Stored=222;bookView.RefreshCharacter(true);
            Check(book.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.text=="222"),"returning role page and refresh rebinds values");
            var ownedFates=new List<DataUnit.LuckData>();
            for(int f=0;f<35;f++)
            {
                ownedFates.Add(new DataUnit.LuckData {id=1000+f});
                g.conf.roleCreateFeature.Items[1000+f]=new FateItem {type=f==0?3:2,name="fate"+f,tips="tip"+f};
                ConfLocalText.Texts["fate"+f]="气运测试"+f;
                ConfLocalText.Texts["tip"+f]=new string('长',1600);
            }
            g.world.playerUnit.data.unitData.propertyData.bornLuck=null;
            g.world.playerUnit.data.unitData.propertyData.addLuck=ownedFates; bookView.RefreshCharacter(true);
            Check(Walk(book).First(t=>t.gameObject.name=="BHML_气运行_2_0").GetComponentInChildren<UnityEngine.UI.Text>().text=="气运测试0","fate individual row binds fate category at "+sizes[s][0]);
            var columnScroll=Walk(book).First(t=>t.gameObject.name=="BHML_气运条目视口_1").GetComponent<UnityEngine.UI.ScrollRect>();
            columnScroll.verticalNormalizedPosition=.4f;
            var stableRow=Walk(book).First(t=>t.gameObject.name=="BHML_气运行_1_0");
            bookView.RefreshCharacter(true);
            Check(columnScroll.content.childCount==34 && columnScroll.content.sizeDelta.y>columnScroll.viewport.sizeDelta.y && columnScroll.verticalScrollbar.gameObject.activeSelf,"all acquired entries live in clipped scrollable column");
            Check(columnScroll.verticalNormalizedPosition==.4f && Walk(book).First(t=>t.gameObject.name=="BHML_气运行_1_0")==stableRow,"unchanged refresh preserves inline rows and scroll position");
            ConfLocalText.Texts["tip1"]="第一条介绍";ConfLocalText.Texts["tip2"]="第二条介绍";bookView.RefreshCharacter(true);
            Walk(book).First(t=>t.gameObject.name=="BHML_气运行_1_0").GetComponent<UIEventListener>().OnPointerEnter(null);
            var firstTip=(UnityEngine.RectTransform)Walk(book).First(t=>t.gameObject.name=="BHML_气运介绍");var firstY=firstTip.anchoredPosition.y;
            Walk(book).First(t=>t.gameObject.name=="BHML_气运行_1_1").GetComponent<UIEventListener>().OnPointerEnter(null);
            var secondTip=(UnityEngine.RectTransform)Walk(book).First(t=>t.gameObject.name=="BHML_气运介绍");
            Check(secondTip.anchoredPosition.y<firstY,"different visible rows produce different tooltip heights at "+sizes[s][0]);
            var secondX=secondTip.anchoredPosition.x;
            Walk(book).First(t=>t.gameObject.name=="BHML_气运行_2_0").GetComponent<UIEventListener>().OnPointerEnter(null);
            Check(((UnityEngine.RectTransform)Walk(book).First(t=>t.gameObject.name=="BHML_气运介绍")).anchoredPosition.x!=secondX,"different columns produce different tooltip positions at "+sizes[s][0]);
            ConfLocalText.Texts["tip1"]=new string('长',1600);ConfLocalText.Texts["tip2"]=new string('长',1600);bookView.RefreshCharacter(true);
            var hovered=Walk(book).First(t=>t.gameObject.name=="BHML_气运行_1_20");
            var hover=hovered.GetComponent<UIEventListener>();
            Check(hovered.GetComponent<UnityEngine.UI.Button>()==null && hover!=null,"owned rows have hover listener without navigation button");
            hover.OnPointerEnter(null);
            var tip=(UnityEngine.RectTransform)Walk(book).First(t=>t.gameObject.name=="BHML_气运介绍");
            Check(tip.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.text==new string('长',1600)) && tip.sizeDelta.x<=300 && tip.sizeDelta.y<=300,"compact tooltip binds full long text and caps dimensions");
            var tipScroll=Walk(tip).First(t=>t.gameObject.name=="BHML_气运介绍视口").GetComponent<UnityEngine.UI.ScrollRect>();
            Check(tipScroll!=null && tipScroll.content.sizeDelta.y>tipScroll.viewport.sizeDelta.y,"long introduction scrolls inside compact tip");
            bookView.RefreshCharacter(true);
            Check(Walk(book).First(t=>t.gameObject.name=="BHML_气运介绍")==tip,"unchanged refresh preserves hovered tip");
            hover.OnPointerExit(null);tip.GetComponent<UIEventListener>().OnPointerEnter(null);UnityEngine.Time.unscaledTime+=.2f;bookView.RefreshCharacter();
            Check(Walk(book).Any(t=>t.gameObject.name=="BHML_气运介绍"),"moving from row into tip keeps scrollable introduction open");
            tip.GetComponent<UIEventListener>().OnPointerExit(null);UnityEngine.Time.unscaledTime+=.2f;bookView.RefreshCharacter();
            Check(!Walk(book).Any(t=>t.gameObject.name=="BHML_气运介绍"),"leaving row and popup closes introduction");
            hover.OnPointerEnter(null);columnScroll.onValueChanged.Invoke(new UnityEngine.Vector2(0,0));
            Check(!Walk(book).Any(t=>t.gameObject.name=="BHML_气运介绍"),"column scroll clears tip from previous row position");
            hover.OnPointerEnter(null);search.text="修士299";rows[0].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Check(!Walk(book).Any(t=>t.gameObject.name=="BHML_气运介绍" || t.gameObject.name.StartsWith("BHML_气运行_")),"target switch clears introduction and owned rows");
            search.text="示例修士";rows[0].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            hover=Walk(book).First(t=>t.gameObject.name=="BHML_气运行_2_0").GetComponent<UIEventListener>();hover.OnPointerEnter(null);
            Walk(book).First(t=>t.gameObject.name=="BHML_卷目1").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Check(!Walk(book).Any(t=>t.gameObject.name=="BHML_气运介绍"),"module switch releases tip");
            Walk(book).First(t=>t.gameObject.name=="BHML_卷目0").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            search.text="示例修士";rows[0].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            hover=Walk(book).First(t=>t.gameObject.name=="BHML_气运行_2_0").GetComponent<UIEventListener>();hover.OnPointerEnter(null);
            g.world.isIntoWorld=false;bookView.RefreshCharacter(true);
            Check(!Walk(book).Any(t=>t.gameObject.name=="BHML_气运介绍" || t.gameObject.name.StartsWith("BHML_气运行_")),"unavailable world clears tip and rows");
            Check(!book.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.text=="222"),"unavailable world clears old values");g.world.isIntoWorld=true;
            bookView.RefreshCharacter(true);
            var shortEntry=g.conf.roleCreateFeature.Items[1000];ConfLocalText.Texts[shortEntry.tips]="移动速度+20，脚力+100，身法熟练提升速度+50%";bookView.RefreshCharacter(true);
            hover=Walk(book).First(t=>t.gameObject.name=="BHML_气运行_2_0").GetComponent<UIEventListener>();hover.OnPointerEnter(null);
            tip=(UnityEngine.RectTransform)Walk(book).First(t=>t.gameObject.name=="BHML_气运介绍");
            Check(tip.sizeDelta.y<150,"short introduction has compact content-driven height");
            using(var bitmap=new Bitmap((int)book.sizeDelta.x,(int)book.sizeDelta.y))
            using(var graphics=Graphics.FromImage(bitmap))
            { graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;Paint(graphics,book,0,0,true);bitmap.Save(Path.Combine(args[1],"fate-tip-07-33-layout-"+(int)sizes[s][0]+".png"),ImageFormat.Png); }
            UnityEngine.Time.frameCount++;UnityEngine.Input.Escape=true;bookView.RefreshCharacter();UnityEngine.Input.Escape=false;
            Check(closeRequested,"Escape forwards window close callback when no editor or child view is open");
            closeButton.onClick.Invoke();Check(closeRequested,"x close forwards window close callback");
            Walk(book).First(t=>t.gameObject.name=="BHML_编辑值_0:4:0").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            var faultEditor=Walk(book).First(t=>t.gameObject.name=="BHML_属性输入_0:4:0").GetComponent<UnityEngine.UI.InputField>();
            UnityEngine.GameObject.ThrowDeactivateName=faultEditor.gameObject.name;
            bookView.Dispose();
            UnityEngine.GameObject.ThrowDeactivateName=null;
            Check(!faultEditor.transform.parent.Children.Contains(faultEditor.transform) && WindowInputScope.Registered.Count==0 && !(bool)typeof(BookView).GetField("endingEditor",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(bookView),"failed input deactivation still destroys editor and releases registration at "+sizes[s][0]);
            bookView.Dispose();Check(!Walk(book).Any(t=>t.gameObject.name=="BHML_气运介绍") && original.enabled && ModelRenderTexture.Active==0 && UnityEngine.Texture2D.LiveProcedural==0 && originalButton.gameObject.activeSelf && originalButton.interactable && !inactiveButton.gameObject.activeSelf,"dispose restores native controls, releases portrait and close texture, and is repeatable");
        }
        Console.WriteLine("BookView geometry, page binding and GDI previews complete. Not a game screenshot.");
    }
}

namespace MOD_YtxSQ1.UI { internal static class WindowInputScope { internal static readonly System.Collections.Generic.List<UnityEngine.UI.InputField> Registered = new System.Collections.Generic.List<UnityEngine.UI.InputField>(); internal static void Register(UnityEngine.UI.InputField field) { Registered.Add(field); } internal static void Unregister(UnityEngine.UI.InputField field) { Registered.Remove(field); } } }
