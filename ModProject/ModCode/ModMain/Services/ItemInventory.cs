using System;
using System.Collections.Generic;
using System.Text;

namespace MOD_YtxSQ1.Services
{
    // UI仅保存上下文、人物ID与物品唯一ID；原生引用只在一次读取/操作内使用。
    internal sealed class InventoryItem
    {
        internal string SoleId, Name, DataSignature;
        internal int Id, Count;
        internal bool Hidden;
    }
    internal sealed class ItemEntry
    {
        internal int Id;
        internal string Name, Description;
        internal bool Stackable;
    }
    internal static class ItemInventory
    {
        internal static string LocalText(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key) || key == "0") return fallback;
            try { var text = ConfLocalText.GetText(key); return string.IsNullOrEmpty(text) || text == key ? fallback : text; }
            catch { return fallback; }
        }
        internal static List<ItemEntry> Catalog(bool includeDescriptions = true)
        {
            var result = new List<ItemEntry>(); var seen = new HashSet<int>();
            var items = g.conf.itemProps._allConfList;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || item.id <= 0 || !seen.Add(item.id)) continue;
                result.Add(new ItemEntry { Id = item.id, Name = LocalText(item.name, "道具 #" + item.id),
                    Description = includeDescriptions ? LocalText(item.desc, "暂无介绍") : null, Stackable = item.isOverlay == 1 });
            }
            return result;
        }
        internal static string DescribeCatalogEntry(ItemEntry entry)
        {
            if (entry == null) return "暂无介绍";
            if (entry.Description != null) return entry.Description;
            try
            {
                var item = g.conf.itemProps.GetItem(entry.Id);
                entry.Description = item == null ? "暂无介绍" : LocalText(item.desc, "暂无介绍");
                return entry.Description;
            }
            catch { return "介绍暂不可读取"; }
        }
        internal static List<InventoryItem> Read(string unitId, bool includeHidden = true)
        {
            var unit = CharacterReader.Resolve(unitId);
            if (unit == null) throw new InvalidOperationException("当前人物已失效");
            var result = new List<InventoryItem>();
            // 原生可见背包集合排除隐藏资源及已装备记录，并使用人物自身显示规则。
            var items = unit.data.unitData.propData._allShowProps;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || item.propsCount <= 0) continue;
                var hidden = item.propsItem != null && item.propsItem.hide == 1;
                if (hidden && !includeHidden) continue;
                var info = item.propsInfoBase;
                result.Add(new InventoryItem { SoleId = item.soleID, Id = item.propsID, Count = item.propsCount, Hidden = hidden,
                    DataSignature = item.soleIDHashData, Name = info == null ? "道具 #" + item.propsID : info.name });
            }
            if (includeHidden)
                foreach (var item in unit.data.unitData.propData.allProps)
                {
                    if (item == null || item.propsCount <= 0 || item.propsItem == null || item.propsItem.hide != 1 || result.Exists(value => value.SoleId == item.soleID)) continue;
                    var info = item.propsInfoBase;
                    result.Add(new InventoryItem { SoleId = item.soleID, Id = item.propsID, Count = item.propsCount, Hidden = true,
                        DataSignature = item.soleIDHashData, Name = info == null ? "道具 #" + item.propsID : info.name });
                }
            return result;
        }
        internal static void ReadCapacity(string unitId, out int used, out int total)
        {
            var unit = CharacterReader.Resolve(unitId);
            if (unit == null) throw new InvalidOperationException("当前人物已失效");
            var bag = unit.data.unitData.propData;
            used = Math.Max(0, bag.GetGridCurNum(true));
            total = Math.Max(0, bag.GetGridMaxNum());
        }
        internal static string Fingerprint(List<InventoryItem> items, bool ordered = false)
        {
            var sorted = new List<InventoryItem>(items); if (!ordered) sorted.Sort((a, b) => string.CompareOrdinal(a.SoleId, b.SoleId));
            var text = new StringBuilder();
            foreach (var item in sorted) text.Append(item.SoleId.Length).Append(':').Append(item.SoleId).Append(':').Append(item.Id).Append(':').Append(item.Count).Append(':').Append((item.DataSignature ?? "").Length).Append(':').Append(item.DataSignature).Append(':').Append(item.Hidden).Append(';');
            return text.ToString();
        }
        internal static WorldUnitBase RequireTarget(string context, string unitId)
        {
            if (string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(context) || CharacterReader.ContextKey() != context)
                throw new InvalidOperationException("人物或存档上下文已变化，请重新操作");
            if (CharacterSave.Busy || DataMgr.isSaveData) throw new InvalidOperationException("游戏正在保存，请稍候");
            var unit = CharacterReader.Resolve(unitId);
            if (unit == null || unit.data.unitData.unitID != unitId) throw new InvalidOperationException("当前人物已失效");
            return unit;
        }
        internal static Action Guard(string context, WorldUnitBase unit)
        {
            var id = unit.data.unitData.unitID; var pointer = unit.Pointer;
            var bag = unit.data.unitData.propData;
            if (bag == null) throw new InvalidOperationException("背包尚未就绪");
            var bagPointer = bag.Pointer;
            Action ensure = () =>
            {
                var current = CharacterSave.RequireCurrentTarget(context, id, pointer);
                if (current.data.unitData.propData == null || current.data.unitData.propData.Pointer != bagPointer)
                    throw new InvalidOperationException("背包对象已变化，请重新读取");
            };
            ensure(); return ensure;
        }
        internal static string Give(string context, string unitId, int id, int amount)
        {
            var unit = RequireTarget(context, unitId);
            var conf = g.conf.itemProps.GetItem(id);
            if (conf == null) throw new InvalidOperationException("道具配置不可发放");
            if (amount < 1 || (conf.isOverlay != 1 && amount != 1))
                throw new InvalidOperationException(conf.isOverlay == 1 ? "请输入正整数数量（最大2147483647）" : "不可叠加道具每次发放1个");
            Add(context, unit, id, amount);
            return LocalText(conf.name, "道具 #" + id) + "已增加" + amount + "个；请点击保存";
        }
        private static void Add(string context, WorldUnitBase unit, int id, int amount)
        {
            var ensure = Guard(context, unit);
            var bag = unit.data.unitData.propData;
            var conf = g.conf.itemProps.GetItem(id);
            // 与原生AddProps相同：满格拒绝新记录，已有叠加记录不再占格。
            if (conf != null && conf.hide == 0 && (conf.isOverlay != 1 || bag.GetProps(id).Count == 0)
                && bag.GetGridCurNum(true) >= bag.GetGridMaxNum())
                throw new InvalidOperationException("背包已满，无法新增这一项；请先腾出格子");
            var current = bag.GetPropsNum(id);
            if (current < 0 || amount > int.MaxValue - current) throw new InvalidOperationException("增加后的总数量超过游戏整数范围（2147483647），未添加");
            var expected = current + amount;
            // 新建唯一ID、叠加、原生子类回调均由工厂维护；失败不假装复杂回调可回滚。
            CharacterSave.Sync(context);
            try
            {
                // 原生代码以customSoleID != ""判断是否覆盖工厂编号；null也会进入
                // 覆盖分支，随后唯一ID字典拒绝空key。空字符串才是自动生成的约定。
                ensure(); var added = bag.AddProps(id, amount, ""); ensure(); bag.UpdateAllShowProps(); ensure();
                if (bag.GetPropsNum(id) != expected) throw new InvalidOperationException("发放数量回读不一致，请核对背包");
                if (added == null || added.Count == 0) throw new InvalidOperationException("发放结果未返回物品记录");
                for (int i = 0; i < added.Count; i++)
                {
                    var item = added[i];
                    if (item == null || string.IsNullOrEmpty(item.soleID)) throw new InvalidOperationException("发放结果缺少物品编号");
                    var stored = bag.GetProps(item.soleID);
                    if (stored == null || stored.propsID != id || stored.propsCount <= 0)
                        throw new InvalidOperationException("发放结果与背包索引不一致");
                }
            }
            catch (Exception ex) { throw new InvalidOperationException("发放未完整确认：" + ex.Message + "；已发生的变化仍在内存，请核对背包后保存"); }
            finally { CharacterSave.MarkDirty(context); }
        }
        private static DataProps.PropsData RequireItem(WorldUnitBase unit, InventoryItem expected, bool includeHidden = true)
        {
            if (expected == null || string.IsNullOrEmpty(expected.SoleId)) throw new InvalidOperationException("物品编号无效，请重新选择");
            var bag = unit.data.unitData.propData; var item = bag.GetProps(expected.SoleId);
            if (item == null || item.soleID != expected.SoleId || item.propsID != expected.Id || item.propsCount != expected.Count || string.IsNullOrEmpty(expected.DataSignature) || item.soleIDHashData != expected.DataSignature)
                throw new InvalidOperationException("这项物品已经变化，请重新选择");
            // 数量修改允许持有的隐藏资源；删除限定原生可见记录，仍排除装备和失效快照。
            bool found = Read(unit.data.unitData.unitID, includeHidden).Exists(value => value.SoleId == expected.SoleId);
            if (!found) throw new InvalidOperationException("这项物品已离开可见背包，请重新选择");
            return item;
        }
        internal static string IncreaseCount(string context, string unitId, InventoryItem expected, int amount)
        {
            if (expected == null || amount < 1 || expected.Count < 1 || expected.Count > int.MaxValue - amount)
                throw new InvalidOperationException("追加后的总数量必须在1–2147483647之间");
            // 同一唯一ID累加，仍通过SetCount检查人物、存档上下文和数量快照。
            return SetCount(context, unitId, expected, expected.Count + amount);
        }
        internal static string SetCount(string context, string unitId, InventoryItem expected, int count)
        {
            var unit = RequireTarget(context, unitId); var item = RequireItem(unit, expected);
            if (item.propsItem == null || item.propsItem.isOverlay != 1) throw new InvalidOperationException("不可叠加物品不能修改数量");
            if (count == item.propsCount) return "数量未改变";
            if (count < 1) throw new InvalidOperationException("请输入正整数数量；清除物品请使用移除操作");
            var ensure = Guard(context, unit);
            var bag = unit.data.unitData.propData;
            var total = (long)bag.GetPropsNum(item.propsID) - item.propsCount + count;
            if (total < 1 || total > int.MaxValue) throw new InvalidOperationException("同类物品总数量超过游戏整数范围（2147483647），未修改");
            CharacterSave.Sync(context);
            try
            {
                // 原生AddProps/DelProps也使用此setter更新values[2]并失效哈希缓存。
                // 仅改指定唯一ID的正数量，保留品质、词条等values和所有背包索引。
                ensure(); item.propsCount = count; ensure(); bag.UpdateAllShowProps(); ensure();
                var stored = bag.GetProps(expected.SoleId);
                if (stored == null || stored.propsID != expected.Id || stored.propsCount != count)
                    throw new InvalidOperationException("数量回读不一致");
            }
            catch (Exception ex) { throw new InvalidOperationException("数量修改未完整确认：" + ex.Message + "；请核对背包", ex); }
            finally { CharacterSave.MarkDirty(context); }
            return expected.Name + "的数量已改为" + count + "；请点击保存";
        }
        internal static string Remove(string context, string unitId, InventoryItem expected)
        {
            var unit = RequireTarget(context, unitId); RequireItem(unit, expected, false);
            var ensure = Guard(context, unit);
            var bag = unit.data.unitData.propData; CharacterSave.Sync(context);
            try
            {
                ensure(); bag.DelProps(expected.SoleId); ensure(); bag.UpdateAllShowProps(); ensure();
                if (bag.IsHasProps(expected.SoleId)) throw new InvalidOperationException("物品仍在背包中");
            }
            catch (Exception ex) { throw new InvalidOperationException("移除未完整确认：" + ex.Message + "；请核对背包", ex); }
            finally { CharacterSave.MarkDirty(context); }
            return "已移除" + expected.Name + "这一项；请点击保存";
        }
        internal static string Clear(string context, string unitId, string expectedFingerprint)
        {
            var unit = RequireTarget(context, unitId); var items = Read(unitId, false);
            if (Fingerprint(items) != expectedFingerprint) throw new InvalidOperationException("背包内容已变化，请重新确认清空");
            if (items.Count == 0) return "背包已经为空";
            var ensure = Guard(context, unit);
            var bag = unit.data.unitData.propData; CharacterSave.Sync(context);
            try
            {
                foreach (var item in items)
                {
                    // 上一件的原生删除回调可能改变下一件或世界；逐件再次核对。
                    ensure(); var current = RequireTarget(context, unitId); RequireItem(current, item, false);
                    current.data.unitData.propData.DelProps(item.SoleId); ensure();
                }
                ensure(); bag.UpdateAllShowProps(); ensure();
                foreach (var item in items) if (bag.IsHasProps(item.SoleId)) throw new InvalidOperationException("部分物品未清除，请核对背包");
            }
            catch (Exception ex) { throw new InvalidOperationException("清空未全部完成：" + ex.Message + "；已删除的物品未恢复，请核对背包后保存"); }
            finally { CharacterSave.MarkDirty(context); }
            return "已清空" + items.Count + "项背包物品；请点击保存";
        }
        internal static int BestMedicine(WorldUnitBase unit, string resource)
        {
            int best = 0, bestEffect = 0, bestLevel = -1;
            var grade = unit.data.dynUnitData.GetGrade(); var pills = g.conf.itemPill._allConfList;
            for (int i = 0; i < pills.Count; i++)
            {
                var pill = pills[i];
                // 补给限定本境界可用的普通恢复药；不赠送越阶服药或带其他作用的药。
                if (pill == null || pill.effectType != 101 || pill.grade > grade || pill.operUse != 1 || pill.battleUIHide == 1) continue;
                var conf = g.conf.itemProps.GetItem(pill.id);
                if (conf == null || conf.hide == 1 || conf.isOverlay != 1) continue;
                var effects = g.conf.itemPill.GetPillAddValue(unit, pill.id, false);
                if (effects == null || !effects.ContainsKey(resource)) continue;
                var value = effects[resource];
                if (value > bestEffect || (value == bestEffect && (conf.level > bestLevel || (conf.level == bestLevel && pill.id < best))))
                { best = pill.id; bestEffect = value; bestLevel = conf.level; }
            }
            if (best == 0) throw new InvalidOperationException("未找到当前境界可用的" + (resource == "hp" ? "回血" : "回蓝") + "药");
            return best;
        }
        internal static string Supply(string context, string unitId)
        {
            var unit = RequireTarget(context, unitId);
            var ensure = Guard(context, unit);
            int hp = BestMedicine(unit, "hp"), mp = BestMedicine(unit, "mp");
            var bag = unit.data.unitData.propData;
            int expectedHp, expectedMp;
            checked { expectedHp = bag.GetPropsNum(hp) + (hp == mp ? 40 : 20); expectedMp = bag.GetPropsNum(mp) + 20; }
            var simulatedUsed = bag.GetGridCurNum(true); var maximum = bag.GetGridMaxNum();
            foreach (var id in hp == mp ? new[] { hp } : new[] { hp, mp })
            {
                var conf = g.conf.itemProps.GetItem(id);
                if (conf.hide == 0 && (conf.isOverlay != 1 || bag.GetProps(id).Count == 0))
                {
                    if (simulatedUsed >= maximum) throw new InvalidOperationException("背包空格不足，未发放补给；请先腾出格子");
                    // 普通物品drop/dieDrop均为0时不计入原生GetGridCurNum(true)。
                    if (conf.drop != 0 || conf.dieDrop != 0) simulatedUsed++;
                }
            }
            ensure(); Add(context, unit, hp, 20);
            try
            {
                ensure(); Add(context, unit, mp, 20); ensure();
                if (bag.GetPropsNum(hp) != expectedHp || (hp != mp && bag.GetPropsNum(mp) != expectedMp))
                    throw new InvalidOperationException("整批补给数量回读不一致");
                ensure();
            }
            catch (Exception ex) { throw new InvalidOperationException("补给未完整确认：" + ex.Message + "；请核对背包，勿直接重复补给", ex); }
            return LocalText(g.conf.itemProps.GetItem(hp).name, "回血药") + "、" + LocalText(g.conf.itemProps.GetItem(mp).name, "回蓝药") + "各增加20个；请点击保存";
        }
    }
}
