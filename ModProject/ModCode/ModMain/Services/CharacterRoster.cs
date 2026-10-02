using System;
using System.Collections.Generic;

namespace MOD_YtxSQ1.Services
{
    internal sealed class CharacterEntry
    {
        public readonly string Id, Name;
        public readonly bool IsPlayer;
        public CharacterEntry(string id, string name, bool isPlayer) { Id = id; Name = name; IsPlayer = isPlayer; }
    }

    internal static class CharacterRoster
    {
        // 只在打开角色页或手动刷新时获取；UI仅保存ID与姓名，搜索不遍历游戏对象。
        public static bool TryRead(out List<CharacterEntry> entries, out string reason)
        {
            entries = new List<CharacterEntry>();
            reason = "修士列表尚未就绪";
            try
            {
                var player = CharacterReader.Resolve(null);
                if (player == null || g.world.unit == null) return false;
                var ownId = player.data.unitData.unitID;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                Add(entries, seen, player, ownId);
                // 包含隐藏单位，避免把隐身/暂未显示的存活修士漏出总名单。
                var units = g.world.unit.GetUnits(true);
                if (units != null)
                    for (int i = 0; i < units.Count; i++) Add(entries, seen, units[i], ownId);
                entries.Sort((a, b) => a.IsPlayer != b.IsPlayer ? (a.IsPlayer ? -1 : 1) :
                    StringComparer.CurrentCulture.Compare(a.Name, b.Name));
                UnityEngine.Debug.Log("[八荒秘录] 修士名单：含隐藏单位原始数=" + (units == null ? 0 : units.Count) +
                    "；存活有效去重数（含自己）=" + entries.Count);
                reason = null;
                return true;
            }
            catch (Exception ex)
            {
                entries.Clear();
                reason = "修士列表暂不可读取";
                UnityEngine.Debug.LogWarning("[八荒秘录] 列表读取失败：" + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        private static void Add(List<CharacterEntry> entries, HashSet<string> seen, WorldUnitBase unit, string ownId)
        {
            if (unit == null || unit.isDie || unit.data == null || unit.data.unitData == null || unit.data.unitData.propertyData == null) return;
            var id = unit.data.unitData.unitID;
            if (string.IsNullOrEmpty(id) || !seen.Add(id)) return;
            entries.Add(new CharacterEntry(id, unit.data.unitData.propertyData.GetName() ?? "无名修士", id == ownId));
        }
    }
}
