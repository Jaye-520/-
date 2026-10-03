using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MOD_YtxSQ1.UI
{
    // 停用承载说明窗口的原导航组；隐藏按钮仍会被导航逻辑强制选中。
    internal static class WindowInputScope
    {
        private static UIBase owner;
        private static InputField input;
        private static readonly List<InputField> editors = new List<InputField>();
        internal static void Register(InputField field) { if (field != null && !editors.Contains(field)) editors.Add(field); }
        internal static void Unregister(InputField field) { editors.Remove(field); }
        private static bool Owns(InputField field) { return field != null && (field == input || editors.Contains(field)); }
        private static Harmony patches;
        private static int diagnostics;
        private static bool ownerOperation;
        private static readonly HashSet<string> caretDiagnostics = new HashSet<string>();
        private sealed class GroupState
        {
            internal UIOperationGroup Group;
            internal bool Enabled, AutoSelect, ActiveOperation;
        }
        private static readonly List<GroupState> groups = new List<GroupState>();

        public static void Attach(UIBase window, InputField field)
        {
            Detach();
            owner = window;
            input = field;
            diagnostics = 0;
            caretDiagnostics.Clear();
            ownerOperation = window.isActiveOperation;
            patches = new Harmony("MOD_YtxSQ1.WindowInputScope");
            try
            {
                window.isActiveOperation = false;
                // Build先创建搜索框，Attach的Detach会清空早期登记；仅重新登记本视图的输入。
                foreach (var ownedInput in window.GetComponentsInChildren<InputField>(true))
                    if (ownedInput.gameObject.name.StartsWith("BHML_", StringComparison.Ordinal)) Register(ownedInput);
                foreach (var group in window.GetComponentsInChildren<UIOperationGroup>(true)) DisableGroup(group);
                // 原窗口缓存也可能持有不在当前子树中的组。
                if (window.operationGroups != null)
                    foreach (var group in window.operationGroups) DisableGroup(group);
                // 仅在本输入框编辑期间拒绝选择已停用的控件，其他选择仍放行。
                patches.Patch(AccessTools.Method(typeof(EventSystem), "SetSelectedGameObject",
                    new[] { typeof(GameObject), typeof(BaseEventData) }),
                    prefix: new HarmonyMethod(typeof(WindowInputScope), nameof(AllowSelection)));
                patches.Patch(AccessTools.Method(typeof(InputField), "DeactivateInputField"),
                    prefix: new HarmonyMethod(typeof(WindowInputScope), nameof(ObserveDeactivation)));
                patches.Patch(AccessTools.Method(typeof(InputField), "GenerateCaret",
                    new[] { typeof(VertexHelper), typeof(Vector2) }),
                    postfix: new HarmonyMethod(typeof(WindowInputScope), nameof(AlignCaret)));
                Debug.Log("[八荒秘录] 07.10已停用原窗口导航组=" + groups.Count + "；无效焦点目标保护已安装");
            }
            catch { Detach(); throw; }
        }

        private static bool IsSelected()
        {
            return SelectedInput() != null;
        }
        private static InputField SelectedInput()
        {
            if (IsActive(input)) return input;
            foreach (var field in editors) if (IsActive(field)) return field;
            return null;
        }
        private static bool IsActive(InputField field)
        {
            return field != null && field.gameObject.activeInHierarchy &&
                (field.isFocused || (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == field.gameObject));
        }

        private static void DisableGroup(UIOperationGroup group)
        {
            if (group == null || groups.Exists(state => state.Group == group)) return;
            groups.Add(new GroupState { Group = group, Enabled = group.enabled,
                AutoSelect = group.isAutoSelect, ActiveOperation = group.m_isActiveOperation });
            group.enabled = false;
            group.isAutoSelect = false;
            group.m_isActiveOperation = false;
            Debug.Log("[八荒秘录] 停用承载导航组：" + group.name);
        }

        private static bool AllowSelection(GameObject __0)
        {
            var selected = SelectedInput();
            if (selected == null || __0 == selected.gameObject) return true;
            if (__0 == null)
            {
                Trace("搜索焦点正常清空");
                return true;
            }
            var target = __0.GetComponent<Selectable>();
            var invalid = !__0.activeInHierarchy ||
                (target != null && (!target.enabled || !target.IsInteractable()));
            if (diagnostics < 4)
            {
                var path = __0.name;
                var parent = __0.transform.parent;
                for (int depth = 0; parent != null && depth < 12; depth++, parent = parent.parent)
                    path = parent.name + "/" + path;
                Trace((invalid ? "已阻止无效焦点转移=" : "放行焦点转移=") + path +
                    "；active=" + __0.activeInHierarchy + "；可交互=" +
                    (target == null ? "非Selectable" : target.IsInteractable().ToString()));
            }
            return !invalid;
        }

        private static void ObserveDeactivation(InputField __instance)
        {
            if (!Owns(__instance) || !IsSelected()) return;
            Trace("搜索DeactivateInputField");
        }

        private static void AlignCaret(InputField __instance, VertexHelper __0, Vector2 __1)
        {
            // 只改本搜索框的光标四边形，不改输入、闪烁、选区和焦点状态。
            if (!Owns(__instance)) return;
            if (__0.currentVertCount != 4) return;
            // 局部量命名为field：早期这里叫input，会遮蔽同名静态字段，容易误用。
            var field = __instance;
            var text = field.textComponent;
            if (text == null) return;
            // 克隆模板后Caret可能保留旧矩形。先同步矩形，再在同一局部坐标系对齐。
            field.AssignPositioningIfNeeded();
            var caretRect = field.caretRectTrans;
            var visual = text;
            if (string.IsNullOrEmpty(field.text) && field.placeholder != null)
            {
                var placeholder = field.placeholder.TryCast<Text>();
                if (placeholder != null) visual = placeholder;
            }
            float bottom, top;
            if (!TryGetTextBounds(visual, out bottom, out top))
            {
                var center = text.rectTransform.rect.center.y;
                bottom = center - text.fontSize * .5f; top = center + text.fontSize * .5f;
            }
            // 只移动光标：让中心跟实际文字对齐，保留原生高度、宽度和闪烁。
            var vertex = new UIVertex();
            __0.PopulateUIVertex(ref vertex, 0);
            var caretBottom = vertex.position.y;
            __0.PopulateUIVertex(ref vertex, 2);
            var caretTop = vertex.position.y;
            var centerY = (bottom + top) * .5f + __1.y;
            if (caretRect != null)
                centerY = caretRect.InverseTransformPoint(visual.rectTransform.TransformPoint(new Vector3(0, centerY, 0))).y;
            var shift = centerY - (caretBottom + caretTop) * .5f;
            var diagnosticKey = field.gameObject.name + (string.IsNullOrEmpty(field.text) ? ":空" : ":输入");
            if (caretDiagnostics.Add(diagnosticKey))
                Debug.Log("[八荒秘录] 07.31光标：" + diagnosticKey + "；字形=" + bottom + "/" + top +
                    "；原光标=" + caretBottom + "/" + caretTop + "；位移=" + shift +
                    "；文字位置=" + visual.rectTransform.localPosition + "；光标位置=" +
                    (caretRect == null ? "未生成" : caretRect.localPosition.ToString()));
            for (int i = 0; i < 4; i++)
            {
                __0.PopulateUIVertex(ref vertex, i);
                var position = vertex.position;
                position.y += shift;
                vertex.position = position;
                __0.SetUIVertex(vertex, i);
            }
        }

        // 取实际字形（空文本时取占位文字）的上下边界。
        // 忽略空格、零宽字符以及TextGenerator末尾的空四边形；无有效字形时返回false由调用方回退。
        private static bool TryGetTextBounds(Text visual, out float bottom, out float top)
        {
            bottom = float.MaxValue; top = float.MinValue;
            var generator = visual.cachedTextGenerator;
            var vertices = generator.verts;
            var units = 1f / visual.pixelsPerUnit;
            for (int i = 0; i + 3 < generator.vertexCount; i += 4)
            {
                float low = float.MaxValue, high = float.MinValue;
                for (int j = 0; j < 4; j++)
                {
                    var y = vertices[i + j].position.y * units;
                    low = Math.Min(low, y); high = Math.Max(high, y);
                }
                if (high - low < .01f) continue;
                bottom = Math.Min(bottom, low); top = Math.Max(top, high);
            }
            return bottom != float.MaxValue;
        }

        private static void Trace(string reason)
        {
            if (diagnostics++ >= 4) return;
            string trace;
            try { trace = Il2CppSystem.Environment.StackTrace; }
            catch (Exception ex) { trace = "原生栈读取失败：" + ex.Message; }
            Debug.Log("[八荒秘录] " + reason + "；游戏调用栈=" + trace);
        }

        public static void Detach()
        {
            var previousOwner = owner;
            owner = null;
            input = null;
            editors.Clear();
            caretDiagnostics.Clear();
            var previous = patches;
            patches = null;
            try { if (previous != null) previous.UnpatchSelf(); }
            finally
            {
                foreach (var state in groups)
                {
                    try
                    {
                        if (state.Group == null) continue;
                        state.Group.isAutoSelect = state.AutoSelect;
                        state.Group.m_isActiveOperation = state.ActiveOperation;
                        state.Group.enabled = state.Enabled;
                    }
                    catch (Exception ex) { Debug.LogWarning("[八荒秘录] 恢复导航组失败：" + ex.Message); }
                }
                groups.Clear();
                if (previousOwner != null) previousOwner.isActiveOperation = ownerOperation;
            }
        }
    }
}

