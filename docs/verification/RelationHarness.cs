using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using MOD_YtxSQ1.Services;
using MOD_YtxSQ1.UI;

internal static class RelationHarness
{
    private static int failures;
    private static void Check(bool condition, string name) { Console.WriteLine((condition ? "PASS: " : "FAIL: ") + name); if (!condition) failures++; }
    private static bool Rejected(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
    private static WorldUnitBase Add(string id, string name)
    { var unit = new WorldUnitBase(); unit.data.unitData.unitID = id; unit.data.unitData.propertyData.Name = name; g.world.unit.Npcs.Add(unit); return unit; }
    private static void Reset()
    {
        g.world = new World(); g.data = new DataMgr(); g.conf = new Config(); g.isDestroy = false; DataMgr.isSaveData = false;
        g.world.playerUnit.data.unitData.propertyData.Name = "天人一";
        g.conf.gameParameter.friendCloseRequire = "60"; g.conf.gameParameter.haterCloseRequire = "-60";
        CharacterSave.Sync(CharacterReader.ContextKey()); CharacterEditSession.Shared.Sync(CharacterReader.ContextKey());
    }
    private static RelationPair Pair(string a, string b) { return CharacterRelations.Pair(CharacterReader.ContextKey(), a, b); }
    private static IEnumerable<UnityEngine.Transform> Walk(UnityEngine.Transform node)
    { yield return node; foreach (var child in node.Children) foreach (var next in Walk(child)) yield return next; }
    private static UnityEngine.Transform Find(UnityEngine.Transform root, string name)
    { return Walk(root).First(t => t.gameObject.name == "BHML_" + name); }
    private static void Click(UnityEngine.Transform root, string name)
    { var button = Find(root, name).GetComponent<UnityEngine.UI.Button>(); if (button.interactable) button.onClick.Invoke(); }
    private static T Field<T>(BookView view, string name)
    { return (T)typeof(BookView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view); }
    private static void SavePreview(UnityEngine.RectTransform book, string directory, string name)
    {
        Directory.CreateDirectory(directory);
        using (var bitmap = new Bitmap((int)book.sizeDelta.x, (int)book.sizeDelta.y))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            typeof(BookLayoutHarness).GetMethod("Paint", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { graphics, book, 0f, 0f, true });
            bitmap.Save(Path.Combine(directory, name + ".png"), ImageFormat.Png);
        }
    }
    private static void ServiceTests()
    {
        Reset(); var a = Add("a", "苏勇"); var b = Add("b", "许愉心"); var c = Add("c", "盛语冰");
        var empty = CharacterRelations.ReadGraph("a", new HashSet<string>(), 0);
        Check(empty.People.Count == 1 && empty.Links.Count == 0 && empty.Errors.Count == 0, "empty relation graph has one real center and no invented relatives");
        a.data.unitData.relationData.parent = new[] { "b", "missing" };
        a.data.unitData.relationData.lover.Add("b"); a.data.unitData.relationData.lover.Add("b");
        a.data.unitData.relationData.children.Add("dead");
        a.data.unitData.relationData.intimToUnit["b"] = 60.25f;
        a.data.unitData.relationData.intimToUnit["c"] = -60.5f;
        g.data.unitDie.Entries["dead"] = new DataUnitDie.DieData { unitID = "dead", Name = "故人", sex = UnitSexType.Man };
        b.data.unitData.relationData.parent = new[] { "c", "" };
        c.data.unitData.relationData.lover.Add("a");
        var expanded = new HashSet<string>(); var direct = CharacterRelations.ReadGraph("a", expanded, 0);
        Check(direct.People.Count == 5 && direct.People["dead"].Dead && !direct.People["missing"].Valid, "direct graph retains deceased and missing relation targets");
        Check(direct.Links.Count(l => l.To == "b" && l.Type == 9) == 1 && direct.Links.Any(l => l.To == "b" && l.Type == 100), "deduplicates repeated identity while retaining multiple real identities");
        expanded.Add("b"); expanded.Add("c"); expanded.Add("a");
        var graph = CharacterRelations.ReadGraph("a", expanded, 0);
        Check(graph.Links.Any(l => l.From == "b" && l.To == "c") && graph.People.Count == 5 && graph.Links.Count < 20, "expanded graph follows reachable nodes and terminates cycles");
        Check(CharacterRelations.ReadGraph("a", expanded, 2).Links.All(l => l.Category == 2), "category filter also constrains expanded links");
        expanded.Clear(); Check(!CharacterRelations.ReadGraph("a", expanded, 0).Links.Any(l => l.From == "b"), "collapse removes indirect branches");
        g.conf.gameParameter.friendCloseRequire = "unread";
        var partial = CharacterRelations.ReadGraph("a", expanded, 0);
        Check(partial.Errors.Count > 0 && partial.Links.Any(l => l.Type == 1), "social read failure preserves structural links and reports partial data");
        g.conf.gameParameter.friendCloseRequire = "60";
        var positions = RelationLayout.Arrange("a", graph.Links);
        Check(positions.Count == graph.People.Count && positions.Values.Select(p => p.Column + ":" + p.Row).Distinct().Count() == positions.Count, "layout merges duplicate people and reserves nonoverlapping slots");
        var many = new List<RelationLink>(); for (int i = 0; i < 200; i++) many.Add(new RelationLink { From = "a", To = "friend" + i, Type = 100 });
        Check(RelationLayout.Arrange("a", many).Count == 201, "large graph places every person without silent truncation");

        Reset(); a = Add("a", "苏勇"); b = Add("b", "许愉心");
        a.data.unitData.relationData.intimToUnit["b"] = 1.234567f; b.data.unitData.relationData.intimToUnit["a"] = -7.5f;
        var pair = Pair("a", "b");
        Check(CharacterRelations.Parse(CharacterRelations.Number(pair.AB)) == pair.AB, "float display and input round-trip without losing hidden fractions");
        RelationOperations.SetAffinity(pair, 85.5f, -42.25f);
        Check(Pair("a", "b").AB == 85.5f && Pair("a", "b").BA == -42.25f && CharacterSave.Pending > 0 && g.data.Calls == 0, "NPC pair sets asymmetric native affinity and marks pending without saving");
        pair = Pair("player", "a"); RelationOperations.SetAffinity(pair, 67.5f, 81.25f);
        Check(a.data.unitData.relationData.intimToPlayerUnit == 81.25f && Pair("player", "a").AB == 67.5f, "player reverse uses dedicated affinity field");
        pair = Pair("a", "b"); a.data.unitData.relationData.intimToUnit["b"]++;
        Check(Rejected(() => RelationOperations.SetAffinity(pair, 8, 9)) && Pair("a", "b").BA == -42.25f, "modal refuses changed affinity before any write");
        pair = Pair("a", "b"); b.data.unitData.relationData.IgnoreSet = true; var oldA = pair.AB;
        Check(Rejected(() => RelationOperations.SetAffinity(pair, 10, 20)) && Pair("a", "b").AB == oldA, "failed second side rolls back attempted affinity writes");
        b.data.unitData.relationData.IgnoreSet = false;
        pair = Pair("a", "b"); a.data.unitData.relationData.AfterSet = () => { a.data.unitData.relationData = new DataUnit.RelationData(); };
        Check(Rejected(() => RelationOperations.SetAffinity(pair, 10, 20)) && CharacterSave.Pending > 0 && b.data.unitData.relationData.intimToUnit["a"] == pair.BA, "identity replacement stops second side and preserves uncertain pending");
        Check(Rejected(() => CharacterRelations.Parse("NaN")) && Rejected(() => CharacterRelations.Parse("1e99")), "rejects nonfinite affinity input");
        Check(Rejected(() => CharacterRelations.Parse("1e20")) && Rejected(() => Pair("", "a")), "rejects finite overflow and implicit player aliases");
        pair = Pair("a", "b"); var beforeSave = CharacterRelations.Raw(b.data.unitData.relationData, "a"); DataMgr.isSaveData = true;
        Check(Rejected(() => RelationOperations.SetAffinity(pair, 1, 2)) && CharacterRelations.Raw(b.data.unitData.relationData, "a") == beforeSave, "busy save refuses affinity without mutation"); DataMgr.isSaveData = false;
        Check(Rejected(() => Pair("a", "a")) && Rejected(() => Pair("a", "missing")), "rejects self and unresolved write targets");
        Reset(); a = Add("a", "苏勇"); b = Add("b", "许愉心");
        foreach (var type in new[] { 5, 6, 7, 8, 9, 10, 11 })
        {
            pair = Pair("a", "b"); RelationOperations.SetRelation(pair, type, false);
            Check(Pair("a", "b").Types.Contains(type) && Pair("b", "a").Types.Contains(CharacterRelations.Reverse(type)), "native action readback checks reciprocal type " + type);
            Check(RelationOperations.Unavailable(Pair("a", "b"), type, false) != null, "duplicate relation refused " + type);
            RelationOperations.SetRelation(Pair("a", "b"), type, true);
            Check(Pair("a", "b").Types.Count == 0 && Pair("b", "a").Types.Count == 0, "native removal checks both sides " + type);
        }
        a.data.unitData.relationData.married = "other";
        Check(RelationOperations.Unavailable(Pair("a", "b"), 8, false) != null, "existing spouse never replaced automatically"); a.data.unitData.relationData.married = "";
        RelationOperations.SetRelation(Pair("a", "b"), 9, false); a.data.unitData.relationData.notRelieveUnitsID["b"] = "story";
        Check(RelationOperations.Unavailable(Pair("a", "b"), 9, true) != null, "protected story relation cannot be removed");
        a.data.unitData.relationData.notRelieveUnitsID.Clear(); RelationOperations.SetRelation(Pair("a", "b"), 9, true);
        a.RejectAction = true; int calls = a.ActionCalls;
        Check(Rejected(() => RelationOperations.SetRelation(Pair("a", "b"), 9, false)) && a.ActionCalls == calls, "native precheck refusal avoids invoking mutation action"); a.RejectAction = false;
        pair = Pair("a", "b"); a.data.unitData.relationData.student.Add("other");
        Check(Rejected(() => RelationOperations.SetRelation(pair, 9, false)), "unrelated structural changes invalidate old confirmation");
        a.data.unitData.relationData.student.Clear(); pair = Pair("a", "b"); a.AfterAction = () => { a.data.unitData.relationData = new DataUnit.RelationData(); };
        Check(Rejected(() => RelationOperations.SetRelation(pair, 9, false)) && CharacterSave.Pending > 0, "relation action identity replacement retains pending and forbids blind retry");
        Reset(); a = Add("a", "孩子"); b = Add("b", "父亲"); b.data.unitData.propertyData.sex = UnitSexType.Man;
        RelationOperations.SetRelation(Pair("a", "b"), 1, false);
        Check(a.data.unitData.relationData.parent[0] == "b" && b.data.unitData.relationData.children.Contains("a"), "parent action targets child with verified parent slot and reciprocal readback");
        Check(RelationOperations.Unavailable(Pair("a", "b"), 1, true) != null, "blood relation removal remains unavailable");
        c = Add("c", "另一位父亲"); Check(RelationOperations.Unavailable(Pair("a", "c"), 1, false) != null, "existing family not rebuilt by parent action");
        Reset(); a = Add("a", "孩子"); b = Add("b", "候选父亲"); b.data.unitData.relationData.parent = new[] { "c", "" }; c = Add("c", "祖辈"); c.data.unitData.relationData.parent = new[] { "a", "" };
        Check(RelationOperations.Unavailable(Pair("a", "b"), 1, false) != null, "transitive blood ancestry cycle refused");
    }
    private static void UiTests(string art, string preview)
    {
        PaperSkin.ArtDirectory = art;
        foreach (var size in new[] { new[] { 1920f, 1200f }, new[] { 1600f, 900f }, new[] { 1366f, 768f }, new[] { 960f, 720f } })
        {
            Reset(); var parent = Add("parent", "苏勇"); parent.data.unitData.propertyData.sex = UnitSexType.Man;
            var partner = Add("partner", "许愉心"); var child = Add("child", "盛语冰"); var grand = Add("grand", "伊梁");
            var own = g.world.playerUnit.data.unitData.relationData; own.parent = new[] { "parent", "" }; own.lover.Add("partner"); own.children.Add("child"); own.intimToUnit["partner"] = 90.5f;
            partner.data.unitData.relationData.lover.Add("player"); partner.data.unitData.relationData.intimToPlayerUnit = 72.25f;
            parent.data.unitData.relationData.parent = new[] { "grand", "" }; parent.data.unitData.relationData.children.Add("player");
            for (int i = 0; i < 80; i++) Add("candidate" + i, "候选修士" + i);
            var canvas = new UnityEngine.GameObject("canvas"); canvas.transform.sizeDelta = new UnityEngine.Vector2(size[0], size[1]); g.ui = new LayoutUI { canvas = canvas.transform };
            var window = new UITextInfoLong(); var view = new BookView(); bool closed = false; view.Build(window, () => closed = true);
            var book = (UnityEngine.RectTransform)Find(window.transform, "秘录外框"); Click(book, "卷目3");
            Check(Field<RelationGraph>(view, "relationGraph").People.Count == 4 && Field<string>(view, "selectedUnitId") == "player", "relation page reuses target and reads direct graph at " + size[0]);
            SavePreview(book, preview, "relations-layout-" + size[0]);
            Click(book, "关系人物_parent"); Check(Field<string>(view, "selectedUnitId") == "player", "click selects node without changing center at " + size[0]);
            Click(book, "关系菜单操作5"); Check(Field<RelationGraph>(view, "relationGraph").People.ContainsKey("grand"), "expand adds next generation at " + size[0]);
            Click(book, "关系人物_parent"); Click(book, "关系菜单操作5"); Check(!Field<RelationGraph>(view, "relationGraph").People.ContainsKey("grand"), "collapse hides indirect generation at " + size[0]);
            Click(book, "关系人物_partner"); SavePreview(book, preview, "relations-menu-" + size[0]); Click(book, "关系菜单操作0");
            Check(Find(book, "正向好感").GetComponentInChildren<UnityEngine.UI.InputField>().text == "90.5", "affinity dialog binds captured direction at " + size[0]);
            SavePreview(book, preview, "relations-affinity-" + size[0]);
            Click(book, "同步双向好感"); Find(book, "正向好感").GetComponentInChildren<UnityEngine.UI.InputField>().SetTextWithoutNotify("88.25"); Click(book, "确认双向好感");
            Check(Pair("player", "partner").AB == 88.25f && Pair("player", "partner").BA == 88.25f && g.data.Calls == 0, "sync applies explicit pair without invoking save at " + size[0]);
            Click(book, "添加关系"); SavePreview(book, preview, "relations-picker-" + size[0]);
            var search = Find(book, "关系人物搜索").GetComponentInChildren<UnityEngine.UI.InputField>(); search.text = "candidate79";
            Check(Find(book, "关系候选行0").GetComponentInChildren<UnityEngine.UI.Text>().text == "候选修士79", "independent picker searches ID at " + size[0]);
            Click(book, "关系候选行0"); Check(Field<string>(view, "selectedUnitId") == "player", "picker does not switch center at " + size[0]);
            SavePreview(book, preview, "relations-types-" + size[0]);
            UnityEngine.Input.Escape = true; UnityEngine.Time.frameCount++; view.RefreshCharacter(); UnityEngine.Input.Escape = false;
            Check(Field<UnityEngine.GameObject>(view, "relationOverlay") == null && !closed, "escape closes relation overlay before book at " + size[0]);
            Click(book, "放大关系图"); Check(Field<float>(view, "relationZoom") > .45f, "zoom changes graph scale at " + size[0]);
            Click(book, "关系回到中心"); Check(Field<float>(view, "relationZoom") <= 1, "return to center fits direct graph at " + size[0]);
            var page = Field<UnityEngine.RectTransform>(view, "relationPage"); var oldPosition = page.anchoredPosition;
            UnityEngine.Input.mousePosition = new UnityEngine.Vector2(10, -10); UnityEngine.Input.MouseDown = true;
            Find(book, "关系图拖动区域").GetComponent<UIEventListener>().onPointDown.Invoke();
            UnityEngine.Input.mousePosition = new UnityEngine.Vector2(40, -25); view.RefreshCharacter(); UnityEngine.Input.MouseDown = false; view.RefreshCharacter();
            Check(Math.Abs(page.anchoredPosition.x - oldPosition.x - 30) < .01f && Math.Abs(page.anchoredPosition.y - oldPosition.y + 15) < .01f, "background drag moves graph in local canvas coordinates at " + size[0]);
            Click(book, "关系人物_partner"); Click(book, "关系菜单外部"); Check(Field<UnityEngine.GameObject>(view, "relationOverlay") == null, "outside click closes node menu at " + size[0]);
            g.data.ImmediateResult = true; Click(book, "卷目6"); CharacterSave.Poll();
            Check(g.data.Calls == 1 && CharacterSave.Pending == 0, "top save persists relation pending through existing callback flow at " + size[0]);
            Click(book, "关系人物_partner"); Click(book, "关系菜单操作4"); Check(Field<string>(view, "selectedUnitId") == "partner", "explicit center action switches shared target at " + size[0]);
            view.FocusTarget("player"); Click(book, "关系人物_partner"); own.intimToUnit["partner"] = 100; view.RefreshCharacter(true);
            Check(Field<UnityEngine.GameObject>(view, "relationOverlay") == null, "native relation refresh closes stale menu at " + size[0]);
            Click(book, "关系人物_partner"); Click(book, "关系菜单操作3"); Check(Field<int>(view, "selectedIndex") == 0 && Field<string>(view, "selectedUnitId") == "partner", "view person opens attributes for clicked identity at " + size[0]);
            view.FocusTarget("player"); Click(book, "卷目3");
            for (int tick = 0; tick < 8; tick++) view.RefreshCharacter();
            Check(ModelRenderTexture.Active == 1 && Field<Dictionary<string, UnityEngine.Texture2D>>(view, "relationTextures").Count > 0, "thumbnail snapshots leave only shared portrait renderer at " + size[0]);
            typeof(BookView).GetMethod("ClearRelationTextures", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            PortraitModel.DeferCallbacks = true; view.RefreshCharacter(); view.RefreshCharacter();
            Check(Field<Dictionary<string, UnityEngine.Texture2D>>(view, "relationTextures").Count == 0 && PortraitModel.PendingCallbacks.Count == 1, "incomplete model is never cached and only one job loads at " + size[0]);
            foreach(var complete in PortraitModel.PendingCallbacks.ToArray()) complete(); PortraitModel.PendingCallbacks.Clear();
            Check(Field<Dictionary<string, UnityEngine.Texture2D>>(view, "relationTextures").Count == 0, "completion waits for next UI tick before capture at " + size[0]);
            view.RefreshCharacter();
            var miniature = Field<Dictionary<string, UnityEngine.Texture2D>>(view, "relationTextures").Values.Single();
            Check(miniature.Width == 160 && miniature.Height == 154 && Math.Abs(ModelRenderTexture.LastCamera.aspect - 80f/77) < .001f && ModelRenderTexture.LastCamera.orthographicSize >= 280, "completed miniature uses card aspect and full body margins at " + size[0]);
            Check(!ModelRenderTexture.LastCamera.ModelAttachedAtRender && Math.Abs(ModelRenderTexture.LastCamera.CapturedCenterY) < .001f, "native camera parenting is detached before fit so subject remains centered at " + size[0]);
            view.RefreshCharacter(); Check(PortraitModel.PendingCallbacks.Count == 1, "next model waits independently at " + size[0]);
            Click(book, "卷目0");
            foreach(var complete in PortraitModel.PendingCallbacks.ToArray()) complete(); PortraitModel.PendingCallbacks.Clear();
            Check(Field<Dictionary<string, UnityEngine.Texture2D>>(view, "relationTextures").Count == 0 && Field<object>(view, "relationPictureJob") == null && ModelRenderTexture.Active == 1, "late callback after page change cannot capture old person at " + size[0]);
            PortraitModel.DeferCallbacks = false; Click(book, "卷目3");
            g.conf.dramaNpc.Ids["partner"] = 8101; g.conf.dramaNpc.Portraits[8101] = new PortraitModelData { body = 9001 }; view.RefreshCharacter(true);
            for (int tick = 0; tick < 8; tick++) view.RefreshCharacter();
            Check(ConfDramaNpc.LastNativeId == 8101 && ModelRenderTexture.Active == 1 && Field<Dictionary<string, UnityEngine.Texture2D>>(view, "relationTextures").ContainsKey("partner"), "special identity thumbnail uses native prefab and releases transient renderer at " + size[0]);
            var baselineInputs = WindowInputScope.Registered.Count; Click(book, "关系人物_partner"); Click(book, "关系菜单操作0"); view.FocusTarget("child");
            Check(Field<UnityEngine.GameObject>(view, "relationOverlay") == null && WindowInputScope.Registered.Count == baselineInputs, "switching target cancels affinity inputs and captured confirmation at " + size[0]);
            view.Dispose(); Check(WindowInputScope.Registered.Count == 0 && ModelRenderTexture.Active == 0 && UnityEngine.Texture2D.LiveProcedural == 0, "dispose releases all relation inputs and thumbnail textures at " + size[0]);
        }
    }
    public static int Main(string[] args)
    {
        ServiceTests(); UiTests(args[0], args[1]);
        Console.WriteLine("Relation managed tests complete; native events, portraits and game save reload remain unverified.");
        return failures == 0 ? 0 : 1;
    }
}
