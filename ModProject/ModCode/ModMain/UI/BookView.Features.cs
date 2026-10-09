using System;
using System.Collections.Generic;
using MOD_YtxSQ1.Services;
using UnityEngine;
using UnityEngine.UI;

namespace MOD_YtxSQ1.UI
{
    internal sealed partial class BookView
    {
        private sealed class FeatureRow
        { internal WorkshopFeature Definition; internal Button Button; internal Text Label; internal string Reason; }
        private readonly List<FeatureRow> featureRows = new List<FeatureRow>();
        private GameObject featureOverlay;
        private Text featureSchool, featureNote;
        private InputField featureTitle, featureCommand, featureDrama;
        private Button featureTitleRun, featureCommandRun, featureDramaRun, featureLeftButton, featureRightButton;
        private string featureLeftId, featureRightId;
        private void BuildFeatures()
        {
            float gap = 12, width = (contentWidth - gap * 2) / 3, h = contentHeight - 86;
            var assists = FeatureColumn("战斗与辅助", 0, width, h);
            assists.content.sizeDelta = new Vector2(assists.content.sizeDelta.x, WorkshopFeatures.Assists.Length * 42);
            for (int i = 0; i < WorkshopFeatures.Assists.Length; i++)
                AddFeatureRow(assists.content, WorkshopFeatures.Assists[i], 0, i * 42, assists.content.sizeDelta.x - 18);
            assists.content.sizeDelta = new Vector2(assists.content.sizeDelta.x, WorkshopFeatures.Assists.Length * 42);
            var operations = FeatureColumn("人物与宗门", width + gap, width, h);
            operations.content.sizeDelta = new Vector2(operations.content.sizeDelta.x, ((WorkshopFeatures.Interactions.Length + 1) / 2) * 42 + 64 + WorkshopFeatures.Schools.Length * 42);
            float rowWidth = (operations.content.sizeDelta.x - 24) / 2;
            for (int i = 0; i < WorkshopFeatures.Interactions.Length; i++)
                AddFeatureRow(operations.content, WorkshopFeatures.Interactions[i], (i % 2) * (rowWidth + 6), (i / 2) * 42, rowWidth);
            float y = ((WorkshopFeatures.Interactions.Length + 1) / 2) * 42 + 12;
            featureSchool = Label(operations.content, "宗门 · 待读取", 0, y, width - 32, 44, 16, Ink); y += 52;
            for (int i = 0; i < WorkshopFeatures.Schools.Length; i++)
                AddFeatureRow(operations.content, WorkshopFeatures.Schools[i], 0, y + i * 42, operations.content.sizeDelta.x - 18);
            operations.content.sizeDelta = new Vector2(operations.content.sizeDelta.x, y + WorkshopFeatures.Schools.Length * 42);
            var tools = Card(content, "原生工具", (width + gap) * 2, 76, width, h);
            Label(tools, "原生工具", 12, 8, width - 24, 30, 20, Ink);
            var scroll = ItemScroll(tools, "原生工具滚动", 12, 46, width - 24, h - 58); AddMartialScrollbar(scroll);
            var host = scroll.content; host.sizeDelta = new Vector2(host.sizeDelta.x, 620); float w = host.sizeDelta.x - 18;
            Label(host, "玩家道号（显示名）", 0, 0, w, 28, 17, Ink);
            featureTitle = CreateSearchInput(host, "功能道号输入", 0, 34, w, 36, "输入道号", _ => { });
            featureTitleRun = MakeButton(host, "道号执行", 0, 78, w, 32, "执行", () => StartFeatureTool("title"));
            BindFateHover(featureTitleRun.gameObject, () => FeatureHelp(featureTitleRun.GetComponent<RectTransform>(), "自定义道号", "玩家自定义显示道号，最多24字；原生道号效果不变。留空恢复原生显示。"), () => fateTipCloseAt = Time.unscaledTime + .12f);
            Label(host, "指令触发", 0, 126, w, 28, 17, Ink);
            featureCommand = CreateSearchInput(host, "原生命令输入", 0, 160, w, 36, "例如 openUI_UIPlayerInfo", _ => { }); featureCommand.characterLimit = 512;
            featureCommandRun = MakeButton(host, "原生命令执行", 0, 204, w, 32, "执行指令", () => StartFeatureTool("command"));
            BindFateHover(featureCommandRun.gameObject, () => FeatureHelp(featureCommandRun.GetComponent<RectTransform>(), "指令触发", FeatureOperations.Availability("command", selectedUnitId) ?? "只执行一次；未知命令不重试。"), () => fateTipCloseAt = Time.unscaledTime + .12f);
            Label(host, "剧情命令", 0, 256, w, 28, 17, Ink);
            featureLeftButton = MakeButton(host, "剧情左角色", 0, 292, w, 32, "选择左角色", () => PickFeatureRole(true));
            featureDrama = CreateSearchInput(host, "原生剧情ID", 0, 332, w, 36, "剧情ID", _ => { }); featureDrama.characterLimit = 10;
            featureRightButton = MakeButton(host, "剧情右角色", 0, 376, w, 32, "选择右角色", () => PickFeatureRole(false));
            featureDramaRun = MakeButton(host, "原生剧情执行", 0, 416, w, 32, "执行剧情", () => StartFeatureTool("drama"));
            BindFateHover(featureDramaRun.gameObject, () => FeatureHelp(featureDramaRun.GetComponent<RectTransform>(), "剧情命令", FeatureOperations.Availability("drama", selectedUnitId) ?? (CharacterReader.Resolve(featureLeftId) == null || CharacterReader.Resolve(featureRightId) == null ? "左右角色失效，请重新选择" : "输入有效剧情ID并核对左右角色后单次执行。")), () => fateTipCloseAt = Time.unscaledTime + .12f);
            featureNote = Label(host, "持续辅助仅玩家；关窗保持，退档清空。\n◆ 悬停查看条件与限制。\n灰色项目不执行写入。\n原生操作可能触发自动保存。", 0, 470, w, 140, 15, Muted);
            host.sizeDelta = new Vector2(host.sizeDelta.x, 620);
        }
        private ScrollRect FeatureColumn(string title, float x, float width, float height)
        {
            var panel = Card(content, title, x, 76, width, height);
            Label(panel, title, 12, 8, width - 24, 30, 20, Ink);
            var scroll = ItemScroll(panel, title + "滚动", 12, 46, width - 24, height - 58); AddMartialScrollbar(scroll); return scroll;
        }
        private void AddFeatureRow(RectTransform parent, WorkshopFeature definition, float x, float y, float width)
        {
            var row = new FeatureRow { Definition = definition };
            row.Button = MakeButton(parent, "功能_" + definition.Id, x, y, width, 34, definition.Name, () => StartFeature(definition));
            row.Label = row.Button.GetComponentInChildren<Text>(); row.Label.resizeTextForBestFit = true;
            row.Label.resizeTextMinSize = 12; row.Label.resizeTextMaxSize = 17;
            Label(row.Button.GetComponent<RectTransform>(), "◆", width - 12, 2, 9, 8, 6, Muted, TextAnchor.MiddleCenter);
            BindFateHover(row.Button.gameObject, () => FeatureHelp(row.Button.GetComponent<RectTransform>(), definition.Name,
                definition.Hint + (row.Reason == null ? "" : "\n不可用：" + row.Reason)), () => fateTipCloseAt = Time.unscaledTime + .12f);
            featureRows.Add(row);
        }
        private void FeatureHelp(RectTransform anchor, string name, string hint)
        { ShowFateTip(anchor, new CharacterFate(0, 0, name, hint ?? ""), contentHost.parent.GetComponent<RectTransform>(), true); }
        private void RefreshFeatures(bool force)
        {
            if (selectedIndex != 4 || suspended) return;
            foreach (var row in featureRows)
            {
                row.Reason = FeatureOperations.Availability(row.Definition.Id, selectedUnitId);
                row.Button.interactable = row.Reason == null;
                bool on = row.Definition.Toggle && FeatureRuntime.Enabled(row.Definition.Id);
                row.Label.text = (row.Definition.Toggle ? (on ? "■ " : "□ ") : "") + row.Definition.Name;
                row.Button.GetComponent<Image>().color = row.Reason != null ? new Color(.64f, .63f, .59f, .35f)
                    : on ? new Color(.69f, .74f, .57f, .65f) : new Color(.92f, .91f, .87f, .7f);
            }
            featureSchool.text = "宗门 · " + FeatureOperations.SchoolSummary(selectedUnitId);
            featureTitleRun.interactable = FeatureOperations.Availability("title", CharacterReader.Resolve(null)?.data.unitData.unitID) == null; featureTitle.interactable = featureTitleRun.interactable;
            var reason = FeatureOperations.Availability("command", selectedUnitId);
            featureCommandRun.interactable = reason == null;
            if (featureLeftId == null) featureLeftId = CharacterReader.Resolve(null)?.data.unitData.unitID;
            if (featureRightId == null && CharacterReader.Resolve(selectedUnitId) != null) featureRightId = selectedUnitId;
            featureDramaRun.interactable = reason == null && CharacterReader.Resolve(featureLeftId) != null && CharacterReader.Resolve(featureRightId) != null;
            featureLeftButton.GetComponentInChildren<Text>().text = "左 · " + FeatureRoleName(featureLeftId);
            featureRightButton.GetComponentInChildren<Text>().text = "右 · " + FeatureRoleName(featureRightId);
            featureNote.text = FeatureRuntime.Error ?? "持续辅助仅玩家；关窗保持，退档清空。\n◆ 悬停查看条件与限制。\n灰色项目不执行写入。\n原生操作可能触发自动保存。";
        }
        private static string FeatureRoleName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "未选择";
            var unit = CharacterReader.Resolve(id); return unit == null ? "已失效" : unit.data.unitData.propertyData.GetName();
        }
        private bool CanStartFeature()
        {
            if (!FinishInlineEdit(false) || !string.IsNullOrEmpty(Input.compositionString)) return false;
            if (edits.Count > 0) { SetEditStatus("请先保存属性草稿，再执行功能操作", true); return false; }
            return true;
        }
        private void StartFeature(WorkshopFeature definition)
        {
            if (!CanStartFeature()) return;
            try
            {
                var reason = FeatureOperations.Availability(definition.Id, selectedUnitId); if (reason != null) throw new InvalidOperationException(reason);
                if (definition.Toggle)
                { FeatureRuntime.Toggle(definition.Id); RefreshFeatures(true); SetEditStatus(definition.Name + (FeatureRuntime.Enabled(definition.Id) ? "已开启 · 仅玩家" : "已关闭"), false); return; }
                if (definition.Id == "dead") { ShowDeathList(); return; }
                if (definition.Id == "give" || definition.Id == "ask") { PickFeatureItem(definition); return; }
                if (definition.Id == "join") { PickFeatureSchool(); return; }
                if (definition.Id == "post") { PickFeaturePost(); return; }
                if (definition.Id == "teach") { OpenNativeTeach(); return; }
                ConfirmFeature(FeatureOperations.Prepare(definition.Id, selectedUnitId), definition.Name, definition.Hint);
            }
            catch (Exception ex) { SetEditStatus(ex.Message, true); }
        }
        private RectTransform FeatureDialog(string title)
        {
            CloseFeatureOverlay(); HideFateTips();
            float width = Mathf.Min(620, contentWidth - 20), height = Mathf.Min(510, contentHeight - 20);
            var layer = Box("功能确认层", content, 0, 0, contentWidth, contentHeight); featureOverlay = layer.gameObject;
            var veil = Fill(layer, "功能遮罩", 0, 0, contentWidth, contentHeight, new Color(0, 0, 0, .15f), true);
            var outside = veil.gameObject.AddComponent<Button>(); outside.targetGraphic = veil;
            outside.onClick.AddListener((UnityEngine.Events.UnityAction)(new Action(CloseFeatureOverlay)));
            var panel = Card(layer, "功能操作", (contentWidth - width) / 2, (contentHeight - height) / 2, width, height);
            Fill(panel, "功能操作纸底", 0, 0, width, height, Paper, true); Border(panel, width, height);
            Label(panel, title, 14, 8, width - 80, 36, 21, Ink);
            MakeButton(panel, "关闭功能操作", width - 60, 8, 44, 32, "×", CloseFeatureOverlay); return panel;
        }
        private void ConfirmFeature(FeatureRequest request, string title, string hint)
        {
            var panel = FeatureDialog(title); float w = panel.sizeDelta.x, h = panel.sizeDelta.y;
            string detail = "操作对象：" + FeatureRoleName(request.TargetId) + "\n" + hint;
            if (request.Id == "command") detail += "\n指令：" + request.Payload;
            if (request.Id == "drama") detail += "\n剧情ID：" + request.Payload + "\n左：" + FeatureRoleName(request.LeftId) + "\n右：" + FeatureRoleName(request.RightId);
            if (request.Id == "capacity") detail += "\n容量：" + request.BeforeCapacity + " → " + WorkshopFeatures.CapacityTarget(request.BeforeCapacity) + "\n扩容记录随保存保留，加载本MOD时恢复；卸载后回到原生容量。";
            Label(panel, detail + "\n\n执行后可能即时改变内存并触发原生自动保存。\n原生复杂动作不保证回滚，失败不自动重复。", 16, 62, w - 32, h - 140, 16, Ink);
            MakeButton(panel, "确认功能操作", 16, h - 58, w - 32, 36, "确认执行", () => RunFeature(request));
        }
        private void RunFeature(FeatureRequest request)
        {
            if (!string.IsNullOrEmpty(Input.compositionString)) return;
            try
            {
                FeatureOperations.Ensure(request); CloseFeatureOverlay();
                if (request.Id != "capacity" && requestClose != null) requestClose();
                string message = FeatureOperations.Execute(request);
                if (!suspended) { RefreshCharacter(true); SetEditStatus(message, false); }
                UITipItem.AddTip(message, 3f);
            }
            catch (Exception ex)
            { Debug.LogError("[八荒秘录] 功能执行失败 · " + request.Id + "：" + ex); if (!suspended) SetEditStatus("未完成：" + ex.Message, true); UITipItem.AddTip("功能未完成：" + ex.Message + "；请核对游戏状态", 4f); }
        }
        private void StartFeatureTool(string id)
        {
            if (!CanStartFeature()) return;
            try
            {
                var request = FeatureOperations.Prepare(id, id == "title" ? CharacterReader.Resolve(null)?.data.unitData.unitID : selectedUnitId, id == "title" ? featureTitle.text : id == "command" ? featureCommand.text : featureDrama.text,
                    leftId: id == "drama" ? featureLeftId : null, rightId: id == "drama" ? featureRightId : null);
                ConfirmFeature(request, id == "title" ? "设置玩家显示道号" : id == "command" ? "原生指令" : "原生剧情", id == "title" ? "显示名：" + request.Payload + "；保留原生道号的属性和归属，留空恢复原生显示。" : "按所选角色执行原生流程，结果需在游戏中核对。");
            }
            catch (Exception ex) { SetEditStatus(ex.Message, true); }
        }
        private void FeaturePicker(string title, List<FeatureChoice> entries, Action<FeatureChoice> choose)
        {
            var panel = FeatureDialog(title); float w = panel.sizeDelta.x, h = panel.sizeDelta.y;
            var scroll = ItemScroll(panel, "功能候选滚动", 16, 106, w - 32, h - 128); AddMartialScrollbar(scroll);
            var filtered = new List<FeatureChoice>(entries); var rows = new List<Button>();
            Action render = () =>
            {
                int first = Math.Max(0, (int)(scroll.content.anchoredPosition.y / 38));
                for (int slot = 0; slot < rows.Count; slot++)
                {
                    int index = first + slot; var button = rows[slot]; button.gameObject.SetActive(index < filtered.Count);
                    if (index >= filtered.Count) continue;
                    button.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -index * 38);
                    button.GetComponentInChildren<Text>().text = filtered[index].Name;
                }
            };
            var search = CreateSearchInput(panel, "功能候选检索", 16, 58, w - 32, 36, "按名称或ID检索", value =>
            {
                filtered.Clear(); foreach (var entry in entries)
                    if (string.IsNullOrWhiteSpace(value) || entry.Name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 || entry.Id.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0) filtered.Add(entry);
                scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Math.Max(1, filtered.Count * 38));
                scroll.content.anchoredPosition = Vector2.zero; scroll.verticalNormalizedPosition = 1; render();
            });
            search.characterLimit = 100;
            int count = (int)Math.Ceiling(scroll.viewport.sizeDelta.y / 38) + 1;
            for (int i = 0; i < count; i++)
            {
                int slot = i;
                rows.Add(MakeButton(scroll.content, "功能候选行" + i, 0, 0, scroll.content.sizeDelta.x - 18, 34, "", () =>
                {
                    int index = Math.Max(0, (int)(scroll.content.anchoredPosition.y / 38)) + slot;
                    if (index < filtered.Count) { try { choose(filtered[index]); } catch (Exception ex) { CloseFeatureOverlay(); SetEditStatus(ex.Message, true); } }
                }));
            }
            scroll.onValueChanged.AddListener((UnityEngine.Events.UnityAction<Vector2>)(new Action<Vector2>(_ => render())));
            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Math.Max(1, filtered.Count * 38)); render();
        }
        private void PickFeatureRole(bool left)
        {
            var expectedContext = contextKey; var entries = new List<FeatureChoice>();
            foreach (var person in roster) entries.Add(new FeatureChoice(person.Id, person.Name));
            FeaturePicker(left ? "剧情左角色" : "剧情右角色", entries, choice =>
            {
                if (CharacterReader.ContextKey() != expectedContext || CharacterReader.Resolve(choice.Id) == null) throw new InvalidOperationException("人物或世界已变化");
                if (left) featureLeftId = choice.Id; else featureRightId = choice.Id;
                CloseFeatureOverlay(); RefreshFeatures(true);
            });
        }
        private void PickFeatureItem(WorkshopFeature definition)
        {
            var target = selectedUnitId; var context = contextKey;
            var origin = FeatureOperations.Prepare(definition.Id == "give" ? "give" : "ask", target, ItemInventory.Read(definition.Id == "give" ? null : target, false).Find(item => !string.IsNullOrEmpty(item.SoleId))?.SoleId);
            var choices = new List<FeatureChoice>();
            foreach (var item in ItemInventory.Read(definition.Id == "give" ? null : target, false))
                if (!string.IsNullOrEmpty(item.SoleId)) choices.Add(new FeatureChoice(item.SoleId, item.Name + " ×" + item.Count));
            FeaturePicker(definition.Name + " · 选择一件", choices, choice =>
            {
                if (contextKey != context || selectedUnitId != target) throw new InvalidOperationException("操作对象已切换");
                FeatureOperations.Ensure(origin);
                ConfirmFeature(FeatureOperations.Prepare(definition.Id, target, choice.Id), definition.Name, choice.Name + "\n本次数量：1；保留原生物品转移规则。");
            });
        }
        private void PickFeatureSchool()
        {
            var origin = FeatureOperations.Prepare("join", selectedUnitId, schoolId: FeatureOperations.SchoolChoices().Find(_ => true)?.Id);
            FeaturePicker("选择加入的宗门", FeatureOperations.SchoolChoices(), choice =>
            { FeatureOperations.Ensure(origin); ConfirmFeature(FeatureOperations.Prepare("join", origin.TargetId, schoolId: choice.Id), "加入宗门", "以外门身份加入 " + choice.Name); });
        }
        private void PickFeaturePost()
        {
            var origin = FeatureOperations.Prepare("post", selectedUnitId);
            var choices = new List<FeatureChoice> {
                new FeatureChoice("Out", "外门弟子"), new FeatureChoice("In", "内门弟子"), new FeatureChoice("Inherit", "真传弟子"),
                new FeatureChoice("Elders", "长老"), new FeatureChoice("BigElders", "大长老"), new FeatureChoice("SchoolMain", "宗主") };
            FeaturePicker("选择宗门职位", choices, choice =>
            {
                FeatureOperations.Ensure(origin); var post = (SchoolPostType)Enum.Parse(typeof(SchoolPostType), choice.Id);
                if (post == SchoolPostType.Elders || post == SchoolPostType.BigElders)
                {
                    var departments = new List<FeatureChoice> { new FeatureChoice("Personnel", "人事"), new FeatureChoice("Security", "戒律"), new FeatureChoice("Diplomacy", "外交"), new FeatureChoice("Constructor", "建设"), new FeatureChoice("Train", "训练") };
                    FeaturePicker("选择职位部门", departments, department =>
                    { FeatureOperations.Ensure(origin); ConfirmFeature(FeatureOperations.Prepare("post", origin.TargetId, post: post, department: (SchoolDepartmentType)Enum.Parse(typeof(SchoolDepartmentType), department.Id)), "设置职位", choice.Name + " · " + department.Name + "；采用原生管理职位设置，保留成员维护、名额与玩家职位上限，不清退其他人物。"); });
                }
                else ConfirmFeature(FeatureOperations.Prepare("post", origin.TargetId, post: post), "设置职位", choice.Name + "；采用原生管理职位设置，保留成员维护、名额与玩家职位上限，不清退其他人物。");
            });
        }
        private void ShowDeathList()
        {
            FeaturePicker("死亡名单 · 只读", FeatureOperations.DeathChoices(), choice =>
            {
                var panel = FeatureDialog(choice.Name); Label(panel, "人物ID：" + choice.Id + "\n\n死亡记录与操作对象分离，不直接恢复已移除人物。\n原生复活入口仅支持仍有原生实例的鬼魂。", 16, 66, panel.sizeDelta.x - 32, panel.sizeDelta.y - 90, 16, Ink);
            });
        }
        private void OpenNativeTeach()
        {
            var request = FeatureOperations.Prepare("teach", selectedUnitId);
            if (requestClose != null) requestClose();
            var player = CharacterReader.Resolve(request.PlayerId);
            var ui = g.ui.OpenUI<UITeachSkill>(UIType.TeachSkill);
            bool submitted = false;
            ui.InitData(player, (Il2CppSystem.Action<DataUnit.ActionMartialData>)(new Action<DataUnit.ActionMartialData>(martial =>
            {
                try
                {
                    if (submitted) return;
                    FeatureOperations.Ensure(request);
                    if (martial == null || martial.data == null || CharacterReader.Resolve(request.PlayerId).data.unitData.GetActionMartial(martial.data.soleID)?.Pointer != martial.Pointer)
                        throw new InvalidOperationException("功法记录已变化");
                    submitted = true;
                    var operation = FeatureOperations.Prepare("teach", request.TargetId, martial.data.soleID);
                    UITipItem.AddTip(FeatureOperations.Execute(operation), 3f);
                }
                catch (Exception ex) { Debug.LogError("[八荒秘录] 教功失败：" + ex); UITipItem.AddTip("教功未完成：" + ex.Message, 4f); }
            })));
        }
        private void CloseFeatureOverlay()
        {
            var previous = featureOverlay; featureOverlay = null;
            if (previous == null) return;
            foreach (var input in previous.GetComponentsInChildren<InputField>(true)) WindowInputScope.Unregister(input);
            ReleaseViewObject(previous, "功能浮层");
        }
        private void InvalidateFeatures()
        { CloseFeatureOverlay(); featureLeftId = featureRightId = null; }
        private void DisposeFeatures()
        { CloseFeatureOverlay(); featureRows.Clear(); featureSchool = featureNote = null; featureTitle = featureCommand = featureDrama = null; featureTitleRun = featureCommandRun = featureDramaRun = featureLeftButton = featureRightButton = null; }
    }
}
