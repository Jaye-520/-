using System;
using System.Collections.Generic;

namespace MOD_YtxSQ1.Services
{
    internal sealed class MartialPrefixEntry
    {
        internal int Index, Number, Value, Maximum, Quality;
        internal string Description, Unavailable, CatalogKey;
        internal bool Unlocked;
    }
    internal static partial class CharacterMartials
    {
        internal static int MaximumExp(WorldUnitBase unit, DataProps.MartialData martial)
        {
            var items = g.conf.battleSkillMastery.GetItems(unit, martial);
            if (items == null || items.Count == 0) throw new InvalidOperationException("修炼上限无法读取");
            return g.conf.battleSkillMastery.GetExp(items[items.Count - 1], martial.grade);
        }
        internal static List<MartialEntry> ReadBooks(string unitId)
        {
            var unit = CharacterReader.Resolve(unitId);
            if (unit == null) throw new InvalidOperationException("当前人物已失效");
            var result = new List<MartialEntry>();
            foreach (var props in unit.data.unitData.propData.allProps)
            {
                if (props == null) continue;
                IntPtr readPointer = IntPtr.Zero;
                try
                {
                    readPointer = props.Pointer;
                    if (props.propsType != DataProps.PropsDataType.Martial) continue;
                    var entry = ReadMartialEntry(unit, props, MartialType.None, 0, true);
                    entry.ReadPointer = readPointer; result.Add(entry);
                }
                catch (Exception ex)
                {
                    var unread = UnreadMartialEntry(MartialType.None, true);
                    unread.Name = "物品类型未读取";
                    unread.ReadError = ex.Message; unread.ReadPointer = readPointer;
                    result.Add(unread);
                }
            }
            return result;
        }
        private static MartialEntry UnreadMartialEntry(MartialType type, bool inBag)
        {
            return new MartialEntry { Type = type, InBag = inBag, Name = "功法记录未读取", School = "",
                Category = type == MartialType.SkillLeft || type == MartialType.None ? 0 : type == MartialType.SkillRight ? 1 : type == MartialType.Step ? 2 : type == MartialType.Ability ? 3 : 4 };
        }
        private static MartialEntry ReadMartialEntry(WorldUnitBase unit, DataProps.PropsData props, MartialType type, int exp, bool inBag)
        {
            var entry = new MartialEntry { Type = type, Exp = exp, InBag = inBag,
                Category = type == MartialType.SkillLeft ? 0 : type == MartialType.SkillRight ? 1 : type == MartialType.Step ? 2 : type == MartialType.Ability ? 3 : 4,
                Name = "功法数据异常", School = "" };
            try
            {
                entry.SoleId = props.soleID;
                entry.Equipped = !inBag && IsEquipped(unit, entry.SoleId);
                var raw = props.GetValues();
                if (inBag && raw != null && raw.Length > 3)
                {
                    var rawType = (MartialType)raw[3];
                    if (rawType != MartialType.SkillLeft && rawType != MartialType.SkillRight && rawType != MartialType.Step && rawType != MartialType.Ability && rawType != MartialType.Ultimate)
                        throw new InvalidOperationException("功法类别无效");
                    type = entry.Type = rawType;
                    entry.Category = type == MartialType.SkillLeft ? 0 : type == MartialType.SkillRight ? 1 : type == MartialType.Step ? 2 : type == MartialType.Ability ? 3 : 4;
                }
                if (raw != null && raw.Length > 4) entry.Id = raw[4];
                if (raw != null && raw.Length > 5) entry.Grade = raw[5];
                entry.Signature = props.soleIDHashData;
                if (string.IsNullOrEmpty(entry.SoleId) || string.IsNullOrEmpty(entry.Signature)) throw new InvalidOperationException("功法编号或签名缺失");
                ValidatePrefixLayout(props); var martial = props.To<DataProps.MartialData>();
                foreach (var prefix in martial.GetPrefixs()) if (prefix.prefixValueItem == null) throw new InvalidOperationException("词条配置缺失");
                var value = martial.martialInfo; entry.Name = Plain(value.name); entry.School = School(value.basType); entry.Level = value.level;
                entry.SuitId = type == MartialType.Ability ? props.To<DataProps.PropsAbilityData>().suitID : 0;
                entry.MaxExp = inBag ? 0 : MaximumExp(unit, martial);
            }
            catch (Exception ex) { entry.ReadError = ex.Message; }
            return entry;
        }
        private static DataProps.PropsData Record(WorldUnitBase unit, MartialEntry entry)
        {
            if (entry == null) throw new InvalidOperationException("请先选择功法");
            if (entry.ReadError != null) throw new InvalidOperationException("此功法数据异常：" + entry.ReadError);
            var props = entry.InBag ? unit.data.unitData.propData.GetProps(entry.SoleId) : unit.data.unitData.GetActionMartial(entry.SoleId)?.data;
            if (props == null || props.propsType != DataProps.PropsDataType.Martial || props.soleIDHashData != entry.Signature)
                throw new InvalidOperationException("这本功法已变化，请重新选择");
            ValidatePrefixLayout(props); return props;
        }
        private static DataUnit.ActionMartialData Learned(WorldUnitBase unit, MartialEntry entry)
        {
            if (entry == null) throw new InvalidOperationException("请先选择功法");
            if (entry.InBag) throw new InvalidOperationException("请先学习这本秘籍");
            Record(unit, entry);
            var action = unit.data.unitData.GetActionMartial(entry.SoleId);
            if (action.exp != entry.Exp || IsEquipped(unit, entry.SoleId) != entry.Equipped)
                throw new InvalidOperationException("修炼或装备状态已变化，请重新选择");
            return action;
        }
        private static bool IsEquipped(WorldUnitBase unit, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var data = unit.data.unitData;
            if (data.skillLeft == id || data.skillRight == id || data.step == id || data.ultimate == id) return true;
            if (data.abilitys != null) foreach (var slot in data.abilitys) if (slot == id) return true;
            return false;
        }
        private static Action GuardLearned(string context, WorldUnitBase unit, MartialEntry entry, DataUnit.ActionMartialData action)
        {
            var ensureTarget = ItemInventory.Guard(context, unit);
            var actionPointer = action.Pointer; var recordPointer = action.data.Pointer;
            Action ensure = () =>
            {
                ensureTarget(); var current = unit.data.unitData.GetActionMartial(entry.SoleId);
                if (current == null || current.Pointer != actionPointer || current.data == null || current.data.Pointer != recordPointer)
                    throw new InvalidOperationException("原功法记录已被替换，请重新选择");
            };
            ensure(); return ensure;
        }
        internal static int StudyTipCode(DataStruct<string, int> tip)
        {
            if (tip == null) throw new InvalidOperationException("学习条件检查未返回结果");
            // 本机Unhollower泛型值字段getter重复装箱，把已装箱对象头当整数读取。
            // 从实际返回对象的原生字段元数据取得偏移，不使用错误getter或固定布局。
            var field = UnhollowerBaseLib.IL2CPP.GetIl2CppField(UnhollowerBaseLib.IL2CPP.il2cpp_object_get_class(tip.Pointer), "t2");
            if (field == IntPtr.Zero) throw new InvalidOperationException("学习条件码字段不可读取");
            var offset = checked((int)UnhollowerBaseLib.IL2CPP.il2cpp_field_get_offset(field));
            return System.Runtime.InteropServices.Marshal.ReadInt32(IntPtr.Add(tip.Pointer, offset));
        }
        internal static string Study(string context, string unitId, MartialEntry entry)
        {
            var unit = ItemInventory.RequireTarget(context, unitId);
            var ensure = ItemInventory.Guard(context, unit);
            if (entry == null) throw new InvalidOperationException("请先选择功法");
            if (!entry.InBag) throw new InvalidOperationException("这本功法已经学会");
            var props = Record(unit, entry);
            if (props.propsCount != 1 || unit.data.unitData.GetActionMartial(entry.SoleId) != null)
                throw new InvalidOperationException("秘籍记录异常，未学习");
            var tip = UnitActionPropMartialStudy.GetStudyTip(unit, props.To<DataProps.MartialData>(), true, true, true);
            var tipCode = StudyTipCode(tip);
            if (!string.IsNullOrEmpty(tip.t1) || tipCode != 0)
                throw new InvalidOperationException(string.IsNullOrEmpty(tip.t1) ? "当前条件不允许学习（" + tipCode + "）" : Plain(tip.t1));
            // 原生学习记录复用秘籍对象；CostPropItem把其数量从1减为0后移出背包。
            // soleIDHashData包含数量，所以成功后的签名必须按数量0预先计算。
            var consumed = props.Clone(); consumed.propsCount = 0;
            var consumedSignature = consumed.soleIDHashData;
            ensure();
            if (Record(unit, entry).Pointer != props.Pointer)
                throw new InvalidOperationException("原秘籍对象已被替换，请重新选择");
            CharacterSave.Sync(context);
            try
            {
                ensure(); unit.data.unitData.StudyActionMartialProp(props); ensure();
                ensure();
                var learned = unit.data.unitData.GetActionMartial(entry.SoleId);
                if (learned == null || learned.data == null || learned.data.Pointer != props.Pointer || learned.data.soleIDHashData != consumedSignature || unit.data.unitData.propData.GetProps(entry.SoleId) != null)
                    throw new InvalidOperationException("学习结果未完整确认，请核对秘籍和已学功法，勿重复提交");
                var ensureLearned = GuardLearned(context, unit, entry, learned);
                unit.data.unitData.propData.UpdateAllShowProps(); ensureLearned(); unit.UpdateConf(); ensureLearned();
                if (learned.data.soleIDHashData != consumedSignature || unit.data.unitData.propData.GetProps(entry.SoleId) != null)
                    throw new InvalidOperationException("学习完成后的秘籍消耗或已学记录不一致，请核对，勿重复提交");
            }
            finally { CharacterSave.MarkDirty(context); }
            return "已学会“" + entry.Name + "”，消耗这本秘籍；未自动装备，请点击保存";
        }
        internal static string Complete(string context, string unitId, MartialEntry entry)
        {
            var unit = ItemInventory.RequireTarget(context, unitId); var action = Learned(unit, entry);
            var ensure = GuardLearned(context, unit, entry, action);
            var maximum = MaximumExp(unit, action.data.To<DataProps.MartialData>());
            if (maximum <= action.exp) return "这本功法已修炼圆满";
            if (action.exp < 0) throw new InvalidOperationException("修炼经验异常");
            CharacterSave.Sync(context);
            try
            {
                ensure(); unit.data.unitData.AddMartialExp(entry.SoleId, checked(maximum - action.exp));
                ensure();
                if (unit.data.unitData.GetActionMartial(entry.SoleId)?.exp != maximum)
                    throw new InvalidOperationException("修炼结果回读不一致，请核对功法");
                RefreshEffects(unit, entry, ensure);
                ensure();
                if (action.exp != maximum || IsEquipped(unit, entry.SoleId) != entry.Equipped)
                    throw new InvalidOperationException("效果刷新后的修炼或装备结果不一致，请核对功法");
            }
            finally { CharacterSave.MarkDirty(context); }
            return "这本功法已修炼圆满；请点击保存";
        }
        internal static string Equip(string context, string unitId, MartialEntry entry, int index, string expectedSlot)
        {
            var unit = ItemInventory.RequireTarget(context, unitId); var action = Learned(unit, entry);
            var ensure = GuardLearned(context, unit, entry, action);
            if (entry.Equipped)
            {
                int originalSlot = EquippedIndex(unit, entry);
                var remove = new UnitActionMartialUnequip(action);
                var removeCheck = unit.IsCreateAction(remove, false); ensure();
                if (removeCheck != 0) throw new InvalidOperationException("当前游戏状态不允许卸下功法");
                CharacterSave.Sync(context);
                try
                {
                    ensure(); if (Slot(unitId, entry.Type, originalSlot) != entry.SoleId) throw new InvalidOperationException("原装备位置已变化，请重新选择");
                    var result = unit.CreateAction(remove, false); ensure();
                    if (result != 0 || IsEquipped(unit, entry.SoleId))
                        throw new InvalidOperationException("卸下结果未确认，请核对装备");
                }
                finally { CharacterSave.MarkDirty(context); }
                return "已卸下这本功法，已学记录保留；请点击保存";
            }
            if (Slot(unitId, entry.Type, index) != expectedSlot) throw new InvalidOperationException("装备位置已变化，请重新选择");
            var equip = new UnitActionMartialEquip(action, index);
            var check = unit.IsCreateAction(equip, false); ensure();
            if (check != 0) throw new InvalidOperationException("游戏拒绝装备（" + check + "），请核对道点、同类心法和境界限制");
            CharacterSave.Sync(context);
            try
            {
                ensure(); if (Slot(unitId, entry.Type, index) != expectedSlot) throw new InvalidOperationException("装备位置已变化，请重新选择");
                var result = unit.CreateAction(equip, false); ensure();
                if (result != 0 || Slot(unitId, entry.Type, index) != entry.SoleId)
                    throw new InvalidOperationException("装备结果未确认，请核对装备");
            }
            finally { CharacterSave.MarkDirty(context); }
            return "已装备这本功法；请点击保存";
        }
        internal static string Slot(string unitId, MartialType type, int index)
        {
            var unit = CharacterReader.Resolve(unitId);
            if (unit == null) throw new InvalidOperationException("当前人物已失效");
            var data = unit.data.unitData;
            if (type == MartialType.Ability)
            {
                if (data.abilitys == null || index < 0 || index >= data.abilitys.Length) throw new InvalidOperationException("心法位置已失效");
                return data.abilitys[index] ?? "";
            }
            if (index != 0) throw new InvalidOperationException("装备位置不匹配");
            return (type == MartialType.SkillLeft ? data.skillLeft : type == MartialType.SkillRight ? data.skillRight : type == MartialType.Step ? data.step : data.ultimate) ?? "";
        }
        private static void RefreshEffects(WorldUnitBase unit, MartialEntry entry, Action ensure)
        {
            // 原生装备对象持有旧BattleSkillValueData；仅重建当前记录，保留原装备槽。
            ensure();
            if (IsEquipped(unit, entry.SoleId))
            {
                unit.DestroyEquip(entry.SoleId); ensure();
                if (!IsEquipped(unit, entry.SoleId)) throw new InvalidOperationException("功法装备位置已变化，已停止效果重建");
                unit.CreateEquip<WorldUnitEquipMartial>(entry.SoleId); ensure();
            }
            unit.UpdateConf(); ensure();
        }
        internal static List<MartialPrefixEntry> Prefixes(string unitId, MartialEntry entry, DataProps.PropsData source = null)
        {
            var unit = CharacterReader.Resolve(unitId);
            if (unit == null) throw new InvalidOperationException("当前人物已失效");
            var props = source ?? Record(unit, entry); var martial = props.To<DataProps.MartialData>();
            var result = new List<MartialPrefixEntry>(); var unlocked = new HashSet<int>();
            if (!entry.InBag) foreach (var prefix in unit.data.unitData.GetActionMartial(entry.SoleId).GetPrefixsUnlock()) unlocked.Add(prefix.index);
            foreach (var prefix in g.conf.battleSkillPrefixValue.GetPrefix(martial))
                result.Add(new MartialPrefixEntry { Index = prefix.index, Number = prefix.prefixValueItem.number, Value = prefix.prefixLevel,
                    Maximum = checked(g.conf.battleSkillPrefixValue.GetPrefixMaxLevel(martial, prefix.prefixValueItem) * 100),
                    Quality = prefix.level, Description = Plain(UIMartialInfoTool.GetDescRichText(prefix, prefix.prefixValueItemDesc, new BattleSkillValueData(martial), "", "", 2, false)),
                    Unlocked = unlocked.Contains(prefix.index) });
            return result;
        }
        // 与原生RandomPrefixsJob一致：重复受multi限制；require正数为前置，负数为互斥。
        private static bool Compatible(DataProps.MartialData martial, int index, ConfBattleSkillPrefixValueItem candidate)
        {
            var counts = new Dictionary<int, int>(); var configs = new List<ConfBattleSkillPrefixValueItem>();
            foreach (var prefix in martial.GetPrefixs())
            {
                var conf = prefix.index == index ? candidate : prefix.prefixValueItem;
                if (conf == null) return false;
                configs.Add(conf); int count; counts.TryGetValue(conf.number, out count); counts[conf.number] = count + 1;
            }
            if (index == configs.Count)
            { configs.Add(candidate); int count; counts.TryGetValue(candidate.number, out count); counts[candidate.number] = count + 1; }
            foreach (var conf in configs)
            {
                if (counts[conf.number] > conf.multi) return false;
                foreach (var token in (conf.require ?? "0").Split('|'))
                {
                    int required;
                    if (!int.TryParse(token, out required)) return false;
                    if (required > 0 && !counts.ContainsKey(required) || required < 0 && counts.ContainsKey(-required)) return false;
                }
            }
            return true;
        }
        internal static List<MartialPrefixEntry> AllPrefixes(string unitId, MartialEntry entry, int index, int quality, DataProps.PropsData source = null, List<MartialEntry> catalog = null)
        {
            var applicable = Candidates(unitId, entry, index, false, quality, true, source); var result = new List<MartialPrefixEntry>();
            var unit = CharacterReader.Resolve(unitId); var martial = (source ?? Record(unit, entry)).To<DataProps.MartialData>();
            if (catalog == null) catalog = Catalog();
            var byId = new Dictionary<int, MartialPrefixEntry>();
            foreach (var item in applicable) byId[g.conf.battleSkillPrefixValue.GetItem((int)martial.martialType, martial.baseID, item.Number).id] = item;
            foreach (var conf in g.conf.battleSkillPrefixValue._allConfList)
            {
                MartialPrefixEntry candidate; byId.TryGetValue(conf.id, out candidate);
                result.Add(new MartialPrefixEntry { Index = index, Number = conf.number, Value = candidate?.Value ?? 0, Maximum = candidate?.Maximum ?? 0,
                    Quality = candidate?.Quality ?? 0, Description = candidate?.Description ?? Plain(GameTool.LS(conf.desc)),
                    Unavailable = candidate == null ? "不属于当前招式的词条配置" : candidate.Unavailable,
                    CatalogKey = "配置#" + conf.id + " · " + PrefixScope(conf, catalog) });
            }
            SortPrefixes(result); return result;
        }
        private static string PrefixScope(ConfBattleSkillPrefixValueItem conf, List<MartialEntry> catalog)
        {
            var type = (MartialType)conf.skillType;
            var category = type == MartialType.SkillLeft ? 0 : type == MartialType.SkillRight ? 1 : type == MartialType.Step ? 2 : type == MartialType.Ability ? 3 : 4;
            if (conf.skillID == null || conf.skillID.Length == 0 || conf.skillID.Length == 1 && conf.skillID[0] == 0) return Categories[category] + " · 通用";
            var names = new List<string>();
            foreach (var id in conf.skillID)
            {
                var entry = catalog.Find(item => item.Id == id && item.Type == type && item.SuitId == 0);
                names.Add((entry == null ? "招式" : entry.Name) + "#" + id);
            }
            return Categories[category] + " · " + string.Join(" / ", names);
        }
        internal static int PrefixCapacity(DataProps.PropsData props)
        {
            var martial = props.To<DataProps.MartialData>();
            var minimum = g.conf.battleSkillLevel.GetGradeMin(martial.martialType, martial.baseID);
            var capacity = g.conf.battleSkillLevel.GetPrefixCount(6, martial.grade, minimum);
            if (capacity < 1 || capacity > 64) throw new InvalidOperationException("原生词条容量异常");
            return capacity;
        }
        private static int PrefixSplit(DataProps.PropsData props)
        {
            var values = props.GetValues();
            if (values == null || values.Length < 6) throw new InvalidOperationException("功法数据头不完整（" + (values == null ? 0 : values.Length) + "项，需要至少6项）");
            if (values.Length == 6) return 6; // 原生GetPrefixs允许没有词条区的六项头。
            int split = 6; while (split < values.Length && values[split] != -1) split++;
            int count = split - 6;
            if (split == values.Length || values.Length < split + 1 + count)
                throw new InvalidOperationException("词条编号与强度数组不完整，未修改此记录");
            for (int i = 0; i < count; i++) if (values[6 + i] <= 0 || values[split + 1 + i] <= 0)
                throw new InvalidOperationException("词条编号或强度异常，未修改此记录");
            return split;
        }
        internal static void ValidatePrefixLayout(DataProps.PropsData props)
        { PrefixSplit(props); }
        private static int PrefixMinimum(ConfBattleSkillPrefixValueItem conf)
        {
            var bounds = g.conf.battleSkillValue.GetMinMaxLevel(conf);
            if (bounds == null || bounds.Length < 2 || bounds[0] <= 0 || bounds[1] < bounds[0])
                throw new InvalidOperationException("词条数值范围不完整");
            return checked((bounds[0] - 1) * 100 + 1);
        }
        // SetPrefixsID只重排原数量。扩容需要按原生六项头/编号/-1/强度重新构造数组。
        private static void AppendPrefix(DataProps.PropsData props, int number)
        {
            int split = PrefixSplit(props); var old = props.GetValues(); int count = split - 6;
            bool bare = old.Length == 6;
            if (count >= PrefixCapacity(props)) throw new InvalidOperationException("已达到这本功法的原生词条容量");
            var values = new int[old.Length + (bare ? 3 : 2)];
            for (int i = 0; i < split; i++) values[i] = old[i];
            values[split] = number; values[split + 1] = -1;
            for (int i = 0; i < count; i++) values[split + 2 + i] = old[split + 1 + i];
            values[split + 2 + count] = 1;
            for (int i = split + 1 + count; i < old.Length; i++) values[i + 2] = old[i];
            props.SetValues(values);
        }
        private static void SortPrefixes(List<MartialPrefixEntry> entries)
        {
            entries.Sort((a, b) => (a.Unavailable == null ? 0 : 1) != (b.Unavailable == null ? 0 : 1)
                ? (a.Unavailable == null ? -1 : 1) : a.Number != b.Number ? a.Number.CompareTo(b.Number) : string.CompareOrdinal(a.CatalogKey, b.CatalogKey));
        }
        internal static DataProps.PropsData Draft(string unitId, MartialEntry entry)
        {
            var unit = CharacterReader.Resolve(unitId); if (unit == null) throw new InvalidOperationException("当前人物已失效");
            var props = Record(unit, entry); ValidatePrefixLayout(props); return props.Clone();
        }
        internal static List<MartialPrefixEntry> Candidates(string unitId, MartialEntry entry, int index, bool maximize = false, int quality = 0, bool includeUnavailable = false, DataProps.PropsData source = null)
        {
            var unit = CharacterReader.Resolve(unitId);
            if (unit == null) throw new InvalidOperationException("当前人物已失效");
            if (quality < 0 || quality > 6) throw new InvalidOperationException("词条品阶无效");
            var props = source ?? Record(unit, entry); var martial = props.To<DataProps.MartialData>();
            ValidatePrefixLayout(props); var prefixes = Prefixes(unitId, entry, props); var current = prefixes.Find(p => p.Index == index);
            if (index < 0 || index > prefixes.Count || index >= PrefixCapacity(props)) throw new InvalidOperationException("请按顺序选择空词条槽");
            var result = new List<MartialPrefixEntry>(); var seen = new HashSet<int>();
            foreach (var item in g.conf.battleSkillPrefixValue.GetItems((int)martial.martialType, martial.baseID))
            {
                if (!seen.Add(item.number)) continue;
                var conf = g.conf.battleSkillPrefixValue.GetItem((int)martial.martialType, martial.baseID, item.number);
                if (conf == null) continue;
                try
                {
                var reason = !Compatible(martial, index, conf) ? "前置、互斥或重复数量不符" : null;
                var copy = props.Clone();
                if (current == null) AppendPrefix(copy, conf.number);
                var preview = copy.To<DataProps.MartialData>();
                if (current != null) preview.SetPrefixID(index, conf.number);
                var minimum = PrefixMinimum(conf);
                var maximum = checked(g.conf.battleSkillPrefixValue.GetPrefixMaxLevel(preview, conf) * 100);
                var value = maximize || current == null ? maximum : Math.Max(minimum, Math.Min(current.Value, maximum));
                if (maximum < minimum) reason = "原生强度范围无法确认";
                if (quality > 0 && maximum >= minimum)
                {
                    int chosen = -1;
                    // 原生颜色判断会合并效果相同的档位，逐档查询，避免假染色。
                    var data = new BattleSkillValueData(preview);
                    for (int tier = 1; tier <= 6; tier++)
                    {
                        int raw = Math.Min(maximum, tier * 100);
                        if (raw >= minimum && g.conf.battleSkillPrefixValue.PrefixLevelToLevel(raw, data, conf) == quality) chosen = raw;
                    }
                    if (chosen < 0) reason = "此词条在本秘籍品质下无所选品阶"; else value = chosen;
                }
                preview.SetPrefixLevel(index, value);
                foreach (var prefix in g.conf.battleSkillPrefixValue.GetPrefix(preview))
                    if (prefix.index == index && (includeUnavailable || reason == null))
                        result.Add(new MartialPrefixEntry { Index = index, Number = conf.number, Value = prefix.prefixLevel, Maximum = maximum, Quality = prefix.level, Unavailable = reason,
                            Description = Plain(UIMartialInfoTool.GetDescRichText(prefix, prefix.prefixValueItemDesc, new BattleSkillValueData(preview), "", "", 2, false)) });
                }
                catch (Exception ex)
                { if (includeUnavailable) result.Add(new MartialPrefixEntry { Index = index, Number = conf.number, Description = Plain(GameTool.LS(conf.desc)), Unavailable = "效果无法预览：" + ex.Message }); }
            }
            SortPrefixes(result); return result;
        }
        internal static DataProps.PropsData ChoosePrefix(string unitId, MartialEntry entry, DataProps.PropsData source, int index, int number, bool maximize = false, int quality = 0)
        {
            var unit = CharacterReader.Resolve(unitId); if (unit == null) throw new InvalidOperationException("Target unavailable"); Record(unit, entry);
            var choice = Candidates(unitId, entry, index, maximize, quality, true, source).Find(p => p.Number == number);
            if (choice == null || choice.Unavailable != null) throw new InvalidOperationException(choice?.Unavailable ?? "词条不适用于这本功法");
            var copy = source.Clone(); var count = copy.To<DataProps.MartialData>().GetPrefixs().Count;
            if (index == count) AppendPrefix(copy, number);
            var martial = copy.To<DataProps.MartialData>();
            if (index < count) martial.SetPrefixID(index, number);
            martial.SetPrefixLevel(index, choice.Value); ValidatePrefixLayout(copy); return copy;
        }
        private static int EquippedIndex(WorldUnitBase unit, MartialEntry entry)
        {
            if (entry.Type != MartialType.Ability) return 0;
            var slots = unit.data.unitData.abilitys;
            if (slots != null) for (int i = 0; i < slots.Length; i++) if (slots[i] == entry.SoleId) return i;
            throw new InvalidOperationException("原心法装备位置已变化");
        }
        internal static string SavePrefixes(string context, string unitId, MartialEntry entry, DataProps.PropsData preview)
        {
            var unit = ItemInventory.RequireTarget(context, unitId); var action = Learned(unit, entry); var props = Record(unit, entry);
            var ensure = GuardLearned(context, unit, entry, action);
            ValidatePrefixLayout(preview); var desired = preview.To<DataProps.MartialData>(); var prefixes = desired.GetPrefixs();
            if (preview.soleID != entry.SoleId || desired.baseID != entry.Id || desired.grade != entry.Grade || desired.martialType != entry.Type
                || prefixes.Count > PrefixCapacity(preview) || desired.martialInfo.level < 1 || desired.martialInfo.level > 6)
                throw new InvalidOperationException("词条草稿身份或容量不匹配");
            if (entry.Type == MartialType.Ability && preview.To<DataProps.PropsAbilityData>().suitID != entry.SuitId) throw new InvalidOperationException("传承身份已变化");
            var originals = props.To<DataProps.MartialData>().GetPrefixs();
            foreach (var prefix in prefixes)
            {
                var conf = prefix.prefixValueItem;
                bool unchanged = false;
                foreach (var old in originals) if (conf != null && old.prefixValueItem != null && old.index == prefix.index && old.prefixValueItem.number == conf.number && old.prefixLevel == prefix.prefixLevel) unchanged = true;
                if (conf == null || !Compatible(desired, prefix.index, conf) || prefix.prefixLevel < PrefixMinimum(conf)
                    || !unchanged && prefix.prefixLevel > checked(g.conf.battleSkillPrefixValue.GetPrefixMaxLevel(desired, conf) * 100))
                    throw new InvalidOperationException("词条整体不满足原生规则，请调整草稿");
            }
            var values = preview.GetValues(); var oldValues = props.GetValues();
            for (int i = 0; i < 6; i++) if (values[i] != oldValues[i]) throw new InvalidOperationException("功法数据头已变化，未写入草稿");
            int oldSplit = PrefixSplit(props), newSplit = PrefixSplit(preview);
            int oldTail = oldValues.Length == 6 ? 6 : oldSplit + 1 + oldSplit - 6, newTail = values.Length == 6 ? 6 : newSplit + 1 + newSplit - 6;
            if (oldValues.Length - oldTail != values.Length - newTail) throw new InvalidOperationException("功法附加数据已变化");
            for (int i = 0; i < oldValues.Length - oldTail; i++) if (oldValues[oldTail + i] != values[newTail + i]) throw new InvalidOperationException("功法附加数据已变化");
            bool same = oldValues.Length == values.Length;
            if (same) for (int i = 0; i < values.Length; i++) if (oldValues[i] != values[i]) { same = false; break; }
            if (same) return "词条没有变化";
            int slot = entry.Equipped ? EquippedIndex(unit, entry) : 0;
            var remove = entry.Equipped ? new UnitActionMartialUnequip(action) : null;
            int removeCheck = remove == null ? 0 : unit.IsCreateAction(remove, false); ensure();
            if (removeCheck != 0) throw new InvalidOperationException("当前游戏状态不允许更新装备功法");
            var copy = new int[values.Length]; for (int i = 0; i < values.Length; i++) copy[i] = values[i];
            CharacterSave.Sync(context);
            try
            {
                ensure();
                if (remove != null)
                {
                    if (Slot(unitId, entry.Type, slot) != entry.SoleId) throw new InvalidOperationException("原装备位置已变化，未写入词条");
                    var result = unit.CreateAction(remove, false); ensure();
                    if (result != 0 || IsEquipped(unit, entry.SoleId)) throw new InvalidOperationException("原装备卸下未确认，未写入词条");
                }
                ensure(); props.SetValues(copy); ensure();
                var stored = unit.data.unitData.GetActionMartial(entry.SoleId); var read = stored?.data;
                if (read == null || read.soleID != entry.SoleId || stored.exp != entry.Exp) throw new InvalidOperationException("功法记录或修炼进度回读不一致");
                var actual = read.GetValues(); if (actual.Length != copy.Length) throw new InvalidOperationException("词条数组长度回读不一致");
                for (int i = 0; i < actual.Length; i++) if (actual[i] != copy[i]) throw new InvalidOperationException("词条结果回读不一致");
                if (entry.Type == MartialType.Ability && read.To<DataProps.PropsAbilityData>().suitID != entry.SuitId) throw new InvalidOperationException("传承身份回读不一致");
                unit.UpdateConf(); ensure();
                if (remove != null)
                {
                    if (Slot(unitId, entry.Type, slot) != "") throw new InvalidOperationException("原装备槽已被其他功法占用，词条已应用，未覆盖当前装备");
                    var equip = new UnitActionMartialEquip(stored, slot); int check = unit.IsCreateAction(equip, false); ensure();
                    if (check != 0) throw new InvalidOperationException("词条已应用；新需求不允许重新装备（" + check + "），功法保持卸下，请核对后保存");
                    if (Slot(unitId, entry.Type, slot) != "") throw new InvalidOperationException("原装备槽已变化，未恢复装备");
                    var result = unit.CreateAction(equip, false); ensure();
                    if (result != 0 || Slot(unitId, entry.Type, slot) != entry.SoleId) throw new InvalidOperationException("词条已应用，原装备恢复未确认，请核对");
                }
                ensure(); var finalValues = props.GetValues();
                if (action.exp != entry.Exp || finalValues == null || finalValues.Length != copy.Length || IsEquipped(unit, entry.SoleId) != entry.Equipped)
                    throw new InvalidOperationException("刷新后的词条、修炼或装备状态不一致，请核对，勿重复提交");
                for (int i = 0; i < copy.Length; i++) if (finalValues[i] != copy[i]) throw new InvalidOperationException("刷新后的词条结果与草稿不一致，请核对，勿重复提交");
                if (entry.Type == MartialType.Ability && props.To<DataProps.PropsAbilityData>().suitID != entry.SuitId)
                    throw new InvalidOperationException("刷新后的传承身份不一致，请核对功法");
                ensure();
            }
            finally { CharacterSave.MarkDirty(context); }
            return "词条修改已应用；数量变化会按游戏规则改变秘籍品质，修炼进度保留";
        }
        internal static string ChangePrefix(string context, string unitId, MartialEntry entry, int index, int number, bool maximize, int quality = 0)
        { return SavePrefixes(context, unitId, entry, ChoosePrefix(unitId, entry, Draft(unitId, entry), index, number, maximize, quality)); }
    }
}
