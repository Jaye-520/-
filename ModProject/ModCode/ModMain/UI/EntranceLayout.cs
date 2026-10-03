using System;
using System.Globalization;
using System.IO;

namespace MOD_YtxSQ1.UI
{
    // 仅保存入口布局，不触碰游戏存档。两类入口独立，坐标相对各自原生根矩形。
    internal sealed class EntranceLayout
    {
        internal sealed class Position
        {
            internal bool Custom;   // 用户调整过，需要写回文件
            internal bool Preset;   // 出厂归一化默认，仅在文件没有该入口的值时生效
            internal float X, Y, Size = 56;
        }
        internal readonly Position Map = new Position(), Npc = new Position();
        internal readonly string Path;
        // NPC入口出厂预设：2026-10-03用户在游戏内（2560x1600）微调后记录的归一化中心与尺寸。
        // 主界面入口不设预设，仍按原生念力读数的参照矩形计算。
        private const float NpcPresetX = 0.253515631f, NpcPresetY = 0.750833333f, NpcPresetSize = 56f;
        internal EntranceLayout(string path)
        {
            Path = path;
            Npc.X = NpcPresetX; Npc.Y = NpcPresetY; Npc.Size = NpcPresetSize; Npc.Preset = true;
        }
        internal void Load()
        {
            if (!File.Exists(Path)) return;
            if (new FileInfo(Path).Length > 4096) throw new InvalidDataException("入口布局文件过大。");
            foreach (var line in File.ReadAllLines(Path))
            {
                var parts = line.Split('=');
                if (parts.Length != 2) continue;
                var target = parts[0] == "map" ? Map : parts[0] == "npc" ? Npc : null;
                if (target == null) continue;
                var values = parts[1].Split(',');
                float x, y, size;
                if (values.Length != 3 || !float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                    !float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
                    !float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out size) ||
                    !(x >= 0 && x <= 1 && y >= 0 && y <= 1 && size >= 40 && size <= 100)) continue;
                target.X = x; target.Y = y; target.Size = size; target.Custom = true;
            }
        }
        internal void Save()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            var content = "# 八荒秘录入口位置；x,y为各自UI根矩形内归一化中心；size为图标尺寸\n";
            if (Map.Custom) content += "map=" + Format(Map) + "\n";
            if (Npc.Custom) content += "npc=" + Format(Npc) + "\n";
            var temporary = Path + ".tmp";
            File.WriteAllText(temporary, content, new System.Text.UTF8Encoding(false));
            if (File.Exists(Path)) File.Replace(temporary, Path, Path + ".bak");
            else File.Move(temporary, Path);
        }
        internal static string Format(Position p)
        { return p.X.ToString("R", CultureInfo.InvariantCulture) + "," + p.Y.ToString("R", CultureInfo.InvariantCulture) + "," + p.Size.ToString("R", CultureInfo.InvariantCulture); }
    }
}
