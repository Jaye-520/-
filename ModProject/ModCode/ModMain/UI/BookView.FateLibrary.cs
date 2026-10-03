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
        private sealed class FateTile
        {
            internal Button Button;
            internal Text Label;
            internal Image Image;
            internal Text Rank;
            internal CharacterFate Entry;
        }
        private GameObject fateLibrary;
        private InputField fateSearch, librarySearch;
        private List<CharacterFate> fateCatalog = new List<CharacterFate>();
        private readonly List<CharacterFate> candidates = new List<CharacterFate>();
        private readonly List<CharacterFate> libraryEntries = new List<CharacterFate>();
        private readonly List<FateTile> candidateTiles = new List<FateTile>();
        private readonly List<FateTile> libraryTiles = new List<FateTile>();
        private readonly HashSet<int> pickedFates = new HashSet<int>();
        private readonly HashSet<int> libraryPicks = new HashSet<int>();
        private ScrollRect libraryScroll;
        private Text libraryCount;
        private const float FateTilePitch = 38;
        private string catalogContext;

        internal static Color FateColor(int level)
        {
            switch (level)
            {
                case 7: return new Color(.76f, .56f, .20f, 1); // 天：琥珀金，与仙级浅金区分
                case 6: return new Color(.53f, .19f, .16f, 1); // 神：朱红
                case 5: return new Color(.85f, .78f, .55f, 1); // 仙：淡金
                case 4: return new Color(.40f, .29f, .49f, 1); // 圣：紫
                case 3: return new Color(.30f, .39f, .54f, 1);
                case 2: return new Color(.34f, .46f, .34f, 1);
                case 1: return new Color(.48f, .49f, .46f, 1);
                default: return level < 0 ? new Color(.22f, .23f, .22f, 1) : new Color(.82f, .81f, .75f, 1);
            }
        }
        private static Color FateInk(int level)
        { return level == 5 || level == 0 || level > 6 ? Ink : new Color(.98f, .96f, .88f, 1); }

        private Text StyleFateRow(Image image, Text name, CharacterFate entry, float width, float height, float rightInset = 8, Text rank = null)
        {
            // 纯色底、细线与品阶字均由UI绘制，不再加载或拉伸游戏气运贴图。
            image.sprite = null; image.type = Image.Type.Simple; image.preserveAspect = false;
            image.color = FateColor(entry.Level);
            name.color = FateInk(entry.Level); name.alignment = TextAnchor.MiddleLeft;
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0, 1);
            name.rectTransform.pivot = new Vector2(0, 1);
            name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;
            name.rectTransform.anchoredPosition = new Vector2(34, -2);
            name.rectTransform.sizeDelta = new Vector2(width - 34 - rightInset, height - 4);
            if (rank == null)
            {
                var line = new Color(.95f, .89f, .72f, .32f);
                Fill(image.rectTransform, "气运细线上", 2, 2, width - 4, 1, line);
                Fill(image.rectTransform, "气运细线下", 2, height - 3, width - 4, 1, line);
                Fill(image.rectTransform, "气运品阶分隔", 28, 6, 1, height - 12, line);
                rank = Label(image.rectTransform, "", 4, 0, 22, height, 14, name.color, TextAnchor.MiddleCenter);
                rank.gameObject.name = "BHML_气运品阶";
                var carving = NewRect("天级纹饰", rank.rectTransform);
                carving.anchorMin = carving.anchorMax = new Vector2(0, 1); carving.pivot = new Vector2(0, 1);
                carving.anchoredPosition = new Vector2(-4, 0); carving.sizeDelta = new Vector2(width, height);
                var bright = new Color(1, .91f, .58f, .9f);
                var shade = new Color(.39f, .24f, .05f, .65f);
                Fill(carving, "天级金线上", 2, 1, width - 4, 1, bright);
                Fill(carving, "天级金线下", 2, height - 2, width - 4, 1, shade);
                Fill(carving, "天级金线左", 1, 1, 1, height - 2, bright);
                Fill(carving, "天级金线右", width - 2, 1, 1, height - 2, shade);
                Fill(carving, "天级雕线左上", 4, 4, 8, 1, bright);
                Fill(carving, "天级雕线右下", width - 12, height - 5, 8, 1, bright);
                Fill(carving, "天级金印", 4, 5, 22, height - 10, new Color(1, .91f, .58f, .22f));
            }
            rank.transform.GetChild(0).gameObject.SetActive(entry.Level == 7);
            rank.color = name.color;
            rank.text = entry.Level == 7 ? "天" : entry.Level == 6 ? "神" : entry.Level == 5 ? "仙" : entry.Level == 4 ? "圣" : "·";
            return rank;
        }

        private static void ResetClonedInput(InputField field, float padding = 8)
        {
            // Instantiate会复制模板编辑时生成的Caret引用和矩形缓存，让新框重建。
            // 运行时复制正在编辑的框会连同生成的Caret子对象一起复制。
            // 非序列化caretRectTrans可能仍指向源框，仅清它无法移除克隆里的孤立光标。
            foreach (var child in field.GetComponentsInChildren<RectTransform>(true))
                if (child != field.transform && child.gameObject.name.EndsWith(" Input Caret", StringComparison.Ordinal))
                { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
            field.caretRectTrans = null; field.m_CachedInputRenderer = null; field.m_InputTextCache = null;
            field.m_Mesh = null; field.m_CursorVerts = null;
            var text = field.textComponent;
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(.5f, .5f); text.rectTransform.localScale = Vector3.one;
            text.rectTransform.offsetMin = new Vector2(padding, 2); text.rectTransform.offsetMax = new Vector2(-padding, -2);
            text.resizeTextForBestFit = false; text.alignByGeometry = false; text.lineSpacing = 1;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Truncate;
            var hint = field.placeholder == null ? null : field.placeholder.TryCast<Text>();
            if (hint != null)
            {
                hint.rectTransform.anchorMin = Vector2.zero; hint.rectTransform.anchorMax = Vector2.one;
                hint.rectTransform.pivot = text.rectTransform.pivot; hint.rectTransform.localScale = Vector3.one;
                hint.rectTransform.offsetMin = text.rectTransform.offsetMin; hint.rectTransform.offsetMax = text.rectTransform.offsetMax;
                hint.alignByGeometry = false; hint.lineSpacing = 1; hint.alignment = text.alignment;
            }
        }

        private InputField CreateSearchInput(RectTransform parent, string name, float x, float y, float width, float height, string hint, Action<string> changed)
        {
            var host = Box(name, parent, x, y, width, height);
            var clone = UnityEngine.Object.Instantiate(targetSearch.gameObject, host, false);
            clone.SetActive(false); clone.name = "BHML_" + name;
            var rect = clone.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var field = clone.GetComponent<InputField>();
            ResetClonedInput(field);
            rect.localScale = Vector3.one;
            foreach (var trigger in clone.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true))
            { trigger.enabled = false; UnityEngine.Object.Destroy(trigger); }
            field.onValueChanged = new InputField.OnChangeEvent(); field.onEndEdit = new InputField.SubmitEvent();
            field.onValidateInput = null; field.characterLimit = 64;
            field.shouldActivateOnSelect = true; field.readOnly = false; field.interactable = true;
            field.contentType = InputField.ContentType.Standard; field.lineType = InputField.LineType.SingleLine;
            field.characterValidation = InputField.CharacterValidation.None;
            field.textComponent.fontSize = 16;
            var placeholder = field.placeholder == null ? null : field.placeholder.TryCast<Text>();
            if (placeholder != null) { placeholder.text = hint; placeholder.fontSize = 15; }
            if (field.placeholder != null) field.placeholder.gameObject.SetActive(true);
            field.SetTextWithoutNotify("");
            field.onValueChanged.AddListener((UnityAction<string>)(new Action<string>(changed)));
            WindowInputScope.Register(field); clone.SetActive(true);
            field.AssignPositioningIfNeeded(); field.UpdateLabel();
            return field;
        }
        private void BuildFateSearch(RectTransform heading, float width)
        {
            fateSearch = CreateSearchInput(heading, "气运检索", 4, 1, width, 26, "检索气运（名称/ID）", _ => FilterCandidates());
        }
        private bool LoadFateCatalog()
        {
            var key = CharacterReader.ContextKey();
            if (key == catalogContext && fateCatalog.Count > 0) return true;
            try { fateCatalog = CharacterFateCatalog.Read(); catalogContext = key; return true; }
            catch (Exception ex) { SetEditStatus("气运配置库读取失败：" + ex.GetType().Name, true); return false; }
        }
        private void FilterCandidates()
        {
            var query = fateSearch == null ? "" : fateSearch.text.Trim();
            // 空检索与空选择不读取两千项配置，打开角色页不需要图鉴全库。
            if (query.Length == 0 && pickedFates.Count == 0) { candidates.Clear(); RenderCandidates(true); return; }
            if (!LoadFateCatalog()) return;
            candidates.Clear();
            foreach (var item in fateCatalog)
                if (query.Length > 0 ? MatchesFate(item, query) : pickedFates.Contains(item.Id)) candidates.Add(item);
            RenderCandidates(true);
        }
        private static bool MatchesFate(CharacterFate entry, string query)
        {
            return entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || entry.Id.ToString().IndexOf(query, StringComparison.Ordinal) >= 0;
        }
        private FateTile MakeFateTile(RectTransform parent, float width, Action<FateTile> clicked, bool library)
        {
            var tile = new FateTile();
            tile.Button = MakeButton(parent, "气运候选", 0, 0, width, 32, "", () => { if (tile.Entry != null) clicked(tile); });
            tile.Image = tile.Button.GetComponent<Image>();
            tile.Label = tile.Button.GetComponentInChildren<Text>();
            tile.Label.fontSize = 16; tile.Label.resizeTextForBestFit = true; tile.Label.resizeTextMinSize = 11; tile.Label.resizeTextMaxSize = 16;
            BindFateHover(tile.Button.gameObject, () =>
            {
                if (tile.Entry != null) ShowFateTip(tile.Button.GetComponent<RectTransform>(), CharacterFateCatalog.Describe(selectedUnitId, tile.Entry));
            }, () => { fateTipCloseAt = Time.unscaledTime + .12f; });
            return tile;
        }
        private void BindFateTiles(List<FateTile> tiles, List<CharacterFate> entries, ScrollRect scroll, int columns, bool library)
        {
            var offset = Mathf.Max(0, scroll.content.anchoredPosition.y);
            var first = (int)(offset / FateTilePitch) * columns;
            var width = (scroll.content.sizeDelta.x - (columns - 1) * 8) / columns;
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i]; var index = first + i;
                tile.Entry = index < entries.Count ? entries[index] : null;
                tile.Button.gameObject.SetActive(tile.Entry != null);
                if (tile.Entry == null) continue;
                var rect = tile.Button.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(index % columns * (width + 8), -(index / columns) * FateTilePitch);
                tile.Label.text = (library && libraryPicks.Contains(tile.Entry.Id) ? "✓ " : "") + CharacterFateCatalog.Prefix(tile.Entry.Category) + tile.Entry.Name;
                tile.Rank = StyleFateRow(tile.Image, tile.Label, tile.Entry, width, 32, 8, tile.Rank);
            }
        }
        private void RenderCandidates(bool reset = false)
        {
            if (fateColumns.Count < 4) return;
            var scroll = fateColumns[3]; var page = scroll.content;
            if (candidateTiles.Count == 0)
            {
                for (int i = page.childCount - 1; i >= 0; i--) { var child = page.GetChild(i).gameObject; child.SetActive(false); UnityEngine.Object.Destroy(child); }
                var count = (int)Math.Ceiling(scroll.viewport.sizeDelta.y / FateTilePitch) + 1;
                for (int i = 0; i < count; i++) candidateTiles.Add(MakeFateTile(page, page.sizeDelta.x, AddCandidateFate, false));
                scroll.onValueChanged.AddListener((UnityAction<Vector2>)(new Action<Vector2>(_ => { HideFateTips(); BindFateTiles(candidateTiles, candidates, scroll, 1, false); })));
            }
            page.sizeDelta = new Vector2(page.sizeDelta.x, Mathf.Max(scroll.viewport.sizeDelta.y, candidates.Count * FateTilePitch));
            scroll.verticalScrollbar.gameObject.SetActive(page.sizeDelta.y > scroll.viewport.sizeDelta.y + .1f);
            if (reset) { page.anchoredPosition = Vector2.zero; scroll.verticalNormalizedPosition = 1; }
            BindFateTiles(candidateTiles, candidates, scroll, 1, false);
        }
        private void AddCandidateFate(FateTile tile)
        {
            if (tile.Entry == null) return;
            if (!FinishInlineEdit(false)) return;
            string message;
            if (CharacterFateOperations.Add(selectedUnitId, tile.Entry.Id, out message))
            { RefreshCharacter(true); SetEditStatus(message + "；请点击保存"); }
            else { RefreshCharacter(true); SetEditStatus(message, true); }
        }
        private void DeleteOwnedFate(CharacterFate entry)
        {
            if (!FinishInlineEdit(false)) return;
            string message;
            if (CharacterFateOperations.Remove(selectedUnitId, entry.Id, out message))
            { RefreshCharacter(true); SetEditStatus(message + "；请点击保存"); }
            else { RefreshCharacter(true); SetEditStatus(message, true); }
        }
        private void OpenFateLibrary()
        {
            if (!FinishInlineEdit(false) || !LoadFateCatalog()) return;
            CloseDropdown(); CloseFateLibrary(); HideFateTips();
            libraryPicks.Clear(); foreach (var id in pickedFates) libraryPicks.Add(id);
            var overlay = Fill(content, "气运图鉴遮罩", 0, 0, contentWidth, contentHeight, new Color(.10f, .11f, .09f, .65f), true);
            fateLibrary = overlay.gameObject;
            var width = contentWidth - 24; var height = contentHeight - 24;
            var panel = Card(overlay.rectTransform, "气运图鉴", 12, 12, width, height);
            Fill(panel, "图鉴纸底", 1, 1, width - 2, height - 2, new Color(.90f, .89f, .85f, 1), true);
            Label(panel, "气运列表", 12, 8, width - 100, 30, 24, Ink);
            MakeButton(panel, "关闭图鉴", width - 48, 8, 36, 28, "×", CloseFateLibrary);
            librarySearch = CreateSearchInput(panel, "图鉴检索", 12, 44, width - 24, 32, "检索全部可见气运（名称/ID）", _ => FilterLibrary());
            libraryCount = Label(panel, "", 12, height - 46, width - 150, 30, 15, Muted);
            MakeButton(panel, "确认气运选择", width - 132, height - 46, 120, 32, "确定", () =>
            {
                pickedFates.Clear(); foreach (var id in libraryPicks) pickedFates.Add(id);
                if (fateSearch != null) fateSearch.SetTextWithoutNotify("");
                CloseFateLibrary(); FilterCandidates(); SetEditStatus("已选气运列在检索区，点击条目才会添加给当前人物");
            });
            var viewport = Box("图鉴视口", panel, 12, 86, width - 24, height - 142);
            Fill(viewport, "图鉴命中", 0, 0, width - 24, height - 142, new Color(1, 1, 1, .001f), true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var page = NewRect("图鉴长页", viewport); page.anchorMin = page.anchorMax = new Vector2(0, 1); page.pivot = new Vector2(0, 1);
            page.sizeDelta = new Vector2(width - 24, viewport.sizeDelta.y);
            libraryScroll = viewport.gameObject.AddComponent<ScrollRect>(); libraryScroll.viewport = viewport; libraryScroll.content = page;
            libraryScroll.horizontal = false; libraryScroll.vertical = true; libraryScroll.inertia = false;
            libraryScroll.movementType = ScrollRect.MovementType.Clamped; libraryScroll.scrollSensitivity = 38;
            var columns = page.sizeDelta.x >= 780 ? 4 : 3;
            var tileWidth = (page.sizeDelta.x - (columns - 1) * 8) / columns;
            var pool = ((int)Math.Ceiling(viewport.sizeDelta.y / FateTilePitch) + 1) * columns;
            for (int i = 0; i < pool; i++) libraryTiles.Add(MakeFateTile(page, tileWidth, tile =>
            {
                if (!libraryPicks.Add(tile.Entry.Id)) libraryPicks.Remove(tile.Entry.Id);
                RenderLibrary();
            }, true));
            libraryScroll.onValueChanged.AddListener((UnityAction<Vector2>)(new Action<Vector2>(_ => { HideFateTips(); RenderLibrary(); })));
            FilterLibrary();
        }
        private void FilterLibrary()
        {
            libraryEntries.Clear(); var query = librarySearch == null ? "" : librarySearch.text.Trim();
            foreach (var item in fateCatalog) if (query.Length == 0 || MatchesFate(item, query)) libraryEntries.Add(item);
            var columns = libraryScroll.content.sizeDelta.x >= 780 ? 4 : 3;
            libraryScroll.content.sizeDelta = new Vector2(libraryScroll.content.sizeDelta.x, Mathf.Max(libraryScroll.viewport.sizeDelta.y, (float)Math.Ceiling((double)libraryEntries.Count / columns) * FateTilePitch));
            libraryScroll.content.anchoredPosition = Vector2.zero; libraryScroll.verticalNormalizedPosition = 1;
            RenderLibrary();
        }
        private void RenderLibrary()
        {
            if (libraryScroll == null) return;
            var columns = libraryScroll.content.sizeDelta.x >= 780 ? 4 : 3;
            BindFateTiles(libraryTiles, libraryEntries, libraryScroll, columns, true);
            libraryCount.text = "匹配 " + libraryEntries.Count + " 项 · 已选 " + libraryPicks.Count + " 项";
        }
        private void CloseFateLibrary()
        {
            HideFateTips();
            if (librarySearch != null) WindowInputScope.Unregister(librarySearch);
            librarySearch = null; libraryScroll = null; libraryCount = null; libraryTiles.Clear(); libraryEntries.Clear();
            var old = fateLibrary; fateLibrary = null;
            if (old != null) { old.SetActive(false); UnityEngine.Object.Destroy(old); }
        }
        private void ClearFateLibraryView()
        {
            CloseFateLibrary();
            if (fateSearch != null) WindowInputScope.Unregister(fateSearch);
            fateSearch = null; candidateTiles.Clear();
        }
    }
}
