using System;
using System.Collections.Generic;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace MOD_YtxSQ1.Services
{
    // The native capacity base getter reads configuration; its setter intentionally does nothing.
    // Persist only our bonus, then attach it through the native additive effect list.
    internal static class CapacityRuntime
    {
        internal const string Key = "BHML.capacity.bonus";
        private sealed class Binding
        {
            internal string Id;
            internal IntPtr Info, FieldPointer;
            internal DynInt Field;
            internal int Bonus;
            internal bool Attached;
            internal DynInt.DynObjectAddHandler Handler;
        }
        private static readonly Dictionary<IntPtr, Binding> bindings = new Dictionary<IntPtr, Binding>();
        private static bool initialized;
        private static float nextCheck;
        private static IntPtr world, data;
        private static bool ready;
        // No native patch is installed: constructors also run in new-game UI and loading threads.
        internal static void Initialize() { initialized = true; }
        private static bool WorldReady()
        {
            var player = g.world?.playerUnit;
            return !g.isDestroy && g.world != null && !g.world.isDestroy && g.world.isIntoWorld && g.data != null && g.world.unit != null
                && player?.data?.unitData?.objData != null && player.data.dynUnitData?.propGridNum != null;
        }
        private static bool Current()
        { return ready && WorldReady() && g.world.Pointer == world && g.data.Pointer == data; }
        internal static void Tick()
        {
            if (!initialized) return;
            if (!WorldReady()) {
                // Old-world native objects may already be invalid. Drop references without native calls.
                bindings.Clear(); ready = false; return;
            }
            if (Current()) {
                if (Time.unscaledTime < nextCheck) return;
                nextCheck = Time.unscaledTime + 1f;
                // Check only units with our bonus, on the frame service's main thread.
                foreach (var binding in new List<Binding>(bindings.Values)) {
                    var unit = CharacterReader.Resolve(binding.Id);
                    if (unit?.data?.dynUnitData?.propGridNum == null) { bindings.Remove(binding.FieldPointer); continue; }
                    Restore(unit.data.dynUnitData, unit.data.unitData);
                }
                return;
            }
            bindings.Clear(); world = g.world.Pointer; data = g.data.Pointer; ready = true; nextCheck = Time.unscaledTime + 1f;
            Restore(g.world.playerUnit.data.dynUnitData, g.world.playerUnit.data.unitData);
            var units = g.world.unit.GetUnits(true);
            if (units != null) for (int i = 0; i < units.Count; i++) {
                var unit = units[i]; if (unit?.data?.unitData != null) Restore(unit.data.dynUnitData, unit.data.unitData);
            }
        }
        private static void Restore(WorldUnitDynData dynamic, DataUnit.UnitInfoData info)
        {
            try { if (Current()) Bind(dynamic, info); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 扩容恢复未完成：" + ex.Message); }
        }
        internal static string Record(DataObjectData store)
        { if (store == null) throw new InvalidOperationException("背包扩容记录不可读取"); return store.ContainsKey(Key) ? store.GetString(Key) : null; }
        internal static int Bonus(DataObjectData store)
        {
            var record = Record(store); if (record == null && !store.ContainsKey(Key)) return 0;
            int value;
            if (record == null || !int.TryParse(record, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value) || value < 0)
                throw new InvalidOperationException("背包扩容记录无效，未修改");
            return value;
        }
        internal static void EnsureBound(WorldUnitBase unit)
        {
            Initialize(); Tick();
            if (!Current()) throw new InvalidOperationException("背包扩容运行环境尚未就绪");
            Bind(unit.data.dynUnitData, unit.data.unitData);
        }
        private static void Bind(WorldUnitDynData dynamic, DataUnit.UnitInfoData info)
        {
            if (dynamic == null || info?.objData == null) return;
            int bonus = Bonus(info.objData); var field = dynamic.propGridNum;
            if (field == null) throw new InvalidOperationException("背包容量字段不可读取");
            Binding previous;
            if (bindings.TryGetValue(field.Pointer, out previous)) {
                if (!previous.Attached) throw new InvalidOperationException("扩容效果挂载状态不确定，请退出存档后核对");
                if (previous.Info == info.Pointer && previous.Bonus == bonus) return;
                previous.Attached = false; field.DelValue(previous.Handler); bindings.Remove(field.Pointer);
            }
            if (bonus == 0) return;
            // A reconstructed field supersedes its old native instance. Do not retain dead wrappers.
            var obsolete = new List<IntPtr>();
            foreach (var pair in bindings) if (pair.Value.Id == info.unitID && pair.Key != field.Pointer) obsolete.Add(pair.Key);
            foreach (var key in obsolete) bindings.Remove(key);
            var binding = new Binding { Id = info.unitID, Info = info.Pointer, FieldPointer = field.Pointer, Field = field, Bonus = bonus };
            binding.Handler = DelegateSupport.ConvertDelegate<DynInt.DynObjectAddHandler>(new Func<int>(() => binding.Bonus));
            // Record ownership before native attachment so a partial failure remains releasable.
            bindings.Add(field.Pointer, binding);
            field.AddValue(binding.Handler, "八荒秘录背包扩容"); binding.Attached = true;
        }
        internal static void Dispose()
        {
            if (Current()) foreach (var binding in new List<Binding>(bindings.Values)) try {
                var unit = CharacterReader.Resolve(binding.Id);
                if (unit?.data?.unitData?.Pointer == binding.Info && unit.data.dynUnitData?.propGridNum?.Pointer == binding.FieldPointer)
                    binding.Field.DelValue(binding.Handler);
            } catch (Exception ex) { Debug.LogWarning("[八荒秘录] 扩容效果释放：" + ex.Message); }
            bindings.Clear(); ready = false; world = data = IntPtr.Zero;
            initialized = false; nextCheck = 0;
        }
    }
}
