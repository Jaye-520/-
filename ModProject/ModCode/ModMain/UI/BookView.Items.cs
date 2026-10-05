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
        private Text itemBagTitle, itemBagCapacity, itemLibraryCount, itemDialogStatus;
        private GameObject itemLibrary, itemDialog;
        private List<ItemEntry> itemCatalog = new List<ItemEntry>();
        private readonly List<ItemEntry> itemResults = new List<ItemEntry>(), itemLibraryResults = new List<ItemEntry>();
        private readonly HashSet<int> itemPicks = new HashSet<int>(), itemLibraryPicks = new HashSet<int>();
        private readonly List<ItemRow> itemRows = new List<ItemRow>(), itemLibraryRows = new List<ItemRow>();
        private readonly List<RectTransform> bagSlots = new List<RectTransform>();
        private readonly List<Image[]> bagSlotBackgrounds = new List<Image[]>();
        private List<InventoryItem> bagItems = new List<InventoryItem>();
        private string bagOwner, bagFingerprint, itemCatalogContext;
        private const float ItemRowPitch = 42, BagPitch = 86;
        private int bagColumns, bagFreeSlots;
        private int bagRenderFirst = -1;

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
            itemSearch = SearchWithLibrary(search, "物品检索", "物品列表", 10, 10, searchWidth - 20, 32,
                82, "名称 / ID", _ => FilterItems(false), OpenItemLibrary);
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
            itemBagTitle.resizeTextForBestFit = true; itemBagTitle.resizeTextMinSize = 14; itemBagTitle.resizeTextMaxSize = 21;
            itemBagCapacity = Label(bag, "", 14, 36, bagWidth - 28, 20, 14, Muted);
            itemBagCapacity.resizeTextForBestFit = true; itemBagCapacity.resizeTextMinSize = 11; itemBagCapacity.resizeTextMaxSize = 14;
            Fill(bag, "背包题栏线", 12, 60, bagWidth - 24, 1, Gold);
            itemBagScroll = ItemScroll(bag, "背包网格", 12, 72, bagWidth - 24, bag.sizeDelta.y - 84);
            var bagTrack = Fill(itemBagScroll.viewport, "背包滚动轨", itemBagScroll.viewport.sizeDelta.x - 6, 0, 6,
                itemBagScroll.viewport.sizeDelta.y, new Color(.35f, .34f, .27f, .12f), true);
            var bagHandle = ScrollHandle(bagTrack, "背包滚动柄", 24, Muted);
            var bagBar = bagTrack.gameObject.AddComponent<Scrollbar>();
            bagBar.handleRect = bagHandle.rectTransform; bagBar.targetGraphic = bagHandle;
            bagBar.direction = Scrollbar.Direction.BottomToTop; itemBagScroll.verticalScrollbar = bagBar;
            bagColumns = Math.Max(1, (int)(itemBagScroll.content.sizeDelta.x / BagPitch));
            var pool = ((int)Math.Ceiling(itemBagScroll.viewport.sizeDelta.y / BagPitch) + 1) * bagColumns;
            for (int i = 0; i < pool; i++)
            {
                var slot = Box("背包格" + i, itemBagScroll.content, 0, 0, 78, 78);
                bagSlots.Add(slot);
                bagSlotBackgrounds.Add(new[] {
                    Fill(slot, "背包格底", 0, 0, 78, 78, new Color(.52f, .53f, .51f, .14f)),
                    Fill(slot, "格上沿", 0, 0, 78, 1, Gold), Fill(slot, "格下沿", 0, 77, 78, 1, Gold),
                    Fill(slot, "格左沿", 0, 0, 1, 78, Gold), Fill(slot, "格右沿", 77, 0, 1, 78, Gold) });
            }
            itemBagScroll.onValueChanged.AddListener((UnityAction<Vector2>)(new Action<Vector2>(_ => { CloseItemMenu(); HideFateTips(); RenderBagSlots(); })));
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
            HideFateTips();
            var query = field == null ? "" : field.text.Trim(); entries.Clear();
            if ((library || query.Length > 0 || itemPicks.Count > 0) && LoadItemCatalog())
                foreach (var entry in itemCatalog)
                    if (library ? query.Length == 0 || MatchesItem(entry, query) : query.Length > 0 ? MatchesItem(entry, query) : itemPicks.Contains(entry.Id)) entries.Add(entry);
            var columns = library ? ItemLibraryColumns() : 1;
            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Mathf.Max(scroll.viewport.sizeDelta.y, (float)Math.Ceiling((double)entries.Count / columns) * ItemRowPitch));
            if (library && scroll.verticalScrollbar != null) scroll.verticalScrollbar.gameObject.SetActive(scroll.content.sizeDelta.y > scroll.viewport.sizeDelta.y + .1f);
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
                    if (row.Entry != null && row.Button.gameObject.activeInHierarchy && (!library || itemLibrary != null)) ShowFateTip(row.Button.GetComponent<RectTransform>(), new CharacterFate(row.Entry.Id, 0, row.Entry.Name,
                        row.Entry.Description + "\n编号：" + row.Entry.Id + (row.Entry.Stackable ? " · 可叠加" : " · 不可叠加")), library ? itemLibrary.GetComponent<RectTransform>() : content, false, true, scroll);
                }, () => { if (fateTipOwner == row.Button.GetComponent<RectTransform>()) HideFateTips(); });
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
                var items = ItemInventory.Read(selectedUnitId);
                var resources = items.FindAll(item => item.Hidden);
                resources.Sort((a, b) => a.Id != b.Id ? a.Id.CompareTo(b.Id) : string.CompareOrdinal(a.SoleId, b.SoleId));
                resources.AddRange(items.FindAll(item => !item.Hidden)); items = resources;
                int used, total; ItemInventory.ReadCapacity(selectedUnitId, out used, out total);
                var free = Math.Max(0, total - used);
                var signature = ItemInventory.Fingerprint(items, true) + "|capacity:" + used + ":" + total;
                var owner = contextKey + ":" + selectedUnitId;
                var resourceCount = items.FindAll(item => item.Hidden).Count;
                itemBagTitle.text = characterName.text + "的背包 · " + (items.Count - resourceCount) + "项" + (resourceCount > 0 ? " + " + resourceCount + "项隐藏资源" : "");
                itemBagCapacity.text = "占用 " + used + " / " + total + " 格 · " + (free == 0 ? "已满" : "剩余 " + free + " 格");
                itemBagCapacity.color = free == 0 ? Cinnabar : Muted;
                if (!force && bagOwner == owner && bagFingerprint == signature) return;
                CloseItemMenu(); HideFateTips();
                if (bagOwner != owner) { itemBagScroll.content.anchoredPosition = Vector2.zero; itemBagScroll.verticalNormalizedPosition = 1; }
                bagOwner = owner; bagFingerprint = signature; bagItems = items; bagFreeSlots = free;
                itemBagScroll.content.sizeDelta = new Vector2(itemBagScroll.content.sizeDelta.x,
                    Mathf.Max(itemBagScroll.viewport.sizeDelta.y, (float)Math.Ceiling(((double)items.Count + bagFreeSlots) / bagColumns) * BagPitch));
                var position = itemBagScroll.content.anchoredPosition;
                itemBagScroll.content.anchoredPosition = new Vector2(0, Mathf.Clamp(position.y, 0, itemBagScroll.content.sizeDelta.y - itemBagScroll.viewport.sizeDelta.y));
                RenderBagSlots(true);
            }
            catch (Exception ex)
            {
                CloseItemMenu(); HideFateTips();
                bagItems.Clear(); bagFreeSlots = 0; bagOwner = null; bagFingerprint = null; itemBagCapacity.text = ""; RenderBagSlots(true);
                itemBagTitle.text = "背包读取失败"; SetEditStatus(ex.Message, true);
            }
        }
        private void RenderBagSlots(bool force = false)
        {
            if (itemBagScroll == null) return;
            var first = (int)(Mathf.Max(0, itemBagScroll.content.anchoredPosition.y) / BagPitch) * bagColumns;
            if (!force && first == bagRenderFirst) return;
            bagRenderFirst = first;
            var unit = CharacterReader.Resolve(selectedUnitId);
            for (int i = 0; i < bagSlots.Count; i++)
            {
                var index = first + i;
                // 按绝对索引循环复用，重叠行仍由原格子承载；跨一行只换一行图标。
                var poolIndex = index % bagSlots.Count;
                var slot = bagSlots[poolIndex];
                var active = index < (long)bagItems.Count + bagFreeSlots && unit != null;
                var key = index < bagItems.Count ? bagOwner + ":" + bagItems[index].SoleId + ":" + bagItems[index].DataSignature + ":hidden:" + bagItems[index].Hidden : bagOwner + ":empty:" + index;
                key += active ? ":on" : ":off";
                if (slot.gameObject.name == key) continue;
                if (slot.gameObject.activeSelf != active) slot.gameObject.SetActive(active);
                slot.anchoredPosition = new Vector2(index % bagColumns * BagPitch, -(index / bagColumns) * BagPitch);
                slot.gameObject.name = key;
                ClearBagSlot(slot, 5);
                if (!slot.gameObject.activeSelf || unit == null) continue;
                var empty = index >= bagItems.Count;
                var resource = !empty && bagItems[index].Hidden;
                var backgrounds = bagSlotBackgrounds[poolIndex];
                backgrounds[0].gameObject.name = resource ? "BHML_隐藏资源格底" : empty ? "BHML_空背包格底" : "BHML_背包格底";
                backgrounds[0].color = resource ? new Color(.66f, .22f, .18f, .25f) : new Color(.52f, .53f, .51f, .14f);
                var edge = resource ? new Color(.65f, .24f, .20f, .6f) : new Color(.40f, .42f, .39f, .20f);
                for (int j = 1; j < backgrounds.Length; j++) backgrounds[j].color = edge;
                if (empty) continue;
                var backgroundCount = slot.childCount;
                try
                {
                    var props = unit.data.unitData.propData.GetProps(bagItems[index].SoleId);
                    if (props == null) continue;
                    // 原生图标保留品阶、数量；介绍由本窗口承载，避免不同原生提示层互相遮挡。
                    var icon = UIIconTool.CreatePropsIcon(unit, props, slot, 1, false, false, false);
                    if (icon == null) throw new InvalidOperationException("图标未返回");
                    var rect = icon.GetComponent<RectTransform>();
                    var scale = rect.rect.width > 0 ? 72 / rect.rect.width : .72f;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = Vector2.zero; rect.localScale = Vector3.one * scale;
                    BindBagHover(icon, slot, bagItems[index]);
                    BindItemMenu(icon.gameObject, slot, bagItems[index]);
                }
                catch
                {
                    ClearBagSlot(slot, backgroundCount);
                    var fallback = Label(slot, bagItems[index].Name + "\n×" + bagItems[index].Count, 0, 0, 72, 72, 13, Ink, TextAnchor.MiddleCenter);
                    fallback.raycastTarget = true;
                    BindBagHoverListener(fallback.gameObject.AddComponent<UIEventListener>(), slot, bagItems[index]);
                    BindItemMenu(fallback.gameObject, slot, bagItems[index]);
                }
            }
        }
        private void BindBagHover(UIIconItemBase icon, RectTransform slot, InventoryItem item)
        {
            var listener = icon.uiEvent ?? icon.gameObject.AddComponent<UIEventListener>();
            BindBagHoverListener(listener, slot, item);
        }
        private void BindBagHoverListener(UIEventListener listener, RectTransform slot, InventoryItem item)
        {
            // 只替换自建图标的悬浮回调，保留原生点击。不能再次CreatePropInfo重复绑定。
            listener.onMouseEnterCall = null; listener.onMouseExitCall = null;
            listener.onMouseEnter = new UnityEvent(); listener.onMouseExit = new UnityEvent();
            var id = selectedUnitId; var context = contextKey;
            listener.onMouseEnter.AddListener((UnityAction)(new Action(() =>
            {
                if (selectedIndex != 1 || id != selectedUnitId || context != contextKey || itemMenu != null || itemDialog != null) return;
                try
                {
                    var unit = CharacterReader.Resolve(id);
                    var props = unit?.data.unitData.propData.GetProps(item.SoleId);
                    if (props == null || props.propsID != item.Id || props.propsCount != item.Count) return;
                    // 技能书是PropsData包装，通过原生To读取；MartialData并非其子类。
                    var martial = props.propsType == DataProps.PropsDataType.Martial ? props.To<DataProps.MartialData>() : null;
                    var description = martial != null
                        ? UIMartialInfoTool.GetDescRichText(UIMartialInfoTool.GetDesc(martial), new BattleSkillValueData(martial), 2)
                        : UIPropInfo.GetDesc(unit, props);
                    var info = props.propsInfoBase;
                    var saleText = info == null ? "—" : info.sale < 0 ? "不可出售" : info.sale + "灵石";
                    var detail = (info?.typeSuffix ?? "") + "\n" + (string.IsNullOrEmpty(description) ? "暂无介绍" : description)
                        + "\n\n出售价格：" + saleText + "\n当前持有：" + props.propsCount;
                    ShowFateTip(slot, new CharacterFate(item.Id, 0, item.Name, detail), content, false, true);
                }
                catch (Exception ex)
                {
                    ShowFateTip(slot, new CharacterFate(item.Id, 0, item.Name, "当前持有：" + item.Count + "\n介绍暂时无法读取"), content, false, true);
                    Debug.LogWarning("[八荒秘录] 物品介绍读取失败 #" + item.Id + "：" + ex.Message);
                }
            })));
            listener.onMouseExit.AddListener((UnityAction)(new Action(() =>
            { if (fateTipOwner == slot) HideFateTips(); })));
        }
        private void TickItemTipWheel()
        {
            if (itemTipScroll == null || fateTip == null || itemTipWheelFrame == Time.frameCount) return;
            itemTipWheelFrame = Time.frameCount;
            var delta = Input.mouseScrollDelta.y;
            var overflow = itemTipScroll.content.rect.height - itemTipScroll.viewport.rect.height;
            if (delta == 0 || overflow <= 0) return;
            itemTipScroll.verticalNormalizedPosition = Mathf.Clamp(itemTipScroll.verticalNormalizedPosition
                + delta * itemTipScroll.scrollSensitivity / overflow, 0, 1);
        }
        private void SetItemStatus(string message, bool error = false)
        {
            SetEditStatus(message, error);
            if (itemDialogStatus != null) { itemDialogStatus.text = message; itemDialogStatus.color = error ? Cinnabar : Muted; }
        }
        private bool RunItemOperation(Func<string> operation)
        {
            if (!FinishInlineEdit(false)) return false;
            if (edits.Count > 0) { SetItemStatus("请先保存属性修改，再操作背包", true); return false; }
            HideFateTips(); var pending = CharacterSave.Pending;
            try { var message = operation(); RefreshCharacter(true); SetItemStatus(message); return true; }
            catch (Exception ex)
            {
                RefreshCharacter(true); SetItemStatus(ex.Message, true);
                // 原生回调可能在异常前完成部分修改；关闭确认框，避免重复提交。
                return CharacterSave.Pending != pending;
            }
        }
        private void ClearBagSlot(RectTransform slot, int keepChildren = 0)
        {
            for (int j = slot.childCount - 1; j >= keepChildren; j--)
            {
                var child = slot.GetChild(j).gameObject;
                var icon = child.GetComponent<UIIconItemBase>();
                if (icon != null) try { icon.CloseIconInfo(); } catch (Exception ex) { Debug.LogWarning("[八荒秘录] 图标提示清理失败：" + ex.Message); }
                ReleaseViewObject(child, "背包格内容");
            }
        }
        private RectTransform ItemDialog(string title, out string unitId, out string context, bool primaryLeft = false)
        {
            CloseItemDialog(); CloseItemLibrary(); CloseDropdown(); HideFateTips();
            unitId = selectedUnitId; context = contextKey;
            var overlay = Fill(content, "物品操作遮罩", 0, 0, contentWidth, contentHeight, new Color(.1f, .12f, .1f, .25f), true);
            itemDialog = overlay.gameObject;
            var width = Mathf.Min(470, contentWidth - 24);
            var panel = Card(overlay.rectTransform, "物品操作", (contentWidth - width) / 2, (contentHeight - 230) / 2, width, 230);
            Fill(panel, "操作纸底", 1, 1, width - 2, 228, Paper, true); Border(panel, width, 230);
            Label(panel, title, 16, 12, width - 32, 34, 23, Ink);
            MakeButton(panel, "取消物品操作", primaryLeft ? width - 116 : 16, 176, 100, 36, "取消", CloseItemDialog);
            itemDialogStatus = Label(panel, "", 16, 138, width - 32, 32, 13, Muted);
            itemDialogStatus.resizeTextForBestFit = true; itemDialogStatus.resizeTextMinSize = 10; itemDialogStatus.resizeTextMaxSize = 13;
            return panel;
        }
        private void ConfirmGiveItem(ItemEntry entry)
        {
            string id, context; var panel = ItemDialog("给" + characterName.text + "添加道具", out id, out context, true);
            Label(panel, entry.Name, 16, 54, panel.sizeDelta.x - 32, 30, 19, Ink);
            itemAmount = CreateSearchInput(panel, "发放数量", 16, 98, panel.sizeDelta.x - 32, 36, "发放数量", _ => { });
            itemAmount.SetTextWithoutNotify("1"); itemAmount.readOnly = !entry.Stackable;
            itemDialogStatus.text = entry.Id == 10001 ? "输入新增灵石数量 · 最大总量2147483647"
                : entry.Stackable ? "输入新增数量 · 最大总量2147483647" : "不可叠加物品每次添加1个";
            MakeButton(panel, "确认添加道具", 16, 176, 120, 36, "添加", () =>
            {
                if (!string.IsNullOrEmpty(Input.compositionString)) return;
                int amount;
                if (!int.TryParse(itemAmount.text, out amount)) { SetItemStatus("请输入整数数量", true); return; }
                if (id != selectedUnitId || context != contextKey) { CloseItemDialog(); SetEditStatus("人物已变化，请重新操作", true); return; }
                if (RunItemOperation(() => ItemInventory.Give(context, id, entry.Id, amount))) CloseItemDialog();
            });
        }
        private void ConfirmClearBag()
        {
            try
            {
                var snapshot = ItemInventory.Read(selectedUnitId, false); var signature = ItemInventory.Fingerprint(snapshot);
                string id, context; var panel = ItemDialog("清空" + characterName.text + "的背包", out id, out context);
                Label(panel, "将移除背包中的" + snapshot.Count + "项物品。\n不含已装备物品和灵石等隐藏资源。\n此操作无法撤销。", 16, 54, panel.sizeDelta.x - 32, 98, 17, Ink);
                MakeButton(panel, "确认清空背包", panel.sizeDelta.x - 136, 176, 120, 36, "确认清空", () =>
                {
                    if (id != selectedUnitId || context != contextKey) { CloseItemDialog(); SetEditStatus("人物已变化，请重新操作", true); return; }
                    if (RunItemOperation(() => ItemInventory.Clear(context, id, signature))) CloseItemDialog();
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
            itemLibraryScroll = ItemScroll(panel, "物品列表视口", 12, 86, width - 38, height - 142);
            var track = Fill(panel, "物品列表滚动轨", width - 18, 86, 6, height - 142,
                new Color(.35f, .34f, .27f, .12f), true);
            var handle = ScrollHandle(track, "物品列表滚动柄", 24, Muted);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform; bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop; itemLibraryScroll.verticalScrollbar = bar;
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
            var old = itemLibrary; itemLibrary = null; ReleaseViewObject(old, "道具浮层");
        }
        private void CloseItemDialog()
        {
            CloseItemMenu();
            if (itemAmount != null) WindowInputScope.Unregister(itemAmount); itemAmount = null; itemDialogStatus = null;
            var old = itemDialog; itemDialog = null; ReleaseViewObject(old, "道具浮层");
        }
        private void ClearItemsView()
        {
            CloseItemDialog(); CloseItemLibrary();
            if (itemSearch != null) WindowInputScope.Unregister(itemSearch);
            foreach (var slot in bagSlots) ClearBagSlot(slot);
            itemSearch = null; itemResultsScroll = null; itemBagScroll = null; itemBagTitle = itemBagCapacity = null; bagFreeSlots = 0;
            itemRows.Clear(); bagSlots.Clear(); bagSlotBackgrounds.Clear(); itemResults.Clear(); bagItems.Clear(); bagOwner = null; bagFingerprint = null;
            bagRenderFirst = -1;
        }
    }
}
