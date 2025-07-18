using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace uPiper.Tests.Runtime.Helpers
{
    /// <summary>
    /// Helper methods for testing async operations synchronously in Unity Test Framework
    /// </summary>
    public static class SyncTestHelpers
    {
        /// <summary>
        /// Run an async task synchronously with proper error handling
        /// </summary>
        public static void RunSync(Func<Task> asyncFunc, int timeoutMs = 5000)
        {
            try
            {
                var task = Task.Run(async () => await asyncFunc());
                if (!task.Wait(timeoutMs))
                {
                    throw new TimeoutException($"Task did not complete within {timeoutMs}ms");
                }

                if (task.IsFaulted)
                {
                    throw task.Exception.GetBaseException();
                }
            }
            catch (AggregateException ae)
            {
                throw ae.GetBaseException();
            }
        }

        /// <summary>
        /// Run an async task that returns a value synchronously
        /// </summary>
        public static T RunSync<T>(Func<Task<T>> asyncFunc, int timeoutMs = 5000)
        {
            try
            {
                var task = Task.Run(async () => await asyncFunc());
                if (!task.Wait(timeoutMs))
                {
                    throw new TimeoutException($"Task did not complete within {timeoutMs}ms");
                }

                if (task.IsFaulted)
                {
                    throw task.Exception.GetBaseException();
                }

                return task.Result;
            }
            catch (AggregateException ae)
            {
                throw ae.GetBaseException();
            }
        }

        /// <summary>
        /// Run an async task and expect it to throw an exception
        /// </summary>
        public static void RunSyncExpectException<TException>(Func<Task> asyncFunc, int timeoutMs = 5000)
            where TException : Exception
        {
            try
            {
                RunSync(asyncFunc, timeoutMs);
                Assert.Fail($"Expected {typeof(TException).Name} but no exception was thrown");
            }
            catch (TException)
            {
                // Expected exception
            }
            catch (Exception ex)
            {
                Assert.Fail($"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}