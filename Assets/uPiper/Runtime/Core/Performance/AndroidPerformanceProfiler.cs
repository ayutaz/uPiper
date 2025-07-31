using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using uPiper.Core.Logging;

namespace uPiper.Core.Performance
{
    /// <summary>
    /// Android向けパフォーマンスプロファイラー
    /// メモリ使用量、処理時間、リソース使用状況を測定
    /// </summary>
    public class AndroidPerformanceProfiler
    {
        private readonly Dictionary<string, ProfileData> _profileData = new();
        private readonly Stopwatch _stopwatch = new();

        public class ProfileData
        {
            public long TotalTime { get; set; }
            public int CallCount { get; set; }
            public long MinTime { get; set; } = long.MaxValue;
            public long MaxTime { get; set; }
            public long MemoryBefore { get; set; }
            public long MemoryAfter { get; set; }

            public double AverageTime => CallCount > 0 ? (double)TotalTime / CallCount : 0;
            public long MemoryDelta => MemoryAfter - MemoryBefore;
        }

        public class ProfileScope : IDisposable
        {
            private readonly AndroidPerformanceProfiler _profiler;
            private readonly string _name;
            private readonly long _startTime;
            private readonly long _startMemory;

            public ProfileScope(AndroidPerformanceProfiler profiler, string name)
            {
                _profiler = profiler;
                _name = name;
                _startTime = Stopwatch.GetTimestamp();
                _startMemory = GC.GetTotalMemory(false);

                // Unity Profilerにも記録
                Profiler.BeginSample($"[uPiper] {name}");
            }

            public void Dispose()
            {
                Profiler.EndSample();

                var endTime = Stopwatch.GetTimestamp();
                var endMemory = GC.GetTotalMemory(false);
                var elapsedMs = (endTime - _startTime) * 1000 / Stopwatch.Frequency;

                _profiler.RecordProfile(_name, elapsedMs, _startMemory, endMemory);
            }
        }

        /// <summary>
        /// プロファイリングスコープを開始
        /// </summary>
        public ProfileScope BeginProfile(string name)
        {
            return new ProfileScope(this, name);
        }

        /// <summary>
        /// プロファイルデータを記録
        /// </summary>
        private void RecordProfile(string name, long elapsedMs, long memoryBefore, long memoryAfter)
        {
            if (!_profileData.TryGetValue(name, out var data))
            {
                data = new ProfileData();
                _profileData[name] = data;
            }

            data.TotalTime += elapsedMs;
            data.CallCount++;
            data.MinTime = Math.Min(data.MinTime, elapsedMs);
            data.MaxTime = Math.Max(data.MaxTime, elapsedMs);
            data.MemoryBefore = memoryBefore;
            data.MemoryAfter = memoryAfter;
        }

        /// <summary>
        /// システム情報を取得
        /// </summary>
        public static string GetSystemInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Android System Info ===");
            sb.AppendLine($"Device Model: {SystemInfo.deviceModel}");
            sb.AppendLine($"Device Type: {SystemInfo.deviceType}");
            sb.AppendLine($"OS: {SystemInfo.operatingSystem}");
            sb.AppendLine($"CPU: {SystemInfo.processorType} ({SystemInfo.processorCount} cores)");
            sb.AppendLine($"CPU Frequency: {SystemInfo.processorFrequency} MHz");
            sb.AppendLine($"System Memory: {SystemInfo.systemMemorySize} MB");
            sb.AppendLine($"Graphics Memory: {SystemInfo.graphicsMemorySize} MB");

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android固有の情報
            using (var activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = activityClass.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var runtimeClass = new AndroidJavaClass("java.lang.Runtime"))
            using (var runtime = runtimeClass.CallStatic<AndroidJavaObject>("getRuntime"))
            {
                long maxMemory = runtime.Call<long>("maxMemory");
                long totalMemory = runtime.Call<long>("totalMemory");
                long freeMemory = runtime.Call<long>("freeMemory");
                long usedMemory = totalMemory - freeMemory;
                
                sb.AppendLine($"JVM Max Memory: {maxMemory / 1024 / 1024} MB");
                sb.AppendLine($"JVM Total Memory: {totalMemory / 1024 / 1024} MB");
                sb.AppendLine($"JVM Used Memory: {usedMemory / 1024 / 1024} MB");
                sb.AppendLine($"JVM Free Memory: {freeMemory / 1024 / 1024} MB");
            }
#endif

            return sb.ToString();
        }

        /// <summary>
        /// プロファイル結果をレポート
        /// </summary>
        public string GenerateReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Performance Profile Report ===");
            sb.AppendLine($"Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            foreach (var kvp in _profileData)
            {
                var data = kvp.Value;
                sb.AppendLine($"[{kvp.Key}]");
                sb.AppendLine($"  Calls: {data.CallCount}");
                sb.AppendLine($"  Total Time: {data.TotalTime}ms");
                sb.AppendLine($"  Average Time: {data.AverageTime:F2}ms");
                sb.AppendLine($"  Min Time: {data.MinTime}ms");
                sb.AppendLine($"  Max Time: {data.MaxTime}ms");
                sb.AppendLine($"  Memory Delta: {data.MemoryDelta / 1024:N0} KB");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// プロファイルデータをクリア
        /// </summary>
        public void Clear()
        {
            _profileData.Clear();
        }

        /// <summary>
        /// メモリ使用量を測定
        /// </summary>
        public static void LogMemoryUsage(string tag)
        {
            var gcMemory = GC.GetTotalMemory(false);
            PiperLogger.LogInfo($"[Memory {tag}] GC: {gcMemory / 1024 / 1024:F2} MB");

#if UNITY_ANDROID && !UNITY_EDITOR
            using (var runtimeClass = new AndroidJavaClass("java.lang.Runtime"))
            using (var runtime = runtimeClass.CallStatic<AndroidJavaObject>("getRuntime"))
            {
                long totalMemory = runtime.Call<long>("totalMemory");
                long freeMemory = runtime.Call<long>("freeMemory");
                long usedMemory = totalMemory - freeMemory;
                
                PiperLogger.LogInfo($"[Memory {tag}] JVM Used: {usedMemory / 1024 / 1024:F2} MB, Free: {freeMemory / 1024 / 1024:F2} MB");
            }
#endif
        }
    }
}