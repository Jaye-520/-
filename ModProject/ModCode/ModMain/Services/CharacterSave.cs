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
            internal Action Apply, Undo, Verify;
            internal int Order;
            internal string UnitId;
            internal IntPtr UnitPointer;
        }
        private static string context;
        // 仅保存身份，不持有原生对象；临时UI失效不能解除同一世界中的不确定状态。
        private static string hobbyContext;
        private static readonly Dictionary<string, IntPtr> interruptedHobbies = new Dictionary<string, IntPtr>();
        private static Il2CppSystem.Action<bool> saveCallback;
        private static Action completion;
        private static readonly object saveGate = new object();
        private static long saveGeneration;
        internal static int Pending { get; private set; }
        internal static bool Busy { get; private set; }
        internal static void Sync(string key)
        {
            SyncHobbyContext(key);
            if (key == context) return;
            context = key; Pending = 0;
        }
        private static string HobbyWorldKey()
        {
            var player = CharacterReader.Resolve(null);
            return player == null ? null : player.Pointer.ToInt64().ToString(CultureInfo.InvariantCulture) + ":" + player.data.unitData.unitID;
        }
        private static void SyncHobbyContext(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            var worldKey = HobbyWorldKey();
            if (string.IsNullOrEmpty(worldKey) || worldKey == hobbyContext) return;
            interruptedHobbies.Clear(); hobbyContext = worldKey;
        }
        private static void RequireHobbyReady(string key, string unitId, IntPtr pointer)
        {
            SyncHobbyContext(key); IntPtr interrupted;
            if (interruptedHobbies.TryGetValue(unitId, out interrupted) && interrupted == pointer)
                throw new InvalidOperationException("此人物上次兴趣修改未完成，请重新载入存档后核对");
        }
        internal static void MarkDirty() { Pending++; }
        internal static void MarkDirty(string expectedContext)
        {
            var current = CharacterReader.ContextKey(); Sync(current);
            if (!string.IsNullOrEmpty(current) && current == expectedContext) MarkDirty();
        }
        internal static void Poll()
        {
            var done = System.Threading.Interlocked.Exchange(ref completion, null);
            if (done != null) done();
        }
        internal static void Commit(CharacterEditSession session, Action<bool, string> report)
        {
            var receiver = report;
            report = (success, message) =>
            {
                try { if (receiver != null) receiver(success, message); }
                catch (Exception ex) { UnityEngine.Debug.LogWarning("[八荒秘录] 保存反馈显示失败，保存状态独立保留：" + ex.GetType().Name + ": " + ex.Message); }
            };
            if (Busy || g.data == null || DataMgr.isSaveData) { report(false, "游戏正在保存，请稍候"); return; }
            Sync(CharacterReader.ContextKey());
            if (session.Context != context || string.IsNullOrEmpty(context)) { report(false, "存档上下文已失效"); return; }
            var writeContext = context;
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
                EnsureWriteTarget(writeContext, null);
            }
            catch (Exception ex) { report(false, "未写入：" + ex.Message); return; }
            int applied = 0;
            try
            {
                for (int i = 0; i < writes.Count; i++)
                {
                    EnsureWriteTarget(writeContext, writes[i]);
                    // 将当前步骤纳入回退；setter/原生回调可能在赋值后抛异常。
                    applied = i + 1; writes[i].Apply();
                    EnsureWriteTarget(writeContext, writes[i]);
                }
                EnsureWriteTarget(writeContext, null);
                foreach (var write in writes) { EnsureWriteTarget(writeContext, write); write.Verify(); }
                if (writes.Count > 0) { MarkDirty(); session.Clear(); }
            }
            catch (Exception ex)
            {
                bool restored = true;
                for (int i = applied - 1; i >= 0; i--)
                {
                    if (writes[i].Undo == null) { restored = false; continue; }
                    try { EnsureWriteTarget(writeContext, writes[i]); writes[i].Undo(); EnsureWriteTarget(writeContext, writes[i]); } catch { restored = false; }
                }
                var currentContext = CharacterReader.ContextKey();
                if (currentContext != writeContext)
                {
                    Sync(currentContext); session.Sync(currentContext);
                    report(false, "写入过程中存档已切换，已停止后续操作；原存档可能部分执行，请核对"); return;
                }
                string rollbackError;
                if (!session.Validate(c => CharacterEditData.Read(CharacterReader.Resolve(c.UnitId), c.Field), out rollbackError)) restored = false;
                if (!restored) { MarkDirty(); session.Clear(); }
                report(false, restored ? "写入失败，已恢复原字段：" + ex.Message : "原生操作未完成，内存可能已改变；未执行磁盘保存，请重新读取：" + ex.Message);
                return;
            }
            var startedContext = context;
            var dirty = Pending;
            long attempt; bool callbackQueued = false;
            lock (saveGate) { attempt = ++saveGeneration; Busy = true; }
            try
            {
                saveCallback = (Il2CppSystem.Action<bool>)(new Action<bool>(success =>
                {
                    // 身份检查、去重与结果发布同一临界区，旧线程不能覆盖新结果。
                    lock (saveGate)
                    {
                        if (attempt != saveGeneration || callbackQueued) return;
                        callbackQueued = true;
                        System.Threading.Interlocked.Exchange(ref completion, new Action(() =>
                        {
                            lock (saveGate)
                            {
                                if (attempt != saveGeneration) return;
                                Busy = false; saveCallback = null;
                            }
                            if (CharacterReader.ContextKey() != startedContext)
                            { report(false, "保存回调到达时存档已切换，请核对当前存档"); return; }
                            if (success) Pending = Math.Max(0, Pending - dirty);
                            report(success, success ? "修改已保存" : "游戏保存失败，修改仍在内存；可再次点击保存");
                        }));
                    }
                }));
                g.data.SaveData(saveCallback);
                report(true, "正在保存…");
            }
            catch (Exception ex)
            {
                bool received;
                lock (saveGate)
                {
                    received = callbackQueued;
                    if (!received && attempt == saveGeneration) { saveGeneration++; Busy = false; saveCallback = null; }
                }
                // 原生可能先同步返回结果再抛错，已收到的结果必须由Poll确认。
                report(received, received ? "保存结果已收到，等待确认；原生调用异常：" + ex.Message : "保存未完成，修改仍在内存：" + ex.Message);
            }
        }
        private static void EnsureContext(string expectedContext)
        {
            if (string.IsNullOrEmpty(expectedContext) || CharacterReader.ContextKey() != expectedContext) throw new InvalidOperationException("存档上下文已变化");
            if (Busy || DataMgr.isSaveData) throw new InvalidOperationException("游戏已开始保存，停止后续写入");
        }
        internal static WorldUnitBase RequireCurrentTarget(string expectedContext, string unitId, IntPtr pointer)
        {
            EnsureContext(expectedContext); var unit = CharacterReader.Resolve(unitId);
            if (unit == null || unit.Pointer != pointer || unit.data.unitData.unitID != unitId) throw new InvalidOperationException("原人物已失效或被替换");
            return unit;
        }
        private static void EnsureWriteTarget(string expectedContext, Write write)
        { if (write == null) EnsureContext(expectedContext); else RequireCurrentTarget(expectedContext, write.UnitId, write.UnitPointer); }
        private static Write Prepare(CharacterEditSession.Change c, List<CharacterEditSession.Change> changes)
        {
            var unit = CharacterReader.Resolve(c.UnitId);
            if (unit == null) throw new InvalidOperationException("人物已失效");
            var preparedContext = CharacterReader.ContextKey(); var pointer = unit.Pointer;
            var dataPointer = unit.data.Pointer; var infoPointer = unit.data.unitData.Pointer;
            var propertyPointer = unit.data.unitData.propertyData.Pointer;
            var number = c.Field.Name || c.Field.Choice ? null : CharacterEditData.Number(unit, c.Field);
            var numberPointer = number == null ? IntPtr.Zero : number.Pointer;
            Action ensure = () =>
            {
                var current = RequireCurrentTarget(preparedContext, c.UnitId, pointer).data;
                if (current == null || current.Pointer != dataPointer || current.unitData == null || current.unitData.Pointer != infoPointer ||
                    current.unitData.propertyData == null || current.unitData.propertyData.Pointer != propertyPointer)
                    throw new InvalidOperationException("人物属性容器已被替换，停止保存");
                if (numberPointer != IntPtr.Zero)
                {
                    var field = CharacterEditData.Number(unit, c.Field);
                    if (field == null || field.Pointer != numberPointer) throw new InvalidOperationException("原属性字段已被替换，停止保存");
                }
            };
            var write = PrepareForUnit(c, changes, unit);
            write.Verify = () =>
            {
                ensure(); var actual = CharacterEditData.Read(unit, c.Field); ensure();
                if (actual != c.Value) throw new InvalidOperationException(CharacterSnapshot.Labels[c.Field.Group][c.Field.Index] + "整批回读与目标不一致");
            };
            write.UnitId = c.UnitId; write.UnitPointer = pointer; return write;
        }
        private static Write PrepareForUnit(CharacterEditSession.Change c, List<CharacterEditSession.Change> changes, WorldUnitBase unit)
        {
            var preparedContext = CharacterReader.ContextKey(); var unitPointer = unit.Pointer;
            Action ensure = () => RequireCurrentTarget(preparedContext, c.UnitId, unitPointer);
            ensure();
            var ownedData = unit.data; var ownedInfo = ownedData.unitData;
            var p = ownedInfo.propertyData;
            var dataPointer = ownedData.Pointer; var infoPointer = ownedInfo.Pointer; var propertyPointer = p.Pointer;
            var ensureIdentity = ensure;
            ensure = () =>
            {
                ensureIdentity();
                var current = CharacterReader.Resolve(c.UnitId).data;
                if (current == null || current.Pointer != dataPointer || current.unitData == null || current.unitData.Pointer != infoPointer ||
                    current.unitData.propertyData == null || current.unitData.propertyData.Pointer != propertyPointer)
                    throw new InvalidOperationException("人物属性容器已被替换，停止写入");
            };
            ensure();
            var f = c.Field;
            if (f.Name)
            {
                var old = p.name;
                if (old == null || old.Length != 2) throw new InvalidOperationException("姓名结构异常，未改写");
                var first = StringInfo.GetNextTextElement(c.Value);
                var next = new Il2CppStringArray(new[] { first, c.Value.Substring(first.Length) });
                return new Write { Apply = () => { ensure(); p.name = next; ensure(); }, Undo = () => { ensure(); p.name = old; ensure(); } };
            }
            var value = int.Parse(c.Value, CultureInfo.InvariantCulture);
            if (f.Choice)
            {
                var legal = CharacterEditData.Choices(f, c.Original).Exists(choice => choice.Id == c.Value);
                if (!legal) throw new InvalidOperationException("选项配置不匹配");
                if (f.Group == 0 && f.Index == 9)
                {
                    var old = p.sex;
                    return new Write { Apply = () => { ensure(); p.sex = (UnitSexType)value; ensure(); }, Undo = () => { ensure(); p.sex = old; ensure(); } };
                }
                if (f.Group == 0)
                {
                    var preparedHobbyContext = HobbyWorldKey();
                    var ensureProperty = ensure;
                    ensure = () => { ensureProperty(); RequireHobbyReady(preparedContext, c.UnitId, unitPointer); };
                    ensure();
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
                        { if (change != c) return new Write { Apply = ensure, Undo = ensure }; break; }
                    bool interrupted = false, started = false; Action runtimeGuard = null;
                    Action markInterrupted = () =>
                    {
                        interrupted = true;
                        if (!started) return;
                        SyncHobbyContext(CharacterReader.ContextKey());
                        if (hobbyContext == preparedHobbyContext) interruptedHobbies[c.UnitId] = unitPointer;
                    };
                    Action begin = () => started = true;
                    return new Write
                    {
                        Apply = () => { try { runtimeGuard = SetHobbies(unit, next, ensure, begin); } catch { markInterrupted(); throw; } },
                        Undo = () =>
                        {
                            if (!started) return;
                            try
                            {
                                if (interrupted) throw new InvalidOperationException("兴趣运行状态未完整，停止自动重建");
                                if (runtimeGuard != null) runtimeGuard();
                                runtimeGuard = SetHobbies(unit, old, ensure, begin);
                            }
                            catch { markInterrupted(); throw; }
                        }
                    };
                }
                if (f.Index == 10)
                {
                    int old = int.Parse(c.Original, CultureInfo.InvariantCulture);
                    Action<int> set = id =>
                    {
                        ensure(); if (f.Slot == 0) p.inTrait = id; else if (f.Slot == 1) p.outTrait1 = id; else p.outTrait2 = id;
                        ensure(); ownedData.GetCharacterWeight(); ensure();
                    };
                    return new Write { Apply = () => set(value), Undo = () => set(old) };
                }
                return PrepareRealm(unit, value);
            }
            var number = CharacterEditData.Number(unit, f);
            if (number == null || number.lockValue || number.onBaseValueSetCall == null)
                throw new InvalidOperationException("该属性缺少可持久化的原生写入入口");
            if (number.allEqualCall.Count > 0)
                throw new InvalidOperationException("该属性被固定效果覆盖，请先移除相关效果再修改");
            var numberPointer = number.Pointer;
            var ensureTarget = ensure;
            ensure = () =>
            {
                ensureTarget();
                var current = CharacterEditData.Number(CharacterReader.Resolve(c.UnitId), f);
                if (current == null || current.Pointer != numberPointer) throw new InvalidOperationException("原属性字段已被替换，停止写入");
            };
            ensure();
            var before = number.baseValue;
            long target = value;
            if (f.Group == 0 && f.Index == 0) target = (long)value * 12 + number.value % 12;
            var multiplied = number.allMulCall.Count > 0 || number.allMulAddCall.Count > 0;
            var adjusted = multiplied ? FindNumberBase(number, before, checked((int)target), ensure)
                : checked((int)(before + target - number.value));
            if (adjusted < number.minValue || adjusted > number.maxValue || target < number.minValue || target > number.maxValue)
                throw new InvalidOperationException("数值超出游戏允许范围（" + number.minValue + "–" + number.maxValue + "）");
            bool numberStarted = false;
            return new Write
            {
                Order = f.Group == 0 && f.Index >= 1 && f.Index <= 6 && f.Slot == 1 ? -1 : 0,
                Apply = () =>
                {
                    ensure();
                    if (multiplied && FindNumberBase(number, before, checked((int)target), ensure) != adjusted)
                        throw new InvalidOperationException("属性倍率已变化，请重新确认修改");
                    numberStarted = true; number.baseValue = adjusted; ensure();
                    if (number.value != target) throw new InvalidOperationException("属性效果导致结果不匹配");
                },
                Undo = () => { if (!numberStarted) return; ensure(); number.baseValue = before; ensure(); }
            };
        }
        private static int FindNumberBase(DynInt source, int before, int target, Action ensure)
        {
            // 独立DynInt仅用于原生计算，不复制持久setter/事件，不探写真实人物。
            ensure();
            if (source.baseValue != before || source.lockValue || source.allEqualCall.Count > 0)
                throw new InvalidOperationException("属性基础值或固定效果已变化，请重新确认修改");
            var original = source.value;
            var model = new DynInt(before) {
                mMinValue = source.minValue, mMaxValue = source.maxValue,
                scaleGetValue = source.scaleGetValue, isOverflow = source.isOverflow,
                isValViewOut = source.isValViewOut, changeRate = source.changeRate,
                effectiveRate = source.effectiveRate, offestFixValue = source.offestFixValue,
                _allAddCall = source.allAddCall, _allMulCall = source.allMulCall,
                _allMulAddCall = source.allMulAddCall };
            Func<int, int> evaluate = candidate => {
                ensure(); model.mBaseValue = candidate; var result = model.value; ensure(); return result;
            };
            if (evaluate(before) != original)
                throw new InvalidOperationException("属性计算无法独立复现，未修改");
            if (original == target) return before;
            bool increasing = target > original;
            int edge = before, edgeValue = original, low = before, high = before;
            long step = Math.Max(1L, Math.Abs((long)target - original));
            bool bracketed = false;
            for (int i = 0; i < 32; i++)
            {
                var next = (int)Math.Max(source.minValue, Math.Min(source.maxValue,
                    increasing ? (long)edge + step : (long)edge - step));
                if (next == edge) break;
                var result = evaluate(next);
                if (increasing ? result < edgeValue : result > edgeValue)
                    throw new InvalidOperationException("倍率计算非单调或溢出，未修改");
                low = Math.Min(edge, next); high = Math.Max(edge, next);
                if (increasing ? result >= target : result <= target) { bracketed = true; break; }
                edge = next; edgeValue = result; step = Math.Min((long)int.MaxValue * 2, step * 2);
            }
            if (bracketed)
                while (low <= high)
                {
                    var candidate = (int)(low + ((long)high - low) / 2);
                    var result = evaluate(candidate);
                    if (result == target) return candidate;
                    if (result < target) { if (candidate == int.MaxValue) break; low = candidate + 1; }
                    else { if (candidate == int.MinValue) break; high = candidate - 1; }
                }
            throw new InvalidOperationException("输入值在当前倍率及取整规则下无法达到，请调整数值");
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
        private static Action SetHobbies(WorldUnitBase unit, int[] ids, Action ensure, Action begin)
        {
            ensure(); var owned = unit.allHobby; var pointer = owned.Pointer;
            var expected = new List<WorldUnitHobbyBase>(); foreach (var hobby in owned) expected.Add(hobby);
            var previous = new List<WorldUnitHobbyBase>(expected);
            var unique = new HashSet<IntPtr>();
            foreach (var hobby in previous) if (hobby != null && !unique.Add(hobby.Pointer)) throw new InvalidOperationException("兴趣运行记录重复");
            Action check = () =>
            {
                ensure(); var current = unit.allHobby;
                if (current == null || current.Pointer != pointer || current.Count != expected.Count)
                    throw new InvalidOperationException("兴趣运行集合已变化，停止重建");
                for (int i = 0; i < expected.Count; i++)
                    if ((current[i] == null ? IntPtr.Zero : current[i].Pointer) != (expected[i] == null ? IntPtr.Zero : expected[i].Pointer))
                        throw new InvalidOperationException("兴趣运行记录已变化，停止重建");
            };
            for (int i = previous.Count - 1; i >= 0; i--)
            {
                check(); var hobby = previous[i];
                if (hobby != null) { begin(); hobby.Destroy(); }
                ensure();
                // 允许Destroy自行移除自己；不能把其他新增/移除记录视为自己的清理。
                if (owned.Count == expected.Count - 1 && !owned.Contains(hobby)) expected.Remove(hobby);
                check();
                if (owned.Contains(hobby)) { begin(); owned.Remove(hobby); expected.Remove(hobby); check(); }
            }
            var slots = new Il2CppStructArray<int>(ids.Length);
            for (int i = 0; i < ids.Length; i++) slots[i] = ids[i];
            check(); begin(); unit.data.unitData.propertyData.hobby = slots; check();
            foreach (var id in ids)
            {
                var conf = g.conf.roleCreateHobby.GetItem(id);
                if (string.IsNullOrEmpty(conf.item) || conf.item == "0") continue;
                foreach (var action in conf.item.Split('|'))
                {
                    check(); var hobby = WorldFactory.CreateUnitHobby(action.Split('_')[0]);
                    check(); if (hobby == null) throw new InvalidOperationException("兴趣运行类不可用");
                    begin(); owned.Add(hobby); expected.Add(hobby); check();
                    hobby.Init(unit, conf, action); check();
                }
            }
            check(); unit.data.GetHobbyItems(); check();
            return check;
        }
    }
}
