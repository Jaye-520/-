using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MOD_YtxSQ1.Services
{
    internal sealed class MartialEntry
    {
        internal int Id, Category, GradeMin, GradeMax, Grade, Level, Exp;
        internal string Name, School, SoleId, Signature;
        internal bool Equipped;
        internal MartialType Type;
    }
    internal static class CharacterMartials
    {
        internal static readonly string[] Categories = { "武技 / 灵技", "绝技", "身法", "心法", "神通" };
        internal static string Plain(string text) { return Regex.Replace(text ?? "", "<[^>]+>", ""); }
        private static string Local(string key) { return Plain(GameTool.LS(key)); }
        internal static string Realm(int grade) { return g.conf.roleGrade.GetGradeName(grade, 1); }
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
            result.Sort((a, b) => a.Category == b.Category ? a.Id.CompareTo(b.Id) : a.Category.CompareTo(b.Category));
            return result;
        }
        internal static List<MartialEntry> Read(string unitId)
        {
            var unit = CharacterReader.Resolve(unitId);
            if (unit == null) throw new InvalidOperationException("当前人物已失效");
            var info = unit.data.unitData; var result = new List<MartialEntry>();
            foreach (var type in new[] { MartialType.SkillLeft, MartialType.SkillRight, MartialType.Step, MartialType.Ability, MartialType.Ultimate })
            {
                var items = info.GetActionMartial(type);
                for (int i = 0; i < items.Count; i++)
                {
                    var data = items[i]; var props = data.data;
                    if (props == null) continue;
                    var martial = props.To<DataProps.MartialData>(); var value = martial.martialInfo;
                    bool equipped = props.soleID == info.skillLeft || props.soleID == info.skillRight || props.soleID == info.step || props.soleID == info.ultimate;
                    if (info.abilitys != null) foreach (var id in info.abilitys) if (id == props.soleID) equipped = true;
                    result.Add(new MartialEntry { Id = martial.baseID, Name = Plain(value.name), School = School(value.basType), Type = type,
                        Category = type == MartialType.SkillLeft ? 0 : type == MartialType.SkillRight ? 1 : type == MartialType.Step ? 2 : type == MartialType.Ability ? 3 : 4,
                        SoleId = props.soleID, Signature = props.soleIDHashData, Grade = martial.grade, Level = value.level, Exp = data.exp, Equipped = equipped });
                }
            }
            result.Sort((a, b) => a.Equipped != b.Equipped ? (a.Equipped ? -1 : 1) : a.Category.CompareTo(b.Category));
            return result;
        }
        internal static string Fingerprint(List<MartialEntry> entries)
        {
            var text = new StringBuilder();
            foreach (var entry in entries) text.Append(entry.SoleId).Append(':').Append(entry.Signature).Append(':').Append(entry.Exp).Append(':').Append(entry.Equipped).Append(';');
            return text.ToString();
        }
        internal static string Description(string unitId, MartialEntry entry)
        {
            var unit = CharacterReader.Resolve(unitId);
            if (unit == null) throw new InvalidOperationException("当前人物已失效");
            var action = unit.data.unitData.GetActionMartial(entry.SoleId);
            if (action == null || action.data.soleIDHashData != entry.Signature) throw new InvalidOperationException("这本功法已变化，请重新选择");
            var martial = action.data.To<DataProps.MartialData>();
            return Plain(UIMartialInfoTool.GetDescRichText(UIMartialInfoTool.GetDesc(martial), new BattleSkillValueData(martial), 2));
        }
        private static DataProps.PropsData Generate(MartialEntry entry, int grade)
        {
            if (grade < entry.GradeMin || grade > entry.GradeMax) throw new InvalidOperationException(entry.Name + "在当前境界不可获取");
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
            var data = props.To<DataProps.MartialData>();
            if (data == null || data.baseID != entry.Id || data.grade != grade) throw new InvalidOperationException("原生秘籍的招式或境界不匹配，未发放");
            return props;
        }
        internal static string Give(string context, string unitId, IList<MartialEntry> entries, int expectedGrade)
        {
            var unit = ItemInventory.RequireTarget(context, unitId);
            if (entries == null || entries.Count == 0) throw new InvalidOperationException("请先选择秘籍");
            if (unit.data.dynUnitData.GetGrade() != expectedGrade) throw new InvalidOperationException("人物境界已变化，请重新选择");
            int used, total; ItemInventory.ReadCapacity(unitId, out used, out total);
            if (total - used < entries.Count) throw new InvalidOperationException("需要" + entries.Count + "个空格，背包仅剩" + Math.Max(0, total - used) + "格；未发放");
            // 全批生成、核对成功才开始入包；前缀与唯一ID由原生奖励/物品工厂负责。
            var props = new List<DataProps.PropsData>();
            foreach (var entry in entries) props.Add(Generate(entry, expectedGrade));
            int completed = 0; CharacterSave.Sync(context);
            try
            {
                foreach (var item in props)
                {
                    unit = ItemInventory.RequireTarget(context, unitId);
                    if (unit.data.dynUnitData.GetGrade() != expectedGrade) throw new InvalidOperationException("人物境界已变化");
                    ItemInventory.ReadCapacity(unitId, out used, out total);
                    if (used >= total) throw new InvalidOperationException("背包空间已变化");
                    try
                    {
                        unit.data.unitData.propData.AddProps(item);
                        unit.data.unitData.propData.UpdateAllShowProps();
                        var stored = unit.data.unitData.propData.GetProps(item.soleID);
                        if (stored == null || stored.propsType != DataProps.PropsDataType.Martial || stored.soleIDHashData != item.soleIDHashData)
                            throw new InvalidOperationException("入包记录回读不一致");
                        completed++;
                    }
                    finally { CharacterSave.MarkDirty(); }
                }
            }
            catch (Exception ex) { throw new InvalidOperationException("已确认添加" + completed + "本，后续未完整确认：" + ex.Message + "；请核对背包，勿直接重复整套添加", ex); }
            return "已添加" + completed + "本" + Realm(expectedGrade) + "秘籍到背包；请点击保存";
        }
    }
}
