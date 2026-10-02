using HexArch.Application.DTOs;

namespace HexArch.Application.Abstractions.PrimaryPorts;

public interface IBinnacleService
{
    Task AddAsyncInfo(BinnacleDTO entity);

    Task<BinnacleDTO> GetAsyncInfo(BinnacleDTO entity);

    Task<PaginationResult> GetAsyncAllInfo(BinnacleDTO? entity = null, int? page = null, int? elemsKey = null, Dictionary<string, string>? binnFilter = null, string? controllerAction = null);

    Task UpdateAsyncInfo(BinnacleDTO entity);

    Task UpdateAsyncVisibility(BinnacleDTO dto);
    Task FollowupPartialAsync(BinnacleDTO entity);

    Task ResetActivitiesAsync(BinnacleDTO entity);

    Task CancelBinnacleAsync(BinnacleDTO entity);

    Task FinishBinnacleAsync(BinnacleDTO entity);
}