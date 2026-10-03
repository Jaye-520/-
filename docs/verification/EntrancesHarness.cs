// 编译真实入口类；只验证托管控制流及局部矩形模型，不模拟IL2CPP/CanvasScaler。
using System;
using System.Collections.Generic;
using MOD_YtxSQ1.UI;
using UnityEngine;
using UnityEngine.UI;
namespace UnityEngine {
    public class Object { public static void Destroy(Object o) { var go=o as GameObject; if(go!=null) { go.activeInHierarchy=false; go.transform.parent.Children.Remove(go.transform); } } }
    public class Component:Object { public GameObject gameObject; public Transform transform { get { return gameObject.transform; } } public T GetComponent<T>() where T:Component { return gameObject.GetComponent<T>(); } }
    public class GameObject:Object { public string name;public int layer;public bool activeInHierarchy=true;public RectTransform transform;private Dictionary<Type,Component> components=new Dictionary<Type,Component>();public GameObject(string n) { name=n;transform=new RectTransform {gameObject=this};components[typeof(RectTransform)]=transform; } public T AddComponent<T>() where T:Component,new() { if(components.ContainsKey(typeof(T)))return (T)components[typeof(T)];var c=new T {gameObject=this};components[typeof(T)]=c;return c; } public T GetComponent<T>() where T:Component { Component c;return components.TryGetValue(typeof(T),out c)?(T)c:null; } public void SetActive(bool value) {activeInHierarchy=value;} }
    public class Transform:Component {public Transform parent; public List<Transform> Children=new List<Transform>();public Vector3 localPosition;public Vector3 lossyScale=new Vector3(1,1,1);public void SetParent(Transform p,bool world) {parent=p;p.Children.Add(this);} public Vector3 InverseTransformPoint(Vector3 v) {return v;} public Vector3 TransformPoint(Vector3 v) {return v + localPosition;} }
    public class RectTransform:Transform {public Vector2 anchorMin,anchorMax,pivot,sizeDelta,anchoredPosition,offsetMin,offsetMax;public Rect rect {get {return new Rect(-sizeDelta.x/2,-sizeDelta.y/2,sizeDelta.x,sizeDelta.y);} }public void GetWorldCorners(Vector3[] c) {throw new Exception("Do not model IL2CPP output-array writeback as an ordinary C# array.");} }
    public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero=new Vector2(0,0),one=new Vector2(1,1);}
    public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 operator +(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}public static Vector3 operator *(Vector3 a,float b){return new Vector3(a.x*b,a.y*b,a.z*b);} }
    public struct Rect {public float xMin,yMin,xMax,yMax;public float width {get{return xMax-xMin;}}public float height {get{return yMax-yMin;}}public Rect(float x,float y,float w,float h){xMin=x;yMin=y;xMax=x+w;yMax=y+h;} }
    public struct Color {public Color(float r,float g,float b,float a){}public static Color white=new Color(1,1,1,1);}
    public static class Mathf {public static float Abs(float v){return Math.Abs(v);}public static float Clamp(float v,float a,float b){return Math.Min(b,Math.Max(a,v));} }
    public static class Time {public static float unscaledTime;}
    public static class Debug {public static void Log(string s){}public static void LogWarning(string s){}public static void LogError(string s){throw new Exception(s);} }
    public enum TextureFormat {RGBA32}public enum FilterMode {Bilinear,Trilinear}public enum TextureWrapMode {Clamp}public enum HideFlags {HideAndDontSave}public enum TextAnchor {MiddleCenter}
    public class Texture2D:Object {public Texture2D(int a,int b,TextureFormat c,bool d){}public FilterMode filterMode;public TextureWrapMode wrapMode;public HideFlags hideFlags;}
    public static class ImageConversion {public static bool LoadImage(Texture2D t,byte[] b,bool n){return b.Length>0;} }
        public class Font:Object {}
    public class Camera:Object {}public class Canvas {public Camera worldCamera;}
    public static class Application {public static string persistentDataPath=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),"entry-test-data");}
    public static class Screen {public static int width=1920,height=1080;}
    public enum KeyCode {F9}
    public static class Input {public static bool F9,Down,Held;public static Vector3 mousePosition;public static Vector2 mouseScrollDelta;public static bool GetKeyDown(KeyCode k){return F9;}public static bool GetMouseButtonDown(int k){return Down;}public static bool GetMouseButton(int k){return Held;}}
    public static class RectTransformUtility {
        public static bool Hit=true;
        public static bool RectangleContainsScreenPoint(RectTransform r,Vector3 p,Camera c){return Hit;}
        public static bool ScreenPointToLocalPointInRectangle(RectTransform r,Vector3 p,Camera c,out Vector2 local){local=new Vector2(p.x,p.y);return true;}
    }
}
namespace UnityEngine.Events {public class UnityAction {internal Action Call;public static explicit operator UnityAction(Action a){return new UnityAction {Call=a};}}public class UnityEvent {private List<UnityAction> calls=new List<UnityAction>();public void AddListener(UnityAction a){calls.Add(a);}public void RemoveAllListeners(){calls.Clear();}public void Invoke(){foreach(var a in calls.ToArray())a.Call();}}}
namespace UnityEngine.UI {
    public class Graphic:Component {public bool raycastTarget;public Color color;public RectTransform rectTransform {get{return (RectTransform)transform;}}}
    public class RawImage:Graphic {public Texture2D texture;}public class Image:Graphic {}
    public class Text:Graphic {public Font font=new Font();public string text;public int fontSize;public TextAnchor alignment;}
    public struct Navigation {public enum Mode {None}public Mode mode;}
    public struct ColorBlock {public Color normalColor,highlightedColor,pressedColor,disabledColor;public float fadeDuration;}
    public class Button:Component {public Graphic targetGraphic;public ColorBlock colors;public Navigation navigation;public bool interactable=true;public UnityEngine.Events.UnityEvent onClick=new UnityEngine.Events.UnityEvent();}
}
public class UIEventListener:Component {public UnityEngine.Events.UnityEvent onMouseEnter,onMouseExit;}
public class UIType {public int uiLayer;public static UIType MapMain=new UIType(),NPCInfo=new UIType();}
public class UIBase:Component {public UIType uiType;public Canvas canvas=new Canvas();}
public class UIMapMain:UIBase {public PlayerInfo uiPlayerInfo=new PlayerInfo();}
public class PlayerInfo {public Text textSPValue;}
public class UINPCInfo:UIBase {public NPCPanel uiUnitInfo=new NPCPanel();public Unit unit;}
public class NPCPanel {public Image imgHeresyBg;public Text textStand1;public Button btnAppellationAttack;}
public class Unit {public UnitData data=new UnitData();}
public class UnitData {public Identity unitData=new Identity();}
public class Identity {public string unitID;}
public class World {public bool isDestroy,isIntoWorld=true;}
public class FakeUI {public UIMapMain Map;public UINPCInfo Npc;public UIBase Top;public T GetUI<T>(UIType type) where T:UIBase {return GetUI(type) as T;}public UIBase GetUI(UIType type){return type==UIType.MapMain?(UIBase)Map:Npc;}public UIBase GetLayerTopUI(int l){return Top;} }
public static class g {public static bool isDestroy;public static World world=new World();public static FakeUI ui=new FakeUI();}
public static class UITipItem {public static int Tips;public static void AddTip(string s){Tips++;}}
namespace MOD_YtxSQ1.Services {internal static class CharacterReader {internal static string Context="world-one";internal static bool Valid=true;internal static string ContextKey(){return Context;}internal static Unit Resolve(string id){return Valid?new Unit():null;} } }
namespace MOD_YtxSQ1.Core {internal static class WorkshopController {internal static bool IsEditingText(){return false;}}}
internal static class EntrancesHarness {
    private static int count;private static void Check(bool ok,string s){if(!ok)throw new Exception(s);count++;Console.WriteLine("PASS: "+s);}
    private static RectTransform Entry(UIBase owner){return (RectTransform)owner.transform.Children.Find(c=>c.gameObject.name.StartsWith("BHML_"));}
    private static void Tick(WorkshopEntrances e){Time.unscaledTime+=.6f;e.Update();}
    public static void Main(){
        var layoutPath=System.IO.Path.Combine(Application.persistentDataPath,"BHML","entrance-layout.ini"); if(System.IO.File.Exists(layoutPath))System.IO.File.Delete(layoutPath);
        var mapGo=new GameObject("map");mapGo.layer=5;mapGo.transform.sizeDelta=new Vector2(1920,1080);g.ui.Map=mapGo.AddComponent<UIMapMain>();g.ui.Map.uiType=UIType.MapMain;
        var value=new GameObject("念力");value.transform.sizeDelta=new Vector2(80,24);value.transform.localPosition=new Vector3(100,-300,0);g.ui.Map.uiPlayerInfo.textSPValue=value.AddComponent<Text>();
        var npcGo=new GameObject("npc");npcGo.layer=7;npcGo.transform.sizeDelta=new Vector2(1920,1080);g.ui.Npc=npcGo.AddComponent<UINPCInfo>();g.ui.Npc.uiType=UIType.NPCInfo;
        var mark=new GameObject("正魔");mark.transform.sizeDelta=new Vector2(32,32);mark.transform.localPosition=new Vector3(-500,400,0);g.ui.Npc.uiUnitInfo.imgHeresyBg=mark.AddComponent<Image>();g.ui.Npc.uiUnitInfo.btnAppellationAttack=mark.AddComponent<Button>();g.ui.Npc.uiUnitInfo.textStand1=value.GetComponent<Text>();g.ui.Npc.unit=new Unit();g.ui.Npc.unit.data.unitData.unitID="npc-A";g.ui.Top=g.ui.Map;
        string selected="unset";int opens=0;var entrances=new WorkshopEntrances(id=>{selected=id;opens++;});Tick(entrances);
        Check(Entry(g.ui.Map)!=null && Entry(g.ui.Npc)!=null,"both native hosts receive entrances");
        var mapEntry=Entry(g.ui.Map);var npcEntry=Entry(g.ui.Npc);
        Check(mapEntry.gameObject.layer==5 && npcEntry.gameObject.layer==7 && npcEntry.Children[0].gameObject.layer==7 && npcEntry.Children[0].Children[0].gameObject.layer==7,"icon and tooltip inherit native camera layers");
        Check(mapEntry.localPosition.x==180 && mapEntry.localPosition.y==-300,"map entrance follows the native resource reading");
        // 出厂位置与WorkshopEntrances的NpcAnchorX/NpcAnchorY一致，改动任一处都必须同步本断言。
        Check(Math.Abs(npcEntry.localPosition.x-(-960f+0.2542969f*1920f))<.01f && Math.Abs(npcEntry.localPosition.y-(-540f+0.746458352f*1080f))<.01f,"NPC entrance uses the frozen user-measured position");
        Tick(entrances);Check(g.ui.Map.transform.Children.Count==1 && g.ui.Npc.transform.Children.Count==1,"polls do not duplicate controls");
        mapEntry.GetComponent<Button>().onClick.Invoke();Check(opens==1 && selected==null,"map click requests player");
        g.ui.Top=g.ui.Npc;Tick(entrances);g.ui.Npc.unit.data.unitData.unitID="npc-B";npcEntry.GetComponent<Button>().onClick.Invoke();Check(selected=="npc-B" && opens==2,"NPC click reads current panel target rather than creation target");
        g.ui.Top=g.ui.Map;npcEntry.GetComponent<Button>().onClick.Invoke();Check(opens==2,"covered NPC panel cannot open");
        g.ui.Top=g.ui.Npc;MOD_YtxSQ1.Services.CharacterReader.Valid=false;npcEntry.GetComponent<Button>().onClick.Invoke();Check(opens==2 && UITipItem.Tips==1,"invalid NPC never falls back to player");MOD_YtxSQ1.Services.CharacterReader.Valid=true;
        MOD_YtxSQ1.Services.CharacterReader.Context="world-two";npcEntry.GetComponent<Button>().onClick.Invoke();Check(opens==2,"stale context click rejected before next poll");Tick(entrances);Check(Entry(g.ui.Npc)!=npcEntry && !npcEntry.gameObject.activeInHierarchy,"context change replaces previous entrances");
        g.ui.Npc.gameObject.activeInHierarchy=false;Tick(entrances);Check(Entry(g.ui.Npc)==null && Entry(g.ui.Map)!=null,"NPC close releases only NPC entrance");
        value.transform.lossyScale=new Vector3(.5f,.5f,1);Tick(entrances);Check(Entry(g.ui.Map).sizeDelta.x==28,"icon size follows native reference scale");
        value.transform.localPosition=new Vector3(2000,-300,0);Tick(entrances);Check(Entry(g.ui.Map).localPosition.x==946,"position clamps inside host boundary");
        g.world.isIntoWorld=false;Tick(entrances);Check(Entry(g.ui.Map)==null,"world exit removes map entrance");entrances.Dispose();entrances.Dispose();Check(g.ui.Map.transform.Children.Count==0 && g.ui.Npc.transform.Children.Count==0,"repeat dispose leaves no controls");
        g.world.isIntoWorld=true;g.ui.Npc.gameObject.activeInHierarchy=true;g.ui.Top=g.ui.Map;value.transform.localPosition=new Vector3(100,-300,0);value.transform.lossyScale=new Vector3(1,1,1);
        entrances=new WorkshopEntrances(id=>{opens++;});Tick(entrances);mapEntry=Entry(g.ui.Map);
        // F9自由移动已移除：按键、拖动与滚轮都不应改变位置，也不应再产生坐标文件。
        var openCount=opens;
        Input.F9=true;Input.mousePosition=mapEntry.localPosition;Input.Down=true;Input.Held=true;Input.mouseScrollDelta=new Vector2(0,2);entrances.Update();
        Input.Down=false;Input.mousePosition=new Vector3(500,-200,0);entrances.Update();Input.Held=false;Input.mouseScrollDelta=Vector2.zero;entrances.Update();Input.F9=false;
        Tick(entrances);
        Check(mapEntry.localPosition.x==180 && mapEntry.localPosition.y==-300,"F9 press, drag and wheel leave the map entrance in place");
        Check(!System.IO.File.Exists(layoutPath),"F9 removal writes no coordinate file");
        mapEntry.GetComponent<Button>().onClick.Invoke();Check(opens==openCount+1,"map entrance click still opens after F9 removal");
        g.ui.Top=g.ui.Npc;Tick(entrances);npcEntry=Entry(g.ui.Npc);
        Check(Math.Abs(npcEntry.localPosition.x-(-960f+0.2542969f*1920f))<.01f && Math.Abs(npcEntry.localPosition.y-(-540f+0.746458352f*1080f))<.01f,"NPC entrance keeps the frozen position across polls");
        entrances.Dispose();
        entrances=new WorkshopEntrances(id=>{});Tick(entrances);Check(Entry(g.ui.Map).localPosition.x==180 && Math.Abs(Entry(g.ui.Npc).localPosition.y-(-540f+0.746458352f*1080f))<.01f,"recreated entrances reuse positions without any file");entrances.Dispose();
        Console.WriteLine("TOTAL: "+count);
    }
}
