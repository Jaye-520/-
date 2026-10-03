using System;
using MOD_YtxSQ1.UI;
using MOD_YtxSQ1.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MOD_YtxSQ1.Core
{
    internal sealed class WorkshopController : IDisposable
    {
        private readonly WorkshopWindow window = new WorkshopWindow();
        private float nextIdleCheck;

        internal void OpenFromEntrance(string unitId)
        {
            if (g.isDestroy || g.ui == null || g.world == null || g.world.isDestroy || !g.world.isIntoWorld) return;
            var map = g.ui.GetUI(UIType.MapMain);
            if (map == null || !map.gameObject.activeInHierarchy) return;
            window.RefreshOwnership();
            window.Open(unitId);
        }

        public void Update()
        {
            CharacterSave.Poll();
            var togglePressed = Input.GetKeyDown(KeyCode.F8);
            // 关闭时只做低频世界状态检查；按键仍逐帧检测，不增加打开延迟。
            if (!window.IsOpen && !togglePressed)
            {
                if (Time.unscaledTime < nextIdleCheck) return;
                nextIdleCheck = Time.unscaledTime + .5f;
                if (g.isDestroy || g.world == null || g.world.isDestroy || !g.world.isIntoWorld)
                { CharacterEditSession.Shared.Sync(null); CharacterSave.Sync(null); }
                window.RefreshOwnership();
                return;
            }
            if (g.isDestroy || g.world == null || g.world.isDestroy || !g.world.isIntoWorld)
            { CharacterEditSession.Shared.Sync(null); CharacterSave.Sync(null); }
            if (g.isDestroy || g.ui == null) return;
            window.RefreshOwnership();
            var map = g.ui.GetUI(UIType.MapMain);
            if (map == null || !map.gameObject.activeInHierarchy)
            {
                window.Close();
                return;
            }
            window.RefreshCharacter();
            if (!togglePressed || IsEditingText()) return;
            // 同层还有其他界面时不抢占入口；自己的窗口可用 F8 合上。
            var top = g.ui.GetLayerTopUI(map.uiType.uiLayer);
            if (!window.IsOpen && top != map) return;
            window.Toggle();
        }

        internal static bool IsEditingText()
        {
            var events = EventSystem.current;
            if (events == null || events.currentSelectedGameObject == null) return false;
            var field = events.currentSelectedGameObject.GetComponentInParent<InputField>();
            if (field != null && field.isFocused) return true;
            var tmp = events.currentSelectedGameObject.GetComponentInParent<TMPro.TMP_InputField>();
            return tmp != null && tmp.isFocused;
        }

        public void Dispose() { window.Close(); CharacterEditSession.Shared.Sync(null); }
    }
}
