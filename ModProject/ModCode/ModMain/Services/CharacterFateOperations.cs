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
            var context = CharacterReader.ContextKey();
            if (string.IsNullOrEmpty(context)) throw new InvalidOperationException("存档上下文已失效");
            CharacterSave.Sync(context);
            return unit;
        }
        internal static bool Add(string unitId, int id, out string message)
        {
            bool mayHaveChanged = false;
            try
            {
                var unit = Target(unitId);
                var context = CharacterReader.ContextKey(); var pointer = unit.Pointer; var actualId = unit.data.unitData.unitID;
                Action ensure = () => CharacterSave.RequireCurrentTarget(context, actualId, pointer);
                ensure();
                var item = g.conf.roleCreateFeature.GetItem(id);
                if (item == null || item.conceal == 1) throw new InvalidOperationException("隐藏效果或无效配置不能手动添加");
                if (unit.GetLuck(id) != null || HasRecord(unit, id)) throw new InvalidOperationException("人物已拥有该气运");
                // 修改器主动赋予，不套用随机生成时的成就、同组和全局名额限制。
                if (item.type == 6) throw new InvalidOperationException("壶妖专属气运不能添加给修士");
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
                    ensure(); if (WorldFactory.CreateLuckInID(id) == null) throw new InvalidOperationException("气运运行类不可用");
                    ensure(); p.bornLuck = new Il2CppReferenceArray<DataUnit.LuckData>(records.ToArray()); ensure();
                    CharacterSave.MarkDirty(); mayHaveChanged = true;
                    ensure(); var runtime = unit.CreateLuck(record); ensure();
                    // Init may already have registered runtime effects or callback-owned records.
                    if (runtime == null) throw new InvalidOperationException("气运运行效果未确认，请核对记录");
                    ensure(); runtime.NewCreateLuck(); ensure();
                }
                else
                {
                    // 原生回调可能在修改拥有记录后抛错；先留待保存标记，不漏记部分执行。
                    CharacterSave.MarkDirty(); mayHaveChanged = true;
                    ensure(); var result = unit.CreateAction(new UnitActionLuckAdd(id), false); ensure();
                    if (!HasRecord(unit, id) || unit.GetLuck(id) == null) throw new InvalidOperationException("气运记录或运行效果未确认（" + result + "）");
                }
                ensure(); if (!HasRecord(unit, id) || unit.GetLuck(id) == null) throw new InvalidOperationException("气运记录或运行效果未一致，请核对");
                message = "已添加“" + CharacterFateCatalog.ReadName(item) + "”"; return true;
            }
            catch (Exception ex) { message = "添加未完成：" + ex.Message + (mayHaveChanged ? "；气运可能部分生效，请重新读取后核对" : ""); return false; }
            finally
            {
                try { CharacterSave.Sync(CharacterReader.ContextKey()); }
                catch (Exception ex) { UnityEngine.Debug.LogWarning("[八荒秘录] 气运操作后上下文同步失败：" + ex); }
            }
        }
        internal static bool Remove(string unitId, int id, out string message)
        {
            try
            {
                var unit = Target(unitId);
                var context = CharacterReader.ContextKey(); var pointer = unit.Pointer; var actualId = unit.data.unitData.unitID;
                Action ensure = () => CharacterSave.RequireCurrentTarget(context, actualId, pointer);
                ensure();
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
                ensure(); var result = unit.CreateAction(new UnitActionLuckDel(id), false); ensure();
                if (unit.GetLuck(id) != null) throw new InvalidOperationException("游戏拒绝移除（" + result + "）");
                // 原生删除注销运行效果并清理addLuck；补齐其不负责的先天拥有记录。
                if (born)
                {
                    // 注销回调可能新增其他先天气运，重新读取而非覆盖为旧快照。
                    ensure(); records.Clear();
                    if (p.bornLuck != null) for (int i = 0; i < p.bornLuck.Length; i++)
                    { var record = p.bornLuck[i]; if (record == null || record.id != id) records.Add(record); }
                    ensure(); p.bornLuck = new Il2CppReferenceArray<DataUnit.LuckData>(records.ToArray()); ensure();
                }
                if (HasRecord(unit, id)) throw new InvalidOperationException("移除后的记录未一致，请重新读取");
                message = "已移除“" + CharacterFateCatalog.ReadName(item) + "”"; return true;
            }
            catch (Exception ex) { message = "移除未完成：" + ex.Message; return false; }
            finally
            {
                try { CharacterSave.Sync(CharacterReader.ContextKey()); }
                catch (Exception ex) { UnityEngine.Debug.LogWarning("[八荒秘录] 气运操作后上下文同步失败：" + ex); }
            }
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
