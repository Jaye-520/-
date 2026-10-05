using System;
using System.Collections.Generic;
using UnityEngine;

namespace MOD_YtxSQ1.Services
{
    internal static class CharacterActions
    {
        private static string returnContext;
        private static readonly Dictionary<string, Vector2Int> returnPoints = new Dictionary<string, Vector2Int>();
        private static readonly Dictionary<string, IntPtr> returnOwners = new Dictionary<string, IntPtr>();
        internal static bool IsNpc(string id)
        { var unit = CharacterReader.Resolve(id); return unit != null && g.world.playerUnit != null && unit.Pointer != g.world.playerUnit.Pointer; }
        private static WorldUnitBase Player()
        {
            var player = CharacterReader.Resolve(null);
            var context = CharacterReader.ContextKey();
            if (player == null || string.IsNullOrEmpty(context)) throw new InvalidOperationException("世界或存档尚未就绪");
            if (CharacterSave.Busy || DataMgr.isSaveData) throw new InvalidOperationException("正在保存，请稍候");
            CharacterSave.Sync(context);
            return player;
        }
        private static Action Guard(WorldUnitBase unit, string context)
        {
            var id = unit.data.unitData.unitID; var pointer = unit.Pointer;
            Action ensure = () => CharacterSave.RequireCurrentTarget(context, id, pointer);
            ensure(); return ensure;
        }
        private static void MarkDirty(string context) { CharacterSave.MarkDirty(context); }

        // 分派：各操作只负责返回提示语，失败一律抛出，由这里统一转成用户可见消息。
        internal static bool Execute(string id, string action, string amount, out string message)
        {
            try
            {
                var player = Player();
                if (action == "满状态") message = RestoreStatus(id);
                else if (action == "满修为") message = FillCultivation(id);
                else if (action == "满材料") message = FillMaterials(CharacterReader.Resolve(id));
                else
                {
                    // 其余操作都要求选中有效且非玩家自身的NPC；未知操作也先做该校验，保持原有报错顺序。
                    var npc = CharacterReader.Resolve(id);
                    if (npc == null || npc.Pointer == player.Pointer) throw new InvalidOperationException("请先选中NPC");
                    if (action == "好感度") message = AddIntimacy(player, npc, id, amount);
                    else if (action == "去" || action == "来" || action == "返") message = Move(player, npc, id, action);
                    else throw new InvalidOperationException("未知快捷操作");
                }
                return true;
            }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 快捷操作 " + action + " 失败：" + ex); message = "操作未完成：" + ex.Message; return false; }
        }

        // 满状态：六项资源补到当前上限，保留加法效果；任一步失败逆序回退并核对。
        private static string RestoreStatus(string id)
        {
            var unit = CharacterReader.Resolve(id);
            if (unit == null) throw new InvalidOperationException("人物已失效");
            var context = CharacterReader.ContextKey(); var ensure = Guard(unit, context);
            var d = unit.data.dynUnitData;
            var fields = new[] { d.hp, d.mp, d.mood, d.health, d.energy, d.sp };
            var maxima = new[] { d.hpMax, d.mpMax, d.moodMax, d.healthMax, d.energyMax, d.spMax };
            var dataPointer = unit.data.Pointer; var dynamicPointer = d.Pointer;
            var fieldPointers = new IntPtr[fields.Length]; var maximumPointers = new IntPtr[maxima.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i] == null || maxima[i] == null) throw new InvalidOperationException("状态字段尚未就绪");
                fieldPointers[i] = fields[i].Pointer; maximumPointers[i] = maxima[i].Pointer;
            }
            var ensureTarget = ensure;
            ensure = () =>
            {
                ensureTarget(); var currentData = unit.data;
                if (currentData == null || currentData.Pointer != dataPointer || currentData.dynUnitData == null || currentData.dynUnitData.Pointer != dynamicPointer)
                    throw new InvalidOperationException("人物状态数据已被替换，停止恢复");
                var current = currentData.dynUnitData;
                var resources = new[] { current.hp, current.mp, current.mood, current.health, current.energy, current.sp };
                var limits = new[] { current.hpMax, current.mpMax, current.moodMax, current.healthMax, current.energyMax, current.spMax };
                for (int i = 0; i < resources.Length; i++)
                    if (resources[i] == null || resources[i].Pointer != fieldPointers[i] || limits[i] == null || limits[i].Pointer != maximumPointers[i])
                        throw new InvalidOperationException("状态或上限字段已被替换，停止恢复");
            };
            ensure();
            var old = new int[fields.Length]; var targets = new int[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                var f = fields[i]; old[i] = f.baseValue;
                if (f.lockValue || f.onBaseValueSetCall == null || f.allMulCall.Count > 0 || f.allMulAddCall.Count > 0 || f.allEqualCall.Count > 0)
                    throw new InvalidOperationException("当前效果限制了状态恢复");
                targets[i] = checked((int)((long)f.baseValue + maxima[i].value - f.value));
                if (targets[i] < f.minValue || targets[i] > f.maxValue) throw new InvalidOperationException("状态恢复超出游戏允许范围");
            }
            int applied = 0;
            try
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    ensure(); applied = i + 1; fields[i].baseValue = targets[i]; ensure();
                    if (fields[i].value != maxima[i].value) throw new InvalidOperationException("状态恢复回读不一致");
                }
                // 后续setter/回调也可能改动已核对的资源，完成整批后再确认全部目标。
                ensure();
                for (int i = 0; i < fields.Length; i++)
                    if (fields[i].value != maxima[i].value) throw new InvalidOperationException("状态恢复整批回读不一致");
            }
            catch
            {
                bool restored = true;
                for (int i = applied - 1; i >= 0; i--)
                    try { ensure(); fields[i].baseValue = old[i]; ensure(); if (fields[i].baseValue != old[i]) restored = false; } catch { restored = false; }
                try { ensure(); for (int i = 0; i < fields.Length; i++) if (fields[i].baseValue != old[i]) restored = false; }
                catch { restored = false; }
                if (!restored)
                {
                    MarkDirty(context);
                    if (CharacterReader.ContextKey() != context) throw new InvalidOperationException("存档已切换，已停止状态恢复；原人物可能部分改变，请核对");
                    throw new InvalidOperationException("部分状态未能恢复原值，请核对人物状态后保存");
                }
                throw;
            }
            ensure(); for (int i = 0; i < fields.Length; i++) if (old[i] != targets[i]) { MarkDirty(context); break; }
            return "体力、灵力、心情、健康、精力、念力已恢复至当前上限；请点击保存";
        }

        // 满修为：累计经验阈值走AddExp，原生瓶颈气运/经验事件在此入口触发；不直接赋exp、不改境界。
        private static string FillCultivation(string id)
        {
            var unit = CharacterReader.Resolve(id);
            if (unit == null) throw new InvalidOperationException("人物已失效");
            var context = CharacterReader.ContextKey(); var ensure = Guard(unit, context);
            var data = unit.data; var info = data.unitData; var property = info.propertyData; var dynamic = data.dynUnitData;
            var dataPointer = data.Pointer; var infoPointer = info.Pointer; var propertyPointer = property.Pointer;
            var dynamicPointer = dynamic.Pointer; var gradePointer = dynamic.gradeID.Pointer; var gradeId = dynamic.gradeID.value;
            var ensureTarget = ensure;
            ensure = () =>
            {
                ensureTarget(); var current = unit.data;
                if (current == null || current.Pointer != dataPointer || current.unitData == null || current.unitData.Pointer != infoPointer ||
                    current.unitData.propertyData == null || current.unitData.propertyData.Pointer != propertyPointer ||
                    current.dynUnitData == null || current.dynUnitData.Pointer != dynamicPointer || current.dynUnitData.gradeID == null ||
                    current.dynUnitData.gradeID.Pointer != gradePointer || current.dynUnitData.gradeID.value != gradeId)
                    throw new InvalidOperationException("人物修为数据或境界已被替换或改变，停止操作");
            };
            ensure();
            var next = g.conf.roleGrade.GetNextGradeItem(gradeId);
            if (next == null) throw new InvalidOperationException("已到最高阶段");
            var delta = checked(next.exp - unit.data.unitData.propertyData.exp);
            var beforeExp = unit.data.unitData.propertyData.exp;
            if (delta > 0)
            {
                try { ensure(); unit.data.unitData.propertyData.AddExp(delta); }
                finally
                {
                    // 无法重新确认对象时不读旧引用；同存档保守保留可能已生效的操作。
                    try { ensure(); if (unit.data.unitData.propertyData.exp != beforeExp) MarkDirty(context); }
                    catch { MarkDirty(context); }
                }
            }
            ensure();
            if (unit.data.unitData.propertyData.exp < next.exp) throw new InvalidOperationException("当前效果限制了经验获取，请核对修为");
            return "修为已满，可按游戏流程突破；请点击保存";
        }

        // 满材料：只补当前人物现有可叠加物品，不新增记录、也不降低已有更大数量。
        private static string FillMaterials(WorldUnitBase unit)
        {
            if (unit == null) throw new InvalidOperationException("人物已失效");
            var context = CharacterReader.ContextKey(); var ensure = ItemInventory.Guard(context, unit);
            var bag = unit.data.unitData.propData;
            var changes = new List<DataProps.PropsData>();
            for (int i = 0; i < bag.allProps.Count; i++)
            {
                var item = bag.allProps[i];
                if (item != null && item.propsItem != null && item.propsItem.isOverlay == 1 && item.propsCount > 0 && item.propsCount < 9999) changes.Add(item);
            }
            var old = new List<int>(); foreach (var item in changes) old.Add(item.propsCount);
            Action<DataProps.PropsData> requireRecord = item =>
            {
                ensure(); bool found = false;
                foreach (var current in bag.allProps) if (current != null && current.Pointer == item.Pointer) { found = true; break; }
                if (!found) throw new InvalidOperationException("原物品记录已被移除或替换，停止补满");
            };
            // 自定义数量可超过9999；补小堆之前核对同类总量，避免原生整数统计溢出。
            var changedIds = new HashSet<int>(); foreach (var item in changes) changedIds.Add(item.propsID);
            var totals = new Dictionary<int, long>();
            foreach (var item in bag.allProps)
            {
                if (item == null || item.propsCount <= 0 || !changedIds.Contains(item.propsID)) continue;
                long total; totals.TryGetValue(item.propsID, out total);
                totals[item.propsID] = total + (item.propsItem != null && item.propsItem.isOverlay == 1 && item.propsCount < 9999 ? 9999 : item.propsCount);
            }
            foreach (var total in totals.Values)
                if (total > int.MaxValue) throw new InvalidOperationException("补满后同类物品总数量超过2147483647，未修改；请先调低已有大数量");
            int applied = 0;
            try
            {
                for (int i = 0; i < changes.Count; i++)
                {
                    requireRecord(changes[i]);
                    if (changes[i].propsCount != old[i]) throw new InvalidOperationException("待补满数量已变化，停止后续操作");
                    applied = i + 1; changes[i].propsCount = 9999; requireRecord(changes[i]);
                    if (changes[i].propsCount != 9999) throw new InvalidOperationException("数量回读不一致");
                }
                ensure(); bag.UpdateAllShowProps(); ensure();
                foreach (var item in changes) { requireRecord(item); if (item.propsCount != 9999) throw new InvalidOperationException("补满整批数量回读不一致"); }
            }
            catch
            {
                bool restored = true;
                for (int i = applied - 1; i >= 0; i--)
                    try { requireRecord(changes[i]); changes[i].propsCount = old[i]; requireRecord(changes[i]); if (changes[i].propsCount != old[i]) restored = false; } catch { restored = false; }
                try { ensure(); bag.UpdateAllShowProps(); ensure(); } catch { restored = false; }
                try { foreach (var item in changes) requireRecord(item); for (int i = 0; i < changes.Count; i++) if (changes[i].propsCount != old[i]) restored = false; }
                catch { restored = false; }
                if (!restored)
                {
                    MarkDirty(context);
                    if (CharacterReader.ContextKey() != context) throw new InvalidOperationException("存档已切换，已停止补满；原背包可能部分改变，请核对");
                    throw new InvalidOperationException("部分背包数量未能恢复，请核对背包");
                }
                throw;
            }
            ensure(); if (changes.Count > 0) MarkDirty(context);
            return "背包中 " + changes.Count + " 项可叠加物品已补至9999；不可叠加物品保持原样；请点击保存";
        }

        // 双向好感度：SetIntim维护原生关系缓存；叠加原始浮点值，避免GetIntim整数化损失。失败逆序回退并核对。
        private static Action GuardRelation(WorldUnitBase unit, string context)
        {
            var ensureTarget = Guard(unit, context);
            var data = unit.data; var info = data.unitData; var relation = info.relationData;
            if (relation == null) throw new InvalidOperationException("人物关系尚未就绪");
            var dataPointer = data.Pointer; var infoPointer = info.Pointer; var relationPointer = relation.Pointer;
            Action ensure = () =>
            {
                ensureTarget(); var current = unit.data;
                if (current == null || current.Pointer != dataPointer || current.unitData == null || current.unitData.Pointer != infoPointer ||
                    current.unitData.relationData == null || current.unitData.relationData.Pointer != relationPointer)
                    throw new InvalidOperationException("人物关系数据已被替换，停止修改");
            };
            ensure(); return ensure;
        }
        // 本机SetIntim在存值前执行Single.ToString("0.##")再Parse；核对原生量化结果。
        internal static float NormalizeIntim(float value)
        { return float.Parse(value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture); }
        private static string AddIntimacy(WorldUnitBase player, WorldUnitBase npc, string id, string amount)
        {
            int add;
            if (!int.TryParse(amount, out add) || add <= 0 || add > 100000) throw new InvalidOperationException("请输入1–100000的好感度增量");
            var context = CharacterReader.ContextKey();
            var ensurePlayer = GuardRelation(player, context); var ensureNpc = GuardRelation(npc, context);
            Action ensure = () => { ensurePlayer(); ensureNpc(); };
            var playerId = player.data.unitData.unitID;
            var a = player.data.unitData.relationData; var b = npc.data.unitData.relationData;
            float beforeA = RawIntim(a, id), beforeB = b.intimToPlayerUnit;
            float nextA = NormalizeIntim(beforeA + add), nextB = NormalizeIntim(beforeB + add);
            if (float.IsNaN(nextA) || float.IsInfinity(nextA) || float.IsNaN(nextB) || float.IsInfinity(nextB) || nextA <= beforeA || nextB <= beforeB || nextA >= 2147483648f || nextB >= 2147483648f || nextA <= -2147483648f || nextB <= -2147483648f)
                throw new InvalidOperationException("原好感度异常或增量超出可表示范围，未修改");
            int applied = 0;
            try
            {
                ensure(); applied = 1; a.SetIntim(id, nextA); ensure();
                var actualA = RawIntim(a, id);
                if (actualA != nextA || b.intimToPlayerUnit != beforeB) throw new InvalidOperationException("第一侧好感或反向基准回读不一致");
                applied = 2; b.SetIntim(playerId, nextB); ensure();
                var actualB = b.intimToPlayerUnit;
                if (actualB != nextB || RawIntim(a, id) != nextA) throw new InvalidOperationException("双向好感结果与目标不一致");
            }
            catch
            {
                bool restored = true;
                if (applied >= 2) try { ensure(); b.SetIntim(playerId, beforeB); ensure(); } catch { restored = false; }
                if (applied >= 1) try { ensure(); a.SetIntim(id, beforeA); ensure(); } catch { restored = false; }
                try { ensure(); if (RawIntim(a, id) != beforeA || b.intimToPlayerUnit != beforeB) restored = false; } catch { restored = false; }
                if (!restored)
                {
                    MarkDirty(context);
                    if (CharacterReader.ContextKey() != context) throw new InvalidOperationException("存档已切换，已停止关系修改；原人物关系可能部分改变，请核对");
                    throw new InvalidOperationException("双向好感度部分应用，请核对双方关系");
                }
                throw;
            }
            ensure(); MarkDirty(context);
            return "双向好感度已叠加：玩家→NPC " + beforeA + "→" + RawIntim(a, id) + "，NPC→玩家 " + beforeB + "→" + b.intimToPlayerUnit + "；请点击保存";
        }

        private static WorldUnitBase ReadMoveTarget(string context, string id, IntPtr pointer)
        {
            if (string.IsNullOrEmpty(context) || CharacterReader.ContextKey() != context) return null;
            var current = CharacterReader.Resolve(id);
            if (current == null || current.Pointer != pointer || current.data.unitData.unitID != id) return null;
            return current;
        }

        // 去/来/返：走原生UnitActionSetPoint；“来”保存首次原位置，“返”成功后清除，记录限当前进程。
        private static string Move(WorldUnitBase player, WorldUnitBase npc, string id, string action)
        {
            var context = CharacterReader.ContextKey();
            var ensurePlayer = Guard(player, context); var ensureNpc = Guard(npc, context);
            Action ensure = () => { ensurePlayer(); ensureNpc(); };
            if (context != returnContext) { returnPoints.Clear(); returnOwners.Clear(); returnContext = context; }
            IntPtr previousOwner;
            if (action == "来" && returnOwners.TryGetValue(id, out previousOwner) && previousOwner != npc.Pointer)
            { returnPoints.Remove(id); returnOwners.Remove(id); }
            Vector2Int destination;
            var moving = action == "去" ? player : npc;
            if (action == "返") { if (!returnOwners.ContainsKey(id) || returnOwners[id] != npc.Pointer || !returnPoints.TryGetValue(id, out destination)) throw new InvalidOperationException("该人物尚未通过“来”召回"); }
            else destination = action == "去" ? npc.data.unitData.GetPoint() : player.data.unitData.GetPoint();
            var movingId = moving.data.unitData.unitID; var movingPointer = moving.Pointer;
            var origin = moving.data.unitData.GetPoint();
            int result;
            try { ensure(); result = moving.CreateAction(new UnitActionSetPoint(destination), false); }
            finally
            {
                try
                {
                    // Autosave forbids more writes, but must not discard the observed return origin.
                    var current = ReadMoveTarget(context, movingId, movingPointer);
                    if (current == null) MarkDirty(context);
                    else
                    {
                        var point = current.data.unitData.GetPoint();
                        if (ReadMoveTarget(context, movingId, movingPointer) == null) MarkDirty(context);
                        else if (point != origin)
                        {
                            if (action == "来" && returnContext == context && !returnPoints.ContainsKey(id))
                            { returnPoints.Add(id, origin); returnOwners[id] = movingPointer; }
                            MarkDirty(context);
                        }
                    }
                }
                catch { MarkDirty(context); }
            }
            ensure();
            if (result != 0 || moving.data.unitData.GetPoint() != destination) throw new InvalidOperationException("游戏拒绝传送（" + result + "），请核对当前位置");
            if (action == "返") { returnPoints.Remove(id); returnOwners.Remove(id); }
            var message = "已完成“" + action + "”；请点击保存";
            try { RefreshMap(); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 传送后的地图显示刷新失败：" + ex); message += "（地图显示刷新失败，请重新进入地图核对）"; }
            return message;
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
