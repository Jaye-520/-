using System;
using MOD_YtxSQ1.Services;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MOD_YtxSQ1.UI
{
    internal sealed partial class BookView
    {
        private GameObject itemMenu;

        private void BindItemMenu(GameObject icon, RectTransform slot, InventoryItem item)
        {
            // 绑定不可变快照而非池下标。复用格子时，旧图标与监听一起销毁。
            var id = selectedUnitId; var context = contextKey;
            var listeners = icon.GetComponentsInChildren<UIEventListener>(true);
            if (listeners.Length == 0) BindItemPress(icon.AddComponent<UIEventListener>(), slot, item, id, context);
            else foreach (var listener in listeners) BindItemPress(listener, slot, item, id, context);
        }
        private void BindItemPress(UIEventListener listener, RectTransform slot, InventoryItem item, string id, string context)
        {
            if (listener.onPointDown == null) listener.onPointDown = new UnityEvent();
            listener.onPointDown.AddListener((UnityAction)(new Action(() =>
            {
                HideFateTips();
                if (Input.GetMouseButtonDown(1) && id == selectedUnitId && context == contextKey)
                    OpenItemMenu(slot, item);
            })));
        }
        private void OpenItemMenu(RectTransform slot, InventoryItem item)
        {
            if (CharacterSave.Busy || DataMgr.isSaveData || !FinishInlineEdit(false)) return;
            CloseItemDialog(); CloseItemLibrary(); CloseDropdown(); HideFateTips();
            if (selectedIndex != 1 || slot == null || !slot.gameObject.activeInHierarchy) return;
            foreach (var nativeIcon in slot.GetComponentsInChildren<UIIconItemBase>(true))
                try { nativeIcon.CloseIconInfo(); } catch (Exception ex) { Debug.LogWarning("[八荒秘录] 图标提示清理失败：" + ex.Message); }
            var id = selectedUnitId; var context = contextKey;
            var width = Mathf.Min(242, contentWidth - 16); const float height = 204;
            var bounds = FateTipBounds(FateRowBounds(slot, content), width, height, content.sizeDelta);
            var layer = Box("背包菜单遮罩", content, 0, 0, contentWidth, contentHeight); itemMenu = layer.gameObject;
            var dismiss = Fill(layer, "背包菜单外部", 0, 0, contentWidth, contentHeight, new Color(1, 1, 1, .001f), true);
            var outside = dismiss.gameObject.AddComponent<Button>(); outside.targetGraphic = dismiss;
            outside.onClick.AddListener((UnityAction)(new Action(CloseItemMenu)));
            var panel = Card(layer, "背包菜单", bounds.x, bounds.y, width, height);
            Fill(panel, "背包菜单纸底", 1, 1, width - 2, height - 2, Paper, true); Border(panel, width, height);
            var title = Label(panel, item.Name, 12, 8, width - 24, 28, 19, Ink);
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 12; title.resizeTextMaxSize = 19;
            Label(panel, characterName.text + " · 数量 " + item.Count, 12, 38, width - 24, 24, 14, Muted);
            var conf = g.conf.itemProps.GetItem(item.Id); bool stackable = conf != null && conf.isOverlay == 1;
            var edit = MakeButton(panel, "修改背包数量", 12, 70, width - 24, 34,
                stackable ? "修改数量" : "不可叠加 · 数量固定", () => ConfirmItemCount(item, id, context));
            edit.interactable = stackable;
            var increase = MakeButton(panel, "增加背包数量", 12, 112, width - 24, 34, "增加数量", () => ConfirmItemCount(item, id, context, true));
            increase.interactable = stackable && item.Count < int.MaxValue;
            var remove = MakeButton(panel, "移除背包单项", 12, 154, width - 24, 34, "移除这一项", () => ConfirmRemoveItem(item, id, context));
            remove.GetComponentInChildren<Text>().color = new Color(.62f, .22f, .16f, 1);
        }
        private bool ItemTargetMatches(string id, string context)
        {
            if (id == selectedUnitId && context == contextKey) return true;
            CloseItemDialog(); SetEditStatus("人物已变化，请重新操作", true); return false;
        }
        private void ConfirmItemCount(InventoryItem item, string id, string context, bool increase = false)
        {
            if (!ItemTargetMatches(id, context)) return;
            string unusedId, unusedContext;
            var panel = ItemDialog((increase ? "增加" : "修改") + characterName.text + "的物品数量", out unusedId, out unusedContext);
            Label(panel, item.Name + " · 当前 " + item.Count, 16, 54, panel.sizeDelta.x - 32, 30, 18, Ink);
            itemAmount = CreateSearchInput(panel, "背包数量", 16, 98, panel.sizeDelta.x - 32, 36, "正整数数量", _ => { });
            itemAmount.SetTextWithoutNotify(increase ? "1" : item.Count.ToString());
            itemDialogStatus.text = increase ? "输入追加数量 · 最多增加" + (int.MaxValue - item.Count) : "输入修改后的总数量 · 最大2147483647";
            MakeButton(panel, "确认修改背包数量", panel.sizeDelta.x - 136, 176, 120, 36, "确定", () =>
            {
                if (!string.IsNullOrEmpty(Input.compositionString) || !ItemTargetMatches(id, context)) return;
                int count;
                if (!int.TryParse(itemAmount.text, out count) || count < 1 || (increase && count > int.MaxValue - item.Count))
                { SetItemStatus(increase ? "追加后的总数量不能超过2147483647" : "请输入1–2147483647的整数数量", true); return; }
                if (RunItemOperation(() => increase ? ItemInventory.IncreaseCount(context, id, item, count) : ItemInventory.SetCount(context, id, item, count))) CloseItemDialog();
            });
        }
        private void ConfirmRemoveItem(InventoryItem item, string id, string context)
        {
            if (!ItemTargetMatches(id, context)) return;
            string unusedId, unusedContext;
            var panel = ItemDialog("移除" + characterName.text + "的这一项", out unusedId, out unusedContext);
            Label(panel, item.Name + " × " + item.Count + "\n将移除这一项的全部数量。\n此操作无法撤销。", 16, 54, panel.sizeDelta.x - 32, 98, 17, Ink);
            MakeButton(panel, "确认移除背包单项", panel.sizeDelta.x - 136, 176, 120, 36, "确认移除", () =>
            {
                if (!ItemTargetMatches(id, context)) return;
                if (RunItemOperation(() => ItemInventory.Remove(context, id, item))) CloseItemDialog();
            });
        }
        private void CloseItemMenu()
        {
            var old = itemMenu; itemMenu = null;
            if (old != null) { old.SetActive(false); UnityEngine.Object.Destroy(old); }
        }
    }
}
