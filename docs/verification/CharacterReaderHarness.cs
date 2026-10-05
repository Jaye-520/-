// 运行真实 CharacterReader，替代游戏依赖；DynInt仅模拟检查所需的分段取整，不证明IL2CPP行为。
using System;
using MOD_YtxSQ1.Services;

public sealed class NativeList<T> : System.Collections.Generic.List<T> { public IntPtr Pointer {get{return new IntPtr(GetHashCode());}} }
public sealed class NativeDictionary<K,V> : System.Collections.Generic.Dictionary<K,V> { public IntPtr Pointer {get{return new IntPtr(GetHashCode());}} }
public sealed class DynInt
{
    public DynInt() : this(0) { }
    public DynInt(int stored) { Stored=stored; _allAddCall.Add(()=>Bonus); }
    public IntPtr Pointer {get{return new IntPtr(GetHashCode());}}
    public int Stored;
    public bool Throw;
    public Action AfterWrite;
    public float Scale = 1f;
    public int Bonus, mMinValue=0,mMaxValue=int.MaxValue, scaleGetValue=1, offestFixValue;
    public int minValue { get {return mMinValue;} set {mMinValue=value;} }
    public int maxValue { get {return mMaxValue;} set {mMaxValue=value;} }
    public bool isOverflow,isValViewOut;
    public float changeRate=100,effectiveRate=100;
    public bool lockValue,RejectWrite;
    public object onBaseValueSetCall=new object();
    public System.Collections.Generic.List<Func<int>> _allAddCall=new System.Collections.Generic.List<Func<int>>();
    public System.Collections.Generic.List<Func<float>> _allMulCall=new System.Collections.Generic.List<Func<float>>(),_allMulAddCall=new System.Collections.Generic.List<Func<float>>();
    public System.Collections.Generic.List<Func<int>> allAddCall {get{return _allAddCall;}}
    public System.Collections.Generic.List<Func<float>> allMulCall {get{return _allMulCall;}}
    public System.Collections.Generic.List<Func<float>> allMulAddCall {get{return _allMulAddCall;}}
    public System.Collections.Generic.List<object> allEqualCall=new System.Collections.Generic.List<object>();
    public int mBaseValue {get{return Stored;} set{Stored=value;}}
    public int baseValue { get { return Stored; } set { Stored=value; if(AfterWrite!=null)AfterWrite(); if(RejectWrite){RejectWrite=false;throw new Exception("injected setter failure");} } }
    public int value { get {
        if (Throw) throw new InvalidOperationException("injected failure");
        var result=Stored*scaleGetValue;
        foreach(var call in allAddCall)result+=(int)Math.Round((float)(call()*scaleGetValue)*changeRate*.01f);
        float factor=1; foreach(var call in allMulCall)factor+=call();
        if(Math.Abs(factor-1)>.000001f)result=(int)Math.Round((float)result*factor*changeRate*.01f);
        factor=1; foreach(var call in allMulAddCall)factor*=1+call();
        if(Math.Abs(factor-1)>.000001f)result=(int)Math.Round((float)result*factor*changeRate*.01f);
        return result+offestFixValue;
    } }
    public float valueFloat { get { return value * Scale; } }
}
public enum UnitSexType { None, Man, Woman }
public sealed class PropertyData { public IntPtr Pointer {get{return new IntPtr(GetHashCode());}} public WorldUnitBase Owner; public Action AfterGrade, AfterExp; public int exp; public void SetGradeID(int id){Owner.data.dynUnitData.gradeID.Stored=id;exp=g.conf.roleGrade.GetItem(id).exp;if(AfterGrade!=null)AfterGrade();} public int AddExp(int value){exp+=value;if(AfterExp!=null)AfterExp();return value;} public UnhollowerBaseLib.Il2CppReferenceArray<DataUnit.LuckData> bornLuck; public System.Collections.Generic.List<DataUnit.LuckData> addLuck=new System.Collections.Generic.List<DataUnit.LuckData>(); public UnhollowerBaseLib.Il2CppStructArray<int> hobby = new[]{201,202,203}; public UnhollowerBaseLib.Il2CppStringArray name=new[]{"",""}; public string Name { get { return GetName(); } set { name=new[]{"",value}; } } public UnitSexType sex = UnitSexType.Woman; public int inTrait=1,outTrait1=2,outTrait2=3; public string GetName() { return name[0]+name[1]; } }
public sealed partial class DataUnit { public class RelationData { public IntPtr Pointer {get{return new IntPtr(GetHashCode());}} public Action AfterSet; public bool IgnoreSet; public float intimToPlayerUnit; public System.Collections.Generic.Dictionary<string,float> intimToUnit=new System.Collections.Generic.Dictionary<string,float>(); public void SetIntim(string id,float value){if(IgnoreSet)return;if(id=="player")intimToPlayerUnit=value;else intimToUnit[id]=value;if(AfterSet!=null)AfterSet();} } public sealed class LuckData { public int id,duration; } }
public class ConfRoleCreateFeatureItem { public int id,type,conceal,group,lockLuck,globalMax,level; public string name,tips; }
public sealed class FateItem : ConfRoleCreateFeatureItem { }
public static class ConfRoleCreateFeatureEx
{
    public static bool Throw;
    public static string NativeText;
    public static WorldUnitBase LastUnit;
    public static DataUnit.LuckData LastLuck;
    public static int BgType;
    public static int duration(ConfRoleCreateFeatureItem item) { return -1; }
    public static string tips(ConfRoleCreateFeatureItem item, WorldUnitBase unit, DataUnit.LuckData luck, int bgType)
    { LastUnit=unit;LastLuck=luck;BgType=bgType;if(Throw)throw new Exception("native tips failed");return NativeText; }
}
public sealed class FateConfig
{
    public readonly System.Collections.Generic.Dictionary<int,FateItem> Items = new System.Collections.Generic.Dictionary<int,FateItem>();
    public bool Throw; public int ThrowId;
    public System.Collections.Generic.List<ConfRoleCreateFeatureItem> _allConfList { get { var list=new System.Collections.Generic.List<ConfRoleCreateFeatureItem>();foreach(var pair in Items){pair.Value.id=pair.Key;list.Add(pair.Value);}return list; } }
    public FateItem GetItem(int id) { if(Throw || ThrowId==id)throw new Exception("fate config failed"); FateItem item; return Items.TryGetValue(id,out item)?item:null; }
}
public sealed partial class UnitData { public IntPtr Pointer {get{return new IntPtr(GetHashCode());}} public DataObjectData objData=new DataObjectData(); public DataProps propData=new DataProps();public DataUnit.RelationData relationData=new DataUnit.RelationData();public NativeDictionary<int,DataWorld.World.PlayerLogData.GradeData> npcUpGrade=new NativeDictionary<int,DataWorld.World.PlayerLogData.GradeData>();public UnityEngine.Vector2Int Point; public UnityEngine.Vector2Int GetPoint(){return Point;} public string unitID = "player"; public PropertyData propertyData = new PropertyData(); }
public sealed class DynamicData
{ public IntPtr Pointer {get{return new IntPtr(GetHashCode());}} public DynInt abilityPoint=new DynInt(); public int GetCurUseAbilityPoint(){return 0;} public FakeValues GetValues(string key){var f=GetType().GetField(key);var r=new FakeValues();if(f!=null)r.Add((DynInt)f.GetValue(this));return r;}
    public DynInt gradeID = new DynInt { Stored = 103 };
    public int GetGrade(){return g.conf.roleGrade.GetItem(gradeID.value).grade;}
    public DynInt hp = new DynInt { Stored = 123 }, hpMax = new DynInt { Stored = 456 };
    public DynInt mp = new DynInt { Stored = 12 }, mpMax = new DynInt { Stored = 34 };
    public DynInt sp = new DynInt { Stored = 5 }, spMax = new DynInt { Stored = 6 };
    public DynInt attack = new DynInt { Stored = 77 }, defense = new DynInt { Stored = 88 }, footSpeed = new DynInt { Stored = 99 };
    public DynInt age = new DynInt { Stored = 192 }, life = new DynInt { Stored = 720 };
    public DynInt mood = new DynInt(), moodMax = new DynInt(), health = new DynInt(), healthMax = new DynInt();
    public DynInt energy = new DynInt(), energyMax = new DynInt(), luck = new DynInt(), talent = new DynInt();
    public DynInt beauty = new DynInt(), reputation = new DynInt(), standUp = new DynInt(), standDown = new DynInt();
    public DynInt phycicalFree = new DynInt(), magicFree = new DynInt(), crit = new DynInt(), guard = new DynInt();
    public DynInt moveSpeed = new DynInt { Stored = 370 };
    public DynInt critValue = new DynInt { Stored = 200, Scale = 10f }, guardValue = new DynInt();
    public DynInt basisBlade = new DynInt(), basisSpear = new DynInt(), basisSword = new DynInt { Stored = 46 };
    public DynInt basisFist = new DynInt(), basisPalm = new DynInt(), basisFinger = new DynInt();
    public DynInt basisFire = new DynInt(), basisFroze = new DynInt { Stored = 2 }, basisThunder = new DynInt();
    public DynInt basisWind = new DynInt(), basisEarth = new DynInt(), basisWood = new DynInt();
    public DynInt refineElixir = new DynInt(), refineWeapon = new DynInt(), geomancy = new DynInt();
    public DynInt symbol = new DynInt(), herbal = new DynInt(), mine = new DynInt { Stored = 18 };
}
public sealed class ConfRoleCreateHobbyItem { public int id; public string name,item="0"; }
public sealed class HobbyConfig { public readonly System.Collections.Generic.List<ConfRoleCreateHobbyItem> _allConfList = new System.Collections.Generic.List<ConfRoleCreateHobbyItem> { new ConfRoleCreateHobbyItem { id=201,name="hobby1" },new ConfRoleCreateHobbyItem { id=202,name="hobby2" },new ConfRoleCreateHobbyItem { id=203,name="陶器" },new ConfRoleCreateHobbyItem { id=204,name="图册" } }; public ConfRoleCreateHobbyItem GetItem(int id) { return _allConfList.Find(i=>i.id==id); } }
public sealed class ConfRoleCreateCharacterItem { public int id,type; public string sc5asd_sd34,xdash_54sd; }
public sealed class TraitConfig { public readonly System.Collections.Generic.List<ConfRoleCreateCharacterItem> _allConfList = new System.Collections.Generic.List<ConfRoleCreateCharacterItem> { new ConfRoleCreateCharacterItem {id=1,type=8,sc5asd_sd34="trait1"},new ConfRoleCreateCharacterItem {id=2,type=9,sc5asd_sd34="trait2"},new ConfRoleCreateCharacterItem {id=3,type=9,sc5asd_sd34="trait3"},new ConfRoleCreateCharacterItem {id=4,type=8,sc5asd_sd34="内在四"} }; public ConfRoleCreateCharacterItem GetItem(int id) { return _allConfList.Find(i=>i.id==id); } }
public sealed class ConfLocalText { public static readonly System.Collections.Generic.Dictionary<string,string> Texts=new System.Collections.Generic.Dictionary<string,string>(); public static string GetText(string key) { string text; if(Texts.TryGetValue(key,out text))return text; return key=="hobby1"?"玉器":key=="hobby2"?"书籍":key=="trait1"?"无私":key=="trait2"?"义气":key=="trait3"?"忠贞":key; } }
public sealed class PlayerData
{ public IntPtr Pointer {get{return new IntPtr(GetHashCode());}}
    public UnitData unitData = new UnitData(); public DynamicData dynUnitData = new DynamicData();
    public bool ThrowHobbies;
    public int WeightCalls; public Action AfterWeight; public int[] GetCharacterWeight() { WeightCalls++; if(AfterWeight!=null)AfterWeight(); return new int[0]; }
    public ConfRoleCreateHobbyItem[] GetHobbyItems() { if(ThrowHobbies)throw new Exception("hobby read failed"); return new[]{new ConfRoleCreateHobbyItem {name="hobby1"},new ConfRoleCreateHobbyItem {name="hobby2"}}; }
}
public interface IStubUnitAction { void Apply(WorldUnitBase unit); }
public sealed partial class WorldUnitBase
{
    public WorldUnitBase(){data.unitData.propertyData.Owner=this;} public int ConfUpdates; public Action AfterConf; public void UpdateConf(){ConfUpdates++;if(AfterConf!=null)AfterConf();} public Action AfterMove, AfterLuck, AfterAction, AfterSuppressedLuck; public bool SuppressLuck; public bool RejectAction, ThrowMoveAfterWrite;
    public int ActionCalls;
    public System.Collections.Generic.List<WorldUnitLuckBase> allLuck=new System.Collections.Generic.List<WorldUnitLuckBase>();
    public NativeList<WorldUnitHobbyBase> allHobby=new NativeList<WorldUnitHobbyBase>();
    public WorldUnitLuckBase GetLuck(int id) { return allLuck.Find(l=>l.luckData.id==id); }
    public WorldUnitLuckBase CreateLuck(DataUnit.LuckData record) { if(SuppressLuck){if(AfterSuppressedLuck!=null)AfterSuppressedLuck();return null;}var l=new WorldUnitLuckBase{luckData=record};allLuck.Add(l);if(AfterLuck!=null)AfterLuck();return l; }
    public int CreateAction(object action,bool tip) { ActionCalls++;if(RejectAction)return 3;var martialAction=action as IStubUnitAction;if(martialAction!=null)martialAction.Apply(this);var move=action as UnitActionSetPoint;if(move!=null){data.unitData.Point=move.point;if(AfterMove!=null)AfterMove();if(ThrowMoveAfterWrite)throw new InvalidOperationException("move callback failed after write");}var add=action as UnitActionLuckAdd;var del=action as UnitActionLuckDel;var grade=action as UnitActionRoleUpGrade;if(add!=null){var r=new DataUnit.LuckData{id=add.Id};data.unitData.propertyData.addLuck.Add(r);CreateLuck(r);}if(del!=null){allLuck.RemoveAll(l=>l.luckData.id==del.Id);data.unitData.propertyData.addLuck.RemoveAll(l=>l.id==del.Id);}if(grade!=null){data.dynUnitData.gradeID.Stored=grade.Id;data.dynUnitData.hpMax.Stored+=100;data.dynUnitData.attack.Stored+=10;}if(AfterAction!=null)AfterAction();return 0; }
    public PlayerData data = new PlayerData(); public bool isDie, isHidden;
    public IntPtr Pointer { get { return new IntPtr(GetHashCode()); } }
}
public sealed class WorldUnitMgr
{
    public System.Collections.Generic.List<WorldUnitBase> Npcs = new System.Collections.Generic.List<WorldUnitBase>();
    public System.Collections.Generic.List<WorldUnitBase> GetUnits(bool includeHidden) { return Npcs.FindAll(u => includeHidden || !u.isHidden); }
    public WorldUnitBase GetUnit(string id, bool hidden)
    {
        if (g.world.playerUnit.data.unitData.unitID == id) return g.world.playerUnit;
        return Npcs.Find(u => u.data.unitData.unitID == id && !u.isDie && (hidden || !u.isHidden));
    }
}
public sealed class World { public bool isDestroy; public bool isIntoWorld = true; public WorldUnitBase playerUnit = new WorldUnitBase(); public WorldUnitMgr unit = new WorldUnitMgr(); }
public class ConfRoleGradeItem { public int id=103, grade = 2, phase = 3,quality,growItemNum,materialsNum,lockFeature,exp,growAddLife,abilityPoint,atkUp,hpUp;public string qualityName; }
public sealed class GradeItem : ConfRoleGradeItem { }
public sealed class GradeConfig
{
    public readonly System.Collections.Generic.List<GradeItem> _allConfList = new System.Collections.Generic.List<GradeItem> { new GradeItem(), new GradeItem { id=104 } };
    public GradeItem GetGradeItem(int grade,int phase,int quality=0){foreach(var c in Overrides.Values)if(c.grade==grade&&c.phase==phase&&c.quality==quality)return c;return new GradeItem{id=grade*100+phase,grade=grade,phase=phase,quality=quality};} public GradeItem GetNextGradeItem(int id){var c=GetItem(id);return new GradeItem{grade=c.phase==3?c.grade+1:c.grade,phase=c.phase==3?1:c.phase+1,exp=c.exp+100};} public int RequestedId;
    public readonly System.Collections.Generic.Dictionary<int,GradeItem> Overrides=new System.Collections.Generic.Dictionary<int,GradeItem>();
    public GradeItem GetItem(int id) { RequestedId = id;GradeItem result; return Overrides.TryGetValue(id,out result)?result:new GradeItem(); }
    public string GetGradeName(int grade, int phase)
    {
        if (grade>10 || phase>3) throw new Exception("grade ID passed as realm");
        return grade==2&&phase==3?"筑基后期":"境界"+grade+"阶段"+phase;
    }
}
public sealed class PortraitModelData { public int body; }
public sealed partial class ConfDramaNpc
{
    public readonly System.Collections.Generic.Dictionary<string,int> Ids = new System.Collections.Generic.Dictionary<string,int>();
    public readonly System.Collections.Generic.Dictionary<int,PortraitModelData> Portraits = new System.Collections.Generic.Dictionary<int,PortraitModelData>();
    public int GetFiveFlowerDramaNpcID(WorldUnitBase unit) { int id; return Ids.TryGetValue(unit.data.unitData.unitID,out id) ? id : 0; }
    public PortraitModelData GetPortraitModelData(int id) { throw new NullReferenceException("independent avatar has no assembled model data"); }
}
public sealed partial class Config { public ItemPropsConfig itemProps=new ItemPropsConfig(); public ItemPillConfig itemPill=new ItemPillConfig(); public HobbyConfig roleCreateHobby = new HobbyConfig(); public FateConfig roleCreateFeature = new FateConfig(); public GradeConfig roleGrade = new GradeConfig(); public ConfDramaNpc dramaNpc = new ConfDramaNpc(); public TraitConfig roleCreateCharacter = new TraitConfig(); public ConfLocalText localText = new ConfLocalText(); }
public static partial class g { public static bool isDestroy; public static World world = new World(); public static Config conf = new Config(); public static DataMgr data=new DataMgr(); }

public sealed class DataMgr { public IntPtr Pointer { get { return new IntPtr(GetHashCode()); } } public DataWorld.World world=new DataWorld.World();public static bool isSaveData;public int Calls;public bool ThrowSave;public bool? ImmediateResult;public Il2CppSystem.Action<bool> Callback;public void SaveData(Il2CppSystem.Action<bool> callback){Calls++;Callback=callback;if(ImmediateResult.HasValue)callback.Invoke(ImmediateResult.Value);if(ThrowSave)throw new InvalidOperationException("save dispatch failed after registration");}public void Complete(bool success){var c=Callback;Callback=null;c.Invoke(success);} }
public sealed class WorldUnitLuckBase { public int NewCalls; public DataUnit.LuckData luckData;public void NewCreateLuck(){NewCalls++;} }
public sealed class WorldUnitHobbyBase { public IntPtr Pointer {get{return new IntPtr(GetHashCode());}} public Action AfterDestroy,AfterInit; public int InitCalls,DestroyCalls; public void Init(WorldUnitBase u,ConfRoleCreateHobbyItem c,string a){InitCalls++;if(AfterInit!=null)AfterInit();} public void Destroy(){DestroyCalls++;if(AfterDestroy!=null)AfterDestroy();} }
public static class WorldFactory { public static WorldUnitLuckBase CreateLuckInID(int id){return new WorldUnitLuckBase();}public static Func<WorldUnitHobbyBase> HobbyFactory;public static WorldUnitHobbyBase CreateUnitHobby(string type){return HobbyFactory==null?new WorldUnitHobbyBase():HobbyFactory();} }
public sealed class UnitActionLuckAdd { public int Id;public UnitActionLuckAdd(int id){Id=id;} }
public sealed class UnitActionLuckDel { public int Id;public UnitActionLuckDel(int id){Id=id;} }
public sealed class UnitActionRoleUpGrade { public static System.Collections.Generic.Dictionary<string,int> GetAddAttr(ConfRoleGradeItem row){return new System.Collections.Generic.Dictionary<string,int>{{"attack",row.atkUp},{"hpMax",row.hpUp}};} public int Id;public bool upgradePhaseForceSuccess;public UnitActionRoleUpGrade(int id,UnhollowerBaseLib.Il2CppStringArray a,UnhollowerBaseLib.Il2CppStringArray b){Id=id;} }
namespace UnhollowerBaseLib
{
    public class Il2CppReferenceArray<T> : System.Collections.Generic.IEnumerable<T> { private readonly T[] a;public Il2CppReferenceArray(T[] values){a=values;}public int Length{get{return a.Length;}}public T this[int i]{get{return a[i];}set{a[i]=value;}}public static implicit operator Il2CppReferenceArray<T>(T[] values){return new Il2CppReferenceArray<T>(values);}public System.Collections.Generic.IEnumerator<T> GetEnumerator(){return ((System.Collections.Generic.IEnumerable<T>)a).GetEnumerator();}System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator(){return a.GetEnumerator();} }
    public class Il2CppStructArray<T> { private readonly T[] a;public Il2CppStructArray(int length){a=new T[length];}public int Length{get{return a.Length;}}public T this[int i]{get{return a[i];}set{a[i]=value;}}public static implicit operator Il2CppStructArray<T>(T[] values){var r=new Il2CppStructArray<T>(values.Length);for(int i=0;i<values.Length;i++)r[i]=values[i];return r;} }
    public class Il2CppStringArray { private readonly string[] a;public Il2CppStringArray(int length){a=new string[length];}public Il2CppStringArray(string[] values){a=values;}public int Length{get{return a.Length;}}public string this[int i]{get{return a[i];}set{a[i]=value;}}public static implicit operator Il2CppStringArray(string[] values){return new Il2CppStringArray(values);} }
}
namespace Il2CppSystem { public class Action<T> { public System.Action<T> Callback;public Action(System.Action<T> value){Callback=value;}public void Invoke(T value){Callback(value);}public static implicit operator Action<T>(System.Action<T> value){return new Action<T>(value);} } }

internal static class CharacterReaderHarness
{
    private static int passes;
    private static void Check(bool result, string name)
    {
        if (!result) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name); passes++;
    }
    public static void Main()
    {
        CharacterSnapshot snapshot; string reason;
        g.world.playerUnit.data.unitData.propertyData.Name = "甲";
        Check(CharacterReader.TryRead(out snapshot, out reason) && snapshot.Name == "甲" &&
            snapshot.Realm == "筑基后期" && g.conf.roleGrade.RequestedId == 103 &&
            snapshot.Values[0] == "123 / 456" && snapshot.Values[3] == "77",
            "resolves config row before realm; reads resource pairs and dynamic stats");
        int count = 0;
        for (int i = 0; i < snapshot.Groups.Length; i++)
        {
            if (snapshot.Groups[i].Length != CharacterSnapshot.Labels[i].Length) throw new Exception("group label mismatch");
            count += snapshot.Groups[i].Length;
        }
        Check(count == 46 && snapshot.Groups[0][0] == "16 / 60" && snapshot.Groups[1][7] == "370" &&
            snapshot.Groups[1][8] == "200%" && snapshot.Groups[2][2] == "46" &&
            snapshot.Groups[3][1] == "2" && snapshot.Groups[4][5] == "18",
            "46 reference and supplemental fields grouped; months converted to years; aptitude categories mapped");
        Check(snapshot.Groups[0][9]=="女" && snapshot.Groups[0][10]=="玉器 · 书籍" && snapshot.Groups[0][11]=="甲" && snapshot.Groups[1][10]=="无私 · 义气 · 忠贞" && snapshot.Groups[1][11]==snapshot.Realm,"reference identity fields read actual unit and localized configuration");
        g.world.playerUnit.data.ThrowHobbies=true;
        Check(CharacterReader.TryRead(out snapshot,out reason) && snapshot.Groups[0][10]=="暂不可读取" && snapshot.Values[3]=="77","optional hobby failure preserves numeric stats");
        g.world.playerUnit.data.ThrowHobbies=false;
        var previous = snapshot;
        g.world = new World(); g.world.playerUnit.data.unitData.propertyData.Name = "乙";
        g.world.playerUnit.data.dynUnitData.attack.Stored = 222;
        Check(CharacterReader.TryRead(out snapshot, out reason) && snapshot.Name == "乙" &&
            snapshot.Values[3] == "222" && previous.Name == "甲", "switching world reads new player without changing old snapshot");
        g.world.playerUnit.data.dynUnitData.mp.Throw = true;
        Check(!CharacterReader.TryRead(out snapshot, out reason) && snapshot == null && CharacterReader.LastFailure != null,
            "mid-read failure discards partial snapshot");
        g.world.isIntoWorld = false;
        Check(!CharacterReader.TryRead(out snapshot, out reason) && snapshot == null && CharacterReader.LastFailure == null,
            "loading state clears stale error and returns no values");
        g.world = null;
        Check(!CharacterReader.TryRead(out snapshot, out reason) && snapshot == null, "no world is unavailable");
        g.world = new World(); g.world.playerUnit.data.dynUnitData.hp = null;
        Check(!CharacterReader.TryRead(out snapshot, out reason) && snapshot == null, "missing attribute cannot appear as zero");
        g.world = new World(); g.conf = null;
        Check(CharacterReader.TryRead(out snapshot, out reason) && snapshot.Realm == "境界待确认",
            "missing realm configuration retains independently available stats");
        g.world.isDestroy = true;
        Check(!CharacterReader.TryRead(out snapshot, out reason) && snapshot == null, "destroyed world is unavailable");
        Console.WriteLine("Passed " + passes + " checks.");
    }
}
