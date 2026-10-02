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
    public void UpdateAllShowProps() { }
    public class PropsData { public ConfItemPropsItem propsItem; public int propsCount; }
}
public class ConfItemPropsItem { public int isOverlay; }
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
