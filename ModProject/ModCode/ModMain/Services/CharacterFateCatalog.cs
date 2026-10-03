using System;
using System.Collections.Generic;

namespace MOD_YtxSQ1.Services
{
    internal static class CharacterFateCatalog
    {
        internal static int Category(int type) { return type == 3 ? 2 : type == 1 || type == 6 || type == 11 ? 0 : 1; }
        internal static string Prefix(int category) { return category == 0 ? "[先] " : category == 2 ? "[逆] " : "[后] "; }
        internal static string ReadName(ConfRoleCreateFeatureItem item) { return Text(item.name, "气运 #" + item.id); }
        internal static List<CharacterFate> Read()
        {
            var result = new List<CharacterFate>();
            var seen = new HashSet<int>();
            var items = g.conf.roleCreateFeature._allConfList;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || item.id <= 0 || item.conceal == 1 || !seen.Add(item.id)) continue;
                var name = Text(item.name, "气运 #" + item.id);
                result.Add(new CharacterFate(item.id, Category(item.type), name, "", item.level));
            }
            return result;
        }
        private static string Text(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key) || key == "0") return fallback;
            try { var text = ConfLocalText.GetText(key); return string.IsNullOrEmpty(text) || text == key ? fallback : text; }
            catch { return fallback; }
        }
        internal static CharacterFate Describe(string unitId, CharacterFate entry)
        {
            try
            {
                var item = g.conf.roleCreateFeature.GetItem(entry.Id);
                var description = ConfRoleCreateFeatureEx.tips(item, CharacterReader.Resolve(unitId), null, 2);
                if (!string.IsNullOrEmpty(description)) return new CharacterFate(entry.Id, entry.Category, entry.Name, description, entry.Level);
            }
            catch { }
            try
            {
                var fallback = g.conf.roleCreateFeature.GetItem(entry.Id);
                return new CharacterFate(entry.Id, entry.Category, entry.Name, fallback == null ? "暂无说明" : Text(fallback.tips, "暂无说明"), entry.Level);
            }
            catch { return new CharacterFate(entry.Id, entry.Category, entry.Name, "说明暂不可读取", entry.Level); }
        }
    }
}
