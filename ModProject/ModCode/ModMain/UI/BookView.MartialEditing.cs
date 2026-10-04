using System;
using MOD_YtxSQ1.Services;
using UnityEngine;
using UnityEngine.UI;

namespace MOD_YtxSQ1.UI
{
    internal sealed partial class BookView
    {
        private bool RunMartialEdit(Func<string> operation)
        {
            if (!FinishInlineEdit(false)) return false;
            if (edits.Count > 0) { SetEditStatus("请先保存属性修改，再修改功法", true); return false; }
            var pending = CharacterSave.Pending;
            var selectionId = martialSelection?.SoleId;
            try
            {
                var message = operation(); CloseMartialOverlay(); RefreshMartials(true); SetEditStatus(message);
                if (martialCompact && martialSelection != null && martialSelection.SoleId == selectionId)
                { martialListPanel.gameObject.SetActive(false); martialDetailPanel.gameObject.SetActive(true); }
                return true;
            }
            catch (Exception ex)
            {
                if (!(ex is InvalidOperationException)) Debug.LogError("[八荒秘录] 功法操作异常：" + ex);
                if (CharacterSave.Pending != pending) { CloseMartialOverlay(); RefreshMartials(true); }
                else if (martialLibraryStatus != null) martialLibraryStatus.text = ex.Message;
                SetEditStatus(ex.Message, true); return false;
            }
        }
        private bool MartialEditTarget()
        {
            if (selectedIndex == 2 && martialLibraryOwner == selectedUnitId && martialLibraryContext == contextKey) return true;
            CloseMartialOverlay(); SetEditStatus("人物或世界已变化，请重新选择功法", true); return false;
        }
        private void OpenMartialStudy(MartialEntry entry)
        {
            try
            {
                var panel = MartialOverlay("学习秘籍 · " + entry.Name); var width = panel.sizeDelta.x; var height = panel.sizeDelta.y;
                Label(panel, "将这一本背包秘籍转为当前人物的已学功法。\n\n保留境界、品质、词条和传承身份；消耗这本秘籍。\n直接完成学习，不消耗学习小游戏的时间和灵石。\n境界、资质及已学容量仍遵循游戏规则。\n\n学会后可单独修炼、装备和参悟。", 16, 56, width - 32, height - 152, 17, Ink);
                martialLibraryStatus = Label(panel, "", 16, height - 88, width - 32, 28, 14, Cinnabar);
                MakeButton(panel, "确认学会秘籍", 16, height - 48, width - 32, 32, "学会并消耗这本秘籍", () =>
                { if (MartialEditTarget()) RunMartialEdit(() => CharacterMartials.Study(martialLibraryContext, martialLibraryOwner, entry)); });
            }
            catch (Exception ex) { CloseMartialOverlay(); SetEditStatus(ex.Message, true); }
        }
        private DataProps.PropsData martialDraft;
        private MartialEntry martialDraftEntry;
        private string martialDraftOwner, martialDraftContext;
        private bool martialDraftChanged;
        private void ResetMartialDraft()
        { martialDraft = null; martialDraftEntry = null; martialDraftOwner = null; martialDraftContext = null; martialDraftChanged = false; }
        private void BeginMartialDraft(MartialEntry entry)
        {
            if (martialDraft == null || martialDraftOwner != selectedUnitId || martialDraftContext != contextKey || martialDraftEntry.SoleId != entry.SoleId
                || martialDraftEntry.Signature != entry.Signature || martialDraftEntry.Exp != entry.Exp)
            {
                ResetMartialDraft(); martialDraft = CharacterMartials.Draft(selectedUnitId, entry);
                martialDraftOwner = selectedUnitId; martialDraftContext = contextKey;
            }
            martialDraftEntry = entry;
        }
        private void SelectMartialPrefix(MartialEntry entry, MartialPrefixEntry original, MartialPrefixEntry candidate, int quality)
        {
            if (!MartialEditTarget()) return;
            try
            {
                if (martialDraftEntry == null || martialDraftEntry.SoleId != entry.SoleId || martialDraftEntry.Signature != entry.Signature || martialDraftEntry.Exp != entry.Exp)
                    throw new InvalidOperationException("功法记录已变化，请重新选择词条");
                martialDraft = CharacterMartials.ChoosePrefix(selectedUnitId, entry, martialDraft, original.Index, candidate.Number, false, quality);
                martialDraftChanged = true; CloseMartialOverlay(); RenderMartialDetail();
                SetEditStatus("词条已放入草稿，点击保存修改后才会写入；切换功法将清理草稿");
            }
            catch (Exception ex) { SetEditStatus(ex.Message, true); if (martialLibraryStatus != null) martialLibraryStatus.text = ex.Message; }
        }
        private void SaveMartialDraft()
        {
            if (!martialDraftChanged || martialDraft == null) { SetEditStatus("没有待保存的词条修改"); return; }
            var preview = martialDraft; var entry = martialDraftEntry; var owner = martialDraftOwner; var context = martialDraftContext;
            if (owner != selectedUnitId || context != contextKey) { ResetMartialDraft(); SetEditStatus("人物或世界已变化，草稿已清理", true); return; }
            if (RunMartialEdit(() => CharacterMartials.SavePrefixes(context, owner, entry, preview)))
            { ResetMartialDraft(); RenderMartialDetail(); SaveEdits(); }
        }
        private void OpenMartialEquip(MartialEntry entry)
        {
            if (entry.Equipped || entry.Type != MartialType.Ability)
            {
                RunMartialEdit(() => CharacterMartials.Equip(contextKey, selectedUnitId, entry, 0,
                    entry.Equipped ? "" : CharacterMartials.Slot(selectedUnitId, entry.Type, 0))); return;
            }
            try
            {
                CloseDropdown(); var unit = CharacterReader.Resolve(selectedUnitId); var count = unit.data.unitData.abilitys?.Length ?? 0;
                if (count == 0) throw new InvalidOperationException("当前人物没有可用心法槽位");
                var width = Mathf.Min(340, contentWidth - 24); var height = Mathf.Min(300, count * 38 + 12);
                var shield = Fill(content, "心法位置浮层", 0, 0, contentWidth, contentHeight, new Color(1, 1, 1, .001f), true);
                dropdownLayer = shield.gameObject; var dismiss = shield.gameObject.AddComponent<Button>(); dismiss.targetGraphic = shield;
                dismiss.onClick.AddListener((UnityEngine.Events.UnityAction)(new Action(CloseDropdown)));
                var panel = Card(shield.rectTransform, "心法位置列表", contentWidth - width - 12, contentHeight - height - 72, width, height);
                var scroll = ItemScroll(panel, "心法位置", 6, 6, width - 12, height - 12); AddMartialScrollbar(scroll);
                scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Mathf.Max(scroll.viewport.sizeDelta.y, count * 38));
                var learned = CharacterMartials.Read(selectedUnitId); var owner = selectedUnitId; var context = contextKey;
                for (int i = 0; i < count; i++)
                {
                    var index = i; var slot = CharacterMartials.Slot(owner, entry.Type, index); var current = learned.Find(item => item.SoleId == slot);
                    MakeButton(scroll.content, "心法位置" + i, 0, i * 38, scroll.content.sizeDelta.x - 12, 32, "位置" + (i + 1) + " · " + (slot.Length == 0 ? "空位" : current?.Name ?? "已占用"), () =>
                    { CloseDropdown(); if (selectedUnitId == owner && contextKey == context) RunMartialEdit(() => CharacterMartials.Equip(context, owner, entry, index, slot)); });
                }
            }
            catch (Exception ex) { CloseDropdown(); SetEditStatus(ex.Message, true); }
        }
        private float RenderMartialPrefixes(ScrollRect scroll, MartialEntry entry, float offset)
        {
            BeginMartialDraft(entry); var prefixes = CharacterMartials.Prefixes(selectedUnitId, entry, martialDraft);
            var capacity = CharacterMartials.PrefixCapacity(martialDraft); var level = martialDraft.To<DataProps.MartialData>().martialInfo.level;
            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Mathf.Max(scroll.content.sizeDelta.y, offset + 42));
            Label(scroll.content, "词条 " + prefixes.Count + "/" + capacity + " · " + PrefixQuality(level).Replace("词条", "秘籍") + (martialDraftChanged ? " · 待保存" : ""), 0, offset, scroll.content.sizeDelta.x - 12, 28, 16, Ink);
            offset += 38;
            for (int i = 0; i < Math.Max(capacity, prefixes.Count); i++)
            {
                var index = i; var prefix = prefixes.Find(item => item.Index == index) ?? new MartialPrefixEntry { Index = index, Description = "无" };
                scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Mathf.Max(scroll.content.sizeDelta.y, offset + 78));
                Label(scroll.content, "词条" + (i + 1), 0, offset + 15, 62, 24, 14, Ink);
                var button = MakeButton(scroll.content, "当前词条" + i, 66, offset, scroll.content.sizeDelta.x - 78, 78, "", () => OpenMartialPrefixChoices(entry, prefix));
                var rect = button.GetComponent<RectTransform>();
                var label = Label(rect, prefix.Number + " · " + prefix.Description, 10, 5, rect.sizeDelta.x - 20, 44, 16, prefix.Number == 0 ? Muted : MartialQualityColor(prefix.Quality));
                var rowHeight = Mathf.Max(78, label.preferredHeight + 36); label.rectTransform.sizeDelta = new Vector2(label.rectTransform.sizeDelta.x, rowHeight - 36);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, rowHeight);
                Label(rect, prefix.Number == 0 ? (i == prefixes.Count ? "点击添加 · 会按原生规则改变品质" : "请先填写前一槽") : PrefixQuality(prefix.Quality) + " · " + (prefix.Unlocked ? "已生效" : "需继续修炼"), 10, rowHeight - 27, rect.sizeDelta.x - 20, 22, 13, Muted);
                button.interactable = i <= prefixes.Count; offset += rowHeight + 10;
            }
            return offset;
        }
        private void OpenMartialPrefixChoices(MartialEntry entry, MartialPrefixEntry original, int quality = 0, string query = "", bool all = false)
        {
            try
            {
                if (selectedIndex != 2 || martialDraft == null || martialDraftEntry?.SoleId != entry.SoleId) return;
                var candidates = all ? CharacterMartials.AllPrefixes(selectedUnitId, entry, original.Index, quality, martialDraft) : CharacterMartials.Candidates(selectedUnitId, entry, original.Index, false, quality, true, martialDraft);
                var panel = MartialOverlay("选择第 " + (original.Index + 1) + " 条词条"); var width = panel.sizeDelta.x; var height = panel.sizeDelta.y;
                Label(panel, "当前：" + original.Number + " · " + original.Description, 16, 48, width - 32, 58, 16, Ink);
                var tabWidth = (width - 32 - 6 * 6) / 7;
                for (int i = 0; i <= 6; i++)
                {
                    var selected = i;
                    var button = MakeButton(panel, "词条品阶" + i, 16 + i * (tabWidth + 6), 114, tabWidth, 30, i == 0 ? "原强度" : PrefixQuality(i).Replace("词条", ""), () =>
                    { if (MartialEditTarget()) OpenMartialPrefixChoices(entry, original, selected, martialLibrarySearch?.text ?? "", all); });
                    button.GetComponentInChildren<Text>().color = i == 0 ? Ink : MartialQualityColor(i);
                    if (i == quality) button.GetComponent<Image>().color = new Color(.78f, .72f, .55f, 1);
                }
                var scroll = ItemScroll(panel, "可替换词条", 16, 194, width - 32, height - 278); AddMartialScrollbar(scroll);
                martialLibraryStatus = Label(panel, "", 16, height - 66, width - 184, 48, 14, Muted);
                int page = 0, pageCount = 1; Action<string> filter = null;
                filter = text =>
                {
                    DestroyMartialChildren(scroll.content, 0); float offset = 0;
                    var match = (text ?? "").Trim();
                    var results = candidates.FindAll(candidate => match.Length == 0 || candidate.Number.ToString().IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0 || candidate.Description.IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0 || (candidate.CatalogKey ?? "").IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0);
                    pageCount = Math.Max(1, (results.Count + 39) / 40); page = Math.Min(page, pageCount - 1);
                    foreach (var candidate in results.GetRange(page * 40, Math.Min(40, results.Count - page * 40)))
                    {
                        scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Mathf.Max(scroll.content.sizeDelta.y, offset + 94));
                        var button = MakeButton(scroll.content, "候选词条" + candidate.Number, 0, offset, scroll.content.sizeDelta.x - 12, 94, "", () => SelectMartialPrefix(entry, original, candidate, quality));
                        var parent = button.GetComponent<RectTransform>();
                        var description = Label(parent, (candidate.CatalogKey == null ? "" : candidate.CatalogKey + "\n") + candidate.Number + " · " + candidate.Description, 12, 5, parent.sizeDelta.x - 24, 58, 16, MartialQualityColor(candidate.Quality));
                        var rowHeight = Mathf.Max(94, description.preferredHeight + 40);
                        description.rectTransform.sizeDelta = new Vector2(description.rectTransform.sizeDelta.x, rowHeight - 40);
                        parent.sizeDelta = new Vector2(parent.sizeDelta.x, rowHeight);
                        Label(parent, candidate.Unavailable ?? PrefixQuality(candidate.Quality) + " · 点击选择", 12, rowHeight - 27, parent.sizeDelta.x - 24, 22, 14, candidate.Unavailable == null ? Muted : Cinnabar);
                        button.interactable = candidate.Unavailable == null; offset += rowHeight + 10;
                    }
                    scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Mathf.Max(scroll.viewport.sizeDelta.y, offset));
                    scroll.verticalNormalizedPosition = 1; scroll.verticalScrollbar.gameObject.SetActive(offset > scroll.viewport.sizeDelta.y);
                    martialLibraryStatus.text = (all ? "全库 " : "本招式 ") + candidates.Count + " 项 · 检索 " + results.Count + " 项 · " + (page + 1) + "/" + pageCount + "页";
                };
                martialLibrarySearch = CreateSearchInput(panel, "词条检索", 16, 152, width - 148, 30, "输入词条编号或效果关键词", text => { page = 0; filter(text); });
                MakeButton(panel, "词条库范围", width - 122, 152, 106, 30, all ? "返回本招式" : "全词条图鉴", () =>
                { if (MartialEditTarget()) OpenMartialPrefixChoices(entry, original, quality, martialLibrarySearch?.text ?? "", !all); });
                MakeButton(panel, "词条上一页", width - 156, height - 62, 64, 30, "上一页", () => { page = Math.Max(0, page - 1); filter(martialLibrarySearch.text); });
                MakeButton(panel, "词条下一页", width - 84, height - 62, 68, 30, "下一页", () => { page = Math.Min(pageCount - 1, page + 1); filter(martialLibrarySearch.text); });
                martialLibrarySearch.SetTextWithoutNotify(query); filter(query);
            }
            catch (Exception ex) { CloseMartialOverlay(); SetEditStatus(ex.Message, true); }
        }
        private static string PrefixQuality(int quality)
        { return quality == 6 ? "红色词条" : quality == 5 ? "金色词条" : quality == 4 ? "紫色词条" : quality == 3 ? "蓝色词条" : quality == 2 ? "绿色词条" : quality == 1 ? "白色词条" : "特殊词条"; }
    }
}
