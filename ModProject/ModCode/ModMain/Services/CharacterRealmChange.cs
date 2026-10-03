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
        private static int Stage(ConfRoleGradeItem c) { return (c.grade - 1) * 3 + c.phase; }
        internal static Plan Prepare(WorldUnitBase unit, int id)
        {
            var d = unit.data.dynUnitData;
            var p = unit.data.unitData.propertyData;
            var old = g.conf.roleGrade.GetItem(d.gradeID.value);
            var next = g.conf.roleGrade.GetItem(id);
            if (old == null || next == null || old.phase < 1 || next.phase < 1) throw new InvalidOperationException("境界配置缺失");
            var history = unit.Pointer == g.world.playerUnit.Pointer ? g.data.world.playerLog.upGrade : unit.data.unitData.npcUpGrade;
            if (history == null) throw new InvalidOperationException("突破品质记录未就绪");
            var beforeHistory = new Dictionary<int, DataWorld.World.PlayerLogData.GradeData>();
            foreach (var pair in history) beforeHistory.Add(pair.Key, pair.Value);
            var oldTotals = Totals(old, beforeHistory);
            var newHistory = new Dictionary<int, DataWorld.World.PlayerLogData.GradeData>(beforeHistory);
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
            var newTotals = Totals(next, newHistory);
            var numbers = new List<Number>();
            var metadata = unit.data.unitData.objData;
            if (metadata == null) throw new InvalidOperationException("人物扩展数据未就绪");
            var keys = new HashSet<string>(oldTotals.Keys); keys.UnionWith(newTotals.Keys);
            foreach (var key in keys)
            {
                long a, b; oldTotals.TryGetValue(key, out a); newTotals.TryGetValue(key, out b);
                var fields = d.GetValues(key);
                var count = fields == null ? 0 : fields.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<DynInt>>().Count;
                if (count == 0) throw new InvalidOperationException("成长字段不存在：" + key);
                for (int i = 0; i < count; i++)
                {
                    var field = fields[i];
                    var debtKey = "BHML.realm.debt:" + key + "/" + i;
                    var beforeDebt = metadata.ContainsKey(debtKey) ? metadata.GetString(debtKey) : null;
                    long debt = 0;
                    if (beforeDebt != null && !long.TryParse(beforeDebt, out debt)) throw new InvalidOperationException("成长差额记录异常");
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
                    numbers.Add(new Number { Field = field, Before = field.baseValue, After = after, DebtKey = debtKey, BeforeDebt = beforeDebt, AfterDebt = ideal - after });
                }
            }
            var resources = new[] { d.hp, d.mp, d.sp };
            var resourceBefore = new[] { d.hp.baseValue, d.mp.baseValue, d.sp.baseValue };
            var oldExp = p.exp; var oldId = old.id;
            // 瓶颈含limitGradeExp效果，换境界后不注销会继续把经验锁在旧阶段。
            var lockIds = new HashSet<int>();
            var configs = g.conf.roleGrade._allConfList;
            for (int i = 0; i < configs.Count; i++) if (configs[i] != null && configs[i].lockFeature > 0) lockIds.Add(configs[i].lockFeature);
            var oldLocks = new List<DataUnit.LuckData>();
            if (p.addLuck != null) for (int i = 0; i < p.addLuck.Count; i++)
                if (p.addLuck[i] != null && lockIds.Contains(p.addLuck[i].id)) oldLocks.Add(p.addLuck[i]);
            Action<Dictionary<int, DataWorld.World.PlayerLogData.GradeData>> setHistory = values =>
            { history.Clear(); foreach (var pair in values) history.Add(pair.Key, pair.Value); };
            return new Plan
            {
                Apply = () =>
                {
                    foreach (var record in oldLocks)
                    {
                        unit.CreateAction(new UnitActionLuckDel(record.id), false);
                        if (unit.GetLuck(record.id) != null) throw new InvalidOperationException("旧瓶颈未能注销");
                    }
                    foreach (var n in numbers)
                    {
                        n.Field.baseValue = n.After;
                        if (n.AfterDebt == 0) metadata.DelString(n.DebtKey);
                        else metadata.SetString(n.DebtKey, n.AfterDebt.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    setHistory(newHistory);
                    p.SetGradeID(id); // 同步原生经验基线和境界刷新事件，不能只赋gradeID。
                    unit.UpdateConf();
                    for (int i = 0; i < resources.Length; i++)
                    {
                        var maximum = i == 0 ? d.hpMax.value : i == 1 ? d.mpMax.value : d.spMax.value;
                        // 显示值含加法效果；不能把计算上限直接作为基础值而再次叠加效果。
                        resources[i].baseValue = checked(resources[i].baseValue + maximum - resources[i].value);
                        if (resources[i].value != maximum) throw new InvalidOperationException("突破后的资源恢复回读不一致");
                    }
                    if (d.gradeID.value != id) throw new InvalidOperationException("境界写入结果不一致");
                    foreach (var n in numbers) if (n.Field.baseValue != n.After) throw new InvalidOperationException("成长写入结果不一致");
                },
                Undo = () =>
                {
                    foreach (var n in numbers)
                    {
                        n.Field.baseValue = n.Before;
                        if (n.BeforeDebt == null) metadata.DelString(n.DebtKey); else metadata.SetString(n.DebtKey, n.BeforeDebt);
                    }
                    setHistory(beforeHistory); p.SetGradeID(oldId); p.exp = oldExp; unit.UpdateConf();
                    for (int i = 0; i < resources.Length; i++) resources[i].baseValue = resourceBefore[i];
                    foreach (var record in oldLocks) if (unit.GetLuck(record.id) == null)
                    {
                        bool owned = false;
                        for (int i = 0; i < p.addLuck.Count; i++) if (p.addLuck[i].id == record.id) owned = true;
                        if (!owned) p.addLuck.Add(record);
                        var runtime = unit.CreateLuck(record);
                        if (runtime == null) throw new InvalidOperationException("原瓶颈恢复失败");
                        runtime.NewCreateLuck();
                    }
                }
            };
        }
        private static Dictionary<string, long> Totals(ConfRoleGradeItem target, Dictionary<int, DataWorld.World.PlayerLogData.GradeData> history)
        {
            var result = new Dictionary<string, long>();
            for (int stage = 2; stage <= Stage(target); stage++)
            {
                int grade = (stage - 1) / 3 + 1, phase = (stage - 1) % 3 + 1;
                DataWorld.World.PlayerLogData.GradeData recorded;
                history.TryGetValue(grade, out recorded);
                var row = stage == Stage(target) ? target : recorded == null ? g.conf.roleGrade.GetGradeItem(grade, phase) : g.conf.roleGrade.GetGradeItem(grade, phase, recorded.quality);
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
