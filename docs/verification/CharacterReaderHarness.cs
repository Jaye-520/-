// 运行真实 CharacterReader，替代游戏依赖；不模拟 IL2CPP 属性计算。
using System;
using MOD_YtxSQ1.Services;

public sealed class DynInt
{
    public int Stored;
    public bool Throw;
    public float Scale = 1f;
    public int value { get { if (Throw) throw new InvalidOperationException("injected failure"); return Stored; } }
    public float valueFloat { get { return value * Scale; } }
}
public enum UnitSexType { None, Man, Woman }
public sealed class PropertyData { public DataUnit.LuckData[] bornLuck; public System.Collections.Generic.List<DataUnit.LuckData> addLuck; public string Name; public UnitSexType sex = UnitSexType.Woman; public int inTrait=1,outTrait1=2,outTrait2=3; public string GetName() { return Name; } }
public sealed class DataUnit { public sealed class LuckData { public int id; } }
public sealed class FateItem { public int type; public string name,tips; }
public static class ConfRoleCreateFeatureEx
{
    public static bool Throw;
    public static string NativeText;
    public static WorldUnitBase LastUnit;
    public static DataUnit.LuckData LastLuck;
    public static int BgType;
    public static string tips(FateItem item, WorldUnitBase unit, DataUnit.LuckData luck, int bgType)
    { LastUnit=unit;LastLuck=luck;BgType=bgType;if(Throw)throw new Exception("native tips failed");return NativeText; }
}
public sealed class FateConfig
{
    public readonly System.Collections.Generic.Dictionary<int,FateItem> Items = new System.Collections.Generic.Dictionary<int,FateItem>();
    public bool Throw;
    public FateItem GetItem(int id) { if(Throw)throw new Exception("fate config failed"); FateItem item; return Items.TryGetValue(id,out item)?item:null; }
}
public sealed class UnitData { public string unitID = "player"; public PropertyData propertyData = new PropertyData(); }
public sealed class DynamicData
{
    public DynInt gradeID = new DynInt { Stored = 103 };
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
public sealed class ConfRoleCreateHobbyItem { public string name; }
public sealed class ConfRoleCreateCharacterItem { public string sc5asd_sd34; }
public sealed class TraitConfig { public ConfRoleCreateCharacterItem GetItem(int id) { return new ConfRoleCreateCharacterItem { sc5asd_sd34="trait"+id }; } }
public sealed class ConfLocalText { public static readonly System.Collections.Generic.Dictionary<string,string> Texts=new System.Collections.Generic.Dictionary<string,string>(); public static string GetText(string key) { string text; if(Texts.TryGetValue(key,out text))return text; return key=="hobby1"?"玉器":key=="hobby2"?"书籍":key=="trait1"?"无私":key=="trait2"?"义气":key=="trait3"?"忠贞":key; } }
public sealed class PlayerData
{
    public UnitData unitData = new UnitData(); public DynamicData dynUnitData = new DynamicData();
    public bool ThrowHobbies;
    public ConfRoleCreateHobbyItem[] GetHobbyItems() { if(ThrowHobbies)throw new Exception("hobby read failed"); return new[]{new ConfRoleCreateHobbyItem {name="hobby1"},new ConfRoleCreateHobbyItem {name="hobby2"}}; }
}
public sealed class WorldUnitBase
{
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
public sealed class GradeItem { public int grade = 2, phase = 3; }
public sealed class GradeConfig
{
    public int RequestedId;
    public GradeItem GetItem(int id) { RequestedId = id; return new GradeItem(); }
    public string GetGradeName(int grade, int phase)
    {
        if (grade != 2 || phase != 3) throw new Exception("grade ID passed as realm");
        return "筑基后期";
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
public sealed class Config { public FateConfig roleCreateFeature = new FateConfig(); public GradeConfig roleGrade = new GradeConfig(); public ConfDramaNpc dramaNpc = new ConfDramaNpc(); public TraitConfig roleCreateCharacter = new TraitConfig(); public ConfLocalText localText = new ConfLocalText(); }
public static partial class g { public static bool isDestroy; public static World world = new World(); public static Config conf = new Config(); }

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
