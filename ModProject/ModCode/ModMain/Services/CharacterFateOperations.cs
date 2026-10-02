using System;
using System.Collections.Generic;
using UnhollowerBaseLib;

namespace MOD_YtxSQ1.Services
{
    internal static class CharacterFateOperations
    {
        private static WorldUnitBase Target(string id)
        {
            if (CharacterSave.Busy || g.data == null || DataMgr.isSaveData) throw new InvalidOperationException("正在保存，请稍候");
            var unit = CharacterReader.Resolve(id);
            if (unit == null) throw new InvalidOperationException("人物已失效");
            CharacterSave.Sync(CharacterReader.ContextKey());
            return unit;
        }
        internal static bool Add(string unitId, int id, out string message)
        {
            try
            {
                var unit = Target(unitId);
                var item = g.conf.roleCreateFeature.GetItem(id);
                if (item == null || item.conceal == 1) throw new InvalidOperationException("隐藏效果或无效配置不能手动添加");
                if (unit.GetLuck(id) != null || HasRecord(unit, id)) throw new InvalidOperationException("人物已拥有该气运");
                // 同组效果可能互相替换；禁止静默删除旧效果或覆盖剧情锁定项。
                for (int i = 0; i < unit.allLuck.Count; i++)
                {
                    var existing = unit.allLuck[i];
                    if (existing == null) continue;
                    var conf = g.conf.roleCreateFeature.GetItem(existing.luckData.id);
                    if (conf != null && item.group > 0 && conf.group == item.group)
                        throw new InvalidOperationException("已有同组气运，请先核对并移除旧项");
                }
                if (item.type == 6) throw new InvalidOperationException("壶妖专属气运不能添加给修士");
                if (item.lockLuck != 0) throw new InvalidOperationException("该气运有成就解锁条件，暂不绕过解锁添加");
                if (item.globalMax != 0) throw new InvalidOperationException("该气运有全局数量限制，暂不绕过限制添加");
                if (CharacterFateCatalog.Category(item.type) == 0)
                {
                    // 先天记录必须留在bornLuck；普通LuckAdd只写addLuck，不能用于替代。
                    var p = unit.data.unitData.propertyData;
                    var previous = p.bornLuck;
                    var records = new List<DataUnit.LuckData>();
                    if (previous != null) for (int i = 0; i < previous.Length; i++) records.Add(previous[i]);
                    var record = new DataUnit.LuckData { id = id, duration = ConfRoleCreateFeatureEx.duration(item) };
                    records.Add(record);
                    // 工厂先验证此配置是否有实际运行类，避免把不能初始化的条目写入记录。
                    if (WorldFactory.CreateLuckInID(id) == null) throw new InvalidOperationException("气运运行类不可用");
                    p.bornLuck = new Il2CppReferenceArray<DataUnit.LuckData>(records.ToArray());
                    CharacterSave.MarkDirty();
                    var runtime = unit.CreateLuck(record);
                    if (runtime == null) { p.bornLuck = previous; throw new InvalidOperationException("气运初始化失败，记录已恢复"); }
                    runtime.NewCreateLuck();
                }
                else
                {
                    // 原生回调可能在修改拥有记录后抛错；先留待保存标记，不漏记部分执行。
                    CharacterSave.MarkDirty();
                    var result = unit.CreateAction(new UnitActionLuckAdd(id), false);
                    if (!HasRecord(unit, id)) throw new InvalidOperationException("游戏拒绝添加（" + result + "）");
                }
                message = "已添加“" + CharacterFateCatalog.ReadName(item) + "”"; return true;
            }
            catch (Exception ex) { message = "添加未完成：" + ex.Message; return false; }
        }
        internal static bool Remove(string unitId, int id, out string message)
        {
            try
            {
                var unit = Target(unitId);
                var item = g.conf.roleCreateFeature.GetItem(id);
                if (item == null || item.conceal == 1)
                    throw new InvalidOperationException("隐藏或缺失配置的效果不能手动移除");
                if (!HasRecord(unit, id)) throw new InvalidOperationException("该气运已不存在");
                var p = unit.data.unitData.propertyData;
                bool born = false;
                var records = new List<DataUnit.LuckData>();
                if (p.bornLuck != null) for (int i = 0; i < p.bornLuck.Length; i++)
                {
                    var record = p.bornLuck[i];
                    if (record != null && record.id == id) born = true; else records.Add(record);
                }
                CharacterSave.MarkDirty();
                var result = unit.CreateAction(new UnitActionLuckDel(id), false);
                if (unit.GetLuck(id) != null) throw new InvalidOperationException("游戏拒绝移除（" + result + "）");
                // 原生删除注销运行效果并清理addLuck；补齐其不负责的先天拥有记录。
                if (born) p.bornLuck = new Il2CppReferenceArray<DataUnit.LuckData>(records.ToArray());
                if (HasRecord(unit, id)) throw new InvalidOperationException("移除后的记录未一致，请重新读取");
                message = "已移除“" + CharacterFateCatalog.ReadName(item) + "”"; return true;
            }
            catch (Exception ex) { message = "移除未完成：" + ex.Message; return false; }
        }
        private static bool HasRecord(WorldUnitBase unit, int id)
        {
            var p = unit.data.unitData.propertyData;
            if (p.bornLuck != null) for (int i = 0; i < p.bornLuck.Length; i++) if (p.bornLuck[i] != null && p.bornLuck[i].id == id) return true;
            if (p.addLuck != null) for (int i = 0; i < p.addLuck.Count; i++) if (p.addLuck[i] != null && p.addLuck[i].id == id) return true;
            return false;
        }
    }
}
