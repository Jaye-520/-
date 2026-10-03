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
        private sealed class EditBinding
        {
            internal CharacterField Field;
            internal RectTransform Rect;
            internal Text Label;
            internal Button Button;
            internal string Original;
        }
        private readonly List<EditBinding> editBindings = new List<EditBinding>();
        private readonly CharacterEditSession edits = CharacterEditSession.Shared;
        private InputField inlineInput;
        private EditBinding activeBinding;
        private string activeOriginal;
        private GameObject dropdownLayer;
        private Text editStatus;
        private bool endingEditor;
        private int escapeHandledFrame = -1;

        private void BuildFieldEditors(int group, int index, Text placeholder)
        {
            placeholder.enabled = false;
            var count = CharacterField.Parts(group, index);
            var pair = group == 0 && index <= 6;
            var percent = group == 1 && (index == 8 || index == 9);
            var host = placeholder.rectTransform;
            var width = host.sizeDelta.x;
            var height = host.sizeDelta.y;
            var gap = pair ? 10f : count > 1 ? 4f : 0f;
            var itemWidth = (width - (count - 1) * gap - (percent ? 14 : 0)) / count;
            for (int slot = 0; slot < count; slot++)
            {
                var binding = new EditBinding { Field = new CharacterField(group, index, slot) };
                var image = Fill(host, "编辑值_" + binding.Field.Key, slot * (itemWidth + gap), 0,
                    itemWidth, height, new Color(.92f, .91f, .86f, .12f), true);
                binding.Rect = image.rectTransform;
                var button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener((UnityAction)(new Action(() => StartFieldEdit(binding))));
                binding.Button = button;
                binding.Label = Label(binding.Rect, "—", 2, 0, itemWidth - 4, height, 17, Ink,
                    count > 1 ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight);
                binding.Label.resizeTextForBestFit = true;
                binding.Label.resizeTextMinSize = 11; binding.Label.resizeTextMaxSize = 17;
                binding.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
                if (binding.Field.Choice) Border(binding.Rect, itemWidth, height);
                editBindings.Add(binding);
            }
            if (pair) Label(host, "/", itemWidth, 0, gap, height, 16, Muted, TextAnchor.MiddleCenter);
            if (percent) Label(host, "%", width - 14, 0, 14, height, 15, Ink, TextAnchor.MiddleRight);
        }

        private void RefreshEditors()
        {
            edits.Sync(CharacterReader.ContextKey());
            var unit = CharacterReader.Resolve(selectedUnitId);
            foreach (var binding in editBindings)
            {
                try
                {
                    binding.Original = unit == null ? null : CharacterEditData.Read(unit, binding.Field);
                    binding.Button.interactable = binding.Original != null;
                    var value = edits.Value(selectedUnitId, binding.Field, binding.Original);
                    binding.Label.text = value == null ? "—" : CharacterEditData.Label(binding.Field, value);
                    binding.Label.color = edits.IsChanged(selectedUnitId, binding.Field) ? Cinnabar : Ink;
                }
                catch
                {
                    binding.Original = null; binding.Label.text = "暂不可读"; binding.Button.interactable = false;
                }
            }
            CharacterSave.Sync(CharacterReader.ContextKey());
            var pending = edits.Count + CharacterSave.Pending;
            if (tabLabels.Count > 6) tabLabels[6].text = CharacterSave.Busy ? "保存中…" : pending == 0 ? "保存" : "保存（" + pending + "）";
            RefreshActions();
        }

        private void SetEditStatus(string message, bool error = false)
        {
            if (editStatus != null) { editStatus.text = message; editStatus.color = error ? Cinnabar : Muted; }
        }

        private void StartFieldEdit(EditBinding binding)
        {
            if (CharacterSave.Busy) return;
            if (binding.Original == null || !FinishInlineEdit(false)) return;
            CloseDropdown(); HideFateTips();
            if (binding.Field.Choice) { OpenChoices(binding); return; }
            activeBinding = binding;
            activeOriginal = binding.Original;
            binding.Label.enabled = false;
            var clone = UnityEngine.Object.Instantiate(targetSearch.gameObject, binding.Rect, false);
            clone.SetActive(false); clone.name = "BHML_属性输入_" + binding.Field.Key;
            var rect = clone.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1); rect.localScale = Vector3.one;
            inlineInput = clone.GetComponent<InputField>();
            ResetClonedInput(inlineInput, 2);
            foreach (var trigger in clone.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true))
            { trigger.enabled = false; UnityEngine.Object.Destroy(trigger); }
            inlineInput.onValueChanged = new InputField.OnChangeEvent();
            inlineInput.onEndEdit = new InputField.SubmitEvent();
            inlineInput.onValidateInput = null;
            inlineInput.characterLimit = binding.Field.Name ? 24 : 10;
            inlineInput.textComponent.fontSize = 16;
            inlineInput.textComponent.alignment = binding.Label.alignment;
            inlineInput.textComponent.rectTransform.offsetMin = new Vector2(2, 2);
            inlineInput.textComponent.rectTransform.offsetMax = new Vector2(-2, -2);
            inlineInput.textComponent.resizeTextForBestFit = false;
            var placeholder = inlineInput.placeholder == null ? null : inlineInput.placeholder.TryCast<Text>();
            if (placeholder != null) placeholder.text = "";
            if (inlineInput.placeholder != null) inlineInput.placeholder.gameObject.SetActive(false);
            var value = edits.Value(selectedUnitId, binding.Field, binding.Original);
            inlineInput.SetTextWithoutNotify(value);
            ResizeInlineInput();
            inlineInput.onValueChanged.AddListener((UnityAction<string>)(new Action<string>(_ => ResizeInlineInput())));
            inlineInput.onEndEdit.AddListener((UnityAction<string>)(new Action<string>(_ =>
            {
                if (!endingEditor && string.IsNullOrEmpty(Input.compositionString))
                    FinishInlineEdit(Input.GetKeyDown(KeyCode.Escape));
            })));
            WindowInputScope.Register(inlineInput);
            clone.SetActive(true);
            inlineInput.AssignPositioningIfNeeded();
            inlineInput.UpdateLabel();
            inlineInput.ActivateInputField();
            inlineInput.selectionAnchorPosition = 0;
            inlineInput.selectionFocusPosition = value.Length;
            SetEditStatus("修改后按Enter确认，Escape撤销；点击保存才会应用到游戏");
        }

        private bool FinishInlineEdit(bool cancel)
        {
            if (inlineInput == null || endingEditor) return true;
            if (cancel && Input.GetKeyDown(KeyCode.Escape)) escapeHandledFrame = Time.frameCount;
            if (!cancel && !string.IsNullOrEmpty(Input.compositionString)) return false;
            if (!cancel)
            {
                string error;
                if (!StageField(activeBinding, inlineInput.text, out error, activeOriginal))
                {
                    SetEditStatus(error, true);
                    inlineInput.ActivateInputField(); return false;
                }
            }
            endingEditor = true;
            var previous = inlineInput;
            var previousBinding = activeBinding;
            inlineInput = null; activeBinding = null; activeOriginal = null;
            try
            {
                if (previousBinding != null) Cleanup(() => previousBinding.Label.enabled = true, "恢复编辑文字");
                WindowInputScope.Unregister(previous);
                Cleanup(() => previous.gameObject.SetActive(false), "停用输入框");
                Cleanup(() => UnityEngine.Object.Destroy(previous.gameObject), "释放输入框");
            }
            finally { endingEditor = false; }
            RefreshEditors();
            SetEditStatus(edits.Count == 0 ? "点击数值修改；兴趣、性格与境界可下拉选择" : "有 " + edits.Count + " 项待保存修改");
            return true;
        }

        private void ResizeInlineInput()
        {
            if (inlineInput == null || activeBinding == null) return;
            var area = activeBinding.Rect.sizeDelta;
            // OnValueChanged在原生UpdateLabel之前触发，不能拿上一帧/空文本计算宽度。
            inlineInput.textComponent.text = inlineInput.text;
            inlineInput.textComponent.fontSize = 16;
            var limit = Mathf.Max(area.x, 96);
            if (!activeBinding.Field.Name)
                while (inlineInput.textComponent.fontSize > 11 && inlineInput.textComponent.preferredWidth + 14 > limit)
                    inlineInput.textComponent.fontSize--;
            var width = activeBinding.Field.Name ? area.x : Mathf.Max(28, inlineInput.textComponent.preferredWidth + 14);
            width = Mathf.Min(limit, width);
            var height = Mathf.Min(area.y, 24);
            var left = activeBinding.Label.alignment == TextAnchor.MiddleCenter ? (area.x - width) / 2 : area.x - width;
            var rect = inlineInput.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -(area.y - height) / 2);
            inlineInput.AssignPositioningIfNeeded();
        }

        private bool StageField(EditBinding binding, string value, out string error, string original = null)
        {
            if (CharacterReader.ContextKey() != edits.Context || CharacterReader.Resolve(selectedUnitId) == null)
            { error = "人物或存档已改变，请重新读取"; return false; }
            if (binding.Field.Group == 0 && binding.Field.Index >= 1 && binding.Field.Index <= 6 && binding.Field.Slot == 0)
            {
                foreach (var item in editBindings)
                    if (item.Field.Group == 0 && item.Field.Index == binding.Field.Index && item.Field.Slot == 1)
                    {
                        int entered, maximum;
                        if (int.TryParse(value, out entered) && int.TryParse(edits.Value(selectedUnitId, item.Field, item.Original), out maximum) && entered > maximum)
                        { error = "当前值不能超过上限 " + maximum + "，可先修改上限"; return false; }
                    }
            }
            if (!edits.Stage(selectedUnitId, binding.Field, original ?? binding.Original, value, out error, true)) return false;
            // 降低资源上限时将受影响的当前值显式列为另一项草稿；年龄/寿命不执行资源钳制。
            if (binding.Field.Group == 0 && binding.Field.Index >= 1 && binding.Field.Index <= 6)
            {
                EditBinding current = null, maximum = null;
                foreach (var item in editBindings)
                    if (item.Field.Group == 0 && item.Field.Index == binding.Field.Index)
                    { if (item.Field.Slot == 0) current = item; else maximum = item; }
                int cur, max;
                if (binding.Field.Slot == 1 && current != null && maximum != null &&
                    int.TryParse(edits.Value(selectedUnitId, current.Field, current.Original), out cur) &&
                    int.TryParse(edits.Value(selectedUnitId, maximum.Field, maximum.Original), out max) && cur > max)
                {
                    edits.Stage(selectedUnitId, current.Field, current.Original, max.ToString(), out error, true);
                }
            }
            return true;
        }

        private void OpenChoices(EditBinding binding)
        {
            var original = binding.Original;
            List<CharacterChoice> choices;
            try { choices = CharacterEditData.Choices(binding.Field, binding.Original); }
            catch (Exception ex) { SetEditStatus("选项暂不可读取：" + ex.GetType().Name, true); return; }
            if (choices.Count == 0) { SetEditStatus("没有可选配置", true); return; }
            var bounds = FateRowBounds(binding.Rect, content);
            var width = Mathf.Min(280, Mathf.Max(180, bounds.width));
            var height = Mathf.Min(224, choices.Count * 30 + 8);
            var left = Mathf.Max(4, Mathf.Min(bounds.x, contentWidth - width - 4));
            var top = bounds.y + bounds.height + 4;
            if (top + height > contentHeight - 4) top = bounds.y - height - 4;
            top = Mathf.Max(4, Mathf.Min(top, contentHeight - height - 4));
            var shield = Fill(content, "选择浮层", 0, 0, contentWidth, contentHeight, new Color(1, 1, 1, .001f), true);
            dropdownLayer = shield.gameObject;
            var dismiss = shield.gameObject.AddComponent<Button>(); dismiss.targetGraphic = shield;
            dismiss.onClick.AddListener((UnityAction)(new Action(CloseDropdown)));
            var panel = Card(shield.rectTransform, "选项列表", left, top, width, height);
            Border(panel, width, height);
            var viewport = Box("选项视口", panel, 4, 4, width - 8, height - 8);
            Fill(viewport, "选项纸底", 0, 0, width - 8, height - 8, new Color(.94f, .93f, .88f, 1), true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var page = NewRect("选项长页", viewport);
            page.anchorMin = page.anchorMax = new Vector2(0, 1); page.pivot = new Vector2(0, 1);
            page.sizeDelta = new Vector2(width - 8, choices.Count * 30);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = page;
            scroll.horizontal = false; scroll.vertical = page.sizeDelta.y > height - 8;
            scroll.inertia = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                var button = MakeButton(page, "选项_" + choice.Id, 0, i * 30, width - 8, 28, choice.Label, () =>
                {
                    string error;
                    if (StageField(binding, choice.Id, out error, original))
                    { CloseDropdown(); RefreshEditors(); SetEditStatus("有 " + edits.Count + " 项待保存修改"); }
                    else SetEditStatus(error, true);
                });
                var label = button.GetComponentInChildren<Text>(); label.fontSize = 16;
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = 16;
                if (binding.Field.Group == 1 && binding.Field.Index == 10)
                    BindFateHover(button.gameObject, () =>
                    {
                        var description = CharacterEditData.ChoiceDescription(binding.Field, choice.Id);
                        if (!string.IsNullOrEmpty(description)) ShowFateTip(button.GetComponent<RectTransform>(), new CharacterFate(0, 0, choice.Label, description));
                    }, () => { fateTipCloseAt = Time.unscaledTime + .12f; });
            }
            scroll.onValueChanged.AddListener((UnityAction<Vector2>)(new Action<Vector2>(_ => HideFateTips())));
        }

        private void CloseDropdown()
        {
            HideFateTips();
            var previous = dropdownLayer; dropdownLayer = null;
            if (previous != null) { previous.SetActive(false); UnityEngine.Object.Destroy(previous); }
        }
        private void TickEditing()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && escapeHandledFrame != Time.frameCount)
            {
                escapeHandledFrame = Time.frameCount;
                if (inlineInput != null) FinishInlineEdit(true);
                else if (dropdownLayer != null) CloseDropdown();
                else if (itemMenu != null) CloseItemMenu();
                else if (itemDialog != null) CloseItemDialog();
                else if (itemLibrary != null) CloseItemLibrary();
                else if (fateLibrary != null) CloseFateLibrary();
                else if (martialOverlay != null) CloseMartialOverlay();
                else if (requestClose != null) requestClose();
            }
        }
        private void ClearEditingView()
        {
            Cleanup(() => FinishInlineEdit(true), "取消输入");
            Cleanup(CloseDropdown, "关闭下拉");
            Cleanup(ClearFateLibraryView, "气运图鉴");
            Cleanup(ClearItemsView, "道具视图");
            Cleanup(ClearMartialsView, "功法视图");
            Cleanup(ClearActions, "快捷说明");
            editBindings.Clear();
        }
        private void SaveEdits()
        {
            if (!FinishInlineEdit(false)) return;
            CloseDropdown();
            if (CharacterSave.Busy) { SetEditStatus("正在保存，请稍候"); return; }
            if (edits.Count == 0 && CharacterSave.Pending == 0) { SetEditStatus("没有待保存修改"); return; }
            if (CharacterReader.ContextKey() != edits.Context)
            { edits.Sync(CharacterReader.ContextKey()); RefreshEditors(); SetEditStatus("存档上下文已变化，旧草稿已清理", true); return; }
            string validation;
            if (!edits.Validate(change =>
            {
                var unit = CharacterReader.Resolve(change.UnitId);
                return unit == null ? null : CharacterEditData.Read(unit, change.Field);
            }, out validation)) { SetEditStatus(validation, true); return; }
            CharacterSave.Commit(edits, (success, message) =>
            {
                if (root == null) return;
                RefreshCharacter(true); RefreshEditors(); SetEditStatus(message, !success);
            });
        }
    }
}
