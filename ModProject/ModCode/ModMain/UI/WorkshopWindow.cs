using System;
using UnityEngine;

namespace MOD_YtxSQ1.UI
{
    // 沿用官方原生 UI 生命周期与字体；视觉和布局由独立视图统一构建。
    internal sealed class WorkshopWindow
    {
        private UITextInfoLong ownedWindow;
        private BookView view;

        public bool IsOpen
        {
            get { return ownedWindow != null && ownedWindow.gameObject.activeInHierarchy; }
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
                return;
            }
            Open(null);
        }

        public void Open(string unitId)
        {
            if (IsOpen) return;
            ReleaseView();
            ownedWindow = null;
            var openTimer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // 共享 UI 类型，不覆盖游戏或其他 MOD 已打开的说明窗口。
                if (g.ui == null) return;
                if (g.ui.GetUI(UIType.TextInfoLong) != null)
                {
                    UITipItem.AddTip("请先关闭当前说明窗口，再打开八荒秘录。");
                    return;
                }
                ownedWindow = g.ui.OpenUI<UITextInfoLong>(UIType.TextInfoLong);
                if (ownedWindow == null) throw new InvalidOperationException("游戏未返回文本窗口。");
                var nativeMs = openTimer.ElapsedMilliseconds;
                MOD_YtxSQ1.Services.CharacterEditSession.Shared.Clear();
                ownedWindow.InitData("八荒秘录", "正在读取人物信息…", "合上秘录");
                view = new BookView();
                view.Build(ownedWindow, Close, unitId);
                var viewMs = openTimer.ElapsedMilliseconds;
                WindowInputScope.Attach(ownedWindow, view.SearchInput);
                Debug.Log("[八荒秘录] 07.40打开总计=" + openTimer.ElapsedMilliseconds + "ms；原生窗口=" + nativeMs + "ms；视图初始化=" + (viewMs - nativeMs) + "ms；输入补丁=" + (openTimer.ElapsedMilliseconds - viewMs) + "ms");
            }
            catch (Exception ex)
            {
                ReleaseView();
                Debug.LogError("[八荒秘录] 窗口加载失败：" + ex);
                try
                {
                    if (ownedWindow != null && g.ui != null && g.ui.GetUI(UIType.TextInfoLong) == ownedWindow)
                        ownedWindow.InitData("八荒秘录 · 加载回退",
                            "书卷加载失败，已恢复原生窗口。\n请提供日志中的 [八荒秘录] 信息。\n本次界面加载未执行人物修改；此前已执行的修改不会因此撤销。", "合上秘录");
                    else Close();
                }
                catch (Exception fallbackError)
                {
                    Debug.LogWarning("[八荒秘录] 原生窗口回退失败，关闭本窗口：" + fallbackError.Message);
                    Close();
                }
            }
        }

        private void ReleaseView()
        {
            try { WindowInputScope.Detach(); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 释放输入隔离失败：" + ex.Message); }
            if (view == null) return;
            var previous = view;
            view = null;
            try { previous.Dispose(); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 释放视图失败：" + ex); }
        }

        public void RefreshCharacter()
        {
            if (IsOpen && view != null) view.RefreshCharacter();
        }

        public void RefreshOwnership()
        {
            // 原生返回/关闭也可能结束窗口；及时移除布局，避免共享窗口复用时残留。
            if (ownedWindow != null && (!ownedWindow.gameObject.activeInHierarchy ||
                g.ui.GetUI(UIType.TextInfoLong) != ownedWindow))
            {
                ReleaseView();
                ownedWindow = null;
            }
        }

        public void Close()
        {
            var previous = ownedWindow;
            ownedWindow = null;
            ReleaseView();
            if (previous == null) return;
            // 共享 UI 已被其他窗口替换时，不关闭别人的实例。
            try { if (g.ui != null && g.ui.GetUI(UIType.TextInfoLong) == previous) g.ui.CloseUI(previous, false); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 释放施工窗口失败：" + ex.Message); }
        }
    }
}
