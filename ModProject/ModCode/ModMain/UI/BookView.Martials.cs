using System;
using System.Collections.Generic;
using MOD_YtxSQ1.Services;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MOD_YtxSQ1.UI
{
    internal sealed partial class BookView
    {
        private sealed class MartialRow { internal Button Button; internal Text Name, Meta; internal Image Mark; internal MartialEntry Entry; }
        private InputField martialSearch, martialLibrarySearch;
        private RectTransform martialListPanel, martialDetailPanel;
        private ScrollRect martialListScroll, martialLibraryScroll;
        private Text martialCount, martialLibraryStatus, martialLibraryHint, martialListTitle;
        private Button martialEquippedButton, martialStageButton;
        private readonly List<MartialRow> martialRows = new List<MartialRow>(), martialLibraryRows = new List<MartialRow>();
        private List<MartialEntry> martialEntries = new List<MartialEntry>(), martialCatalog = new List<MartialEntry>();
        private readonly List<MartialEntry> martialFiltered = new List<MartialEntry>(), martialLibraryResults = new List<MartialEntry>();
        private readonly HashSet<string> martialPicks = new HashSet<string>();
        private readonly List<Button> martialCategoryButtons = new List<Button>();
        private string martialQuery = "", martialOwner, martialFingerprint, martialLibraryOwner, martialLibraryContext;
        private MartialEntry martialSelection;
        private bool martialCompact, martialEquippedOnly, martialBooksOnly;
        private int martialCategory, martialLibraryGrade;
        private GameObject martialOverlay;
        private const float MartialPitch = 72;

        private void BuildMartials()
        {
            martialCompact = contentWidth < 720;
            var height = contentHeight - 76;
            var width = martialCompact ? contentWidth : Mathf.Min(352, Mathf.Max(280, contentWidth * .35f));
            martialListPanel = Card(content, "功法选取", 0, 76, width, height); Border(martialListPanel, width, height);
            martialListTitle = Label(martialListPanel, martialBooksOnly ? "背包秘籍" : "已学功法", 12, 10, width - 118, 28, 22, Ink);
            martialStageButton = MakeButton(martialListPanel, "切换功法阶段", width - 106, 10, 94, 28, martialBooksOnly ? "已学功法" : "背包秘籍", () =>
            {
                CloseMartialOverlay(); martialBooksOnly = !martialBooksOnly; martialSelection = null;
                martialListTitle.text = martialBooksOnly ? "背包秘籍" : "已学功法";
                martialStageButton.GetComponentInChildren<Text>().text = martialBooksOnly ? "已学功法" : "背包秘籍";
                martialEquippedButton.interactable = !martialBooksOnly; RefreshMartials(true);
            });
            martialStageButton.GetComponentInChildren<Text>().fontSize = 14;
            martialSearch = SearchWithLibrary(martialListPanel, "功法检索", "秘籍库", 10, 48, width - 20, 32,
                82, "名称 / 招式 / ID", value => { martialQuery = value.Trim(); FilterMartials(); }, OpenMartialLibrary);
            martialSearch.SetTextWithoutNotify(martialQuery);
            var half = (width - 28) / 2;
            martialEquippedButton = MakeButton(martialListPanel, "显示装备功法", 10, 92, half, 32,
                martialEquippedOnly ? "显示全部功法" : "显示装备功法", () =>
                { martialEquippedOnly = !martialEquippedOnly; martialEquippedButton.GetComponentInChildren<Text>().text = martialEquippedOnly ? "显示全部功法" : "显示装备功法"; FilterMartials(); });
            martialEquippedButton.GetComponentInChildren<Text>().fontSize = 16;
            martialEquippedButton.interactable = !martialBooksOnly;
            MakeButton(martialListPanel, "功法搭配", half + 18, 92, half, 32, "功法搭配", OpenMartialBuilds);
            martialCount = Label(martialListPanel, "正在读取功法…", 12, 134, width - 24, 22, 14, Muted);
            martialListScroll = ItemScroll(martialListPanel, "功法列表", 10, 164, width - 20, Mathf.Max(70, height - 180));
            AddMartialScrollbar(martialListScroll); BuildMartialRows(martialRows, martialListScroll, 1, false);
            martialListScroll.onValueChanged.AddListener((UnityAction<Vector2>)(new Action<Vector2>(_ => RenderMartialRows(false))));
            var detailWidth = martialCompact ? contentWidth : contentWidth - width - 16;
            martialDetailPanel = Card(content, "功法详情", martialCompact ? 0 : width + 16, 76, detailWidth, height); Border(martialDetailPanel, detailWidth, height);
            RefreshMartials(true);
        }
        private void RefreshMartials(bool force = false)
        {
            if (selectedIndex != 2 || martialListScroll == null) return;
            try
            {
                var entries = martialBooksOnly ? CharacterMartials.ReadBooks(selectedUnitId) : CharacterMartials.Read(selectedUnitId); var signature = CharacterMartials.Fingerprint(entries);
                if (!force && martialOwner == selectedUnitId && martialFingerprint == signature) return;
                if (martialOwner != selectedUnitId) { CloseMartialOverlay(); martialSelection = null; }
                martialOwner = selectedUnitId; martialFingerprint = signature; martialEntries = entries;
                FilterMartials();
            }
            catch (Exception ex)
            { martialOwner = selectedUnitId; martialFingerprint = null; martialEntries.Clear(); martialSelection = null; FilterMartials(); SetEditStatus("功法读取失败：" + ex.Message, true); }
        }
        private static bool MatchesMartial(MartialEntry entry, string query)
        { return query.Length == 0 || entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || entry.Id.ToString().IndexOf(query, StringComparison.Ordinal) >= 0; }
        private static bool SameMartialSelection(MartialEntry previous, MartialEntry current)
        {
            if (previous == null || current == null || previous.InBag != current.InBag) return false;
            if (ReferenceEquals(previous, current)) return true;
            if (!string.IsNullOrEmpty(previous.SoleId) && !string.IsNullOrEmpty(current.SoleId)) return previous.SoleId == current.SoleId;
            return previous.ReadPointer != IntPtr.Zero && previous.ReadPointer == current.ReadPointer && previous.Type == current.Type;
        }
        private void FilterMartials()
        {
            if (martialListScroll == null) return;
            var previous = martialSelection;
            martialFiltered.Clear();
            foreach (var entry in martialEntries) if ((martialBooksOnly || !martialEquippedOnly || entry.Equipped) && MatchesMartial(entry, martialQuery)) martialFiltered.Add(entry);
            martialSelection = martialFiltered.Find(entry => SameMartialSelection(previous, entry));
            if (martialSelection == null && martialFiltered.Count > 0) martialSelection = martialFiltered[0];
            martialCount.text = (martialBooksOnly ? "背包秘籍" : martialEquippedOnly ? "已装备" : "已学") + " · " + martialFiltered.Count + " 本";
            ResizeMartialList(martialListScroll, martialFiltered.Count, 1); RenderMartialRows(false); RenderMartialDetail();
            if (martialCompact) { martialListPanel.gameObject.SetActive(true); martialDetailPanel.gameObject.SetActive(false); }
        }
        private void BuildMartialRows(List<MartialRow> rows, ScrollRect scroll, int columns, bool library)
        {
            var width = (scroll.content.sizeDelta.x - 12 - (columns - 1) * 10) / columns;
            var count = ((int)Math.Ceiling(scroll.viewport.sizeDelta.y / MartialPitch) + 1) * columns;
            for (int i = 0; i < count; i++)
            {
                var row = new MartialRow();
                row.Button = MakeButton(scroll.content, library ? "秘籍条目" : "功法条目", 0, 0, width, 64, "", () =>
                {
                    if (row.Entry == null) return;
                    if (library)
                    { var key = MartialKey(row.Entry); if (!martialPicks.Add(key)) martialPicks.Remove(key); RenderMartialRows(true); UpdateMartialLibraryStatus(); return; }
                    martialSelection = row.Entry; RenderMartialRows(false); RenderMartialDetail();
                    if (martialCompact) { martialListPanel.gameObject.SetActive(false); martialDetailPanel.gameObject.SetActive(true); }
                });
                var panel = row.Button.GetComponent<RectTransform>();
                row.Mark = Fill(panel, "功法品质侧线", 0, 0, 3, 64, Gold);
                row.Name = Label(panel, "", 12, 6, width - 24, 28, 19, Ink);
                row.Meta = Label(panel, "", 12, 36, width - 24, 22, 13, Muted);
                if (library) { row.Meta.resizeTextForBestFit = true; row.Meta.resizeTextMinSize = 10; row.Meta.resizeTextMaxSize = 13; }
                rows.Add(row);
            }
        }
        private static string MartialKey(MartialEntry entry) { return ((int)entry.Type) + ":" + entry.Id + ":" + entry.SuitId; }
        private int MartialLibraryColumns() { return martialLibraryScroll.content.sizeDelta.x >= 900 ? 3 : martialLibraryScroll.content.sizeDelta.x >= 600 ? 2 : 1; }
        private void ResizeMartialList(ScrollRect scroll, int count, int columns)
        {
            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Mathf.Max(scroll.viewport.sizeDelta.y, (float)Math.Ceiling((double)count / columns) * MartialPitch));
            scroll.content.anchoredPosition = Vector2.zero; scroll.verticalNormalizedPosition = 1;
            scroll.verticalScrollbar.gameObject.SetActive(scroll.content.sizeDelta.y > scroll.viewport.sizeDelta.y);
        }
        private void RenderMartialRows(bool library)
        {
            var scroll = library ? martialLibraryScroll : martialListScroll; if (scroll == null) return;
            var rows = library ? martialLibraryRows : martialRows; var entries = library ? martialLibraryResults : martialFiltered;
            int columns = library ? MartialLibraryColumns() : 1;
            int first = (int)(Mathf.Max(0, scroll.content.anchoredPosition.y) / MartialPitch) * columns;
            var width = (scroll.content.sizeDelta.x - 12 - (columns - 1) * 10) / columns;
            for (int i = 0; i < rows.Count; i++)
            {
                var index = first + i; var row = rows[i]; row.Entry = index < entries.Count ? entries[index] : null;
                row.Button.gameObject.SetActive(row.Entry != null); if (row.Entry == null) continue;
                var entry = row.Entry; bool picked = library ? martialPicks.Contains(MartialKey(entry)) : entry == martialSelection;
                row.Button.GetComponent<RectTransform>().anchoredPosition = new Vector2(index % columns * (width + 10), -(index / columns) * MartialPitch);
                row.Button.GetComponent<Image>().color = picked ? new Color(.78f, .74f, .62f, 1) : new Color(.88f, .86f, .80f, 1);
                row.Name.text = (library && picked ? "✓ " : "") + entry.Name;
                row.Mark.color = library ? CharacterMartials.CanAcquire(entry, martialLibraryGrade) ? MartialQualityColor(6) : Muted : MartialQualityColor(entry.Level);
                row.Meta.text = library ? LibraryMartialMeta(entry)
                    : CharacterMartials.Realm(entry.Grade) + " · " + (entry.Type == MartialType.None ? "类别未读取" : CharacterMartials.Categories[entry.Category]) + (entry.InBag ? " · 未学习" : entry.Equipped ? " · 已装备" : "");
                row.Button.interactable = !library || CharacterMartials.CanAcquire(entry, martialLibraryGrade);
            }
        }
        private string LibraryMartialMeta(MartialEntry entry)
        {
            var source = (entry.Type == MartialType.None ? "类别未读取" : CharacterMartials.Categories[entry.Category]) + " · " + entry.School + (entry.SuitId != 0 ? "/传承" : "");
            if (entry.SuitGradeUnknown) return source + " · 境界待核实 · 不可获取";
            if (martialLibraryGrade < entry.GradeMin) return source + " · 最低" + CharacterMartials.Realm(entry.GradeMin) + " · 未解锁";
            if (martialLibraryGrade > entry.GradeMax) return source + " · 最高" + CharacterMartials.Realm(entry.GradeMax) + " · 不可获取";
            return source + " · " + CharacterMartials.Realm(martialLibraryGrade) + " · 红色";
        }
        private void RenderMartialDetail()
        {
            if (martialDetailPanel == null) return;
            // 空检索或切到背包不会进入BeginMartialDraft，须在详情入口释放旧草稿。
            if (martialSelection == null || martialSelection.InBag || martialSelection.ReadError != null ||
                martialDraftEntry != null && (!SameMartialSelection(martialDraftEntry, martialSelection) ||
                    martialDraftEntry.Signature != martialSelection.Signature || martialDraftEntry.Exp != martialSelection.Exp))
                ResetMartialDraft();
            DestroyMartialChildren(martialDetailPanel, 7);
            var width = martialDetailPanel.sizeDelta.x; var height = martialDetailPanel.sizeDelta.y;
            if (martialCompact) MakeButton(martialDetailPanel, "返回功法列表", 12, 10, 100, 30, "‹ 返回列表", () =>
                { martialListPanel.gameObject.SetActive(true); martialDetailPanel.gameObject.SetActive(false); });
            var top = martialCompact ? 48 : 12;
            if (martialSelection == null)
            {
                Label(martialDetailPanel, martialBooksOnly ? "背包没有符合条件的秘籍" : "没有符合条件的已学功法", 16, top + 60, width - 32, 38, 21, Ink, TextAnchor.MiddleCenter);
                Label(martialDetailPanel, "秘籍库获取 → 背包秘籍学习 → 修炼与装备 → 参悟词条", 16, top + 110, width - 32, 48, 16, Muted, TextAnchor.MiddleCenter); return;
            }
            var entry = martialSelection;
            Label(martialDetailPanel, entry.Name, 18, top, width - 36, 40, 26, MartialQualityColor(entry.Level));
            Label(martialDetailPanel, CharacterMartials.Realm(entry.Grade) + " · " + entry.School + " · " + (entry.Type == MartialType.None ? "类别未读取" : CharacterMartials.Categories[entry.Category]), 18, top + 44, width - 36, 24, 16, Muted);
            Label(martialDetailPanel, entry.ReadError != null ? "记录读取未完成" + (entry.Id > 0 ? " · #" + entry.Id : "") : entry.InBag ? "背包秘籍 · 未学习 · #" + entry.Id
                : (entry.Equipped ? "已装备" : "未装备") + " · 修炼 " + entry.Exp + " / " + entry.MaxExp + (entry.Exp >= entry.MaxExp ? " · 圆满" : "") + " · #" + entry.Id, 18, top + 74, width - 36, 22, 14, Muted);
            Fill(martialDetailPanel, "功法详情题线", 16, top + 104, width - 32, 1, Gold);
            if (entry.ReadError != null)
            { ResetMartialDraft(); Label(martialDetailPanel, "此记录暂不可修改：" + entry.ReadError + "\n其他功法仍可操作。", 18, top + 116, width - 36, 100, 17, Cinnabar); return; }
            var scroll = ItemScroll(martialDetailPanel, "功法效果", 16, top + 116, width - 32, Mathf.Max(60, height - top - 204)); AddMartialScrollbar(scroll);
            string description;
            try { description = CharacterMartials.Description(selectedUnitId, entry); }
            catch (Exception ex) { description = "介绍读取失败：" + ex.Message; }
            var textWidth = scroll.content.sizeDelta.x - 16;
            var label = Label(scroll.content, description, 0, 0, textWidth, scroll.viewport.sizeDelta.y, 17, Ink, TextAnchor.UpperLeft);
            var textHeight = label.preferredHeight + 20;
            label.rectTransform.sizeDelta = new Vector2(textWidth, textHeight); scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, textHeight);
            if (!entry.InBag)
            {
                try { textHeight = RenderMartialPrefixes(scroll, entry, textHeight); }
                catch (Exception ex) { Label(scroll.content, "词条暂不可读取：" + ex.Message, 0, textHeight, textWidth, 70, 15, Cinnabar); textHeight += 80; }
            }
            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Mathf.Max(scroll.viewport.sizeDelta.y, textHeight));
            scroll.verticalScrollbar.gameObject.SetActive(textHeight > scroll.viewport.sizeDelta.y);
            if (entry.InBag)
            {
                MakeButton(martialDetailPanel, "直接学会秘籍", 16, height - 72, width - 32, 32, "直接学会这本秘籍", () => OpenMartialStudy(entry));
            }
            else
            {
                var third = (width - 56) / 3;
                var complete = MakeButton(martialDetailPanel, "修炼圆满", 16, height - 72, third, 32, entry.Exp >= entry.MaxExp ? "已修炼圆满" : "修炼圆满", () => RunMartialEdit(() => CharacterMartials.Complete(contextKey, selectedUnitId, entry)));
                complete.interactable = entry.Exp < entry.MaxExp;
                MakeButton(martialDetailPanel, "装备功法", 28 + third, height - 72, third, 32, entry.Equipped ? "卸下功法" : "装备功法", () => OpenMartialEquip(entry));
                var save = MakeButton(martialDetailPanel, "保存修改", 40 + third * 2, height - 72, third, 32, "保存修改", SaveMartialDraft);
                save.interactable = martialDraftChanged && !CharacterSave.Busy;
            }
            Label(martialDetailPanel, entry.InBag ? "学习消耗这本秘籍；不自动修炼或装备" : "选择词条仅改草稿；保存修改后应用并存档，装备效果由原生动作更新", 16, height - 28, width - 32, 20, 13, Muted, TextAnchor.MiddleCenter);
        }
        private RectTransform MartialOverlay(string title)
        {
            CloseMartialOverlay(); CloseDropdown(); HideFateTips();
            martialLibraryOwner = selectedUnitId; martialLibraryContext = contextKey; martialLibraryGrade = CharacterMartials.Grade(selectedUnitId);
            var layer = Fill(content, "功法弹窗遮罩", 0, 0, contentWidth, contentHeight, new Color(.1f, .12f, .1f, .25f), true); martialOverlay = layer.gameObject;
            var panel = Card(layer.rectTransform, "功法弹窗", 12, 12, contentWidth - 24, contentHeight - 24);
            var width = panel.sizeDelta.x; var height = panel.sizeDelta.y;
            Fill(panel, "功法弹窗纸底", 1, 1, width - 2, height - 2, Paper, true); Border(panel, width, height);
            Label(panel, title, 14, 8, width - 66, 30, 23, Ink);
            MakeButton(panel, "关闭功法弹窗", width - 48, 8, 36, 28, "×", CloseMartialOverlay);
            return panel;
        }
        private bool LoadMartialCatalog()
        {
            try { martialCatalog = CharacterMartials.Catalog(); return true; }
            catch (Exception ex) { SetEditStatus("秘籍配置读取失败：" + ex.Message, true); return false; }
        }
        private void OpenMartialLibrary()
        {
            if (!LoadMartialCatalog()) return;
            try
            {
                var panel = MartialOverlay("秘籍库"); var width = panel.sizeDelta.x; var height = panel.sizeDelta.y;
                martialLibrarySearch = CreateSearchInput(panel, "秘籍库检索", 12, 46, width - 24, 32, "检索全部秘籍（名称 / 招式 / ID）", _ => FilterMartialLibrary());
                martialLibraryHint = Label(panel, "", 12, 82, width - 24, 30, 13, Muted);
                var tabWidth = (width - 56) / 5;
                for (int i = 0; i < 5; i++)
                {
                    var category = i; var button = MakeButton(panel, "秘籍类别" + i, 12 + i * (tabWidth + 8), 118, tabWidth, 32, CharacterMartials.Categories[i], () => { martialCategory = category; martialLibrarySearch.SetTextWithoutNotify(""); FilterMartialLibrary(); });
                    button.GetComponentInChildren<Text>().fontSize = tabWidth < 140 ? 15 : 18; martialCategoryButtons.Add(button);
                }
                martialLibraryScroll = ItemScroll(panel, "秘籍库列表", 12, 162, width - 24, height - 238); AddMartialScrollbar(martialLibraryScroll);
                BuildMartialRows(martialLibraryRows, martialLibraryScroll, MartialLibraryColumns(), true);
                martialLibraryScroll.onValueChanged.AddListener((UnityAction<Vector2>)(new Action<Vector2>(_ => RenderMartialRows(true))));
                martialLibraryStatus = Label(panel, "", 12, height - 64, width - 156, 48, 14, Muted);
                MakeButton(panel, "添加所选秘籍", width - 132, height - 52, 120, 36, "添加到背包", () =>
                {
                    var selected = martialCatalog.FindAll(entry => martialPicks.Contains(MartialKey(entry)));
                    AddMartialBooks(selected);
                });
                FilterMartialLibrary();
            }
            catch (Exception ex) { CloseMartialOverlay(); SetEditStatus(ex.Message, true); }
        }
        private void FilterMartialLibrary()
        {
            if (martialLibraryScroll == null) return;
            martialLibraryResults.Clear(); var query = martialLibrarySearch.text.Trim();
            string basis;
            martialLibraryResults.AddRange(CharacterMartials.SearchCatalog(martialCatalog, query, martialCategory, out basis));
            martialLibraryHint.text = basis == null ? "目录按基础招式与传承列出；完整书名前缀由词条生成。"
                : "已识别类型「" + basis + "」：请选择流派；获取后的书名由原生词条生成。";
            for (int i = 0; i < martialCategoryButtons.Count; i++) martialCategoryButtons[i].GetComponent<Image>().color = query.Length == 0 && i == martialCategory ? new Color(.73f, .69f, .56f, 1) : new Color(.87f, .85f, .78f, 1);
            ResizeMartialList(martialLibraryScroll, martialLibraryResults.Count, MartialLibraryColumns()); RenderMartialRows(true); UpdateMartialLibraryStatus();
        }
        private void UpdateMartialLibraryStatus()
        {
            if (martialLibraryStatus == null) return;
            int used, total;
            try { ItemInventory.ReadCapacity(martialLibraryOwner, out used, out total); martialLibraryStatus.text = (martialLibrarySearch.text.Trim().Length > 0 ? "全库检索" : CharacterMartials.Categories[martialCategory]) + " " + martialLibraryResults.Count + " 项 · 已选 " + martialPicks.Count + " 本 · 空格 " + Math.Max(0, total - used) + "\n" + CharacterMartials.Realm(martialLibraryGrade) + " · 红色秘籍，词条原生随机"; }
            catch (Exception ex) { martialLibraryStatus.text = ex.Message; }
        }
        private void AddMartialBooks(List<MartialEntry> entries)
        {
            if (edits.Count > 0) { SetEditStatus("请先保存属性修改，再添加秘籍", true); return; }
            if (martialLibraryOwner != selectedUnitId || martialLibraryContext != contextKey) { CloseMartialOverlay(); SetEditStatus("人物已变化，请重新选择", true); return; }
            var pending = CharacterSave.Pending;
            try
            { var message = CharacterMartials.Give(martialLibraryContext, martialLibraryOwner, entries, martialLibraryGrade); CloseMartialOverlay(); SetEditStatus(message); }
            catch (Exception ex)
            {
                if (CharacterSave.Pending != pending) CloseMartialOverlay();
                else if (martialLibraryStatus != null) martialLibraryStatus.text = ex.Message;
                SetEditStatus(ex.Message, true);
            }
        }
        private void OpenMartialBuilds()
        {
            if (!LoadMartialCatalog()) return;
            try
            {
                var panel = MartialOverlay("功法搭配 · 已核对 " + MartialBuilds.Verified.Length + " 套");
                var width = panel.sizeDelta.x; var height = panel.sizeDelta.y;
                var scroll = ItemScroll(panel, "功法搭配列表", 12, 50, width - 24, height - 66); AddMartialScrollbar(scroll);
                var page = scroll.content; var rowWidth = page.sizeDelta.x - 14;
                page.sizeDelta = new Vector2(page.sizeDelta.x, Mathf.Max(scroll.viewport.sizeDelta.y, MartialBuilds.Verified.Length * 112));
                for (int i = 0; i < MartialBuilds.Verified.Length; i++)
                {
                    var build = MartialBuilds.Verified[i];
                    var row = MakeButton(page, "流派搭配" + i, 0, i * 112, rowWidth, 100, "", () => OpenMartialBuildDetail(build));
                    var parent = row.GetComponent<RectTransform>();
                    Label(parent, build.Name + " · " + build.School, 14, 6, rowWidth - 28, 28, 22, Ink);
                    Label(parent, build.Summary, 14, 40, rowWidth - 28, 24, 16, Ink);
                    Label(parent, build.Evidence, 14, 72, rowWidth - 28, 20, 13, Muted);
                }
                scroll.verticalScrollbar.gameObject.SetActive(page.sizeDelta.y > scroll.viewport.sizeDelta.y);
            }
            catch (Exception ex) { CloseMartialOverlay(); SetEditStatus(ex.Message, true); }
        }
        private void OpenMartialBuildDetail(MartialBuild build)
        {
            try
            {
                var panel = MartialOverlay(build.Name); var width = panel.sizeDelta.x; var height = panel.sizeDelta.y;
                var scroll = ItemScroll(panel, "搭配说明", 16, 50, width - 32, height - 134); AddMartialScrollbar(scroll);
                var text = build.Summary + "\n\n" + build.Requirements + "\n\n功法清单：\n";
                foreach (var id in build.Ids)
                { var entry = martialCatalog.Find(item => item.Id == id); text += (entry == null ? "缺少配置 #" + id : (entry.Type == MartialType.None ? "类别未读取" : CharacterMartials.Categories[entry.Category]) + " · " + entry.Name) + "\n"; }
                text += "\n" + build.Evidence + "\n来源：" + build.Source + "\n\n按当前人物境界获取；不自动学习、装备、改词条或发放传承套装。通用心法保留合法原生词条，按攻略自行调整。";
                var label = Label(scroll.content, text, 0, 0, scroll.content.sizeDelta.x - 16, scroll.viewport.sizeDelta.y, 16, Ink, TextAnchor.UpperLeft);
                var textHeight = Mathf.Max(scroll.viewport.sizeDelta.y, label.preferredHeight + 16);
                label.rectTransform.sizeDelta = new Vector2(label.rectTransform.sizeDelta.x, textHeight); scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, textHeight);
                scroll.verticalScrollbar.gameObject.SetActive(textHeight > scroll.viewport.sizeDelta.y);
                martialLibraryStatus = Label(panel, "", 16, height - 76, width - 32, 26, 13, Muted);
                MakeButton(panel, "返回搭配列表", 16, height - 44, 110, 32, "返回列表", OpenMartialBuilds);
                var add = MakeButton(panel, "获取整套功法", width - 150, height - 44, 134, 32, "获取整套秘籍", () =>
                {
                    try { AddMartialBooks(MartialBuilds.Resolve(build, martialCatalog, martialLibraryGrade)); }
                    catch (Exception ex) { martialLibraryStatus.text = ex.Message; }
                });
                try
                { var resolved = MartialBuilds.Resolve(build, martialCatalog, martialLibraryGrade); int used, total; ItemInventory.ReadCapacity(selectedUnitId, out used, out total); martialLibraryStatus.text = CharacterMartials.Realm(martialLibraryGrade) + " · 需要" + resolved.Count + "格 · 背包剩余" + Math.Max(0, total - used) + "格"; }
                catch (Exception ex) { martialLibraryStatus.text = ex.Message; add.interactable = false; }
            }
            catch (Exception ex) { CloseMartialOverlay(); SetEditStatus(ex.Message, true); }
        }
        private void CloseMartialOverlay()
        {
            if (martialLibrarySearch != null) WindowInputScope.Unregister(martialLibrarySearch);
            martialLibrarySearch = null; martialLibraryScroll = null; martialLibraryStatus = null; martialLibraryHint = null; martialLibraryRows.Clear(); martialLibraryResults.Clear(); martialCategoryButtons.Clear(); martialPicks.Clear();
            var old = martialOverlay; martialOverlay = null; ReleaseViewObject(old, "功法浮层");
        }
        private void AddMartialScrollbar(ScrollRect scroll)
        {
            var track = Fill(scroll.viewport, "功法滚动轨", scroll.viewport.sizeDelta.x - 6, 0, 6, scroll.viewport.sizeDelta.y, new Color(.35f, .34f, .27f, .12f), true);
            var handle = ScrollHandle(track, "功法滚动柄", 24, Muted); var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform; bar.targetGraphic = handle; bar.direction = Scrollbar.Direction.BottomToTop; scroll.verticalScrollbar = bar;
        }
        private static Color MartialQualityColor(int level)
        { return level == 6 ? new Color(.59f, .24f, .20f, 1) : level == 5 ? new Color(.59f, .43f, .20f, 1) : level == 4 ? new Color(.43f, .33f, .52f, 1) : level == 3 ? new Color(.30f, .39f, .54f, 1) : level == 2 ? new Color(.34f, .46f, .34f, 1) : Muted; }
        private static void DestroyMartialChildren(RectTransform parent, int keep)
        { for (int i = parent.childCount - 1; i >= keep; i--) { var child = parent.GetChild(i).gameObject; ReleaseViewObject(child, "功法行内容"); } }
        private void ClearMartialsView()
        {
            ResetMartialDraft(); CloseMartialOverlay(); if (martialSearch != null) WindowInputScope.Unregister(martialSearch);
            martialSearch = null; martialListPanel = null; martialDetailPanel = null; martialListScroll = null; martialCount = null; martialEquippedButton = null; martialStageButton = null; martialListTitle = null;
            martialRows.Clear(); martialEntries.Clear(); martialFiltered.Clear(); martialCatalog.Clear(); martialSelection = null; martialOwner = martialFingerprint = null;
        }
    }
}
