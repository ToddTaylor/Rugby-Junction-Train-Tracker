using System.Collections.Concurrent;

namespace Services
{
    /// <summary>
    /// Service to throttle telemetry messages based on AddressID.
    /// Only allows one message per AddressID within a configurable time interval.
    /// Thread-safe for concurrent access.
    /// </summary>
    public class TelemetryThrottleService
    {
        // Thread-safe dictionary to track last sent timestamp for each AddressID
        private readonly ConcurrentDictionary<int, DateTime> _lastSentTimestamps = new();
        private readonly int _throttleIntervalSeconds;

        /// <summary>
        /// Initializes a new instance of the TelemetryThrottleService.
        /// </summary>
        /// <param name="throttleIntervalSeconds">The minimum interval in seconds between messages for the same AddressID.</param>
        public TelemetryThrottleService(int throttleIntervalSeconds)
        {
            _throttleIntervalSeconds = throttleIntervalSeconds;
        }

        /// <summary>
        /// Determines if a message for the given AddressID should be sent based on throttle rules.
        /// If allowed, updates the last sent timestamp for that AddressID.
        /// </summary>
        /// <param name="addressId">The unique AddressID of the telemetry message.</param>
        /// <returns>True if the message should be sent; false if it should be throttled.</returns>
        public bool ShouldSendMessage(int addressId)
        {
            var now = DateTime.UtcNow;

            // Try to get the last sent timestamp for this AddressID
            if (_lastSentTimestamps.TryGetValue(addressId, out var lastSent))
            {
                // Calculate time elapsed since last message
                var elapsedSeconds = (now - lastSent).TotalSeconds;

                // If not enough time has passed, throttle this message
                if (elapsedSeconds < _throttleIntervalSeconds)
                {
                    return false;
                }
            }

            // Update the last sent timestamp for this AddressID
            _lastSentTimestamps[addressId] = now;
            return true;
        }

        /// <summary>
        /// Clears all tracked AddressID timestamps. Useful for testing or reset scenarios.
        /// </summary>
        public void Clear()
        {
            _lastSentTimestamps.Clear();
        }

        /// <summary>
        /// Gets the count of currently tracked AddressIDs.
        /// </summary>
        public int TrackedAddressCount => _lastSentTimestamps.Count;
    }
}
