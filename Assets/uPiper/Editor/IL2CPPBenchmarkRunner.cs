using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace uPiper.Editor
{
    /// <summary>
    /// Automated benchmark runner for IL2CPP performance comparison
    /// </summary>
    public class IL2CPPBenchmarkRunner : EditorWindow
    {
        private bool _isRunning = false;
        private StringBuilder _results = new StringBuilder();
        private Vector2 _scrollPosition;

        [MenuItem("uPiper/IL2CPP Benchmark Runner")]
        public static void ShowWindow()
        {
            var window = GetWindow<IL2CPPBenchmarkRunner>("IL2CPP Benchmark");
            window.minSize = new Vector2(600, 400);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("IL2CPP Performance Benchmark Runner", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Current backend info
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            var currentBackend = PlayerSettings.GetScriptingBackend(namedBuildTarget);
            EditorGUILayout.LabelField("Current Scripting Backend:", currentBackend.ToString());

            EditorGUILayout.Space();

            // Instructions
            EditorGUILayout.HelpBox(
                "This tool helps you run performance benchmarks on both Mono and IL2CPP backends.\n\n" +
                "Steps:\n" +
                "1. Run benchmarks with current backend (Mono)\n" +
                "2. Switch to IL2CPP backend\n" +
                "3. Build and run benchmarks\n" +
                "4. Compare results",
                MessageType.Info);

            EditorGUILayout.Space();

            // Action buttons
            using (new EditorGUI.DisabledScope(_isRunning))
            {
                if (GUILayout.Button("Run Editor Benchmarks (Current Backend)", GUILayout.Height(30)))
                {
                    RunEditorBenchmarks();
                }

                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (currentBackend == ScriptingImplementation.Mono2x)
                    {
                        if (GUILayout.Button("Switch to IL2CPP", GUILayout.Height(25)))
                        {
                            SwitchToIL2CPP();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Switch to Mono", GUILayout.Height(25)))
                        {
                            SwitchToMono();
                        }
                    }

                    if (GUILayout.Button("Configure Build Settings", GUILayout.Height(25)))
                    {
                        IL2CPPBuildSettings.ConfigureIL2CPPSettings();
                    }
                }

                EditorGUILayout.Space();

                if (GUILayout.Button("Build Benchmark Player", GUILayout.Height(30)))
                {
                    BuildBenchmarkPlayer();
                }
            }

            EditorGUILayout.Space();

            // Results display
            EditorGUILayout.LabelField("Benchmark Results:", EditorStyles.boldLabel);

            using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPosition, GUILayout.ExpandHeight(true)))
            {
                _scrollPosition = scrollView.scrollPosition;

                if (_results.Length > 0)
                {
                    EditorGUILayout.TextArea(_results.ToString(), GUILayout.ExpandHeight(true));
                }
                else
                {
                    EditorGUILayout.HelpBox("No benchmark results yet. Run benchmarks to see results.", MessageType.None);
                }
            }

            EditorGUILayout.Space();

            // Export buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear Results"))
                {
                    _results.Clear();
                    Repaint();
                }

                using (new EditorGUI.DisabledScope(_results.Length == 0))
                {
                    if (GUILayout.Button("Export Results"))
                    {
                        ExportResults();
                    }
                }
            }
        }

        private void RunEditorBenchmarks()
        {
            _isRunning = true;
            _results.AppendLine($"=== Benchmark Run: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            _results.AppendLine($"Backend: {PlayerSettings.GetScriptingBackend(namedBuildTarget)}");
            _results.AppendLine($"Unity Version: {Application.unityVersion}");
            _results.AppendLine($"Platform: {Application.platform}");
            _results.AppendLine();

            // Note: In a real implementation, you would run the actual benchmark tests here
            // For now, we'll add placeholder text
            _results.AppendLine("To run actual benchmarks:");
            _results.AppendLine("1. Open Unity Test Runner (Window > General > Test Runner)");
            _results.AppendLine("2. Navigate to uPiper > Tests > Runtime > Performance");
            _results.AppendLine("3. Run IL2CPPPerformanceTest");
            _results.AppendLine("4. Copy results here");
            _results.AppendLine();

            _isRunning = false;
            Repaint();
        }

        private void SwitchToIL2CPP()
        {
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            PlayerSettings.SetScriptingBackend(namedBuildTarget, ScriptingImplementation.IL2CPP);
            Debug.Log("Switched to IL2CPP backend");
            Repaint();
        }

        private void SwitchToMono()
        {
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            PlayerSettings.SetScriptingBackend(namedBuildTarget, ScriptingImplementation.Mono2x);
            Debug.Log("Switched to Mono backend");
            Repaint();
        }

        private void BuildBenchmarkPlayer()
        {
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            var buildPath = EditorUtility.SaveFilePanel(
                "Save Benchmark Build",
                "",
                $"uPiper_Benchmark_{PlayerSettings.GetScriptingBackend(namedBuildTarget)}",
                GetBuildExtension());

            if (string.IsNullOrEmpty(buildPath))
                return;

            // Configure build options
            var buildOptions = BuildOptions.Development | BuildOptions.IncludeTestAssemblies;

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/uPiper/Scenes/InferenceEngineDemo.unity" },
                locationPathName = buildPath,
                target = EditorUserBuildSettings.activeBuildTarget,
                options = buildOptions
            };

            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"Benchmark build completed: {buildPath}");
                EditorUtility.RevealInFinder(buildPath);
            }
            else
            {
                Debug.LogError($"Build failed: {report.summary.result}");
            }
        }

        private string GetBuildExtension()
        {
            return EditorUserBuildSettings.activeBuildTarget switch
            {
                BuildTarget.StandaloneWindows64 => "exe",
                BuildTarget.StandaloneOSX => "app",
                BuildTarget.StandaloneLinux64 => "",
                _ => ""
            };
        }

        private void ExportResults()
        {
            var path = EditorUtility.SaveFilePanel(
                "Export Benchmark Results",
                "",
                $"benchmark_results_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                "txt");

            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, _results.ToString());
                EditorUtility.RevealInFinder(path);
                Debug.Log($"Results exported to: {path}");
            }
        }
    }
}