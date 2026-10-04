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
            internal bool Player;
            internal Text Caption;
            internal RectTransform Root;
            internal float Scale;
        }
        private readonly Action<string> open;
        private Entry mapEntry, npcEntry;
        private Texture2D texture;
        private float nextCheck;
        private string lastFailure;
        // 入口出厂位置（不再提供游戏内调整）：主界面跟随原生念力读数右边中点+40单位；
        // NPC为2026-10-03用户在2560x1600游戏内实测微调后固化的归一化中心（相对各自UI根矩形）。
        private const float MapOffset = 40f, MapSize = 56f;
        private const float NpcAnchorX = 0.2542969f, NpcAnchorY = 0.746458352f, NpcSize = 56f;

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
                if (npc != null && npc.gameObject.activeInHierarchy && npc.uiUnitInfo != null && npc.uiUnitInfo.btnAppellationAttack != null)
                    Sync(ref npcEntry, npc, npc.uiUnitInfo.btnAppellationAttack.GetComponent<RectTransform>(), false, context);
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
            // GetWorldCorners的IL2CPP数组转换不会写回普通C#数组；使用单值TransformPoint。
            var referenceBounds = reference.rect;
            var center = root.InverseTransformPoint(reference.TransformPoint(new Vector3(referenceBounds.xMax,
                (referenceBounds.yMin + referenceBounds.yMax) * .5f, 0)));
            var scale = Mathf.Abs(reference.lossyScale.x / root.lossyScale.x);
            if (!(scale > 0 && scale < 100 && root.rect.width > 0 && root.rect.height > 0))
                throw new InvalidOperationException("原生入口参照矩形尚未就绪。");
            entry.Root = root; entry.Scale = scale;
            var bounds = root.rect;
            var size = (player ? MapSize : NpcSize) * scale;
            if (player) center.x += MapOffset * scale;
            else { center.x = bounds.xMin + NpcAnchorX * bounds.width; center.y = bounds.yMin + NpcAnchorY * bounds.height; }
            // clamp到原生面板内，避免宽高比变化把入口挤出可点击区域。
            center.x = Mathf.Clamp(center.x, bounds.xMin + size * .5f, bounds.xMax - size * .5f);
            center.y = Mathf.Clamp(center.y, bounds.yMin + size * .5f, bounds.yMax - size * .5f);
            entry.Rect.localPosition = new Vector3(center.x, center.y, 0);
            entry.Rect.sizeDelta = new Vector2(size, size);
            var allowed = g.ui.GetLayerTopUI(owner.uiType.uiLayer) == owner;
            entry.Button.interactable = allowed;
            UpdateCaption(entry, allowed);
        }

        private Entry Create(UIBase owner, bool player, string context)
        {
            EnsureTexture();
            var go = new GameObject(player ? "BHML_MapEntrance" : "BHML_NPCEntrance");
            GameObject tip = null, label = null;
            var entry = new Entry { Owner = owner, Context = context, Player = player };
            try
            {
                go.layer = owner.gameObject.layer;
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
                tip = new GameObject("BHML_EntranceTip");
                tip.layer = go.layer;
                entry.Tip = tip;
                var tipRect = tip.AddComponent<RectTransform>();
                tipRect.SetParent(entry.Rect, false);
                tipRect.anchorMin = tipRect.anchorMax = tipRect.pivot = new Vector2(.5f, .5f);
                tipRect.sizeDelta = new Vector2(260, 28);
                tipRect.anchoredPosition = new Vector2(0, player ? 43 : -43);
                var background = tip.AddComponent<Image>();
                background.color = new Color(.08f, .085f, .075f, .96f);
                background.raycastTarget = false;
                label = new GameObject("文字");
                label.layer = go.layer;
                var labelRect = label.AddComponent<RectTransform>();
                labelRect.SetParent(tipRect, false);
                labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                var text = label.AddComponent<Text>();
                entry.Caption = text;
                text.font = player ? ((UIMapMain)owner).uiPlayerInfo.textSPValue.font : ((UINPCInfo)owner).uiUnitInfo.textStand1.font;
                text.text = player ? "八荒秘录 · 玩家" : "八荒秘录 · 当前人物";
                text.fontSize = 16; text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(.93f, .85f, .65f, 1); text.raycastTarget = false;
                tip.SetActive(false);
                var listener = go.AddComponent<UIEventListener>();
                listener.onMouseEnter = new UnityEvent(); listener.onMouseExit = new UnityEvent();
                listener.onMouseEnter.AddListener((UnityAction)(new Action(() => { if (entry.Button.interactable) tip.SetActive(true); })));
                listener.onMouseExit.AddListener((UnityAction)(new Action(() => tip.SetActive(false))));
                Debug.Log("[八荒秘录] " + ModVersion.Current + "已挂载" + (player ? "主界面" : "NPC面板") + "玉简入口。");
                return entry;
            }
            catch
            {
                // Children may fail before parenting, so the root alone cannot own cleanup.
                ReleaseIncompleteObject(label);
                ReleaseIncompleteObject(tip);
                ReleaseIncompleteObject(go);
                throw;
            }
        }

        private static void ReleaseIncompleteObject(GameObject previous)
        {
            if (previous == null) return;
            try { previous.SetActive(false); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 隐藏未完成入口失败：" + ex.Message); }
            try { UnityEngine.Object.Destroy(previous); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 销毁未完成入口失败：" + ex.Message); }
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
                var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                try
                {
                    loaded.filterMode = FilterMode.Trilinear;
                    loaded.wrapMode = TextureWrapMode.Clamp;
                    loaded.hideFlags = HideFlags.HideAndDontSave;
                    if (!ImageConversion.LoadImage(loaded, buffer.ToArray(), true)) throw new InvalidOperationException("玉简图片解码失败。");
                    texture = loaded;
                }
                catch
                {
                    try { UnityEngine.Object.Destroy(loaded); }
                    catch (Exception ex) { Debug.LogWarning("[八荒秘录] 销毁未完成入口纹理失败：" + ex.Message); }
                    throw;
                }
            }
        }
        private void UpdateCaption(Entry entry, bool allowed)
        {
            if (!allowed) { entry.Tip.SetActive(false); return; }
            var tip = entry.Tip.GetComponent<RectTransform>();
            tip.sizeDelta = new Vector2(260, 28);
            var above = entry.Root.rect.yMax - entry.Rect.localPosition.y > 120;
            var tipX = Mathf.Clamp(entry.Rect.localPosition.x, entry.Root.rect.xMin + tip.sizeDelta.x * .5f,
                entry.Root.rect.xMax - tip.sizeDelta.x * .5f) - entry.Rect.localPosition.x;
            tip.anchoredPosition = new Vector2(tipX, (above ? 1 : -1) * (entry.Rect.sizeDelta.y * .5f + 18));
            entry.Caption.text = "八荒秘录 · " + (entry.Player ? "玩家" : "当前人物");
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
