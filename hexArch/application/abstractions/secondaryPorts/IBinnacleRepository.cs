using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace HexArch.Application.Abstractions.SecondaryPorts;

public interface IBinnacleRepository
{
    Task AddAsyncInfo(BinnacleEntity entity);

    Task<BinnacleDTO> GetAsyncInfo(BinnacleEntity entity);

    Task<PaginationResult> GetAsyncAllInfo(BinnacleEntity? entity = null, int? page = null, int? elemsKey = null, Dictionary<string, string>? binnFilter = null, string? controllerAction = null);

    Task UpdateAsyncInfo(BinnacleEntity entity);

    Task UpdateAsyncVisibility(BinnacleDTO dto);

    Task FollowupPartialAsync(BinnacleEntity entity);

    Task ResetActivitiesAsync(BinnacleEntity entity);

    Task CancelBinnacleAsync(BinnacleEntity entity);

    Task FinishBinnacleAsync(BinnacleEntity entity);
}