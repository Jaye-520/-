using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MOD_YtxSQ1.Services
{
    internal sealed class RelationPerson
    {
        internal string Id, Name, Error;
        internal bool Dead, Valid;
        internal UnitSexType Sex;
        internal int DramaId;
    }
    internal sealed class RelationLink
    {
        internal string From, To, Label;
        internal int Type, Category;
        internal float Affinity;
    }
    internal sealed class RelationGraph
    {
        internal readonly Dictionary<string, RelationPerson> People = new Dictionary<string, RelationPerson>(StringComparer.Ordinal);
        internal readonly List<RelationLink> Links = new List<RelationLink>();
        internal readonly List<string> Errors = new List<string>();
        internal string Fingerprint;
    }
    internal sealed class RelationPair
    {
        internal string Context, A, B, NameA, NameB, StateA, StateB;
        internal IntPtr UnitA, UnitB, DataA, DataB, InfoA, InfoB, RelationA, RelationB;
        internal float AB, BA;
        internal readonly List<int> Types = new List<int>();
    }

    // Read original containers, not GetRelation(type, true), which prunes deceased entries.
    internal static class CharacterRelations
    {
        internal static readonly string[] Filters = { "全部", "亲族", "伴侣", "师徒", "交好", "仇怨" };
        internal static readonly int[] EditableTypes = { 8, 9, 10, 11, 5, 6, 7, 1, 2 };
        internal static string TypeName(int type)
        {
            switch (type)
            {
                case 1: return "父母"; case 2: return "子女"; case 3: return "私生子女";
                case 4: return "兄弟姐妹"; case 5: return "义父母"; case 6: return "义子女";
                case 7: return "义兄弟姐妹"; case 8: return "夫妻"; case 9: return "道侣";
                case 10: return "师父"; case 11: return "徒弟"; case 100: return "好友";
                case 101: return "仇人"; default: return "无身份关系";
            }
        }
        internal static int Category(int type)
        { return type <= 7 ? 1 : type <= 9 ? 2 : type <= 11 ? 3 : type == 100 ? 4 : 5; }
        internal static int Reverse(int type)
        { return type == 1 ? 2 : type == 2 || type == 3 ? 1 : type == 5 ? 6 : type == 6 ? 5 : type == 10 ? 11 : type == 11 ? 10 : type; }
        internal static string Number(float value) { return value.ToString("R", CultureInfo.InvariantCulture); }
        internal static float Parse(string text)
        {
            float value;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !EditableAffinity(value))
                throw new InvalidOperationException("好感须大于-2147483648、小于2147483648，可输入小数");
            return value;
        }
        internal static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        internal static bool EditableAffinity(float value) { return Finite(value) && value > -2147483648f && value < 2147483648f; }
        internal static WorldUnitBase ResolveIncludingDead(string id)
        { return g.world == null || g.world.unit == null ? null : g.world.unit.GetUnit(id, true); }
        internal static RelationPerson Person(string id)
        {
            var result = new RelationPerson { Id = id, Name = "人物失效", Error = "未找到人物记录" };
            try
            {
                var unit = ResolveIncludingDead(id);
                if (unit != null && unit.data != null && unit.data.unitData != null && unit.data.unitData.propertyData != null)
                {
                    result.Name = unit.data.unitData.propertyData.GetName() ?? "无名修士";
                    result.Sex = unit.data.unitData.propertyData.sex; result.Dead = unit.isDie;
                    result.DramaId = g.conf == null || g.conf.dramaNpc == null ? 0 : g.conf.dramaNpc.GetFiveFlowerDramaNpcID(unit);
                    result.Valid = !unit.isDie; result.Error = null; return result;
                }
                var dead = g.data == null || g.data.unitDie == null ? null : g.data.unitDie.GetUnitDie(id);
                if (dead != null) { result.Name = dead.GetName() ?? "无名修士"; result.Sex = dead.sex; result.Dead = true; result.Error = null; }
            }
            catch (Exception ex) { result.Error = "人物读取失败：" + ex.Message; }
            return result;
        }
        private static DataUnit.RelationData Data(string id)
        {
            var unit = ResolveIncludingDead(id);
            if (unit != null && unit.data != null && unit.data.unitData != null) return unit.data.unitData.relationData;
            var dead = g.data == null || g.data.unitDie == null ? null : g.data.unitDie.GetUnitDie(id);
            return dead == null ? null : dead.relationData;
        }
        internal static float Raw(DataUnit.RelationData data, string other)
        {
            var player = CharacterReader.Resolve(null);
            if (player == null) throw new InvalidOperationException("玩家尚未就绪");
            if (other == player.data.unitData.unitID) return data.intimToPlayerUnit;
            return data.intimToUnit != null && data.intimToUnit.ContainsKey(other) ? data.intimToUnit[other] : 0f;
        }
        internal static List<RelationLink> Direct(string id)
        {
            string error; var result = Direct(id, out error);
            if (error != null) throw new InvalidOperationException(error);
            return result;
        }
        private static List<RelationLink> Direct(string id, out string error)
        {
            error = null;
            var data = Data(id);
            if (data == null) throw new InvalidOperationException("关系数据未读取");
            var result = new List<RelationLink>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            Action<string, int> add = (other, type) =>
            {
                if (string.IsNullOrEmpty(other) || other == id || !seen.Add(other + ":" + type)) return;
                result.Add(new RelationLink { From = id, To = other, Type = type, Category = Category(type), Label = TypeName(type), Affinity = Raw(data, other) });
            };
            if (data.parent != null) for (int i = 0; i < data.parent.Length; i++) add(data.parent[i], 1);
            var lists = new[] { data.children, data.childrenPrivate, data.brother, data.parentBack, data.childrenBack, data.brotherBack, data.lover, data.master, data.student };
            var types = new[] { 2, 3, 4, 5, 6, 7, 9, 10, 11 };
            for (int i = 0; i < lists.Length; i++) if (lists[i] != null) foreach (var other in lists[i]) add(other, types[i]);
            add(data.married, 8);
            int friend, hate;
            if (g.conf == null || g.conf.gameParameter == null ||
                !int.TryParse(g.conf.gameParameter.friendCloseRequire, out friend) || !int.TryParse(g.conf.gameParameter.haterCloseRequire, out hate))
            { error = "交好／仇怨阈值未读取，身份关系已保留"; return result; }
            if (data.intimToUnit != null) foreach (var entry in data.intimToUnit)
            { if (entry.Value >= friend) add(entry.Key, 100); else if (entry.Value <= hate) add(entry.Key, 101); }
            var playerId = CharacterReader.Resolve(null).data.unitData.unitID;
            if (id != playerId) { if (data.intimToPlayerUnit >= friend) add(playerId, 100); else if (data.intimToPlayerUnit <= hate) add(playerId, 101); }
            result.Sort((a, b) => { int c = a.Type.CompareTo(b.Type); return c != 0 ? c : StringComparer.Ordinal.Compare(a.To, b.To); });
            return result;
        }
        internal static RelationGraph ReadGraph(string center, HashSet<string> expanded, int filter)
        {
            var graph = new RelationGraph(); var queue = new Queue<string>(); var read = new HashSet<string>(StringComparer.Ordinal);
            graph.People[center] = Person(center); queue.Enqueue(center);
            while (queue.Count > 0)
            {
                var source = queue.Dequeue(); if (!read.Add(source)) continue;
                try
                {
                    string error; var direct = Direct(source, out error);
                    if (error != null) graph.Errors.Add(graph.People[source].Name + "：" + error);
                    foreach (var link in direct)
                    {
                        if (filter != 0 && link.Category != filter) continue;
                        graph.Links.Add(link);
                        if (!graph.People.ContainsKey(link.To)) graph.People.Add(link.To, Person(link.To));
                        if (expanded.Contains(link.To) && !read.Contains(link.To)) queue.Enqueue(link.To);
                    }
                }
                catch (Exception ex) { graph.Errors.Add(graph.People[source].Name + "：" + ex.Message); }
            }
            var signature = new StringBuilder();
            foreach (var person in graph.People.Values) signature.Append(person.Id).Append('|').Append(person.Name).Append('|').Append(person.Sex).Append('|').Append(person.DramaId).Append('|').Append(person.Dead).Append('|').Append(person.Error).Append(';');
            foreach (var link in graph.Links) signature.Append(link.From).Append('|').Append(link.To).Append('|').Append(link.Type).Append('|').Append(Number(link.Affinity)).Append(';');
            foreach (var error in graph.Errors) signature.Append(error);
            graph.Fingerprint = signature.ToString(); return graph;
        }
        internal static string State(DataUnit.RelationData data)
        {
            var sb = new StringBuilder();
            if (data.parent != null) for (int i = 0; i < data.parent.Length; i++) sb.Append(data.parent[i]).Append(';');
            sb.Append('/');
            foreach (var list in new[] { data.children, data.childrenPrivate, data.brother, data.parentBack, data.childrenBack, data.brotherBack, data.lover, data.master, data.student })
            { if (list != null) foreach (var id in list) sb.Append(id).Append(';'); sb.Append('/'); }
            sb.Append(data.married).Append('/');
            if (data.notRelieveUnitsID != null)
            {
                var locks = new List<string>();
                foreach (var entry in data.notRelieveUnitsID) locks.Add(entry.Key + ":" + entry.Value);
                locks.Sort(StringComparer.Ordinal); foreach (var entry in locks) sb.Append(entry).Append(';');
            }
            return sb.ToString();
        }
        internal static RelationPair Pair(string context, string a, string b)
        {
            if (string.IsNullOrEmpty(context) || CharacterReader.ContextKey() != context) throw new InvalidOperationException("存档已变化");
            var ua = CharacterReader.Resolve(a); var ub = CharacterReader.Resolve(b);
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || ua == null || ub == null || a == b || ua.Pointer == ub.Pointer ||
                ua.data.unitData.unitID != a || ub.data.unitData.unitID != b) throw new InvalidOperationException("请选择两名不同且存活的人物");
            var da = ua.data; var db = ub.data; var ia = da.unitData; var ib = db.unitData; var ra = ia.relationData; var rb = ib.relationData;
            if (ra == null || rb == null) throw new InvalidOperationException("双方关系尚未就绪");
            var pair = new RelationPair { Context = context, A = a, B = b, NameA = ia.propertyData.GetName(), NameB = ib.propertyData.GetName(),
                UnitA = ua.Pointer, UnitB = ub.Pointer, DataA = da.Pointer, DataB = db.Pointer, InfoA = ia.Pointer, InfoB = ib.Pointer,
                RelationA = ra.Pointer, RelationB = rb.Pointer, StateA = State(ra), StateB = State(rb), AB = Raw(ra, b), BA = Raw(rb, a) };
            foreach (var link in Direct(a)) if (link.To == b && link.Type <= 11) pair.Types.Add(link.Type);
            return pair;
        }
    }
}
