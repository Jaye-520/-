using System;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace MOD_YtxSQ1.Services
{
    // Fixed-size samples; no log, sorting or allocation on a measured frame.
    internal static class WorkshopPerformance
    {
        private sealed class Samples
        {
            internal readonly double[] Values = new double[512];
            internal int Count, Next;
            internal long Total;
            internal double Maximum;
        }
        internal struct Measurement : IDisposable
        {
            private readonly Samples samples;
            private readonly long start;
            internal Measurement(string name, long started = 0)
            { samples = Get(name); start = started == 0 ? Stopwatch.GetTimestamp() : started; }
            public void Dispose()
            {
                var ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                samples.Values[samples.Next] = ms; samples.Next = (samples.Next + 1) % samples.Values.Length;
                samples.Count = Math.Min(samples.Count + 1, samples.Values.Length);
                samples.Total++; samples.Maximum = Math.Max(samples.Maximum, ms);
            }
        }
        private static readonly Dictionary<string, Samples> stages = new Dictionary<string, Samples>();
        private static readonly HashSet<int> controls = new HashSet<int>();
        internal static long Created, Destroyed, NativeIconsCreated;
        private static int closes;
        internal static void Closed() { if (++closes % 10 == 0) Report(); }
        internal static void RecordCreated(UnityEngine.GameObject value)
        { if (controls.Add(value.GetInstanceID())) Created++; }
        internal static void RecordDestroy(UnityEngine.GameObject value)
        {
            foreach (var child in value.GetComponentsInChildren<UnityEngine.Transform>(true))
                if (controls.Remove(child.gameObject.GetInstanceID())) Destroyed++;
        }
        private static Samples Get(string name)
        {
            Samples value;
            if (!stages.TryGetValue(name, out value)) { value = new Samples(); stages.Add(name, value); }
            return value;
        }
        internal static Measurement Measure(string name) { return new Measurement(name); }
        internal static Measurement MeasureFrom(string name, long start) { return new Measurement(name, start); }
        internal static void Report()
        {
            foreach (var pair in stages)
            {
                var sample = pair.Value; if (sample.Count == 0) continue;
                var sorted = new double[sample.Count]; Array.Copy(sample.Values, sorted, sample.Count); Array.Sort(sorted);
                Debug.Log("[八荒秘录] 性能 " + pair.Key + " 次数=" + sample.Total + " 样本=" + sample.Count
                    + " 中位=" + sorted[(sample.Count - 1) / 2].ToString("F2") + "ms P95="
                    + sorted[Math.Max(0, (int)Math.Ceiling(sample.Count * .95) - 1)].ToString("F2")
                    + "ms 最大=" + sample.Maximum.ToString("F2") + "ms");
            }
            Debug.Log("[八荒秘录] 性能 自建控件累计创建=" + Created + " 累计销毁=" + Destroyed
                + " 持有=" + controls.Count + " 原生背包图标累计创建=" + NativeIconsCreated);
        }
        internal static void Clear() { stages.Clear(); controls.Clear(); closes = 0; Created = Destroyed = NativeIconsCreated = 0; }
    }
}
