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
        Check(entries[0].Name=="天赋" && entries[0].Description=="天赋\n效果\n第二行","fallback tooltip includes name and plain description");
        Check(entries[1].Description=="恩赐\n暂无说明" && entries[3].Name=="未知气运 #4" && entries[4].Name=="未知气运 #999","missing localization and config remain visible by ID");
        Check(entries[2].Description=="妖术入门\n效果{0}","fallback preserves placeholders without invented values");
        ConfRoleCreateFeatureEx.NativeText="<color=#ffaa00>原生说明</color>";
        Check(CharacterFates.TryRead(null,out entries,out reason) && entries[0].Description==ConfRoleCreateFeatureEx.NativeText,"native formatted rich tooltip preserved");
        Check(ConfRoleCreateFeatureEx.LastUnit==g.world.playerUnit && property.addLuck.Contains(ConfRoleCreateFeatureEx.LastLuck) && ConfRoleCreateFeatureEx.BgType==2,"native formatter receives selected target, owned record and native bgType");
        ConfRoleCreateFeatureEx.NativeText=null;ConfRoleCreateFeatureEx.Throw=true;
        Check(CharacterFates.TryRead(null,out entries,out reason) && entries[0].Description=="天赋\n效果\n第二行","native formatting failure falls back without dropping owned rows");
        ConfRoleCreateFeatureEx.Throw=false;
        var prior = entries;
        var npc = new WorldUnitBase(); npc.data.unitData.unitID="npc"; g.world.unit.Npcs.Add(npc);
        Check(CharacterFates.TryRead("npc",out entries,out reason) && entries.Length==0 && prior.Length==5,"NPC selection reads independent empty snapshot");
        npc.isDie=true;
        Check(!CharacterFates.TryRead("npc",out entries,out reason) && entries.Length==0 && reason!=null,"dead target rejects stale values");
        g.conf.roleCreateFeature.Throw=true;
        Check(CharacterFates.TryRead(null,out entries,out reason) && entries.Length==5 && reason==null && CharacterFates.LastFailure.Contains("配置#1"),"description/config failure retains IDs without claiming missing owned records");
        g.conf.roleCreateFeature.Throw=false; g.conf.roleCreateFeature.ThrowId=3;
        Check(CharacterFates.TryRead(null,out entries,out reason) && entries[0].Name=="天赋" && entries[2].Name=="未知气运 #3" && entries[3].Category==1,"one bad config cannot erase independent rows");
        g.conf.roleCreateFeature.ThrowId=0;
        g.conf.roleCreateFeature.Throw=false;
        Check(property.bornLuck.Length==3 && property.addLuck.Count==5,"reads do not mutate owned records");
        property.bornLuck=new[]{new DataUnit.LuckData{id=1},new DataUnit.LuckData{id=1},new DataUnit.LuckData{id=1}};
        property.addLuck.Add(new DataUnit.LuckData{id=8012523});property.addLuck.Add(new DataUnit.LuckData{id=231125});
        g.conf.roleCreateFeature.Items[8012523]=new FateItem{type=2,conceal=1,name="invalid",tips="invalid"};
        g.conf.roleCreateFeature.Items[231125]=new FateItem{type=2,conceal=1,name="0",tips="0"};
        Check(CharacterFates.TryRead(null,out entries,out reason)&&reason==null&&entries.Length==5&&!entries.Any(e=>e.Id==8012523||e.Id==231125),"DLC hidden runtime effects do not create visible rows or partial warnings");
        g.world.isIntoWorld=false;
        Check(!CharacterFates.TryRead(null,out entries,out reason) && entries.Length==0,"world loading clears snapshot");
        Console.WriteLine(passed+" fate checks passed");
    }
}
