// 故障注入：编译真实 WorkshopWindow/ModMain，替代游戏和视图依赖。
// 验证托管生命周期控制流，不模拟 Unity 渲染、IL2CPP 或真实游戏场景。
using System;
using System.Reflection;
using MOD_YtxSQ1;
using MOD_YtxSQ1.UI;

namespace UnityEngine
{
    public static class Debug
    {
        public static int Errors;
        public static void Log(string value) { }
        public static void LogWarning(string value) { }
        public static void LogError(string value) { Errors++; }
    }
}
public sealed class GameObject { public bool activeInHierarchy = true; }
public sealed class UITextInfoLong
{
    public GameObject gameObject = new GameObject();
    public int InitCount;
    public void InitData(string title, string info, string button) { InitCount++; }
    public void InitInputField() { }
}
public enum UIType { TextInfoLong }
public static class UITipItem { public static void AddTip(string value) { } }
public sealed class TimerCoroutine { }
public sealed class FakeTimer
{
    public Action Callback;
    public int Stops;
    public bool ThrowStop;
    public TimerCoroutine Frame(Action action, int frames, bool loop) { Callback = action; return new TimerCoroutine(); }
    public void Stop(TimerCoroutine timer) { Stops++; if (ThrowStop) throw new Exception("stop failure"); }
}
public sealed class FakeUI
{
    public UITextInfoLong Current;
    public int Closes;
    public UITextInfoLong GetUI(UIType type) { return Current; }
    public T OpenUI<T>(UIType type) where T : class { Current = new UITextInfoLong(); return Current as T; }
    public void CloseUI(UITextInfoLong window, bool animated) { Closes++; Current = null; }
}
public static class g
{
    public static FakeUI ui = new FakeUI();
    public static FakeTimer timer = new FakeTimer();
}
namespace MOD_YtxSQ1.UI
{
    internal static class WindowInputScope
    {
        public static bool Active;
        public static void Attach(UITextInfoLong window, object input) { Active=true; }
        public static void Detach() { Active=false; }
    }
    internal sealed class BookView
    {
        public object SearchInput { get { return null; } }
        public static bool ThrowBuild;
        public static bool ThrowDispose;
        public static int Disposals;
        public void Build(UITextInfoLong window, Action close) { if (ThrowBuild) throw new Exception("build failure"); }
        public void RefreshCharacter(bool force = false) { }
        public void Dispose() { Disposals++; if (ThrowDispose) throw new Exception("dispose failure"); }
    }
}
namespace MOD_YtxSQ1.Core
{
    internal sealed class WorkshopController
    {
        public static bool ThrowUpdate;
        public static bool ThrowDispose;
        public static int Disposals;
        public void Update() { if (ThrowUpdate) throw new Exception("update failure"); }
        public void Dispose() { Disposals++; if (ThrowDispose) throw new Exception("controller dispose failure"); }
    }
}
internal static class LifecycleHarness
{
    private static int passes;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        passes++;
        Console.WriteLine("PASS: " + name);
    }
    private static void Reset()
    {
        g.ui = new FakeUI(); g.timer = new FakeTimer();
        BookView.ThrowBuild = false; BookView.ThrowDispose = false; BookView.Disposals = 0;
        MOD_YtxSQ1.Core.WorkshopController.ThrowUpdate = false;
        MOD_YtxSQ1.Core.WorkshopController.ThrowDispose = false;
        MOD_YtxSQ1.Core.WorkshopController.Disposals = 0;
    }
    public static void Main()
    {
        Reset();
        var window = new WorkshopWindow(); window.Toggle();
        BookView.ThrowDispose = true; window.Close(); window.Close();
        Check(g.ui.Closes == 1 && BookView.Disposals == 1 && !window.IsOpen && !WindowInputScope.Active, "cleanup failure still closes window and releases input patches; repeated close is safe");

        Reset(); window = new WorkshopWindow(); window.Toggle();
        var other = new UITextInfoLong(); g.ui.Current = other; window.Close();
        Check(g.ui.Current == other && g.ui.Closes == 0, "close preserves replacement shared window");

        Reset(); BookView.ThrowBuild = true; window = new WorkshopWindow(); window.Toggle();
        Check(window.IsOpen && g.ui.Current.InitCount == 2 && BookView.Disposals == 1, "build failure restores native fallback");
        g.ui.Current.gameObject.activeInHierarchy = false; window.RefreshOwnership();
        var field = typeof(WorkshopWindow).GetField("ownedWindow", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(field.GetValue(window) == null, "native fallback close clears ownership without a view");

        Reset(); window = new WorkshopWindow(); window.Toggle(); other = new UITextInfoLong();
        g.ui.Current = other; window.RefreshOwnership();
        Check(BookView.Disposals == 1 && field.GetValue(window) == null && g.ui.Current == other, "ownership loss releases view and preserves replacement");

        Reset(); var mod = new ModMain(); mod.Init();
        MOD_YtxSQ1.Core.WorkshopController.ThrowUpdate = true;
        MOD_YtxSQ1.Core.WorkshopController.ThrowDispose = true;
        var errorCount = UnityEngine.Debug.Errors;
        g.timer.Callback(); g.timer.Callback(); mod.Destroy();
        Check(g.timer.Stops == 1 && MOD_YtxSQ1.Core.WorkshopController.Disposals == 1 && UnityEngine.Debug.Errors == errorCount + 1,
            "update failure logs original error and releases timer once despite disposal failure");

        Reset(); mod = new ModMain(); mod.Init(); g.timer.ThrowStop = true;
        MOD_YtxSQ1.Core.WorkshopController.ThrowDispose = true; mod.Destroy(); mod.Destroy();
        Check(g.timer.Stops == 1 && MOD_YtxSQ1.Core.WorkshopController.Disposals == 1, "failed destroy detaches references and stays idempotent");

        Reset(); mod = new ModMain(); mod.Init(); mod.Init(); mod.Destroy();
        Check(g.timer.Stops == 2 && MOD_YtxSQ1.Core.WorkshopController.Disposals == 2, "reinitialization releases each previous lifecycle");
        Console.WriteLine("TOTAL: " + passes);
    }
}
