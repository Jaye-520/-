using System;
using System.Collections.Generic;
using System.Text;

namespace MOD_YtxSQ1.Services
{
    internal sealed class RelationRemoval
    {
        internal readonly List<RelationPair> Steps = new List<RelationPair>();
        internal readonly List<RelationPair> Guards = new List<RelationPair>();
        internal string Description;
    }
    internal static class RelationOperations
    {
        private static DataUnit.RelationData Relations(string id) { return CharacterReader.Resolve(id).data.unitData.relationData; }
        private static bool SharedParent(string a, string b, string excluding = null)
        {
            var pa = Relations(a).parent; var pb = Relations(b).parent;
            for (int i = 0; i < pa.Length; i++)
            {
                var p = pa[i];
                if (!string.IsNullOrEmpty(p) && p != excluding)
                    for (int j = 0; j < pb.Length; j++) if (p == pb[j]) return true;
            }
            return false;
        }
        private static void Ready(string id)
        {
            var r = Relations(id);
            if (r.parent == null || r.parent.Length != 2 || r.parent[0] == id || r.parent[1] == id || (!string.IsNullOrEmpty(r.parent[0]) && r.parent[0] == r.parent[1]))
                throw new InvalidOperationException("父母槽异常，拒绝修改家谱");
            foreach (var list in new[] { r.children, r.childrenPrivate, r.brother, r.parentBack, r.childrenBack, r.brotherBack, r.lover, r.master, r.student })
            {
                if (list == null) throw new InvalidOperationException("关系容器未就绪");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var value in list) if (string.IsNullOrEmpty(value) || value == id || !seen.Add(value))
                    throw new InvalidOperationException("关系记录存在空项、自关联或重复，拒绝自动修复");
            }
        }
        private static void Reciprocal(RelationPair pair)
        {
            Ready(pair.A); Ready(pair.B);
            var reverse = CharacterRelations.Pair(pair.Context, pair.B, pair.A);
            Func<List<int>, int, bool> has = (types, t) => t == 1 ? types.Contains(2) || types.Contains(3) : types.Contains(CharacterRelations.Reverse(t));
            foreach (var t in pair.Types) if (!has(reverse.Types, t)) throw new InvalidOperationException("双方身份记录不一致，请先核对家谱");
            foreach (var t in reverse.Types) if (!has(pair.Types, t)) throw new InvalidOperationException("反向身份记录不一致，请先核对家谱");
            if ((pair.Types.Contains(2) && pair.Types.Contains(3)) || (reverse.Types.Contains(2) && reverse.Types.Contains(3)))
                throw new InvalidOperationException("子女与私生记录冲突，拒绝自动修复");
        }
        private static void AddRemoval(RelationRemoval plan, RelationPair pair, bool relatedSibling = false)
        {
            Reciprocal(pair);
            var a = Relations(pair.A); var b = Relations(pair.B);
            if ((a.notRelieveUnitsID != null && a.notRelieveUnitsID.ContainsKey(pair.B)) || (b.notRelieveUnitsID != null && b.notRelieveUnitsID.ContainsKey(pair.A)))
                throw new InvalidOperationException("涉及剧情保护关系，整次解除已拒绝");
            if (pair.Types.Count == 0) throw new InvalidOperationException("双方没有可解除的身份关系");
            if (relatedSibling && (pair.Types.Count != 1 || pair.Types[0] != 4))
                throw new InvalidOperationException("连带同胞存在其他身份，不能隐式解除；请先处理该人物关系");
            plan.Steps.Add(pair); plan.Guards.Add(pair);
        }
        internal static RelationRemoval PlanRemoval(RelationPair expected)
        {
            Ensure(expected, true);
            var plan = new RelationRemoval(); AddRemoval(plan, expected);
            bool blood = expected.Types.Exists(t => t <= 4);
            if (blood && expected.Types.Count != 1) throw new InvalidOperationException("血亲存在多重身份冲突，拒绝自动重建家谱");
            int type = expected.Types[0];
            if (type == 4 && SharedParent(expected.A, expected.B))
                throw new InvalidOperationException("双方仍共有父母；请从父母—子女关系解除，系统会同步整理同胞，避免留下矛盾家谱");
            if (type >= 1 && type <= 3)
            {
                string child = type == 1 ? expected.A : expected.B, parent = type == 1 ? expected.B : expected.A;
                var ids = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var id in Relations(parent).children) ids.Add(id);
                foreach (var id in Relations(parent).childrenPrivate) ids.Add(id);
                foreach (var id in Relations(child).brother) ids.Add(id);
                ids.Remove(child);
                foreach (var id in ids)
                {
                    // Bind retained siblings too: a changed second parent must invalidate the confirmation.
                    var guard = CharacterRelations.Pair(expected.Context, child, id); Reciprocal(guard); plan.Guards.Add(guard);
                    var parentPair = CharacterRelations.Pair(expected.Context, id, parent); Reciprocal(parentPair); plan.Guards.Add(parentPair);
                    bool linked = Relations(parent).children.Contains(id) || Relations(parent).childrenPrivate.Contains(id);
                    var slots = Relations(id).parent; bool slot = slots[0] == parent || slots[1] == parent;
                    if (linked != slot) throw new InvalidOperationException("关联子女与父母槽不一致，整次解除已拒绝");
                    if (guard.Types.Contains(4) && !SharedParent(child, id)) throw new InvalidOperationException("已有同胞缺少共同父母依据，拒绝自动修复");
                    if (slot && guard.Types.Contains(4) && !SharedParent(child, id, parent)) AddRemoval(plan, guard, true);
                }
            }
            var text = new StringBuilder("将双向解除以下身份：");
            foreach (var step in plan.Steps)
            {
                text.Append("\n").Append(step.NameA).Append(" ↔ ").Append(step.NameB).Append("：");
                for (int i = 0; i < step.Types.Count; i++) text.Append(i == 0 ? "" : "、").Append(CharacterRelations.TypeName(step.Types[i]));
            }
            text.Append("\n\n保留另一位父母、仍共有父母的同胞及各自后代；保留姓名与好感。原生动作可能改变行为与声望。");
            plan.Description = text.ToString(); return plan;
        }
        internal static string RemoveRelations(RelationRemoval plan)
        {
            if (plan == null || plan.Steps.Count == 0) throw new InvalidOperationException("解除计划为空");
            var states = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var guard in plan.Guards)
            {
                Ensure(guard, true); states[guard.A] = guard.StateA; states[guard.B] = guard.StateB;
            }
            Action validate = () =>
            {
                foreach (var guard in plan.Guards) Ensure(guard);
                foreach (var state in states) if (CharacterRelations.State(Relations(state.Key)) != state.Value)
                    throw new InvalidOperationException("关联人物关系已变化，停止解除，请重新核对");
                foreach (var guard in plan.Guards)
                    if (CharacterRelations.Raw(Relations(guard.A), guard.B) != guard.AB || CharacterRelations.Raw(Relations(guard.B), guard.A) != guard.BA)
                        throw new InvalidOperationException("关联人物好感已变化，停止解除，请重新核对");
            };
            // Check every native action before the first mutation, then repeat at the actual step.
            foreach (var step in plan.Steps)
            {
                validate(); int check = CharacterReader.Resolve(step.A).IsCreateAction(new UnitActionRelationSet(CharacterReader.Resolve(step.B), UnitRelationType.None, 0), false);
                validate(); if (check != 0) throw new InvalidOperationException("原生解除检查拒绝，原因码 " + check);
            }
            CharacterSave.Sync(plan.Steps[0].Context); bool attempted = false;
            try
            {
                foreach (var step in plan.Steps)
                {
                    validate(); var actor = CharacterReader.Resolve(step.A);
                    var action = new UnitActionRelationSet(CharacterReader.Resolve(step.B), UnitRelationType.None, 0);
                    int check = actor.IsCreateAction(action, false); validate();
                    if (check != 0) throw new InvalidOperationException("原生解除检查拒绝，原因码 " + check);
                    states[step.A] = CharacterRelations.State(Relations(step.A), step.B);
                    states[step.B] = CharacterRelations.State(Relations(step.B), step.A);
                    attempted = true; int result = actor.CreateAction(action, false); validate();
                    if (result != 0) throw new InvalidOperationException("原生解除结果异常，原因码 " + result);
                }
                validate(); CharacterSave.MarkDirty(plan.Steps[0].Context);
                return "已完成双向关系解除（" + plan.Steps.Count + " 组），关联家谱已核对；请点击保存";
            }
            catch (Exception ex)
            {
                if (!attempted) throw;
                CharacterSave.MarkDirty(plan.Steps[0].Context);
                throw new InvalidOperationException("解除可能部分完成，已保留待保存状态；停止后续修改，请核对家谱，不要重复提交：" + ex.Message, ex);
            }
        }
        private static void Ensure(RelationPair expected, bool values = false)
        {
            if (CharacterSave.Busy || DataMgr.isSaveData) throw new InvalidOperationException("正在保存，请稍候");
            CharacterSave.RequireCurrentTarget(expected.Context, expected.A, expected.UnitA);
            CharacterSave.RequireCurrentTarget(expected.Context, expected.B, expected.UnitB);
            var a = CharacterReader.Resolve(expected.A).data; var b = CharacterReader.Resolve(expected.B).data;
            if (a.Pointer != expected.DataA || b.Pointer != expected.DataB || a.unitData.Pointer != expected.InfoA || b.unitData.Pointer != expected.InfoB ||
                a.unitData.relationData == null || b.unitData.relationData == null || a.unitData.relationData.Pointer != expected.RelationA || b.unitData.relationData.Pointer != expected.RelationB)
                throw new InvalidOperationException("人物关系数据已被替换，请重新操作");
            if (values && (CharacterRelations.State(a.unitData.relationData) != expected.StateA || CharacterRelations.State(b.unitData.relationData) != expected.StateB ||
                CharacterRelations.Raw(a.unitData.relationData, expected.B) != expected.AB || CharacterRelations.Raw(b.unitData.relationData, expected.A) != expected.BA))
                throw new InvalidOperationException("双方关系或好感已变化，请重新打开操作面板");
        }
        internal static string SetAffinity(RelationPair expected, float ab, float ba)
        {
            if (!CharacterRelations.EditableAffinity(ab) || !CharacterRelations.EditableAffinity(ba)) throw new InvalidOperationException("好感须大于-2147483648、小于2147483648，可输入小数");
            Ensure(expected, true); CharacterSave.Sync(expected.Context);
            ab = CharacterActions.NormalizeIntim(ab); ba = CharacterActions.NormalizeIntim(ba);
            if (ab == expected.AB && ba == expected.BA) return "好感未变化（原生最多保留两位小数）";
            var a = CharacterReader.Resolve(expected.A).data.unitData.relationData; var b = CharacterReader.Resolve(expected.B).data.unitData.relationData;
            bool appliedA = false, appliedB = false;
            try
            {
                if (ab != expected.AB) { Ensure(expected); appliedA = true; a.SetIntim(expected.B, ab); Ensure(expected); }
                if (CharacterRelations.State(a) != expected.StateA || CharacterRelations.State(b) != expected.StateB || CharacterRelations.Raw(a, expected.B) != ab || CharacterRelations.Raw(b, expected.A) != expected.BA)
                    throw new InvalidOperationException("原生回调期间双方关系已变化，停止第二侧修改");
                if (ba != expected.BA) { Ensure(expected); appliedB = true; b.SetIntim(expected.A, ba); Ensure(expected); }
                if (CharacterRelations.State(a) != expected.StateA || CharacterRelations.State(b) != expected.StateB)
                    throw new InvalidOperationException("好感回调期间身份关系已变化，请核对双方");
                var actualA = CharacterRelations.Raw(a, expected.B); var actualB = CharacterRelations.Raw(b, expected.A);
                if (actualA != ab || actualB != ba)
                    throw new InvalidOperationException("原生好感结果与目标不一致");
                if (appliedA || appliedB) CharacterSave.MarkDirty(expected.Context);
                return "好感 " + expected.NameA + "→" + expected.NameB + " " + CharacterRelations.Number(actualA) + "；反向 " + CharacterRelations.Number(actualB) + "；请点击保存";
            }
            catch (Exception ex)
            {
                bool restored = true;
                if (appliedB) try { Ensure(expected); b.SetIntim(expected.A, expected.BA); Ensure(expected); } catch { restored = false; }
                if (appliedA) try { Ensure(expected); a.SetIntim(expected.B, expected.AB); Ensure(expected); } catch { restored = false; }
                try { Ensure(expected); restored &= CharacterRelations.Raw(a, expected.B) == expected.AB && CharacterRelations.Raw(b, expected.A) == expected.BA && CharacterRelations.State(a) == expected.StateA && CharacterRelations.State(b) == expected.StateB; } catch { restored = false; }
                if (!restored) { CharacterSave.MarkDirty(expected.Context); throw new InvalidOperationException("好感可能部分改变，已标记待保存；请核对双方，不要重复提交", ex); }
                throw;
            }
        }
        internal static string Unavailable(RelationPair pair, int type, bool remove)
        {
            if (remove)
            {
                try { PlanRemoval(pair); return null; } catch (Exception ex) { return ex.Message; }
            }
            if (pair.Types.Contains(type)) return "此关系已经存在";
            if (pair.Types.Count > 0) return "双方已有身份关系，请先明确解除原关系";
            var reverse = CharacterRelations.Pair(pair.Context, pair.B, pair.A);
            if (reverse.Types.Count > 0) return "反向已有身份关系，请先核对双方";
            var a = CharacterReader.Resolve(pair.A); var b = CharacterReader.Resolve(pair.B);
            if (type == 8 && (!string.IsNullOrEmpty(a.data.unitData.relationData.married) || !string.IsNullOrEmpty(b.data.unitData.relationData.married))) return "一方已有配偶，不自动替换";
            if (type == 1 || type == 2)
            {
                var child = type == 1 ? a : b; var parent = type == 1 ? b : a;
                var parents = child.data.unitData.relationData.parent;
                if (parents == null || parents.Length != 2) return "原生父母槽未就绪";
                for (int i = 0; i < parents.Length; i++) if (!string.IsNullOrEmpty(parents[i])) return "已有血亲父母，不自动替换或重建家谱";
                if (parent.data.unitData.propertyData.sex != UnitSexType.Man && parent.data.unitData.propertyData.sex != UnitSexType.Woman) return "父母性别未确认";
                // Do not introduce a descendant as a parent, even when native prechecks miss it.
                var pending = new System.Collections.Generic.Queue<string>(); var seen = new System.Collections.Generic.HashSet<string>(); pending.Enqueue(parent.data.unitData.unitID);
                while (pending.Count > 0)
                {
                    var id = pending.Dequeue(); if (!seen.Add(id)) continue;
                    if (id == child.data.unitData.unitID) return "不能形成血亲循环";
                    foreach (var edge in CharacterRelations.Direct(id)) if (edge.Type == 1) pending.Enqueue(edge.To);
                }
            }
            return null;
        }
        internal static string SetRelation(RelationPair expected, int type, bool remove)
        {
            if (remove) return RemoveRelations(PlanRemoval(expected));
            if (Array.IndexOf(CharacterRelations.EditableTypes, type) < 0) throw new InvalidOperationException("此关系尚未开放编辑");
            Ensure(expected, true); var unavailable = Unavailable(expected, type, remove);
            if (unavailable != null) throw new InvalidOperationException(unavailable);
            var a = CharacterReader.Resolve(expected.A); var b = CharacterReader.Resolve(expected.B);
            UnitActionBase action; WorldUnitBase actor = a;
            if (!remove && (type == 1 || type == 2))
            {
                var parent = type == 1 ? b : a; actor = type == 1 ? a : b;
                bool man = parent.data.unitData.propertyData.sex == UnitSexType.Man;
                action = new UnitActionRelationSetParent(man ? parent.data.unitData.unitID : "", man ? "" : parent.data.unitData.unitID, 0);
            }
            else action = new UnitActionRelationSet(b, remove ? UnitRelationType.None : (UnitRelationType)type, 0);
            Ensure(expected, true);
            int precheck = actor.IsCreateAction(action, false); Ensure(expected, true);
            if (precheck != 0) throw new InvalidOperationException("原生关系检查拒绝，原因码 " + precheck);
            CharacterSave.Sync(expected.Context); bool attempted = false;
            try
            {
                Ensure(expected, true); attempted = true;
                int result = actor.CreateAction(action, false); Ensure(expected);
                var actual = CharacterRelations.Pair(expected.Context, expected.A, expected.B);
                var back = CharacterRelations.Pair(expected.Context, expected.B, expected.A);
                if (result != 0 || (remove ? actual.Types.Contains(type) || back.Types.Contains(CharacterRelations.Reverse(type)) :
                    !actual.Types.Contains(type) || !back.Types.Contains(CharacterRelations.Reverse(type))))
                    throw new InvalidOperationException("原生关系动作结果不一致，原因码 " + result);
                CharacterSave.MarkDirty(expected.Context);
                return (remove ? "已解除 " : "已建立 ") + expected.NameA + " ↔ " + expected.NameB + " · " + CharacterRelations.TypeName(type) + "；请点击保存";
            }
            catch (Exception ex)
            {
                if (attempted) CharacterSave.MarkDirty(expected.Context);
                throw new InvalidOperationException("关系动作可能已部分应用，已保留待保存状态，请核对后再操作：" + ex.Message);
            }
        }
    }
}
