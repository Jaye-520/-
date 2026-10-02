using System;
using System.Collections.Generic;
using System.Globalization;

namespace MOD_YtxSQ1.Services
{
    internal sealed class CharacterChoice
    {
        internal readonly string Id, Label;
        internal CharacterChoice(int id, string label) { Id = id.ToString(CultureInfo.InvariantCulture); Label = label; }
    }

    // 明确绑定底层字段；不以界面合并字符串解析兴趣、性格或当前值/上限。
    internal static class CharacterEditData
    {
        internal static DynInt Number(WorldUnitBase unit, CharacterField field)
        {
            var d = unit.data.dynUnitData;
            if (field.Group == 0)
            {
                switch (field.Index)
                {
                    case 0: return field.Slot == 0 ? d.age : d.life;
                    case 1: return field.Slot == 0 ? d.mood : d.moodMax;
                    case 2: return field.Slot == 0 ? d.health : d.healthMax;
                    case 3: return field.Slot == 0 ? d.energy : d.energyMax;
                    case 4: return field.Slot == 0 ? d.hp : d.hpMax;
                    case 5: return field.Slot == 0 ? d.mp : d.mpMax;
                    case 6: return field.Slot == 0 ? d.sp : d.spMax;
                    case 7: return d.luck;
                    case 8: return d.talent;
                }
            }
            DynInt[] fields = null;
            switch (field.Group)
            {
                case 1: fields = new[] { d.attack, d.defense, d.footSpeed, d.phycicalFree, d.magicFree, d.crit, d.guard, d.moveSpeed, d.critValue, d.guardValue }; break;
                case 2: fields = new[] { d.basisBlade, d.basisSpear, d.basisSword, d.basisFist, d.basisPalm, d.basisFinger }; break;
                case 3: fields = new[] { d.basisFire, d.basisFroze, d.basisThunder, d.basisWind, d.basisEarth, d.basisWood }; break;
                case 4: fields = new[] { d.refineElixir, d.refineWeapon, d.geomancy, d.symbol, d.herbal, d.mine }; break;
                case 5: fields = new[] { d.beauty, d.reputation, d.standUp, d.standDown }; break;
            }
            return fields == null || field.Index >= fields.Length ? null : fields[field.Index];
        }
        internal static string Read(WorldUnitBase unit, CharacterField field)
        {
            var p = unit.data.unitData.propertyData;
            if (field.Name) return p.GetName();
            if (field.Choice)
            {
                int id;
                if (field.Group == 0 && field.Index == 9) id = (int)p.sex;
                else if (field.Group == 0) id = p.hobby == null || field.Slot >= p.hobby.Length ? 0 : p.hobby[field.Slot];
                else if (field.Index == 10) id = field.Slot == 0 ? p.inTrait : field.Slot == 1 ? p.outTrait1 : p.outTrait2;
                else id = unit.data.dynUnitData.gradeID.value;
                return id.ToString(CultureInfo.InvariantCulture);
            }
            var number = Number(unit, field).value;
            if (field.Group == 0 && field.Index == 0) number /= 12;
            return number.ToString(CultureInfo.InvariantCulture);
        }
        internal static string Label(CharacterField field, string value)
        {
            if (!field.Choice) return value;
            int id;
            if (!int.TryParse(value, out id)) return "—";
            try
            {
                if (field.Group == 0 && field.Index == 9) return id == (int)UnitSexType.Man ? "男" : id == (int)UnitSexType.Woman ? "女" : "未确认";
                if (id == 0) return "无";
                if (field.Group == 0) { var item = g.conf.roleCreateHobby.GetItem(id); if (item != null) return Local(item.name); }
                else if (field.Index == 10) { var item = g.conf.roleCreateCharacter.GetItem(id); if (item != null) return Local(item.sc5asd_sd34); }
                else { var item = g.conf.roleGrade.GetItem(id); if (item != null) return g.conf.roleGrade.GetGradeName(item.grade, item.phase); }
            }
            catch { }
            return "未知 #" + id;
        }
        private static string Local(string key)
        {
            var label = ConfLocalText.GetText(key);
            return string.IsNullOrEmpty(label) ? key : label;
        }
        internal static List<CharacterChoice> Choices(CharacterField field, string original)
        {
            var result = new List<CharacterChoice>();
            if (field.Group == 0 && field.Index == 9)
            { result.Add(new CharacterChoice((int)UnitSexType.Man, "男")); result.Add(new CharacterChoice((int)UnitSexType.Woman, "女")); }
            else if (field.Group == 0)
            {
                var items = g.conf.roleCreateHobby._allConfList;
                for (int i = 0; i < items.Count; i++) if (items[i] != null) result.Add(new CharacterChoice(items[i].id, Local(items[i].name)));
            }
            else if (field.Index == 10)
            {
                int currentId;
                if (!int.TryParse(original, out currentId)) throw new InvalidOperationException("性格槽位未就绪");
                var current = g.conf.roleCreateCharacter.GetItem(currentId);
                if (current == null) throw new InvalidOperationException("性格槽位类型未确认");
                // 按该槽当前真实配置的类型筛选，不猜测混淆配置的类型编号。
                var items = g.conf.roleCreateCharacter._allConfList;
                for (int i = 0; i < items.Count; i++)
                    if (items[i] != null && items[i].type == current.type)
                        result.Add(new CharacterChoice(items[i].id, Local(items[i].sc5asd_sd34)));
            }
            else
            {
                var items = g.conf.roleGrade._allConfList;
                for (int i = 0; i < items.Count; i++) if (items[i] != null)
                    result.Add(new CharacterChoice(items[i].id, g.conf.roleGrade.GetGradeName(items[i].grade, items[i].phase)));
            }
            return result;
        }
    }
}

