using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace uPiper.Phonemizers.Unity
{
    /// <summary>
    /// Extension methods for Unity coroutine integration
    /// </summary>
    public static class PhonemizerCoroutineExtensions
    {
        /// <summary>
        /// Convert async Task to Unity coroutine
        /// </summary>
        public static IEnumerator AsCoroutine(this Task task, Action<Exception> onError = null)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                var exception = task.Exception?.InnerException ?? task.Exception;
                onError?.Invoke(exception);
                Debug.LogError($"Task failed: {exception}");
            }
        }

        /// <summary>
        /// Convert async Task<T> to Unity coroutine with result callback
        /// </summary>
        public static IEnumerator AsCoroutine<T>(this Task<T> task, Action<T> onComplete, Action<Exception> onError = null)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                var exception = task.Exception?.InnerException ?? task.Exception;
                onError?.Invoke(exception);
                Debug.LogError($"Task failed: {exception}");
            }
            else if (!task.IsCanceled)
            {
                onComplete?.Invoke(task.Result);
            }
        }

        /// <summary>
        /// Yield instruction that waits for a Task to complete
        /// </summary>
        public class TaskYieldInstruction : CustomYieldInstruction
        {
            private readonly Task task;

            public TaskYieldInstruction(Task task)
            {
                this.task = task;
            }

            public override bool keepWaiting => !task.IsCompleted;
        }

        /// <summary>
        /// Create a yield instruction from a Task
        /// </summary>
        public static TaskYieldInstruction AsYieldInstruction(this Task task)
        {
            return new TaskYieldInstruction(task);
        }

        /// <summary>
        /// Run async operation with timeout
        /// </summary>
        public static IEnumerator WithTimeout<T>(this Task<T> task, float timeoutSeconds, Action<T> onComplete, Action onTimeout)
        {
            float elapsedTime = 0;

            while (!task.IsCompleted && elapsedTime < timeoutSeconds)
            {
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
            {
                onComplete?.Invoke(task.Result);
            }
            else if (elapsedTime >= timeoutSeconds)
            {
                onTimeout?.Invoke();
            }
        }

        /// <summary>
        /// Batch process with frame rate limiting
        /// </summary>
        public static IEnumerator ProcessBatchWithFrameLimit<T>(
            System.Collections.Generic.IEnumerable<T> items,
            Func<T, Task> processor,
            int itemsPerFrame = 1,
            Action<float> onProgress = null)
        {
            var itemList = new System.Collections.Generic.List<T>(items);
            var totalItems = itemList.Count;
            var processedItems = 0;

            for (var i = 0; i < itemList.Count; i += itemsPerFrame)
            {
                var batchSize = Math.Min(itemsPerFrame, itemList.Count - i);
                var tasks = new Task[batchSize];

                // Start batch tasks
                for (var j = 0; j < batchSize; j++)
                {
                    tasks[j] = processor(itemList[i + j]);
                }

                // Wait for batch completion
                yield return Task.WhenAll(tasks).AsYieldInstruction();

                processedItems += batchSize;
                onProgress?.Invoke((float)processedItems / totalItems);

                // Yield to maintain frame rate
                yield return null;
            }
        }
    }

    /// <summary>
    /// Unity-specific async operation wrapper
    /// </summary>
    public class UnityAsyncOperation<T>
    {
        private readonly Func<Task<T>> taskFactory;
        private T result;
        private Exception error;
        private bool isCompleted;

        public T Result => result;
        public Exception Error => error;
        public bool IsCompleted => isCompleted;
        public bool HasError => error != null;

        public UnityAsyncOperation(Func<Task<T>> taskFactory)
        {
            this.taskFactory = taskFactory;
        }

        public IEnumerator Execute(Action<T> onComplete = null, Action<Exception> onError = null)
        {
            isCompleted = false;
            error = null;
            result = default;

            var task = taskFactory();

            while (!task.IsCompleted)
            {
                yield return null;
            }

            isCompleted = true;

            if (task.IsFaulted)
            {
                error = task.Exception?.InnerException ?? task.Exception;
                onError?.Invoke(error);
            }
            else if (!task.IsCanceled)
            {
                result = task.Result;
                onComplete?.Invoke(result);
            }
        }
    }
}