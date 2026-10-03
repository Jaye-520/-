// 原生替身用于托管分支和布局；配置来自本机官方只读JSON，不证明游戏工厂行为。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
public enum MartialType { SkillLeft, SkillRight, Step, Ability, Ultimate }
public class ReturnAction<T, A>
{
    public Func<A,T> Function;
    public static implicit operator ReturnAction<T,A>(Func<A,T> value) { return new ReturnAction<T,A>{Function=value}; }
}
public class MartialInfo { public string name,basType; public int level=6; }
public sealed partial class DataUnit { public class ActionMartialData { public DataProps.PropsData data;public int exp; } }
public sealed partial class UnitData
{
    public string skillLeft,skillRight,step,ultimate;
    public string[] abilitys=new string[0];
    public Dictionary<MartialType,List<DataUnit.ActionMartialData>> Learned=new Dictionary<MartialType,List<DataUnit.ActionMartialData>>();
    public List<DataUnit.ActionMartialData> GetActionMartial(MartialType type) { return Learned.ContainsKey(type)?Learned[type]:new List<DataUnit.ActionMartialData>(); }
    public DataUnit.ActionMartialData GetActionMartial(string id) { return Learned.Values.SelectMany(x=>x).FirstOrDefault(x=>x.data.soleID==id); }
}
public partial class DataProps
{
    public void AddProps(PropsData data) { AddCalls++;if(FailAdd)throw new InvalidOperationException("native add failed");allProps.Add(data);if(AfterAdd!=null)AfterAdd(); }
    public partial class PropsData
    {
        public static PropsData NewMartial(MartialType type,UnhollowerBaseLib.Il2CppStructArray<int> values)
        {
            var result=new PropsData{soleID=Guid.NewGuid().ToString(),propsType=PropsDataType.Martial,propsCount=1,
                propsItem=new ConfItemPropsItem{name="秘籍"},Martial=new MartialData{baseID=values[0],grade=values[1],martialInfo=new MartialInfo{name=MartialFixture.Name(values[0]),basType=MartialFixture.School(values[0])}}};
            if(type==MartialType.Ability)result.Martial=new PropsAbilityData{Owner=result,baseID=values[0],grade=values[1],martialInfo=result.Martial.martialInfo};
            if(RewardTool.WrongIdentity)result.Martial.baseID++;
            return result;
        }
    }
    public class PropsAbilityData : MartialData
    {
        public PropsData Owner;private int suit;
        public int suitID { get{return suit;}set{if(RewardTool.WrongSuit)return;suit=value;Owner.ExtraData="suit:"+value;martialInfo.name=GameTool.LS(g.conf.battleAbilitySuitBase.GetItem(value).suitName)+"·"+MartialFixture.Name(baseID);} }
    }
    public partial class MartialData { public int baseID,grade;public MartialInfo martialInfo; }
}
public class ConfBattleSkillAttackItem { public int id,type,gradeMin,gradeMax;public string name,basType,className; }
public class ConfBattleStepBaseItem : ConfBattleSkillAttackItem { }
public class ConfBattleAbilitySuitBaseItem {public int id,suitType;public string suitName,suitMember;}
public class ConfGameItemRewardAbilitySuitItem {public int id,rewardID,suitID,level,rate,weight;}
public class ConfGameItemRewardItem {public int id,rewardID,itemType,item;}
public class ConfBattleAbilityBaseItem { public int id,type,gradeMin,gradeMax;public string name,basType;public int[] className; }
public class MartialConfig<T> where T:class { public List<T> _allConfList=new List<T>();public T GetItem(int id){return _allConfList.First(x=>(int)typeof(T).GetField("id").GetValue(x)==id);} }
public sealed partial class Config
{
    public MartialConfig<ConfBattleSkillAttackItem> battleSkillAttack=new MartialConfig<ConfBattleSkillAttackItem>();
    public MartialConfig<ConfBattleStepBaseItem> battleStepBase=new MartialConfig<ConfBattleStepBaseItem>();
    public MartialConfig<ConfBattleAbilitySuitBaseItem> battleAbilitySuitBase=new MartialConfig<ConfBattleAbilitySuitBaseItem>();
    public MartialConfig<ConfGameItemRewardAbilitySuitItem> gameItemRewardAbilitySuit=new MartialConfig<ConfGameItemRewardAbilitySuitItem>();
    public MartialConfig<ConfGameItemRewardItem> gameItemReward=new MartialConfig<ConfGameItemRewardItem>();
    public MartialConfig<ConfBattleAbilityCombineItem> battleAbilityCombine=new MartialConfig<ConfBattleAbilityCombineItem>();
    public MartialConfig<ConfBattleAbilityBaseItem> battleAbilityBase=new MartialConfig<ConfBattleAbilityBaseItem>();
}
public class ConfBattleSkillCombineItem { public string type,baseID,level,className; }
public class ConfBattleStepCombineItem { public string type,level,className; }
public class ConfBattleAbilityCombineItem { public int id,rewardGradeID;public string type,level,className; }
public static class GameTool { public static string LS(string key){return ConfLocalText.GetText(key);} }
public static class RewardTool
{
    public static int Calls;public static bool WrongIdentity,WrongSuit;
    public static UnhollowerBaseLib.Il2CppStructArray<int> GetSkillReward(ConfBattleSkillCombineItem conf,int grade,object a,object b){Calls++;return new[]{int.Parse(conf.baseID),grade};}
    public static UnhollowerBaseLib.Il2CppStructArray<int> GetStepReward(ConfBattleStepCombineItem conf,int grade,ReturnAction<bool,ConfBattleStepBaseItem> filter,object unused)
    { Calls++;return new[]{g.conf.battleStepBase._allConfList.First(x=>filter.Function(x)).id,grade}; }
    public static UnhollowerBaseLib.Il2CppStructArray<int> GetAbilityReward(ConfBattleAbilityCombineItem conf,int grade,ReturnAction<bool,ConfBattleAbilityBaseItem> filter,object unused)
    { Calls++;return new[]{g.conf.battleAbilityBase._allConfList.First(x=>filter.Function(x)).id,grade}; }
}
public static class MartialFixture
{
    private static object[] Read(string folder,string file)
    {return new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Deserialize<object[]>(File.ReadAllText(Path.Combine(folder,file+".json")));}
    private static string S(Dictionary<string,object> row,string key){return Convert.ToString(row[key]);}
    private static int I(Dictionary<string,object> row,string key){return int.Parse(S(row,key));}
    public static void Load(string folder)
    {
        foreach(Dictionary<string,object> row in Read(folder,"LocalText"))ConfLocalText.Texts[S(row,"key")]=S(row,"ch");
        foreach(Dictionary<string,object> r in Read(folder,"BattleSkillAttack"))g.conf.battleSkillAttack._allConfList.Add(new ConfBattleSkillAttackItem{id=I(r,"id"),type=I(r,"type"),gradeMin=I(r,"gradeMin"),gradeMax=I(r,"gradeMax"),name=S(r,"name"),className=S(r,"className"),basType=S(r,"basType")});
        foreach(Dictionary<string,object> r in Read(folder,"BattleStepBase"))g.conf.battleStepBase._allConfList.Add(new ConfBattleStepBaseItem{id=I(r,"id"),type=I(r,"type"),gradeMin=I(r,"gradeMin"),gradeMax=I(r,"gradeMax"),name=S(r,"name"),className=S(r,"className"),basType=S(r,"basType")});
        foreach(Dictionary<string,object> r in Read(folder,"BattleAbilityBase"))g.conf.battleAbilityBase._allConfList.Add(new ConfBattleAbilityBaseItem{id=I(r,"id"),type=I(r,"type"),gradeMin=I(r,"gradeMin"),gradeMax=I(r,"gradeMax"),name=S(r,"name"),className=S(r,"className").Split('|').Select(int.Parse).ToArray(),basType=S(r,"basType")});
        foreach(Dictionary<string,object> r in Read(folder,"BattleAbilitySuitBase"))g.conf.battleAbilitySuitBase._allConfList.Add(new ConfBattleAbilitySuitBaseItem{id=I(r,"id"),suitType=I(r,"suitType"),suitName=S(r,"suitName"),suitMember=S(r,"suitMember")});
        foreach(Dictionary<string,object> r in Read(folder,"GameItemRewardAbilitySuit"))g.conf.gameItemRewardAbilitySuit._allConfList.Add(new ConfGameItemRewardAbilitySuitItem{id=I(r,"id"),rewardID=I(r,"rewardID"),suitID=I(r,"suitID"),level=I(r,"level"),rate=I(r,"rate"),weight=I(r,"weight")});
        foreach(Dictionary<string,object> r in Read(folder,"GameItemReward"))g.conf.gameItemReward._allConfList.Add(new ConfGameItemRewardItem{id=I(r,"id"),rewardID=I(r,"rewardID"),itemType=I(r,"itemType"),item=I(r,"item")});
        foreach(Dictionary<string,object> r in Read(folder,"BattleAbilityCombine"))g.conf.battleAbilityCombine._allConfList.Add(new ConfBattleAbilityCombineItem{id=I(r,"id"),rewardGradeID=I(r,"rewardGradeID")});
    }
    public static string Name(int id){var a=g.conf.battleSkillAttack._allConfList.FirstOrDefault(x=>x.id==id);if(a!=null)return GameTool.LS(a.name);var b=g.conf.battleStepBase._allConfList.FirstOrDefault(x=>x.id==id);if(b!=null)return GameTool.LS(b.name);return GameTool.LS(g.conf.battleAbilityBase.GetItem(id).name);}
    public static string School(int id){var a=g.conf.battleSkillAttack._allConfList.FirstOrDefault(x=>x.id==id);if(a!=null)return a.basType;var b=g.conf.battleStepBase._allConfList.FirstOrDefault(x=>x.id==id);if(b!=null)return b.basType;return g.conf.battleAbilityBase.GetItem(id).basType;}
}
