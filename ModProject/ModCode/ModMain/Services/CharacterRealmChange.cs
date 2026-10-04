using System;
using System.Collections.Generic;

namespace MOD_YtxSQ1.Services
{
    // 境界编辑采用配置成长差额，不执行突破剧情、材料消耗和随机奖励。
    // 历史材料/丹药/装备的额外加成保留；降阶不是回滚整个游戏进度。
    internal static class CharacterRealmChange
    {
        internal sealed class Plan { internal Action Apply, Undo; }
        private sealed class Number { internal DynInt Field; internal int Before, After; internal string DebtKey, BeforeDebt; internal long AfterDebt; }
        private sealed class HistoryStamp
        {
            private readonly IntPtr pointer;
            private readonly int quality, luck;
            internal HistoryStamp(DataWorld.World.PlayerLogData.GradeData record)
            { pointer = record == null ? IntPtr.Zero : record.Pointer; quality = record == null ? 0 : record.quality; luck = record == null ? 0 : record.luck; }
            internal bool Matches(DataWorld.World.PlayerLogData.GradeData record)
            { return record == null ? pointer == IntPtr.Zero : pointer == record.Pointer && quality == record.quality && luck == record.luck; }
        }
        private static int Stage(ConfRoleGradeItem c) { return (c.grade - 1) * 3 + c.phase; }
        internal static Plan Prepare(WorldUnitBase unit, int id)
        {
            var preparedContext = CharacterReader.ContextKey(); var unitId = unit.data.unitData.unitID; var pointer = unit.Pointer;
            Action ensure = () => CharacterSave.RequireCurrentTarget(preparedContext, unitId, pointer);
            ensure();
            var ownedData = unit.data; var ownedInfo = ownedData.unitData;
            var d = ownedData.dynUnitData;
            var p = ownedInfo.propertyData;
            var old = g.conf.roleGrade.GetItem(d.gradeID.value);
            var next = g.conf.roleGrade.GetItem(id);
            if (old == null || next == null || old.phase < 1 || next.phase < 1) throw new InvalidOperationException("境界配置缺失");
            var history = unit.Pointer == g.world.playerUnit.Pointer ? g.data.world.playerLog.upGrade : unit.data.unitData.npcUpGrade;
            if (history == null) throw new InvalidOperationException("突破品质记录未就绪");
            var beforeHistory = new Dictionary<int, DataWorld.World.PlayerLogData.GradeData>();
            foreach (var pair in history) beforeHistory.Add(pair.Key, pair.Value);
            // 字典/记录引用的浅快照不能发现同一GradeData的品质或气运被回调修改。
            var expectedHistory = new Dictionary<int, HistoryStamp>();
            foreach (var pair in beforeHistory) expectedHistory.Add(pair.Key, new HistoryStamp(pair.Value));
            var metadata = unit.data.unitData.objData;
            if (metadata == null) throw new InvalidOperationException("人物扩展数据未就绪");
            var dataPointer = ownedData.Pointer; var infoPointer = ownedInfo.Pointer;
            var dynamicPointer = d.Pointer; var propertyPointer = p.Pointer;
            var metadataPointer = metadata.Pointer; var historyPointer = history.Pointer;
            var isPlayer = unit.Pointer == g.world.playerUnit.Pointer;
            var fieldGuards = new List<Action>();
            ensure = () =>
            {
                var current = CharacterSave.RequireCurrentTarget(preparedContext, unitId, pointer);
                var currentData = current.data;
                if (currentData == null || currentData.Pointer != dataPointer || currentData.unitData == null || currentData.unitData.Pointer != infoPointer ||
                    currentData.dynUnitData == null || currentData.dynUnitData.Pointer != dynamicPointer)
                    throw new InvalidOperationException("人物成长数据已被替换，停止境界操作");
                var info = currentData.unitData;
                if (info.propertyData == null || info.propertyData.Pointer != propertyPointer || info.objData == null || info.objData.Pointer != metadataPointer)
                    throw new InvalidOperationException("人物属性或扩展数据已被替换，停止境界操作");
                var currentHistory = isPlayer ? g.data.world.playerLog.upGrade : info.npcUpGrade;
                if (currentHistory == null || currentHistory.Pointer != historyPointer)
                    throw new InvalidOperationException("突破历史已被替换，停止境界操作");
                if (currentHistory.Count != expectedHistory.Count) throw new InvalidOperationException("突破历史内容已变化，停止境界操作");
                foreach (var pair in expectedHistory)
                {
                    DataWorld.World.PlayerLogData.GradeData record;
                    if (!currentHistory.TryGetValue(pair.Key, out record) || !pair.Value.Matches(record))
                        throw new InvalidOperationException("突破品质或气运记录已变化，停止境界操作");
                }
                foreach (var guard in fieldGuards) guard();
            };
            ensure();
            // 原生历史只记品质；同品质有多个成长分支，不能用品质缓存重建已知分支。
            var branchBefore = new Dictionary<int, string>();
            var branches = new Dictionary<int, int>();
            var configs = g.conf.roleGrade._allConfList;
            var branchGrades = new HashSet<int> { old.grade, next.grade };
            for (int i = 0; i < configs.Count; i++) if (configs[i] != null) branchGrades.Add(configs[i].grade);
            foreach (var grade in branchGrades)
            {
                var key = BranchKey(grade);
                if (!metadata.ContainsKey(key)) continue;
                var raw = metadata.GetString(key); branchBefore.Add(grade, raw);
                int rowId;
                if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out rowId))
                    throw new InvalidOperationException("境界分支记录异常");
                var row = g.conf.roleGrade.GetItem(rowId);
                DataWorld.World.PlayerLogData.GradeData recorded; beforeHistory.TryGetValue(grade, out recorded);
                if (row == null || row.grade != grade || row.phase != 1 ||
                    (recorded != null && row.quality != recorded.quality && !(old.grade == grade && old.phase == 1)))
                    throw new InvalidOperationException("境界分支配置与历史不一致");
                branches.Add(grade, rowId);
            }
            // 当前配置ID是直接依据；离开初期前把其分支保留下来。
            if (old.phase == 1) branches[old.grade] = old.id;
            var oldTotals = Totals(old, beforeHistory, branches);
            var newHistory = new Dictionary<int, DataWorld.World.PlayerLogData.GradeData>(beforeHistory);
            if (old.grade > 1 && old.phase == 1)
            {
                DataWorld.World.PlayerLogData.GradeData previous;
                newHistory.TryGetValue(old.grade, out previous);
                newHistory[old.grade] = new DataWorld.World.PlayerLogData.GradeData { quality = old.quality, luck = previous == null ? 0 : previous.luck };
            }
            // 只调整目标境界和以上的品质记录，保留低境界已有品质及气运记录。
            var remove = new List<int>();
            foreach (var key in newHistory.Keys) if (key > next.grade) remove.Add(key);
            foreach (var key in remove) newHistory.Remove(key);
            if (next.grade > 1 && next.phase == 1)
            {
                DataWorld.World.PlayerLogData.GradeData previous;
                newHistory.TryGetValue(next.grade, out previous);
                newHistory[next.grade] = new DataWorld.World.PlayerLogData.GradeData { quality = next.quality, luck = previous == null ? 0 : previous.luck };
            }
            var newBranches = new Dictionary<int, int>(branches);
            foreach (var grade in branchGrades) if (grade > next.grade) newBranches.Remove(grade);
            if (next.phase == 1) newBranches[next.grade] = next.id;
            var newTotals = Totals(next, newHistory, newBranches);
            var numbers = new List<Number>();
            var keys = new HashSet<string>(oldTotals.Keys); keys.UnionWith(newTotals.Keys);
            foreach (var key in keys)
            {
                long a, b; oldTotals.TryGetValue(key, out a); newTotals.TryGetValue(key, out b);
                var fields = d.GetValues(key);
                var count = fields == null ? 0 : fields.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<DynInt>>().Count;
                if (count == 0) throw new InvalidOperationException("成长字段不存在：" + key);
                var fieldPointers = new IntPtr[count];
                for (int i = 0; i < count; i++)
                {
                    if (fields[i] == null) throw new InvalidOperationException("成长字段未就绪：" + key);
                    fieldPointers[i] = fields[i].Pointer;
                }
                fieldGuards.Add(() =>
                {
                    var currentFields = d.GetValues(key);
                    var currentCount = currentFields == null ? 0 : currentFields.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<DynInt>>().Count;
                    if (currentCount != fieldPointers.Length) throw new InvalidOperationException("成长字段结构已变化：" + key);
                    for (int i = 0; i < currentCount; i++)
                        if (currentFields[i] == null || currentFields[i].Pointer != fieldPointers[i])
                            throw new InvalidOperationException("成长字段已被替换：" + key);
                });
                for (int i = 0; i < count; i++)
                {
                    var field = fields[i];
                    var debtKey = "BHML.realm.debt:" + key + "/" + i;
                    var hasDebt = metadata.ContainsKey(debtKey);
                    var beforeDebt = hasDebt ? metadata.GetString(debtKey) : null;
                    long debt = 0;
                    if (hasDebt && (!long.TryParse(beforeDebt, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out debt) || debt > 0)) throw new InvalidOperationException("成长差额记录异常");
                    if (a == b && debt == 0) continue;
                    var ideal = checked(field.baseValue + b - a + debt);
                    // 下限截断的差额存入人物扩展数据；再升回去不会凭空多加一轮成长。
                    int minimum = field.minValue;
                    if (key == "hp" || key == "hpMax") minimum = Math.Max(1, minimum);
                    if (key == "life") minimum = Math.Max(minimum, checked(d.age.value + 12));
                    if (key == "abilityPoint") minimum = Math.Max(minimum, d.GetCurUseAbilityPoint());
                    int after = checked((int)Math.Max(minimum, ideal));
                    if (field.onBaseValueSetCall == null || field.lockValue || after > field.maxValue)
                        throw new InvalidOperationException("成长属性无法安全写入：" + key);
                    numbers.Add(new Number { Field = field, Before = field.baseValue, After = after, DebtKey = debtKey, BeforeDebt = beforeDebt, AfterDebt = checked(ideal - after) });
                }
            }
            // GetValues返回的集合可重建，只绑定其中字段，另绑定境界和补满所用资源/上限。
            var sources = new Func<DynInt>[] { () => d.gradeID, () => d.hp, () => d.mp, () => d.sp, () => d.hpMax, () => d.mpMax, () => d.spMax };
            foreach (var source in sources)
            {
                var field = source();
                if (field == null) throw new InvalidOperationException("境界或资源字段未就绪");
                var fieldPointer = field.Pointer;
                fieldGuards.Add(() =>
                {
                    var currentField = source();
                    if (currentField == null || currentField.Pointer != fieldPointer)
                        throw new InvalidOperationException("境界或资源字段已被替换，停止操作");
                });
            }
            ensure();
            var resources = new[] { d.hp, d.mp, d.sp };
            var resourceBefore = new[] { d.hp.baseValue, d.mp.baseValue, d.sp.baseValue };
            var oldExp = p.exp; var oldId = old.id;
            // 瓶颈含limitGradeExp效果，换境界后不注销会继续把经验锁在旧阶段。
            var lockIds = new HashSet<int>();
            for (int i = 0; i < configs.Count; i++) if (configs[i] != null && configs[i].lockFeature > 0) lockIds.Add(configs[i].lockFeature);
            var oldLocks = new List<DataUnit.LuckData>();
            if (p.addLuck != null) for (int i = 0; i < p.addLuck.Count; i++)
                if (p.addLuck[i] != null && lockIds.Contains(p.addLuck[i].id)) oldLocks.Add(p.addLuck[i]);
            Action<Dictionary<int, DataWorld.World.PlayerLogData.GradeData>> setHistory = values =>
            {
                ensure();
                var removeKeys = new List<int>();
                foreach (var key in expectedHistory.Keys) if (!values.ContainsKey(key)) removeKeys.Add(key);
                foreach (var key in removeKeys)
                {
                    ensure(); history.Remove(key); expectedHistory.Remove(key); ensure();
                }
                foreach (var pair in values)
                {
                    ensure(); HistoryStamp previous;
                    if (expectedHistory.TryGetValue(pair.Key, out previous) && previous.Matches(pair.Value)) continue;
                    var stamp = new HistoryStamp(pair.Value);
                    history[pair.Key] = pair.Value; expectedHistory[pair.Key] = stamp; ensure();
                }
                ensure();
            };
            return new Plan
            {
                Apply = () =>
                {
                    foreach (var record in oldLocks)
                    {
                        ensure(); unit.CreateAction(new UnitActionLuckDel(record.id), false); ensure();
                        if (unit.GetLuck(record.id) != null) throw new InvalidOperationException("旧瓶颈未能注销");
                    }
                    foreach (var n in numbers)
                    {
                        ensure(); n.Field.baseValue = n.After; ensure();
                        if (n.AfterDebt == 0) metadata.DelString(n.DebtKey);
                        else metadata.SetString(n.DebtKey, n.AfterDebt.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    foreach (var grade in branchGrades)
                    {
                        ensure(); int rowId;
                        if (newBranches.TryGetValue(grade, out rowId)) metadata.SetString(BranchKey(grade), rowId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        else metadata.DelString(BranchKey(grade));
                        ensure();
                    }
                    setHistory(newHistory);
                    ensure(); p.SetGradeID(id); ensure(); // 同步原生经验基线和境界刷新事件，不能只赋gradeID。
                    ensure(); unit.UpdateConf(); ensure();
                    for (int i = 0; i < resources.Length; i++)
                    {
                        ensure(); var maximum = i == 0 ? d.hpMax.value : i == 1 ? d.mpMax.value : d.spMax.value;
                        // 显示值含加法效果；不能把计算上限直接作为基础值而再次叠加效果。
                        resources[i].baseValue = checked((int)((long)resources[i].baseValue + maximum - resources[i].value));
                        ensure(); if (resources[i].value != maximum) throw new InvalidOperationException("突破后的资源恢复回读不一致");
                    }
                    if (d.gradeID.value != id) throw new InvalidOperationException("境界写入结果不一致");
                    foreach (var n in numbers) if (n.Field.baseValue != n.After) throw new InvalidOperationException("成长写入结果不一致");
                    foreach (var grade in branchGrades)
                    {
                        ensure(); int rowId; var key = BranchKey(grade);
                        if (newBranches.TryGetValue(grade, out rowId) ? !metadata.ContainsKey(key) || metadata.GetString(key) != rowId.ToString(System.Globalization.CultureInfo.InvariantCulture) : metadata.ContainsKey(key))
                            throw new InvalidOperationException("境界分支记录回读不一致");
                    }
                },
                Undo = () =>
                {
                    foreach (var n in numbers)
                    {
                        ensure(); n.Field.baseValue = n.Before; ensure();
                        if (n.BeforeDebt == null) metadata.DelString(n.DebtKey); else metadata.SetString(n.DebtKey, n.BeforeDebt);
                    }
                    foreach (var grade in branchGrades)
                    {
                        ensure(); string raw;
                        if (branchBefore.TryGetValue(grade, out raw)) metadata.SetString(BranchKey(grade), raw);
                        else metadata.DelString(BranchKey(grade));
                        ensure();
                    }
                    setHistory(beforeHistory); ensure(); p.SetGradeID(oldId); ensure(); p.exp = oldExp; unit.UpdateConf(); ensure();
                    for (int i = 0; i < resources.Length; i++) { ensure(); resources[i].baseValue = resourceBefore[i]; ensure(); }
                    foreach (var record in oldLocks) { ensure(); if (unit.GetLuck(record.id) == null)
                    {
                        bool owned = false;
                        for (int i = 0; i < p.addLuck.Count; i++) if (p.addLuck[i].id == record.id) owned = true;
                        if (!owned) p.addLuck.Add(record);
                        ensure(); var runtime = unit.CreateLuck(record); ensure();
                        if (runtime == null) throw new InvalidOperationException("原瓶颈恢复失败");
                        runtime.NewCreateLuck(); ensure();
                    } }
                }
            };
        }
        private static string BranchKey(int grade) { return "BHML.realm.branch:" + grade; }
        private static Dictionary<string, long> Totals(ConfRoleGradeItem target, Dictionary<int, DataWorld.World.PlayerLogData.GradeData> history, Dictionary<int, int> branches)
        {
            var result = new Dictionary<string, long>();
            for (int stage = 2; stage <= Stage(target); stage++)
            {
                int grade = (stage - 1) / 3 + 1, phase = (stage - 1) % 3 + 1;
                DataWorld.World.PlayerLogData.GradeData recorded;
                history.TryGetValue(grade, out recorded);
                int branchId;
                var row = stage == Stage(target) ? target : phase == 1 && branches.TryGetValue(grade, out branchId) ? g.conf.roleGrade.GetItem(branchId) :
                    recorded == null ? g.conf.roleGrade.GetGradeItem(grade, phase) : g.conf.roleGrade.GetGradeItem(grade, phase, recorded.quality);
                if (row == null) row = g.conf.roleGrade.GetGradeItem(grade, phase);
                if (row == null) throw new InvalidOperationException("成长路径缺少配置：" + grade + "/" + phase);
                foreach (var pair in UnitActionRoleUpGrade.GetAddAttr(row)) Add(result, pair.Key, pair.Value);
                Add(result, "life", row.growAddLife);
                Add(result, "abilityPoint", row.abilityPoint);
            }
            return result;
        }
        private static void Add(Dictionary<string, long> totals, string key, int value)
        { long old; totals.TryGetValue(key, out old); totals[key] = checked(old + value); }
    }
}
