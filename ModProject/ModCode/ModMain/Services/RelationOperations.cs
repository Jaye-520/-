using System;

namespace MOD_YtxSQ1.Services
{
    internal static class RelationOperations
    {
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
            var a = CharacterReader.Resolve(expected.A).data.unitData.relationData; var b = CharacterReader.Resolve(expected.B).data.unitData.relationData;
            bool appliedA = false, appliedB = false;
            try
            {
                if (ab != expected.AB) { Ensure(expected); appliedA = true; a.SetIntim(expected.B, ab); Ensure(expected); }
                if (CharacterRelations.State(a) != expected.StateA || CharacterRelations.State(b) != expected.StateB || CharacterRelations.Raw(b, expected.A) != expected.BA)
                    throw new InvalidOperationException("原生回调期间双方关系已变化，停止第二侧修改");
                if (ba != expected.BA) { Ensure(expected); appliedB = true; b.SetIntim(expected.A, ba); Ensure(expected); }
                if (CharacterRelations.State(a) != expected.StateA || CharacterRelations.State(b) != expected.StateB)
                    throw new InvalidOperationException("好感回调期间身份关系已变化，请核对双方");
                var actualA = CharacterRelations.Raw(a, expected.B); var actualB = CharacterRelations.Raw(b, expected.A);
                if (!CharacterRelations.Finite(actualA) || !CharacterRelations.Finite(actualB) ||
                    (ab != expected.AB && actualA == expected.AB) || (ba != expected.BA && actualB == expected.BA))
                    throw new InvalidOperationException("原生好感修改未生效");
                if (appliedA || appliedB) CharacterSave.MarkDirty(expected.Context);
                return "好感 " + expected.NameA + "→" + expected.NameB + " " + CharacterRelations.Number(actualA) + "；反向 " + CharacterRelations.Number(actualB) + "；请点击保存";
            }
            catch
            {
                bool restored = true;
                if (appliedB) try { Ensure(expected); b.SetIntim(expected.A, expected.BA); Ensure(expected); } catch { restored = false; }
                if (appliedA) try { Ensure(expected); a.SetIntim(expected.B, expected.AB); Ensure(expected); } catch { restored = false; }
                try { Ensure(expected); restored &= CharacterRelations.Raw(a, expected.B) == expected.AB && CharacterRelations.Raw(b, expected.A) == expected.BA && CharacterRelations.State(a) == expected.StateA && CharacterRelations.State(b) == expected.StateB; } catch { restored = false; }
                if (!restored) { CharacterSave.MarkDirty(expected.Context); throw new InvalidOperationException("好感可能部分改变，已标记待保存；请核对双方，不要重复提交"); }
                throw;
            }
        }
        internal static string Unavailable(RelationPair pair, int type, bool remove)
        {
            if (remove)
            {
                var protectedA = CharacterReader.Resolve(pair.A).data.unitData.relationData; var protectedB = CharacterReader.Resolve(pair.B).data.unitData.relationData;
                if ((protectedA.notRelieveUnitsID != null && protectedA.notRelieveUnitsID.ContainsKey(pair.B)) || (protectedB.notRelieveUnitsID != null && protectedB.notRelieveUnitsID.ContainsKey(pair.A))) return "此关系受游戏剧情保护，不能解除";
                if (!pair.Types.Contains(type)) return "双方没有此身份关系";
                if (type <= 4) return "血亲解除暂未开放：尚无可靠的单项家谱解除路径";
                if (pair.Types.Count != 1) return "双方有多重身份，原生解除会清理多项，暂不开放";
                var reversePair = CharacterRelations.Pair(pair.Context, pair.B, pair.A);
                if (reversePair.Types.Count != 1 || reversePair.Types[0] != CharacterRelations.Reverse(type)) return "双方身份记录不一致，暂不开放解除";
                return null;
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
