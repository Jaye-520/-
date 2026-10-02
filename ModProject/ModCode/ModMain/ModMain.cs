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

        public void Init()
        {
            Destroy();
            updateFailed = false;
            controller = new WorkshopController();
            try
            {
                updateTimer = g.timer.Frame(new Action(OnUpdate), 1, true);
                Debug.Log("[八荒秘录] 界面样板已初始化；大地图按 F8 打开秘录。");
            }
            catch { Destroy(); throw; }
        }

        public void Destroy()
        {
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
        }

        private void OnUpdate()
        {
            if (controller == null || updateFailed) return;
            try { controller.Update(); }
            catch (Exception ex)
            {
                // 熔断更新，避免异常每帧刷屏；重进存档可重新初始化。
                updateFailed = true;
                Debug.LogError("[八荒秘录] 已停止施工版更新：" + ex);
                Destroy();
            }
        }
    }
}
