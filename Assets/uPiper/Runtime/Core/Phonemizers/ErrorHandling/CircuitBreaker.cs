using System;
using System.Threading;
using UnityEngine;

namespace uPiper.Core.Phonemizers.ErrorHandling
{
    /// <summary>
    /// Circuit Breaker pattern implementation to prevent cascading failures.
    /// </summary>
    public class CircuitBreaker : ICircuitBreaker
    {
        private readonly object lockObject = new();
        private readonly int failureThreshold;
        private readonly TimeSpan timeout;
        private readonly TimeSpan halfOpenTestInterval;

        private CircuitState state = CircuitState.Closed;
        private int failureCount;
        private DateTime lastFailureTime;
        private DateTime lastStateChangeTime;

        /// <summary>
        /// Gets the current state of the circuit breaker.
        /// </summary>
        public CircuitState State
        {
            get
            {
                lock (lockObject)
                {
                    return state;
                }
            }
        }

        /// <summary>
        /// Gets the failure count.
        /// </summary>
        public int FailureCount
        {
            get
            {
                lock (lockObject)
                {
                    return failureCount;
                }
            }
        }

        /// <summary>
        /// Event fired when circuit state changes.
        /// </summary>
        public event Action<CircuitState, CircuitState> StateChanged;

        /// <summary>
        /// Creates a new circuit breaker.
        /// </summary>
        /// <param name="failureThreshold">Number of failures before opening.</param>
        /// <param name="timeout">Time to wait before attempting to close.</param>
        /// <param name="halfOpenTestInterval">Interval for half-open tests.</param>
        public CircuitBreaker(
            int failureThreshold = 3,
            TimeSpan? timeout = null,
            TimeSpan? halfOpenTestInterval = null)
        {
            this.failureThreshold = failureThreshold;
            this.timeout = timeout ?? TimeSpan.FromSeconds(30);
            this.halfOpenTestInterval = halfOpenTestInterval ?? TimeSpan.FromSeconds(5);
            lastStateChangeTime = DateTime.UtcNow;
        }

        /// <inheritdoc/>
        public bool CanExecute()
        {
            lock (lockObject)
            {
                UpdateState();
                return state != CircuitState.Open;
            }
        }

        /// <inheritdoc/>
        public void OnSuccess()
        {
            lock (lockObject)
            {
                if (state == CircuitState.HalfOpen)
                {
                    // Success in half-open state moves to closed
                    TransitionTo(CircuitState.Closed);
                    failureCount = 0;
                    Debug.Log($"Circuit breaker closed after successful test");
                }
                else if (state == CircuitState.Closed)
                {
                    // Reset failure count on success
                    failureCount = 0;
                }
            }
        }

        /// <inheritdoc/>
        public void OnFailure(Exception exception = null)
        {
            lock (lockObject)
            {
                lastFailureTime = DateTime.UtcNow;
                failureCount++;

                if (state == CircuitState.HalfOpen)
                {
                    // Failure in half-open state immediately opens
                    TransitionTo(CircuitState.Open);
                    Debug.Log($"[CircuitBreaker] Opened after half-open test failure");
                }
                else if (state == CircuitState.Closed && failureCount >= failureThreshold)
                {
                    // Threshold reached, open circuit
                    TransitionTo(CircuitState.Open);
                    Debug.Log($"[CircuitBreaker] Opened after {failureCount} failures");
                }

                if (exception != null)
                {
                    // Use Log instead of LogError to avoid test runner warnings
                    Debug.Log($"[CircuitBreaker] Recording failure: {exception.Message}");
                }
            }
        }

        /// <inheritdoc/>
        public void Reset()
        {
            lock (lockObject)
            {
                TransitionTo(CircuitState.Closed);
                failureCount = 0;
                lastFailureTime = default;
                Debug.Log("Circuit breaker manually reset");
            }
        }

        /// <inheritdoc/>
        public CircuitBreakerStatistics GetStatistics()
        {
            lock (lockObject)
            {
                return new CircuitBreakerStatistics
                {
                    State = state,
                    FailureCount = failureCount,
                    LastFailureTime = lastFailureTime,
                    LastStateChangeTime = lastStateChangeTime,
                    TimeUntilHalfOpen = GetTimeUntilHalfOpen()
                };
            }
        }

        private void UpdateState()
        {
            if (state == CircuitState.Open)
            {
                var timeSinceLastFailure = DateTime.UtcNow - lastFailureTime;
                if (timeSinceLastFailure >= timeout)
                {
                    // Transition to half-open for testing
                    TransitionTo(CircuitState.HalfOpen);
                    Debug.Log("Circuit breaker moved to half-open state");
                }
            }
        }

        private void TransitionTo(CircuitState newState)
        {
            if (state != newState)
            {
                var oldState = state;
                state = newState;
                lastStateChangeTime = DateTime.UtcNow;
                StateChanged?.Invoke(oldState, newState);
            }
        }

        private TimeSpan? GetTimeUntilHalfOpen()
        {
            if (state != CircuitState.Open)
                return null;

            var timeSinceLastFailure = DateTime.UtcNow - lastFailureTime;
            var remaining = timeout - timeSinceLastFailure;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Interface for circuit breaker implementations.
    /// </summary>
    public interface ICircuitBreaker
    {
        /// <summary>
        /// Checks if an operation can be executed.
        /// </summary>
        public bool CanExecute();

        /// <summary>
        /// Records a successful operation.
        /// </summary>
        public void OnSuccess();

        /// <summary>
        /// Records a failed operation.
        /// </summary>
        public void OnFailure(Exception exception = null);

        /// <summary>
        /// Manually resets the circuit breaker.
        /// </summary>
        public void Reset();

        /// <summary>
        /// Gets circuit breaker statistics.
        /// </summary>
        public CircuitBreakerStatistics GetStatistics();
    }

    /// <summary>
    /// Circuit breaker states.
    /// </summary>
    public enum CircuitState
    {
        /// <summary>
        /// Normal operation, requests pass through.
        /// </summary>
        Closed,

        /// <summary>
        /// Requests are blocked.
        /// </summary>
        Open,

        /// <summary>
        /// Limited requests allowed for testing.
        /// </summary>
        HalfOpen
    }

    /// <summary>
    /// Circuit breaker statistics.
    /// </summary>
    public class CircuitBreakerStatistics
    {
        public CircuitState State { get; set; }
        public int FailureCount { get; set; }
        public DateTime LastFailureTime { get; set; }
        public DateTime LastStateChangeTime { get; set; }
        public TimeSpan? TimeUntilHalfOpen { get; set; }
    }
}