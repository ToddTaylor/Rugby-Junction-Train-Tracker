using Web.Server.Entities;
using Web.Server.Repositories;

namespace Web.Server.Services.Rules
{
    internal static class RecentTelemetry
    {
        /// <summary>
        /// Recent non-discarded telemetry for the train being evaluated, newest first, with the
        /// telemetry under evaluation at index 0.
        ///
        /// When the context names a map pin, history is that pin's own telemetry. Two trains on one
        /// railroad can report the same address ID, and mixing their readings would make one train
        /// appear to jump between their positions. Without a map pin, falls back to every reading for
        /// the address on the railroad.
        /// </summary>
        public static async Task<List<Telemetry>> GetAsync(
            ITelemetryRepository telemetryRepository,
            TelemetryRuleContext context,
            DateTime sinceUtc)
        {
            if (context.MapPinId == null)
            {
                return await telemetryRepository
                    .GetRecentsWithinTimeOffsetAsync(context.Telemetry.AddressID, context.RailroadId, sinceUtc)
                    ?? [];
            }

            // The telemetry under evaluation is not stamped with its map pin until it is accepted,
            // so the query never returns it and it is placed at the front explicitly.
            var history = await telemetryRepository.GetRecentsForMapPinAsync(context.MapPinId.Value, sinceUtc) ?? [];

            return [context.Telemetry, .. history];
        }
    }
}
