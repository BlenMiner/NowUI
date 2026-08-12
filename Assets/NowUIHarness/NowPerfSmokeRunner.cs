using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace NowUI.Editor
{
    public static class NowPerfSmokeRunner
    {
        const int WarmupFrames = 3;
        const int MeasureFrames = 12;

        public static void Run()
        {
            string outputRoot = NowHarnessScenarios.ReadArgument(
                "-nowuiArtifactsPath",
                Path.Combine(NowHarnessScenarios.ProjectPath(), "artifacts", "local", "perf"));
            Directory.CreateDirectory(outputRoot);

            var metrics = new List<PerfMetric>();
            foreach (var scenario in NowHarnessScenarios.All())
                metrics.Add(MeasureScenario(scenario));

            string path = Path.Combine(outputRoot, "nowui-perf.json");
            File.WriteAllText(path, BuildJson(metrics));
            UnityEngine.Debug.Log($"NowUI perf smoke wrote {path}.");
        }

        static PerfMetric MeasureScenario(NowHarnessScenario scenario)
        {
            for (int i = 0; i < WarmupFrames; ++i)
                NowHarnessScenarios.CapturePngBytes(scenario);

            long allocatedBefore = AllocatedBytesOrZero();
            var stopwatch = Stopwatch.StartNew();
            NowHarnessCapture last = null;

            for (int i = 0; i < MeasureFrames; ++i)
            {
                string temp = Path.Combine(Path.GetTempPath(), $"nowui-perf-{scenario.name}-{Guid.NewGuid():N}.png");
                try
                {
                    last = NowHarnessScenarios.Capture(scenario, temp);
                }
                finally
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
            }

            stopwatch.Stop();
            long allocatedAfter = AllocatedBytesOrZero();

            return new PerfMetric
            {
                name = scenario.name,
                width = scenario.width,
                height = scenario.height,
                frames = MeasureFrames,
                totalMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                averageMilliseconds = stopwatch.Elapsed.TotalMilliseconds / MeasureFrames,
                allocatedBytes = Math.Max(0, allocatedAfter - allocatedBefore),
                batchCount = last != null ? last.batchCount : 0,
                vertexCount = last != null ? last.vertexCount : 0
            };
        }

        static long AllocatedBytesOrZero()
        {
            try
            {
                return GC.GetAllocatedBytesForCurrentThread();
            }
            catch (NotImplementedException)
            {
                return 0;
            }
        }

        static string BuildJson(IEnumerable<PerfMetric> metrics)
        {
            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"warmupFrames\": " + WarmupFrames + ",");
            json.AppendLine("  \"measureFrames\": " + MeasureFrames + ",");
            json.AppendLine("  \"scenarios\": [");

            bool first = true;
            foreach (var metric in metrics)
            {
                if (!first)
                    json.AppendLine(",");

                first = false;
                json.Append("    { ");
                json.AppendFormat("\"name\": \"{0}\", ", metric.name);
                json.AppendFormat("\"width\": {0}, \"height\": {1}, ", metric.width, metric.height);
                json.AppendFormat("\"frames\": {0}, ", metric.frames);
                json.AppendFormat("\"totalMilliseconds\": {0:0.###}, ", metric.totalMilliseconds);
                json.AppendFormat("\"averageMilliseconds\": {0:0.###}, ", metric.averageMilliseconds);
                json.AppendFormat("\"allocatedBytes\": {0}, ", metric.allocatedBytes);
                json.AppendFormat("\"batchCount\": {0}, \"vertexCount\": {1}", metric.batchCount, metric.vertexCount);
                json.Append(" }");
            }

            json.AppendLine();
            json.AppendLine("  ]");
            json.AppendLine("}");
            return json.ToString();
        }

        sealed class PerfMetric
        {
            public string name;
            public int width;
            public int height;
            public int frames;
            public double totalMilliseconds;
            public double averageMilliseconds;
            public long allocatedBytes;
            public int batchCount;
            public int vertexCount;
        }
    }
}
