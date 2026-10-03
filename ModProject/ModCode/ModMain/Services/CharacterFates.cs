using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MOD_YtxSQ1.Services
{
    // 快照仅含托管值；分类以官方RoleCreateFeature表头及拥有条目的来源为准。
    internal sealed class CharacterFate
    {
        public readonly int Id, Category, Level;
        public readonly string Name, Description;
        public CharacterFate(int id, int category, string name, string description, int level = 0)
        { Id = id; Category = category; Name = name; Description = description; Level = level; }
    }

    internal static class CharacterFates
    {
        public static readonly string[] Categories = { "先天气运", "后天气运", "逆天改命", "所有气运" };
        internal static string LastFailure { get; private set; }
        public static bool TryRead(string unitId, out CharacterFate[] entries, out string reason)
        {
            entries = new CharacterFate[0];
            reason = "气运暂不可读取";
            LastFailure = null;
            var failures = new List<string>();
            var result = new List<CharacterFate>();
            var seen = new HashSet<string>();
            try
            {
                var unit = CharacterReader.Resolve(unitId);
                if (unit == null || unit.data == null || unit.data.unitData == null ||
                    unit.data.unitData.propertyData == null || g.conf == null || g.conf.roleCreateFeature == null)
                { LastFailure = "对象/气运配置未就绪"; return false; }
                var property = unit.data.unitData.propertyData;
                // 使用索引，避免特殊人物的原生集合枚举器使整批读取中断。
                try
                {
                    var born = property.bornLuck;
                    if (born != null) for (int i = 0; i < born.Length; i++)
                    {
                        try { Add(result, seen, unit, born[i], 0, failures); }
                        catch (Exception ex) { failures.Add("先天条目[" + i + "]:" + ex.GetType().Name); }
                    }
                }
                catch (Exception ex) { failures.Add("先天来源:" + ex.GetType().Name); }
                try
                {
                    var acquired = property.addLuck;
                    if (acquired != null) for (int i = 0; i < acquired.Count; i++)
                    {
                        try { Add(result, seen, unit, acquired[i], 1, failures); }
                        catch (Exception ex) { failures.Add("后天条目[" + i + "]:" + ex.GetType().Name); }
                    }
                }
                catch (Exception ex) { failures.Add("后天来源:" + ex.GetType().Name); }
                entries = result.ToArray();
                LastFailure = failures.Count == 0 ? null : string.Join("；", failures.ToArray());
                reason = failures.Exists(f => f.StartsWith("先天来源") || f.StartsWith("后天来源") || f.StartsWith("先天条目") || f.StartsWith("后天条目")) ? "部分拥有记录未读取" : null;
                return failures.Count == 0 || entries.Length > 0;
            }
            catch (Exception ex)
            {
                LastFailure = "拥有记录:" + ex.GetType().Name + ":" + ex.Message;
                return false;
            }
        }

        private static void Add(List<CharacterFate> result, HashSet<string> seen, WorldUnitBase unit,
            DataUnit.LuckData luck, int source, List<string> failures)
        {
            if (luck == null) return;
            var id = luck.id;
            if (id <= 0 || !seen.Add(source + ":" + id)) return;
            var fallback = "未知气运 #" + id.ToString(CultureInfo.InvariantCulture);
            var name = fallback;
            var description = "当前配置中未找到此气运。";
            var category = source;
            var level = 0;
            var tooltip = "";
            try
            {
                var item = g.conf.roleCreateFeature.GetItem(id);
                if (item != null)
                {
                    // 原NPC界面也会过滤conceal=1；隐藏运行效果不能充当可见气运。
                    if (item.conceal == 1) return;
                    category = item.type == 3 ? 2 : source;
                    level = item.level;
                    try { name = LocalText(item.name, fallback); }
                    catch (Exception ex) { failures.Add("名称#" + id + ":" + ex.GetType().Name); }
                    try { description = LocalText(item.tips, "暂无说明"); }
                    catch (Exception ex) { failures.Add("说明#" + id + ":" + ex.GetType().Name); }
                    try { tooltip = ConfRoleCreateFeatureEx.tips(item, unit, luck, 2); }
                    catch { /* 原生介绍失败仍可显示基础说明。 */ }
                }
            }
            catch (Exception ex) { failures.Add("配置#" + id + ":" + ex.GetType().Name); }
            if (string.IsNullOrEmpty(tooltip)) tooltip = name + "\n" + description;
            result.Add(new CharacterFate(id, category, name, tooltip, level));
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
