// 编译真实控制器，核对关闭低频检查不会吞掉F8、保存回调或场景释放。
using System;
using MOD_YtxSQ1.Core;
using MOD_YtxSQ1.UI;
using MOD_YtxSQ1.Services;
namespace UnityEngine {
    public enum KeyCode { F8 }
    public static class Time { public static float unscaledTime; }
    public static class Input { public static bool Pressed; public static bool GetKeyDown(KeyCode key) { return Pressed; } }
}
namespace UnityEngine.UI { public class InputField { public bool isFocused; } }
namespace TMPro { public class TMP_InputField { public bool isFocused; } }
namespace UnityEngine.EventSystems {
    public class EventSystem { public static EventSystem current; public GameObject currentSelectedGameObject; }
}
public class GameObject {
    public bool activeInHierarchy = true;
    public object Field;
    public T GetComponentInParent<T>() where T : class { return Field as T; }
}
public class UIType { public static readonly UIType MapMain = new UIType(); public int uiLayer; }
public class MapUI { public GameObject gameObject = new GameObject(); public UIType uiType = UIType.MapMain; }
public class World { public bool isDestroy; public bool isIntoWorld = true; }
public class UI {
    public MapUI Map = new MapUI(); public object Top; public int MapReads;
    public MapUI GetUI(UIType type) { MapReads++; return Map; }
    public object GetLayerTopUI(int layer) { return Top ?? Map; }
}
public static class g { public static bool isDestroy; public static World world = new World(); public static UI ui = new UI(); }
namespace MOD_YtxSQ1.Services {
    internal static class CharacterSave { internal static int Polls, Clears; internal static void Poll() { Polls++; } internal static void Sync(string key) { Clears++; } }
    internal class CharacterEditSession { internal static CharacterEditSession Shared = new CharacterEditSession(); internal int Clears; internal void Sync(string key) { Clears++; } }
}
namespace MOD_YtxSQ1.UI {
    internal class WorkshopWindow {
        internal static WorkshopWindow Last;
        internal bool IsOpen; internal int Checks, Refreshes, Toggles, Closes;
        internal WorkshopWindow() { Last = this; }
        internal void RefreshOwnership() { Checks++; }
        internal void RefreshCharacter() { Refreshes++; }
        internal void Toggle() { Toggles++; IsOpen = !IsOpen; }
        internal string OpenId;
        internal void Open(string id) { OpenId = id; IsOpen = true; }
        internal void Close() { Closes++; IsOpen = false; }
    }
}
internal static class ControllerIdleHarness {
    private static int passed;
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; Console.WriteLine("PASS: " + name); }
    private static void Main() {
        var controller = new WorkshopController(); var window = WorkshopWindow.Last;
        for (int i = 0; i < 100; i++) { UnityEngine.Time.unscaledTime = i * .001f; controller.Update(); }
        Check(g.ui.MapReads == 0 && window.Checks == 1, "closed window skips map queries and throttles ownership checks");
        Check(CharacterSave.Polls == 100, "save completion is polled every frame while closed");
        UnityEngine.Input.Pressed = true; controller.Update();
        Check(window.IsOpen && window.Toggles == 1, "F8 opens immediately inside idle interval");
        UnityEngine.Input.Pressed = false; controller.Update();
        Check(window.Refreshes == 2, "open window continues frame updates");
        g.ui.Map.gameObject.activeInHierarchy = false; controller.Update();
        Check(!window.IsOpen && window.Closes == 1, "leaving map closes active window");
        g.world.isIntoWorld = false; UnityEngine.Time.unscaledTime = 1; controller.Update();
        Check(CharacterSave.Clears == 1 && CharacterEditSession.Shared.Clears == 1, "world exit clears session during idle check");
        g.world.isIntoWorld = true; g.ui.Map.gameObject.activeInHierarchy = true;
        UnityEngine.EventSystems.EventSystem.current = new UnityEngine.EventSystems.EventSystem { currentSelectedGameObject = new GameObject { Field = new UnityEngine.UI.InputField { isFocused = true } } };
        UnityEngine.Input.Pressed = true; controller.Update();
        Check(window.Toggles == 1, "F8 does not interrupt active text input");
        UnityEngine.EventSystems.EventSystem.current = null; g.ui.Top = new object(); controller.Update();
        Check(window.Toggles == 1, "another top UI blocks opening");
        controller.Dispose(); Check(CharacterEditSession.Shared.Clears == 2, "dispose releases edit session");
        controller = new WorkshopController(); window = WorkshopWindow.Last; g.ui.Top = null; UnityEngine.Input.Pressed = false;
        var mapReads = g.ui.MapReads; var polls = CharacterSave.Polls;
        for (int i = 0; i < 4*60*60*60; i++) { UnityEngine.Time.unscaledTime = 100 + i/60f; controller.Update(); }
        Check(g.ui.MapReads == mapReads && window.Toggles == 0 && window.Refreshes == 0, "four simulated hours closed do not query map or create a view");
        Check(CharacterSave.Polls-polls == 4*60*60*60, "four simulated hours retain save completion polling");
        UnityEngine.Input.Pressed = true; controller.Update(); UnityEngine.Input.Pressed = false;
        Check(window.IsOpen && window.Toggles == 1, "F8 opens immediately after four simulated hours idle");
        controller.Dispose();
        g.ui.Top = new object(); controller = new WorkshopController(); window = WorkshopWindow.Last;
        controller.OpenFromEntrance("npc-panel"); Check(window.IsOpen && window.OpenId == "npc-panel", "authorized NPC entrance opens above underlying NPC panel");
        window.Close(); g.world.isIntoWorld = false; controller.OpenFromEntrance("npc-panel"); Check(!window.IsOpen, "entrance rejects exited world");
        g.world.isIntoWorld = true; g.ui.Map.gameObject.activeInHierarchy = false; controller.OpenFromEntrance(null); Check(!window.IsOpen, "entrance rejects unsupported scene");
        controller.Dispose();
        Console.WriteLine("TOTAL: " + passed);
    }
}
