using System;
using System.Collections.Generic;
using System.Text;

namespace MOD_YtxSQ1.Services
{
    // UI仅保存上下文、人物ID与物品唯一ID；原生引用只在一次读取/操作内使用。
    internal sealed class InventoryItem
    {
        internal string SoleId, Name;
        internal int Id, Count;
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
        internal static List<ItemEntry> Catalog()
        {
            var result = new List<ItemEntry>(); var seen = new HashSet<int>();
            var items = g.conf.itemProps._allConfList;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || item.id <= 0 || item.hide == 1 || !seen.Add(item.id)) continue;
                result.Add(new ItemEntry { Id = item.id, Name = LocalText(item.name, "道具 #" + item.id),
                    Description = LocalText(item.desc, "暂无介绍"), Stackable = item.isOverlay == 1 });
            }
            return result;
        }
        internal static List<InventoryItem> Read(string unitId)
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
                var info = item.propsInfoBase;
                result.Add(new InventoryItem { SoleId = item.soleID, Id = item.propsID, Count = item.propsCount,
                    Name = info == null ? "道具 #" + item.propsID : info.name });
            }
            return result;
        }
        internal static string Fingerprint(List<InventoryItem> items)
        {
            var sorted = new List<InventoryItem>(items); sorted.Sort((a, b) => string.CompareOrdinal(a.SoleId, b.SoleId));
            var text = new StringBuilder();
            foreach (var item in sorted) text.Append(item.SoleId.Length).Append(':').Append(item.SoleId).Append(':').Append(item.Id).Append(':').Append(item.Count).Append(';');
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
        internal static string Give(string context, string unitId, int id, int amount)
        {
            var unit = RequireTarget(context, unitId);
            var conf = g.conf.itemProps.GetItem(id);
            if (conf == null || conf.hide == 1) throw new InvalidOperationException("道具配置不可发放");
            if (amount < 1 || amount > 9999 || (conf.isOverlay != 1 && amount != 1))
                throw new InvalidOperationException(conf.isOverlay == 1 ? "请输入1–9999的数量" : "不可叠加道具每次发放1个");
            Add(unit, id, amount);
            return LocalText(conf.name, "道具 #" + id) + "已增加" + amount + "个；请点击保存";
        }
        private static void Add(WorldUnitBase unit, int id, int amount)
        {
            var bag = unit.data.unitData.propData;
            var expected = checked(bag.GetPropsNum(id) + amount);
            // 新建唯一ID、叠加、原生子类回调均由工厂维护；失败不假装复杂回调可回滚。
            CharacterSave.Sync(CharacterReader.ContextKey());
            try
            {
                // 原生代码以customSoleID != ""判断是否覆盖工厂编号；null也会进入
                // 覆盖分支，随后唯一ID字典拒绝空key。空字符串才是自动生成的约定。
                bag.AddProps(id, amount, ""); bag.UpdateAllShowProps();
                if (bag.GetPropsNum(id) != expected) throw new InvalidOperationException("发放数量回读不一致，请核对背包");
            }
            catch (Exception ex) { throw new InvalidOperationException("发放未完整确认：" + ex.Message + "；已发生的变化仍在内存，请核对背包后保存"); }
            finally { CharacterSave.MarkDirty(); }
        }
        internal static string Clear(string context, string unitId, string expectedFingerprint)
        {
            var unit = RequireTarget(context, unitId); var items = Read(unitId);
            if (Fingerprint(items) != expectedFingerprint) throw new InvalidOperationException("背包内容已变化，请重新确认清空");
            if (items.Count == 0) return "背包已经为空";
            var bag = unit.data.unitData.propData; CharacterSave.Sync(context);
            try
            {
                foreach (var item in items) bag.DelProps(item.SoleId);
                bag.UpdateAllShowProps();
                foreach (var item in items) if (bag.IsHasProps(item.SoleId)) throw new InvalidOperationException("部分物品未清除，请核对背包");
            }
            catch (Exception ex) { throw new InvalidOperationException("清空未全部完成：" + ex.Message + "；已删除的物品未恢复，请核对背包后保存"); }
            finally { CharacterSave.MarkDirty(); }
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
            int hp = BestMedicine(unit, "hp"), mp = BestMedicine(unit, "mp");
            // 两项先校验溢出，再开始发放。重试会再次增加，失败需明确展示已应用边界。
            checked { var bag = unit.data.unitData.propData; _ = bag.GetPropsNum(hp) + 20; _ = bag.GetPropsNum(mp) + 20; }
            Add(unit, hp, 20);
            try { Add(unit, mp, 20); }
            catch (Exception ex) { throw new InvalidOperationException("回血药已增加20个，回蓝药未确认完成：" + ex.Message); }
            return LocalText(g.conf.itemProps.GetItem(hp).name, "回血药") + "、" + LocalText(g.conf.itemProps.GetItem(mp).name, "回蓝药") + "各增加20个；请点击保存";
        }
    }
}
