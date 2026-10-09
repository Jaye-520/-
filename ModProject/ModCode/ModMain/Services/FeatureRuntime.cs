using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnhollowerRuntimeLib;
using UnityEngine.EventSystems;

namespace MOD_YtxSQ1.Services
{
    // All state belongs to this loaded world. No configuration, save record or skill definition is modified.
    internal static class FeatureRuntime
    {
        private static readonly HashSet<string> enabled = new HashSet<string>(StringComparer.Ordinal);
        private static Harmony patches;
        private static IntPtr worldPointer, dataPointer, playerPointer;
        private static DynInt.DynObjectEqualHandler zeroCooldown;
        private sealed class CooldownScope { internal DynInt Field; internal DynInt.DynObjectEqualHandler Handler; }
        private static string fault;
        private static UIMinMap mapView;
        private static bool maskOwned, maskWasEnabled, mapPress;
        private static Vector3 mapPressPosition;
        private static IntPtr mapPressOwner;
        private sealed class RelationScope { internal UnitActionFeedback104 Action; internal bool SpecialPassed, ScoreRead; internal RelationScope Previous; }
        private static RelationScope relationScope;
        private sealed class GridVision { internal GameObject Mask, View; internal bool MaskActive, ViewActive; }
        private static readonly Dictionary<IntPtr, GridVision> gridVision = new Dictionary<IntPtr, GridVision>();
        internal static string Error { get { ResetExpiredFault(); return fault; } }
        private static void ResetExpiredFault()
        { if (fault != null && (g.world == null || g.data == null || !g.world.isIntoWorld || g.world.Pointer != worldPointer || g.data.Pointer != dataPointer)) Dispose(); }
        internal static bool Enabled(string id) { return Current() && enabled.Contains(id); }
        private static bool Current()
        { return patches != null && !g.isDestroy && g.world != null && !g.world.isDestroy && g.world.isIntoWorld
            && g.data != null && g.world.Pointer == worldPointer && g.data.Pointer == dataPointer
            && g.world.playerUnit != null && g.world.playerUnit.Pointer == playerPointer; }
        internal static void BindTitle() { if (!Current()) { Dispose(); Attach(); } }
        internal static void Toggle(string id)
        {
            var definition = WorkshopFeatures.Find(id);
            if (definition == null || !definition.Toggle || definition.Unavailable != null) throw new InvalidOperationException("此开关尚无可靠原生路径");
            if (!Current()) Dispose();
            if (CharacterReader.Resolve(null) == null || g.data == null) throw new InvalidOperationException("玩家或存档尚未就绪");
            if (patches == null) Attach();
            if (!enabled.Remove(id)) enabled.Add(id);
            if (id == "vision") {
                if (mapView != null) ApplyMapVision(mapView);
                if (!enabled.Contains("vision")) RestoreGridVision();
                else if (SceneType.map != null) foreach (var grid in SceneType.map.world.showGrids) if (grid.Value?.gridUI != null) GridUpdated(grid.Value.gridUI);
            }
            if (id == "move") mapPress = false;
        }
        private static void Attach()
        {
            fault = null;
            var installed = new Harmony("MOD_YtxSQ1.Features"); patches = installed;
            worldPointer = g.world.Pointer; dataPointer = g.data.Pointer; playerPointer = g.world.playerUnit.Pointer;
            try
            {
                Patch(AccessTools.Method(typeof(UITaoistNameItem), "InitData"), nameof(TitleDisplay), false);
                Patch(AccessTools.Method(typeof(UITaoistNameItem), "ShowNoAppellation"), nameof(TitleDisplay), false);
                // IsOpenGrid is called once per fog-texture pixel: never detour that hot loop.
                Patch(AccessTools.Method(typeof(UIMinMap), "UpdateUI"), nameof(MapUpdated), false);
                Patch(AccessTools.Method(typeof(UIMinMap), "OnUpdate"), nameof(MapInput), false);
                Patch(AccessTools.Method(typeof(GridBaseUI), "UpdateUI"), nameof(GridReleased), true);
                Patch(AccessTools.Method(typeof(GridBaseUI), "UpdateUI"), nameof(GridUpdated), false);
                Patch(AccessTools.Method(typeof(GridBaseUI), "Destroy"), nameof(GridReleased), true);
                Patch(AccessTools.Method(typeof(GridBaseUI), "HideGrid"), nameof(GridReleased), true);
                Patch(AccessTools.Method(typeof(GridBaseUI), "ShowGrid"), nameof(GridUpdated), false);
                Patch(AccessTools.Method(typeof(GridBaseUI), "InitData"), nameof(GridReleased), true);
                Patch(AccessTools.Method(typeof(UnitActionFeedbackBase), "DramaPackageFeedback"), nameof(RelationPackageFeedback), true);
                Patch(AccessTools.PropertyGetter(typeof(UnitActionFeedbackBase), "feedbackLog"), nameof(RelationDecision), true);
                Patch(AccessTools.Method(typeof(UnitActionFeedbackBase), "SpecialFeedback"), nameof(RelationSpecialChecked), false);
                Patch(AccessTools.Method(typeof(DataUnit.RelationData), "GetIntim", new[] { typeof(WorldUnitBase) }), nameof(RelationScoreRead), false);
                var relationCreate = AccessTools.Method(typeof(UnitActionFeedback104), "OnCreate");
                if (relationCreate == null) throw new MissingMethodException("原生关系接受流程");
                patches.Patch(relationCreate, prefix: new HarmonyMethod(typeof(FeatureRuntime), nameof(RelationStart)), finalizer: new HarmonyMethod(typeof(FeatureRuntime), nameof(RelationFinished)));
                Patch(AccessTools.PropertyGetter(typeof(SkillDataAttack), "cdTime"), nameof(SkillCooldown), false);
                Patch(AccessTools.PropertyGetter(typeof(StepDataBase), "cdTime"), nameof(StepCooldown), false);
                Patch(AccessTools.Method(typeof(UnitDataBase), "AddMP"), nameof(KeepMP), true);
                Patch(AccessTools.Method(typeof(UnitDataBase), "AddSP"), nameof(KeepSP), true);
                Patch(AccessTools.Method(typeof(UnitDataBase), "AddHP"), nameof(KeepHP), true);
                Patch(AccessTools.Method(typeof(UnitActionRoleStealItem), "PlayerToNPCAction"), nameof(StealResult), true);
                Patch(AccessTools.PropertySetter(typeof(DynInt), "baseValue"), nameof(KeepWorldResource), true);
                zeroCooldown = DelegateSupport.ConvertDelegate<DynInt.DynObjectEqualHandler>(new Func<int>(() => 0));
                var attackCheck = AccessTools.Method(typeof(SkillAttack), "IsCreate");
                var stepCheck = AccessTools.Method(typeof(StepBase), "IsCreate");
                if (attackCheck == null || stepCheck == null) throw new MissingMethodException("实际技能释放入口");
                patches.Patch(attackCheck, prefix: new HarmonyMethod(typeof(FeatureRuntime), nameof(AttackCheck)), finalizer: new HarmonyMethod(typeof(FeatureRuntime), nameof(CooldownChecked)));
                patches.Patch(stepCheck, prefix: new HarmonyMethod(typeof(FeatureRuntime), nameof(StepCheck)), finalizer: new HarmonyMethod(typeof(FeatureRuntime), nameof(CooldownChecked)));
                var study = AccessTools.Method(typeof(UnitActionPropMartialStudy), "OnCreate");
                if (study == null) throw new MissingMethodException("原生学习完成入口");
                patches.Patch(study, prefix: new HarmonyMethod(typeof(FeatureRuntime), nameof(StudyStart)),
                    postfix: new HarmonyMethod(typeof(FeatureRuntime), nameof(StudyFinished)));
                var escape = AccessTools.Method(typeof(UnitActionRoleEscape), "Init");
                if (escape == null) throw new MissingMethodException("玩家逃跑初始化入口");
                patches.Patch(escape, prefix: new HarmonyMethod(typeof(FeatureRuntime), nameof(EscapeInit)),
                    postfix: new HarmonyMethod(typeof(FeatureRuntime), nameof(EscapeInitialized)));
            }
            catch (Exception ex)
            { var failedWorld = worldPointer; var failedData = dataPointer; var failedPlayer = playerPointer;
                Dispose(); worldPointer = failedWorld; dataPointer = failedData; playerPointer = failedPlayer;
                fault = "辅助补丁挂载失败，全部关闭：" + ex.Message; throw new InvalidOperationException(fault, ex); }
        }
        private static void Patch(MethodInfo original, string callback, bool prefix)
        {
            if (original == null) throw new MissingMethodException(callback);
            var hook = new HarmonyMethod(typeof(FeatureRuntime), callback);
            patches.Patch(original, prefix: prefix ? hook : null, postfix: prefix ? null : hook);
        }
        private static void TitleDisplay(UITaoistNameItem __instance)
        {
            try { if (!Current() || __instance.unit == null || __instance.unit.Pointer != playerPointer) return;
                var title = FeatureOperations.PlayerTitle(); if (string.IsNullOrEmpty(title) || __instance.textName == null) return;
                __instance.gameObject.SetActive(true); __instance.textName.text = title; __instance.textName.gameObject.SetActive(true); }
            catch (Exception ex) { Fail(ex); }
        }
        private static bool IsPlayer(UnitCtrlBase unit)
        {
            if (!Current() || SceneType.battle == null || SceneType.battle.battleMap == null || unit == null) return false;
            var current = SceneType.battle.battleMap.playerUnitCtrl;
            return current != null && unit.Pointer == current.Pointer && !unit.mIsDie;
        }
        private static void Fail(Exception ex)
        { enabled.Clear(); fault = "辅助已关闭，请重新进入存档后核对：" + ex.GetType().Name + " · " + ex.Message; }
        private static void MapUpdated(UIMinMap __instance)
        {
            try { if (!Current()) return; if (mapView == null || mapView.Pointer != __instance.Pointer) { RestoreMapVision(); mapView = __instance; } ApplyMapVision(__instance); }
            catch (Exception ex) { Fail(ex); }
        }
        private static void ApplyMapVision(UIMinMap view)
        {
            var mask = view.imgMask; if (mask == null) return;
            if (Enabled("vision")) { if (!maskOwned) { maskWasEnabled = mask.enabled; maskOwned = true; } mask.enabled = false; }
            else RestoreMapVision();
        }
        private static void RestoreMapVision()
        { if (maskOwned && mapView != null && mapView.imgMask != null) mapView.imgMask.enabled = maskWasEnabled; maskOwned = false; }
        private static void GridUpdated(GridBaseUI __instance)
        {
            try {
                if (!Enabled("vision") || gridVision.ContainsKey(__instance.Pointer)) return;
                // These getters create missing UI. Never create fog on an already explored/visible tile.
                var state = new GridVision { Mask = __instance.isOpen ? null : __instance.goMask, View = __instance.isView ? null : __instance.goMaskView };
                state.MaskActive = state.Mask != null && state.Mask.activeSelf;
                state.ViewActive = state.View != null && state.View.activeSelf;
                gridVision.Add(__instance.Pointer, state);
                if (state.Mask != null) state.Mask.SetActive(false);
                if (state.View != null) state.View.SetActive(false);
            } catch (Exception ex) { Fail(ex); }
        }
        private static void RestoreGrid(GridVision state)
        {
            if (state.Mask != null) state.Mask.SetActive(state.MaskActive);
            if (state.View != null) state.View.SetActive(state.ViewActive);
        }
        private static void GridReleased(GridBaseUI __instance)
        {
            GridVision state;
            if (!gridVision.TryGetValue(__instance.Pointer, out state)) return;
            gridVision.Remove(__instance.Pointer);
            try { RestoreGrid(state); } catch (Exception ex) { Fail(ex); }
        }
        private static void RestoreGridVision()
        {
            foreach (var state in gridVision.Values) try { RestoreGrid(state); } catch (Exception ex) { Debug.LogWarning("[八荒秘录] 地图迷雾恢复：" + ex.Message); }
            gridVision.Clear();
        }
        private static bool RelationPackageFeedback(UnitActionFeedbackBase __instance, ref int __result)
        {
            // Package feedback installs refusal logs, jumpDrama and failure/success drama IDs.
            // Skip it before those side effects; continue SpecialFeedback's other checks and 104's conflict/score flow.
            if (relationScope?.Action == null || relationScope.Action.Pointer != __instance.Pointer || !Enabled("relation")) return true;
            __result = 0; return false;
        }
        private static void MapInput(UIMinMap __instance)
        {
            if (!enabled.Contains("move")) return;
            try
            {
                if (!Current() || SceneType.map == null || __instance.imgBG == null || EventSystem.current == null) { mapPress = false; return; }
                if (Input.GetMouseButtonDown(0)) { mapPressPosition = Input.mousePosition; mapPressOwner = __instance.Pointer; mapPress = IsMapHit(__instance); }
                if (!Input.GetMouseButtonUp(0)) return;
                bool click = mapPress && mapPressOwner == __instance.Pointer && !__instance.isDrag && Vector3.Distance(mapPressPosition, Input.mousePosition) <= 5f;
                mapPress = false; if (!click) return;
                // Native raycast order prevents clicks on filters, overlays or other windows from moving the player.
                if (!IsMapHit(__instance)) return;
                var rect = __instance.imgBG.rectTransform; Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, g.ui.uiCamera, out local) || !rect.rect.Contains(local)) return;
                // Same normalized coordinates and rounding as UIMinMap.OnUpdate; independent of pan/zoom.
                var point = new Vector2Int(Mathf.RoundToInt((local.x - rect.rect.xMin) / rect.rect.width * (g.data.grid.mapWidth - 1)),
                    Mathf.RoundToInt((local.y - rect.rect.yMin) / rect.rect.height * (g.data.grid.mapHeight - 1)));
                MoveOnMap(point);
            } catch (Exception ex) { Debug.LogWarning("[八荒秘录] 地图点选传送未完成：" + ex); }
        }
        private static bool IsMapHit(UIMinMap view)
        {
            var hits = new Il2CppSystem.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Input.mousePosition }, hits);
            if (hits.Count == 0 || hits[0].gameObject == null) return false;
            var hit = hits[0].gameObject.transform;
            return hit.IsChildOf(view.imgBG.transform) || view.imgMask != null && hit.IsChildOf(view.imgMask.transform);
        }
        internal static void MoveOnMap(Vector2Int point)
        {
            if (!Enabled("move") || SceneType.map == null || FeatureOperations.Busy || CharacterSave.Busy || DataMgr.isSaveData) return;
            var player = CharacterReader.Resolve(null); if (player == null || player.data.unitData.GetPoint() == point) return;
            if (g.data.grid.GetGridData(point) == null) throw new InvalidOperationException("地图点击位置不存在");
            var world = SceneType.map.world; var pointer = world.Pointer;
            world.StopPlayerMove();
            var request = FeatureOperations.Prepare("move", player.data.unitData.unitID, point.x + "," + point.y);
            FeatureOperations.Execute(request); FeatureOperations.Ensure(request, false);
            if (SceneType.map == null || SceneType.map.world.Pointer != pointer) throw new InvalidOperationException("移动后地图场景已变化，已保留待保存状态");
            // SetPlayerPosi updates the map model, camera, local fog and selection, not the unit record.
            world.SetPlayerPosi(point);
        }
        private static void RelationStart(UnitActionFeedback104 __instance, out RelationScope __state)
        {
            __state = new RelationScope { Previous = relationScope };
            relationScope = __state;
            try { if (Enabled("relation") && !__instance.schoolAdmin && __instance.requestUnit != null && __instance.requestUnit.Pointer == playerPointer) __state.Action = __instance; }
            catch (Exception ex) { Fail(ex); }
        }
        private static void RelationSpecialChecked(UnitActionFeedbackBase __instance, int __result)
        { if (relationScope?.Action != null && relationScope.Action.Pointer == __instance.Pointer) relationScope.SpecialPassed = __result == 0; }
        private static void RelationScoreRead(DataUnit.RelationData __instance, WorldUnitBase __0)
        {
            var scope = relationScope;
            // This first score read is AFTER SpecialFeedback and the monogamy conflict branch.
            if (scope?.Action == null || !scope.SpecialPassed || !Current() || scope.Action.state != 0 || __0 == null || __0.Pointer != playerPointer) return;
            try { if (scope.Action.unit?.data.unitData.relationData?.Pointer == __instance.Pointer) scope.ScoreRead = true; }
            catch (Exception ex) { Fail(ex); }
        }
        private static Exception RelationFinished(RelationScope __state, Exception __exception)
        { if (relationScope == __state) relationScope = __state.Previous; return __exception; }
        private static void RelationDecision(UnitActionFeedbackBase __instance)
        {
            try
            {
                if (relationScope?.Action == null || !relationScope.SpecialPassed || !relationScope.ScoreRead || relationScope.Action.Pointer != __instance.Pointer || !Enabled("relation")) return;
                var decision = __instance.TryCast<UnitActionFeedback104>();
                // Native 104 writes the acceptance score into state, then reads feedbackLog before
                // displaying the result and invoking the parent's relation callback. Touch only that decision.
                if (decision != null && !decision.schoolAdmin && decision.requestUnit != null && decision.requestUnit.Pointer == playerPointer && decision.state == 2)
                    decision.state = 1;
            } catch (Exception ex) { Fail(ex); }
        }
        private static void SkillCooldown(SkillDataAttack __instance, ref float __result)
        { try { if (Enabled("cooldown") && __instance.skill != null && IsPlayer(__instance.skill.unit)) __result = 0; } catch (Exception ex) { Fail(ex); } }
        private static void StepCooldown(StepDataBase __instance, ref float __result)
        { try { if (Enabled("cooldown") && __instance.step != null && IsPlayer(__instance.step.unit)) __result = 0; } catch (Exception ex) { Fail(ex); } }
        private static void KeepMP(UnitDataBase __instance, ref int __0)
        { try { if (__0 < 0 && Enabled("mp") && IsPlayer(__instance.unit)) __0 = 0; } catch (Exception ex) { Fail(ex); } }
        private static void KeepSP(UnitDataBase __instance, ref int __0)
        { try { if (__0 < 0 && Enabled("sp") && IsPlayer(__instance.unit)) __0 = 0; } catch (Exception ex) { Fail(ex); } }
        private static bool KeepHP(UnitDataBase __instance, MartialTool.HitData __0)
        {
            try { return __0 == null || __0.hitValue >= 0 || !(Enabled("hp") || Enabled("invincible")) || !IsPlayer(__instance.unit); }
            catch (Exception ex) { Fail(ex); return true; }
        }
        private static void StealResult(UnitActionRoleStealItem __instance)
        { try { if (Enabled("steal") && __instance.unit != null && __instance.unit.Pointer == playerPointer) { __instance.isStealComplete = true; __instance.isStealDiscover = false; } } catch (Exception ex) { Fail(ex); } }
        private static void KeepWorldResource(DynInt __instance, ref int __0)
        {
            try
            {
                if (!Current()) return;
                var data = CharacterReader.Resolve(null)?.data.dynUnitData;
                if (data == null || __0 >= __instance.baseValue) return;
                if ((Enabled("hp") && (__instance.Pointer == data.hp.Pointer || __instance.Pointer == data.energy.Pointer))
                    || (Enabled("mp") && __instance.Pointer == data.mp.Pointer) || (Enabled("sp") && __instance.Pointer == data.sp.Pointer))
                    __0 = __instance.baseValue;
            } catch (Exception ex) { Fail(ex); }
        }
        private static CooldownScope BeginCooldown(DynInt field)
        {
            if (field == null || field.lockValue || field.allEqualCall.Count > 0) throw new InvalidOperationException("运行冷却被锁定或已有固定覆盖；已停止辅助，请核对其他效果");
            var state = new CooldownScope { Field = field, Handler = zeroCooldown };
            field.AddEqual(state.Handler, "八荒秘录临时冷却"); return state;
        }
        private static void AttackCheck(SkillAttack __instance, out CooldownScope __state)
        { __state = null; try { if (Enabled("cooldown") && IsPlayer(__instance.unit)) __state = BeginCooldown(__instance.data.cd); } catch (Exception ex) { Fail(ex); } }
        private static void StepCheck(StepBase __instance, out CooldownScope __state)
        { __state = null; try { if (Enabled("cooldown") && IsPlayer(__instance.unit)) __state = BeginCooldown(__instance.data.cd); } catch (Exception ex) { Fail(ex); } }
        private static Exception CooldownChecked(CooldownScope __state, Exception __exception)
        { try { if (__state != null) __state.Field.DelEqual(__state.Handler); } catch (Exception ex) { Fail(ex); } return __exception; }
        // Native Init uses isSureComplete only to choose state=1 after its probability roll.
        // Restore the instance option afterwards; IsCreate and the subsequent native escape flow remain intact.
        private static void EscapeInit(UnitActionRoleEscape __instance, WorldUnitBase __0, out bool __state)
        {
            __state = __instance.isSureComplete;
            try { if (Enabled("escape") && __0 != null && __0.Pointer == playerPointer) __instance.isSureComplete = true; }
            catch (Exception ex) { Fail(ex); }
        }
        private static void EscapeInitialized(UnitActionRoleEscape __instance, bool __state)
        { try { __instance.isSureComplete = __state; } catch (Exception ex) { Fail(ex); } }
        private static void StudyStart(UnitActionPropMartialStudy __instance, out bool __state)
        {
            __state = __instance.isOneStudy;
            try { if (Enabled("study") && __instance.unit != null && __instance.unit.Pointer == playerPointer) __instance.isOneStudy = true; }
            catch (Exception ex) { Fail(ex); }
        }
        private static void StudyFinished(UnitActionPropMartialStudy __instance, bool __state)
        { try { __instance.isOneStudy = __state; } catch (Exception ex) { Fail(ex); } }
        internal static void Tick()
        {
            ResetExpiredFault();
            if (patches == null && fault == null && CharacterReader.ContextKey() != null && !string.IsNullOrEmpty(FeatureOperations.PlayerTitle())) Attach();
            if (patches == null) return;
            if (!Current()) { Dispose(); return; }
            if (fault != null) { RestoreMapVision(); RestoreGridVision(); mapPress = false; var previous = patches; patches = null; enabled.Clear(); if (previous != null) previous.UnpatchSelf(); Debug.LogWarning("[八荒秘录] " + fault); return; }
            // No refill or forced resource assignment. Native losses are intercepted only while enabled.
        }
        internal static void Dispose()
        {
            RestoreMapVision(); RestoreGridVision(); mapView = null; mapPress = false;
            enabled.Clear(); worldPointer = dataPointer = playerPointer = IntPtr.Zero; zeroCooldown = null; relationScope = null;
            var previous = patches; patches = null;
            if (previous != null) previous.UnpatchSelf();
            fault = null;
        }
    }
}
