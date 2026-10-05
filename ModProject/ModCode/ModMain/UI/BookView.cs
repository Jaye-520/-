using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MOD_YtxSQ1.Services;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MOD_YtxSQ1.UI
{
    // 原生窗口只承载生命周期和字体。布局使用自己的单一边界，不借用未知 Sprite。
    internal sealed partial class BookView
    {
        private const float MaximumWidth = 1760;
        private const float MaximumHeight = 900;
        private static readonly Color Ink = new Color(.18f, .19f, .17f, 1f);
        private static readonly Color Muted = new Color(.37f, .38f, .33f, 1f);
        private static readonly Color Paper = new Color(.862f, .855f, .832f, 1f);
        private static readonly Color Gold = new Color(.48f, .49f, .41f, 1f);
        private static readonly Color Cinnabar = new Color(.56f, .25f, .19f, 1f);
        private static readonly string[] Modules = { "属性", "道具", "功法", "关系", "功能", "批量", "保存" };
        private Texture2D closeTexture;
        private const float BodyTop = 124;
        private readonly List<Graphic> hiddenGraphics = new List<Graphic>();
        private readonly List<Button> nativeButtons = new List<Button>();
        private readonly List<bool> nativeButtonStates = new List<bool>();
        private readonly List<bool> nativeButtonActiveStates = new List<bool>();
        private readonly List<Text> tabLabels = new List<Text>();
        private readonly List<GameObject> tabFrames = new List<GameObject>();
        private GameObject root;
        private CanvasGroup appearance;
        private float appearanceStarted;
        private bool appearing;
        private const float AppearanceDuration = .16f;

        private void TickAppearance()
        {
            if (!appearing || appearance == null) return;
            var progress = Math.Min(1f, Math.Max(0f, (Time.unscaledTime - appearanceStarted) / AppearanceDuration));
            appearance.alpha = progress * progress * (3f - 2f * progress);
            if (progress >= 1f) { appearance.interactable = true; appearing = false; CompleteInteractionTiming(); }
        }
        private RectTransform content;
        private Font font;
        private Font calligraphyFont;
        private PaperSkin skin;
        private int selectedIndex = -1;
        private Action requestClose;
        private Text characterName;
        private static bool fontCreationUnavailable;
        private Text characterRealm;
        private readonly List<Text[]> attributeTexts = new List<Text[]>();
        private readonly List<ScrollRect> fateColumns = new List<ScrollRect>();
        private RectTransform fateTip, fateTipOwner;
        private float fateTipCloseAt;
        private ScrollRect itemTipScroll;
        private float itemBagWheelSensitivity;
        private ScrollRect itemTipSourceScroll;
        private int itemTipWheelFrame = -1;
        private CharacterFate[] fates = new CharacterFate[0];
        private string fateReason, fateUnitId;
        private float nextCharacterRefresh;
        private string lastReadFailure, lastFateFailure;
        private float contentWidth;
        private float contentHeight;
        private float bookWidth, targetWidth;
        private RectTransform targetPanel, rosterContent;
        private ScrollRect rosterScroll;
        private InputField targetSearch;
        internal InputField SearchInput { get { return targetSearch; } }
        private string searchQuery = "";
        private int inputFocusDiagnostics;
        private Text rosterCount, portraitStatus;
        private RawImage portraitImage;
        private RawImage specialPortraitImage;
        private GameObject ownedPortraitRoot;
        private ModelRenderTexture portraitRenderer;
        private string portraitKey, selectedUnitId, contextKey;
        private List<CharacterEntry> roster = new List<CharacterEntry>();
        private readonly List<CharacterEntry> filteredRoster = new List<CharacterEntry>();
        private readonly List<Button> rosterRows = new List<Button>();
        private readonly List<string> rowIds = new List<string>();
        private float rosterHeight;
        private const float RosterRowHeight = 38;
        private System.Diagnostics.Stopwatch openingTimer;
        private long openingCheckpoint;
        private string openingStages;

        private void RecordOpeningStage(string name)
        {
            if (openingTimer == null) return;
            var elapsed = openingTimer.ElapsedMilliseconds;
            openingStages += "；" + name + "=" + (elapsed - openingCheckpoint) + "ms";
            openingCheckpoint = elapsed;
        }

        public void Build(UITextInfoLong window, Action close, string initialUnitId = null)
        {
            var openTimer = System.Diagnostics.Stopwatch.StartNew();
            openingTimer = openTimer;
            openingCheckpoint = 0;
            openingStages = "";
            requestClose = close;
            selectedUnitId = string.IsNullOrEmpty(initialUnitId) ? CharacterReader.Resolve(null)?.data.unitData.unitID : initialUnitId;
            ResolveFonts(window);
            try { skin = PaperSkin.Acquire(); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 纸墨材质回退为纯色：" + ex.Message); }
            RecordOpeningStage("字体/纸墨纹理");
            HideNativeControls(window);

            var overlay = NewRect("界面遮罩", window.transform);
            root = overlay.gameObject;
            appearance = root.AddComponent<CanvasGroup>();
            appearance.alpha = 0;
            appearance.interactable = false;
            appearance.blocksRaycasts = true;
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            var veil = overlay.gameObject.AddComponent<Image>();
            veil.color = new Color(0, 0, 0, .34f);

            var book = NewRect("秘录外框", overlay);
            book.anchorMin = book.anchorMax = new Vector2(.5f, .5f);
            book.pivot = new Vector2(.5f, .5f);
            book.anchoredPosition = Vector2.zero;
            var available = g.ui.canvas.GetComponent<RectTransform>().rect.size;
            var width = Mathf.Min(MaximumWidth, available.x * .96f);
            var height = Mathf.Min(MaximumHeight, available.y * .94f);
            bookWidth = width;
            if (width < 850 || height < 620) throw new InvalidOperationException("当前画布过小，无法清晰显示综合面板。");
            book.sizeDelta = new Vector2(width, height);

            // 外框、页眉、正文、页脚共享同一个完整矩形，不使用带留白的背景素材。
            Fill(book, "完整纸底", 0, 0, width, height, Paper, true);
            if (skin != null && skin.Landscape != null)
                Texture(book, "山水宣纸", 0, 0, width, height, skin.Landscape, new Rect(0, 0, 1, 1));
            else if (skin != null) Texture(book, "纸纤维", 6, 6, width - 12, height - 12, skin.Paper,
                new Rect(0, 0, (width - 12) / 192f, (height - 12) / 192f));
            var header = Box("题签区", book, 16, 10, width - 32, 50);
            var title = Box("墨迹题签", header, (width - 32 - 330) / 2f, 0, 330, 48);
            // UV取透明素材的墨迹范围，避免图像上下留白使标题墨底过薄。
            if (skin != null && skin.TitleBrush != null)
                Texture(title, "参考标题墨底", 0, 0, 330, 48, skin.TitleBrush,
                    new Rect(24f / 2172f, 170f / 724f, 2128f / 2172f, 376f / 724f));
            else if (skin != null) Texture(title, "墨迹", 0, 0, 330, 48, skin.Brush, new Rect(0, 0, 1, 1));
            else Fill(title, "墨底", 12, 4, 306, 40, Ink);
            Label(title, "八荒秘录", 0, 0, 330, 48, 30, new Color(.95f, .94f, .90f, 1f), TextAnchor.MiddleCenter);
            MakeCloseButton(header, width - 82, 4, close);
            Fill(book, "标题分界", 20, 60, width - 40, 1, Gold);

            BuildNavigation(book, width);
            targetWidth = width < 1300 ? 190f : 224f;
            var bodyHeight = height - BodyTop - 50;
            BuildTargetPanel(book, bodyHeight);
            contentWidth = width - targetWidth - 76;
            contentHeight = bodyHeight;
            contentHost = content = Box("卷页", book, targetWidth + 52, BodyTop, contentWidth, contentHeight);
            preparedCanvas = available; preparedContext = CharacterReader.ContextKey();

            Fill(book, "页脚分界", 24, height - 40, width - 48, 1, Gold);
            var footer = Box("页脚", book, 24, height - 36, width - 48, 28);
            // 两侧对称留白，署名始终相对整个窗口居中，不随页面或读取状态变化。
            Label(footer, "Jaye@2026-爱你一生一世", 214, 1, width - 48 - 428, 24, 16, Ink, TextAnchor.MiddleCenter);
            Label(footer, ModVersion.Current + " / Esc 合上", width - 254, 1, 190, 24, 16, Muted, TextAnchor.MiddleRight);
            RecordOpeningStage("窗口骨架/搜索模板");
            PreparePages(); RecordOpeningStage("静态六页/配置索引");
            ReloadRoster(); RecordOpeningStage("人物名单"); Select(0);
            if (!string.IsNullOrEmpty(initialUnitId)) ScrollToInitialUnit(initialUnitId);
            RecordOpeningStage("完成绑定");
            Debug.Log("[八荒秘录] " + ModVersion.Current + "界面构建=" + openTimer.ElapsedMilliseconds + "ms；画布=" + available + "；窗口=" + width + "×" + height + openingStages);
            openingTimer = null;
            openingStages = null;
            appearanceStarted = Time.unscaledTime;
            appearing = true;
        }

        // 原生中文字体优先；楷体只作标题字，取不到或创建失败就回退，且失败的创建不再重试。
        private void ResolveFonts(UITextInfoLong window)
        {
            foreach (var text in window.GetComponentsInChildren<Text>(true))
            {
                if (text.font != null && (font == null || text.text.Length > 20)) font = text.font;
            }
            if (font == null) throw new InvalidOperationException("未找到原生中文字体。");
            try
            {
                if (!fontCreationUnavailable) calligraphyFont = Font.CreateDynamicFontFromOSFont("KaiTi", 24);
                if (calligraphyFont != null && !calligraphyFont.HasCharacter('荒'))
                {
                    UnityEngine.Object.Destroy(calligraphyFont);
                    calligraphyFont = null;
                }
            }
            catch (Exception ex)
            {
                fontCreationUnavailable = true;
                var failedFont = calligraphyFont;
                calligraphyFont = null;
                if (failedFont != null) Cleanup(() => UnityEngine.Object.Destroy(failedFont), "失败字体");
                Debug.LogWarning("[八荒秘录] 楷体回退为游戏字体：" + ex.Message);
            }
        }

        // 隐藏并停用原生承载窗口的全部可见控件，同时记录原状态供释放时恢复。
        private void HideNativeControls(UITextInfoLong window)
        {
            foreach (var graphic in window.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.enabled) continue;
                hiddenGraphics.Add(graphic);
                graphic.enabled = false;
            }
            foreach (var button in window.GetComponentsInChildren<Button>(true))
            {
                nativeButtons.Add(button);
                nativeButtonStates.Add(button.interactable);
                nativeButtonActiveStates.Add(button.gameObject.activeSelf);
                button.interactable = false;
                button.gameObject.SetActive(false);
            }
        }

        // 七项卷目；末项“保存”是操作而非页面，其余切换页面。
        private void BuildNavigation(RectTransform book, float width)
        {
            var navigation = Box("卷目区", book, 24, 64, width - 48, 44);
            var tabWidth = (width - 48 - (Modules.Length - 1) * 12) / Modules.Length;
            for (int i = 0; i < Modules.Length; i++)
            {
                int index = i;
                var tab = MakeButton(navigation, "卷目" + i, i * (tabWidth + 12), 0, tabWidth, 42, Modules[i], () => { if (index == 6) SaveEdits(); else Select(index); });
                var rect = tab.GetComponent<RectTransform>();
                var label = tab.GetComponentInChildren<Text>();
                tab.GetComponent<Image>().color = new Color(.38f, .38f, .32f, .025f);
                // 保存只执行操作，不创建永远不会显示的页面选中框。
                if (index < Modules.Length - 1)
                {
                    tabFrames.Add(BuildNavigationFrame(rect, tabWidth));
                    label.transform.SetAsLastSibling();
                }
                tabLabels.Add(label);
            }
        }

        // 独立装饰层：细金框与朱砂折角，所有图形不接收指针。
        private GameObject BuildNavigationFrame(RectTransform tab, float width)
        {
            var frame = Box("选页金框", tab, 0, 0, width, 42);
            Fill(frame, "暖纸", 0, 0, width, 42, new Color(.94f, .90f, .79f, .88f));
            var metal = new Color(.52f, .40f, .23f, .92f);
            Fill(frame, "金框上", 0, 0, width, 1.25f, metal);
            Fill(frame, "金框下", 0, 40.75f, width, 1.25f, metal);
            Fill(frame, "金框左", 0, 0, 1.25f, 42, metal);
            Fill(frame, "金框右", width - 1.25f, 0, 1.25f, 42, metal);
            var fine = new Color(.64f, .48f, .27f, .34f);
            Fill(frame, "内框上", 4, 4, width - 8, .75f, fine);
            Fill(frame, "内框下", 4, 37.25f, width - 8, .75f, fine);
            // 四角短线形成收束的朱砂书签，不侵入文字区。
            for (int corner = 0; corner < 4; corner++)
            {
                bool right = corner % 2 == 1, bottom = corner >= 2;
                Fill(frame, "朱砂横角" + corner, right ? width - 17 : 4,
                    bottom ? 36.5f : 4, 13, 1.5f, Cinnabar);
                Fill(frame, "朱砂竖角" + corner, right ? width - 5.5f : 4,
                    bottom ? 30 : 4, 1.5f, 8, Cinnabar);
            }
            frame.gameObject.SetActive(false);
            return frame.gameObject;
        }

        // 只在真正切页时更新旧项与新项；装饰无需逐帧刷新。
        private void SetNavigationSelection(int index)
        {
            if (selectedIndex >= 0)
            {
                tabFrames[selectedIndex].SetActive(false);
                tabLabels[selectedIndex].color = Ink;
            }
            if (index < 0) return;
            tabFrames[index].SetActive(true);
            tabLabels[index].color = Cinnabar;
        }

        // 从玉简入口打开时把名单滚到目标人物；无有效目标或无需滚动时只重绘行。
        private void ScrollToInitialUnit(string unitId)
        {
            var targetIndex = filteredRoster.FindIndex(entry => entry.Id == unitId);
            var range = Mathf.Max(0, rosterContent.sizeDelta.y - rosterHeight);
            if (targetIndex >= 0 && range > 0)
                rosterScroll.verticalNormalizedPosition = 1 - Math.Min(1, Math.Max(0, targetIndex * RosterRowHeight / range));
            RenderRosterRows();
        }

        private void Select(int index)
        {
            if (index == selectedIndex || index < 0 || index >= pages.Count) return;
            if (!FinishInlineEdit(false)) return;
            using (WorkshopPerformance.Measure("切页_" + Modules[index]))
            {
                if (selectedIndex >= 0) pages[selectedIndex].Deactivate();
                var page = pages[index];
                targetPanel.gameObject.SetActive(page.UsesTarget);
                contentHost.anchoredPosition = new Vector2(page.UsesTarget ? targetWidth + 52 : 24, -BodyTop);
                contentHost.sizeDelta = new Vector2(page.Root.sizeDelta.x, contentHeight);
                SetNavigationSelection(index); selectedIndex = index;
                page.Activate();
                if (!page.UsesTarget) ReleasePortrait();
                else if (index != 0 && portraitKey == null) UpdatePortrait();
                if (index == 0) RefreshCharacter(true);
                else
                {
                    var previousRefresh = nextCharacterRefresh; RefreshCharacter();
                    if (!suspended && nextCharacterRefresh == previousRefresh) page.Refresh(false);
                }
            }
        }

        private void BuildTargetPanel(RectTransform book, float height)
        {
            targetPanel = Card(book, "对象栏", 24, BodyTop, targetWidth, height);
            Label(targetPanel, "操作对象", 14, 12, targetWidth - 28, 30, 21, Ink);
            rosterCount = Label(targetPanel, "", 14, 43, targetWidth - 28, 20, 14, Muted);
            BuildSearchInput();
            rosterHeight = height > 600 ? 248 : Mathf.Max(112, height * .30f);
            var viewport = Box("名单视口", targetPanel, 12, 116, targetWidth - 24, rosterHeight);
            Fill(viewport, "名单底", 0, 0, targetWidth - 24, rosterHeight, new Color(1, 1, 1, .01f), true);
            viewport.gameObject.AddComponent<RectMask2D>();
            rosterContent = NewRect("名单长页", viewport);
            rosterContent.anchorMin = rosterContent.anchorMax = new Vector2(0, 1);
            rosterContent.pivot = new Vector2(0, 1);
            rosterContent.sizeDelta = new Vector2(targetWidth - 38, rosterHeight);
            rosterScroll = viewport.gameObject.AddComponent<ScrollRect>();
            rosterScroll.viewport = viewport; rosterScroll.content = rosterContent;
            rosterScroll.horizontal = false; rosterScroll.vertical = true;
            rosterScroll.inertia = false; rosterScroll.movementType = ScrollRect.MovementType.Clamped;
            rosterScroll.scrollSensitivity = 32;
            var track = Fill(viewport, "名单滚动轨", targetWidth - 32, 0, 6, rosterHeight, new Color(.35f, .34f, .27f, .12f), true);
            var handle = ScrollHandle(track, "名单滚动柄", 36, new Color(.36f, .34f, .27f, .6f));
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop; rosterScroll.verticalScrollbar = scrollbar;
            int poolSize = (int)Math.Ceiling(rosterHeight / RosterRowHeight) + 1;
            for (int i = 0; i < poolSize; i++)
            {
                int slot = i;
                var row = MakeButton(rosterContent, "修士行" + i, 0, 0, targetWidth - 38, 34, "", () => SelectTarget(slot));
                row.GetComponentInChildren<Text>().fontSize = 16;
                rosterRows.Add(row); rowIds.Add(null);
            }
            rosterScroll.onValueChanged.AddListener((UnityAction<Vector2>)(new Action<Vector2>(_ => RenderRosterRows())));
            float summaryTop = 116 + rosterHeight + 12;
            BuildRestoreButton(targetPanel, summaryTop, targetWidth - 28);
            summaryTop += 40;
            characterName = Label(targetPanel, "正在读取", 14, summaryTop, targetWidth - 28, 28, 20, Ink);
            characterRealm = Label(targetPanel, "—", 14, summaryTop + 30, targetWidth - 28, 24, 17, Muted);
            float portraitTop = summaryTop + 62;
            float portraitHeight = height - portraitTop - 12;
            var portraitBox = Box("立绘区", targetPanel, 12, portraitTop, targetWidth - 24, portraitHeight);
            portraitBox.gameObject.AddComponent<RectMask2D>();
            portraitImage = Texture(portraitBox, "人物立绘", 0, 0, targetWidth - 24, portraitHeight, null, new Rect(0, 0, 1, 1));
            portraitImage.color = Color.white;
            portraitImage.enabled = false;
            // 裁去上方空白并保留底边，使已验证位置上的人物放大30%。
            var visible = 1f / 1.30f;
            portraitImage.uvRect = new Rect((1 - visible) / 2, 0, visible, visible);
            portraitStatus = Label(portraitBox, "", 6, 0, targetWidth - 36, portraitHeight, 16, Muted, TextAnchor.MiddleCenter);
        }

        private void ReloadRoster()
        {
            contextKey = CharacterReader.ContextKey();
            string reason;
            bool ready;
            using (WorkshopPerformance.Measure("人物名单读取")) ready = CharacterRoster.TryRead(out roster, out reason);
            FilterRoster();
            if (!ready) rosterCount.text = reason;
        }

        private void BuildSearchInput()
        {
            var uiName = UIType.ModWorkshopUpload.uiName;
            var path = uiName.StartsWith("UI/", StringComparison.Ordinal) ? uiName : "UI/" + uiName;
            var prefab = g.res.Load<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("未加载输入预制体：" + path);
            var source = prefab.GetComponentInChildren<InputField>(true);
            if (source == null || source.textComponent == null)
                throw new InvalidOperationException("原生InputField模板缺少文字组件");
            var host = Box("对象搜索", targetPanel, 12, 70, targetWidth - 24, 36);
            var clone = UnityEngine.Object.Instantiate(source.gameObject, host, false);
            clone.SetActive(false);
            clone.name = "BHML_姓名输入";
            var rect = clone.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            targetSearch = clone.GetComponent<InputField>();
            // 模板的EventTrigger仍带有原窗口回调，替换文字事件并不会清除它们。
            // 参照鬼谷ModLib的控件复制初始化，先停用并移除这些事件代理。
            foreach (var trigger in clone.GetComponentsInChildren<EventTrigger>(true))
            {
                trigger.enabled = false;
                UnityEngine.Object.Destroy(trigger);
            }
            var inheritedActivation = targetSearch.shouldActivateOnSelect;
            targetSearch.shouldActivateOnSelect = true;
            targetSearch.onValidateInput = null;
            targetSearch.navigation = new Navigation { mode = Navigation.Mode.None };
            targetSearch.onValueChanged = new InputField.OnChangeEvent();
            targetSearch.onEndEdit = new InputField.SubmitEvent();
            targetSearch.contentType = InputField.ContentType.Standard;
            targetSearch.lineType = InputField.LineType.SingleLine;
            targetSearch.characterValidation = InputField.CharacterValidation.None;
            targetSearch.readOnly = false; targetSearch.interactable = true;
            targetSearch.characterLimit = 48;
            var textRect = targetSearch.textComponent.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(.5f, .5f);
            textRect.localScale = Vector3.one;
            textRect.offsetMin = new Vector2(8, 4);
            textRect.offsetMax = new Vector2(-8, -4);
            targetSearch.textComponent.fontSize = 17;
            targetSearch.textComponent.resizeTextForBestFit = false;
            targetSearch.textComponent.alignment = TextAnchor.MiddleLeft;
            // 光标按字体行度量生成；不要继承模板的几何对齐或多行行距。
            targetSearch.textComponent.alignByGeometry = false;
            targetSearch.textComponent.lineSpacing = 1f;
            targetSearch.textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
            targetSearch.textComponent.verticalOverflow = VerticalWrapMode.Truncate;
            targetSearch.textComponent.color = Ink;
            var placeholder = targetSearch.placeholder.TryCast<Text>();
            if (placeholder != null)
            {
                placeholder.text = "搜索修士姓名"; placeholder.fontSize = 16; placeholder.color = Muted;
                placeholder.alignment = TextAnchor.MiddleLeft;
                placeholder.alignByGeometry = false; placeholder.lineSpacing = 1f;
                placeholder.rectTransform.pivot = new Vector2(.5f, .5f);
                placeholder.rectTransform.localScale = Vector3.one;
                placeholder.rectTransform.anchorMin = Vector2.zero; placeholder.rectTransform.anchorMax = Vector2.one;
                placeholder.rectTransform.offsetMin = new Vector2(8, 4); placeholder.rectTransform.offsetMax = new Vector2(-8, -4);
                placeholder.raycastTarget = false;
            }
            targetSearch.textComponent.raycastTarget = false;
            FrameSearchInput(host, targetSearch);
            targetSearch.SetTextWithoutNotify(searchQuery);
            targetSearch.onValueChanged.AddListener((UnityAction<string>)(new Action<string>(query =>
            {
                searchQuery = query ?? "";
                FilterRoster();
            })));
            // 有限诊断：保留实际取消选中的调用栈，不逐帧重抢焦点。
            targetSearch.onEndEdit.AddListener((UnityAction<string>)(new Action<string>(_ =>
            {
                if (inputFocusDiagnostics++ >= 2) return;
                Debug.Log("[八荒秘录] 搜索结束编辑；isFocused=" + targetSearch.isFocused +
                    "；调用栈=" + Environment.StackTrace);
            })));
            clone.SetActive(true);
            targetSearch.AssignPositioningIfNeeded();
            targetSearch.UpdateLabel();
            Debug.Log("[八荒秘录] 内联输入模板：" + path + "；模板自动编辑=" + inheritedActivation +
                "；已清除EventTrigger并启用自动编辑");
        }

        private void FilterRoster()
        {
            filteredRoster.Clear();
            var query = searchQuery;
            foreach (var entry in roster)
                if (entry.Name.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) filteredRoster.Add(entry);
            rosterContent.sizeDelta = new Vector2(targetWidth - 38, Mathf.Max(rosterHeight, filteredRoster.Count * RosterRowHeight));
            rosterScroll.verticalNormalizedPosition = 1;
            rosterCount.text = roster.Count == 0 ? "暂无可选修士" : filteredRoster.Count + " / " + roster.Count + " 位修士";
            RenderRosterRows();
        }

        private void RenderRosterRows()
        {
            if (rosterScroll == null) return;
            var range = Mathf.Max(0, rosterContent.sizeDelta.y - rosterHeight);
            int start = (int)Math.Floor((1 - rosterScroll.verticalNormalizedPosition) * range / RosterRowHeight);
            for (int i = 0; i < rosterRows.Count; i++)
            {
                int index = start + i;
                var row = rosterRows[i];
                var valid = index >= 0 && index < filteredRoster.Count;
                row.gameObject.SetActive(valid);
                rowIds[i] = valid ? filteredRoster[index].Id : null;
                if (!valid) continue;
                var entry = filteredRoster[index];
                row.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -index * RosterRowHeight);
                var text = row.GetComponentInChildren<Text>();
                text.text = (entry.IsPlayer ? "自己 · " : "") + entry.Name;
                var selected = entry.Id == selectedUnitId || (selectedUnitId == null && entry.IsPlayer);
                text.color = selected ? Cinnabar : Ink;
                row.GetComponent<Image>().color = selected ? new Color(.70f, .59f, .40f, .20f) : new Color(.94f, .92f, .84f, .24f);
            }
        }

        private void SelectTarget(int slot)
        {
            if (rowIds[slot] == null || !FinishInlineEdit(false)) return;
            FocusTarget(rowIds[slot]);
        }

        public void FocusTarget(string unitId)
        {
            if (string.IsNullOrEmpty(unitId) || !FinishInlineEdit(false)) return;
            CloseDropdown(); HideFateTips(); CloseItemDialog(); CloseItemLibrary(); CloseFateLibrary(); CloseMartialOverlay(); CloseRelationOverlay();
            selectedUnitId = unitId;
            ReleasePortrait();
            RefreshCharacter(true);
            ScrollToInitialUnit(unitId);
        }

        private void UpdatePortrait()
        {
            var key = contextKey + ":" + selectedUnitId;
            var stage = "解析人物身份";
            try
            {
                var unit = CharacterReader.Resolve(selectedUnitId);
                if (unit == null) { ReleasePortrait(); return; }
                var dramaId = CharacterPortrait.GetDramaId(unit);
                key += ":" + dramaId + ":" + CharacterPortrait.AppearanceKey(unit, dramaId);
                if (portraitKey == key) return;
                ReleasePortrait();
                portraitKey = key;
                if (dramaId > 0)
                {
                    stage = "原生特殊立绘创建，剧情ID=" + dramaId;
                    var rect = portraitImage.rectTransform;
                    specialPortraitImage = Texture(rect, "特殊人物立绘", 0, 0, rect.sizeDelta.x, rect.sizeDelta.y, null, portraitImage.uvRect);
                    CharacterPortrait.CreateSpecialTexture(dramaId, specialPortraitImage, null);
                    // 原生预制体分支会关闭RawImage，由其子模型直接显示。
                    if (specialPortraitImage.enabled && specialPortraitImage.texture == null)
                        throw new InvalidOperationException("原生入口未返回立绘纹理");
                    portraitStatus.text = "";
                    return;
                }
                stage = "普通立绘模型创建";
                var model = PortraitModel.CreateModel(unit, false, false, null);
                if (model == null || model.root == null) throw new InvalidOperationException("未返回立绘模型");
                ownedPortraitRoot = model.root;
                portraitRenderer = new ModelRenderTexture(ownedPortraitRoot, portraitImage, new Vector2(0, -8));
                portraitImage.enabled = true;
                stage = "普通立绘取景";
                FitPortraitCamera();
                portraitStatus.text = "";
            }
            catch (Exception ex)
            {
                ReleasePortrait();
                portraitKey = key;
                portraitStatus.text = "立绘暂不可用";
                Debug.LogWarning("[八荒秘录] 立绘加载失败（" + stage + "）：" + ex);
            }
        }

        private void ReleasePortrait()
        {
            portraitKey = null;
            var special = specialPortraitImage; specialPortraitImage = null;
            var renderer = portraitRenderer; portraitRenderer = null;
            var model = ownedPortraitRoot; ownedPortraitRoot = null;
            if (portraitImage != null)
            {
                Cleanup(() => portraitImage.texture = null, "清空立绘纹理");
                Cleanup(() => portraitImage.enabled = false, "隐藏立绘");
            }
            if (special != null)
            {
                Cleanup(() => special.enabled = false, "隐藏特殊立绘");
                Cleanup(() => special.gameObject.SetActive(false), "停用特殊立绘承载");
                // 专属承载销毁后，原生autoDestroy检测释放模型/RenderTexture并停止计时器。
                Cleanup(() => ReleaseViewObject(special.gameObject, "特殊立绘承载控件"), "特殊立绘承载控件");
            }
            if (renderer != null) Cleanup(renderer.Destroy, "立绘渲染资源");
            if (model != null) Cleanup(() => UnityEngine.Object.Destroy(model), "立绘模型");
        }

        private void FitPortraitCamera()
        {
            // 使用创建后的世界边界，包含模型原点偏移；不再猜测模型缩放系数。
            var renderers = ownedPortraitRoot.GetComponentsInChildren<Renderer>(true);
            var found = false;
            var bounds = new Bounds();
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var current = renderer.bounds;
                if (current.size.x <= .001f || current.size.y <= .001f) continue;
                if (!found) { bounds = current; found = true; }
                else bounds.Encapsulate(current);
            }
            var camera = portraitRenderer.camera;
            if (!found || camera == null) throw new InvalidOperationException("立绘边界或相机未就绪");
            var aspect = portraitImage.rectTransform.rect.width / portraitImage.rectTransform.rect.height;
            camera.orthographic = true;
            camera.aspect = aspect;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) * 1.28f;
            var position = camera.transform.position;
            // 降低画面中的人物：取景中心上移，给头部留出空间，不改变模型本体。
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y + bounds.size.y * .12f, position.z);
        }

        private static float AttributeGroupTop(float viewportHeight, bool wide)
        {
            return wide ? Mathf.Max(154, viewportHeight - 342 - 84) : 154;
        }

        private void BuildAttributeOverview()
        {
            var viewportHeight = contentHeight - 64;
            var flowWidth = contentWidth - 20;
            var wide = flowWidth >= 1180;
            var firstHeight = wide ? 342f : 506f;
            // 补充区之后不再额外留12单位，使正文底边与左侧对象栏一致。
            var groupTop = AttributeGroupTop(viewportHeight, wide);
            var fateHeight = groupTop - 18;
            var extraTop = wide ? groupTop + firstHeight + 12 : groupTop + firstHeight + 18 + 332 + 12;
            var extraHeight = wide ? 72f : 126f;
            var flowHeight = extraTop + extraHeight;
            var viewport = Box("属性视口", content, 0, 64, flowWidth, viewportHeight);
            // 透明接收层使滚轮命中内容区；RectMask2D限制长页绘制。
            Fill(viewport, "滚动命中区", 0, 0, flowWidth, viewportHeight, new Color(1, 1, 1, .001f), true);
            viewport.gameObject.AddComponent<RectMask2D>();
            // 可滚动内容允许高于视口，内部控件仍由Box检查自身边界。
            var flow = NewRect("属性长页", viewport);
            flow.anchorMin = flow.anchorMax = new Vector2(0, 1);
            flow.pivot = new Vector2(0, 1);
            flow.anchoredPosition = Vector2.zero;
            flow.sizeDelta = new Vector2(flowWidth, flowHeight);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = flow;
            scroll.horizontal = false;
            scroll.vertical = flowHeight > viewportHeight + 1;
            scroll.inertia = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            scroll.onValueChanged.AddListener((UnityAction<Vector2>)(new Action<Vector2>(_ => { CloseDropdown(); HideFateTips(); })));
            if (scroll.vertical)
            {
                var track = Fill(content, "滚动轨", contentWidth - 10, 64, 7, viewportHeight, new Color(.30f, .30f, .24f, .12f), true);
                var handle = ScrollHandle(track, "滚动柄", 40, new Color(.36f, .34f, .27f, .65f));
                var scrollbar = track.gameObject.AddComponent<Scrollbar>();
                scrollbar.handleRect = handle.rectTransform;
                scrollbar.targetGraphic = handle;
                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scroll.verticalScrollbar = scrollbar;
            }

            string[] fates = { "先天气运", "后天气运", "逆天改命", "所有气运" };
            var fateWidth = (flowWidth - 16 - 36) / 5;
            for (int i = 0; i < fates.Length; i++)
            {
                var panelWidth = i == 3 ? fateWidth * 2 : fateWidth;
                var panel = Card(flow, fates[i], 8 + i * (fateWidth + 12), 0, panelWidth, fateHeight);
                Border(panel, panelWidth, fateHeight);
                var heading = Fill(panel, "气运题栏_" + i, 8, 8, panelWidth - 16, 28, new Color(.91f, .90f, .85f, .88f)).rectTransform;
                if (i < 3) Border(heading, panelWidth - 16, 28);
                if (i < 3) Label(heading, fates[i], 4, 0, panelWidth - 24, 28, 17, Ink, TextAnchor.MiddleCenter);
                if (i == 3)
                {
                    BuildFateSearch(heading);
                }
                var bodyWidth = panelWidth - 16;
                var bodyHeight = fateHeight - 48;
                var body = Fill(panel, "气运列表底_" + i, 8, 40, bodyWidth, bodyHeight, new Color(.94f, .93f, .89f, .45f), true).rectTransform;
                Border(body, bodyWidth, bodyHeight);
                var fateViewport = Box("气运条目视口_" + i, body, 6, 6, bodyWidth - 12, bodyHeight - 12);
                Fill(fateViewport, "气运滚动命中区", 0, 0, bodyWidth - 12, bodyHeight - 12, new Color(1, 1, 1, .001f), true);
                fateViewport.gameObject.AddComponent<RectMask2D>();
                var page = NewRect("气运条目长页_" + i, fateViewport);
                page.anchorMin = page.anchorMax = new Vector2(0, 1); page.pivot = new Vector2(0, 1);
                page.sizeDelta = new Vector2(bodyWidth - 22, bodyHeight - 12);
                var column = fateViewport.gameObject.AddComponent<ScrollRect>();
                column.viewport = fateViewport; column.content = page;
                column.horizontal = false; column.vertical = true; column.inertia = false;
                column.movementType = ScrollRect.MovementType.Clamped; column.scrollSensitivity = 32;
                var track = Fill(fateViewport, "气运滚动轨_" + i, bodyWidth - 18, 0, 6, bodyHeight - 12, new Color(.35f, .34f, .27f, .12f), true);
                var handle = ScrollHandle(track, "气运滚动柄", 28, new Color(.36f, .34f, .27f, .6f));
                var scrollbar = track.gameObject.AddComponent<Scrollbar>();
                scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle;
                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                column.verticalScrollbar = scrollbar;
                column.onValueChanged.AddListener((UnityAction<Vector2>)(Action<Vector2>)(_ => HideFateTips()));
                fateColumns.Add(column);
            }

            if (wide)
            {
                var frame = Fill(flow, "属性总框", 0, groupTop, flowWidth, firstHeight, new Color(.92f, .91f, .87f, .35f)).rectTransform;
                Border(frame, flowWidth, firstHeight);
                var usable = flowWidth - 56;
                float[] weights = { .26f, .26f, .16f, .16f, .16f };
                var x = 8f;
                for (int group = 0; group < 5; group++)
                {
                    var panelWidth = usable * weights[group];
                    BuildAttributeGroup(frame, group, x, 8, panelWidth, firstHeight - 16, group < 2 ? 2 : 1);
                    x += panelWidth + 10;
                }
            }
            else
            {
                var pairWidth = (flowWidth - 12) / 2;
                BuildAttributeGroup(flow, 0, 0, groupTop, pairWidth, firstHeight, 1);
                BuildAttributeGroup(flow, 1, pairWidth + 12, groupTop, pairWidth, firstHeight, 1);
                var thirdWidth = (flowWidth - 24) / 3;
                for (int group = 2; group < 5; group++)
                    BuildAttributeGroup(flow, group, (group - 2) * (thirdWidth + 12), groupTop + firstHeight + 18, thirdWidth, 332, 1);
            }
            BuildAttributeGroup(flow, 5, 0, extraTop, flowWidth, extraHeight, wide ? 4 : 2);
        }

        private void GroupHeading(RectTransform panel, string title, float width)
        {
            var titleWidth = Mathf.Min(176, width - 24);
            var heading = Box("分组题签", panel, (width - titleWidth) / 2, 12, titleWidth, 34);
            if (skin != null) Texture(heading, "题签墨迹", 0, 0, titleWidth, 34, skin.Brush, new Rect(0, 0, 1, 1));
            else Fill(heading, "墨底", 0, 0, titleWidth, 34, Ink);
            Label(heading, title, 0, 0, titleWidth, 34, 19, new Color(.95f, .94f, .90f, 1), TextAnchor.MiddleCenter);
        }

        private void BuildAttributeGroup(RectTransform parent, int group, float x, float y, float width, float height, int columns)
        {
            var panel = Fill(parent, CharacterSnapshot.GroupNames[group], x, y, width, height, new Color(.91f, .90f, .86f, .52f)).rectTransform;
            Border(panel, width, height);
            if (group != 5) GroupHeading(panel, CharacterSnapshot.GroupNames[group], width);
            var labels = CharacterSnapshot.Labels[group];
            var fields = new Text[labels.Length];
            attributeTexts.Add(fields);
            var cellWidth = (width - 20 - (columns - 1) * 10) / columns;
            var rows = group < 2 && columns == 2 ? 7 : (labels.Length + columns - 1) / columns;
            var startTop = group == 5 ? 30f : 54f;
            var rowHeight = group == 5 ? 34f : Mathf.Min(38, (height - 64) / rows);
            if (group == 5) Label(panel, "补充属性", 10, 2, width - 20, 24, 15, Muted);
            int[] order = null;
            if (columns == 2 && group == 0) order = new[] { 0, 4, 1, 5, 2, 6, 3, 7, 9, 8, 10, 11 };
            if (columns == 2 && group == 1) order = new[] { 0, 5, 1, 6, 2, 7, 3, 8, 4, 9, 10, 11 };
            for (int i = 0; i < labels.Length; i++)
            {
                var index = order == null ? i : order[i];
                var left = 10 + (i % columns) * (cellWidth + 10);
                var top = startTop + (i / columns) * rowHeight;
                var fullRow = group < 2 && i >= 10;
                if (fullRow) { left = 10; top = startTop + (columns == 2 ? 5 + i - 10 : i) * rowHeight; }
                // 最后一个单项铺满整行，避免留下半个空格；数据索引保持不变。
                var fieldWidth = fullRow ? width - 20 : cellWidth;
                var fieldHeight = rowHeight - 6;
                var cell = Fill(panel, "属性格_" + labels[index], left, top, fieldWidth, fieldHeight, new Color(.40f, .41f, .37f, .48f)).rectTransform;
                Fill(cell, "格内页", 1, 1, fieldWidth - 2, fieldHeight - 2, new Color(.90f, .90f, .87f, .96f));
                var labelWidth = group == 1 ? 78f : group == 0 ? 54f : 66f;
                var caption = Label(cell, labels[index], 8, 0, labelWidth, fieldHeight, 16, Ink);
                caption.horizontalOverflow = HorizontalWrapMode.Overflow;
                caption.resizeTextForBestFit = true;
                caption.resizeTextMinSize = 12;
                caption.resizeTextMaxSize = 16;
                fields[index] = Label(cell, "—", labelWidth + 10, 0, fieldWidth - labelWidth - 18, fieldHeight, 17, Ink, TextAnchor.MiddleRight);
                fields[index].horizontalOverflow = HorizontalWrapMode.Overflow;
                fields[index].resizeTextForBestFit = true;
                fields[index].resizeTextMinSize = 12;
                fields[index].resizeTextMaxSize = 17;
                BuildFieldEditors(group, index, fields[index]);
            }
            if (group >= 2 && group <= 4)
            {
                var buttonTop = 54 + 6 * rowHeight;
                BuildActions(panel, group, width, buttonTop);
            }
        }

        public void RefreshCharacter(bool force = false)
        {
            if (root == null || suspended || preparingPages) return;
            TickAppearance();
            TickItemTipWheel();
            TickEditing();
            if (root == null) return;
            TickRelations();
            if (fateTipCloseAt > 0 && Time.unscaledTime >= fateTipCloseAt) HideFateTips();
            if (root == null || characterName == null || selectedIndex < 0) return;
            var now = Time.unscaledTime;
            if (!force && now < nextCharacterRefresh) return;
            nextCharacterRefresh = now + 1f;
            if (!pages[selectedIndex].UsesTarget)
            {
                if (CharacterReader.ContextKey() != contextKey)
                { if (requestClose != null) requestClose(); else Dispose(); return; }
                if (pages[selectedIndex].Implemented)
                {
                    CheckCatalogSources();
                    using (WorkshopPerformance.Measure("数据刷新_" + Modules[selectedIndex])) pages[selectedIndex].Refresh(force);
                }
                return;
            }
            using (WorkshopPerformance.Measure("数据刷新_" + Modules[selectedIndex])) RefreshData(force);
        }

        private void RefreshData(bool force)
        {
            CheckCatalogSources();
            var currentContext = CharacterReader.ContextKey();
            if (currentContext != contextKey)
            {
                FinishInlineEdit(true); CloseDropdown(); edits.Sync(currentContext);
                CloseFateLibrary(); CloseItemDialog(); CloseItemLibrary(); CloseMartialOverlay();
                pickedFates.Clear(); fateCatalog.Clear(); catalogContext = null;
                itemPicks.Clear(); itemCatalog.Clear(); itemCatalogContext = null;
                if (itemSearch != null) { itemSearch.SetTextWithoutNotify(""); FilterItems(false); }
                if (fateSearch != null) fateSearch.SetTextWithoutNotify("");
                // 换世界时不静默改为玩家；关闭窗口，入口下一次重新指定目标。
                if (requestClose != null) requestClose(); else Dispose();
                return;
            }
            RenderRosterRows();
            CharacterSnapshot snapshot;
            string reason;
            var ready = selectedIndex == 0 ? CharacterReader.TryRead(selectedUnitId, out snapshot, out reason)
                : CharacterReader.TryReadSummary(selectedUnitId, out snapshot, out reason);
            if (!ready)
            {
                characterName.text = reason;
                characterRealm.text = "—";
                ReleasePortrait();
                if (selectedIndex == 0) foreach (var group in attributeTexts) foreach (var value in group) value.text = "—";
                FinishInlineEdit(true); CloseDropdown(); RefreshEditors();
                UpdateFates(new CharacterFate[0], reason);
                if (selectedIndex == 1) RefreshBag(true);
                if (selectedIndex == 2) RefreshMartials(true);
                if (selectedIndex == 3) RefreshRelations(true);
                var failure = CharacterReader.LastFailure;
                if (failure != null && failure != lastReadFailure)
                    Debug.LogWarning("[八荒秘录] 角色读取失败：" + failure);
                lastReadFailure = failure;
                return;
            }
            lastReadFailure = null;
            characterName.text = snapshot.Name;
            characterRealm.text = snapshot.Realm;
            if (selectedUnitId == null) selectedUnitId = CharacterReader.Resolve(null).data.unitData.unitID;
            RefreshEditors();
            RecordOpeningStage("人物快照/编辑绑定");
            using (WorkshopPerformance.Measure("共享人物立绘")) UpdatePortrait();
            RecordOpeningStage("人物立绘");
            if (selectedIndex != 0)
            {
                pages[selectedIndex].Refresh(force);
                return;
            }
            for (int group = 0; group < attributeTexts.Count; group++)
                for (int i = 0; i < attributeTexts[group].Length; i++) attributeTexts[group][i].text = snapshot.Groups[group][i];
            CharacterFate[] entries;
            string fateError;
            using (WorkshopPerformance.Measure("拥有气运读取")) CharacterFates.TryRead(selectedUnitId, out entries, out fateError);
            UpdateFates(entries, fateError);
            var fateFailure = CharacterFates.LastFailure;
            if (fateFailure != null && fateFailure != lastFateFailure) Debug.LogWarning("[八荒秘录] 气运读取：" + fateFailure);
            lastFateFailure = fateFailure;
            RecordOpeningStage("气运读取/显示");
        }

        private List<CharacterFate> FilterFates(int filter)
        {
            var result = new List<CharacterFate>();
            foreach (var entry in fates) if (filter == 3 || entry.Category == filter) result.Add(entry);
            return result;
        }

        private void UpdateFates(CharacterFate[] entries, string reason)
        {
            var owner = contextKey + ":" + selectedUnitId + ":" + (characterName == null ? "" : characterName.text);
            var changed = fateUnitId != owner || fateReason != reason || fates.Length != entries.Length;
            if (!changed)
                for (int i = 0; i < entries.Length; i++)
                    if (fates[i].Id != entries[i].Id || fates[i].Category != entries[i].Category ||
                        fates[i].Name != entries[i].Name || fates[i].Description != entries[i].Description) { changed = true; break; }
            var ownerChanged = fateUnitId != owner;
            fateUnitId = owner;
            fates = entries;
            fateReason = reason;
            if (changed) RenderFateColumns(ownerChanged);
        }

        private void RenderFateColumns(bool resetScroll)
        {
            HideFateTips();
            for (int i = 0; i < fateColumns.Count; i++)
            {
                if (i == 3) { FilterCandidates(); continue; }
                var column = fateColumns[i];
                var page = column.content;
                var position = resetScroll ? 1f : column.verticalNormalizedPosition;
                var items = FilterFates(i);
                var rowPitch = Mathf.Min(36, Mathf.Max(24, column.viewport.sizeDelta.y / 3));
                var rowHeight = rowPitch - 4;
                var pageHeight = Mathf.Max(column.viewport.sizeDelta.y, (items.Count + (items.Count > 0 && fateReason != null ? 1 : 0)) * rowPitch);
                page.sizeDelta = new Vector2(page.sizeDelta.x, pageHeight);
                var pool = ownedFateRows[i];
                // Reuse bindings without retaining a growing historical list after large shrinkage.
                var retained = Math.Max(items.Count, (int)Math.Ceiling(column.viewport.sizeDelta.y / rowPitch) + 1);
                while (pool.Count > retained) { ReleaseViewObject(pool[pool.Count - 1].Root.gameObject, "多余气运行"); pool.RemoveAt(pool.Count - 1); }
                while (pool.Count < items.Count) pool.Add(CreateOwnedFateRow(i, page, page.sizeDelta.x, rowHeight, pool.Count));
                if (fateEmptyLabels[i] == null) fateEmptyLabels[i] = Label(page, "", 4, 4, page.sizeDelta.x - 8, 28, 15, Muted, TextAnchor.MiddleCenter);
                var empty = fateEmptyLabels[i]; empty.gameObject.SetActive(items.Count == 0 || fateReason != null);
                empty.text = items.Count == 0 ? fateReason ?? "无" : fateReason ?? "";
                empty.rectTransform.anchoredPosition = new Vector2(4, items.Count == 0 ? -4 : -items.Count * rowPitch);
                empty.color = items.Count == 0 ? Muted : Cinnabar;
                for (int n = 0; n < pool.Count; n++)
                {
                    var row = pool[n]; row.Entry = n < items.Count ? items[n] : null;
                    row.Root.gameObject.SetActive(row.Entry != null); if (row.Entry == null) continue;
                    row.Root.anchoredPosition = new Vector2(0, -n * rowPitch);
                    row.Name.text = row.Entry.Name;
                    StyleFateRow(row.Background, row.Name, row.Entry, page.sizeDelta.x, rowHeight, 32, row.Rank);
                    row.Delete.color = FateInk(row.Entry.Level);
                }
                column.verticalScrollbar.gameObject.SetActive(pageHeight > column.viewport.sizeDelta.y + .1f);
                column.verticalNormalizedPosition = position;
            }
        }

        private void HideFateTips()
        {
            var previous = fateTip;
            if (itemTipSourceScroll != null) Cleanup(() => itemTipSourceScroll.scrollSensitivity = itemBagWheelSensitivity, "提示滚轮状态");
            itemTipScroll = itemTipSourceScroll = null;
            fateTip = fateTipOwner = null;
            fateTipCloseAt = 0;
            if (previous == null) return;
            ReleaseViewObject(previous.gameObject, "介绍浮层");
        }

        private static void BindFateHover(GameObject target, Action enter, Action exit)
        {
            var listener = target.AddComponent<UIEventListener>();
            listener.onMouseEnter = new UnityEvent();
            listener.onMouseExit = new UnityEvent();
            listener.onMouseEnter.AddListener((UnityAction)enter);
            listener.onMouseExit.AddListener((UnityAction)exit);
        }

        internal static Rect FateTipBounds(Rect row, float width, float height, Vector2 area)
        {
            const float margin = 8, gap = 8;
            var right = row.x + row.width + gap;
            var left = row.x - gap - width;
            var fitsRight = right + width <= area.x - margin;
            var fitsLeft = left >= margin;
            var x = fitsRight ? right : left;
            var y = row.y;
            if (!fitsRight && !fitsLeft)
            {
                x = row.x;
                var below = row.y + row.height + gap;
                y = below + height <= area.y - margin ? below : row.y - height - gap;
            }
            x = Mathf.Max(margin, Mathf.Min(x, area.x - width - margin));
            y = Mathf.Max(margin, Mathf.Min(y, area.y - height - margin));
            return new Rect(x, y, width, height);
        }

        internal static Rect ActionTipBounds(Rect row, float width, float height, Vector2 area)
        {
            const float margin = 8, gap = 6;
            var below = row.y + row.height + gap;
            var y = below + height <= area.y - margin ? below : row.y - height - gap;
            return new Rect(Mathf.Max(margin, Mathf.Min(row.x, area.x - width - margin)),
                Mathf.Max(margin, Mathf.Min(y, area.y - height - margin)), width, height);
        }

        internal static Rect FateRowBounds(RectTransform row, RectTransform area)
        {
            // 对象栏与正文是同一书页下的兄弟节点，额外换算兄弟栏按钮。
            if (row != area && !row.IsChildOf(area))
            {
                if (area.parent == null) throw new InvalidOperationException("提示条目已离开书页层级");
                var relative = FateRowBounds(row, area.parent.GetComponent<RectTransform>());
                return new Rect(relative.x - area.anchoredPosition.x, relative.y + area.anchoredPosition.y, relative.width, relative.height);
            }
            // 本视图所有祖先均为左上锚点/左上pivot、固定尺寸且单位localScale。
            // 累加实际anchoredPosition，包含两个ScrollRect长页的实时滚动偏移。
            // 不依赖原生CalculateRelativeRectTransformBounds的跨IL2CPP返回值。
            float x = 0, y = 0;
            var node = row;
            while (node != area)
            {
                if (node == null || node.parent == null) throw new InvalidOperationException("气运条目已离开正文层级");
                x += node.anchoredPosition.x;
                y += node.anchoredPosition.y;
                // parent在IL2CPP中可能只是Transform包装，显式取RectTransform组件。
                node = node.parent.GetComponent<RectTransform>();
            }
            return new Rect(x, -y, row.sizeDelta.x, row.sizeDelta.y);
        }

        private void ShowFateTip(RectTransform row, CharacterFate entry, RectTransform area = null, bool belowButton = false, bool itemTip = false, ScrollRect itemSource = null)
        {
            HideFateTips();
            if (row == null || !row.gameObject.activeInHierarchy || selectedIndex != (itemTip ? 1 : 0)) return;
            fateTipOwner = row;
            area = area ?? content;
            var width = Mathf.Min(280, area.sizeDelta.x - 20);
            var maxHeight = Mathf.Min(itemTip ? 400 : 300, area.sizeDelta.y - 20);
            // 原生tips可能包含字号标签；小提示统一字号，保留介绍内容与换行。
            var description = Regex.Replace(entry.Description.Replace("<br>", "\n").Replace("\\n", "\n"), "<[^>]+>", "");
            if (description.StartsWith(entry.Name + "\n", StringComparison.Ordinal)) description = description.Substring(entry.Name.Length + 1);
            fateTip = Fill(area, itemTip ? "物品介绍" : "气运介绍", 8, 8, width, maxHeight, new Color(.96f, .95f, .90f, 1), true).rectTransform;
            var title = Label(fateTip, entry.Name, 12, 8, width - 24, 24, 17, Cinnabar);
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 12; title.resizeTextMaxSize = 17;
            var viewport = Box("气运介绍视口", fateTip, 12, 40, width - 24, maxHeight - 52);
            Fill(viewport, "介绍滚动命中区", 0, 0, width - 24, maxHeight - 52, new Color(1, 1, 1, .001f), true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var page = NewRect("气运介绍长页", viewport);
            page.anchorMin = page.anchorMax = new Vector2(0, 1); page.pivot = new Vector2(0, 1);
            page.sizeDelta = new Vector2(width - 34, maxHeight - 52);
            var text = Label(page, description, 0, 0, width - 34, maxHeight - 52, 14, Ink, TextAnchor.UpperLeft);
            var textHeight = Mathf.Max(20, text.preferredHeight + 4);
            var height = Mathf.Min(maxHeight, Mathf.Max(72, textHeight + 52));
            fateTip.sizeDelta = new Vector2(width, height);
            viewport.sizeDelta = new Vector2(width - 24, height - 52);
            viewport.GetChild(0).GetComponent<RectTransform>().sizeDelta = viewport.sizeDelta;
            page.sizeDelta = new Vector2(width - 34, Mathf.Max(textHeight, height - 52));
            text.rectTransform.sizeDelta = page.sizeDelta;
            Border(fateTip, width, height);
            Fill(fateTip, "介绍分隔线", 12, 35, width - 24, 1, new Color(.55f, .48f, .32f, .3f));
            var rowBounds = FateRowBounds(row, area);
            var position = belowButton ? ActionTipBounds(rowBounds, width, height, area.sizeDelta) : FateTipBounds(rowBounds, width, height, area.sizeDelta);
            fateTip.anchoredPosition = new Vector2(position.x, -position.y);
            if (textHeight > height - 52)
            {
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport; scroll.content = page; scroll.horizontal = false; scroll.vertical = true;
                scroll.inertia = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
                var track = Fill(viewport, "介绍滚动轨", width - 30, 0, 6, height - 52, new Color(.35f, .34f, .27f, .12f), true);
                var handle = ScrollHandle(track, "介绍滚动柄", 18, Muted);
                var scrollbar = track.gameObject.AddComponent<Scrollbar>();
                scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle; scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scroll.verticalScrollbar = scrollbar;
                if (itemTip && (itemSource ?? itemBagScroll) != null)
                {
                    itemTipScroll = scroll;
                    itemTipSourceScroll = itemSource ?? itemBagScroll;
                    itemBagWheelSensitivity = itemTipSourceScroll.scrollSensitivity;
                    itemTipSourceScroll.scrollSensitivity = 0;
                }
            }
            if (itemTip)
            {
                // 不接收指针，移出图标即关闭；滚轮仍从图标所在位置读取。
                fateTip.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
                return;
            }
            var tipPanel = fateTip;
            BindFateHover(tipPanel.gameObject, () => { if (fateTip == tipPanel) fateTipCloseAt = 0; },
                () => { if (fateTip == tipPanel) fateTipCloseAt = Time.unscaledTime + .12f; });
        }
        private Image ScrollHandle(Image track, string name, float minimum, Color color)
        {
            // Unity驱动锚点，但保留sizeDelta。两端各留半个最小柄高，使全程都在轨内。
            var size = track.rectTransform.sizeDelta;
            minimum = Mathf.Min(minimum, size.y / 2);
            var sliding = Box(name + "滑动范围", track.rectTransform, 0, minimum / 2, size.x, size.y - minimum);
            var handle = NewRect(name, sliding);
            handle.pivot = new Vector2(.5f, .5f); handle.sizeDelta = new Vector2(0, minimum);
            handle.anchoredPosition = Vector2.zero;
            var image = handle.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = true;
            return image;
        }
        private RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject("BHML_" + name);
            WorkshopPerformance.RecordCreated(go);
            try
            {
                go.layer = parent.gameObject.layer;
                var rect = go.AddComponent<RectTransform>();
                rect.SetParent(parent, false);
                return rect;
            }
            catch
            {
                ReleaseViewObject(go, "未挂载控件");
                throw;
            }
        }

        // 所有内容统一采用左上角坐标，尺寸与安全区同源；越界直接触发原生回退。
        private RectTransform Box(string name, RectTransform parent, float left, float top, float width, float height)
        {
            var size = parent.sizeDelta;
            if (left < 0 || top < 0 || width <= 0 || height <= 0 || left + width > size.x + .1f || top + height > size.y + .1f)
                throw new InvalidOperationException("布局越界：" + name);
            var rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private Image Fill(RectTransform parent, string name, float left, float top, float width, float height, Color color, bool raycast = false)
        {
            var image = Box(name, parent, left, top, width, height).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private RectTransform Card(RectTransform parent, string name, float left, float top, float width, float height)
        {
            var border = Fill(parent, name, left, top, width, height, new Color(.36f, .36f, .31f, .18f)).rectTransform;
            Fill(border, "内页", 1, 1, width - 2, height - 2, new Color(.92f, .91f, .88f, .48f));
            return border;
        }

        private Text Label(RectTransform parent, string value, float left, float top, float width, float height, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var text = Box("文字", parent, left, top, width, height).gameObject.AddComponent<Text>();
            text.font = calligraphyFont != null ? calligraphyFont : font;
            text.fontSize = size;
            text.color = color;
            text.text = value;
            text.alignment = align;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private RawImage Texture(RectTransform parent, string name, float left, float top, float width, float height, Texture2D texture, Rect uv)
        {
            var image = Box(name, parent, left, top, width, height).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uv;
            image.raycastTarget = false;
            return image;
        }

        private void MakeCloseButton(RectTransform parent, float left, float top, Action action)
        {
            // 自绘小圆底，不依赖游戏版本未知的Sprite；纹理由本视图释放。
            const int size = 64;
            closeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var dx = x - 31.5; var dy = y - 31.5;
                var radius = Math.Sqrt(dx * dx + dy * dy);
                var alpha = (float)Math.Max(0, Math.Min(1, 31 - radius));
                var edge = radius > 29 ? new Color(.12f, .13f, .12f, alpha) :
                    radius > 26 ? new Color(.61f, .55f, .36f, alpha) : new Color(.18f, .20f, .18f, alpha);
                pixels[y * size + x] = edge;
            }
            closeTexture.SetPixels(pixels);
            closeTexture.Apply();
            var image = Texture(parent, "关闭", left, top, 38, 38, closeTexture, new Rect(0, 0, 1, 1));
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = colors.selectedColor = Color.white;
            colors.highlightedColor = new Color(1f, .96f, .80f, 1f);
            colors.pressedColor = new Color(.78f, .75f, .64f, 1f);
            button.colors = colors;
            button.onClick.AddListener((UnityAction)action);
            Label(image.rectTransform, "×", 0, 0, 38, 38, 30, new Color(.88f, .76f, .43f, 1f), TextAnchor.MiddleCenter);
        }

        private Button MakeButton(RectTransform parent, string name, float left, float top, float width, float height, string text, Action action, bool framed = true)
        {
            var image = Fill(parent, name, left, top, width, height, new Color(.84f, .82f, .74f, 1f), true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.93f, .92f, .84f, 1f);
            colors.pressedColor = new Color(.77f, .78f, .69f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener((UnityAction)action);
            if (framed) Border(image.rectTransform, width, height);
            Label(image.rectTransform, text, 0, 0, width, height, 18, Ink, TextAnchor.MiddleCenter);
            return button;
        }

        private void Border(RectTransform parent, float width, float height, bool transparentInner = false)
        {
            var edge = new Color(.43f, .39f, .28f, .62f);
            Fill(parent, "上边", 0, 0, width, 1, edge);
            Fill(parent, "下边", 0, height - 1, width, 1, edge);
            Fill(parent, "左边", 0, 0, 1, height, edge);
            Fill(parent, "右边", width - 1, 0, 1, height, edge);
            var trim = new Color(.55f, .48f, .32f, transparentInner ? 0 : .30f);
            Fill(parent, "上内线", 3, 3, width - 6, 1, trim);
            Fill(parent, "下内线", 3, height - 4, width - 6, 1, trim);
        }

        public void Dispose()
        {
            appearing = false;
            appearance = null;
            openingTimer = null;
            openingStages = null;
            Cleanup(ClearEditingView, "编辑视图");
            Cleanup(ReleasePortrait, "立绘资源");
            Cleanup(HideFateTips, "关闭气运提示");
            foreach (var pool in ownedFateRows) pool.Clear(); Array.Clear(fateEmptyLabels, 0, fateEmptyLabels.Length);
            foreach (var page in pages) page.Dispose();
            pages.Clear(); contentHost = null; suspended = false;
            var host = sleepingHost; sleepingHost = null;
            var previousRoot = root; root = null;
            if (previousRoot != null)
            {
                Cleanup(() => previousRoot.SetActive(false), "隐藏视图");
                Cleanup(() => ReleaseViewObject(previousRoot, "视图对象"), "视图对象");
            }
            if (host != null) Cleanup(() => ReleaseViewObject(host, "休眠承载"), "休眠承载");
            var previousCloseTexture = closeTexture; closeTexture = null;
            if (previousCloseTexture != null) Cleanup(() => UnityEngine.Object.Destroy(previousCloseTexture), "关闭按钮纹理");
            // 只解除视图引用；固定美术纹理由ModMain.Destroy统一释放。
            skin = null;
            var previousFont = calligraphyFont; calligraphyFont = null;
            if (previousFont != null) Cleanup(() => UnityEngine.Object.Destroy(previousFont), "自建字体");
            RestoreNativeControls();
            tabLabels.Clear(); tabFrames.Clear();
            content = null; font = null; interactionPending = false;
            characterName = null; characterRealm = null; attributeTexts.Clear();
            fateColumns.Clear(); fates = new CharacterFate[0];
            roster.Clear(); filteredRoster.Clear(); rosterRows.Clear(); rowIds.Clear();
            targetPanel = null; rosterContent = null; rosterScroll = null; targetSearch = null; searchQuery = "";
            portraitImage = null; portraitStatus = null; rosterCount = null;
            selectedUnitId = null; contextKey = null;
            lastReadFailure = null; nextCharacterRefresh = 0;
            selectedIndex = -1;
        }

        private static void ReleaseViewObject(GameObject previous, string part)
        {
            if (previous == null) return;
            WorkshopPerformance.RecordDestroy(previous);
            // 隐藏触发原生回调可能抛错，销毁仍须独立尝试。
            Cleanup(() => previous.SetActive(false), "隐藏" + part);
            Cleanup(() => UnityEngine.Object.Destroy(previous), "销毁" + part);
        }

        private static void Cleanup(Action action, string part)
        {
            try { action(); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 清理" + part + "失败：" + ex); }
        }
    }
}
