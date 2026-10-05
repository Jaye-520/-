using System;
using System.Collections.Generic;
using MOD_YtxSQ1.Services;
using UnityEngine;
using UnityEngine.UI;

namespace MOD_YtxSQ1.UI
{
    internal sealed partial class BookView
    {
        internal interface IWorkshopPage : IDisposable
        {
            void Prepare();
            void Activate();
            void Refresh(bool force);
            void Deactivate();
            void Invalidate();
        }
        private sealed class Page : IWorkshopPage
        {
            private readonly BookView owner;
            internal readonly int Id;
            internal readonly bool Implemented, UsesTarget;
            internal readonly string Hint;
            internal readonly Action BuildContent, InvalidateContent;
            internal readonly Action<bool> RefreshContent;
            private readonly Action activateContent, deactivateContent, disposeContent;
            internal RectTransform Root;
            internal Text Status;
            internal Page(BookView owner, int id, bool implemented, bool usesTarget, string hint,
                Action build = null, Action<bool> refresh = null, Action invalidate = null,
                Action activate = null, Action deactivate = null, Action dispose = null)
            {
                this.owner = owner; Id = id; Implemented = implemented; UsesTarget = usesTarget; Hint = hint;
                BuildContent = build; RefreshContent = refresh; InvalidateContent = invalidate;
                activateContent = activate; deactivateContent = deactivate; disposeContent = dispose;
            }
            public void Prepare() { if (Root == null) owner.PreparePage(this); }
            public void Activate()
            {
                Prepare(); owner.content = Root; owner.contentWidth = Root.sizeDelta.x;
                owner.editStatus = Status; Root.gameObject.SetActive(true);
                foreach (var input in Root.GetComponentsInChildren<InputField>(true)) WindowInputScope.Register(input);
                if (activateContent != null) activateContent();
            }
            public void Refresh(bool force)
            {
                if (!Implemented) return;
                if (RefreshContent != null) RefreshContent(force);
            }
            public void Deactivate()
            {
                owner.ClosePageTransient();
                if (deactivateContent != null) deactivateContent();
                if (Id == 3) owner.ResetCurrentRelationPictures();
                if (Root != null)
                {
                    foreach (var input in Root.GetComponentsInChildren<InputField>(true)) WindowInputScope.Unregister(input);
                    Root.gameObject.SetActive(false);
                }
            }
            public void Invalidate()
            {
                if (InvalidateContent != null) InvalidateContent();
            }
            public void Dispose()
            {
                if (Root == null) return;
                if (disposeContent != null) Cleanup(disposeContent, "页面资源");
                ReleaseViewObject(Root.gameObject, "页面"); Root = null; Status = null;
            }
        }
        private readonly List<Page> pages = new List<Page>();
        private RectTransform contentHost;
        private GameObject sleepingHost;
        private Vector2 preparedCanvas;
        private bool suspended, preparingPages;
        private string preparedContext;
        private WorkshopPerformance.Measurement interactionTiming;
        private bool interactionPending;
        private string catalogsKey;
        private sealed class OwnedFateRow
        {
            internal RectTransform Root;
            internal Image Background;
            internal Text Name, Rank, Delete;
            internal CharacterFate Entry;
        }
        private readonly List<OwnedFateRow>[] ownedFateRows = { new List<OwnedFateRow>(), new List<OwnedFateRow>(), new List<OwnedFateRow>() };
        private readonly Text[] fateEmptyLabels = new Text[3];
        private OwnedFateRow CreateOwnedFateRow(int column, RectTransform page, float width, float height, int slot)
        {
            var row = new OwnedFateRow();
            row.Background = Fill(page, "气运行_" + column + "_" + slot, 0, 0, width, height, Paper, true);
            row.Root = row.Background.rectTransform; Border(row.Root, width, height);
            BindFateHover(row.Root.gameObject, () => { if (row.Entry != null) ShowFateTip(row.Root, row.Entry); },
                () => { if (fateTipOwner == row.Root) fateTipCloseAt = Time.unscaledTime + .12f; });
            row.Name = Label(row.Root, "", 6, 0, width - 36, height, 16, Ink, TextAnchor.MiddleCenter);
            row.Name.resizeTextForBestFit = true; row.Name.resizeTextMinSize = 12; row.Name.resizeTextMaxSize = 16;
            row.Rank = StyleFateRow(row.Background, row.Name, new CharacterFate(0, column, "", ""), width, height, 32);
            var remove = Fill(row.Root, "移除气运_" + column + "_" + slot, width - 28, 2, 26, height - 4, new Color(1, 1, 1, .001f), true);
            var button = remove.gameObject.AddComponent<Button>(); button.targetGraphic = remove;
            button.onClick.AddListener((UnityEngine.Events.UnityAction)(new Action(() => { if (row.Entry != null) DeleteOwnedFate(row.Entry); })));
            row.Delete = Label(remove.rectTransform, "×", 0, 0, 26, height - 4, (int)Mathf.Min(23, height - 5), Ink, TextAnchor.MiddleCenter);
            return row;
        }
        private string CatalogKey()
        {
            if (g.conf == null) return null;
            // Store a value identity, never a configuration/native wrapper across world lifetimes.
            var key = CharacterReader.ContextKey() + ":" + g.conf.Pointer + ":" + g.conf.localText.Pointer
                + ":" + g.conf.itemProps.Pointer + ":" + g.conf.roleCreateFeature.Pointer
                + ":" + g.conf.battleSkillAttack.Pointer + ":" + g.conf.battleStepBase.Pointer
                + ":" + g.conf.battleAbilityBase.Pointer + ":" + g.conf.battleAbilitySuitBase.Pointer
                + ":" + g.conf.gameItemRewardAbilitySuit.Pointer + ":" + g.conf.gameItemReward.Pointer
                + ":" + g.conf.battleAbilityCombine.Pointer + ":" + g.conf.battleSkillPrefixValue.Pointer
                + ":" + g.conf.battleSkillLevel.Pointer;
            if (g.conf.itemProps != null)
            {
                var items = g.conf.itemProps._allConfList; key += ":" + items.Count;
                if (items.Count > 0 && items[0] != null) key += ":" + GameTool.LS(items[0].name);
            }
            foreach (var item in g.conf.roleCreateFeature._allConfList)
            { if (item != null) { key += ":" + GameTool.LS(item.name); break; } }
            return key;
        }
        private void CheckCatalogSources()
        {
            var key = CatalogKey(); if (key == catalogsKey) return;
            catalogsKey = key; catalogContext = itemCatalogContext = martialCatalogContext = null;
            fateCatalog.Clear(); itemCatalog.Clear(); martialCatalog.Clear();
            martialProbe = null; fateUnitId = null;
            foreach (var page in pages) page.Invalidate();
        }
        internal void BeginInteractionTiming(bool warm, long start = 0)
        { interactionTiming = WorkshopPerformance.MeasureFrom(warm ? "重复打开可交互" : "首次打开可交互", start); interactionPending = true; }
        private void CompleteInteractionTiming()
        { if (!interactionPending) return; interactionPending = false; interactionTiming.Dispose(); }

        private void PreparePages()
        {
            using (WorkshopPerformance.Measure("首次页面准备"))
            {
                preparingPages = true;
                try
                {
                    // Save is an action. Future modules use this same registration and lifecycle.
                    pages.Add(new Page(this, 0, true, true, "点击数值修改；选择字段可下拉，修改待保存", BuildAttributeOverview,
                        null, () => fateUnitId = null));
                    pages.Add(new Page(this, 1, true, true, "检索或选择道具，添加给当前人物；操作后点击保存", BuildItems,
                        RefreshBag, () => bagOwner = bagFingerprint = null));
                    pages.Add(new Page(this, 2, true, true, "获取秘籍 → 学习 → 修炼与装备 → 参悟；操作当前选中人物", BuildMartials,
                        RefreshMartials, () => { martialOwner = martialFingerprint = martialProbe = null; }));
                    pages.Add(new Page(this, 3, true, true, "以左侧人物为中心；点击关系人物查看、展开或修改", BuildRelations,
                        RefreshRelations, () => { relationGraph = null; relationOwner = null; }));
                    pages.Add(new Page(this, 4, false, false, "功能尚未开放"));
                    pages.Add(new Page(this, 5, false, false, "功能尚未开放"));
                    foreach (var page in pages)
                    {
                        using (WorkshopPerformance.Measure("静态页面准备_" + Modules[page.Id])) page.Prepare();
                        page.Root.gameObject.SetActive(false);
                        foreach (var input in page.Root.GetComponentsInChildren<InputField>(true)) WindowInputScope.Unregister(input);
                    }
                    PrepareCatalogs();
                }
                finally { preparingPages = false; selectedIndex = -1; }
            }
        }
        private void PreparePage(Page page)
        {
            contentWidth = page.UsesTarget ? bookWidth - targetWidth - 76 : bookWidth - 48;
            contentHost.sizeDelta = new Vector2(contentWidth, contentHeight);
            content = Box("页面_" + Modules[page.Id], contentHost, 0, 0, contentWidth, contentHeight);
            page.Root = content; page.Root.gameObject.SetActive(false); selectedIndex = page.Id;
            if (page.Id == 0)
            {
                var width = contentWidth - 20;
                var height = 64 + AttributeGroupTop(contentHeight - 64, width >= 1180) - 10;
                var panel = Fill(content, "总览气运底框", 0, 0, width, height, new Color(.92f, .91f, .87f, .35f)).rectTransform;
                Border(panel, width, height);
            }
            else if (page.UsesTarget)
            {
                var heading = Fill(content, "模块题栏底框", 0, 0, contentWidth, 64, new Color(.92f, .91f, .87f, .35f)).rectTransform;
                Border(heading, contentWidth, 64);
            }
            Label(content, page.Id == 0 ? "属性总览" : Modules[page.Id], 20, 4, contentWidth - 152, 30, 25, Ink);
            editStatus = page.Status = Label(content, page.Hint, 20, 34, contentWidth - 152, 24, 16, Muted);
            if (page.BuildContent != null) page.BuildContent();
            else
            {
                var blank = Card(content, "待编纂", 0, 76, contentWidth, contentHeight - 76);
                Label(blank, "此卷待编纂", 16, 80, contentWidth - 32, 42, 25, Ink, TextAnchor.MiddleCenter);
                Label(blank, "功能尚未开放", 16, 140, contentWidth - 32, 30, 16, Muted, TextAnchor.MiddleCenter);
            }
        }
        private void ClosePageTransient()
        {
            FinishInlineEdit(true); CloseDropdown(); HideFateTips(); CloseFateLibrary();
            CloseItemMenu(); CloseItemDialog(); CloseItemLibrary(); CloseMartialOverlay(); ResetMartialDraft();
            CloseRelationOverlay(); CancelRelationPicture(); relationDragging = false;
        }
        private void RestoreNativeControls()
        {
            foreach (var graphic in hiddenGraphics) if (graphic != null) Cleanup(() => graphic.enabled = true, "恢复原生图形");
            for (int i = 0; i < nativeButtons.Count; i++)
            {
                var button = nativeButtons[i]; var enabled = nativeButtonStates[i]; var active = nativeButtonActiveStates[i];
                if (button == null) continue;
                Cleanup(() => button.interactable = enabled, "恢复原生按钮");
                Cleanup(() => button.gameObject.SetActive(active), "恢复原生按钮状态");
            }
            hiddenGraphics.Clear(); nativeButtons.Clear(); nativeButtonStates.Clear(); nativeButtonActiveStates.Clear();
        }
        internal bool CanResume()
        {
            if (contentHost == null || pages.Count != Modules.Length - 1) return false;
            foreach (var page in pages) if (page.Root == null || page.Root.parent != contentHost) return false;
            return suspended && root != null && g.ui != null && g.ui.canvas != null
                && preparedContext == CharacterReader.ContextKey()
                && preparedCanvas.Equals(g.ui.canvas.GetComponent<RectTransform>().rect.size);
        }
        internal void Suspend()
        {
            if (root == null || suspended) return;
            if (selectedIndex >= 0) pages[selectedIndex].Deactivate(); else ClosePageTransient();
            ReleasePortrait();
            // Strip all native records/callback snapshots while keeping static widgets and bounded textures.
            martialEntries.Clear(); martialFiltered.Clear(); martialSelection = null;
            foreach (var row in martialRows) row.Entry = null;
            foreach (var slot in bagSlots) { ClearBagSlot(slot, 5); slot.gameObject.name = "BHML_休眠背包格"; }
            bagRenderFirst = -1;
            bagItems.Clear(); relationGraph = null; relationSelection = null;
            foreach (var page in pages)
            {
                page.Invalidate(); page.Root.gameObject.SetActive(false);
                foreach (var input in page.Root.GetComponentsInChildren<InputField>(true)) WindowInputScope.Unregister(input);
            }
            if (sleepingHost == null) sleepingHost = new GameObject("BHML_休眠缓存");
            sleepingHost.SetActive(false); root.SetActive(false); root.transform.SetParent(sleepingHost.transform, false);
            RestoreNativeControls(); appearing = false; suspended = true; requestClose = null; interactionPending = false;
        }
        internal void Resume(UITextInfoLong window, Action close, string unitId)
        {
            if (!CanResume()) throw new InvalidOperationException("窗口缓存已失效");
            using (WorkshopPerformance.Measure("复用视图"))
            {
                HideNativeControls(window); root.transform.SetParent(window.transform, false);
                suspended = false; requestClose = close;
                selectedUnitId = string.IsNullOrEmpty(unitId) ? CharacterReader.Resolve(null)?.data.unitData.unitID : unitId;
                contextKey = CharacterReader.ContextKey(); ResetOpenState(); ReloadRoster();
                root.SetActive(true);
                SetNavigationSelection(-1); selectedIndex = -1; Select(0);
                if (!string.IsNullOrEmpty(unitId)) ScrollToInitialUnit(unitId);
                appearance.alpha = 0; appearance.interactable = false;
                appearanceStarted = Time.unscaledTime; appearing = true;
            }
        }
        private void ResetOpenState()
        {
            searchQuery = ""; targetSearch.SetTextWithoutNotify(""); FilterRoster();
            pickedFates.Clear(); itemPicks.Clear(); martialPicks.Clear();
            if (fateSearch != null) fateSearch.SetTextWithoutNotify("");
            if (itemSearch != null) itemSearch.SetTextWithoutNotify("");
            martialQuery = ""; if (martialSearch != null) martialSearch.SetTextWithoutNotify("");
            martialBooksOnly = martialEquippedOnly = false; relationExpanded.Clear(); relationFilter = 0;
            martialListTitle.text = "已学功法"; martialStageButton.GetComponentInChildren<Text>().text = "背包秘籍";
            martialEquippedButton.GetComponentInChildren<Text>().text = "显示装备功法"; martialEquippedButton.interactable = true;
            FilterItems(false); FilterCandidates();
            foreach (var page in pages)
            {
                foreach (var scroll in page.Root.GetComponentsInChildren<ScrollRect>(true))
                { scroll.content.anchoredPosition = Vector2.zero; scroll.verticalNormalizedPosition = 1; }
                page.Status.text = page.Hint;
            }
        }
        private void PrepareCatalogs()
        {
            CheckCatalogSources();
            using (WorkshopPerformance.Measure("气运配置索引")) LoadFateCatalog();
            using (WorkshopPerformance.Measure("道具配置索引")) LoadItemCatalog();
            using (WorkshopPerformance.Measure("功法配置索引")) LoadMartialCatalog();
        }
    }
}
