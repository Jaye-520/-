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
        private RectTransform relationViewport, relationPage;
        private GameObject relationOverlay;
        private Text relationSummary, relationDialogStatus;
        private RelationGraph relationGraph;
        private string relationOwner, relationSelection;
        private int relationFilter;
        private readonly HashSet<string> relationExpanded = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, RectTransform> relationCards = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2> relationPositions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private readonly Dictionary<string, RawImage> relationPictures = new Dictionary<string, RawImage>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> relationTextures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly HashSet<string> relationPictureFailures = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<Image> relationLines = new List<Image>();
        private readonly List<bool> relationLineVertical = new List<bool>();
        private readonly List<RelationLink> relationLineLinks = new List<RelationLink>();
        private readonly List<Button> relationFilters = new List<Button>();
        private float relationZoom = 1;
        private bool relationDragging;
        private Vector2 relationDragMouse, relationDragPosition;

        private void BuildRelations()
        {
            relationFilter = 0; relationOwner = null;
            float buttonWidth = Mathf.Min(78, (contentWidth - 214) / 6);
            for (int i = 0; i < CharacterRelations.Filters.Length; i++)
            {
                int filter = i;
                relationFilters.Add(MakeButton(content, "关系筛选" + i, i * buttonWidth, 76, buttonWidth - 4, 32, CharacterRelations.Filters[i], () =>
                { CloseRelationOverlay(); relationFilter = filter; RefreshRelations(true); }));
            }
            MakeButton(content, "添加关系", contentWidth - 208, 76, 98, 32, "添加关系", OpenRelationPicker);
            MakeButton(content, "关系回到中心", contentWidth - 104, 76, 104, 32, "回到中心", CenterRelationGraph);
            relationViewport = Card(content, "关系图视口", 0, 116, contentWidth, contentHeight - 150);
            Fill(relationViewport, "关系图纸底", 0, 0, relationViewport.sizeDelta.x, relationViewport.sizeDelta.y, new Color(.92f, .91f, .87f, .55f), true);
            Border(relationViewport, relationViewport.sizeDelta.x, relationViewport.sizeDelta.y);
            relationViewport.gameObject.AddComponent<RectMask2D>();
            var background = Fill(relationViewport, "关系图拖动区域", 0, 0, relationViewport.sizeDelta.x, relationViewport.sizeDelta.y, new Color(1, 1, 1, .001f), true);
            var listener = background.gameObject.AddComponent<UIEventListener>(); listener.onPointDown = new UnityEvent();
            listener.onPointDown.AddListener((UnityAction)(new Action(() =>
            {
                if (!Input.GetMouseButtonDown(0) || relationOverlay != null) return;
                relationDragging = true; relationDragMouse = RelationMouse(); relationDragPosition = relationPage.anchoredPosition;
                HighlightRelation(null);
            })));
            relationPage = Box("关系图画布", relationViewport, 0, 0, 1, 1);
            relationSummary = Label(content, "", 4, contentHeight - 30, contentWidth - 100, 26, 14, Muted);
            MakeButton(content, "缩小关系图", contentWidth - 94, contentHeight - 30, 42, 26, "－", () => ZoomRelations(-.1f));
            MakeButton(content, "放大关系图", contentWidth - 46, contentHeight - 30, 42, 26, "＋", () => ZoomRelations(.1f));
        }
        private Vector2 RelationMouse()
        {
            Vector2 point;
            var canvas = g.ui.canvas.GetComponent<Canvas>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(relationViewport, Input.mousePosition, canvas == null ? null : canvas.worldCamera, out point);
            return point;
        }
        private bool RelationMouseInside()
        { var p = RelationMouse(); return p.x >= 0 && p.x <= relationViewport.sizeDelta.x && p.y <= 0 && p.y >= -relationViewport.sizeDelta.y; }
        private void CenterRelationGraph()
        {
            CloseRelationOverlay();
            if (relationPage == null || relationViewport == null) return;
            Vector2 center; relationPositions.TryGetValue(selectedUnitId, out center);
            float extentX = Mathf.Max(center.x, relationPage.sizeDelta.x - center.x), extentY = Mathf.Max(center.y, relationPage.sizeDelta.y - center.y);
            relationZoom = Mathf.Clamp(Mathf.Min(1, Mathf.Min((relationViewport.sizeDelta.x - 20) / (extentX * 2), (relationViewport.sizeDelta.y - 20) / (extentY * 2))), .45f, 1);
            relationPage.localScale = new Vector3(relationZoom, relationZoom, 1);
            UpdateRelationLineWidths();
            relationPage.anchoredPosition = new Vector2(relationViewport.sizeDelta.x / 2 - center.x * relationZoom, -relationViewport.sizeDelta.y / 2 + center.y * relationZoom);
        }
        private void ZoomRelations(float delta)
        {
            if (relationPage == null || relationOverlay != null) return;
            float next = Mathf.Clamp(relationZoom + delta, .45f, 1.6f);
            var mouse = RelationMouseInside() ? RelationMouse() : new Vector2(relationViewport.sizeDelta.x / 2, -relationViewport.sizeDelta.y / 2);
            relationPage.anchoredPosition = mouse + (relationPage.anchoredPosition - mouse) * (next / relationZoom);
            relationZoom = next; relationPage.localScale = new Vector3(next, next, 1);
            UpdateRelationLineWidths();
        }
        private void TickRelations()
        {
            if (selectedIndex != 3 || relationPage == null || relationViewport == null) return;
            if (CharacterReader.ContextKey() != contextKey) { CloseRelationOverlay(); return; }
            if (relationOverlay != null) { relationDragging = false; return; }
            if (relationDragging)
            {
                if (!Input.GetMouseButton(0)) relationDragging = false;
                else relationPage.anchoredPosition = relationDragPosition + RelationMouse() - relationDragMouse;
            }
            if (RelationMouseInside() && Input.mouseScrollDelta.y != 0) ZoomRelations(Input.mouseScrollDelta.y * .08f);
            TickRelationPictures();
        }
        private void RefreshRelations(bool force)
        {
            if (relationPage == null || string.IsNullOrEmpty(selectedUnitId)) return;
            bool changedOwner = relationOwner != selectedUnitId;
            if (changedOwner) { CloseRelationOverlay(); relationExpanded.Clear(); relationOwner = selectedUnitId; relationGraph = null; ClearRelationTextures(); }
            var graph = CharacterRelations.ReadGraph(selectedUnitId, relationExpanded, relationFilter);
            if (!force && relationGraph != null && graph.Fingerprint == relationGraph.Fingerprint) return;
            if (relationGraph != null && graph.Fingerprint != relationGraph.Fingerprint) CloseRelationOverlay();
            if (relationGraph != null) foreach (var person in graph.People.Values)
            {
                RelationPerson previous;
                if (relationGraph.People.TryGetValue(person.Id, out previous) && (previous.DramaId != person.DramaId || previous.Dead != person.Dead))
                {
                    Texture2D old;
                    if (relationTextures.TryGetValue(person.Id, out old)) { UnityEngine.Object.Destroy(old); relationTextures.Remove(person.Id); }
                    relationPictureFailures.Remove(person.Id);
                }
            }
            Vector2 previousCenter; bool hadCenter = relationPositions.TryGetValue(selectedUnitId, out previousCenter);
            relationGraph = graph; DrawRelations();
            Vector2 newCenter;
            if (hadCenter && relationPositions.TryGetValue(selectedUnitId, out newCenter))
                relationPage.anchoredPosition += new Vector2((previousCenter.x - newCenter.x) * relationZoom, (newCenter.y - previousCenter.y) * relationZoom);
            if (changedOwner) CenterRelationGraph();
            for (int i = 0; i < relationFilters.Count; i++) relationFilters[i].GetComponent<Image>().color = i == relationFilter ? new Color(.79f, .73f, .55f, .8f) : new Color(.94f, .92f, .84f, .24f);
            var text = "当前 " + CharacterRelations.Filters[relationFilter] + " · " + Math.Max(0, graph.People.Count - 1) + " 人 · 拖动移动，滚轮缩放，点击人物操作";
            if (graph.Errors.Count > 0) text = "部分关系未读取：" + string.Join("；", graph.Errors.ToArray());
            else if (graph.People.Count == 1) text = "当前筛选下没有关系 · 可通过添加关系选择人物";
            relationSummary.text = text; relationSummary.color = graph.Errors.Count > 0 ? Cinnabar : Muted;
        }
        private void DrawRelations()
        {
            for (int i = relationPage.childCount - 1; i >= 0; i--) ReleaseViewObject(relationPage.GetChild(i).gameObject, "关系图旧内容");
            relationCards.Clear(); relationPictures.Clear(); relationPositions.Clear(); relationLines.Clear(); relationLineVertical.Clear(); relationLineLinks.Clear();
            var placed = RelationLayout.Arrange(selectedUnitId, relationGraph.Links);
            float minX = 0, minY = 0, maxX = 0, maxY = 0;
            foreach (var position in placed.Values) { minX = Mathf.Min(minX, position.Column * 126); minY = Mathf.Min(minY, position.Row * 164); maxX = Mathf.Max(maxX, position.Column * 126); maxY = Mathf.Max(maxY, position.Row * 164); }
            var offset = new Vector2(64 - minX, 80 - minY);
            relationPage.sizeDelta = new Vector2(maxX - minX + 128, maxY - minY + 160);
            foreach (var entry in placed) relationPositions[entry.Key] = new Vector2(entry.Value.Column * 126, entry.Value.Row * 164) + offset;
            foreach (var link in relationGraph.Links)
            {
                Vector2 from, to;
                if (!relationPositions.TryGetValue(link.From, out from) || !relationPositions.TryGetValue(link.To, out to)) continue;
                // Orthogonal routes stay behind cards. Reciprocal native links can share a route.
                float bend = (from.y + to.y) / 2;
                RelationLine(link, from.x, from.y, from.x, bend);
                RelationLine(link, from.x, bend, to.x, bend);
                RelationLine(link, to.x, bend, to.x, to.y);
            }
            foreach (var person in relationGraph.People.Values)
            {
                Vector2 position; if (!relationPositions.TryGetValue(person.Id, out position)) continue;
                var captured = person;
                var button = MakeButton(relationPage, "关系人物_" + person.Id, position.x - 46, position.y - 65, 92, 130, "", () => OpenRelationMenu(captured));
                var rect = button.GetComponent<RectTransform>(); relationCards[person.Id] = rect;
                var image = button.GetComponent<Image>(); image.color = person.Id == selectedUnitId ? new Color(.79f, .73f, .55f, .9f) : Paper;
                Border(rect, 92, 130);
                var picture = Texture(rect, "关系人物小立绘", 6, 5, 80, 77, null, new Rect(0, 0, 1, 1)); picture.enabled = false; relationPictures[person.Id] = picture;
                Texture2D existing; if (relationTextures.TryGetValue(person.Id, out existing)) { picture.texture = existing; picture.enabled = true; }
                Label(rect, person.Name, 3, 83, 86, 22, 15, Ink, TextAnchor.MiddleCenter).resizeTextForBestFit = true;
                var labels = new List<string>();
                if (person.Id == selectedUnitId) labels.Add("中心人物");
                else foreach (var link in relationGraph.Links)
                {
                    var label = link.Label;
                    if (link.Type == 1) label = person.Sex == UnitSexType.Man ? "父亲" : person.Sex == UnitSexType.Woman ? "母亲" : "父母";
                    if (link.Type == 2 || link.Type == 3) label = person.Sex == UnitSexType.Man ? "儿子" : person.Sex == UnitSexType.Woman ? "女儿" : "子女";
                    if (link.From != selectedUnitId) label = "关联·" + label;
                    if (link.To == person.Id && !labels.Contains(label)) labels.Add(label);
                }
                var caption = person.Dead ? "已故" : !person.Valid ? "人物失效" : string.Join("／", labels.ToArray());
                Label(rect, caption, 3, 106, 86, 20, 12, person.Dead || !person.Valid ? Cinnabar : Muted, TextAnchor.MiddleCenter);
                if (!person.Valid) Label(rect, person.Dead ? "故" : "？", 8, 14, 76, 62, 30, Muted, TextAnchor.MiddleCenter);
            }
            UpdateRelationLineWidths();
            HighlightRelation(relationSelection);
        }
        private void RelationLine(RelationLink link, float x1, float y1, float x2, float y2)
        {
            if (Math.Abs(x1 - x2) < .01f && Math.Abs(y1 - y2) < .01f) return;
            var line = Fill(relationPage, "关系连线", Mathf.Min(x1, x2), Mathf.Min(y1, y2), Mathf.Max(1, Math.Abs(x2 - x1)), Mathf.Max(1, Math.Abs(y2 - y1)), Gold);
            relationLines.Add(line); relationLineVertical.Add(Math.Abs(x1 - x2) < .01f); relationLineLinks.Add(link);
        }
        private void UpdateRelationLineWidths()
        {
            // Avoid subpixel Image geometry disappearing during canvas pixel adjustment.
            // Compensate only the thin dimension; endpoints and routing still scale with the graph.
            float thickness = 2f / relationZoom;
            for (int i = 0; i < relationLines.Count; i++)
            {
                var rect = relationLines[i].rectTransform;
                var size = rect.sizeDelta;
                rect.sizeDelta = relationLineVertical[i] ? new Vector2(thickness, size.y) : new Vector2(size.x, thickness);
            }
        }
        private void HighlightRelation(string id)
        {
            relationSelection = id;
            for (int i = 0; i < relationLines.Count; i++) relationLines[i].color = id != null && (relationLineLinks[i].From == id || relationLineLinks[i].To == id) ? Cinnabar : new Color(.40f, .41f, .35f, .45f);
            foreach (var card in relationCards) card.Value.GetComponent<Image>().color = card.Key == id ? new Color(.85f, .76f, .58f, 1) : card.Key == selectedUnitId ? new Color(.79f, .73f, .55f, .9f) : Paper;
        }
        private RectTransform RelationDialog(string title)
        {
            CloseRelationOverlay(); relationDragging = false;
            float width = Mathf.Min(530, contentWidth - 24), height = Mathf.Min(470, contentHeight - 20);
            var layer = Box("关系操作遮罩", content, 0, 0, contentWidth, contentHeight); relationOverlay = layer.gameObject;
            var veil = Fill(layer, "关系操作外部", 0, 0, contentWidth, contentHeight, new Color(0, 0, 0, .12f), true);
            var outside = veil.gameObject.AddComponent<Button>(); outside.targetGraphic = veil; outside.onClick.AddListener((UnityAction)(new Action(CloseRelationOverlay)));
            var panel = Card(layer, "关系操作", (contentWidth - width) / 2, (contentHeight - height) / 2, width, height);
            Fill(panel, "关系操作纸底", 0, 0, width, height, Paper, true); Border(panel, width, height);
            Label(panel, title, 14, 8, width - 76, 34, 22, Ink);
            MakeButton(panel, "关闭关系操作", width - 58, 8, 44, 32, "×", CloseRelationOverlay);
            relationDialogStatus = Label(panel, "", 14, height - 52, width - 28, 42, 14, Muted);
            return panel;
        }
        private void OpenRelationMenu(RelationPerson person)
        {
            if (!FinishInlineEdit(false)) return;
            HighlightRelation(person.Id); CloseRelationOverlay();
            RectTransform anchor; if (!relationCards.TryGetValue(person.Id, out anchor)) return;
            float width = Mathf.Min(286, contentWidth - 16), height = Mathf.Min(366, contentHeight - 16);
            var bounds = FateTipBounds(FateRowBounds(anchor, content), width, height, content.sizeDelta);
            var layer = Box("关系人物菜单层", content, 0, 0, contentWidth, contentHeight); relationOverlay = layer.gameObject;
            var outsideImage = Fill(layer, "关系菜单外部", 0, 0, contentWidth, contentHeight, new Color(1, 1, 1, .001f), true);
            var outside = outsideImage.gameObject.AddComponent<Button>(); outside.targetGraphic = outsideImage; outside.onClick.AddListener((UnityAction)(new Action(CloseRelationOverlay)));
            var panel = Card(layer, "关系人物菜单", bounds.x, bounds.y, width, height); Fill(panel, "关系菜单纸底", 0, 0, width, height, Paper, true); Border(panel, width, height);
            Label(panel, person.Id == selectedUnitId ? person.Name + " · 中心人物" : characterName.text + " ↔ " + person.Name, 12, 8, width - 24, 42, 17, Ink);
            string affinity = person.Error ?? (person.Dead ? "已故人物 · 关系记录保留" : "点击操作仅作用于这两个人物");
            RelationPair pair = null;
            if (person.Valid && person.Id != selectedUnitId)
                try { pair = CharacterRelations.Pair(contextKey, selectedUnitId, person.Id); affinity = "正向好感 " + CharacterRelations.Number(pair.AB) + " · 反向 " + CharacterRelations.Number(pair.BA); }
                catch (Exception ex) { affinity = ex.Message; }
            Label(panel, affinity, 12, 51, width - 24, 34, 13, Muted);
            var captured = pair;
            string[] actions = { "修改双向好感", "设置关系", "解除关系", "查看人物", "以此人为中心", relationExpanded.Contains(person.Id) ? "收起关联" : "展开关联" };
            for (int i = 0; i < actions.Length; i++)
            {
                int action = i;
                var button = MakeButton(panel, "关系菜单操作" + i, 12, 89 + i * 42, width - 24, 34, actions[i], () =>
                {
                    if (action == 0) OpenAffinityDialog(captured);
                    else if (action == 1 || action == 2) OpenRelationTypes(captured, action == 2);
                    else if (action == 3 || action == 4) { CloseRelationOverlay(); FocusTarget(person.Id); if (action == 3) Select(0); }
                    else { CloseRelationOverlay(); if (!relationExpanded.Add(person.Id)) relationExpanded.Remove(person.Id); RefreshRelations(true); }
                });
                button.interactable = i < 3 ? pair != null : i < 5 ? person.Valid : person.Error == null && person.Id != selectedUnitId;
            }
        }
        private void OpenAffinityDialog(RelationPair pair)
        {
            if (pair == null) return;
            var panel = RelationDialog("双向好感 · " + pair.NameA + " ↔ " + pair.NameB); float w = panel.sizeDelta.x;
            Label(panel, pair.NameA + " → " + pair.NameB, 16, 58, w - 32, 26, 17, Ink);
            var a = CreateSearchInput(panel, "正向好感", 16, 88, w - 32, 36, "输入好感数值", _ => { }); a.SetTextWithoutNotify(CharacterRelations.Number(pair.AB));
            Label(panel, pair.NameB + " → " + pair.NameA, 16, 140, w - 32, 26, 17, Ink);
            var b = CreateSearchInput(panel, "反向好感", 16, 170, w - 32, 36, "输入好感数值", _ => { }); b.SetTextWithoutNotify(CharacterRelations.Number(pair.BA));
            bool sync = false;
            var toggle = MakeButton(panel, "同步双向好感", 16, 220, w - 32, 34, "□ 同步修改双方", () => { });
            toggle.onClick.AddListener((UnityAction)(new Action(() => { sync = !sync; b.interactable = !sync; toggle.GetComponentInChildren<Text>().text = sync ? "☑ 同步修改双方" : "□ 同步修改双方"; })));
            Label(panel, "身份另行管理；原生好感最多两位小数。\n确认后即时修改，顶部保存负责落盘。", 16, 270, w - 32, 60, 15, Muted);
            MakeButton(panel, "确认双向好感", 16, panel.sizeDelta.y - 98, w - 32, 36, "确认修改", () =>
            {
                if (!string.IsNullOrEmpty(Input.compositionString)) return;
                RunRelationOperation(pair, () => RelationOperations.SetAffinity(pair, CharacterRelations.Parse(a.text), CharacterRelations.Parse(sync ? a.text : b.text)));
            });
        }
        private void OpenRelationTypes(RelationPair pair, bool remove)
        {
            if (pair == null) return;
            var panel = RelationDialog((remove ? "解除关系 · " : "设置关系 · ") + pair.NameA + " ↔ " + pair.NameB);
            float w = panel.sizeDelta.x, h = panel.sizeDelta.y;
            Label(panel, "以下称谓指右侧人物相对于左侧人物的身份。\n好友／仇人由原生好感阈值判定，通过好感编辑调整。", 14, 48, w - 28, 48, 15, Muted);
            var scroll = ItemScroll(panel, "关系类型列表", 14, 104, w - 28, h - 170); AddMartialScrollbar(scroll);
            var types = remove ? pair.Types.ToArray() : CharacterRelations.EditableTypes;
            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Mathf.Max(scroll.viewport.sizeDelta.y, types.Length * 66));
            for (int i = 0; i < types.Length; i++)
            {
                int type = types[i]; string unavailable;
                try { unavailable = RelationOperations.Unavailable(pair, type, remove); } catch (Exception ex) { unavailable = ex.Message; }
                var row = MakeButton(scroll.content, "关系类型" + type, 0, i * 66, scroll.content.sizeDelta.x - 14, 60, "", () => ConfirmRelation(pair, type, remove));
                Label(row.GetComponent<RectTransform>(), (remove ? "解除 " : "设为 ") + CharacterRelations.TypeName(type), 10, 4, scroll.content.sizeDelta.x - 34, 24, 18, Ink);
                Label(row.GetComponent<RectTransform>(), unavailable ?? "点击查看双方与操作后果，再确认", 10, 30, scroll.content.sizeDelta.x - 34, 26, 12, unavailable == null ? Muted : Cinnabar);
                row.interactable = unavailable == null;
            }
            if (types.Length == 0) relationDialogStatus.text = "双方没有可解除的身份关系";
        }
        private void ConfirmRelation(RelationPair pair, int type, bool remove)
        {
            var panel = RelationDialog((remove ? "确认解除" : "确认建立") + CharacterRelations.TypeName(type)); float w = panel.sizeDelta.x;
            Label(panel, pair.NameA + " ↔ " + pair.NameB + "\n右侧人物身份：" + CharacterRelations.TypeName(type) + "\n\n" +
                (type <= 2 ? "原生血亲动作会更新父母、子女、兄弟姐妹与后代姓氏。只允许父母槽为空的人物，不替换已有家谱。" :
                "执行游戏原生关系动作，可能改变称谓、关系缓存、行为与声望。不会替换其他人物的已有身份。") +
                "\n\n确认后即时改变游戏内存，自动保存也可能落盘；此操作不提供撤销。", 16, 60, w - 32, panel.sizeDelta.y - 180, 17, Ink);
            MakeButton(panel, "确认关系动作", 16, panel.sizeDelta.y - 98, w - 32, 36, remove ? "确认解除关系" : "确认建立关系", () => RunRelationOperation(pair, () => RelationOperations.SetRelation(pair, type, remove)));
        }
        private void OpenRelationPicker()
        {
            if (!FinishInlineEdit(false)) return;
            var owner = selectedUnitId; var context = contextKey;
            var panel = RelationDialog("添加关系 · 为" + characterName.text + "选择人物"); float w = panel.sizeDelta.x, h = panel.sizeDelta.y;
            var scroll = ItemScroll(panel, "关系候选人物", 14, 94, w - 28, h - 156); AddMartialScrollbar(scroll);
            var candidates = new List<CharacterEntry>(); var buttons = new List<Button>();
            int pool = (int)Math.Ceiling(scroll.viewport.sizeDelta.y / 38) + 1;
            Action render = () =>
            {
                int first = Math.Max(0, (int)(scroll.content.anchoredPosition.y / 38));
                for (int slot = 0; slot < buttons.Count; slot++)
                {
                    int index = first + slot; var button = buttons[slot]; button.gameObject.SetActive(index < candidates.Count);
                    if (index >= candidates.Count) continue;
                    button.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -index * 38);
                    button.GetComponentInChildren<Text>().text = candidates[index].Name + (candidates[index].IsPlayer ? "（自己）" : "");
                }
            };
            for (int slot = 0; slot < pool; slot++)
            {
                int captured = slot;
                buttons.Add(MakeButton(scroll.content, "关系候选行" + slot, 0, 0, scroll.content.sizeDelta.x - 14, 34, "", () =>
                {
                    int index = Math.Max(0, (int)(scroll.content.anchoredPosition.y / 38)) + captured;
                    if (index >= candidates.Count || owner != selectedUnitId || context != contextKey) return;
                    try { OpenRelationTypes(CharacterRelations.Pair(context, owner, candidates[index].Id), false); }
                    catch (Exception ex) { relationDialogStatus.text = ex.Message; }
                }));
            }
            Action<string> filter = query =>
            {
                candidates.Clear(); var q = (query ?? "").Trim();
                foreach (var entry in roster) if (entry.Id != owner && (q.Length == 0 || entry.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || entry.Id.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)) candidates.Add(entry);
                scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Mathf.Max(scroll.viewport.sizeDelta.y, candidates.Count * 38));
                scroll.content.anchoredPosition = Vector2.zero; render(); relationDialogStatus.text = "找到 " + candidates.Count + " 人 · 点击后选择关系种类";
            };
            scroll.onValueChanged.AddListener((UnityAction<Vector2>)(new Action<Vector2>(_ => render())));
            CreateSearchInput(panel, "关系人物搜索", 14, 48, w - 28, 34, "姓名 / ID", filter); filter("");
        }
        private void RunRelationOperation(RelationPair pair, Func<string> operation)
        {
            if (CharacterSave.Busy || DataMgr.isSaveData) { relationDialogStatus.text = "正在保存，请稍候"; return; }
            if (pair.A != selectedUnitId || pair.Context != contextKey) { CloseRelationOverlay(); SetEditStatus("人物或存档已变化，请重新操作", true); return; }
            if (edits.Count > 0) { relationDialogStatus.text = "请先保存属性草稿，再执行关系修改"; return; }
            int pending = CharacterSave.Pending;
            try { var message = operation(); CloseRelationOverlay(); RefreshCharacter(true); SetEditStatus(message); }
            catch (Exception ex)
            {
                if (CharacterSave.Pending != pending) { Debug.LogWarning("[八荒秘录] 关系操作未完整确认 " + pair.A + " ↔ " + pair.B + "：" + ex); CloseRelationOverlay(); RefreshCharacter(true); }
                else if (relationDialogStatus != null) { relationDialogStatus.text = ex.Message; relationDialogStatus.color = Cinnabar; }
                SetEditStatus(ex.Message, true);
            }
        }
        private void CloseRelationOverlay()
        {
            var old = relationOverlay; relationOverlay = null; relationDialogStatus = null;
            if (old != null) foreach (var input in old.GetComponentsInChildren<InputField>(true)) Cleanup(() => WindowInputScope.Unregister(input), "关系输入登记");
            ReleaseViewObject(old, "关系浮层");
        }
        private void ClearRelationTextures()
        {
            CancelRelationPicture();
            foreach (var texture in relationTextures.Values) if (texture != null) Cleanup(() => UnityEngine.Object.Destroy(texture), "关系静态立绘");
            relationTextures.Clear(); relationPictureFailures.Clear();
        }
        private void ClearRelationsView()
        {
            CloseRelationOverlay(); ClearRelationTextures(); relationDragging = false;
            relationGraph = null; relationOwner = null; relationSelection = null; relationPage = null; relationViewport = null; relationSummary = null;
            relationCards.Clear(); relationPictures.Clear(); relationPositions.Clear(); relationLines.Clear(); relationLineVertical.Clear(); relationLineLinks.Clear(); relationExpanded.Clear(); relationFilters.Clear();
        }
    }
}
