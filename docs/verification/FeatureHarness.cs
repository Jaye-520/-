// Executes the real feature services against deterministic managed substitutes.
// Proves guards, dispatch and patch callback scope; does not emulate IL2CPP or native side effects.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MOD_YtxSQ1.Services;
public class Native {
    private static int next;
    public IntPtr Pointer = new IntPtr(++next);
    public T TryCast<T>() where T:class { return this as T; }
    public T Cast<T>() where T:class { return (T)(object)this; }
}
namespace UnityEngine {
    public struct Vector2Int {
        public int x,y; public Vector2Int(int x,int y){this.x=x;this.y=y;}
        public static bool operator==(Vector2Int a,Vector2Int b){return a.x==b.x&&a.y==b.y;}
        public static bool operator!=(Vector2Int a,Vector2Int b){return !(a==b);}
        public override bool Equals(object o){return o is Vector2Int && this==(Vector2Int)o;}
        public override int GetHashCode(){return x*31+y;}
    }
    public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static implicit operator Vector2(Vector3 v){return new Vector2(v.x,v.y);}}
    public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}public static float Distance(Vector3 a,Vector3 b){return (float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));}}
    public class Camera {}
    public class GameObject {public bool activeSelf=true;public void SetActive(bool value){activeSelf=value;}public Transform transform=new Transform();}
    public class Transform {public Transform Parent;public bool IsChildOf(Transform t){return this==t||Parent!=null&&Parent.IsChildOf(t);}}
    public struct Rect {public float xMin,yMin,width,height;public bool Contains(Vector2 v){return v.x>=xMin&&v.y>=yMin&&v.x<=xMin+width&&v.y<=yMin+height;}}
    public class RectTransform:Transform {public Rect rect=new Rect{xMin=-50,yMin=-50,width=100,height=100};}
    public static class RectTransformUtility {public static bool ScreenPointToLocalPointInRectangle(RectTransform r,Vector2 p,Camera c,out Vector2 l){l=p;return true;}}
    public static class Mathf {public static int RoundToInt(float f){return (int)Math.Round(f);}}
    public static class Input {public static bool Down,Up;public static Vector3 mousePosition;public static bool GetMouseButtonDown(int i){return Down;}public static bool GetMouseButtonUp(int i){return Up;}}
    public static class Time {public static float unscaledTime;}
    public static class Debug {public static void LogWarning(string s) {}}
}
namespace UnityEngine.UI {public class Image {public bool enabled=true;public UnityEngine.RectTransform rectTransform=new UnityEngine.RectTransform();public UnityEngine.Transform transform;public Image(){transform=rectTransform;}}}
namespace UnityEngine.EventSystems {public class PointerEventData {public UnityEngine.Vector2 position;public PointerEventData(EventSystem e){}}public struct RaycastResult {public UnityEngine.GameObject gameObject;}public class EventSystem {public static EventSystem current=new EventSystem();public UnityEngine.GameObject Hit;public void RaycastAll(PointerEventData d,Il2CppSystem.Collections.Generic.List<RaycastResult> l){if(Hit!=null)l.Add(new RaycastResult{gameObject=Hit});}}}
public class UIMinMap:Native {public UnityEngine.UI.Image imgBG=new UnityEngine.UI.Image(),imgMask=new UnityEngine.UI.Image();public bool isDrag;public void UpdateUI(){}public void OnUpdate(){}}
public class GridBase:Native {public GridBaseUI gridUI=new GridBaseUI();}
public class GridBaseUI:Native {public bool isOpen,isView;public UnityEngine.GameObject goMask=new UnityEngine.GameObject(),goMaskView=new UnityEngine.GameObject();public void UpdateUI(){}public void InitData(GridBase grid){}public void Destroy(){}public void HideGrid(){}public void ShowGrid(){}}
public class UIMgr {public UnityEngine.Camera uiCamera=new UnityEngine.Camera();}
namespace UnhollowerRuntimeLib { public static class DelegateSupport {public static T ConvertDelegate<T>(Delegate value)where T:class{return (typeof(T)==typeof(DynInt.DynObjectAddHandler)?(object)new DynInt.DynObjectAddHandler((Func<int>)value):new DynInt.DynObjectEqualHandler((Func<int>)value))as T;}} }
namespace Il2CppSystem {
    public class Action {private readonly System.Action callback; public Action(System.Action value){callback=value;} public void Invoke(){callback();} public static explicit operator Action(System.Action a){return new Action(a);}}
    namespace Collections.Generic { public class List<T>:System.Collections.Generic.List<T>{} }
}
namespace HarmonyLib {
    public class HarmonyMethod {public HarmonyMethod(Type t,string s){}}
    public class Harmony {public static int Attached,Released;public static bool Fail; public Harmony(string id){} public void Patch(MethodBase m,HarmonyMethod prefix=null,HarmonyMethod postfix=null,HarmonyMethod finalizer=null){if(Fail)throw new Exception("patch failure");Attached++;} public void UnpatchSelf(){Released++;}}
    public static class AccessTools {public static ConstructorInfo Constructor(Type t,Type[] args){return t.GetConstructor(args);}public static MethodInfo Method(Type t,string n){return t.GetMethod(n);}public static MethodInfo Method(Type t,string n,Type[] a){return t.GetMethod(n,a);} public static MethodInfo PropertyGetter(Type t,string n){return t.GetProperty(n).GetGetMethod();}public static MethodInfo PropertySetter(Type t,string n){return t.GetProperty(n).GetSetMethod();}}
}
public enum UnitRelationType {None=0,Married=8,Lover=9}
public class ConfRoleRelationItem:Native {public int type=8,gender,age,relationRequire;public string askText="native proposal";}
public class RelationConfig {public ConfRoleRelationItem Item=new ConfRoleRelationItem();public ConfRoleRelationItem GetRelationItem(WorldUnitBase a,WorldUnitBase b,UnitRelationType type){return Item;}}
public enum SchoolPostType {None,SchoolMain,BigElders,Elders,Inherit,In,Out}
public enum SchoolDepartmentType {None,Personnel,Security,Diplomacy,Constructor,Train}
public class DynInt:Native {
    public bool ReadOnly,isOverflow,lockValue;public int maxValue=int.MaxValue; public object onBaseValueSetCall=new object(); public readonly List<object> allMulCall=new List<object>(),allMulAddCall=new List<object>(),allEqualCall=new List<object>();
    public sealed class DynObjectEqualHandler {private Func<int> callback;public DynObjectEqualHandler(Func<int> f){callback=f;}public int Invoke(){return callback();}}
    private DynObjectEqualHandler equal;public void AddEqual(DynObjectEqualHandler f,string label){equal=f;allEqualCall.Add(f);}public void DelEqual(DynObjectEqualHandler f){allEqualCall.Remove(f);if(equal==f)equal=null;}
    public sealed class DynObjectAddHandler {private Func<int> callback;public DynObjectAddHandler(Func<int> f){callback=f;}public int Invoke(){return callback();}}
    public readonly List<DynObjectAddHandler> Added=new List<DynObjectAddHandler>();public System.Action AfterAdd;
    public void AddValue(DynObjectAddHandler handler,string name){Added.Add(handler);if(AfterAdd!=null)AfterAdd();}public void DelValue(DynObjectAddHandler handler){Added.Remove(handler);}
    public int Stored,Add; public System.Action AfterSet; public int baseValue {get{return Stored;}set{if(!ReadOnly)Stored=value;if(AfterSet!=null)AfterSet();}} public int value {get{return equal==null?Stored+Add+Added.Sum(h=>h.Invoke()):equal.Invoke();}}
}
public class DataProps:Native {
    public class PropsData:Native {public string soleID,soleIDHashData;public int propsCount;public ItemConfig propsItem=new ItemConfig();public PropsData Clone(){return new PropsData{soleID=soleID,soleIDHashData=soleIDHashData,propsCount=propsCount,propsItem=propsItem};}}
    public readonly List<PropsData> Items=new List<PropsData>();public DynInt Capacity;
    public PropsData GetProps(string id){return Items.Find(x=>x.soleID==id);}
    public int GetGridMaxNum(){return Capacity.value;}
}
public class ItemConfig {public int hide;}
public class DataUnit {
    public class RelationData:Native {public int GetIntim(WorldUnitBase u){return 0;}public string married="";public bool Lover=true;public bool IsRelation(WorldUnitBase other,UnitRelationType type){return type==UnitRelationType.Lover&&Lover;}}
    public class ActionMartialData:Native {public DataProps.PropsData data;}
    public class UnitInfoData:Native {
        public RelationData relationData=new RelationData();public DataObjectData objData=new DataObjectData();public string unitID,schoolID=""; public DataProps propData=new DataProps(); public UnityEngine.Vector2Int Point;
        public readonly List<ActionMartialData> Skills=new List<ActionMartialData>();
        public UnityEngine.Vector2Int GetPoint(){return Point;}
        public bool IsEquipProps(string id){return false;}
        public ActionMartialData GetActionMartial(string id){return Skills.Find(x=>x.data.soleID==id);}
    }
}
public class WorldUnitDynData:Native {public DataUnit.UnitInfoData unitData;public WorldUnitDynData(DataUnit.UnitInfoData info){unitData=info;}public DynInt sex=new DynInt{Stored=1},age=new DynInt{Stored=240};public DynInt hp=new DynInt{Stored=80},mp=new DynInt{Stored=30},sp=new DynInt{Stored=20},energy=new DynInt{Stored=15};public DynInt propGridNum=new DynInt{Stored=100,ReadOnly=true};}
public class WorldUnitData:Native {public DataUnit.UnitInfoData unitData=new DataUnit.UnitInfoData();public WorldUnitDynData dynUnitData;public MapBuildSchool school;public WorldUnitData(){dynUnitData=new WorldUnitDynData(unitData);unitData.propData.Capacity=dynUnitData.propGridNum;}}
public class WorldUnitBase:Native {
    public WorldUnitData data=new WorldUnitData();public bool isDie,Ghost,Async,ThrowAfterWrite;public int Refusal,Checks,Creates;public UnitActionBase Last;
    public int IsCreateAction(UnitActionBase a,bool tip){Checks++;a.unit=this;return Refusal;}
    public int CreateAction(UnitActionBase a,bool tip){if(!a.isInit)a.Init(this);Checks++;if(Refusal!=0)return Refusal;Creates++;Last=a;a.Apply();if(ThrowAfterWrite)throw new Exception("native partial failure");var r=a as UnitActionRoleBase;if(r!=null&&!Async)r.Complete();return 0;}
}
public class UnitActionBase:Native {public WorldUnitBase unit;public bool isInit;public virtual void Init(WorldUnitBase actor){unit=actor;isInit=true;}public virtual void Apply(){}}
public class UnitActionRoleBase:UnitActionBase {public int state;private Il2CppSystem.Action ended;public void AddEndCall(Il2CppSystem.Action a){ended=a;}public void Complete(){if(ended!=null)ended.Invoke();}}
public class TargetRole:UnitActionRoleBase {public WorldUnitBase Target;public TargetRole(WorldUnitBase t){Target=t;}}
public class UnitActionRoleBattle:TargetRole {public string battleType;public UnitActionRoleBattle(WorldUnitBase t,string type):base(t){battleType=type;}}
public class UnitActionRoleDrill:TargetRole {public UnitActionRoleDrill(WorldUnitBase t):base(t){}}
public class UnitActionRoleAttack:TargetRole {public UnitActionRoleAttack(WorldUnitBase t):base(t){}}
public class UnitActionRoleInvite:TargetRole {public UnitActionRoleInvite(WorldUnitBase t):base(t){}}
public class UnitActionRoleTrains:TargetRole {public UnitActionRoleTrains(WorldUnitBase t):base(t){}}
public class UnitActionRoleRoleSchoolRecruit:TargetRole {public UnitActionRoleRoleSchoolRecruit(WorldUnitBase t):base(t){}}
public class UnitActionRoleKill:TargetRole {public UnitActionRoleKill(WorldUnitBase t,bool a,bool b,bool c,bool d):base(t){}public override void Apply(){Target.isDie=true;}}
public class UnitActionRoleSave:UnitActionBase {}
public class UnitActionRoleGive:TargetRole {public UnitActionRoleGive(WorldUnitBase t,Il2CppSystem.Collections.Generic.List<DataProps.PropsData> list):base(t){}public static bool PropsItemCanGive(WorldUnitBase a,WorldUnitBase b,DataProps.PropsData p){return p.propsCount>0;}}
public class UnitActionRoleAskfor:TargetRole {public UnitActionRoleAskfor(WorldUnitBase t,string id,int count):base(t){}}
public class UnitActionRoleTeachSkill:TargetRole {public enum TeachType{Teach,Study}public UnitActionRoleTeachSkill(WorldUnitBase t,DataUnit.ActionMartialData d,TeachType ty):base(t){}public static bool SkillCanTeach(WorldUnitBase t,DataUnit.ActionMartialData d){return d!=null;}}
public class UnitActionFeedbackBase:UnitActionRoleBase {public int SpecialFeedback(WorldUnitBase u,UnitConditionData d){return 0;}public int DramaPackageFeedback(WorldUnitBase u,UnitConditionData d){return 2;}public object feedbackLog{get{return null;}}}
public class UnitConditionData {}
public class UnitActionFeedback104:UnitActionFeedbackBase {public void OnCreate(){}public bool schoolAdmin;public WorldUnitBase requestUnit;}
public class UnitActionRoleRelation:TargetRole {public UnitRelationType Type;public ConfRoleRelationItem Item;public bool isRequestMarried;public UnitActionRoleRelation(WorldUnitBase t,UnitRelationType type,ConfRoleRelationItem item):base(t){Type=type;Item=item;}public override void Apply(){if(!isRequestMarried||Item==null)throw new Exception("invalid proposal");}}
public class UnitActionRoleMarry:TargetRole {public UnitActionRoleMarry(WorldUnitBase t,bool done,UnityEngine.Vector2Int p):base(t){}}
public class UnitActionSchoolExit:UnitActionBase {public override void Apply(){unit.data.school=null;unit.data.unitData.schoolID="";}}
public class UnitActionSchoolSetPostType:UnitActionBase {public MapBuildSchool school;public object postItem=new object();public SchoolPostType postType;public bool isTest;private SchoolDepartmentType department;
public static bool IgnoreWrite,MissingConfig;
public UnitActionSchoolSetPostType(string id,SchoolPostType post,SchoolDepartmentType dep){school=g.world.build.GetBuild<MapBuildSchool>(id);postType=post;department=dep;if(MissingConfig)postItem=null;}
public override void Apply(){if(!IgnoreWrite){school.buildData.Post=postType;school.buildData.postData.Department=department;}}}
public class UnitActionSchoolJoin:UnitActionBase {private string id;public UnitActionSchoolJoin(string id,SchoolPostType post,SchoolDepartmentType department){this.id=id;}public override void Apply(){unit.data.unitData.schoolID=id;unit.data.school=g.world.build.GetBuild<MapBuildSchool>(id);}}
public class UnitActionSetPoint:UnitActionBase {public UnityEngine.Vector2Int point;public UnitActionSetPoint(UnityEngine.Vector2Int p){point=p;}public override void Apply(){unit.data.unitData.Point=point;}}
public class UnitActionPropMartialStudy:UnitActionBase {public bool isOneStudy;public void OnCreate(){} }
public class UnitActionRoleEscape:UnitActionBase {public bool isSureComplete;public override void Init(WorldUnitBase p){base.Init(p);} }
public class UnitActionRoleStealItem:UnitActionBase {public bool isStealComplete,isStealDiscover;public void PlayerToNPCAction(){} public static float CompleteRate(WorldUnitBase a,WorldUnitBase b){return .25f;}}
public class SchoolData {public string id;public SchoolPostType Post;public PostData postData=new PostData();public SchoolPostType GetPostType(string id){return Post;}}
public class PostData {public SchoolDepartmentType Department;public SchoolDepartmentType GetUnitDepartmentType(string id){return Department;}}
public class MapBuildSchool:Native {public SchoolData buildData=new SchoolData();public string name;public UnityEngine.Vector2Int[] points=new[]{new UnityEngine.Vector2Int(4,5)};}
public class Builds {public List<MapBuildSchool> Items=new List<MapBuildSchool>();public T GetBuild<T>(string id)where T:class{return Items.Find(x=>x.buildData.id==id)as T;}public List<T> GetBuilds<T>()where T:class{return Items.Cast<T>().ToList();}}
public class WorldUnitMgr {public int Reads;public List<WorldUnitBase> GetUnits(bool hidden){Reads++;return g.world.Units.Values.ToList();}}
public class World:Native {public WorldUnitMgr unit=new WorldUnitMgr();public bool isDestroy,isIntoWorld=true;public WorldUnitBase playerUnit;public Builds build=new Builds();public Dictionary<string,WorldUnitBase> Units=new Dictionary<string,WorldUnitBase>();}
public class DataMap {public class Map {public bool IsOpenGrid(UnityEngine.Vector2Int p){return false;}}}
public class Grid {public int mapWidth=100,mapHeight=100;public object GetGridData(UnityEngine.Vector2Int p){return p.x>=0&&p.y>=0&&p.x<100&&p.y<100?new object():null;}}
public class DeadRecord {public string unitID;public string[] name;}
public class DeadContainer {public Dictionary<string,DeadRecord> allUnitDie=new Dictionary<string,DeadRecord>();}
public class DataMgr:Native {public static bool isSaveData;public Grid grid=new Grid();public DeadContainer unitDie=new DeadContainer();}
public class DramaFunctionData {public WorldUnitBase unitLeft,unitRight;}
public static class DramaFunctionTool {public static string Last;public static void OptionsFunction(string s,DramaFunctionData d){Last=s;if(s=="npcReviveNoDrama_2")d.unitRight.Ghost=false;}}
public class DramaData {public WorldUnitBase unitLeft,unitRight;}
public static class DramaTool {public static bool OpenDrama(int id,DramaData d){return true;}}
public class DramaConfig {public object GetItem(int id){return id==610011?new object():null;}}
public class Config {public RelationConfig roleRelation=new RelationConfig();public DramaConfig dramaDialogue=new DramaConfig();}
public static class ConfRoleCreateFeatureEx {public static bool IsGhostLuck(WorldUnitBase u){return u!=null&&u.Ghost;}}
public static class g {public static bool isDestroy;public static World world;public static DataMgr data;public static Config conf=new Config();public static UIMgr ui=new UIMgr();}
public class DataObjectData:Native {private Dictionary<string,string> values=new Dictionary<string,string>();public bool ContainsKey(string k){return values.ContainsKey(k);}public string GetString(string k){return values[k];}public void SetString(string k,string v){values[k]=v;}}
public class UITaoistNameItem {public WorldUnitBase unit;public Widget gameObject=new Widget();public TextWidget textName=new TextWidget();public void InitData(){}public void ShowNoAppellation(){}}
public class Widget {public bool Active;public void SetActive(bool a){Active=a;}}
public class TextWidget {public string text;public Widget gameObject=new Widget();}
public class SkillCreateData {}
public class MartialTool {public class HitData {public int hitValue;public HitData(UnitCtrlBase unit,SkillCreateData data,int weapon,int magic,int hit){hitValue=hit;}}}
public class UnitCtrlBase:Native {public bool mIsDie;}
public class UnitCtrlPlayer:UnitCtrlBase {public UnitDataBase data;}
public class UnitDataBase:Native {
    public UnitCtrlBase unit; public DynInt maxMP=new DynInt{Stored=100},maxSP=new DynInt{Stored=80},maxHP=new DynInt{Stored=120};public int mp=20,sp=30,hp=40;
    public void AddMP(int add,bool emit){mp+=add;}public void AddSP(int add,bool emit){sp+=add;}public void AddHP(MartialTool.HitData hit){hp+=hit.hitValue;}
}
public class SkillBase {public UnitCtrlBase unit;}
public class SkillAttack:SkillBase {public SkillDataAttack data=new SkillDataAttack();public bool IsCreate(){return data.cd.value<=0;}}
public class SkillDataAttack {public DynInt cd=new DynInt{Stored=500};public SkillBase skill;public float cdTime{get{return 10;}}}
public class StepBase {public UnitCtrlBase unit;public StepDataBase data=new StepDataBase();public bool IsCreate(){return data.cd.value<=0;}}
public class StepDataBase {public DynInt cd=new DynInt{Stored=800};public StepBase step;public float cdTime{get{return 10;}}}
public class BattleMap {public UnitCtrlPlayer playerUnitCtrl;}
public class SceneBattle {public BattleMap battleMap=new BattleMap();}
public class MapWorld:Native {public Dictionary<string,GridBase> showGrids=new Dictionary<string,GridBase>();public int Stops;public UnityEngine.Vector2Int playerPoint;public void StopPlayerMove(){Stops++;}public void SetPlayerPosi(UnityEngine.Vector2Int point){playerPoint=point;}public int Refreshes;public void UpdateAllUI(){Refreshes++;}}
public class SceneMap {public MapWorld world=new MapWorld();}
public static class SceneType {public static SceneBattle battle;public static SceneMap map;}
namespace MOD_YtxSQ1.Services {
    internal static class CharacterReader {
        internal static WorldUnitBase Resolve(string id){if(g.isDestroy||g.world==null||!g.world.isIntoWorld||g.world.isDestroy)return null;WorldUnitBase u;if(string.IsNullOrEmpty(id))u=g.world.playerUnit;else g.world.Units.TryGetValue(id,out u);return u!=null&&!u.isDie?u:null;}
        internal static string ContextKey(){var p=Resolve(null);return p==null||g.data==null?null:p.Pointer+":"+g.data.Pointer;}
    }
    internal static class CharacterSave {
        internal static bool Busy;internal static int Pending;internal static void MarkDirty(string context){if(CharacterReader.ContextKey()==context)Pending++;}
        internal static WorldUnitBase RequireCurrentTarget(string context,string id,IntPtr pointer){var u=CharacterReader.Resolve(id);if(context!=CharacterReader.ContextKey()||u==null||u.Pointer!=pointer||Busy||DataMgr.isSaveData)throw new InvalidOperationException("stale context/target/save");return u;}
    }
}
internal static class FeatureHarness {
    private static int count;
    private static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
    private static bool Reject(System.Action action){try{action();return false;}catch{return true;}}
    private static WorldUnitBase Npc {get{return g.world.Units["npc"];}}
    private static void Reset(){CapacityRuntime.Dispose();FeatureRuntime.Dispose();g.conf=new Config();g.isDestroy=false;g.world=new World();g.data=new DataMgr();g.world.playerUnit=new WorldUnitBase();g.world.playerUnit.data.unitData.unitID="player";var npc=new WorldUnitBase();npc.data.unitData.unitID="npc";g.world.Units.Add("npc",npc);g.world.Units.Add("player",g.world.playerUnit);CharacterSave.Busy=false;CharacterSave.Pending=0;DataMgr.isSaveData=false;UnityEngine.Time.unscaledTime=0;SceneType.battle=null;SceneType.map=new SceneMap();HarmonyLib.Harmony.Fail=false;}
    private static object Hook(string name,params object[] args){return typeof(FeatureRuntime).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);}
    private static UnitCtrlPlayer Battle(){var p=new UnitCtrlPlayer();p.data=new UnitDataBase{unit=p};SceneType.battle=new SceneBattle();SceneType.battle.battleMap.playerUnitCtrl=p;return p;}
    private static void Decide(UnitActionFeedback104 action,int special=0,bool readScore=true){var scope=new object[]{action,null};Hook("RelationStart",scope);Hook("RelationSpecialChecked",action,special);if(readScore)Hook("RelationScoreRead",action.unit.data.unitData.relationData,g.world.playerUnit);action.state=2;Hook("RelationDecision",action);Hook("RelationFinished",scope[1],null);}
    private static void Main(){try{
        Reset();var all=WorkshopFeatures.Assists.Concat(WorkshopFeatures.Interactions).Concat(WorkshopFeatures.Schools).ToArray();Check(all.Select(x=>x.Id).Distinct().Count()==all.Length,"all feature IDs are unique");Check(all.Any(x=>x.Id=="cooldown")&&!all.Any(x=>x.Name.Contains("魅力")),"reference charm controls replaced by cooldown");Check(WorkshopFeatures.CapacityTarget(100)==1099&&Reject(()=>WorkshopFeatures.CapacityTarget(int.MaxValue)),"capacity addition checked before write");
        foreach(var bad in new[]{"", " ", "openUI_X\n", "a|b", "a;b",new string('a',513)})Check(!FeatureOperations.ValidCommand(bad),"single command rejects malformed or batch input");Check(FeatureOperations.ValidCommand("openUI_UIPlayerInfo"),"valid single native command accepted");
        Check(FeatureOperations.Availability("battle","player")!=null,"self interaction rejected before native dispatch");foreach(var f in all.Where(x=>x.Unavailable!=null))Check(FeatureOperations.Availability(f.Id,"npc")!=null,"unverified capability disabled: "+f.Id);
        for(int i=0;i<50;i++)foreach(var f in all)FeatureOperations.Availability(f.Id,"npc");Check(g.world.playerUnit.Checks==0&&Npc.Checks==0,"availability refresh never initializes or checks native actions with side effects");
        var marriage=FeatureOperations.Prepare("marry","npc");FeatureOperations.Execute(marriage);var proposal=g.world.playerUnit.Last as UnitActionRoleRelation;Check(proposal!=null&&proposal.Target==Npc&&proposal.Type==UnitRelationType.Married&&proposal.isRequestMarried&&proposal.Item==g.conf.roleRelation.Item,"marriage dispatches configured native proposal, never outcome or outdoor venue");Check(g.world.playerUnit.Checks==1&&g.world.playerUnit.Creates==1,"proposal initializes and checks once");
        Reset();g.conf.roleRelation.Item=null;Check(Reject(()=>FeatureOperations.Prepare("marry","npc"))&&g.world.playerUnit.Creates==0&&CharacterSave.Pending==0,"missing marriage config rejected before mutation");
        Reset();Npc.data.unitData.relationData.married="other";Check(Reject(()=>FeatureOperations.Prepare("marry","npc")),"target spouse conflict rejected");
        Reset();g.world.playerUnit.data.unitData.relationData.married="other";Check(Reject(()=>FeatureOperations.Prepare("marry","npc")),"player spouse conflict rejected");
        Reset();g.conf.roleRelation.Item.gender=3;Check(Reject(()=>FeatureOperations.Prepare("marry","npc")),"native marriage gender gate retained");
        Reset();g.conf.roleRelation.Item.age=1;Check(Reject(()=>FeatureOperations.Prepare("marry","npc")),"native marriage age gap gate retained");
        Reset();g.conf.roleRelation.Item.relationRequire=9;g.world.playerUnit.data.unitData.relationData.Lover=false;Check(Reject(()=>FeatureOperations.Prepare("marry","npc")),"native marriage required relationship retained");
        Reset();g.conf.roleRelation.Item.age=1;Npc.data.dynUnitData.age.Stored=252;marriage=FeatureOperations.Prepare("marry","npc");FeatureOperations.Execute(marriage);Check(g.world.playerUnit.Creates==1,"positive native age threshold accepts exact twelve-month boundary");
        Reset();g.conf.roleRelation.Item.age=-1;Npc.data.dynUnitData.age.Stored=228;marriage=FeatureOperations.Prepare("marry","npc");FeatureOperations.Execute(marriage);Check(g.world.playerUnit.Creates==1,"negative native age threshold accepts exact twelve-month boundary");
        Reset();g.conf.roleRelation.Item.askText="";Check(Reject(()=>FeatureOperations.Prepare("marry","npc"))&&CharacterSave.Pending==0,"missing native proposal dialogue prevents submission");
        Reset();marriage=FeatureOperations.Prepare("marry","npc");Npc.data.unitData.relationData=new DataUnit.RelationData();Check(Reject(()=>FeatureOperations.Execute(marriage))&&g.world.playerUnit.Creates==0,"marriage container replacement prevents submission");
        Reset();marriage=FeatureOperations.Prepare("marry","npc");g.conf.roleRelation.Item=new ConfRoleRelationItem();Check(Reject(()=>FeatureOperations.Execute(marriage))&&g.world.playerUnit.Creates==0,"marriage config replacement prevents submission");
        Reset();marriage=FeatureOperations.Prepare("marry","npc");Npc.data.unitData.relationData.married="other";Check(Reject(()=>FeatureOperations.Execute(marriage))&&g.world.playerUnit.Creates==0,"marriage conflict after confirmation prevents submission");
        Reset();g.world.playerUnit.Async=true;marriage=FeatureOperations.Prepare("marry","npc");FeatureOperations.Execute(marriage);Check(FeatureOperations.Busy&&Reject(()=>FeatureOperations.Execute(marriage)),"proposal conversation remains busy and rejects duplicate submissions");((UnitActionRoleBase)g.world.playerUnit.Last).Complete();Check(!FeatureOperations.Busy,"native proposal completion releases busy state");
        Reset();var patchCount=HarmonyLib.Harmony.Attached;g.world.isIntoWorld=false;Npc.data.unitData.objData.SetString(CapacityRuntime.Key,"999");CapacityRuntime.Initialize();CapacityRuntime.Tick();Check(HarmonyLib.Harmony.Attached==patchCount&&g.world.unit.Reads==0&&Npc.data.dynUnitData.propGridNum.Added.Count==0,"capacity initialization installs no constructor patches and leaves new-game/loading units untouched");
        g.world.isIntoWorld=true;var loadingDynamic=g.world.playerUnit.data.dynUnitData;g.world.playerUnit.data.dynUnitData=null;CapacityRuntime.Tick();Check(g.world.unit.Reads==0,"capacity defers work while player dynamic data is incomplete");g.world.playerUnit.data.dynUnitData=loadingDynamic;CapacityRuntime.Tick();Check(Npc.data.unitData.propData.GetGridMaxNum()==1099,"capacity resumes only after world and player are complete");
        var rosterReads=g.world.unit.Reads;for(int frame=0;frame<120;frame++)CapacityRuntime.Tick();Check(g.world.unit.Reads==rosterReads,"capacity does not rescan the world roster on steady frames");
        Reset();var r=FeatureOperations.Prepare("capacity","npc");FeatureOperations.Execute(r);Check(Npc.data.dynUnitData.propGridNum.value==1099&&CharacterSave.Pending>0,"capacity persists additive bonus and native bag reads expansion despite empty base setter");
        Check(Npc.data.dynUnitData.propGridNum.baseValue==100&&CapacityRuntime.Bonus(Npc.data.unitData.objData)==999,"capacity keeps native configured base and records only its own bonus");
        r=FeatureOperations.Prepare("capacity","npc");FeatureOperations.Execute(r);Check(Npc.data.unitData.propData.GetGridMaxNum()==2098&&Npc.data.dynUnitData.propGridNum.Added.Count==1,"repeated expansion replaces owned additive effect without stacking duplicate handlers");
        CapacityRuntime.Dispose();Check(Npc.data.unitData.propData.GetGridMaxNum()==100&&CapacityRuntime.Bonus(Npc.data.unitData.objData)==1998,"mod unload releases runtime bonus but preserves persistent record");CapacityRuntime.Initialize();CapacityRuntime.Tick();Check(Npc.data.unitData.propData.GetGridMaxNum()==2098,"runtime reload restores persisted capacity for existing units");
        var replacement=new WorldUnitDynData(Npc.data.unitData);Npc.data.dynUnitData=replacement;Npc.data.unitData.propData.Capacity=replacement.propGridNum;UnityEngine.Time.unscaledTime+=1.1f;CapacityRuntime.Tick();Check(Npc.data.unitData.propData.GetGridMaxNum()==2098,"main-thread bounded check restores capacity after native dynamic data reconstruction");
        Reset();Npc.data.dynUnitData.propGridNum.Add=40;r=FeatureOperations.Prepare("capacity","npc");FeatureOperations.Execute(r);Check(Npc.data.unitData.propData.GetGridMaxNum()==1139&&Npc.data.dynUnitData.propGridNum.Add==40,"capacity preserves existing native additive effects");
        Reset();r=FeatureOperations.Prepare("capacity","npc");Npc.data.unitData.objData=new DataObjectData();Check(Reject(()=>FeatureOperations.Execute(r))&&CharacterSave.Pending==0,"capacity record container replacement rejected before write");
        Reset();r=FeatureOperations.Prepare("capacity","npc");Npc.data.unitData.objData.SetString(CapacityRuntime.Key,"5");Check(Reject(()=>FeatureOperations.Execute(r))&&CharacterSave.Pending==0,"capacity record content conflict rejected before write");
        Reset();Npc.data.unitData.objData.SetString(CapacityRuntime.Key,"invalid");Check(Reject(()=>FeatureOperations.Prepare("capacity","npc"))&&CharacterSave.Pending==0,"corrupt persistent capacity rejected without write");
        Reset();Npc.data.dynUnitData.propGridNum.maxValue=1000;Check(Reject(()=>FeatureOperations.Prepare("capacity","npc"))&&CharacterSave.Pending==0,"native capacity clamp checked before persistent write");
        Reset();r=FeatureOperations.Prepare("capacity","npc");Npc.data.dynUnitData.propGridNum.Stored++;Check(Reject(()=>FeatureOperations.Execute(r))&&CharacterSave.Pending==0,"capacity conflict rejected without native write");
        Reset();r=FeatureOperations.Prepare("capacity","npc");Npc.data.dynUnitData.propGridNum.AfterAdd=()=>{throw new Exception("after effect attachment");};Check(Reject(()=>FeatureOperations.Execute(r))&&CharacterSave.Pending>0&&Npc.data.dynUnitData.propGridNum.value==1099,"partial capacity failure preserves Pending and no blind rollback");
        Reset();Npc.data.unitData.objData.SetString(CapacityRuntime.Key,"999");Npc.data.dynUnitData.propGridNum.AfterAdd=()=>{throw new Exception("effect attachment");};CapacityRuntime.Initialize();CapacityRuntime.Tick();Check(FeatureOperations.Availability("capacity","npc")!=null,"uncertain restored effect attachment cannot be treated as complete or retried blindly");
        Reset();g.world.playerUnit.data.unitData.objData.SetString(CapacityRuntime.Key,"invalid");Npc.data.unitData.objData.SetString(CapacityRuntime.Key,"999");CapacityRuntime.Initialize();CapacityRuntime.Tick();Check(Npc.data.unitData.propData.GetGridMaxNum()==1099,"one malformed unit record cannot prevent other units from restoring capacity");
        Reset();Npc.data.unitData.objData.SetString(CapacityRuntime.Key,"999");CapacityRuntime.Initialize();CapacityRuntime.Tick();g.world.isIntoWorld=false;CapacityRuntime.Tick();g.world=new World();g.data=new DataMgr();g.world.playerUnit=new WorldUnitBase();g.world.playerUnit.data.unitData.unitID="player";g.world.Units.Add("player",g.world.playerUnit);CapacityRuntime.Tick();Check(g.world.playerUnit.data.unitData.propData.GetGridMaxNum()==100,"capacity runtime never carries a previous world's bonus into a new world");
        Reset();r=FeatureOperations.Prepare("drill","npc");g.world.Units["npc"]=new WorldUnitBase();Check(Reject(()=>FeatureOperations.Execute(r))&&g.world.playerUnit.Creates==0,"same ID replacement rejected");
        Reset();r=FeatureOperations.Prepare("drill","npc");Npc.data=new WorldUnitData();Check(Reject(()=>FeatureOperations.Execute(r))&&g.world.playerUnit.Creates==0,"container replacement rejected");
        Reset();r=FeatureOperations.Prepare("drill","npc");g.data=new DataMgr();Check(Reject(()=>FeatureOperations.Execute(r))&&g.world.playerUnit.Creates==0,"world data replacement rejected");
        Reset();r=FeatureOperations.Prepare("drill","npc");g.world.playerUnit.Refusal=7;Check(Reject(()=>FeatureOperations.Execute(r))&&g.world.playerUnit.Creates==0&&CharacterSave.Pending>0&&!FeatureOperations.Busy&&FeatureOperations.Availability("post","npc")!="原生动作可能部分执行，已阻止重复提交；请退出存档后核对","native refusal preserved without global quarantine");
        Reset();r=FeatureOperations.Prepare("drill","npc");DataMgr.isSaveData=true;Check(Reject(()=>FeatureOperations.Execute(r))&&g.world.playerUnit.Creates==0,"saving blocks prepared action");
        Reset();r=FeatureOperations.Prepare("drill","npc");g.world.playerUnit.Async=true;FeatureOperations.Execute(r);Check(FeatureOperations.Busy,"asynchronous native action remains busy");Check(Reject(()=>FeatureOperations.Execute(r))&&g.world.playerUnit.Creates==1,"repeat submission blocked while native action active");((UnitActionRoleBase)g.world.playerUnit.Last).Complete();Check(!FeatureOperations.Busy,"native completion callback releases busy state");
        Reset();r=FeatureOperations.Prepare("drill","npc");g.world.playerUnit.Async=true;FeatureOperations.Execute(r);Npc.data=new WorldUnitData();((UnitActionRoleBase)g.world.playerUnit.Last).Complete();Check(FeatureOperations.Busy&&CharacterSave.Pending>0,"async completion identity conflict preserves Pending and blocks replay");
        Reset();r=FeatureOperations.Prepare("drill","npc");g.world.playerUnit.ThrowAfterWrite=true;Check(Reject(()=>FeatureOperations.Execute(r))&&CharacterSave.Pending>0,"native partial failure preserves Pending");Check(FeatureOperations.Busy && Reject(()=>FeatureOperations.Prepare("drill","npc")),"uncertain partial native failure blocks replay until world reset");
        Reset();var props=new DataProps.PropsData{soleID="unique",soleIDHashData="sig",propsCount=2};g.world.playerUnit.data.unitData.propData.Items.Add(props);r=FeatureOperations.Prepare("give","npc","unique");props.soleIDHashData="changed";Check(Reject(()=>FeatureOperations.Execute(r))&&g.world.playerUnit.Creates==0,"gift unique record content conflict rejected");
        Reset();Npc.Ghost=true;r=FeatureOperations.Prepare("revive","npc");FeatureOperations.Execute(r);Check(!Npc.Ghost&&DramaFunctionTool.Last=="npcReviveNoDrama_2","revive resolves right role, never left player");
        Reset();r=FeatureOperations.Prepare("drama","npc","610011",leftId:"player",rightId:"npc");g.world.Units["npc"]=new WorldUnitBase();Check(Reject(()=>FeatureOperations.Execute(r)),"drama roles bound by native identity");Check(Reject(()=>FeatureOperations.Prepare("drama","player","0",leftId:"player",rightId:"player")),"unknown drama ID rejected");
        Reset();r=FeatureOperations.Prepare("drama","npc","610011",leftId:"player",rightId:"npc");Npc.data=new WorldUnitData();Check(Reject(()=>FeatureOperations.Execute(r)),"drama role container replacement rejected");
        Reset();Npc.isDie=true;Check(FeatureOperations.Availability("revive","npc")!=null,"removed dead target cannot bypass living write guard");
        Reset();r=FeatureOperations.Prepare("move","player","5,6");FeatureOperations.Execute(r);Check(g.world.playerUnit.data.unitData.GetPoint()==new UnityEngine.Vector2Int(5,6),"map movement uses native point action and readback");
        Reset();r=FeatureOperations.Prepare("move","player","-1,6");Check(Reject(()=>FeatureOperations.Execute(r))&&g.world.playerUnit.Creates==0,"invalid map grid rejected before native action");
        Reset();var school=new MapBuildSchool{name="native school"};school.buildData.id="school";g.world.build.Items.Add(school);r=FeatureOperations.Prepare("join","npc",schoolId:"school");FeatureOperations.Execute(r);Check(Npc.data.unitData.schoolID=="school"&&Npc.Creates==1&&g.world.playerUnit.Creates==0,"school join acts on selected target through native action");
        r=FeatureOperations.Prepare("post","npc",post:SchoolPostType.Elders,department:SchoolDepartmentType.Train);FeatureOperations.Execute(r);Check(school.buildData.Post==SchoolPostType.Elders&&school.buildData.postData.Department==SchoolDepartmentType.Train,"school post and department native readback");Check(((UnitActionSchoolSetPostType)Npc.Last).isTest,"school post uses same administrative option as native drama entry");int beforeNoop=Npc.Creates;r=FeatureOperations.Prepare("post","npc",post:SchoolPostType.Elders,department:SchoolDepartmentType.Train);FeatureOperations.Execute(r);Check(Npc.Creates==beforeNoop,"same post and department is no-op");
        r=FeatureOperations.Prepare("post","npc");UnitActionSchoolSetPostType.MissingConfig=true;int postCreates=Npc.Creates;Check(Reject(()=>FeatureOperations.Execute(r))&&Npc.Creates==postCreates&&!FeatureOperations.Busy,"missing school post config rejected before dispatch");UnitActionSchoolSetPostType.MissingConfig=false;
        r=FeatureOperations.Prepare("post","npc");var originalSchool=Npc.data.school;Npc.data.school=new MapBuildSchool();Check(Reject(()=>FeatureOperations.Execute(r)),"same membership but replaced school instance rejected");Npc.data.school=originalSchool;
        r=FeatureOperations.Prepare("exit","npc");FeatureOperations.Execute(r);Check(Npc.data.unitData.schoolID=="","school exit native identity readback");
        Reset();school=new MapBuildSchool{name="native school"};school.buildData.id="school";g.world.build.Items.Add(school);Npc.data.school=school;Npc.data.unitData.schoolID="school";r=FeatureOperations.Prepare("post","npc",post:SchoolPostType.In);UnitActionSchoolSetPostType.IgnoreWrite=true;Check(Reject(()=>FeatureOperations.Execute(r))&&FeatureOperations.Busy&&CharacterSave.Pending>0,"unchanged native post quarantined instead of declaring success");UnitActionSchoolSetPostType.IgnoreWrite=false;
        Reset();var player=Battle();FeatureRuntime.Toggle("cooldown");object[] cd={new SkillDataAttack{skill=new SkillBase{unit=player}},10f};Hook("SkillCooldown",cd);Check((float)cd[1]==0,"player cooldown callback changes runtime calculation");cd=new object[]{new SkillDataAttack{skill=new SkillBase{unit=new UnitCtrlBase()}},10f};Hook("SkillCooldown",cd);Check((float)cd[1]==10,"NPC cooldown remains native");
        cd=new object[]{new SkillDataAttack{skill=new SkillBase{unit=player}},22f};Hook("SkillCooldown",cd);Check((float)cd[1]==0,"new skill instance picked up without retaining previous skill");
        var attack=new SkillAttack{unit=player};Check(!attack.IsCreate(),"native release substitute rejects existing cooldown");object[] scope={attack,null};Hook("AttackCheck",scope);Check(attack.IsCreate()&&attack.data.cd.baseValue==500,"actual release reads temporary zero runtime cooldown without base write");Hook("CooldownChecked",scope[1],null);Check(!attack.IsCreate()&&attack.data.cd.value==500&&attack.data.cd.allEqualCall.Count==0,"release finalizer removes only temporary cooldown modifier");
        var step=new StepBase{unit=player};scope=new object[]{step,null};Hook("StepCheck",scope);Check(step.IsCreate(),"native step release uses temporary dynamic cooldown");Hook("CooldownChecked",scope[1],new Exception("cast failure"));Check(step.data.cd.value==800,"exceptional release restores native cooldown field");
        FeatureRuntime.Toggle("mp");FeatureRuntime.Toggle("sp");FeatureRuntime.Toggle("hp");FeatureRuntime.Tick();Check(player.data.mp==20&&player.data.sp==30&&player.data.hp==40,"enabling resource locks never refills or changes current values");object[] mp={player.data,-10};Hook("KeepMP",mp);Check((int)mp[1]==0,"player negative resource cost prevented");mp=new object[]{new UnitDataBase{unit=new UnitCtrlBase()},-10};Hook("KeepMP",mp);Check((int)mp[1]==-10,"NPC resource cost remains native");
        FeatureRuntime.Toggle("steal");var theft=new UnitActionRoleStealItem{unit=g.world.playerUnit,isStealDiscover=true};Hook("StealResult",theft);Check(theft.isStealComplete&&!theft.isStealDiscover,"player final theft and concealment both succeed");theft=new UnitActionRoleStealItem{unit=Npc};Hook("StealResult",theft);Check(!theft.isStealComplete,"NPC final theft result remains native");
        FeatureRuntime.Toggle("escape");var escape=new UnitActionRoleEscape();object[] init={escape,g.world.playerUnit,false};Hook("EscapeInit",init);Check(escape.isSureComplete,"player native escape initializer option set");Hook("EscapeInitialized",escape,init[2]);Check(!escape.isSureComplete,"escape instance option restored after initialization");init=new object[]{escape,Npc,false};Hook("EscapeInit",init);Check(!escape.isSureComplete,"NPC escape initialization unchanged");
        FeatureRuntime.Toggle("study");var study=new UnitActionPropMartialStudy{unit=g.world.playerUnit};object[] learning={study,false};Hook("StudyStart",learning);Check(study.isOneStudy,"player native learning one-study flag set after preflight");Hook("StudyFinished",study,learning[1]);Check(!study.isOneStudy,"learning option restored after original native completion");study.unit=Npc;Hook("StudyStart",study,false);Check(!study.isOneStudy,"NPC learning unaffected");
        object[] cost={g.world.playerUnit.data.dynUnitData.energy,2};Hook("KeepWorldResource",cost);Check((int)cost[1]==15,"world player stamina loss locked at original value");FeatureRuntime.Toggle("hp");cost=new object[]{g.world.playerUnit.data.dynUnitData.energy,2};Hook("KeepWorldResource",cost);Check((int)cost[1]==2,"turning lock off permits normal stamina changes again");
        int released=HarmonyLib.Harmony.Released;g.world.isIntoWorld=false;FeatureRuntime.Tick();Check(!FeatureRuntime.Enabled("cooldown")&&HarmonyLib.Harmony.Released==released+1,"world exit clears flags and unpatches owned hooks");FeatureRuntime.Dispose();Check(HarmonyLib.Harmony.Released==released+1,"runtime release idempotent");
        Reset();r=FeatureOperations.Prepare("title","player","太虚真人");FeatureOperations.Execute(r);Check(FeatureOperations.PlayerTitle()=="太虚真人"&&CharacterSave.Pending>0,"custom player display title stored in isolated native extension data");var titleView=new UITaoistNameItem{unit=g.world.playerUnit};Hook("TitleDisplay",titleView);Check(titleView.textName.text=="太虚真人","native title widget uses player custom text");r=FeatureOperations.Prepare("title","player","");FeatureOperations.Execute(r);Check(FeatureOperations.PlayerTitle()=="","blank custom title restores original native display");Check(Reject(()=>FeatureOperations.Prepare("title","npc","冒充")),"NPC cannot receive player title");
        Reset();HarmonyLib.Harmony.Fail=true;Check(Reject(()=>FeatureRuntime.Toggle("mp"))&&!FeatureRuntime.Enabled("mp"),"partial patch attachment failure disables all effects");HarmonyLib.Harmony.Fail=false;
        Check(FeatureRuntime.Error!=null,"patch installation fault persists until world replacement");int attachedAfterFailure=HarmonyLib.Harmony.Attached;g.world.playerUnit.data.unitData.objData.SetString(FeatureOperations.TitleKey,"故障后显示名");FeatureRuntime.Tick();Check(HarmonyLib.Harmony.Attached==attachedAfterFailure&&FeatureRuntime.Error!=null,"saved custom title cannot trigger automatic patch retry after fault");

        Reset();var sceneGrid=new GridBase();SceneType.map.world.showGrids.Add("visible",sceneGrid);sceneGrid.gridUI.goMaskView.SetActive(false);
        FeatureRuntime.Toggle("vision");Check(!sceneGrid.gridUI.goMask.activeSelf&&!sceneGrid.gridUI.goMaskView.activeSelf,"vision immediately covers existing scene grids");
        Hook("GridReleased",sceneGrid.gridUI);Check(sceneGrid.gridUI.goMask.activeSelf&&!sceneGrid.gridUI.goMaskView.activeSelf,"grid refresh restores original layers before native rebuild");
        sceneGrid.gridUI.goMaskView.SetActive(true);Hook("GridUpdated",sceneGrid.gridUI);Check(!sceneGrid.gridUI.goMaskView.activeSelf,"grid refresh hides the newly rebuilt view layer");
        FeatureRuntime.Toggle("vision");Check(sceneGrid.gridUI.goMask.activeSelf&&sceneGrid.gridUI.goMaskView.activeSelf,"vision off restores scene layers");
        FeatureRuntime.Toggle("vision");var newGrid=new GridBaseUI();Hook("GridUpdated",newGrid);Check(!newGrid.goMask.activeSelf,"new scene grid inherits active vision");Hook("GridReleased",newGrid);Check(newGrid.goMask.activeSelf,"pooled or destroyed grid restores and releases ownership");
        var explored=new GridBaseUI{isOpen=true,isView=true};Hook("GridUpdated",explored);Check(explored.goMask.activeSelf&&explored.goMaskView.activeSelf,"explored visible tiles never request or change absent fog UI");
        FeatureRuntime.Dispose();Check(sceneGrid.gridUI.goMask.activeSelf,"world release restores scene fog");
        Reset();FeatureRuntime.Toggle("vision");var map=new UIMinMap();Hook("MapUpdated",map);Check(!map.imgMask.enabled&&SceneType.map.world.Refreshes==0,"vision hides native fog renderer without global UI refresh");Check(!new DataMap.Map().IsOpenGrid(new UnityEngine.Vector2Int(4,4)),"vision leaves persistent exploration query unchanged");FeatureRuntime.Toggle("vision");Check(map.imgMask.enabled,"vision off restores prior mask state");map.imgMask.enabled=false;FeatureRuntime.Toggle("vision");Hook("MapUpdated",map);FeatureRuntime.Dispose();Check(!map.imgMask.enabled,"release preserves preexisting hidden mask");
        Reset();FeatureRuntime.Toggle("move");map=new UIMinMap();var bgHit=new UnityEngine.GameObject();bgHit.transform.Parent=map.imgBG.transform;UnityEngine.EventSystems.EventSystem.current.Hit=bgHit;UnityEngine.Input.mousePosition=new UnityEngine.Vector3(0,0);UnityEngine.Input.Down=true;UnityEngine.Input.Up=false;Hook("MapInput",map);UnityEngine.Input.Down=false;UnityEngine.Input.Up=true;Hook("MapInput",map);Check(g.world.playerUnit.data.unitData.GetPoint()==new UnityEngine.Vector2Int(50,50)&&g.world.playerUnit.Creates==1,"map click normalized through native point movement");Check(SceneType.map.world.Stops==1&&SceneType.map.world.playerPoint==new UnityEngine.Vector2Int(50,50),"map movement stops old path and synchronizes native model/camera position");Hook("MapInput",map);Check(g.world.playerUnit.Creates==1,"mouse release handled once");
        UnityEngine.Input.Down=true;UnityEngine.Input.Up=false;UnityEngine.Input.mousePosition=new UnityEngine.Vector3(-20,-20);Hook("MapInput",map);UnityEngine.Input.Down=false;UnityEngine.Input.Up=true;UnityEngine.Input.mousePosition=new UnityEngine.Vector3(20,20);Hook("MapInput",map);Check(g.world.playerUnit.Creates==1,"map drag never teleports");
        UnityEngine.Input.Down=true;UnityEngine.Input.Up=false;Hook("MapInput",map);UnityEngine.Input.Down=false;UnityEngine.Input.Up=true;UnityEngine.EventSystems.EventSystem.current.Hit=new UnityEngine.GameObject();Hook("MapInput",map);Check(g.world.playerUnit.Creates==1,"overlay raycast blocks teleport");UnityEngine.EventSystems.EventSystem.current.Hit=new UnityEngine.GameObject();UnityEngine.EventSystems.EventSystem.current.Hit.transform.Parent=map.imgMask.transform;Check((bool)typeof(FeatureRuntime).GetMethod("IsMapHit",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{map}),"fog layer remains a valid map hit without vision");FeatureRuntime.Toggle("move");FeatureRuntime.MoveOnMap(new UnityEngine.Vector2Int(20,20));Check(g.world.playerUnit.Creates==1,"map move disabled restores original operation");
        Reset();FeatureRuntime.Toggle("relation");var dlcAction=new UnitActionFeedback104{unit=Npc,requestUnit=g.world.playerUnit};var dlcScope=new object[]{dlcAction,null};Hook("RelationStart",dlcScope);
        var packageArgs=new object[]{dlcAction,2};Check(!(bool)Hook("RelationPackageFeedback",packageArgs)&&(int)packageArgs[1]==0,"DLC relation skips package response before refusal drama side effects");
        var unrelated=new UnitActionFeedbackBase();Check((bool)Hook("RelationPackageFeedback",new object[]{unrelated,2}),"unrelated package invitation and request feedback stays native");
        Hook("RelationSpecialChecked",dlcAction,0);Hook("RelationScoreRead",dlcAction.unit.data.unitData.relationData,g.world.playerUnit);dlcAction.state=2;Hook("RelationDecision",dlcAction);Check(dlcAction.state==1,"DLC relation continues score flow to acceptance");Hook("RelationFinished",dlcScope[1],null);
        Check((bool)Hook("RelationPackageFeedback",new object[]{dlcAction,2}),"package response stays native outside relation scope");
        var adminPackage=new UnitActionFeedback104{unit=Npc,requestUnit=g.world.playerUnit,schoolAdmin=true};var adminScope=new object[]{adminPackage,null};Hook("RelationStart",adminScope);Check((bool)Hook("RelationPackageFeedback",new object[]{adminPackage,2}),"package school administration feedback remains native");Hook("RelationFinished",adminScope[1],null);
        var npcPackage=new UnitActionFeedback104{unit=Npc,requestUnit=Npc};var npcScope=new object[]{npcPackage,null};Hook("RelationStart",npcScope);Check((bool)Hook("RelationPackageFeedback",new object[]{npcPackage,2}),"NPC to NPC package relation remains native");Hook("RelationFinished",npcScope[1],null);

        Reset();FeatureRuntime.Toggle("relation");var accept=new UnitActionFeedback104{unit=Npc,requestUnit=g.world.playerUnit};Decide(accept);Check(accept.state==1,"player scoring rejection becomes acceptance before native feedback");accept=new UnitActionFeedback104{unit=Npc,requestUnit=Npc};Decide(accept);Check(accept.state==2,"NPC relation acceptance unchanged");accept=new UnitActionFeedback104{unit=Npc,requestUnit=g.world.playerUnit,schoolAdmin=true};Decide(accept);Check(accept.state==2,"school administrative feedback unchanged");accept=new UnitActionFeedback104{unit=Npc,requestUnit=g.world.playerUnit};Decide(accept,special:2);Check(accept.state==2,"special drama or identity refusal never overridden");accept=new UnitActionFeedback104{unit=Npc,requestUnit=g.world.playerUnit,state=2};Decide(accept);Check(accept.state==2,"pre-scoring monogamy conflict never overridden");accept=new UnitActionFeedback104{unit=Npc,requestUnit=g.world.playerUnit};Decide(accept,readScore:false);Check(accept.state==2,"failure that never reaches score phase retained");accept=new UnitActionFeedback104{unit=Npc,requestUnit=g.world.playerUnit,state=0};Hook("RelationDecision",accept);Check(accept.state==0,"uninitialized decision never forced complete");accept.state=2;Hook("RelationDecision",accept);Check(accept.state==2,"out-of-scope feedback remains native after finalizer");FeatureRuntime.Toggle("relation");accept.state=0;Decide(accept);Check(accept.state==2,"relation switch off restores native acceptance");

        Reset();school=new MapBuildSchool{name="native school"};school.buildData.id="school";g.world.build.Items.Add(school);Npc.data.school=school;Npc.data.unitData.schoolID="school";Npc.Refusal=7;r=FeatureOperations.Prepare("outer","npc");Check(Reject(()=>FeatureOperations.Execute(r))&&!FeatureOperations.Busy&&CharacterSave.Pending==0,"school rejection creates no write and no quarantine");Check(FeatureOperations.Availability("post","npc")==null&&FeatureOperations.Availability("mp","player")==null,"school refusal leaves post picker and assists available");Npc.Refusal=0;r=FeatureOperations.Prepare("post","npc",post:SchoolPostType.In);FeatureOperations.Execute(r);Check(school.buildData.Post==SchoolPostType.In,"different valid post succeeds after declined operation");
        Reset();FeatureRuntime.Toggle("mp");r=FeatureOperations.Prepare("drill","npc");g.world.playerUnit.ThrowAfterWrite=true;Check(Reject(()=>FeatureOperations.Execute(r))&&FeatureOperations.Busy,"real partial execution still quarantined");Check(FeatureOperations.Availability("mp","player")==null&&FeatureOperations.Availability("dead","npc")==null,"quarantine does not disable runtime switches or read-only list");FeatureRuntime.Toggle("mp");Check(!FeatureRuntime.Enabled("mp"),"resource locks can be switched off during write quarantine");
        Console.WriteLine("TOTAL: "+count+" passed; managed substitutes only");
    }catch(Exception ex){Console.WriteLine(ex);Environment.Exit(1);}}
}
