using System;
using System.Collections.Generic;

namespace MOD_YtxSQ1.Services
{
    internal sealed class RelationPosition { internal int Column, Row; }
    internal static class RelationLayout
    {
        // Deterministic integer slots: relatives above/below, partners and social links to the sides.
        internal static Dictionary<string, RelationPosition> Arrange(string center, List<RelationLink> links)
        {
            var result = new Dictionary<string, RelationPosition>(StringComparer.Ordinal);
            var occupied = new HashSet<string>(StringComparer.Ordinal); var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            result[center] = new RelationPosition(); occupied.Add("0:0");
            foreach (var link in links)
            {
                RelationPosition source;
                if (!result.TryGetValue(link.From, out source) || result.ContainsKey(link.To)) continue;
                var key = link.From + ":" + link.Type; int count; counts.TryGetValue(key, out count); counts[key] = count + 1;
                int row = source.Row, column = source.Column;
                if (link.Type == 1 || link.Type == 5) { row--; column += count % 2 == 0 ? -1 - count / 2 : 1 + count / 2; }
                else if (link.Type == 2 || link.Type == 3 || link.Type == 6) { row++; column += count == 0 ? 0 : count % 2 == 0 ? -count / 2 : (count + 1) / 2; }
                else if (link.Type == 10) { row--; column -= 3 + count; }
                else if (link.Type == 11) { row++; column -= 3 + count; }
                else if (link.Type == 4 || link.Type == 7) column -= 2 + count;
                else if (link.Type == 8 || link.Type == 9) column += 2 + count;
                else { column += link.Type == 100 ? 5 : -5; row += count; }
                int original = column, attempt = 0;
                while (occupied.Contains(column + ":" + row)) { attempt++; column = original + (attempt % 2 == 0 ? -attempt / 2 : (attempt + 1) / 2); }
                occupied.Add(column + ":" + row); result[link.To] = new RelationPosition { Column = column, Row = row };
            }
            return result;
        }
    }
}
