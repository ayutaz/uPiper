using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

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

            // 各入力モジュールを取得
            var standaloneModule = GetComponent<StandaloneInputModule>();
#if ENABLE_INPUT_SYSTEM
            var inputSystemModule = GetComponent<InputSystemUIInputModule>();
#endif

            // プロジェクトの設定に基づいて適切なモジュールを有効化
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            // Input System のみ有効
            Debug.Log("[EventSystemAutoSetup] Using Input System only mode");
            
            if (standaloneModule != null)
                standaloneModule.enabled = false;
                
            if (inputSystemModule != null)
            {
                inputSystemModule.enabled = true;
            }
            else
            {
                Debug.LogWarning("[EventSystemAutoSetup] InputSystemUIInputModule not found. UI input may not work correctly.");
            }
            
#elif ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            // Input Manager のみ有効
            Debug.Log("[EventSystemAutoSetup] Using Input Manager only mode");
            
            #if ENABLE_INPUT_SYSTEM
            if (inputSystemModule != null)
                inputSystemModule.enabled = false;
            #endif
            
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
            
#else
            // 両方有効、または判定できない場合
            // Input Managerを優先（より互換性が高いため）
            Debug.Log("[EventSystemAutoSetup] Using compatibility mode (Input Manager preferred)");
            
            #if ENABLE_INPUT_SYSTEM
            if (inputSystemModule != null)
                inputSystemModule.enabled = false;
            #endif
            
            if (standaloneModule != null)
            {
                standaloneModule.enabled = true;
            }
            else
            {
                standaloneModule = gameObject.AddComponent<StandaloneInputModule>();
                Debug.Log("[EventSystemAutoSetup] Added StandaloneInputModule for compatibility");
            }
#endif
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
        }
    }
}