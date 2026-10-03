using System;
using System.IO;
using System.Reflection;
using MOD_YtxSQ1.Services;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MOD_YtxSQ1.UI
{
    // 入口独立于内容控制器；内容熔断后仍可点击重试，不缓存人物引用。
    internal sealed class WorkshopEntrances : IDisposable
    {
        private sealed class Entry
        {
            internal UIBase Owner;
            internal RectTransform Rect;
            internal Button Button;
            internal GameObject Tip;
            internal string Context;
        }
        private readonly Action<string> open;
        private Entry mapEntry, npcEntry;
        private Texture2D texture;
        private float nextCheck;
        private string lastFailure;

        internal WorkshopEntrances(Action<string> open) { this.open = open; }

        internal void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + .5f;
            try
            {
                var context = CharacterReader.ContextKey();
                if (g.isDestroy || g.ui == null || g.world == null || g.world.isDestroy || !g.world.isIntoWorld || context == null)
                { ClearEntries(); return; }
                var map = g.ui.GetUI<UIMapMain>(UIType.MapMain);
                if (map == null || !map.gameObject.activeInHierarchy) { ClearEntries(); return; }
                if (map.uiPlayerInfo != null && map.uiPlayerInfo.textSPValue != null)
                    Sync(ref mapEntry, map, map.uiPlayerInfo.textSPValue.rectTransform, true, context);
                else Remove(ref mapEntry);
                var npc = g.ui.GetUI<UINPCInfo>(UIType.NPCInfo);
                if (npc != null && npc.gameObject.activeInHierarchy && npc.uiUnitInfo != null && npc.uiUnitInfo.imgHeresyBg != null)
                    Sync(ref npcEntry, npc, npc.uiUnitInfo.imgHeresyBg.rectTransform, false, context);
                else Remove(ref npcEntry);
                lastFailure = null;
            }
            catch (Exception ex)
            {
                ClearEntries();
                if (lastFailure != ex.Message) Debug.LogWarning("[八荒秘录] 入口暂未就绪，将重试：" + ex.Message);
                lastFailure = ex.Message;
                nextCheck = Time.unscaledTime + 3f;
            }
        }

        private void Sync(ref Entry entry, UIBase owner, RectTransform reference, bool player, string context)
        {
            if (entry != null && (entry.Owner != owner || entry.Rect == null || entry.Context != context)) Remove(ref entry);
            if (entry == null) entry = Create(owner, player, context);
            var root = owner.GetComponent<RectTransform>();
            var corners = new Vector3[4];
            reference.GetWorldCorners(corners);
            // 使用原生控件局部尺寸及缩放：主入口靠念力读数右侧，NPC入口在正魔标记下方。
            var center = player ? (corners[2] + corners[3]) * .5f : (corners[0] + corners[3]) * .5f;
            center = root.InverseTransformPoint(center);
            var scale = Mathf.Abs(reference.lossyScale.x / root.lossyScale.x);
            var size = 56f * scale;
            center.x += player ? 40f * scale : 0;
            center.y -= player ? 0 : 40f * scale;
            // clamp到原生面板内，避免宽高比变化把入口挤出可点击区域。
            var bounds = root.rect;
            center.x = Mathf.Clamp(center.x, bounds.xMin + size * .5f, bounds.xMax - size * .5f);
            center.y = Mathf.Clamp(center.y, bounds.yMin + size * .5f, bounds.yMax - size * .5f);
            entry.Rect.localPosition = new Vector3(center.x, center.y, 0);
            entry.Rect.sizeDelta = new Vector2(size, size);
            var allowed = g.ui.GetLayerTopUI(owner.uiType.uiLayer) == owner;
            entry.Button.interactable = allowed;
            if (!allowed) entry.Tip.SetActive(false);
        }

        private Entry Create(UIBase owner, bool player, string context)
        {
            EnsureTexture();
            var go = new GameObject(player ? "BHML_MapEntrance" : "BHML_NPCEntrance");
            go.layer = owner.gameObject.layer;
            var entry = new Entry { Owner = owner, Context = context };
            try
            {
                entry.Rect = go.AddComponent<RectTransform>();
                entry.Rect.SetParent(owner.transform, false);
                entry.Rect.anchorMin = entry.Rect.anchorMax = entry.Rect.pivot = new Vector2(.5f, .5f);
                var art = go.AddComponent<RawImage>();
                art.texture = texture;
                art.raycastTarget = true;
                entry.Button = go.AddComponent<Button>();
                entry.Button.targetGraphic = art;
                var colors = entry.Button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 1f, .80f, 1f);
                colors.pressedColor = new Color(.75f, .70f, .55f, 1f);
                colors.disabledColor = new Color(1f, 1f, 1f, .6f);
                colors.fadeDuration = .10f;
                entry.Button.colors = colors;
                entry.Button.navigation = new Navigation { mode = Navigation.Mode.None };
                entry.Button.onClick.AddListener((UnityAction)(new Action(() => Click(entry, player))));
                var tip = new GameObject("BHML_EntranceTip");
                tip.layer = go.layer;
                entry.Tip = tip;
                var tipRect = tip.AddComponent<RectTransform>();
                tipRect.SetParent(entry.Rect, false);
                tipRect.anchorMin = tipRect.anchorMax = tipRect.pivot = new Vector2(.5f, .5f);
                tipRect.sizeDelta = new Vector2(152, 28);
                tipRect.anchoredPosition = new Vector2(0, player ? 43 : -43);
                var background = tip.AddComponent<Image>();
                background.color = new Color(.08f, .085f, .075f, .96f);
                background.raycastTarget = false;
                var label = new GameObject("文字");
                label.layer = go.layer;
                var labelRect = label.AddComponent<RectTransform>();
                labelRect.SetParent(tipRect, false);
                labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                var text = label.AddComponent<Text>();
                text.font = player ? ((UIMapMain)owner).uiPlayerInfo.textSPValue.font : ((UINPCInfo)owner).uiUnitInfo.textStand1.font;
                text.text = player ? "八荒秘录 · 玩家" : "八荒秘录 · 当前人物";
                text.fontSize = 16; text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(.93f, .85f, .65f, 1); text.raycastTarget = false;
                tip.SetActive(false);
                var listener = go.AddComponent<UIEventListener>();
                listener.onMouseEnter = new UnityEvent(); listener.onMouseExit = new UnityEvent();
                listener.onMouseEnter.AddListener((UnityAction)(new Action(() => { if (entry.Button.interactable) tip.SetActive(true); })));
                listener.onMouseExit.AddListener((UnityAction)(new Action(() => tip.SetActive(false))));
                Debug.Log("[八荒秘录] 07.39已挂载" + (player ? "主界面" : "NPC面板") + "玉简入口。");
                return entry;
            }
            catch { go.SetActive(false); UnityEngine.Object.Destroy(go); throw; }
        }

        private void Click(Entry entry, bool player)
        {
            try
            {
                entry.Tip.SetActive(false);
                if (g.isDestroy || g.ui == null || g.world == null || g.world.isDestroy || !g.world.isIntoWorld ||
                    entry.Context != CharacterReader.ContextKey() || entry.Owner == null || !entry.Owner.gameObject.activeInHierarchy ||
                    g.ui.GetUI(player ? UIType.MapMain : UIType.NPCInfo) != entry.Owner ||
                    g.ui.GetLayerTopUI(entry.Owner.uiType.uiLayer) != entry.Owner) return;
                string id = null;
                if (!player)
                {
                    var npc = g.ui.GetUI<UINPCInfo>(UIType.NPCInfo);
                    var unit = npc == null ? null : npc.unit;
                    if (unit == null || unit.data == null || unit.data.unitData == null) { UITipItem.AddTip("当前人物尚未就绪。"); return; }
                    id = unit.data.unitData.unitID;
                    if (string.IsNullOrEmpty(id) || CharacterReader.Resolve(id) == null) { UITipItem.AddTip("当前人物已失效，请重新打开人物面板。"); return; }
                }
                open(id);
            }
            catch (Exception ex) { Debug.LogError("[八荒秘录] 入口打开失败：" + ex); UITipItem.AddTip("秘录暂未打开，请重新点击入口。"); }
        }

        private void EnsureTexture()
        {
            if (texture != null) return;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MOD_YtxSQ1.Art.EntranceJade.png"))
            using (var buffer = new MemoryStream())
            {
                if (stream == null) throw new InvalidOperationException("缺少玉简入口资源。");
                stream.CopyTo(buffer);
                var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                try { if (!ImageConversion.LoadImage(loaded, buffer.ToArray(), true)) throw new InvalidOperationException("玉简图片解码失败。"); texture = loaded; }
                catch { UnityEngine.Object.Destroy(loaded); throw; }
            }
        }
        private static void Remove(ref Entry entry)
        {
            var old = entry; entry = null;
            if (old == null || old.Rect == null) return;
            try { if (old.Button != null) old.Button.onClick.RemoveAllListeners(); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 解绑入口失败：" + ex.Message); }
            try
            {
                var listener = old.Rect.GetComponent<UIEventListener>();
                if (listener != null) { listener.onMouseEnter.RemoveAllListeners(); listener.onMouseExit.RemoveAllListeners(); }
            }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 解绑入口悬停失败：" + ex.Message); }
            try { old.Rect.gameObject.SetActive(false); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 隐藏入口失败：" + ex.Message); }
            try { UnityEngine.Object.Destroy(old.Rect.gameObject); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 销毁入口失败：" + ex.Message); }
        }
        private void ClearEntries() { Remove(ref mapEntry); Remove(ref npcEntry); }
        public void Dispose()
        {
            ClearEntries();
            var old = texture; texture = null;
            if (old != null) UnityEngine.Object.Destroy(old);
        }
    }
}
