using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MOD_YtxSQ1.UI
{
    internal sealed partial class BookView
    {
        // 这一阶段只演示界面，不把示例绑定到人物记录，也不调用游戏写入接口。
        private sealed class MartialPreview
        {
            internal string Name, Type, School, Realm, Quality, Summary;
            internal bool Equipped;
            internal string[] Prefixes;
        }
        private readonly MartialPreview[] martialExamples =
        {
            new MartialPreview { Name = "月影剑", Type = "武技 / 灵技", School = "剑", Realm = "元婴", Quality = "红", Equipped = true,
                Summary = "向前方发出剑气，攻击路径上的敌人。\n搭配重点：攻击频率、剑气数量与流血效果。",
                Prefixes = new[] { "命中时，有一定概率使敌人流血", "减少此功法的冷却时间", "增加剑气的数量", "提高此功法的伤害", "增加流血的持续时间", "提高剑气的飞行速度" } },
            new MartialPreview { Name = "疾风剑", Type = "绝技", School = "剑", Realm = "元婴", Quality = "橙", Equipped = true,
                Summary = "施展剑系绝技，造成伤害。\n搭配重点：与武技产生的状态相配合。",
                Prefixes = new[] { "提高此功法的伤害", "减少此功法的冷却时间", "降低施展时的灵力消耗", "增加功法的作用范围" } },
            new MartialPreview { Name = "剑系身法", Type = "身法", School = "剑", Realm = "元婴", Quality = "红", Equipped = true,
                Summary = "施展身法，移动并调整战斗位置。\n搭配重点：持续时间、移动速度与冷却。",
                Prefixes = new[] { "延长身法的持续时间", "减少此功法的冷却时间", "提高身法期间的移动速度", "施展身法时获得额外护心" } },
            new MartialPreview { Name = "骤天剑", Type = "神通", School = "剑", Realm = "元婴", Quality = "红", Equipped = false,
                Summary = "剑系神通示例。\n神通需要查看具体施展条件；拥有或装备并不等于随时可以施放。",
                Prefixes = new[] { "提高此功法的伤害", "减少此功法的冷却时间", "增加功法的作用范围", "降低施展时的灵力消耗" } },
            new MartialPreview { Name = "剑系大法", Type = "心法", School = "剑", Realm = "元婴", Quality = "橙", Equipped = true,
                Summary = "心法提供常驻属性与功法加成。\n详情将展示所属槽位、道点占用及装备要求。",
                Prefixes = new[] { "减少武技的冷却时间", "减少身法的冷却时间", "提高剑系功法的伤害", "满足条件时获得额外战斗加成" } },
            new MartialPreview { Name = "烈火术", Type = "武技 / 灵技", School = "火", Realm = "金丹", Quality = "紫", Equipped = false,
                Summary = "火系灵技示例。\n可用于查看跨流派检索和不同品质的列表样式。",
                Prefixes = new[] { "提高此功法的伤害", "增加燃烧效果的持续时间", "降低施展时的灵力消耗" } },
            new MartialPreview { Name = "通用劲", Type = "心法", School = "通用", Realm = "金丹", Quality = "红", Equipped = false,
                Summary = "通用心法示例。\n后续按真实配置显示属性加成、资质要求和道点占用。",
                Prefixes = new[] { "提高攻击", "根据人物属性获得额外攻击", "满足战斗条件时提高伤害" } }
        };
        private static readonly string[] MartialTypes = { "全部类别", "武技 / 灵技", "绝技", "身法", "神通", "心法" };
        private static readonly string[] MartialSchools = { "全部流派", "剑", "火", "通用" };
        private static readonly string[] MartialRealms = { "全部境界", "金丹", "元婴" };
        private static readonly string[] MartialQualities = { "全部品质", "红", "橙", "紫" };
        private InputField martialSearch;
        private RectTransform martialListPanel, martialDetailPanel;
        private ScrollRect martialListScroll;
        private Text martialCount;
        private readonly List<Button> martialModeButtons = new List<Button>();
        private int martialMode, martialTypeFilter, martialSchoolFilter, martialRealmFilter, martialQualityFilter;
        private string martialQuery = "";
        private MartialPreview martialSelection;
        private bool martialCompact;

        private void BuildMartials()
        {
            martialCompact = contentWidth < 720;
            var height = contentHeight - 76;
            var listWidth = martialCompact ? contentWidth : Mathf.Min(352, Mathf.Max(280, contentWidth * .35f));
            martialListPanel = Card(content, "功法选取", 0, 76, listWidth, height);
            Border(martialListPanel, listWidth, height);
            var modeWidth = (listWidth - 28) / 2;
            for (int i = 0; i < 2; i++)
            {
                var mode = i;
                martialModeButtons.Add(MakeButton(martialListPanel, "功法来源" + i, 10 + i * (modeWidth + 8), 10,
                    modeWidth, 34, i == 0 ? "已学功法" : "秘籍库", () => SetMartialMode(mode)));
            }
            martialSearch = CreateSearchInput(martialListPanel, "功法检索", 10, 54, listWidth - 20, 32,
                "名称 / 招式", value => { martialQuery = value.Trim(); FilterMartialPreview(); });
            martialSearch.SetTextWithoutNotify(martialQuery);
            BuildMartialFilters(listWidth);
            martialCount = Label(martialListPanel, "", 12, 164, listWidth - 24, 22, 14, Muted);
            martialListScroll = ItemScroll(martialListPanel, "功法列表", 10, 192, listWidth - 20, Mathf.Max(70, height - 228));
            AddMartialScrollbar(martialListScroll);
            Label(martialListPanel, "示例内容 · 不代表人物实际功法", 12, height - 28, listWidth - 24, 20, 13, Muted);
            var detailWidth = martialCompact ? contentWidth : contentWidth - listWidth - 16;
            martialDetailPanel = Card(content, "功法详情", martialCompact ? 0 : listWidth + 16, 76, detailWidth, height);
            Border(martialDetailPanel, detailWidth, height);
            FilterMartialPreview();
        }
        private void BuildMartialFilters(float width)
        {
            var half = (width - 28) / 2;
            MartialFilter("类别", 10, 96, half, MartialTypes, () => martialTypeFilter, value => martialTypeFilter = value);
            MartialFilter("流派", 18 + half, 96, half, MartialSchools, () => martialSchoolFilter, value => martialSchoolFilter = value);
            MartialFilter("境界", 10, 130, half, MartialRealms, () => martialRealmFilter, value => martialRealmFilter = value);
            MartialFilter("品质", 18 + half, 130, half, MartialQualities, () => martialQualityFilter, value => martialQualityFilter = value);
        }
        private void MartialFilter(string name, float x, float y, float width, string[] choices, Func<int> read, Action<int> write)
        {
            Button button = null;
            button = MakeButton(martialListPanel, "功法筛选" + name, x, y, width, 28, choices[read()] + " ›", () =>
            {
                write((read() + 1) % choices.Length);
                button.GetComponentInChildren<Text>().text = choices[read()] + " ›";
                FilterMartialPreview();
            });
            button.GetComponentInChildren<Text>().fontSize = 15;
        }
        private void SetMartialMode(int mode)
        {
            martialMode = mode;
            FilterMartialPreview();
        }
        private bool MatchesMartialPreview(MartialPreview entry)
        {
            return (martialTypeFilter == 0 || entry.Type == MartialTypes[martialTypeFilter])
                && (martialSchoolFilter == 0 || entry.School == MartialSchools[martialSchoolFilter])
                && (martialRealmFilter == 0 || entry.Realm == MartialRealms[martialRealmFilter])
                && (martialQualityFilter == 0 || entry.Quality == MartialQualities[martialQualityFilter])
                && (martialQuery.Length == 0 || entry.Name.IndexOf(martialQuery, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        private void FilterMartialPreview()
        {
            if (martialListScroll == null) return;
            var entries = new List<MartialPreview>();
            foreach (var entry in martialExamples) if (MatchesMartialPreview(entry)) entries.Add(entry);
            if (!entries.Contains(martialSelection)) martialSelection = entries.Count > 0 ? entries[0] : null;
            for (int i = 0; i < martialModeButtons.Count; i++)
                martialModeButtons[i].GetComponent<Image>().color = i == martialMode ? new Color(.73f, .69f, .56f, 1) : new Color(.87f, .85f, .78f, 1);
            DestroyMartialChildren(martialListScroll.content, 0);
            var width = martialListScroll.content.sizeDelta.x - 12;
            martialListScroll.content.sizeDelta = new Vector2(martialListScroll.content.sizeDelta.x,
                Mathf.Max(martialListScroll.viewport.sizeDelta.y, entries.Count * 78));
            for (int i = 0; i < entries.Count; i++) BuildMartialPreviewRow(entries[i], i, width);
            martialListScroll.content.anchoredPosition = Vector2.zero;
            martialListScroll.verticalNormalizedPosition = 1;
            martialListScroll.verticalScrollbar.gameObject.SetActive(entries.Count * 78 > martialListScroll.viewport.sizeDelta.y);
            martialCount.text = (martialMode == 0 ? "已学功法" : "秘籍库") + " · " + entries.Count + " 项示例";
            RenderMartialDetail();
            if (martialCompact) { martialListPanel.gameObject.SetActive(true); martialDetailPanel.gameObject.SetActive(false); }
        }
        private void BuildMartialPreviewRow(MartialPreview entry, int index, float width)
        {
            var button = MakeButton(martialListScroll.content, "功法条目" + index, 0, index * 78, width, 70, "", () =>
            {
                martialSelection = entry;
                // 重绘选择色时保留列表位置；选择不重置滚动。
                foreach (var row in martialListScroll.content.GetComponentsInChildren<Button>(true))
                    row.GetComponent<Image>().color = row.gameObject.name == "BHML_功法条目" + index ? new Color(.78f, .74f, .62f, 1) : new Color(.88f, .86f, .80f, 1);
                RenderMartialDetail();
                if (martialCompact) { martialListPanel.gameObject.SetActive(false); martialDetailPanel.gameObject.SetActive(true); }
            });
            button.GetComponent<Image>().color = entry == martialSelection ? new Color(.78f, .74f, .62f, 1) : new Color(.88f, .86f, .80f, 1);
            var parent = button.GetComponent<RectTransform>();
            Fill(parent, "品质侧线", 0, 0, 3, 70, MartialQualityColor(entry.Quality));
            Label(parent, entry.School == "通用" ? "劲" : entry.School, 8, 14, 34, 36, 23, MartialQualityColor(entry.Quality), TextAnchor.MiddleCenter);
            Label(parent, entry.Name, 48, 7, width - 56, 27, 20, Ink);
            Label(parent, entry.Realm + " · " + entry.Quality + "品 · " + entry.Type, 48, 36, width - 56, 22, 13, Muted);
            if (martialMode == 0 && entry.Equipped)
                Label(parent, "装", width - 30, 8, 22, 22, 13, MartialQualityColor("橙"), TextAnchor.MiddleCenter);
        }
        private void RenderMartialDetail()
        {
            if (martialDetailPanel == null) return;
            // Card的内页和四条边保留，其余详情控件随选择释放。
            DestroyMartialChildren(martialDetailPanel, 5);
            var width = martialDetailPanel.sizeDelta.x;
            var height = martialDetailPanel.sizeDelta.y;
            if (martialCompact) MakeButton(martialDetailPanel, "返回功法列表", 12, 10, 100, 30, "‹ 返回列表", () =>
                { martialListPanel.gameObject.SetActive(true); martialDetailPanel.gameObject.SetActive(false); });
            var top = martialCompact ? 48 : 12;
            if (martialSelection == null)
            {
                Label(martialDetailPanel, "没有符合条件的示例功法", 16, top + 70, width - 32, 36, 21, Ink, TextAnchor.MiddleCenter);
                Label(martialDetailPanel, "调整筛选或清空搜索后再试", 16, top + 112, width - 32, 24, 16, Muted, TextAnchor.MiddleCenter);
                return;
            }
            var entry = martialSelection;
            Label(martialDetailPanel, entry.Name, 18, top, width - 110, 36, 27, Ink);
            Label(martialDetailPanel, entry.Quality + "品", width - 82, top + 3, 64, 28, 19, MartialQualityColor(entry.Quality), TextAnchor.MiddleCenter);
            Label(martialDetailPanel, entry.Realm + " · " + entry.School + "系 · " + entry.Type, 18, top + 42, width - 36, 24, 16, Muted);
            Label(martialDetailPanel, martialMode == 0 ? (entry.Equipped ? "示例状态：已装备 · 修炼未圆满" : "示例状态：未装备 · 修炼未圆满") : "秘籍预览 · 获取与学习尚未接入",
                18, top + 72, width - 36, 22, 14, Muted);
            Fill(martialDetailPanel, "功法详情题线", 16, top + 105, width - 32, 1, Gold);
            var scroll = ItemScroll(martialDetailPanel, "功法效果", 16, top + 116, width - 32, Mathf.Max(60, height - top - 248));
            AddMartialScrollbar(scroll);
            BuildMartialPreviewEffects(scroll, entry);
            BuildMartialPreviewActions(width, height);
        }
        private void BuildMartialPreviewEffects(ScrollRect scroll, MartialPreview entry)
        {
            var page = scroll.content;
            var width = page.sizeDelta.x - 12;
            page.sizeDelta = new Vector2(page.sizeDelta.x, Mathf.Max(scroll.viewport.sizeDelta.y, 116 + entry.Prefixes.Length * 62));
            Label(page, "功法效果", 0, 0, width, 26, 20, Ink);
            Label(page, entry.Summary, 0, 30, width, 44, 16, Ink);
            Label(page, "词条示例", 0, 80, width - 100, 28, 20, Ink);
            Label(page, "效果与数值待读取", width - 138, 84, 138, 22, 13, Muted, TextAnchor.MiddleRight);
            for (int i = 0; i < entry.Prefixes.Length; i++)
            {
                var row = Card(page, "功法词条" + i, 0, 116 + i * 62, width, 54);
                Fill(row, "词条品质", 0, 0, 3, 54, MartialQualityColor(i % 3 == 0 ? "橙" : entry.Quality));
                Label(row, (i + 1).ToString("00"), 8, 8, 28, 32, 15, Muted);
                Label(row, entry.Prefixes[i], 42, 4, width - 112, 46, 16, Ink);
                var edit = MakeButton(row, "修改词条" + i, width - 62, 12, 54, 30, "修改", () => { });
                edit.interactable = false;
                edit.GetComponentInChildren<Text>().color = Muted;
            }
            scroll.verticalScrollbar.gameObject.SetActive(page.sizeDelta.y > scroll.viewport.sizeDelta.y);
        }
        private void BuildMartialPreviewActions(float width, float height)
        {
            var names = martialMode == 0 ? new[] { "修炼圆满", "词条提质", "装备 / 卸下", "遗忘功法" } : new[] { "获取秘籍", "直接学习" };
            var columns = 2;
            var buttonWidth = (width - 44) / columns;
            for (int i = 0; i < names.Length; i++)
            {
                var button = MakeButton(martialDetailPanel, "功法操作" + i, 16 + i % columns * (buttonWidth + 12),
                    height - 116 + i / columns * 40, buttonWidth, 32, names[i], () => { });
                button.interactable = false;
                button.GetComponentInChildren<Text>().color = Muted;
            }
            Label(martialDetailPanel, "上述操作与词条修改待接入", 16, height - 30, width - 32, 20, 13, Muted, TextAnchor.MiddleCenter);
        }
        private void AddMartialScrollbar(ScrollRect scroll)
        {
            var track = Fill(scroll.viewport, "功法滚动轨", scroll.viewport.sizeDelta.x - 6, 0, 6,
                scroll.viewport.sizeDelta.y, new Color(.35f, .34f, .27f, .12f), true);
            var handle = ScrollHandle(track, "功法滚动柄", 24, Muted);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform; bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop; scroll.verticalScrollbar = bar;
        }
        private static Color MartialQualityColor(string quality)
        {
            return quality == "红" ? new Color(.59f, .24f, .20f, 1) : quality == "橙" ? new Color(.59f, .43f, .20f, 1) : new Color(.43f, .33f, .52f, 1);
        }
        private static void DestroyMartialChildren(RectTransform parent, int keep)
        {
            for (int i = parent.childCount - 1; i >= keep; i--)
            { var child = parent.GetChild(i).gameObject; child.SetActive(false); UnityEngine.Object.Destroy(child); }
        }
        private void ClearMartialsView()
        {
            if (martialSearch != null) WindowInputScope.Unregister(martialSearch);
            martialSearch = null; martialListPanel = null; martialDetailPanel = null;
            martialListScroll = null; martialCount = null; martialModeButtons.Clear();
        }
    }
}
