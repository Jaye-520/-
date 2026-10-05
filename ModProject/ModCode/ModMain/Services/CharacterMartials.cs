using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MOD_YtxSQ1.Services
{
    internal sealed class MartialEntry
    {
        internal int Id, Category, GradeMin, GradeMax, Grade, Level, Exp, SuitId;
        internal string Name, School, SoleId, Signature, ReadError;
        internal bool Equipped, SuitGradeUnknown, InBag;
        internal int MaxExp;
        internal MartialType Type;
        internal IntPtr ReadPointer;
    }
    internal static partial class CharacterMartials
    {
        internal static readonly string[] Categories = { "武技 / 灵技", "绝技", "身法", "心法", "神通" };
        internal static string Plain(string text) { return Regex.Replace(text ?? "", "<[^>]+>", ""); }
        private static string Local(string key) { return Plain(GameTool.LS(key)); }
        internal static string Realm(int grade) { return grade > 0 ? g.conf.roleGrade.GetGradeName(grade, 1) : "境界未读取"; }
        internal static int Grade(string unitId)
        {
            var unit = CharacterReader.Resolve(unitId);
            if (unit == null) throw new InvalidOperationException("当前人物已失效");
            return unit.data.dynUnitData.GetGrade();
        }
        private static string School(string basType)
        {
            switch (basType)
            {
                case "basBlade": return "刀"; case "basSpear": return "枪"; case "basSword": return "剑";
                case "basFist": return "拳"; case "basPalm": return "掌"; case "basFinger": return "指";
                case "basFire": return "火"; case "basFroze": return "水"; case "basThunder": return "雷";
                case "basWind": return "风"; case "basEarth": return "土"; case "basWood": return "木";
                default: return "通用";
            }
        }
        private static bool Normal(string classes) { return Array.IndexOf((classes ?? "").Split('|'), "1") >= 0; }
        internal static List<MartialEntry> Catalog()
        {
            var result = new List<MartialEntry>();
            foreach (var conf in g.conf.battleSkillAttack._allConfList)
            {
                if (!Normal(conf.className) || conf.gradeMin < 1 || conf.gradeMax < conf.gradeMin || conf.type < 1 || conf.type > 4) continue;
                var type = conf.type <= 2 ? MartialType.SkillLeft : conf.type == 3 ? MartialType.SkillRight : MartialType.Ultimate;
                result.Add(new MartialEntry { Id = conf.id, Name = Local(conf.name), School = School(conf.basType),
                    Type = type, Category = type == MartialType.SkillLeft ? 0 : type == MartialType.SkillRight ? 1 : 4,
                    GradeMin = conf.gradeMin, GradeMax = conf.gradeMax });
            }
            foreach (var conf in g.conf.battleStepBase._allConfList)
                if (Normal(conf.className) && conf.gradeMin > 0 && conf.gradeMax >= conf.gradeMin)
                    result.Add(new MartialEntry { Id = conf.id, Name = Local(conf.name), School = School(conf.basType),
                        Type = MartialType.Step, Category = 2, GradeMin = conf.gradeMin, GradeMax = conf.gradeMax });
            foreach (var conf in g.conf.battleAbilityBase._allConfList)
            {
                bool normal = false; foreach (var value in conf.className) if (value == 1) normal = true;
                if (!normal || conf.gradeMin < 1 || conf.gradeMax < conf.gradeMin) continue;
                var school = School(conf.basType);
                result.Add(new MartialEntry { Id = conf.id, Name = (school == "通用" ? "" : school + "系") + Local(conf.name), School = school,
                    Type = MartialType.Ability, Category = 3, GradeMin = conf.gradeMin, GradeMax = conf.gradeMax });
            }
            AddSuitCatalog(result);
            result.Sort((a, b) => a.Category != b.Category ? a.Category.CompareTo(b.Category)
                : a.SuitId != b.SuitId ? a.SuitId.CompareTo(b.SuitId) : a.Id.CompareTo(b.Id));
            return result;
        }
        private static void AddSuitCatalog(List<MartialEntry> result)
        {
            var grades = SuitRewardGrades(); var baseEntries = new List<MartialEntry>(result);
            foreach (var suit in g.conf.battleAbilitySuitBase._allConfList)
            {
                int minimum; var known = grades.TryGetValue(suit.id, out minimum);
                foreach (var member in (suit.suitMember ?? "").Split('|'))
                {
                    int id; if (!int.TryParse(member, out id)) continue;
                    var basis = baseEntries.Find(entry => entry.Type == MartialType.Ability && entry.Id == id);
                    if (basis == null) continue;
                    result.Add(new MartialEntry { Id = id, SuitId = suit.id, Category = 3, Type = MartialType.Ability,
                        Name = Local(suit.suitName) + "·" + basis.Name, School = basis.School,
                        GradeMin = known ? Math.Max(basis.GradeMin, minimum) : basis.GradeMin,
                        GradeMax = basis.GradeMax, SuitGradeUnknown = !known });
                }
            }
        }
        private static Dictionary<int, int> SuitRewardGrades()
        {
            // 境界来自传承奖励所引用的心法生成配置；未知来源不猜低阶版本。
            var rewardIds = new HashSet<int>(); var rewardGrades = new Dictionary<int, int>();
            foreach (var suit in g.conf.gameItemRewardAbilitySuit._allConfList)
                if (suit.level <= 6 && suit.rate > 0 && suit.weight > 0) rewardIds.Add(suit.rewardID);
            foreach (var reward in g.conf.gameItemReward._allConfList)
            {
                if (reward.itemType != 4 || !rewardIds.Contains(reward.rewardID)) continue;
                var combine = g.conf.battleAbilityCombine.GetItem(reward.item);
                if (combine == null || combine.rewardGradeID < 1 || combine.rewardGradeID > 10) continue;
                int old; if (!rewardGrades.TryGetValue(reward.rewardID, out old) || combine.rewardGradeID < old)
                    rewardGrades[reward.rewardID] = combine.rewardGradeID;
            }
            var result = new Dictionary<int, int>();
            foreach (var suit in g.conf.gameItemRewardAbilitySuit._allConfList)
            {
                int grade, old;
                if (suit.level > 6 || suit.rate <= 0 || suit.weight <= 0 || !rewardGrades.TryGetValue(suit.rewardID, out grade)) continue;
                if (!result.TryGetValue(suit.suitID, out old) || grade < old) result[suit.suitID] = grade;
            }
            return result;
        }
        internal static bool CanAcquire(MartialEntry entry, int grade)
        { return !entry.SuitGradeUnknown && grade >= entry.GradeMin && grade <= entry.GradeMax; }
        internal static List<MartialEntry> SearchCatalog(List<MartialEntry> catalog, string query, int category, out string basis)
        {
            basis = null;
            var direct = catalog.FindAll(entry => query.Length == 0 ? entry.Category == category
                : entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || entry.Id.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            if (query.Length < 3 || direct.Count > 0) return direct;
            // 完整名称只反查基础类型，不能从前缀推定流派或保证生成相同词条。
            var first = new HashSet<string>(); var second = new HashSet<string>();
            foreach (var prefix in g.conf.battleSkillPrefixName._allConfList)
            {
                if (prefix.index == 1) first.Add(prefix.text);
                else if (prefix.index == 2) second.Add(prefix.text);
            }
            if (!first.Contains(query.Substring(0, 1)) || !second.Contains(query.Substring(1, 1))) return direct;
            var suffix = query.Substring(2);
            var matches = catalog.FindAll(entry => entry.SuitId == 0 &&
                (entry.Type == MartialType.Ability ? Local(g.conf.battleAbilityBase.GetItem(entry.Id).name) : entry.Name) == suffix);
            if (matches.Count > 0) basis = suffix;
            return matches;
        }
        internal static List<MartialEntry> Read(string unitId)
        {
            var unit = CharacterReader.Resolve(unitId);
            if (unit == null) throw new InvalidOperationException("当前人物已失效");
            var info = unit.data.unitData; var result = new List<MartialEntry>();
            foreach (var type in new[] { MartialType.SkillLeft, MartialType.SkillRight, MartialType.Step, MartialType.Ability, MartialType.Ultimate })
            {
                var items = info.GetActionMartial(type);
                if (items == null) continue;
                for (int i = 0; i < items.Count; i++)
                {
                    var data = items[i];
                    if (data == null) continue;
                    DataProps.PropsData props = null;
                    IntPtr readPointer = IntPtr.Zero;
                    try
                    {
                        readPointer = data.Pointer;
                        props = data.data;
                        if (props == null) continue;
                        var entry = ReadMartialEntry(unit, props, type, data.exp, false);
                        entry.ReadPointer = readPointer; result.Add(entry);
                    }
                    catch (Exception ex)
                    {
                        var unread = props == null ? UnreadMartialEntry(type, false) : ReadMartialEntry(unit, props, type, 0, false);
                        unread.ReadError = ex.Message; unread.ReadPointer = readPointer;
                        result.Add(unread);
                    }
                }
            }
            result.Sort((a, b) => a.Equipped != b.Equipped ? (a.Equipped ? -1 : 1) : a.Category.CompareTo(b.Category));
            return result;
        }
        internal static string Fingerprint(List<MartialEntry> entries)
        {
            var text = new StringBuilder();
            foreach (var entry in entries) text.Append(entry.ReadPointer).Append(':').Append(entry.Type).Append(':').Append(entry.SoleId).Append(':').Append(entry.Signature).Append(':').Append(entry.MaxExp).Append(':').Append(entry.InBag).Append(':').Append(entry.Exp).Append(':').Append(entry.Equipped).Append(':').Append(entry.ReadError).Append(';');
            return text.ToString();
        }
        internal static string Description(string unitId, MartialEntry entry)
        {
            var unit = CharacterReader.Resolve(unitId);
            if (unit == null) throw new InvalidOperationException("当前人物已失效");
            var martial = Record(unit, entry).To<DataProps.MartialData>();
            return Plain(UIMartialInfoTool.GetDescRichText(UIMartialInfoTool.GetDesc(martial), new BattleSkillValueData(martial), 2));
        }
        private static DataProps.PropsData Generate(MartialEntry entry, int grade)
        {
            if (!CanAcquire(entry, grade)) throw new InvalidOperationException(entry.Name + (entry.SuitGradeUnknown ? "的获取境界来源尚未核实" : "在当前境界不可获取"));
            UnhollowerBaseLib.Il2CppStructArray<int> values;
            if (entry.Type == MartialType.Step)
            {
                var conf = new ConfBattleStepCombineItem { type = g.conf.battleStepBase.GetItem(entry.Id).type.ToString(), level = "6", className = "1" };
                values = RewardTool.GetStepReward(conf, grade, (ReturnAction<bool, ConfBattleStepBaseItem>)(new Func<ConfBattleStepBaseItem, bool>(item => item.id == entry.Id)), null);
            }
            else if (entry.Type == MartialType.Ability)
            {
                var conf = new ConfBattleAbilityCombineItem { type = g.conf.battleAbilityBase.GetItem(entry.Id).type.ToString(), level = "6", className = "1" };
                values = RewardTool.GetAbilityReward(conf, grade, (ReturnAction<bool, ConfBattleAbilityBaseItem>)(new Func<ConfBattleAbilityBaseItem, bool>(item => item.id == entry.Id)), null);
            }
            else
            {
                var conf = new ConfBattleSkillCombineItem { type = g.conf.battleSkillAttack.GetItem(entry.Id).type.ToString(), baseID = entry.Id.ToString(), level = "6", className = "1" };
                values = RewardTool.GetSkillReward(conf, grade, null, null);
            }
            var props = DataProps.PropsData.NewMartial(entry.Type, values);
            if (props == null || string.IsNullOrEmpty(props.soleID)) throw new InvalidOperationException("原生秘籍生成失败");
            if (entry.SuitId != 0)
            {
                var suit = g.conf.battleAbilitySuitBase.GetItem(entry.SuitId);
                if (entry.Type != MartialType.Ability || suit == null || Array.IndexOf(suit.suitMember.Split('|'), entry.Id.ToString()) < 0)
                    throw new InvalidOperationException("传承与心法成员不匹配，未发放");
                // 原生奖励 SetRewardPropsDataAttr 也通过此 setter 写入 AbilityData 并失效缓存。
                var ability = props.To<DataProps.PropsAbilityData>();
                if (ability == null) throw new InvalidOperationException("原生心法类型不匹配，未发放");
                ability.suitID = entry.SuitId;
                if (props.To<DataProps.PropsAbilityData>().suitID != entry.SuitId)
                    throw new InvalidOperationException("原生传承身份回读不匹配，未发放");
            }
            var data = props.To<DataProps.MartialData>();
            if (data == null || data.baseID != entry.Id || data.grade != grade || data.martialInfo.level != 6) throw new InvalidOperationException("原生秘籍的招式或境界不匹配，未发放");
            return props;
        }
        internal static string Give(string context, string unitId, IList<MartialEntry> entries, int expectedGrade)
        {
            var unit = ItemInventory.RequireTarget(context, unitId);
            var ensure = ItemInventory.Guard(context, unit);
            if (entries == null || entries.Count == 0) throw new InvalidOperationException("请先选择秘籍");
            if (unit.data.dynUnitData.GetGrade() != expectedGrade) throw new InvalidOperationException("人物境界已变化，请重新选择");
            int used, total; ItemInventory.ReadCapacity(unitId, out used, out total);
            if (total - used < entries.Count) throw new InvalidOperationException("需要" + entries.Count + "个空格，背包仅剩" + Math.Max(0, total - used) + "格；未发放");
            // 全批生成、核对成功才开始入包；前缀与唯一ID由原生奖励/物品工厂负责。
            var props = new List<DataProps.PropsData>();
            foreach (var entry in entries) { ensure(); props.Add(Generate(entry, expectedGrade)); ensure(); }
            var ids = new List<string>(); var signatures = new List<string>();
            foreach (var item in props) { ids.Add(item.soleID); signatures.Add(item.soleIDHashData); }
            int completed = 0; CharacterSave.Sync(context);
            try
            {
                foreach (var item in props)
                {
                    ensure();
                    if (unit.data.dynUnitData.GetGrade() != expectedGrade) throw new InvalidOperationException("人物境界已变化");
                    ItemInventory.ReadCapacity(unitId, out used, out total);
                    if (used >= total) throw new InvalidOperationException("背包空间已变化");
                    var signature = item.soleIDHashData; var soleId = item.soleID;
                    var source = entries[completed];
                    try
                    {
                        ensure(); unit.data.unitData.propData.AddProps(item); ensure();
                        unit.data.unitData.propData.UpdateAllShowProps(); ensure();
                        var stored = unit.data.unitData.propData.GetProps(soleId);
                        if (stored == null || stored.propsType != DataProps.PropsDataType.Martial || stored.soleIDHashData != signature)
                            throw new InvalidOperationException("入包记录回读不一致");
                        if (source.SuitId != 0 && stored.To<DataProps.PropsAbilityData>().suitID != source.SuitId)
                            throw new InvalidOperationException("入包传承身份回读不一致");
                        completed++;
                    }
                    finally { CharacterSave.MarkDirty(context); }
                }
                // 后续原生回调可能改变先前入包的记录；逐项成功不代表整批仍成立。
                for (int i = 0; i < props.Count; i++)
                {
                    ensure(); var stored = unit.data.unitData.propData.GetProps(ids[i]);
                    if (stored == null || stored.propsType != DataProps.PropsDataType.Martial || stored.soleIDHashData != signatures[i])
                        throw new InvalidOperationException("整批秘籍入包记录回读不一致");
                    if (entries[i].SuitId != 0 && stored.To<DataProps.PropsAbilityData>().suitID != entries[i].SuitId)
                        throw new InvalidOperationException("整批秘籍传承身份回读不一致");
                    ensure();
                }
            }
            catch (Exception ex) { throw new InvalidOperationException("已逐项处理" + completed + "本，整批未完整确认：" + ex.Message + "；请核对背包，勿直接重复整套添加", ex); }
            return "已添加" + completed + "本" + Realm(expectedGrade) + "秘籍到背包；请点击保存";
        }
    }
}
