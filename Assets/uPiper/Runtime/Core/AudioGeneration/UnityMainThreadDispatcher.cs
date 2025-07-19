using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// Unity APIをメインスレッドから呼び出すためのヘルパークラス
    /// </summary>
    public static class UnityMainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_initialized) return;

            var gameObject = new GameObject("UnityMainThreadDispatcher");
            gameObject.AddComponent<UnityMainThreadDispatcherComponent>();
            GameObject.DontDestroyOnLoad(gameObject);
            _initialized = true;
        }

        /// <summary>
        /// メインスレッドでアクションを実行する
        /// </summary>
        public static Task RunOnMainThreadAsync(Action action, CancellationToken cancellationToken = default)
        {
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

        private class UnityMainThreadDispatcherComponent : MonoBehaviour
        {
            void Update()
            {
                while (_actions.TryDequeue(out var action))
                {
                    action?.Invoke();
                }
            }
        }
    }
}