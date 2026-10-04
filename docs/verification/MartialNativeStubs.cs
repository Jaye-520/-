// 原生替身用于托管分支和布局；配置来自本机官方只读JSON，不证明游戏工厂行为。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
public enum MartialType { None, SkillLeft, SkillRight, Step, Ability, Ultimate }
public class ReturnAction<T, A>
{
    public Func<A,T> Function;
    public static implicit operator ReturnAction<T,A>(Func<A,T> value) { return new ReturnAction<T,A>{Function=value}; }
}
public class MartialInfo { public string name,basType;public DataProps.MartialData Owner;public int level {get{return Owner==null?6:g.conf.battleSkillLevel.GetLevel(Owner.grade,g.conf.battleSkillLevel.GetGradeMin(Owner.martialType,Owner.baseID),Owner.GetPrefixs().Count);}} }
public sealed partial class DataUnit { public class ActionMartialData { public IntPtr Pointer {get{return new IntPtr(GetHashCode());}} public bool ThrowData,ThrowExp;private DataProps.PropsData nativeData;private int nativeExp;public DataProps.PropsData data {get{if(ThrowData)throw new InvalidOperationException("native learned data getter failed");return nativeData;}set{nativeData=value;}}public int exp {get{if(ThrowExp)throw new InvalidOperationException("native learned exp getter failed");return nativeExp;}set{nativeExp=value;}} public List<DataProps.MartialData.Prefix> GetPrefixsUnlock(){return data.Martial.GetPrefixs().Take(g.conf.battleSkillMastery.Items.Count(item=>g.conf.battleSkillMastery.GetExp(item,data.Martial.grade)<=exp)-1).ToList();} } }
public sealed partial class UnitData
{
    public string skillLeft,skillRight,step,ultimate;
    public string[] abilitys=new string[0];
    public Dictionary<MartialType,List<DataUnit.ActionMartialData>> Learned=new Dictionary<MartialType,List<DataUnit.ActionMartialData>>();
    public List<DataUnit.ActionMartialData> GetActionMartial(MartialType type) { return Learned.ContainsKey(type)?Learned[type]:new List<DataUnit.ActionMartialData>(); }
    public DataUnit.ActionMartialData GetActionMartial(string id) { return Learned.Values.SelectMany(x=>x).FirstOrDefault(x=>x.data.soleID==id); }
    public bool IsEquipProps(string id){return false; } // 普通物品装备判断不包含功法槽位。}
    public Action AfterStudy, AfterExp; public bool FailStudyAfterWrite, FailExpAfterWrite;
    public DataUnit.ActionMartialData StudyActionMartialProp(DataProps.PropsData props)
    {var item=new DataUnit.ActionMartialData{data=props};if(!Learned.ContainsKey(props.Martial.martialType))Learned[props.Martial.martialType]=new List<DataUnit.ActionMartialData>();Learned[props.Martial.martialType].Add(item);propData.DelProps(props.soleID);if(AfterStudy!=null)AfterStudy();if(FailStudyAfterWrite)throw new InvalidOperationException("study callback failed");return item;}
    public void AddMartialExp(string id,int delta){var action=GetActionMartial(id);var items=g.conf.battleSkillMastery.GetItems(null,action.data.Martial);action.exp=Math.Min(g.conf.battleSkillMastery.GetExp(items.Last(),action.data.Martial.grade),action.exp+delta);if(AfterExp!=null)AfterExp();if(FailExpAfterWrite)throw new InvalidOperationException("exp callback failed");}
}
public partial class DataProps
{
    public void AddProps(PropsData data) { AddCalls++;if(FailAdd)throw new InvalidOperationException("native add failed");allProps.Add(data);if(AfterAdd!=null)AfterAdd(); }
    public partial class PropsData
    {
        public static PropsData NewMartial(MartialType type,UnhollowerBaseLib.Il2CppStructArray<int> values)
        {
            var result=new PropsData{soleID=Guid.NewGuid().ToString(),propsType=PropsDataType.Martial,propsCount=1,
                propsItem=new ConfItemPropsItem{name="秘籍"},Martial=new MartialData{martialType=type,baseID=values[0],grade=values[1],martialInfo=new MartialInfo{name=MartialFixture.Name(values[0]),basType=MartialFixture.School(values[0])}}};
            if(type==MartialType.Ability)result.Martial=new PropsAbilityData{Owner=result,martialType=type,baseID=values[0],grade=values[1],martialInfo=result.Martial.martialInfo};
            result.Martial.Record=result;
            result.Martial.martialInfo.Owner=result.Martial;
            var capacity=g.conf.battleSkillLevel.GetPrefixCount(6,result.Martial.grade,g.conf.battleSkillLevel.GetGradeMin(type,result.Martial.baseID));
            for(int i=0;i<capacity;i++)result.Martial.Prefixes.Add(new MartialData.Prefix{index=i,prefixLevel=2,prefixValueItem=g.conf.battleSkillPrefixValue.GetItem((int)type,values[0],i==capacity-1?3:i==capacity-2?2:1)});
            if(RewardTool.WrongIdentity)result.Martial.baseID++;
            return result;
        }
        public bool ThrowValues; public Action AfterSetValues; public int[] Raw;public bool FailSetValuesAfterWrite;
        public UnhollowerBaseLib.Il2CppStructArray<int> GetValues()
        {
            if(ThrowValues)throw new InvalidOperationException("native values getter failed");
            if(Raw!=null)return Raw;
            var count=Martial.Prefixes.Count;var raw=new int[7+count*2];raw[0]=propsID;raw[1]=propsCount;raw[2]=(int)propsType;raw[3]=(int)Martial.martialType;raw[4]=Martial.baseID;raw[5]=Martial.grade;raw[6+count]=-1;
            for(int i=0;i<count;i++){raw[6+i]=Martial.Prefixes[i].prefixValueItem.number;raw[7+count+i]=Martial.Prefixes[i].prefixLevel;}Raw=raw;return raw;
        }
        public void SetValues(UnhollowerBaseLib.Il2CppStructArray<int> values)
        {
            Raw=new int[values.Length];for(int i=0;i<values.Length;i++)Raw[i]=values[i];ExtraData=string.Join(",",Raw);
            if(Raw.Length==6)Martial.Prefixes=new List<MartialData.Prefix>();
            if(Raw.Length>=7){int split=6;while(split<Raw.Length && Raw[split]!=-1)split++;int count=split-6;
                if(split<Raw.Length && split+1+count<=Raw.Length){Martial.Prefixes=new List<MartialData.Prefix>();for(int i=0;i<count;i++)Martial.Prefixes.Add(new MartialData.Prefix{index=i,prefixValueItem=g.conf.battleSkillPrefixValue.GetItem((int)Martial.martialType,Martial.baseID,Raw[6+i]),prefixLevel=Raw[split+1+i]});}}
            if(AfterSetValues!=null)AfterSetValues();
            if(FailSetValuesAfterWrite)throw new InvalidOperationException("set values callback failed");
        }
        public PropsData Clone(){var clone=NewMartial(Martial.martialType,new[]{Martial.baseID,Martial.grade});clone.soleID=soleID;clone.propsID=propsID;clone.propsCount=propsCount;clone.SetValues(GetValues());if(Martial is PropsAbilityData)clone.To<PropsAbilityData>().suitID=To<PropsAbilityData>().suitID;clone.ExtraData=ExtraData;return clone;}
    }
    public class PropsAbilityData : MartialData
    {
        public PropsData Owner;private int suit;
        public int suitID { get{return suit;}set{if(RewardTool.WrongSuit)return;suit=value;Owner.ExtraData="suit:"+value;martialInfo.name=GameTool.LS(g.conf.battleAbilitySuitBase.GetItem(value).suitName)+"·"+MartialFixture.Name(baseID);} }
    }
    public partial class MartialData
    {
        public int baseID,grade;public MartialType martialType;public MartialInfo martialInfo;public PropsData Record;
        public List<Prefix> Prefixes=new List<Prefix>();public bool FailPrefixAfterWrite;
        public class Prefix {public int index,prefixLevel;public ConfBattleSkillPrefixValueItem prefixValueItem;}
        public List<Prefix> GetPrefixs(){return Prefixes;}
        public void SetPrefixID(int index,int number){Prefixes.First(p=>p.index==index).prefixValueItem=g.conf.battleSkillPrefixValue.GetItem((int)martialType,baseID,number);Changed();}
        public void SetPrefixLevel(int index,int value){Prefixes.First(p=>p.index==index).prefixLevel=value;Changed();if(FailPrefixAfterWrite)throw new InvalidOperationException("prefix callback failed");}
        private void Changed(){Record.Raw=null;Record.ExtraData=string.Join(";",Prefixes.Select(p=>p.prefixValueItem.number+":"+p.prefixLevel));}
    }
}
public class DataStruct<T,A>{public T t1;public A t2;}
public class ConfBattleSkillMasteryItem {public int id;public int[] Grades;}
public class ConfBattleSkillMastery
{
    public List<ConfBattleSkillMasteryItem> Items=new List<ConfBattleSkillMasteryItem>();
    public List<ConfBattleSkillMasteryItem> GetItems(WorldUnitBase unit,DataProps.MartialData martial){return Enumerable.Range(0,martial.GetPrefixs().Count+1).Select(i=>Items[Math.Min(i,Items.Count-1)]).ToList();}
    public int GetExp(ConfBattleSkillMasteryItem item,int grade){return grade<1||grade>10?0:item.Grades[grade-1];}
}
public class ConfBattleSkillLevel
{
    public int[][] Counts;
    public int GetGradeMin(MartialType type,int id){if(type==MartialType.Ability)return g.conf.battleAbilityBase.GetItem(id).gradeMin;if(type==MartialType.Step)return g.conf.battleStepBase.GetItem(id).gradeMin;return g.conf.battleSkillAttack.GetItem(id).gradeMin;}
    public int GetPrefixCount(int level,int grade,int minimum){return Counts[level-1][Math.Max(0,Math.Min(4,grade-minimum))];}
    public int GetLevel(int grade,int minimum,int count){for(int level=6;level>=1;level--)if(count>=GetPrefixCount(level,grade,minimum))return level;return 1;}
}
public class ConfBattleSkillPrefixValueItem {public int id,skillType=1;public int[] skillID=new[]{0};public int number,multi=1,weight1=30,weight2=30,weight3=30,weight4=30,weight5=30,weight6=30,weight7=30,weight8=30,weight9=30,weight10=30;public string require="0",desc;}
public class SkillPrefixData {public int index,prefixLevel,level=3;public string prefixValueItemDesc;public ConfBattleSkillPrefixValueItem prefixValueItem;}
public class ConfBattleSkillPrefixValue
{
    public List<ConfBattleSkillPrefixValueItem> Items=new List<ConfBattleSkillPrefixValueItem>{new ConfBattleSkillPrefixValueItem{id=1,number=1,desc="伤害提升",multi=11},new ConfBattleSkillPrefixValueItem{id=1,number=2,desc="冷却降低"},new ConfBattleSkillPrefixValueItem{id=1,number=3,desc="射程提升"},new ConfBattleSkillPrefixValueItem{id=1,number=4,desc="互斥词条",require="-1"},new ConfBattleSkillPrefixValueItem{id=1,number=5,desc="依赖词条",require="1"},new ConfBattleSkillPrefixValueItem{id=1,number=6,desc="连击提升"},new ConfBattleSkillPrefixValueItem{id=1,number=7,desc="红色效果"}};
    public List<ConfBattleSkillPrefixValueItem> _allConfList=new List<ConfBattleSkillPrefixValueItem>();
    public List<ConfBattleSkillPrefixValueItem> GetItems(int type,int id){return Items;}
    public ConfBattleSkillPrefixValueItem GetItem(int type,int id,int number){return Items.First(p=>p.number==number);}
    public int GetPrefixMaxLevel(DataProps.MartialData martial,ConfBattleSkillPrefixValueItem item){return Math.Min(martial.martialInfo.level,item.number==7?6:item.number==6?3:5);}
    public int PrefixLevelToLevel(int raw,BattleSkillValueData data,ConfBattleSkillPrefixValueItem item){return Math.Max(1,Math.Min(6,(raw-1)/100+1));}
    public void SetPrefixsID(DataProps.MartialData martial,UnhollowerBaseLib.Il2CppStructArray<int> ids){for(int i=0;i<Math.Min(martial.GetPrefixs().Count,ids.Length);i++)martial.SetPrefixID(i,ids[i]);}
    public List<SkillPrefixData> GetPrefix(DataProps.MartialData martial){return martial.GetPrefixs().Select(p=>new SkillPrefixData{index=p.index,prefixLevel=p.prefixLevel,level=PrefixLevelToLevel(p.prefixLevel,null,p.prefixValueItem),prefixValueItem=p.prefixValueItem,prefixValueItemDesc=p.prefixValueItem.desc+" "+p.prefixLevel}).ToList();}
}
public class ConfBattleSkillValue {public int EmptyNumber;public int[] GetMinMaxLevel(ConfBattleSkillPrefixValueItem item){return item.number==EmptyNumber?new int[0]:new[]{1,item.number==7?6:5};}}
public class UnitActionPropMartialStudy {public static bool Refuse;public static DataStruct<string,int> GetStudyTip(WorldUnitBase unit,DataProps.MartialData martial,bool grade,bool prop,bool bas){return new DataStruct<string,int>{t1=Refuse?"当前条件不允许学习":""};}}
public class UnitActionMartialEquip : IStubUnitAction
{
    public DataUnit.ActionMartialData Data;public int Index;public UnitActionMartialEquip(DataUnit.ActionMartialData data,int index){Data=data;Index=index;}
    public void Apply(WorldUnitBase unit){var d=unit.data.unitData;switch(Data.data.Martial.martialType){case MartialType.SkillLeft:d.skillLeft=Data.data.soleID;break;case MartialType.SkillRight:d.skillRight=Data.data.soleID;break;case MartialType.Step:d.step=Data.data.soleID;break;case MartialType.Ultimate:d.ultimate=Data.data.soleID;break;case MartialType.Ability:d.abilitys[Index]=Data.data.soleID;break;}}
}
public class UnitActionMartialUnequip : IStubUnitAction
{
    public DataUnit.ActionMartialData Data;public UnitActionMartialUnequip(DataUnit.ActionMartialData data){Data=data;}
    public void Apply(WorldUnitBase unit){var d=unit.data.unitData;var id=Data.data.soleID;if(d.skillLeft==id)d.skillLeft="";if(d.skillRight==id)d.skillRight="";if(d.step==id)d.step="";if(d.ultimate==id)d.ultimate="";for(int i=0;i<d.abilitys.Length;i++)if(d.abilitys[i]==id)d.abilitys[i]="";}
}
public class WorldUnitEquipMartial { }
public sealed partial class WorldUnitBase
{
    public int IsCreateAction(object action,bool tip){return RejectAction?3:0;}
    public int EquipRebuilds;
    public Action AfterDestroyEquip; public void DestroyEquip(string id){EquipRebuilds++;if(AfterDestroyEquip!=null)AfterDestroyEquip();}
    public T CreateEquip<T>(string id) where T:new(){EquipRebuilds++;return new T();}
}
public class ConfBattleSkillAttackItem { public int id,type,gradeMin,gradeMax;public string name,basType,className; }
public class ConfBattleStepBaseItem : ConfBattleSkillAttackItem { }
public class ConfBattleAbilitySuitBaseItem {public int id,suitType;public string suitName,suitMember;}
public class ConfBattleSkillPrefixNameItem {public int id,index;public string text;}
public class ConfGameItemRewardAbilitySuitItem {public int id,rewardID,suitID,level,rate,weight;}
public class ConfGameItemRewardItem {public int id,rewardID,itemType,item;}
public class ConfBattleAbilityBaseItem { public int id,type,gradeMin,gradeMax;public string name,basType;public int[] className; }
public class MartialConfig<T> where T:class { public List<T> _allConfList=new List<T>();public T GetItem(int id){return _allConfList.First(x=>(int)typeof(T).GetField("id").GetValue(x)==id);} }
public sealed partial class Config
{
    public ConfBattleSkillMastery battleSkillMastery=new ConfBattleSkillMastery();
    public ConfBattleSkillLevel battleSkillLevel=new ConfBattleSkillLevel();
    public ConfBattleSkillPrefixValue battleSkillPrefixValue=new ConfBattleSkillPrefixValue();
    public ConfBattleSkillValue battleSkillValue=new ConfBattleSkillValue();
    public MartialConfig<ConfBattleSkillPrefixNameItem> battleSkillPrefixName=new MartialConfig<ConfBattleSkillPrefixNameItem>();
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
        g.conf.battleSkillLevel.Counts=Read(folder,"BattleSkillLevel").Cast<Dictionary<string,object>>().Select(r=>new[]{I(r,"grade"),I(r,"gradeA"),I(r,"gradeB"),I(r,"gradeC"),I(r,"gradeD")}).ToArray();
        foreach(Dictionary<string,object> r in Read(folder,"BattleSkillMastery"))g.conf.battleSkillMastery.Items.Add(new ConfBattleSkillMasteryItem{id=I(r,"id"),Grades=Enumerable.Range(1,10).Select(i=>I(r,"grade"+i)).ToArray()});
        foreach(var item in g.conf.battleSkillPrefixValue.Items)item.id=item.number;
        foreach(Dictionary<string,object> r in Read(folder,"BattleSkillPrefixValue"))g.conf.battleSkillPrefixValue._allConfList.Add(new ConfBattleSkillPrefixValueItem{id=I(r,"id"),skillType=I(r,"skillType"),skillID=S(r,"skillID").Split('|').Select(int.Parse).ToArray(),number=I(r,"number"),desc=S(r,"desc")});
        foreach(Dictionary<string,object> r in Read(folder,"BattleSkillPrefixName"))g.conf.battleSkillPrefixName._allConfList.Add(new ConfBattleSkillPrefixNameItem{id=I(r,"id"),index=I(r,"index"),text=S(r,"text")});
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
