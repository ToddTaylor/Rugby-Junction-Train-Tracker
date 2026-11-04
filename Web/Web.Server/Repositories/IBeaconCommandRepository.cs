using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IBeaconCommandRepository
    {
        Task<BeaconCommand> AddAsync(BeaconCommand command);

        Task<BeaconCommand?> GetByIdAsync(int id);

        /// <summary>
        /// Commands for every beacon requested at or after the given time, newest first.
        /// </summary>
        Task<IReadOnlyList<BeaconCommand>> GetRequestedSinceAsync(DateTime since);

        /// <summary>
        /// The beacon's commands requested at or after the given time, newest first.
        /// </summary>
        Task<IReadOnlyList<BeaconCommand>> GetForBeaconSinceAsync(int beaconId, DateTime since);

        /// <summary>
        /// Saves the command's delivery, progress, result and cancellation fields.
        /// </summary>
        Task UpdateAsync(BeaconCommand command);
    }
}
