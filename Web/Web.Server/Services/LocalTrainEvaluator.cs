using Web.Server.Entities;

namespace Web.Server.Services
{
    /// <summary>
    /// Determines whether an address ID is in a subdivision's local train list.
    /// Shared by any read path that needs to reflect the subdivision's current
    /// local-train configuration rather than a stale, previously-computed value.
    /// </summary>
    public static class LocalTrainEvaluator
    {
        public static bool IsLocalTrain(int addressID, Subdivision? subdivision)
        {
            if (subdivision == null || string.IsNullOrWhiteSpace(subdivision.LocalTrainAddressIDs))
            {
                return false;
            }

            var localAddressIDs = subdivision.LocalTrainAddressIDs
                .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => int.TryParse(id, out var parsed) ? parsed : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();

            return localAddressIDs.Contains(addressID);
        }
    }
}
