using System;
using System.Collections.Generic;
using MOD_YtxSQ1.Services;
using UnityEngine;
using UnityEngine.UI;

namespace MOD_YtxSQ1.UI
{
    internal sealed partial class BookView
    {
        private readonly List<Button> npcActions = new List<Button>();
        private InputField affectionInput;
        private void BuildActions(RectTransform panel, int group, float width, float top)
        {
            if (group == 2)
            {
                var w = (width - 26) / 2;
                ActionButton(panel, "满修为", 10, top, w, "补满选中人物下一阶段所需修为，保留游戏正常瓶颈和突破流程。", false);
                ActionButton(panel, "满材料", 16 + w, top, w, "将玩家背包内可叠加物品补至9999，不可叠加物品保持原样。", false);
            }
            else if (group == 3)
            {
                var w = (width - 32) / 3;
                ActionButton(panel, "去", 10, top, w, "传送玩家到选中的NPC位置；选中自己时不可用。", true);
                ActionButton(panel, "来", 16 + w, top, w, "将选中的NPC传送到玩家位置，并记录该NPC原来的位置。", true);
                ActionButton(panel, "返", 22 + 2 * w, top, w, "将通过“来”召回的NPC送回原来的位置。", true);
            }
            else
            {
                var w = width - 82;
                affectionInput = CreateFateSearch(panel, "双向好感度", 10, top, w, 32, "双向好感度增量", _ => { });
                ActionButton(panel, "执行", 16 + w, top, 56, "输入增量，再点击执行；玩家与所选NPC对彼此的好感度各增加此值。", true, "好感度");
            }
        }
        private void ActionButton(RectTransform parent, string label, float x, float y, float width, string hint, bool npc, string action = null)
        {
            var button = MakeButton(parent, "快捷_" + label, x, y, width, 32, label, () =>
            {
                if (!FinishInlineEdit(false)) return;
                CloseDropdown(); HideFateTips();
                if (edits.Count > 0) { SetEditStatus("请先保存属性修改，再执行快捷操作", true); return; }
                string message;
                var success = CharacterActions.Execute(selectedUnitId, action ?? label, affectionInput == null ? null : affectionInput.text, out message);
                RefreshCharacter(true); RefreshEditors(); SetEditStatus(message, !success);
            });
            if (npc) npcActions.Add(button);
            BindFateHover(button.gameObject, () => ShowFateTip(button.GetComponent<RectTransform>(), new CharacterFate(0, 0, label, hint)), () => { fateTipCloseAt = Time.unscaledTime + .12f; });
        }
        private void RefreshActions()
        {
            var npc = CharacterActions.IsNpc(selectedUnitId) && !CharacterSave.Busy;
            foreach (var button in npcActions) button.interactable = npc;
            if (affectionInput != null) affectionInput.interactable = npc;
        }
        private void ClearActions()
        {
            if (affectionInput != null) WindowInputScope.Unregister(affectionInput);
            affectionInput = null; npcActions.Clear();
        }
    }
}
