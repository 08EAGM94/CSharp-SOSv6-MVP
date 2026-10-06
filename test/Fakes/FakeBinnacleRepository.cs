using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace test.Fakes;

public class FakeBinnacleRepository : IBinnacleRepository
{
    public int AddCalls { get; private set; }
    public int GetCalls { get; private set; }
    public int GetAllCalls { get; private set; }
    public int UpdateCalls { get; private set; }
    public int UpdateVisibilityCalls { get; private set; }
    public int FollowupPartialCalls { get; private set; }
    public int ResetActivitiesCalls { get; private set; }
    public int CancelBinnacleCalls { get; private set; }
    public int FinishBinnacleCalls { get; private set; }

    public BinnacleEntity? LastAddedEntity { get; private set; }
    public BinnacleEntity? LastGetEntity { get; private set; }
    public BinnacleEntity? LastGetAllEntity { get; private set; }
    public int? LastGetAllPage { get; private set; }
    public int? LastGetAllElemsKey { get; private set; }
    public Dictionary<string, string>? LastGetAllBinnFilter { get; private set; }
    public string? LastGetAllControllerAction { get; private set; }
    public BinnacleEntity? LastUpdatedEntity { get; private set; }
    public BinnacleDTO? LastUpdatedVisibilityDto { get; private set; }
    public BinnacleEntity? LastFollowupPartialEntity { get; private set; }
    public BinnacleEntity? LastResetActivitiesEntity { get; private set; }
    public BinnacleEntity? LastCancelledEntity { get; private set; }
    public BinnacleEntity? LastFinishedEntity { get; private set; }

    public BinnacleDTO GetResult { get; set; } = new BinnacleDTO();
    public PaginationResult GetAllResult { get; set; } = new PaginationResult();
    public Exception? ExceptionToThrow { get; set; }

    public Task AddAsyncInfo(BinnacleEntity entity)
    {
        AddCalls++;
        LastAddedEntity = entity;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task<BinnacleDTO> GetAsyncInfo(BinnacleEntity entity)
    {
        GetCalls++;
        LastGetEntity = entity;

        return ExceptionToThrow is null
            ? Task.FromResult(GetResult)
            : Task.FromException<BinnacleDTO>(ExceptionToThrow);
    }

    public Task<PaginationResult> GetAsyncAllInfo(BinnacleEntity? entity = null, int? page = null, int? elemsKey = null, Dictionary<string, string>? binnFilter = null, string? controllerAction = null)
    {
        GetAllCalls++;
        LastGetAllEntity = entity;
        LastGetAllPage = page;
        LastGetAllElemsKey = elemsKey;
        LastGetAllBinnFilter = binnFilter;
        LastGetAllControllerAction = controllerAction;

        return ExceptionToThrow is null
            ? Task.FromResult(GetAllResult)
            : Task.FromException<PaginationResult>(ExceptionToThrow);
    }

    public Task UpdateAsyncInfo(BinnacleEntity entity)
    {
        UpdateCalls++;
        LastUpdatedEntity = entity;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task UpdateAsyncVisibility(BinnacleDTO dto)
    {
        UpdateVisibilityCalls++;
        LastUpdatedVisibilityDto = dto;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task FollowupPartialAsync(BinnacleEntity entity)
    {
        FollowupPartialCalls++;
        LastFollowupPartialEntity = entity;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task ResetActivitiesAsync(BinnacleEntity entity)
    {
        ResetActivitiesCalls++;
        LastResetActivitiesEntity = entity;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task CancelBinnacleAsync(BinnacleEntity entity)
    {
        CancelBinnacleCalls++;
        LastCancelledEntity = entity;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task FinishBinnacleAsync(BinnacleEntity entity)
    {
        FinishBinnacleCalls++;
        LastFinishedEntity = entity;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }
}
