using System;
using MOD_YtxSQ1.Core;
using UnityEngine;

namespace MOD_YtxSQ1
{
    public class ModMain
    {
        private TimerCoroutine updateTimer;
        private WorkshopController controller;
        private bool updateFailed;
        private MOD_YtxSQ1.UI.WorkshopEntrances entrances;

        public void Init()
        {
            var initTimer = System.Diagnostics.Stopwatch.StartNew();
            Destroy();
            updateFailed = false;
            controller = new WorkshopController();
            entrances = new MOD_YtxSQ1.UI.WorkshopEntrances(OpenFromEntrance);
            try
            {
                MOD_YtxSQ1.Services.CapacityRuntime.Initialize();
                updateTimer = g.timer.Frame(new Action(OnUpdate), 1, true);
                Debug.Log("[八荒秘录] " + ModVersion.Current + "已初始化；点击主界面或NPC面板玉简打开秘录，F8备用。");
                Debug.Log("[八荒秘录] 初始化CPU=" + initTimer.Elapsed.TotalMilliseconds.ToString("F2") + "ms");
            }
            catch { Destroy(); throw; }
        }

        public void Destroy()
        {
            updateFailed = false;
            try { MOD_YtxSQ1.Services.CapacityRuntime.Dispose(); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 释放扩容运行效果失败：" + ex.Message); }
            try { MOD_YtxSQ1.Services.FeatureRuntime.Dispose(); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 释放功能辅助失败：" + ex.Message); }
            var oldEntrances = entrances; entrances = null;
            if (oldEntrances != null)
                try { oldEntrances.Dispose(); }
                catch (Exception ex) { Debug.LogWarning("[八荒秘录] 释放入口失败：" + ex.Message); }
            if (updateTimer != null)
            {
                try { g.timer.Stop(updateTimer); }
                catch (Exception ex) { Debug.LogWarning("[八荒秘录] 停止计时器失败：" + ex.Message); }
                updateTimer = null;
            }
            var previous = controller;
            controller = null;
            if (previous != null)
            {
                try { previous.Dispose(); }
                catch (Exception ex) { Debug.LogWarning("[八荒秘录] 释放控制器失败：" + ex); }
            }
            MOD_YtxSQ1.UI.PaperSkin.ClearCache();
            MOD_YtxSQ1.Services.WorkshopPerformance.Report();
            MOD_YtxSQ1.Services.WorkshopPerformance.Clear();
        }

        private void OnUpdate()
        {
            using (MOD_YtxSQ1.Services.WorkshopPerformance.Measure("模组帧CPU")) OnUpdateCore();
        }

        private void OnUpdateCore()
        {
            try { MOD_YtxSQ1.Services.CapacityRuntime.Tick(); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 扩容恢复未完成：" + ex.Message); }
            try { MOD_YtxSQ1.Services.FeatureRuntime.Tick(); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 功能辅助更新已停止：" + ex.Message);
                try { MOD_YtxSQ1.Services.FeatureRuntime.Dispose(); } catch (Exception release) { Debug.LogWarning("[八荒秘录] 辅助释放失败：" + release.Message); } }
            if (entrances != null) entrances.Update();
            if (updateFailed && !Input.GetKeyDown(KeyCode.F8)) return;
            if (controller == null && !updateFailed) return;
            try
            {
                if (updateFailed)
                {
                    controller = new WorkshopController();
                    updateFailed = false;
                    Debug.Log("[八荒秘录] F8重新创建控制器，重试打开；不会重放人物修改。");
                }
                controller.Update();
            }
            catch (Exception ex)
            {
                // 熔断后仅等待F8人工重试，保留帧入口，不每帧重建或重放写入。
                updateFailed = true;
                MOD_YtxSQ1.Services.FeatureRuntime.Dispose();
                Debug.LogError("[八荒秘录] 已暂停本模组更新，可按F8重试：" + ex);
                var failed = controller; controller = null;
                if (failed != null)
                    try { failed.Dispose(); }
                    catch (Exception disposeError) { Debug.LogWarning("[八荒秘录] 异常控制器释放失败：" + disposeError.Message); }
                MOD_YtxSQ1.UI.PaperSkin.ClearCache();
            }
        }

        private void OpenFromEntrance(string unitId)
        {
            if (controller == null)
            {
                controller = new WorkshopController();
                updateFailed = false;
                Debug.Log("[八荒秘录] 玉简入口重建控制器；不会重放人物修改。");
            }
            controller.OpenFromEntrance(unitId);
        }
    }
}
