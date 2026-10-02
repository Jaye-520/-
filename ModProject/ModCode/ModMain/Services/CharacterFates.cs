using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MOD_YtxSQ1.Services
{
    // 快照仅含托管值；分类以官方RoleCreateFeature表头及拥有条目的来源为准。
    internal sealed class CharacterFate
    {
        public readonly int Id, Category;
        public readonly string Name, Description;
        public CharacterFate(int id, int category, string name, string description)
        { Id = id; Category = category; Name = name; Description = description; }
    }

    internal static class CharacterFates
    {
        public static readonly string[] Categories = { "先天气运", "后天气运", "逆天改命", "所有气运" };
        public static bool TryRead(string unitId, out CharacterFate[] entries, out string reason)
        {
            entries = new CharacterFate[0];
            reason = "气运暂不可读取";
            try
            {
                var unit = CharacterReader.Resolve(unitId);
                if (unit == null || unit.data == null || unit.data.unitData == null ||
                    unit.data.unitData.propertyData == null || g.conf == null ||
                    g.conf.roleCreateFeature == null || g.conf.localText == null) return false;
                var property = unit.data.unitData.propertyData;
                var result = new List<CharacterFate>();
                var seen = new HashSet<string>();
                if (property.bornLuck != null)
                    foreach (var luck in property.bornLuck)
                        if (luck != null) Add(result, seen, unit, luck, 0);
                if (property.addLuck != null)
                    foreach (var luck in property.addLuck)
                        if (luck != null) Add(result, seen, unit, luck, 1);
                entries = result.ToArray();
                reason = null;
                return true;
            }
            catch { return false; } // 不用部分列表冒充完整读取，也不影响独立的数值属性。
        }

        private static void Add(List<CharacterFate> result, HashSet<string> seen, WorldUnitBase unit, DataUnit.LuckData luck, int source)
        {
            var id = luck.id;
            if (id <= 0 || !seen.Add(source + ":" + id)) return;
            var item = g.conf.roleCreateFeature.GetItem(id);
            var category = item != null && item.type == 3 ? 2 : source;
            var fallback = "未知气运 #" + id.ToString(CultureInfo.InvariantCulture);
            var name = item == null ? fallback : LocalText(item.name, fallback);
            var description = item == null ? "当前配置中未找到此气运。" : LocalText(item.tips, "暂无说明");
            var tooltip = name + "\n" + description;
            if (item != null)
            {
                try
                {
                    // 与原生UIAddLuckItem一致的bgType=2；使用选中人物及真实拥有记录。
                    var native = ConfRoleCreateFeatureEx.tips(item, unit, luck, 2);
                    if (!string.IsNullOrEmpty(native)) tooltip = native;
                }
                catch { } // 特殊/缺失效果配置失败仍保留条目与基础说明。
            }
            result.Add(new CharacterFate(id, category, name, tooltip));
        }

        private static string LocalText(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key) || key == "0") return fallback;
            var text = ConfLocalText.GetText(key);
            if (string.IsNullOrEmpty(text) || text == key) return fallback;
            // 自绘Text禁用富文本；只剥离原生格式标签，保留内容和未解析占位符。
            return Regex.Replace(text.Replace("\\n", "\n"), @"</?(?:color|size|b|i|material|quad)\b[^>]*>", "", RegexOptions.IgnoreCase);
        }
    }
}
