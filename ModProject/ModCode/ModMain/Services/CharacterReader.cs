using System;
using System.Globalization;

namespace MOD_YtxSQ1.Services
{
    // 只携带托管字符串，不在窗口中保留游戏对象或存档对象引用。
    internal sealed class CharacterSnapshot
    {
        public readonly string Name;
        public readonly string Realm;
        public readonly string[] Values;
        public readonly string[][] Groups;
        public static readonly string[] GroupNames = { "个人属性", "战斗属性", "功法资质", "灵根资质", "技艺资质" };
        public static readonly string[][] Labels = {
            new[] { "寿命", "心情", "健康", "精力", "体力", "灵力", "念力", "幸运", "悟性", "魅力", "声望", "正道值", "魔道值" },
            new[] { "攻击", "防御", "脚力", "功法抗性", "灵根抗性", "会心", "护心", "移速", "暴击倍数", "抗暴倍数" },
            new[] { "刀法", "枪法", "剑法", "拳法", "掌法", "指法" },
            new[] { "火灵根", "水灵根", "雷灵根", "风灵根", "土灵根", "木灵根" },
            new[] { "炼丹", "炼器", "风水", "画符", "药材", "矿材" }
        };

        public CharacterSnapshot(string name, string realm, string[] values, string[][] groups)
        {
            Name = name;
            Realm = realm;
            Values = values;
            Groups = groups;
        }
    }

    internal static class CharacterReader
    {
        public static bool TryRead(out CharacterSnapshot snapshot, out string reason)
        {
            return TryRead(null, out snapshot, out reason);
        }

        public static bool TryRead(string unitId, out CharacterSnapshot snapshot, out string reason)
        {
            snapshot = null;
            LastFailure = null;
            reason = "当前角色尚未就绪";
            try
            {
                if (g.isDestroy || g.world == null || g.world.isDestroy || !g.world.isIntoWorld) return false;
                var player = Resolve(unitId);
                if (player == null || player.data == null) return false;
                var data = player.data;
                var unit = data.unitData;
                var current = data.dynUnitData;
                if (unit == null || unit.propertyData == null || current == null) return false;

                // gradeID 是配置行 ID，不是大境界编号；先查行，再取境界与阶段名称。
                var gradeId = Read(current.gradeID);
                var realm = "境界待确认";
                if (g.conf != null && g.conf.roleGrade != null)
                {
                    var grade = g.conf.roleGrade.GetItem(gradeId);
                    if (grade != null) realm = g.conf.roleGrade.GetGradeName(grade.grade, grade.phase);
                }
                var name = unit.propertyData.GetName();
                var values = new[] {
                    Pair(current.hp, current.hpMax), Pair(current.mp, current.mpMax),
                    Pair(current.sp, current.spMax), Number(current.attack),
                    Number(current.defense), Number(current.footSpeed)
                };
                var groups = new[] {
                    new[] { Years(current.age) + " / " + Years(current.life), Pair(current.mood, current.moodMax),
                        Pair(current.health, current.healthMax), Pair(current.energy, current.energyMax),
                        values[0], values[1], values[2], Number(current.luck), Number(current.talent),
                        Number(current.beauty), Number(current.reputation), Number(current.standUp), Number(current.standDown) },
                    new[] { values[3], values[4], values[5], Number(current.phycicalFree), Number(current.magicFree),
                        Number(current.crit), Number(current.guard), Number(current.moveSpeed),
                        Percent(current.critValue), Percent(current.guardValue) },
                    new[] { Number(current.basisBlade), Number(current.basisSpear), Number(current.basisSword),
                        Number(current.basisFist), Number(current.basisPalm), Number(current.basisFinger) },
                    new[] { Number(current.basisFire), Number(current.basisFroze), Number(current.basisThunder),
                        Number(current.basisWind), Number(current.basisEarth), Number(current.basisWood) },
                    new[] { Number(current.refineElixir), Number(current.refineWeapon), Number(current.geomancy),
                        Number(current.symbol), Number(current.herbal), Number(current.mine) }
                };
                snapshot = new CharacterSnapshot(string.IsNullOrEmpty(name) ? "无名修士" : name,
                    string.IsNullOrEmpty(realm) ? "境界待确认" : realm,
                    values, groups);
                reason = null;
                return true;
            }
            catch (Exception ex)
            {
                // 不将异常传播为界面构建失败；调用者清空旧值并控制日志频率。
                snapshot = null;
                reason = "角色信息暂不可读取";
                LastFailure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        // 仅保留必要异常摘要，不保存任何游戏数据或目标引用。
        internal static string LastFailure { get; private set; }
        private static int Read(DynInt value)
        {
            if (value == null) throw new InvalidOperationException("角色属性未初始化");
            return value.value;
        }
        private static string Number(DynInt value) { return Read(value).ToString(CultureInfo.InvariantCulture); }
        private static string Pair(DynInt value, DynInt maximum) { return Number(value) + " / " + Number(maximum); }
        private static string Years(DynInt value) { return (Read(value) / 12).ToString(CultureInfo.InvariantCulture); }
        internal static WorldUnitBase Resolve(string id)
        {
            if (g.isDestroy || g.world == null || g.world.isDestroy || !g.world.isIntoWorld) return null;
            var unit = string.IsNullOrEmpty(id) ? g.world.playerUnit :
                (g.world.unit == null ? null : g.world.unit.GetUnit(id, true));
            return unit != null && !unit.isDie ? unit : null;
        }

        internal static string ContextKey()
        {
            var player = Resolve(null);
            return player == null || player.data == null || player.data.unitData == null ? null :
                player.Pointer.ToInt64().ToString(CultureInfo.InvariantCulture) + ":" + player.data.unitData.unitID;
        }

        private static string Percent(DynInt value)
        {
            // 07.1游戏截图：本角色原游戏200%，除10后为20%。使用整数百分数，
            // 不从valueFloat反推存储单位；其缩放与整数getter不能在替身里臆测。
            return Number(value) + "%";
        }
    }
}
