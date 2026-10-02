using System;
using System.Collections.Generic;
using System.Globalization;
using UnhollowerBaseLib;

namespace MOD_YtxSQ1.Services
{
    // 数字修改走DynInt的原生setter，保存走DataMgr；绝不直接序列化游戏存档。
    internal static class CharacterSave
    {
        private sealed class Write
        {
            internal Action Apply, Undo;
            internal int Order;
        }
        private static string context;
        private static Il2CppSystem.Action<bool> saveCallback;
        private static Action completion;
        internal static int Pending { get; private set; }
        internal static bool Busy { get; private set; }
        internal static void Sync(string key)
        {
            if (key == context) return;
            context = key; Pending = 0;
        }
        internal static void MarkDirty() { Pending++; }
        internal static void Poll()
        {
            var done = System.Threading.Interlocked.Exchange(ref completion, null);
            if (done != null) done();
        }
        internal static void Commit(CharacterEditSession session, Action<bool, string> report)
        {
            if (Busy || g.data == null || DataMgr.isSaveData) { report(false, "游戏正在保存，请稍候"); return; }
            Sync(CharacterReader.ContextKey());
            if (session.Context != context || string.IsNullOrEmpty(context)) { report(false, "存档上下文已失效"); return; }
            var writes = new List<Write>();
            try
            {
                string error;
                if (!session.Validate(c => CharacterEditData.Read(CharacterReader.Resolve(c.UnitId), c.Field), out error))
                    throw new InvalidOperationException(error);
                var changes = new List<CharacterEditSession.Change>(session.Changes);
                foreach (var c in changes)
                {
                    if (c.Field.Group == 1 && c.Field.Index == 11 && changes.Count != 1)
                        throw new InvalidOperationException("境界突破会改变属性，请单独保存境界，再修改数值");
                    try { writes.Add(Prepare(c, changes)); }
                    catch (Exception ex) { throw new InvalidOperationException(CharacterEditData.Read(CharacterReader.Resolve(c.UnitId), new CharacterField(0, 11)) + " · " + CharacterSnapshot.Labels[c.Field.Group][c.Field.Index] + "：" + ex.Message); }
                }
                writes.Sort((a, b) => a.Order.CompareTo(b.Order));
            }
            catch (Exception ex) { report(false, "未写入：" + ex.Message); return; }
            int applied = 0;
            try
            {
                for (int i = 0; i < writes.Count; i++)
                {
                    // 将当前步骤纳入回退；setter/原生回调可能在赋值后抛异常。
                    applied = i + 1; writes[i].Apply();
                }
                if (writes.Count > 0) { MarkDirty(); session.Clear(); }
            }
            catch (Exception ex)
            {
                bool restored = true;
                for (int i = applied - 1; i >= 0; i--)
                {
                    if (writes[i].Undo == null) { restored = false; continue; }
                    try { writes[i].Undo(); } catch { restored = false; }
                }
                string rollbackError;
                if (!session.Validate(c => CharacterEditData.Read(CharacterReader.Resolve(c.UnitId), c.Field), out rollbackError)) restored = false;
                if (!restored) { MarkDirty(); session.Clear(); }
                report(false, restored ? "写入失败，已恢复原字段：" + ex.Message : "原生操作未完成，内存可能已改变；未执行磁盘保存，请重新读取：" + ex.Message);
                return;
            }
            var startedContext = context;
            var dirty = Pending;
            Busy = true;
            try
            {
                saveCallback = (Il2CppSystem.Action<bool>)(new Action<bool>(success =>
                {
                    // 回调只存托管结果；UI刷新留到主线程Poll，关闭窗口也不遗失结果。
                    System.Threading.Interlocked.Exchange(ref completion, new Action(() =>
                    {
                        Busy = false; saveCallback = null;
                        if (CharacterReader.ContextKey() != startedContext)
                        { report(false, "保存回调到达时存档已切换，请核对当前存档"); return; }
                        if (success) Pending = Math.Max(0, Pending - dirty);
                        report(success, success ? "修改已保存" : "游戏保存失败，修改仍在内存；可再次点击保存");
                    }));
                }));
                g.data.SaveData(saveCallback);
                report(true, "正在保存…");
            }
            catch (Exception ex) { Busy = false; saveCallback = null; report(false, "保存未完成，修改仍在内存：" + ex.Message); }
        }
        private static Write Prepare(CharacterEditSession.Change c, List<CharacterEditSession.Change> changes)
        {
            var unit = CharacterReader.Resolve(c.UnitId);
            if (unit == null) throw new InvalidOperationException("人物已失效");
            var p = unit.data.unitData.propertyData;
            var f = c.Field;
            if (f.Name)
            {
                var old = p.name;
                if (old == null || old.Length != 2) throw new InvalidOperationException("姓名结构异常，未改写");
                var first = StringInfo.GetNextTextElement(c.Value);
                var next = new Il2CppStringArray(new[] { first, c.Value.Substring(first.Length) });
                return new Write { Apply = () => p.name = next, Undo = () => p.name = old };
            }
            var value = int.Parse(c.Value, CultureInfo.InvariantCulture);
            if (f.Choice)
            {
                var legal = CharacterEditData.Choices(f, c.Original).Exists(choice => choice.Id == c.Value);
                if (!legal) throw new InvalidOperationException("选项配置不匹配");
                if (f.Group == 0 && f.Index == 9)
                {
                    var old = p.sex;
                    return new Write { Apply = () => p.sex = (UnitSexType)value, Undo = () => p.sex = old };
                }
                if (f.Group == 0)
                {
                    if (p.hobby == null || p.hobby.Length != 3) throw new InvalidOperationException("兴趣槽位结构异常");
                    var old = new int[3]; var next = new int[3];
                    for (int i = 0; i < 3; i++) old[i] = next[i] = p.hobby[i];
                    foreach (var change in changes) if (change.UnitId == c.UnitId && change.Field.Group == 0 && change.Field.Index == 10)
                        next[change.Field.Slot] = int.Parse(change.Value, CultureInfo.InvariantCulture);
                    if (new HashSet<int>(next).Count != next.Length) throw new InvalidOperationException("三个兴趣不能重复");
                    ValidateHobbies(next);
                    // 同人物三个槽合并一次重建；其余步骤为空，避免重复注册运行回调。
                    foreach (var change in changes)
                        if (change.UnitId == c.UnitId && change.Field.Group == 0 && change.Field.Index == 10)
                        { if (change != c) return new Write { Apply = () => { }, Undo = () => { } }; break; }
                    return new Write { Apply = () => SetHobbies(unit, next), Undo = () => SetHobbies(unit, old) };
                }
                if (f.Index == 10)
                {
                    int old = int.Parse(c.Original, CultureInfo.InvariantCulture);
                    Action<int> set = id => { if (f.Slot == 0) p.inTrait = id; else if (f.Slot == 1) p.outTrait1 = id; else p.outTrait2 = id; unit.data.GetCharacterWeight(); };
                    return new Write { Apply = () => set(value), Undo = () => set(old) };
                }
                return PrepareRealm(unit, value);
            }
            var number = CharacterEditData.Number(unit, f);
            if (number == null || number.lockValue || number.onBaseValueSetCall == null)
                throw new InvalidOperationException("该属性缺少可持久化的原生写入入口");
            if (number.allMulCall.Count > 0 || number.allMulAddCall.Count > 0 || number.allEqualCall.Count > 0)
                throw new InvalidOperationException("该属性受倍率或固定效果控制，请先移除相关效果再修改");
            var before = number.baseValue;
            long target = value;
            if (f.Group == 0 && f.Index == 0) target = (long)value * 12 + number.value % 12;
            var adjusted = checked((int)(before + target - number.value));
            if (adjusted < number.minValue || adjusted > number.maxValue || target < number.minValue || target > number.maxValue)
                throw new InvalidOperationException("数值超出游戏允许范围（" + number.minValue + "–" + number.maxValue + "）");
            return new Write
            {
                Order = f.Group == 0 && f.Index >= 1 && f.Index <= 6 && f.Slot == 1 ? -1 : 0,
                Apply = () => { number.baseValue = adjusted; if (number.value != target) throw new InvalidOperationException("属性效果导致结果不匹配"); },
                Undo = () => number.baseValue = before
            };
        }
        private static Write PrepareRealm(WorldUnitBase unit, int id)
        {
            var plan = CharacterRealmChange.Prepare(unit, id);
            return new Write { Order = 1, Apply = plan.Apply, Undo = plan.Undo };
        }
        private static void ValidateHobbies(int[] ids)
        {
            foreach (var id in ids)
            {
                var conf = g.conf.roleCreateHobby.GetItem(id);
                if (conf == null) throw new InvalidOperationException("兴趣配置缺失");
                if (string.IsNullOrEmpty(conf.item) || conf.item == "0") continue;
                foreach (var action in conf.item.Split('|'))
                    if (WorldFactory.CreateUnitHobby(action.Split('_')[0]) == null) throw new InvalidOperationException("兴趣运行类不可用");
            }
        }
        private static void SetHobbies(WorldUnitBase unit, int[] ids)
        {
            for (int i = unit.allHobby.Count - 1; i >= 0; i--) unit.allHobby[i].Destroy();
            unit.allHobby.Clear();
            var slots = new Il2CppStructArray<int>(ids.Length);
            for (int i = 0; i < ids.Length; i++) slots[i] = ids[i];
            unit.data.unitData.propertyData.hobby = slots;
            foreach (var id in ids)
            {
                var conf = g.conf.roleCreateHobby.GetItem(id);
                if (string.IsNullOrEmpty(conf.item) || conf.item == "0") continue;
                foreach (var action in conf.item.Split('|'))
                {
                    var hobby = WorldFactory.CreateUnitHobby(action.Split('_')[0]);
                    unit.allHobby.Add(hobby); hobby.Init(unit, conf, action);
                }
            }
            unit.data.GetHobbyItems();
        }
    }
}
