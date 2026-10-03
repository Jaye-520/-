// 执行真实隔离条件与补丁生命周期；不模拟IL2CPP挂钩或IME。
using System;
using System.Reflection;
using System.Collections.Generic;
using MOD_YtxSQ1.UI;
namespace UnityEngine
{
    public struct Vector2 { public float x,y; public Vector2(float a,float b){x=a;y=b;} }
    public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c) { x=a;y=b;z=c; } }
    public struct UIVertex { public Vector3 position; }
    public class TextGenerator { public List<UIVertex> verts=new List<UIVertex>();public int vertexCount { get { return verts.Count; } } }
    public class Rect { public Vector2 center; public float yMin=-50,yMax=50; public float height {get{return yMax-yMin;}} }
    public class RectTransform
    {
        public Rect rect=new Rect(); public Vector3 localPosition;
        public Vector3 TransformPoint(Vector3 v) { return new Vector3(v.x+localPosition.x,v.y+localPosition.y,v.z); }
        public Vector3 InverseTransformPoint(Vector3 v) { return new Vector3(v.x-localPosition.x,v.y-localPosition.y,v.z); }
    }
    public class Transform { public Transform parent;public string name="root"; }
    public class GameObject
    {
        public bool activeInHierarchy=true;public string name="field";public Transform transform=new Transform();
        public UnityEngine.UI.Selectable selectable;
        public T GetComponent<T>() where T:class { return selectable as T; }
    }
    public static class Debug { public static int Logs;public static void Log(string value) { Logs++; }public static void LogWarning(string value) { Logs++; } }
}
namespace UnityEngine.EventSystems
{
    public class BaseEventData { }
    public class EventSystem
    {
        public static EventSystem current=new EventSystem();public UnityEngine.GameObject currentSelectedGameObject;
        public void SetSelectedGameObject(UnityEngine.GameObject value,BaseEventData data) { currentSelectedGameObject=value; }
    }
}
namespace UnityEngine.UI
{
    public class Graphic { public T TryCast<T>() where T:class { return this as T; } }
    public class Text : Graphic { public int fontSize=17;public float pixelsPerUnit=1;public UnityEngine.Vector2 PixelOffset;public UnityEngine.Vector2 PixelAdjustPoint(UnityEngine.Vector2 p){return new UnityEngine.Vector2(p.x+PixelOffset.x,p.y+PixelOffset.y);}public UnityEngine.TextGenerator cachedTextGenerator=new UnityEngine.TextGenerator();public UnityEngine.RectTransform rectTransform=new UnityEngine.RectTransform(); }
    public class VertexHelper
    {
        public List<UnityEngine.UIVertex> Vertices=new List<UnityEngine.UIVertex>();public int currentVertCount { get { return Vertices.Count; } }
        public void PopulateUIVertex(ref UnityEngine.UIVertex vertex,int index) { vertex=Vertices[index]; }
        public void SetUIVertex(UnityEngine.UIVertex vertex,int index) { Vertices[index]=vertex; }
    }
    public class Selectable { public bool enabled=true,interactable=true;public bool IsInteractable() { return interactable; } }
    public class InputField
    {
        // 不同包装共享一个原生对象（这里以gameObject标识）；托管Equals仍为引用相等。
        public static bool operator ==(InputField a,InputField b)
        { return ReferenceEquals(a,b) || (!ReferenceEquals(a,null) && !ReferenceEquals(b,null) && ReferenceEquals(a.gameObject,b.gameObject)); }
        public static bool operator !=(InputField a,InputField b) { return !(a==b); }
        public override bool Equals(object other) { return base.Equals(other); }
        public override int GetHashCode() { return base.GetHashCode(); }
        public bool isFocused;public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();
        public string text="";public void DeactivateInputField() { isFocused=false; }
        public Text textComponent=new Text();public Graphic placeholder;
        public UnityEngine.RectTransform caretRectTrans; public void AssignPositioningIfNeeded() { }
        public void GenerateCaret(VertexHelper vertices,UnityEngine.Vector2 offset) { }
    }
}
public class UIBase
{
    public bool isActiveOperation=true;
    public List<UIOperationGroup> operationGroups=new List<UIOperationGroup>();
    public UIOperationGroup[] Children=new UIOperationGroup[0];
    public UnityEngine.UI.InputField[] InputChildren=new UnityEngine.UI.InputField[0];
    public T[] GetComponentsInChildren<T>(bool includeInactive) where T:class { return typeof(T)==typeof(UnityEngine.UI.InputField)?InputChildren as T[]:Children as T[]; }
    public virtual void UpdateHandleInput() { }
}
public class UIOperationGroup
{
    public bool enabled=true,isAutoSelect=true,m_isActiveOperation=true;public string name="native navigation";
    public UnityEngine.GameObject target=new UnityEngine.GameObject { name="G:btnOK" };
    public void Tick(UIBase owner,UnityEngine.UI.InputField field)
    {
        if(!enabled || !isAutoSelect || !m_isActiveOperation || !owner.isActiveOperation)return;
        UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject=target;
        field.DeactivateInputField();
    }
}
public class UITextInfoLong : UIBase { public override void UpdateHandleInput() { } }
namespace Il2CppSystem { public static class Environment { public static string StackTrace { get { return "native test stack"; } } } }
namespace HarmonyLib
{
    public class HarmonyMethod { public HarmonyMethod(Type type,string method) { } }
    public static class AccessTools { public static MethodInfo Method(Type type,string name,Type[] args=null) { return args==null ? type.GetMethod(name) : type.GetMethod(name,args); } }
    public class Harmony
    {
        public static int Count,FailAt;
        public Harmony(string id) { }
        public void Patch(MethodInfo method,HarmonyMethod prefix=null,HarmonyMethod postfix=null)
        { if(method==null) throw new Exception("missing target");Count++;if(Count==FailAt) throw new Exception("injected patch failure"); }
        public void UnpatchSelf() { Count=0; }
    }
}
internal static class WindowInputScopeHarness
{
    private static int passes;
    private static object Call(string name,params object[] values)
    { return typeof(WindowInputScope).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,values); }
    private static void Check(bool value,string name) { if(!value)throw new Exception(name);passes++;Console.WriteLine("PASS: "+name); }
    private static bool Allows(UnityEngine.GameObject target) { return (bool)Call("AllowSelection",target); }
    public static void Main()
    {
        var owner=new UITextInfoLong();var other=new UITextInfoLong();var field=new UnityEngine.UI.InputField();
        var catalog=new UnityEngine.UI.InputField();catalog.gameObject.name="BHML_气运检索";
        owner.InputChildren=new[]{catalog};WindowInputScope.Register(catalog);
        var child=new UIOperationGroup();var cached=new UIOperationGroup { enabled=false,isAutoSelect=false };
        owner.Children=new[] { child };owner.operationGroups.Add(child);owner.operationGroups.Add(cached);
        field.isFocused=true;UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject=field.gameObject;
        child.Tick(owner,field);
        Check(!field.isFocused && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name=="G:btnOK","reproduces navigation stealing focus independently of window keyboard handler");
        WindowInputScope.Attach(owner,field);
        Check(HarmonyLib.Harmony.Count==3 && !owner.isActiveOperation && other.isActiveOperation,"installs scoped selection and caret hooks while other windows keep navigation");
        var caret=new UnityEngine.UI.VertexHelper();
        for(int i=0;i<4;i++)caret.Vertices.Add(new UnityEngine.UIVertex { position=new UnityEngine.Vector3 { x=i%2,y=i<2 ? -17 : 7 } });
        field.text="字";
        for(int i=0;i<4;i++)field.textComponent.cachedTextGenerator.verts.Add(new UnityEngine.UIVertex { position=new UnityEngine.Vector3 { y=i<2 ? -4 : 4 } });
        Call("AlignCaret",field,caret,new UnityEngine.Vector2());
        Check(caret.Vertices[0].position.y==-12 && caret.Vertices[2].position.y==12 && caret.Vertices[1].position.x==1,"moves low caret up to glyph center without changing height or horizontal position");
        field.textComponent.PixelOffset=new UnityEngine.Vector2(0,.25f);
        Call("AlignCaret",field,caret,new UnityEngine.Vector2(0,5));
        Check(caret.Vertices[0].position.y==-11.75f && caret.Vertices[2].position.y==12.25f,"caret uses rendered text pixel rounding instead of input origin offset");
        field.textComponent.PixelOffset=new UnityEngine.Vector2();Call("AlignCaret",field,caret,new UnityEngine.Vector2());
        Call("AlignCaret",new UnityEngine.UI.InputField(),caret,new UnityEngine.Vector2 { y=30 });
        Check(caret.Vertices[0].position.y==-12,"leaves all other input field caret geometry unchanged");
        field.textComponent.rectTransform.localPosition=new UnityEngine.Vector3(0,6,0);
        field.caretRectTrans=new UnityEngine.RectTransform { localPosition=new UnityEngine.Vector3(0,-3,0) };
        Call("AlignCaret",field,caret,new UnityEngine.Vector2());
        Check(caret.Vertices[0].position.y==-3 && caret.Vertices[2].position.y==21,"caret and text with differing origins align in the actual caret local coordinates");
        field.caretRectTrans=null;field.textComponent.rectTransform.localPosition=new UnityEngine.Vector3();
        field.textComponent.rectTransform.rect.yMin=-9;field.textComponent.rectTransform.rect.yMax=9;
        Call("AlignCaret",field,caret,new UnityEngine.Vector2());
        Check(caret.Vertices[0].position.y==-9 && caret.Vertices[2].position.y==9 && caret.Vertices[1].position.x==1,"compact field clips oversized native caret to padded text bounds without moving x");
        field.textComponent.rectTransform.rect.yMin=-50;field.textComponent.rectTransform.rect.yMax=50;
        caret.Vertices[0]=new UnityEngine.UIVertex{position=new UnityEngine.Vector3(0,-12,0)};caret.Vertices[1]=new UnityEngine.UIVertex{position=new UnityEngine.Vector3(1,-12,0)};
        caret.Vertices[2]=new UnityEngine.UIVertex{position=new UnityEngine.Vector3(0,12,0)};caret.Vertices[3]=new UnityEngine.UIVertex{position=new UnityEngine.Vector3(1,12,0)};

        field.text="";var placeholder=new UnityEngine.UI.Text();field.placeholder=placeholder;
        for(int i=0;i<4;i++)placeholder.cachedTextGenerator.verts.Add(new UnityEngine.UIVertex { position=new UnityEngine.Vector3 { y=i<2 ? 0 : 8 } });
        Call("AlignCaret",field,caret,new UnityEngine.Vector2());
        Check(caret.Vertices[0].position.y==-8 && caret.Vertices[2].position.y==16,"empty field aligns caret to placeholder while preserving native height");
        field.text="";
        Check(!child.enabled && !child.isAutoSelect && !child.m_isActiveOperation && !cached.m_isActiveOperation,"disables child and cached groups without duplicate state capture");
        UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject=field.gameObject;
        field.isFocused=true;
        for(int i=0;i<30;i++) { child.Tick(owner,field);if(field.isFocused)field.text+="a"; }
        Check(field.isFocused && field.text.Length==30,"navigation no longer steals selection across repeated updates");
        var hidden=new UnityEngine.GameObject { name="G:btnOK",activeInHierarchy=false };
        Check(!Allows(hidden),"rejects direct explicit selection of an inactive confirm button");
        var disabled=new UnityEngine.GameObject { selectable=new UnityEngine.UI.Selectable { interactable=false } };
        Check(!Allows(disabled),"rejects active but noninteractable controls while editing");
        disabled.selectable.interactable=true;disabled.selectable.enabled=false;
        Check(!Allows(disabled),"rejects disabled Selectable components while editing");
        for(int i=0;i<30;i++)
        {
            if(Allows(hidden)) { UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject=hidden;field.DeactivateInputField(); }
            if(field.isFocused)field.text+="b";
        }
        Check(field.isFocused && field.text.Length==60 && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject==field.gameObject,"direct focus stealing attempts do not cancel editing across repeated frames");
        Check(Allows(field.gameObject) && Allows(null) && Allows(new UnityEngine.GameObject { selectable=new UnityEngine.UI.Selectable() }),"allows input self, outside click clearing and active interactive targets");
        field.isFocused=false;UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject=null;
        Check(Allows(hidden),"does not change focus behavior outside own search editing");
        field.isFocused=true;UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject=field.gameObject;
        var start=UnityEngine.Debug.Logs;
        for(int i=0;i<12;i++) Call("ObserveDeactivation",field);
        Check(UnityEngine.Debug.Logs-start==0,"diagnostics remain capped after repeated blocked transfers");
        field.isFocused=false;
        var editor=new UnityEngine.UI.InputField { isFocused=true, text="123" };
        WindowInputScope.Register(editor);
        var callback=new UnityEngine.UI.InputField { gameObject=editor.gameObject, text="1", textComponent=editor.textComponent };
        Check(!editor.Equals(callback) && (bool)Call("Owns",callback),"recognizes another managed wrapper of the same registered Unity input");
        WindowInputScope.Register(callback);
        var registered=(System.Collections.Generic.List<UnityEngine.UI.InputField>)typeof(WindowInputScope).GetField("editors",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        Check(registered.Count==2,"registering an alternate wrapper does not duplicate native input ownership");
        Call("AlignCaret",callback,caret,new UnityEngine.Vector2());
        Check(caret.Vertices[0].position.y==-12 && caret.Vertices[2].position.y==12,"caret hook aligns a registered input through its callback wrapper");
        UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject=editor.gameObject;
        Check(!Allows(hidden) && Allows(field.gameObject),"registered property input gets invalid-focus protection while legitimate search focus is allowed");
        WindowInputScope.Unregister(callback);
        Check(!(bool)Call("Owns",editor),"unregistering an alternate wrapper removes the same native input");
        Check(Allows(hidden),"unregistered property input no longer affects focus handling");
        catalog.isFocused=true;UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject=catalog.gameObject;
        Check(!Allows(hidden),"Attach rediscovers owned catalog search after clearing pre-build registrations");
        catalog.isFocused=false;UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject=null;
        WindowInputScope.Detach();WindowInputScope.Detach();
        Check(HarmonyLib.Harmony.Count==0 && owner.isActiveOperation && child.enabled && child.isAutoSelect && child.m_isActiveOperation,"closing restores original navigation and removes patches");
        Check(!cached.enabled && !cached.isAutoSelect && cached.m_isActiveOperation,"restores originally disabled group flags exactly");
        HarmonyLib.Harmony.FailAt=2;bool failed=false;
        try { WindowInputScope.Attach(owner,field); } catch { failed=true; }
        Check(failed && HarmonyLib.Harmony.Count==0 && owner.isActiveOperation && child.enabled && child.isAutoSelect,"partial patch failure rolls back navigation flags and hooks");
        Console.WriteLine("TOTAL: "+passes);
    }
}
