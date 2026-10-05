// Managed control-flow substitutes matching the inspected local signatures.
// They do not model native relation events, reputation, surname propagation or save reload.
using System;
using System.Collections.Generic;
public enum UnitRelationType { None, Parent, Children, ChildrenPrivate, Brother, ParentBack, ChildrenBack, BrotherBack, Married, Lover, Master, Student }
public sealed partial class DataUnit
{
    public partial class RelationData
    {
        public UnhollowerBaseLib.Il2CppStringArray parent = new[] { "", "" };
        public List<string> children = new List<string>(), childrenPrivate = new List<string>(), brother = new List<string>(),
            parentBack = new List<string>(), childrenBack = new List<string>(), brotherBack = new List<string>(),
            lover = new List<string>(), master = new List<string>(), student = new List<string>();
        public string married;
        public Dictionary<string, string> notRelieveUnitsID = new Dictionary<string, string>();
        public List<string> ForType(int type)
        { return type == 2 ? children : type == 3 ? childrenPrivate : type == 4 ? brother : type == 5 ? parentBack : type == 6 ? childrenBack : type == 7 ? brotherBack : type == 9 ? lover : type == 10 ? master : student; }
    }
}
public class DataUnitDie
{
    public class DieData { public string unitID, Name; public UnitSexType sex; public PortraitModelData modelData; public DataUnit.RelationData relationData = new DataUnit.RelationData(); public string GetName() { return Name; } }
    public class UnitDie { public Dictionary<string, DieData> Entries = new Dictionary<string, DieData>(); public DieData GetUnitDie(string id) { DieData result; return Entries.TryGetValue(id, out result) ? result : null; } }
}
public sealed partial class DataMgr { public DataUnitDie.UnitDie unitDie = new DataUnitDie.UnitDie(); }
public sealed partial class Config { public RelationGameParameters gameParameter = new RelationGameParameters(); }
public sealed class RelationGameParameters { public string friendCloseRequire = "100", haterCloseRequire = "-100"; }
public abstract class UnitActionBase : IStubUnitAction { public abstract void Apply(WorldUnitBase unit); }
public sealed class UnitActionRelationSet : UnitActionBase
{
    public WorldUnitBase Target; public UnitRelationType Type;
    public UnitActionRelationSet(WorldUnitBase target, UnitRelationType type, int add) { Target = target; Type = type; }
    public override void Apply(WorldUnitBase unit)
    {
        var a = unit.data.unitData.relationData; var b = Target.data.unitData.relationData;
        string aid = unit.data.unitData.unitID, bid = Target.data.unitData.unitID;
        // ClearRelation's inspected native lambda clears both parent slots and all nine lists.
        foreach (var r in new[] { a, b })
        {
            string other = r == a ? bid : aid;
            for (int i = 0; i < 2; i++) if (r.parent[i] == other) r.parent[i] = "";
            r.children.Remove(other); r.childrenPrivate.Remove(other); r.brother.Remove(other);
        }
        for (int i = 5; i <= 11; i++) if (i != 8) { a.ForType(i).Remove(bid); b.ForType(i).Remove(aid); }
        if (a.married == bid) a.married = ""; if (b.married == aid) b.married = "";
        if (Type == UnitRelationType.None) return;
        if (Type == UnitRelationType.Married) { a.married = bid; b.married = aid; return; }
        int type = (int)Type; a.ForType(type).Add(bid); b.ForType(MOD_YtxSQ1.Services.CharacterRelations.Reverse(type)).Add(aid);
    }
}
public sealed class UnitActionRelationSetParent : UnitActionBase
{
    private readonly string man, woman;
    public UnitActionRelationSetParent(string manId, string womanId, int add) { man = manId; woman = womanId; }
    public override void Apply(WorldUnitBase unit)
    {
        unit.data.unitData.relationData.parent = new[] { man, woman };
        foreach (var id in new[] { man, woman }) if (!string.IsNullOrEmpty(id)) g.world.unit.GetUnit(id, true).data.unitData.relationData.children.Add(unit.data.unitData.unitID);
    }
}

public sealed class ConfDramaNpcItem { public string avatar="special"; public int modelID; }
public sealed partial class ConfDramaNpc { public ConfDramaNpcItem GetItem(int id) { return new ConfDramaNpcItem(); } public PortraitModelData GetPortraitModelDataInModel(int id) { return Portraits[id]; } }
