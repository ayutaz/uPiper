using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace uPiper.Demo
{
    /// <summary>
    /// EventSystemの入力モジュールを自動的に設定するコンポーネント
    /// プロジェクトの入力設定に応じて適切なモジュールを有効化します
    /// </summary>
    [RequireComponent(typeof(EventSystem))]
    [DefaultExecutionOrder(-100)] // 他のスクリプトより先に実行
    public class EventSystemAutoSetup : MonoBehaviour
    {
        // Input System UIモジュールの型名（リフレクション用）
        private const string INPUT_SYSTEM_MODULE_TYPENAME = "UnityEngine.InputSystem.UI.InputSystemUIInputModule";

        private void Awake()
        {
            SetupInputModule();
        }

        private void SetupInputModule()
        {
            var eventSystem = GetComponent<EventSystem>();
            if (eventSystem == null)
            {
                Debug.LogError("[EventSystemAutoSetup] EventSystem component not found");
                return;
            }

            // StandaloneInputModuleを取得
            var standaloneModule = GetComponent<StandaloneInputModule>();

            // Input System UIモジュールを動的に検索（存在する場合）
            Component inputSystemModule = null;
            Type inputSystemModuleType = null;

            try
            {
                // Input Systemパッケージが存在する場合、型を取得
                inputSystemModuleType = Type.GetType(INPUT_SYSTEM_MODULE_TYPENAME + ", Unity.InputSystem");
                if (inputSystemModuleType != null)
                {
                    inputSystemModule = GetComponent(inputSystemModuleType);
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[EventSystemAutoSetup] Input System not found: {e.Message}");
            }

            // 入力システムの設定を判定して適切なモジュールを有効化
            ConfigureInputModules(standaloneModule, inputSystemModule, inputSystemModuleType);
        }

        private void ConfigureInputModules(StandaloneInputModule standaloneModule, Component inputSystemModule, Type inputSystemModuleType)
        {
            // Input Systemモジュールが存在し、StandaloneModuleが無効または存在しない場合
            if (inputSystemModule != null && inputSystemModule.GetType() == inputSystemModuleType)
            {
                bool useInputSystem = false;

                // Active Input Handlingの設定を確認
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                // Input System のみ有効
                useInputSystem = true;
                Debug.Log("[EventSystemAutoSetup] Using Input System only mode");
#elif ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
                // Input Manager のみ有効
                useInputSystem = false;
                Debug.Log("[EventSystemAutoSetup] Using Input Manager only mode");
#else
                // 両方有効、または判定できない場合はInput Managerを優先
                useInputSystem = false;
                Debug.Log("[EventSystemAutoSetup] Using compatibility mode (Input Manager preferred)");
#endif

                if (useInputSystem)
                {
                    // Input Systemを使用
                    SetComponentEnabled(inputSystemModule, true);
                    if (standaloneModule != null)
                        standaloneModule.enabled = false;
                }
                else
                {
                    // Input Managerを使用
                    SetComponentEnabled(inputSystemModule, false);
                    EnsureStandaloneModule(standaloneModule);
                }
            }
            else
            {
                // Input Systemモジュールが存在しない場合は、StandaloneModuleを使用
                Debug.Log("[EventSystemAutoSetup] Input System module not found, using StandaloneInputModule");
                EnsureStandaloneModule(standaloneModule);
            }
        }

        private void EnsureStandaloneModule(StandaloneInputModule standaloneModule)
        {
            if (standaloneModule != null)
            {
                standaloneModule.enabled = true;
            }
            else
            {
                // StandaloneInputModuleが存在しない場合は追加
                standaloneModule = gameObject.AddComponent<StandaloneInputModule>();
                Debug.Log("[EventSystemAutoSetup] Added StandaloneInputModule");
            }
        }

        private void SetComponentEnabled(Component component, bool enabled)
        {
            if (component == null) return;

            var behaviour = component as Behaviour;
            if (behaviour != null)
            {
                behaviour.enabled = enabled;
            }
        }

        /// <summary>
        /// 現在の入力システム設定を取得
        /// </summary>
        public static string GetCurrentInputSystemInfo()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return "Input System (New)";
#elif ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            return "Input Manager (Legacy)";
#elif ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            return "Both (Hybrid Mode)";
#else
            return "Unknown/Default";
#endif
        }

        private void Start()
        {
            // デバッグ情報を出力
            Debug.Log($"[EventSystemAutoSetup] Input configuration: {GetCurrentInputSystemInfo()}");

            // 現在有効なモジュールを確認
            var standaloneModule = GetComponent<StandaloneInputModule>();
            if (standaloneModule != null && standaloneModule.enabled)
            {
                Debug.Log("[EventSystemAutoSetup] Active module: StandaloneInputModule");
            }

            // Input Systemモジュールの確認
            try
            {
                var inputSystemModuleType = Type.GetType(INPUT_SYSTEM_MODULE_TYPENAME + ", Unity.InputSystem");
                if (inputSystemModuleType != null)
                {
                    var inputSystemModule = GetComponent(inputSystemModuleType) as Behaviour;
                    if (inputSystemModule != null && inputSystemModule.enabled)
                    {
                        Debug.Log("[EventSystemAutoSetup] Active module: InputSystemUIInputModule");
                    }
                }
            }
            catch
            {
                // Input Systemが存在しない場合は無視
            }
        }
    }
}