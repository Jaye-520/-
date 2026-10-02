using System;
using System.Linq;
using System.Collections.Generic;
using MOD_YtxSQ1.Services;

internal static class CharacterFatesHarness
{
    private static int passed;
    private static void Check(bool ok, string message)
    { if (!ok) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
    public static void Main()
    {
        g.world = new World(); g.conf = new Config(); g.isDestroy = false;
        var property = g.world.playerUnit.data.unitData.propertyData;
        property.bornLuck = new[] { new DataUnit.LuckData { id=1 }, null, new DataUnit.LuckData { id=1 } };
        property.addLuck = new List<DataUnit.LuckData> { new DataUnit.LuckData { id=2 }, new DataUnit.LuckData { id=3 }, new DataUnit.LuckData { id=4 }, new DataUnit.LuckData { id=999 }, new DataUnit.LuckData { id=0 } };
        g.conf.roleCreateFeature.Items[1] = new FateItem { type=1, name="f1", tips="t1" };
        g.conf.roleCreateFeature.Items[2] = new FateItem { type=2, name="f2", tips="0" };
        g.conf.roleCreateFeature.Items[3] = new FateItem { type=3, name="f3", tips="t3" };
        g.conf.roleCreateFeature.Items[4] = new FateItem { type=5, name="missing", tips="missing" };
        ConfLocalText.Texts["f1"]="天赋"; ConfLocalText.Texts["f2"]="恩赐"; ConfLocalText.Texts["f3"]="妖术入门";
        ConfLocalText.Texts["t1"]="<color=#ffaa00>效果</color>\\n第二行";
        ConfLocalText.Texts["t3"]="效果{0}";
        CharacterFate[] entries; string reason;
        Check(CharacterFates.TryRead(null,out entries,out reason) && reason==null && entries.Length==5,"null/invalid records skipped and duplicate source IDs deduplicated");
        Check(entries[0].Category==0 && entries[1].Category==1 && entries[2].Category==2 && entries[3].Category==1,"born, acquired, fate and pre-battle classification");
        Check(entries[0].Name=="天赋" && entries[0].Description=="效果\n第二行","localized names and plain description formatting");
        Check(entries[1].Description=="暂无说明" && entries[3].Name=="未知气运 #4" && entries[4].Name=="未知气运 #999","missing localization and config remain visible by ID");
        Check(entries[2].Description=="效果{0}","dynamic placeholders preserved without invented values");
        var prior = entries;
        var npc = new WorldUnitBase(); npc.data.unitData.unitID="npc"; g.world.unit.Npcs.Add(npc);
        Check(CharacterFates.TryRead("npc",out entries,out reason) && entries.Length==0 && prior.Length==5,"NPC selection reads independent empty snapshot");
        npc.isDie=true;
        Check(!CharacterFates.TryRead("npc",out entries,out reason) && entries.Length==0 && reason!=null,"dead target rejects stale values");
        g.conf.roleCreateFeature.Throw=true;
        Check(!CharacterFates.TryRead(null,out entries,out reason) && entries.Length==0,"mid-read failure discards partial list");
        g.conf.roleCreateFeature.Throw=false;
        Check(property.bornLuck.Length==3 && property.addLuck.Count==5,"reads do not mutate owned records");
        g.world.isIntoWorld=false;
        Check(!CharacterFates.TryRead(null,out entries,out reason) && entries.Length==0,"world loading clears snapshot");
        Console.WriteLine(passed+" fate checks passed");
    }
}
