// 故障注入：编译真实 WorkshopWindow/ModMain，替代游戏和视图依赖。
// 验证托管生命周期控制流，不模拟 Unity 渲染、IL2CPP 或真实游戏场景。
using System;
using System.Reflection;
using MOD_YtxSQ1;
using MOD_YtxSQ1.UI;

namespace UnityEngine
{
    public enum KeyCode { F8 }
    public static class Input { public static bool Pressed; public static bool GetKeyDown(KeyCode key) { return Pressed; } }
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
    public static int FailInitCount;
    public GameObject gameObject = new GameObject();
    public int InitCount;
    public void InitData(string title, string info, string button) { InitCount++; if (FailInitCount > 0) { FailInitCount--; throw new Exception("init failure"); } }
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
    public bool ThrowOpen, ReturnNull;
    public UITextInfoLong Current;
    public int Closes;
    public UITextInfoLong GetUI(UIType type) { return Current; }
    public T OpenUI<T>(UIType type) where T : class { if (ThrowOpen) throw new Exception("open failure"); if (ReturnNull) return null; Current = new UITextInfoLong(); return Current as T; }
    public void CloseUI(UITextInfoLong window, bool animated) { Closes++; Current = null; }
}
public static class g
{
    public static FakeUI ui = new FakeUI();
    public static FakeTimer timer = new FakeTimer();
}
namespace MOD_YtxSQ1.UI
{
    internal static class PaperSkin { internal static int Clears; internal static void ClearCache() { Clears++; } }
    internal static class WindowInputScope
    {
        public static bool Active;
        public static bool ThrowAttach;
        public static void Attach(UITextInfoLong window, object input) { Active=true; if (ThrowAttach) throw new Exception("attach failure"); }
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
namespace MOD_YtxSQ1.Services { internal class CharacterEditSession { internal static CharacterEditSession Shared=new CharacterEditSession(); internal int Count; internal void Clear(){Count=0;} } }
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
        UnityEngine.Input.Pressed = false;
        UITextInfoLong.FailInitCount = 0; WindowInputScope.ThrowAttach = false; WindowInputScope.Active = false;
        BookView.ThrowBuild = false; BookView.ThrowDispose = false; BookView.Disposals = 0;
        MOD_YtxSQ1.Core.WorkshopController.ThrowUpdate = false;
        MOD_YtxSQ1.Core.WorkshopController.ThrowDispose = false;
        MOD_YtxSQ1.Core.WorkshopController.Disposals = 0;
    }
    public static void Main()
    {
        Reset(); var cycles = new WorkshopWindow();
        for (int i = 0; i < 30; i++) { cycles.Toggle(); if (!cycles.IsOpen || !WindowInputScope.Active) throw new Exception("open cycle " + i); cycles.Close(); if (cycles.IsOpen || WindowInputScope.Active) throw new Exception("close cycle " + i); }
        Check(g.ui.Closes == 30 && BookView.Disposals == 30, "30 open-close cycles release each view and input patch");

        Reset(); g.ui.ThrowOpen = true; var failed = new WorkshopWindow(); failed.Toggle();
        Check(!failed.IsOpen && !WindowInputScope.Active, "native open exception is contained locally");
        g.ui.ThrowOpen = false; failed.Toggle(); Check(failed.IsOpen, "F8 can retry after native open failure"); failed.Close();

        Reset(); g.ui.ReturnNull = true; failed = new WorkshopWindow(); failed.Toggle();
        Check(!failed.IsOpen, "null native window does not escape as update failure");

        Reset(); UITextInfoLong.FailInitCount = 1; failed = new WorkshopWindow(); failed.Toggle();
        Check(failed.IsOpen && g.ui.Current.InitCount == 2 && !WindowInputScope.Active, "initialization failure restores native fallback"); failed.Close();

        Reset(); UITextInfoLong.FailInitCount = 2; failed = new WorkshopWindow(); failed.Toggle();
        Check(!failed.IsOpen && g.ui.Current == null && g.ui.Closes == 1, "failed fallback closes owned window without escaping");

        Reset(); WindowInputScope.ThrowAttach = true; failed = new WorkshopWindow(); failed.Toggle();
        Check(failed.IsOpen && BookView.Disposals == 1 && !WindowInputScope.Active, "partial input attachment is removed before fallback"); failed.Close();

        Reset();
        MOD_YtxSQ1.Services.CharacterEditSession.Shared.Count=5; var window = new WorkshopWindow(); window.Toggle(); Check(MOD_YtxSQ1.Services.CharacterEditSession.Shared.Count==0,"opening discards unapplied drafts before building fresh view");
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
        Check(PaperSkin.Clears > 0, "mod destruction releases fixed art cache");
        Reset(); mod = new ModMain(); mod.Init(); MOD_YtxSQ1.Core.WorkshopController.ThrowUpdate = true;
        var beforeError = UnityEngine.Debug.Errors; g.timer.Callback();
        for (int i = 0; i < 1000; i++) g.timer.Callback();
        Check(g.timer.Stops == 0 && UnityEngine.Debug.Errors == beforeError + 1 && MOD_YtxSQ1.Core.WorkshopController.Disposals == 1, "faulted idle keeps retry entry without repeated errors or controller creation");
        MOD_YtxSQ1.Core.WorkshopController.ThrowUpdate = false; UnityEngine.Input.Pressed = true; g.timer.Callback(); UnityEngine.Input.Pressed = false;
        Check(!(bool)typeof(ModMain).GetField("updateFailed", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(mod), "F8 recovers after a transient update error");
        mod.Destroy(); Check(g.timer.Stops == 1 && MOD_YtxSQ1.Core.WorkshopController.Disposals == 2, "recovered controller and retained timer release on unload");
        Console.WriteLine("TOTAL: " + passes);
    }
}
