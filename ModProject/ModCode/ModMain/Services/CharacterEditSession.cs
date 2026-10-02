using System;
using System.Collections.Generic;
using System.Globalization;

namespace MOD_YtxSQ1.Services
{
    internal sealed class CharacterField
    {
        internal readonly int Group, Index, Slot;
        internal readonly bool Choice, Name;
        internal string Key { get { return Group + ":" + Index + ":" + Slot; } }
        internal CharacterField(int group, int index, int slot = 0)
        {
            Group = group; Index = index; Slot = slot;
            Choice = group == 0 && (index == 9 || index == 10) || group == 1 && index >= 10;
            Name = group == 0 && index == 11;
        }
        internal static int Parts(int group, int index)
        {
            if (group == 0 && index <= 6) return 2;
            if (group == 0 && index == 10 || group == 1 && index == 10) return 3;
            return 1;
        }
    }

    // 草稿不持有任何游戏引用；跨视图重开保留，世界上下文改变清空。
    internal sealed class CharacterEditSession
    {
        internal sealed class Change
        {
            internal string UnitId, Original, Value;
            internal CharacterField Field;
        }
        internal static readonly CharacterEditSession Shared = new CharacterEditSession();
        private readonly Dictionary<string, Change> changes = new Dictionary<string, Change>();
        internal string Context { get; private set; }
        internal int Count { get { return changes.Count; } }
        internal IEnumerable<Change> Changes { get { return changes.Values; } }
        internal void Sync(string context)
        {
            if (Context == context) return;
            changes.Clear(); Context = context;
        }
        private static string Key(string unit, CharacterField field) { return unit + "/" + field.Key; }
        internal string Value(string unit, CharacterField field, string current)
        {
            Change change;
            return changes.TryGetValue(Key(unit, field), out change) ? change.Value : current;
        }
        internal bool IsChanged(string unit, CharacterField field) { return changes.ContainsKey(Key(unit, field)); }
        internal bool Stage(string unit, CharacterField field, string original, string input, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(Context) || string.IsNullOrEmpty(unit) || original == null)
            { error = "人物信息已失效"; return false; }
            input = (input ?? "").Trim();
            if (field.Name)
            {
                if (input.Length == 0 || input.Length > 24 || input.IndexOfAny(new[] { '\n', '\r', '\t' }) >= 0)
                { error = "姓名请输入1–24个字符"; return false; }
            }
            else
            {
                int value;
                if (!int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value < 0)
                { error = "请输入非负整数，且不超过2147483647"; return false; }
                if (field.Group == 0 && field.Index == 0 && value > int.MaxValue / 12 - 1)
                { error = "年数过大，换算为月会溢出"; return false; }
                input = value.ToString(CultureInfo.InvariantCulture);
            }
            var key = Key(unit, field);
            Change existing;
            if (changes.TryGetValue(key, out existing)) original = existing.Original;
            if (input == original) changes.Remove(key);
            else changes[key] = new Change { UnitId = unit, Field = field, Original = original, Value = input };
            return true;
        }
        internal void Clear() { changes.Clear(); }
        internal bool Validate(Func<Change, string> readCurrent, out string error)
        {
            error = null;
            foreach (var change in changes.Values)
            {
                try
                {
                    var current = readCurrent(change);
                    if (current == null || current != change.Original)
                    { error = "人物数据已变化，请核对“" + CharacterSnapshot.Labels[change.Field.Group][change.Field.Index] + "”后重新编辑"; return false; }
                }
                catch { error = "目标人物已失效，未执行保存"; return false; }
            }
            return true;
        }
    }
}
