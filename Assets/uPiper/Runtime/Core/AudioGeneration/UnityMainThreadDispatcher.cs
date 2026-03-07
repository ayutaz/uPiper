using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// Unity APIをメインスレッドから呼び出すためのヘルパークラス
    /// </summary>
    public static class UnityMainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> _actions = new();
        private static bool _initialized;
        private static int _mainThreadId;

        /// <summary>
        /// Check if current thread is the main thread
        /// </summary>
        private static bool IsMainThread => _mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            if (_initialized) return;

            var gameObject = new GameObject("UnityMainThreadDispatcher");
            gameObject.AddComponent<UnityMainThreadDispatcherComponent>();

            // DontDestroyOnLoadはPlayModeでのみ使用
            if (Application.isPlaying)
            {
                GameObject.DontDestroyOnLoad(gameObject);
            }

            _initialized = true;
        }

        /// <summary>
        /// メインスレッドでアクションを実行する
        /// </summary>
        public static Task RunOnMainThreadAsync(Action action, CancellationToken cancellationToken = default)
        {
            // If already on main thread, execute directly to avoid deadlock
            if (IsMainThread)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return Task.FromCanceled(cancellationToken);

                    action();
                    return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }
            }

            if (!_initialized)
                Initialize();

            var tcs = new TaskCompletionSource<bool>();

            _actions.Enqueue(() =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.SetCanceled();
                        return;
                    }

                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// メインスレッドで関数を実行して結果を返す
        /// </summary>
        public static Task<T> RunOnMainThreadAsync<T>(Func<T> func, CancellationToken cancellationToken = default)
        {
            // If already on main thread, execute directly to avoid deadlock
            if (IsMainThread)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return Task.FromCanceled<T>(cancellationToken);

                    PiperLogger.LogDebug("[UnityMainThreadDispatcher] Already on main thread, executing directly");
                    var result = func();
                    return Task.FromResult(result);
                }
                catch (Exception ex)
                {
                    PiperLogger.LogError($"[UnityMainThreadDispatcher] Error executing function: {ex.Message}");
                    return Task.FromException<T>(ex);
                }
            }

            if (!_initialized)
            {
                PiperLogger.LogDebug("[UnityMainThreadDispatcher] Initializing...");
                Initialize();
            }

            var tcs = new TaskCompletionSource<T>();

            _actions.Enqueue(() =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.SetCanceled();
                        return;
                    }

                    PiperLogger.LogDebug("[UnityMainThreadDispatcher] Executing function on main thread");
                    var result = func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    PiperLogger.LogError($"[UnityMainThreadDispatcher] Error executing function: {ex.Message}");
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        private class UnityMainThreadDispatcherComponent : MonoBehaviour
        {
            private void Update()
            {
                while (_actions.TryDequeue(out var action))
                {
                    action?.Invoke();
                }
            }
        }
    }
}