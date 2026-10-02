using System;
using System.Linq;
using MOD_YtxSQ1.Services;

internal static class CharacterEditingHarness
{
    private static int passed;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
    public static void Main()
    {
        var session = new CharacterEditSession();
        var current = new CharacterField(0, 4, 0); var maximum = new CharacterField(0, 4, 1);
        var hobby1 = new CharacterField(0, 10, 0); var hobby2 = new CharacterField(0, 10, 1);
        string error;
        Check(!session.Stage("player", current, "123", "100", out error), "unavailable world cannot stage values");
        session.Sync("test-world");
        Check(session.Stage("player", current, "123", "100", out error) && session.Stage("player", maximum, "456", "500", out error) && session.Count == 2, "current and maximum have independent draft keys");
        Check(session.Value("player", current, "99") == "100" && session.Value("npc", current, "99") == "99", "refresh overlays drafts only for matching unit and field");
        Check(session.Stage("player", current, "99", "110", out error) && session.Changes.First(c => c.Field.Key == current.Key).Original == "123", "refresh cannot replace original conflict baseline");
        Check(!session.Stage("player", current, "99", "-1", out error) && !session.Stage("player", current, "99", "2147483648", out error) && session.Value("player", current, "99") == "110", "negative and overflow input do not alter existing draft");
        Check(!session.Stage("player", new CharacterField(0, 0), "16", "2147483647", out error), "year conversion rejects month overflow");
        var name = new CharacterField(0, 11);
        Check(session.Stage("player", name, "旧名", "新名字", out error) && !session.Stage("player", name, "旧名", "", out error), "Chinese name drafts validate independently from numeric input");
        Check(session.Stage("player", hobby1, "201", "204", out error) && session.Stage("player", hobby2, "202", "203", out error) && session.Value("player", hobby1, "201") == "204" && session.Value("player", hobby2, "202") == "203", "hobby slots remain independent numeric IDs");
        Check(session.Validate(c => c.Original, out error) && !session.Validate(c => null, out error) && !session.Validate(c => "external change", out error), "preflight rejects missing units and external field changes without mutation");
        Check(session.Stage("player", current, "99", "123", out error) && !session.IsChanged("player", current), "restoring original value removes pending change");
        session.Sync("test-world"); Check(session.Count > 0, "same context retains drafts across window recreation");
        session.Sync("other-world"); Check(session.Count == 0, "new world clears all old drafts");
        session.Stage("player",current,"123","100",out error);
        session.Stage("player",current,"99","110",out error,true);
        Check(session.Changes.First().Original=="99"&&session.Validate(c=>"99",out error),"explicit re-edit can resolve conflict using newly captured live baseline");
        g.world = new World(); g.conf = new Config(); g.isDestroy = false;
        var unit = g.world.playerUnit;
        Check(CharacterEditData.Read(unit, current) == "123" && CharacterEditData.Read(unit, maximum) == "456", "resource components come from independent native field bindings");
        Check(CharacterEditData.Read(unit, new CharacterField(0, 0)) == "16" && CharacterEditData.Read(unit, new CharacterField(1, 8)) == "200", "years and percent editing retain established units");
        Check(CharacterEditData.Read(unit, hobby2) == "202" && CharacterEditData.Label(hobby2, "204") == "图册", "hobby slots read raw IDs and localize choice labels");
        Check(CharacterEditData.Choices(new CharacterField(1, 10, 0), "1").Select(c => c.Id).SequenceEqual(new[] { "1", "4" }) && CharacterEditData.Choices(new CharacterField(1, 10, 1), "2").Select(c => c.Id).SequenceEqual(new[] { "2", "3" }), "trait options match real config type even with nonstandard type IDs");
        Check(CharacterEditData.Choices(new CharacterField(1, 11), "103").Select(c => c.Id).SequenceEqual(new[] { "103", "104" }), "realm options preserve row IDs rather than major-grade ordinals");
        Check(unit.data.dynUnitData.hp.value == 123 && unit.data.unitData.propertyData.hobby[1] == 202 && unit.data.unitData.propertyData.inTrait == 1, "draft and catalog operations never change game data");
        Console.WriteLine(passed + " editing checks passed");
    }
}
