using System;
using System.Collections.Generic;

namespace MOD_YtxSQ1.Services
{
    internal sealed class MartialBuild
    {
        internal readonly string Name, School, Summary, Source, Evidence, Requirements;
        internal readonly int[] Ids;
        internal MartialBuild(string name, string school, string summary, string source, string evidence, string requirements, params int[] ids)
        { Name = name; School = school; Summary = summary; Source = source; Evidence = evidence; Requirements = requirements; Ids = ids; }
    }
    internal static class MartialBuilds
    {
        // 只收录正文和本机配置已核对的搭配。来源热度不是本机版本强度验收。
        internal static readonly MartialBuild[] Verified =
        {
            new MartialBuild("火剑", "剑 / 火", "流血刷新身法，烈焰叠加增伤。", "https://www.taptap.cn/moment/619147536988376225",
                "2025-02-04 · 攻略66万浏览；剑系投票13913人，火剑49.2%",
                "核心：月影挂流血、绝影引爆减身法冷却、火身法生成烈焰、火秘卷烈焰增伤、剑神功刷新绝技。通用五心法按实际需要调整；需要配词条，获取不自动成型。",
                14113, 24111, 510111, 61112, 81001, 82001, 83001, 84001, 85001, 86007, 87003, 88003),
            new MartialBuild("水剑", "剑 / 水", "幻影协助叠流血，绝技与身法循环。", "https://www.taptap.cn/moment/763292084974127219",
                "核对时约7.8万浏览 · 手游攻略参考，PC实战待验证",
                "核心：月影流血、绝影引爆减身法冷却、水身法幻影、骤天剑剑影无敌、剑神功刷新绝技。经和诀选择己方单位消失时回复，心法需配词条；套装、器灵等不包含在秘籍包。",
                14113, 24111, 510121, 34111, 81001, 82001, 83001, 84001, 85001, 86003, 87003, 88003),
            new MartialBuild("三吸指", "指", "内虚引爆削蓝，通过心法换取攻击与续航。", "https://www.taptap.cn/moment/645393593510399137",
                "2025-03-02 · 攻略3.3万浏览，作者有限实玩；PC待验证",
                "核心：两仪指内虚、灵虚爆削蓝、指神功吸攻击，经/诀削蓝回复；指秘卷辅助开脉/封脉。攻略列武法精通、血魔大法、炉火纯青为必要逆天；本包选指身法，作者在羽化前自动攻击会破隐时推荐风身法。秘籍包不修改气运。",
                17112, 27113, 510061, 37112, 81001, 82001, 83001, 84001, 85001, 86006, 87006, 88003),
            new MartialBuild("神脉指", "指", "开脉与封脉配合，以神脉指作为输出核心。", "https://www.taptap.cn/moment/645393593510399137",
                "2025-03-02 · 攻略3.3万浏览，作者有限实玩；PC待验证",
                "核心：两仪指开脉、封灵爆封脉、神脉指输出、指神功削蓝吸攻击。攻略建议武法精通、血魔大法、烈火燎原；与三吸指的内虚引爆路线不同。需刷对应词条，获取后仍需学习和装备。",
                17112, 27114, 510061, 37112, 81001, 82001, 83001, 84001, 85001, 86006, 87006, 88003)
        };
        internal static List<MartialEntry> Resolve(MartialBuild build, List<MartialEntry> catalog, int grade)
        {
            var result = new List<MartialEntry>(); var missing = new List<string>();
            foreach (var id in build.Ids)
            {
                var entry = catalog.Find(item => item.Id == id && item.SuitId == 0);
                if (entry == null) missing.Add("配置#" + id);
                else if (grade < entry.GradeMin || grade > entry.GradeMax) missing.Add(entry.Name + "（最低" + CharacterMartials.Realm(entry.GradeMin) + "）");
                else result.Add(entry);
            }
            if (missing.Count > 0) throw new InvalidOperationException("本境界无法取得完整搭配：" + string.Join("、", missing.ToArray()) + "；未发放，不替换招式");
            return result;
        }
    }
}
