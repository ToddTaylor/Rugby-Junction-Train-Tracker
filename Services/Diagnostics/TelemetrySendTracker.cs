using System.Collections.Concurrent;

namespace Services.Diagnostics
{
    /// <summary>
    /// Records telemetry send outcomes per subscriber so the health check can report them.
    /// Send errors are otherwise only written to the console.
    /// </summary>
    public static class TelemetrySendTracker
    {
        private static readonly TimeSpan FailureWindow = TimeSpan.FromHours(1);

        private static readonly ConcurrentDictionary<string, SubscriberSendState> States = new();

        public static void RecordSuccess(string subscriberId, DateTime utcNow)
        {
            var state = States.GetOrAdd(subscriberId, _ => new SubscriberSendState());
            lock (state)
            {
                state.LastSuccessAt = utcNow;
            }
        }

        public static void RecordFailure(string subscriberId, DateTime utcNow)
        {
            var state = States.GetOrAdd(subscriberId, _ => new SubscriberSendState());
            lock (state)
            {
                state.Failures.Add(utcNow);
                Prune(state, utcNow);
            }
        }

        public static (DateTime? LastSuccessAt, int FailuresLastHour) GetSnapshot(string subscriberId, DateTime utcNow)
        {
            if (!States.TryGetValue(subscriberId, out var state))
            {
                return (null, 0);
            }

            lock (state)
            {
                Prune(state, utcNow);
                return (state.LastSuccessAt, state.Failures.Count);
            }
        }

        private static void Prune(SubscriberSendState state, DateTime utcNow)
        {
            state.Failures.RemoveAll(failedAt => failedAt < utcNow - FailureWindow);
        }

        private sealed class SubscriberSendState
        {
            public DateTime? LastSuccessAt { get; set; }
            public List<DateTime> Failures { get; } = new();
        }
    }
}
