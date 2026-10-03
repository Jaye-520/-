// 原生接口替身，仅验证调度、差额、回退及上下文，不证明游戏事件行为。
using System;
using System.Collections.Generic;
using System.Linq;
public class DataWorld
{
    public class World
    {
        public PlayerLogData playerLog = new PlayerLogData();
        public class PlayerLogData
        {
            public Dictionary<int, GradeData> upGrade = new Dictionary<int, GradeData>();
            public class GradeData { public int quality, luck; }
        }
    }
}
public class DataProps
{
    public List<PropsData> allProps = new List<PropsData>();
    public List<PropsData> _allShowProps { get { return allProps.Where(p=>p.propsCount>0 && (p.propsItem==null || p.propsItem.hide!=1) && !p.Equipped).ToList(); } }
    public int AddCalls, DeleteCalls; public bool FailAdd, FailDelete;
    public List<PropsData> AddProps(int id,int count,string sole)
    {
        AddCalls++; if(FailAdd)throw new InvalidOperationException("add failed");
        var item=allProps.FirstOrDefault(p=>p.propsID==id && p.propsItem.isOverlay==1);
        if(item==null){item=new PropsData{propsID=id,soleID=sole??Guid.NewGuid().ToString(),propsItem=g.conf.itemProps.GetItem(id)};allProps.Add(item);}
        item.propsCount+=count;return new List<PropsData>{item};
    }
    public PropsData DelProps(string sole){DeleteCalls++;if(FailDelete)throw new InvalidOperationException("delete failed");var item=GetProps(sole);allProps.Remove(item);return item;}
    public bool IsHasProps(string sole){return GetProps(sole)!=null;}
    public PropsData GetProps(string sole){return allProps.FirstOrDefault(p=>p.soleID==sole);}
    public int GetPropsNum(int id){return allProps.Where(p=>p.propsID==id).Sum(p=>p.propsCount);}
    public void UpdateAllShowProps() { }
    public class PropsData { public ConfItemPropsItem propsItem; public int propsCount,propsID; public string soleID="item"; public bool Equipped; public UIIconTool.PropsInfoDataBase propsInfoBase { get { return new UIIconTool.PropsInfoDataBase { name=propsItem==null?"物品":propsItem.name }; } } }
}
public class ConfItemPropsItem { public int isOverlay,id,hide,level;public string name,desc; }
public class ItemPropsConfig { public List<ConfItemPropsItem> _allConfList=new List<ConfItemPropsItem>();public ConfItemPropsItem GetItem(int id){return _allConfList.FirstOrDefault(p=>p.id==id);} }
public class ConfItemPillItem { public int id,effectType=101,grade,operUse=1,battleUIHide;public Dictionary<string,int> Effects=new Dictionary<string,int>(); }
public class ItemPillConfig { public List<ConfItemPillItem> _allConfList=new List<ConfItemPillItem>(); public Dictionary<string,int> GetPillAddValue(WorldUnitBase unit,int id,bool ignore){return _allConfList.First(p=>p.id==id).Effects;} }
public static partial class UIIconTool { public class PropsInfoDataBase { public string name; } }
public class DataObjectData
{
    private Dictionary<string,string> data=new Dictionary<string,string>();
    public bool ContainsKey(string key){return data.ContainsKey(key);}
    public string GetString(string key){return data[key];}
    public void SetString(string key,string value){data[key]=value;}
    public void DelString(string key){data.Remove(key);}
}
public class FakeValues : List<DynInt>
{
    public T Cast<T>() where T : class { return new Il2CppSystem.Collections.Generic.IReadOnlyCollection<DynInt> { Count = Count } as T; }
}
namespace Il2CppSystem.Collections.Generic { public class IReadOnlyCollection<T> { public int Count; } }
namespace UnityEngine
{
    public struct Vector2Int
    {
        public int x,y; public Vector2Int(int a,int b){x=a;y=b;}
        public static bool operator ==(Vector2Int a,Vector2Int b){return a.x==b.x&&a.y==b.y;}
        public static bool operator !=(Vector2Int a,Vector2Int b){return !(a==b);}
        public override bool Equals(object o){return o is Vector2Int && this==(Vector2Int)o;}
        public override int GetHashCode(){return x^y;}
    }
}
public class UnitActionSetPoint { public UnityEngine.Vector2Int point;public UnitActionSetPoint(UnityEngine.Vector2Int value){point=value;} }
public static class SceneType { public static SceneMap map; }
public class SceneMap { public MapWorldMgr world = new MapWorldMgr(); }
public class MapWorldMgr
{
    public static bool isEnableMap = true;
    public int Refreshes, Commands;
    public UnityEngine.Vector2Int curSelectPoint;
    public void UpdateGridUI() { Refreshes++; }
    public void UpdateGridUICmd() { Commands++; }
}
public class UIMapMain { public UIMapMainGridUnit uiGridUnit = new UIMapMainGridUnit(); }
public class UIMapMainGridUnit { public int Refreshes; public void UpdateUI(UnityEngine.Vector2Int point) { Refreshes++; } }
public static class NativeCastStub { public static T TryCast<T>(this object value) where T:class { return value as T; } }
#if ACTIONS_ONLY
public class UIType { public static UIType MapMain = new UIType(); }
public class ActionsUI { public UIMapMain Map = new UIMapMain(); public object GetUI(UIType type) { return Map; } }
public static partial class g { public static ActionsUI ui = new ActionsUI(); }
namespace UnityEngine { public static class Debug { public static void LogWarning(string text) { } } }
#endif
