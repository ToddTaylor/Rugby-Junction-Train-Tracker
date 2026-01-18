using AutoMapper;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public interface ISubdivisionTrackageRightService
    {
        Task<IEnumerable<SubdivisionTrackageRightDTO>> GetByFromSubdivisionAsync(int fromSubdivisionID);
        Task<SubdivisionTrackageRightDTO?> GetByIdAsync(int id);
        Task<SubdivisionTrackageRightDTO> AddAsync(CreateSubdivisionTrackageRightDTO dto);
        Task DeleteAsync(int id);
        Task ReplaceTrackageRightsAsync(int fromSubdivisionID, IEnumerable<int> toSubdivisionIDs);
    }

    public class SubdivisionTrackageRightService : ISubdivisionTrackageRightService
    {
        private readonly ISubdivisionTrackageRightRepository _repository;
        private readonly ISubdivisionRepository _subdivisionRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<SubdivisionTrackageRightService> _logger;

        public SubdivisionTrackageRightService(
            ISubdivisionTrackageRightRepository repository,
            ISubdivisionRepository subdivisionRepository,
            IMapper mapper,
            ILogger<SubdivisionTrackageRightService> logger)
        {
            _repository = repository;
            _subdivisionRepository = subdivisionRepository;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<SubdivisionTrackageRightDTO>> GetByFromSubdivisionAsync(int fromSubdivisionID)
        {
            var trackageRights = await _repository.GetByFromSubdivisionAsync(fromSubdivisionID);
            return trackageRights.Select(str => new SubdivisionTrackageRightDTO
            {
                ID = str.ID,
                FromSubdivisionID = str.FromSubdivisionID,
                ToSubdivisionID = str.ToSubdivisionID,
                FromSubdivisionName = str.FromSubdivision?.Name,
                ToSubdivisionName = str.ToSubdivision?.Name,
                ToRailroadName = str.ToSubdivision?.Railroad?.Name
            });
        }

        public async Task<SubdivisionTrackageRightDTO?> GetByIdAsync(int id)
        {
            var trackageRight = await _repository.GetByIdAsync(id);
            if (trackageRight == null) return null;

            return new SubdivisionTrackageRightDTO
            {
                ID = trackageRight.ID,
                FromSubdivisionID = trackageRight.FromSubdivisionID,
                ToSubdivisionID = trackageRight.ToSubdivisionID,
                FromSubdivisionName = trackageRight.FromSubdivision?.Name,
                ToSubdivisionName = trackageRight.ToSubdivision?.Name,
                ToRailroadName = trackageRight.ToSubdivision?.Railroad?.Name
            };
        }

        public async Task<SubdivisionTrackageRightDTO> AddAsync(CreateSubdivisionTrackageRightDTO dto)
        {
            try
            {
                // Validate that the two subdivisions are on different railroads
                var fromSub = await _subdivisionRepository.GetByIdAsync(dto.FromSubdivisionID);
                var toSub = await _subdivisionRepository.GetByIdAsync(dto.ToSubdivisionID);

                if (fromSub == null || toSub == null)
                {
                    throw new ArgumentException("One or both subdivisions not found");
                }

                if (fromSub.RailroadID == toSub.RailroadID)
                {
                    throw new ArgumentException("Cannot add trackage rights between subdivisions of the same railroad");
                }

                var trackageRight = new SubdivisionTrackageRight
                {
                    FromSubdivisionID = dto.FromSubdivisionID,
                    ToSubdivisionID = dto.ToSubdivisionID
                };

                var result = await _repository.AddAsync(trackageRight);
                _logger.LogDebug("Added trackage right from subdivision {FromID} to {ToID}", dto.FromSubdivisionID, dto.ToSubdivisionID);

                return new SubdivisionTrackageRightDTO
                {
                    ID = result.ID,
                    FromSubdivisionID = result.FromSubdivisionID,
                    ToSubdivisionID = result.ToSubdivisionID,
                    FromSubdivisionName = fromSub?.Name,
                    ToSubdivisionName = toSub?.Name,
                    ToRailroadName = toSub?.Railroad?.Name
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding trackage right");
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            try
            {
                await _repository.DeleteAsync(id);
                _logger.LogDebug("Deleted trackage right {ID}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting trackage right {ID}", id);
                throw;
            }
        }

        public async Task ReplaceTrackageRightsAsync(int fromSubdivisionID, IEnumerable<int> toSubdivisionIDs)
        {
            try
            {
                // Delete all existing trackage rights for this subdivision
                await _repository.DeleteByFromSubdivisionAsync(fromSubdivisionID);

                // Add new ones
                foreach (var toSubID in toSubdivisionIDs)
                {
                    await AddAsync(new CreateSubdivisionTrackageRightDTO
                    {
                        FromSubdivisionID = fromSubdivisionID,
                        ToSubdivisionID = toSubID
                    });
                }

                _logger.LogDebug("Replaced trackage rights for subdivision {ID}", fromSubdivisionID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replacing trackage rights for subdivision {ID}", fromSubdivisionID);
                throw;
            }
        }
    }
}
