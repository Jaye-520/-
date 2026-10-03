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
            Destroy();
            updateFailed = false;
            controller = new WorkshopController();
            entrances = new MOD_YtxSQ1.UI.WorkshopEntrances(OpenFromEntrance);
            try
            {
                updateTimer = g.timer.Frame(new Action(OnUpdate), 1, true);
                Debug.Log("[八荒秘录] 07.41已初始化；点击主界面或NPC面板玉简打开秘录，F8备用。");
            }
            catch { Destroy(); throw; }
        }

        public void Destroy()
        {
            updateFailed = false;
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
        }

        private void OnUpdate()
        {
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
