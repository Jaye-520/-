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
        private sealed class ItemRow
        {
            internal Button Button;
            internal Text Label;
            internal ItemEntry Entry;
        }
        private InputField itemSearch, itemLibrarySearch, itemAmount;
        private ScrollRect itemResultsScroll, itemBagScroll, itemLibraryScroll;
        private Text itemBagTitle, itemLibraryCount;
        private GameObject itemLibrary, itemDialog;
        private List<ItemEntry> itemCatalog = new List<ItemEntry>();
        private readonly List<ItemEntry> itemResults = new List<ItemEntry>(), itemLibraryResults = new List<ItemEntry>();
        private readonly HashSet<int> itemPicks = new HashSet<int>(), itemLibraryPicks = new HashSet<int>();
        private readonly List<ItemRow> itemRows = new List<ItemRow>(), itemLibraryRows = new List<ItemRow>();
        private readonly List<RectTransform> bagSlots = new List<RectTransform>();
        private List<InventoryItem> bagItems = new List<InventoryItem>();
        private string bagOwner, bagFingerprint, itemCatalogContext;
        private const float ItemRowPitch = 42, BagPitch = 86;
        private int bagColumns;

        private ScrollRect ItemScroll(RectTransform parent, string name, float x, float y, float width, float height)
        {
            var viewport = Box(name, parent, x, y, width, height);
            Fill(viewport, "滚动命中", 0, 0, width, height, new Color(1, 1, 1, .001f), true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var page = NewRect(name + "长页", viewport);
            page.anchorMin = page.anchorMax = new Vector2(0, 1); page.pivot = new Vector2(0, 1);
            page.sizeDelta = new Vector2(width, height);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = page; scroll.horizontal = false; scroll.vertical = true;
            scroll.inertia = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 42;
            return scroll;
        }
        private void BuildItems()
        {
            var searchWidth = Mathf.Min(284, Mathf.Max(210, contentWidth * .24f));
            var search = Card(content, "物品检索栏", 0, 76, searchWidth, contentHeight - 76);
            Border(search, searchWidth, search.sizeDelta.y);
            MakeButton(search, "物品列表", 10, 10, 82, 32, "物品列表", OpenItemLibrary);
            itemSearch = CreateSearchInput(search, "物品检索", 98, 10, searchWidth - 108, 32, "名称 / ID", _ => FilterItems(false));
            itemResultsScroll = ItemScroll(search, "物品检索结果", 10, 54, searchWidth - 20, search.sizeDelta.y - 114);
            BuildItemRows(itemRows, itemResultsScroll, 1, false);
            itemResultsScroll.onValueChanged.AddListener((UnityAction<Vector2>)(new Action<Vector2>(_ => { HideFateTips(); RenderItemRows(itemRows, itemResults, itemResultsScroll, 1, false); })));
            var w = (searchWidth - 26) / 2;
            MakeButton(search, "补给", 10, search.sizeDelta.y - 48, w, 36, "补给", () => RunItemOperation(() => ItemInventory.Supply(contextKey, selectedUnitId)));
            MakeButton(search, "一键清空", 16 + w, search.sizeDelta.y - 48, w, 36, "一键清空", ConfirmClearBag);
            var bagWidth = contentWidth - searchWidth - 16;
            var bag = Card(content, "人物背包", searchWidth + 16, 76, bagWidth, contentHeight - 76);
            Border(bag, bagWidth, bag.sizeDelta.y);
            itemBagTitle = Label(bag, "背包", 14, 8, bagWidth - 28, 30, 21, Ink);
            Fill(bag, "背包题栏线", 12, 44, bagWidth - 24, 1, Gold);
            itemBagScroll = ItemScroll(bag, "背包网格", 12, 56, bagWidth - 24, bag.sizeDelta.y - 68);
            bagColumns = Math.Max(1, (int)(itemBagScroll.content.sizeDelta.x / BagPitch));
            var pool = ((int)Math.Ceiling(itemBagScroll.viewport.sizeDelta.y / BagPitch) + 1) * bagColumns;
            for (int i = 0; i < pool; i++) bagSlots.Add(Box("背包格" + i, itemBagScroll.content, 0, 0, 72, 72));
            itemBagScroll.onValueChanged.AddListener((UnityAction<Vector2>)(new Action<Vector2>(_ => RenderBagSlots())));
            FilterItems(false);
        }
        private bool LoadItemCatalog()
        {
            var context = CharacterReader.ContextKey();
            if (context == itemCatalogContext && itemCatalog.Count > 0) return true;
            try { itemCatalog = ItemInventory.Catalog(); itemCatalogContext = context; return true; }
            catch (Exception ex) { SetEditStatus("物品配置读取失败：" + ex.Message, true); return false; }
        }
        private static bool MatchesItem(ItemEntry entry, string query)
        { return entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || entry.Id.ToString().IndexOf(query, StringComparison.Ordinal) >= 0; }
        private void FilterItems(bool library)
        {
            var field = library ? itemLibrarySearch : itemSearch;
            var entries = library ? itemLibraryResults : itemResults;
            var scroll = library ? itemLibraryScroll : itemResultsScroll;
            if (scroll == null) return;
            var query = field == null ? "" : field.text.Trim(); entries.Clear();
            if ((library || query.Length > 0 || itemPicks.Count > 0) && LoadItemCatalog())
                foreach (var entry in itemCatalog)
                    if (library ? query.Length == 0 || MatchesItem(entry, query) : query.Length > 0 ? MatchesItem(entry, query) : itemPicks.Contains(entry.Id)) entries.Add(entry);
            var columns = library ? ItemLibraryColumns() : 1;
            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Mathf.Max(scroll.viewport.sizeDelta.y, (float)Math.Ceiling((double)entries.Count / columns) * ItemRowPitch));
            scroll.content.anchoredPosition = Vector2.zero; scroll.verticalNormalizedPosition = 1;
            RenderItemRows(library ? itemLibraryRows : itemRows, entries, scroll, columns, library);
        }
        private int ItemLibraryColumns() { return itemLibraryScroll.content.sizeDelta.x >= 800 ? 3 : 2; }
        private void BuildItemRows(List<ItemRow> rows, ScrollRect scroll, int columns, bool library)
        {
            var width = (scroll.content.sizeDelta.x - (columns - 1) * 8) / columns;
            var count = ((int)Math.Ceiling(scroll.viewport.sizeDelta.y / ItemRowPitch) + 1) * columns;
            for (int i = 0; i < count; i++)
            {
                var row = new ItemRow();
                row.Button = MakeButton(scroll.content, "物品条目", 0, 0, width, 36, "", () =>
                {
                    if (row.Entry == null) return;
                    if (!library) { ConfirmGiveItem(row.Entry); return; }
                    if (!itemLibraryPicks.Add(row.Entry.Id)) itemLibraryPicks.Remove(row.Entry.Id);
                    RenderItemRows(rows, itemLibraryResults, scroll, columns, true);
                });
                row.Label = row.Button.GetComponentInChildren<Text>(); row.Label.fontSize = 16;
                row.Label.resizeTextForBestFit = true; row.Label.resizeTextMinSize = 12; row.Label.resizeTextMaxSize = 16;
                BindFateHover(row.Button.gameObject, () =>
                {
                    if (row.Entry != null) ShowFateTip(row.Button.GetComponent<RectTransform>(), new CharacterFate(row.Entry.Id, 0, row.Entry.Name,
                        row.Entry.Description + "\n编号：" + row.Entry.Id + (row.Entry.Stackable ? " · 可叠加" : " · 不可叠加")));
                }, () => { fateTipCloseAt = Time.unscaledTime + .12f; });
                rows.Add(row);
            }
        }
        private void RenderItemRows(List<ItemRow> rows, List<ItemEntry> entries, ScrollRect scroll, int columns, bool library)
        {
            var first = (int)(Mathf.Max(0, scroll.content.anchoredPosition.y) / ItemRowPitch) * columns;
            var width = (scroll.content.sizeDelta.x - (columns - 1) * 8) / columns;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i]; var index = first + i;
                row.Entry = index < entries.Count ? entries[index] : null; row.Button.gameObject.SetActive(row.Entry != null);
                if (row.Entry == null) continue;
                row.Button.GetComponent<RectTransform>().anchoredPosition = new Vector2(index % columns * (width + 8), -index / columns * ItemRowPitch);
                row.Label.text = (library && itemLibraryPicks.Contains(row.Entry.Id) ? "✓ " : "") + row.Entry.Name;
                row.Label.color = Ink; row.Button.GetComponent<Image>().color = library && itemLibraryPicks.Contains(row.Entry.Id)
                    ? new Color(.78f, .72f, .55f, 1) : new Color(.92f, .90f, .84f, .9f);
            }
            if (library && itemLibraryCount != null) itemLibraryCount.text = "匹配 " + entries.Count + " 项 · 已选 " + itemLibraryPicks.Count + " 项";
        }
        private void RefreshBag(bool force)
        {
            if (itemBagScroll == null) return;
            try
            {
                var items = ItemInventory.Read(selectedUnitId); var signature = ItemInventory.Fingerprint(items);
                var owner = contextKey + ":" + selectedUnitId;
                itemBagTitle.text = characterName.text + "的背包 · " + items.Count + "项";
                if (!force && bagOwner == owner && bagFingerprint == signature) return;
                if (bagOwner != owner) { itemBagScroll.content.anchoredPosition = Vector2.zero; itemBagScroll.verticalNormalizedPosition = 1; }
                bagOwner = owner; bagFingerprint = signature; bagItems = items;
                itemBagScroll.content.sizeDelta = new Vector2(itemBagScroll.content.sizeDelta.x,
                    Mathf.Max(itemBagScroll.viewport.sizeDelta.y, (float)Math.Ceiling((double)items.Count / bagColumns) * BagPitch));
                RenderBagSlots();
            }
            catch (Exception ex)
            {
                bagItems.Clear(); bagOwner = null; bagFingerprint = null; RenderBagSlots();
                itemBagTitle.text = "背包读取失败"; SetEditStatus(ex.Message, true);
            }
        }
        private void RenderBagSlots()
        {
            if (itemBagScroll == null) return;
            var first = (int)(Mathf.Max(0, itemBagScroll.content.anchoredPosition.y) / BagPitch) * bagColumns;
            var unit = CharacterReader.Resolve(selectedUnitId);
            for (int i = 0; i < bagSlots.Count; i++)
            {
                var slot = bagSlots[i]; var index = first + i;
                slot.gameObject.SetActive(index < bagItems.Count && unit != null);
                var key = index < bagItems.Count ? bagOwner + ":" + bagItems[index].SoleId + ":" + bagItems[index].Count : "空";
                if (slot.gameObject.name == key) continue;
                slot.gameObject.name = key;
                ClearBagSlot(slot);
                if (index >= bagItems.Count || unit == null) continue;
                slot.anchoredPosition = new Vector2(index % bagColumns * BagPitch, -(index / bagColumns) * BagPitch);
                try
                {
                    var props = unit.data.unitData.propData.GetProps(bagItems[index].SoleId);
                    if (props == null) continue;
                    // 原生图标带真实品阶、数量与悬停详情，但关闭原生点击操作。
                    var icon = UIIconTool.CreatePropsIcon(unit, props, slot, 1, false, false, false);
                    if (icon == null) throw new InvalidOperationException("图标未返回");
                    var rect = icon.GetComponent<RectTransform>();
                    var scale = rect.rect.width > 0 ? 72 / rect.rect.width : .72f;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = Vector2.zero; rect.localScale = Vector3.one * scale;
                }
                catch { Label(slot, bagItems[index].Name + "\n×" + bagItems[index].Count, 0, 0, 72, 72, 13, Ink, TextAnchor.MiddleCenter); }
            }
        }
        private void RunItemOperation(Func<string> operation)
        {
            if (!FinishInlineEdit(false)) return;
            if (edits.Count > 0) { SetEditStatus("请先保存属性修改，再操作背包", true); return; }
            HideFateTips();
            try { var message = operation(); RefreshCharacter(true); SetEditStatus(message); }
            catch (Exception ex) { RefreshCharacter(true); SetEditStatus(ex.Message, true); }
        }
        private void ClearBagSlot(RectTransform slot)
        {
            for (int j = slot.childCount - 1; j >= 0; j--)
            {
                var child = slot.GetChild(j).gameObject;
                var icon = child.GetComponent<UIIconItemBase>();
                if (icon != null) icon.CloseIconInfo();
                child.SetActive(false); UnityEngine.Object.Destroy(child);
            }
        }
        private RectTransform ItemDialog(string title, out string unitId, out string context)
        {
            CloseItemDialog(); CloseItemLibrary(); CloseDropdown(); HideFateTips();
            unitId = selectedUnitId; context = contextKey;
            var overlay = Fill(content, "物品操作遮罩", 0, 0, contentWidth, contentHeight, new Color(.1f, .12f, .1f, .25f), true);
            itemDialog = overlay.gameObject;
            var width = Mathf.Min(470, contentWidth - 24);
            var panel = Card(overlay.rectTransform, "物品操作", (contentWidth - width) / 2, (contentHeight - 230) / 2, width, 230);
            Fill(panel, "操作纸底", 1, 1, width - 2, 228, Paper, true); Border(panel, width, 230);
            Label(panel, title, 16, 12, width - 32, 34, 23, Ink);
            MakeButton(panel, "取消物品操作", 16, 176, 100, 36, "取消", CloseItemDialog);
            return panel;
        }
        private void ConfirmGiveItem(ItemEntry entry)
        {
            string id, context; var panel = ItemDialog("给" + characterName.text + "添加道具", out id, out context);
            Label(panel, entry.Name, 16, 54, panel.sizeDelta.x - 32, 30, 19, Ink);
            itemAmount = CreateSearchInput(panel, "发放数量", 16, 98, panel.sizeDelta.x - 32, 36, "发放数量", _ => { });
            itemAmount.SetTextWithoutNotify("1"); itemAmount.readOnly = !entry.Stackable;
            MakeButton(panel, "确认添加道具", panel.sizeDelta.x - 136, 176, 120, 36, "添加", () =>
            {
                if (!string.IsNullOrEmpty(Input.compositionString)) return;
                int amount;
                if (!int.TryParse(itemAmount.text, out amount)) { SetEditStatus("请输入整数数量", true); return; }
                if (id != selectedUnitId || context != contextKey) { CloseItemDialog(); SetEditStatus("人物已变化，请重新操作", true); return; }
                RunItemOperation(() => ItemInventory.Give(context, id, entry.Id, amount)); CloseItemDialog();
            });
        }
        private void ConfirmClearBag()
        {
            try
            {
                var snapshot = ItemInventory.Read(selectedUnitId); var signature = ItemInventory.Fingerprint(snapshot);
                string id, context; var panel = ItemDialog("清空" + characterName.text + "的背包", out id, out context);
                Label(panel, "将移除背包中的" + snapshot.Count + "项物品。\n不含已装备物品和灵石等隐藏资源。\n此操作无法撤销。", 16, 54, panel.sizeDelta.x - 32, 98, 17, Ink);
                MakeButton(panel, "确认清空背包", panel.sizeDelta.x - 136, 176, 120, 36, "确认清空", () =>
                {
                    if (id != selectedUnitId || context != contextKey) { CloseItemDialog(); SetEditStatus("人物已变化，请重新操作", true); return; }
                    RunItemOperation(() => ItemInventory.Clear(context, id, signature)); CloseItemDialog();
                });
            }
            catch (Exception ex) { SetEditStatus(ex.Message, true); }
        }
        private void OpenItemLibrary()
        {
            if (!LoadItemCatalog()) return;
            CloseItemDialog(); CloseItemLibrary(); HideFateTips(); itemLibraryPicks.Clear();
            foreach (var id in itemPicks) itemLibraryPicks.Add(id);
            var overlay = Fill(content, "物品列表遮罩", 0, 0, contentWidth, contentHeight, new Color(.1f, .12f, .1f, .25f), true);
            itemLibrary = overlay.gameObject;
            var panel = Card(overlay.rectTransform, "物品图鉴", 12, 12, contentWidth - 24, contentHeight - 24);
            var width = panel.sizeDelta.x; var height = panel.sizeDelta.y;
            Fill(panel, "物品图鉴纸底", 1, 1, width - 2, height - 2, Paper, true); Border(panel, width, height);
            Label(panel, "物品列表", 12, 8, width - 100, 30, 24, Ink);
            MakeButton(panel, "关闭物品列表", width - 48, 8, 36, 28, "×", CloseItemLibrary);
            itemLibrarySearch = CreateSearchInput(panel, "物品列表检索", 12, 44, width - 24, 32, "检索物品（名称 / ID）", _ => FilterItems(true));
            itemLibraryScroll = ItemScroll(panel, "物品列表视口", 12, 86, width - 24, height - 142);
            BuildItemRows(itemLibraryRows, itemLibraryScroll, ItemLibraryColumns(), true);
            itemLibraryScroll.onValueChanged.AddListener((UnityAction<Vector2>)(new Action<Vector2>(_ => { HideFateTips(); RenderItemRows(itemLibraryRows, itemLibraryResults, itemLibraryScroll, ItemLibraryColumns(), true); })));
            itemLibraryCount = Label(panel, "", 12, height - 46, width - 150, 30, 15, Muted);
            MakeButton(panel, "确认物品选择", width - 132, height - 46, 120, 32, "确定", () =>
            {
                itemPicks.Clear(); foreach (var id in itemLibraryPicks) itemPicks.Add(id);
                itemSearch.SetTextWithoutNotify(""); CloseItemLibrary(); FilterItems(false);
                SetEditStatus("所选物品已列入检索栏，点击条目选择数量并添加");
            });
            FilterItems(true);
        }
        private void CloseItemLibrary()
        {
            HideFateTips(); if (itemLibrarySearch != null) WindowInputScope.Unregister(itemLibrarySearch);
            itemLibrarySearch = null; itemLibraryScroll = null; itemLibraryCount = null; itemLibraryRows.Clear(); itemLibraryResults.Clear();
            var old = itemLibrary; itemLibrary = null; if (old != null) { old.SetActive(false); UnityEngine.Object.Destroy(old); }
        }
        private void CloseItemDialog()
        {
            if (itemAmount != null) WindowInputScope.Unregister(itemAmount); itemAmount = null;
            var old = itemDialog; itemDialog = null; if (old != null) { old.SetActive(false); UnityEngine.Object.Destroy(old); }
        }
        private void ClearItemsView()
        {
            CloseItemDialog(); CloseItemLibrary();
            if (itemSearch != null) WindowInputScope.Unregister(itemSearch);
            foreach (var slot in bagSlots) ClearBagSlot(slot);
            itemSearch = null; itemResultsScroll = null; itemBagScroll = null; itemBagTitle = null;
            itemRows.Clear(); bagSlots.Clear(); itemResults.Clear(); bagItems.Clear(); bagOwner = null; bagFingerprint = null;
        }
    }
}
