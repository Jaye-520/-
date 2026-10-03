using System;
using System.Collections.Generic;
using UnityEngine;

namespace MOD_YtxSQ1.Services
{
    internal static class CharacterActions
    {
        private static string returnContext;
        private static readonly Dictionary<string, Vector2Int> returnPoints = new Dictionary<string, Vector2Int>();
        internal static bool IsNpc(string id)
        { var unit = CharacterReader.Resolve(id); return unit != null && g.world.playerUnit != null && unit.Pointer != g.world.playerUnit.Pointer; }
        private static WorldUnitBase Player()
        {
            if (g.world == null || !g.world.isIntoWorld || g.world.playerUnit == null) throw new InvalidOperationException("世界尚未就绪");
            if (CharacterSave.Busy || DataMgr.isSaveData) throw new InvalidOperationException("正在保存，请稍候");
            CharacterSave.Sync(CharacterReader.ContextKey());
            return g.world.playerUnit;
        }
        internal static bool Execute(string id, string action, string amount, out string message)
        {
            try
            {
                var player = Player();
                if (action == "满状态")
                {
                    var unit = CharacterReader.Resolve(id);
                    if (unit == null) throw new InvalidOperationException("人物已失效");
                    var d = unit.data.dynUnitData;
                    var fields = new[] { d.hp, d.mp, d.mood, d.health, d.energy, d.sp };
                    var maxima = new[] { d.hpMax, d.mpMax, d.moodMax, d.healthMax, d.energyMax, d.spMax };
                    var old = new int[fields.Length]; var targets = new int[fields.Length];
                    for (int i = 0; i < fields.Length; i++)
                    {
                        var f = fields[i]; old[i] = f.baseValue;
                        if (f.lockValue || f.onBaseValueSetCall == null || f.allMulCall.Count > 0 || f.allMulAddCall.Count > 0 || f.allEqualCall.Count > 0)
                            throw new InvalidOperationException("当前效果限制了状态恢复");
                        targets[i] = checked(f.baseValue + maxima[i].value - f.value);
                        if (targets[i] < f.minValue || targets[i] > f.maxValue) throw new InvalidOperationException("状态恢复超出游戏允许范围");
                    }
                    int applied = 0;
                    try
                    {
                        for (int i = 0; i < fields.Length; i++)
                        {
                            applied = i + 1; fields[i].baseValue = targets[i];
                            if (fields[i].value != maxima[i].value) throw new InvalidOperationException("状态恢复回读不一致");
                        }
                    }
                    catch
                    {
                        bool restored = true;
                        for (int i = applied - 1; i >= 0; i--)
                            try { fields[i].baseValue = old[i]; if (fields[i].baseValue != old[i]) restored = false; } catch { restored = false; }
                        if (!restored) { CharacterSave.MarkDirty(); throw new InvalidOperationException("部分状态未能恢复原值，请核对人物状态后保存"); }
                        throw;
                    }
                    for (int i = 0; i < fields.Length; i++) if (old[i] != targets[i]) { CharacterSave.MarkDirty(); break; }
                    message = "体力、灵力、心情、健康、精力、念力已恢复至当前上限；请点击保存";
                }
                else if (action == "满修为")
                {
                    var unit = CharacterReader.Resolve(id);
                    if (unit == null) throw new InvalidOperationException("人物已失效");
                    var next = g.conf.roleGrade.GetNextGradeItem(unit.data.dynUnitData.gradeID.value);
                    if (next == null) throw new InvalidOperationException("已到最高阶段");
                    // 累计经验阈值走AddExp，原生瓶颈气运/经验事件在此入口触发。
                    var delta = checked(next.exp - unit.data.unitData.propertyData.exp);
                    var beforeExp = unit.data.unitData.propertyData.exp;
                    if (delta > 0)
                        try { unit.data.unitData.propertyData.AddExp(delta); }
                        finally { if (unit.data.unitData.propertyData.exp != beforeExp) CharacterSave.MarkDirty(); }
                    if (unit.data.unitData.propertyData.exp < next.exp) throw new InvalidOperationException("当前效果限制了经验获取，请核对修为");
                    message = "修为已满，可按游戏流程突破；请点击保存";
                }
                else if (action == "满材料")
                {
                    var bag = player.data.unitData.propData;
                    var changes = new List<DataProps.PropsData>();
                    for (int i = 0; i < bag.allProps.Count; i++)
                    {
                        var item = bag.allProps[i];
                        if (item != null && item.propsItem != null && item.propsItem.isOverlay == 1 && item.propsCount > 0 && item.propsCount < 9999) changes.Add(item);
                    }
                    var old = new List<int>(); foreach (var item in changes) old.Add(item.propsCount);
                    int applied = 0;
                    try
                    {
                        for (int i = 0; i < changes.Count; i++) { applied = i + 1; changes[i].propsCount = 9999; if (changes[i].propsCount != 9999) throw new InvalidOperationException("数量回读不一致"); }
                        bag.UpdateAllShowProps();
                    }
                    catch
                    {
                        bool restored = true;
                        for (int i = applied - 1; i >= 0; i--)
                            try { changes[i].propsCount = old[i]; if (changes[i].propsCount != old[i]) restored = false; } catch { restored = false; }
                        try { bag.UpdateAllShowProps(); } catch { restored = false; }
                        if (!restored) { CharacterSave.MarkDirty(); throw new InvalidOperationException("部分背包数量未能恢复，请核对背包"); }
                        throw;
                    }
                    if (changes.Count > 0) CharacterSave.MarkDirty();
                    message = "背包中 " + changes.Count + " 项可叠加物品已补至9999；不可叠加物品保持原样；请点击保存";
                }
                else
                {
                    var npc = CharacterReader.Resolve(id);
                    if (npc == null || npc.Pointer == player.Pointer) throw new InvalidOperationException("请先选中NPC");
                    if (action == "好感度")
                    {
                        int add;
                        if (!int.TryParse(amount, out add) || add <= 0 || add > 100000) throw new InvalidOperationException("请输入1–100000的好感度增量");
                        var a = player.data.unitData.relationData; var b = npc.data.unitData.relationData;
                        // SetIntim维护原生关系缓存；叠加原始浮点值，避免GetIntim整数化损失。
                        float beforeA = RawIntim(a, id), beforeB = b.intimToPlayerUnit;
                        try { a.SetIntim(id, beforeA + add); b.SetIntim(player.data.unitData.unitID, beforeB + add); }
                        catch
                        {
                            bool restored = true;
                            try { a.SetIntim(id, beforeA); } catch { restored = false; }
                            try { b.SetIntim(player.data.unitData.unitID, beforeB); } catch { restored = false; }
                            try { if (RawIntim(a, id) != beforeA || b.intimToPlayerUnit != beforeB) restored = false; } catch { restored = false; }
                            if (!restored) { CharacterSave.MarkDirty(); throw new InvalidOperationException("双向好感度部分应用，请核对双方关系"); }
                            throw;
                        }
                        CharacterSave.MarkDirty();
                        message = "双向好感度已叠加：玩家→NPC " + beforeA + "→" + RawIntim(a, id) + "，NPC→玩家 " + beforeB + "→" + b.intimToPlayerUnit + "；请点击保存";
                    }
                    else
                    {
                        if (action != "去" && action != "来" && action != "返") throw new InvalidOperationException("未知快捷操作");
                        var context = CharacterReader.ContextKey();
                        if (context != returnContext) { returnPoints.Clear(); returnContext = context; }
                        Vector2Int destination;
                        var moving = action == "去" ? player : npc;
                        if (action == "返") { if (!returnPoints.TryGetValue(id, out destination)) throw new InvalidOperationException("该人物尚未通过“来”召回"); }
                        else destination = action == "去" ? npc.data.unitData.GetPoint() : player.data.unitData.GetPoint();
                        var origin = moving.data.unitData.GetPoint();
                        int result;
                        try { result = moving.CreateAction(new UnitActionSetPoint(destination), false); }
                        finally { if (moving.data.unitData.GetPoint() != origin) CharacterSave.MarkDirty(); }
                        if (result != 0 || moving.data.unitData.GetPoint() != destination) throw new InvalidOperationException("游戏拒绝传送（" + result + "），请核对当前位置");
                        if (action == "来" && !returnPoints.ContainsKey(id)) returnPoints.Add(id, origin);
                        if (action == "返") returnPoints.Remove(id);
                        message = "已完成“" + action + "”；请点击保存";
                        try { RefreshMap(); }
                        catch (Exception ex) { Debug.LogWarning("[八荒秘录] 传送后的地图显示刷新失败：" + ex); message += "（地图显示刷新失败，请重新进入地图核对）"; }
                    }
                }
                return true;
            }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 快捷操作 " + action + " 失败：" + ex); message = "操作未完成：" + ex.Message; return false; }
        }
        private static float RawIntim(DataUnit.RelationData data, string id)
        {
            // 本机Unhollower的泛型byref生成器把float当引用类型构造，TryGetValue会抛Single..ctor(IntPtr)。
            // 索引getter使用PointerToValueGeneric<float>解箱，保留小数且不触碰原生内存布局。
            return data.intimToUnit.ContainsKey(id) ? data.intimToUnit[id] : 0;
        }
        private static void RefreshMap()
        {
            var scene = SceneType.map;
            if (scene == null || scene.world == null || !MapWorldMgr.isEnableMap) return;
            scene.world.UpdateGridUI();
            scene.world.UpdateGridUICmd();
            var ui = g.ui.GetUI(UIType.MapMain);
            var map = ui == null ? null : ui.TryCast<UIMapMain>();
            if (map != null && map.uiGridUnit != null) map.uiGridUnit.UpdateUI(scene.world.curSelectPoint);
        }
    }
}
