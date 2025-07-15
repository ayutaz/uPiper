using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using NUnit.Framework;

namespace uPiper.Tests.Runtime.Helpers
{
    /// <summary>
    /// Helper methods for testing async operations in Unity Test Framework
    /// </summary>
    public static class AsyncTestHelpers
    {
        /// <summary>
        /// Run an async task and wait for completion without blocking
        /// </summary>
        public static IEnumerator RunAsync(Task task, float timeout = 5f)
        {
            if (task == null)
            {
                Assert.Fail("Task is null");
                yield break;
            }
            
            var startTime = Time.realtimeSinceStartup;
            
            // Poll the task completion state
            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup - startTime > timeout)
                {
                    Assert.Fail($"Task did not complete within {timeout} seconds");
                    yield break;
                }
                
                // Yield control back to Unity Test Framework
                yield return null;
            }
            
            // Handle task completion states
            if (task.IsFaulted)
            {
                if (task.Exception != null)
                {
                    // Properly unwrap and throw the exception
                    var ex = task.Exception.GetBaseException();
                    throw ex;
                }
                Assert.Fail("Task faulted without exception");
            }
            else if (task.IsCanceled)
            {
                throw new OperationCanceledException("Task was canceled");
            }
            
            // Task completed successfully
        }
        
        /// <summary>
        /// Run an async task that returns a value
        /// </summary>
        public static IEnumerator RunAsync<T>(Task<T> task, Action<T> onComplete, float timeout = 5f)
        {
            yield return RunAsync(task, timeout);
            
            if (task.IsCompletedSuccessfully)
            {
                onComplete(task.Result);
            }
        }
        
        /// <summary>
        /// Run an async task and expect it to throw an exception
        /// </summary>
        public static IEnumerator RunAsyncExpectException<TException>(Task task, float timeout = 5f) 
            where TException : Exception
        {
            var startTime = Time.realtimeSinceStartup;
            
            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup - startTime > timeout)
                {
                    Assert.Fail($"Task did not complete within {timeout} seconds");
                    yield break;
                }
                
                yield return null;
            }
            
            if (task.IsFaulted)
            {
                var baseException = task.Exception?.GetBaseException();
                if (baseException is TException)
                {
                    // Expected exception type
                    yield break;
                }
                else
                {
                    Assert.Fail($"Expected {typeof(TException).Name} but got {baseException?.GetType().Name}: {baseException?.Message}");
                }
            }
            else
            {
                Assert.Fail($"Expected {typeof(TException).Name} but task completed successfully");
            }
        }
        
        /// <summary>
        /// Consume an async enumerable and collect results
        /// </summary>
        public static IEnumerator ConsumeAsyncEnumerable<T>(
            IAsyncEnumerable<T> enumerable, 
            List<T> results, 
            CancellationToken cancellationToken = default,
            float timeout = 10f)
        {
            var enumerator = enumerable.GetAsyncEnumerator(cancellationToken);
            var startTime = Time.realtimeSinceStartup;
            bool completed = false;
            
            // Main enumeration logic
            while (!completed)
            {
                var moveNextTask = enumerator.MoveNextAsync();
                
                while (!moveNextTask.IsCompleted)
                {
                    if (Time.realtimeSinceStartup - startTime > timeout)
                    {
                        // Clean up before failing
                        var cleanupTask = enumerator.DisposeAsync();
                        while (!cleanupTask.IsCompleted)
                        {
                            yield return null;
                        }
                        
                        Assert.Fail($"Async enumeration did not complete within {timeout} seconds");
                        yield break;
                    }
                    
                    yield return null;
                }
                
                if (!moveNextTask.Result)
                {
                    completed = true;
                }
                else
                {
                    results.Add(enumerator.Current);
                }
            }
            
            // Dispose enumerator
            var disposeTask = enumerator.DisposeAsync();
            while (!disposeTask.IsCompleted)
            {
                yield return null;
            }
        }
        
        /// <summary>
        /// Run multiple async tasks concurrently
        /// </summary>
        public static IEnumerator RunAllAsync(Task[] tasks, float timeout = 10f)
        {
            var startTime = Time.realtimeSinceStartup;
            
            while (!Task.WhenAll(tasks).IsCompleted)
            {
                if (Time.realtimeSinceStartup - startTime > timeout)
                {
                    Assert.Fail($"Tasks did not complete within {timeout} seconds");
                    yield break;
                }
                
                yield return null;
            }
            
            // Check for faulted tasks
            foreach (var task in tasks)
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    throw task.Exception.GetBaseException();
                }
            }
        }
    }
}