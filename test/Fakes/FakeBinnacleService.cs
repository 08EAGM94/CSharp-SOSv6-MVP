using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;

namespace test.Fakes;

public class FakeBinnacleService : IBinnacleService
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

    public int TotalCalls =>
        AddCalls
        + GetCalls
        + GetAllCalls
        + UpdateCalls
        + UpdateVisibilityCalls
        + FollowupPartialCalls
        + ResetActivitiesCalls
        + CancelBinnacleCalls
        + FinishBinnacleCalls;

    public BinnacleDTO? LastAddedDto { get; private set; }
    public BinnacleDTO? LastGetDto { get; private set; }
    public BinnacleDTO? LastGetAllDto { get; private set; }
    public int? LastGetAllPage { get; private set; }
    public int? LastGetAllElemsKey { get; private set; }
    public Dictionary<string, string>? LastGetAllBinnFilter { get; private set; }
    public string? LastGetAllControllerAction { get; private set; }
    public BinnacleDTO? LastUpdatedDto { get; private set; }
    public BinnacleDTO? LastUpdatedVisibilityDto { get; private set; }
    public BinnacleDTO? LastFollowupPartialDto { get; private set; }
    public BinnacleDTO? LastResetActivitiesDto { get; private set; }
    public BinnacleDTO? LastCancelledDto { get; private set; }
    public BinnacleDTO? LastFinishedDto { get; private set; }

    public BinnacleDTO GetResult { get; set; } = new BinnacleDTO();
    public PaginationResult GetAllResult { get; set; } = new PaginationResult();
    public Exception? ExceptionToThrow { get; set; }

    public Task AddAsyncInfo(BinnacleDTO entity)
    {
        AddCalls++;
        LastAddedDto = entity;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task<BinnacleDTO> GetAsyncInfo(BinnacleDTO entity)
    {
        GetCalls++;
        LastGetDto = entity;

        return ExceptionToThrow is null
            ? Task.FromResult(GetResult)
            : Task.FromException<BinnacleDTO>(ExceptionToThrow);
    }

    public Task<PaginationResult> GetAsyncAllInfo(BinnacleDTO? entity = null, int? page = null, int? elemsKey = null, Dictionary<string, string>? binnFilter = null, string? controllerAction = null)
    {
        GetAllCalls++;
        LastGetAllDto = entity;
        LastGetAllPage = page;
        LastGetAllElemsKey = elemsKey;
        LastGetAllBinnFilter = binnFilter;
        LastGetAllControllerAction = controllerAction;

        return ExceptionToThrow is null
            ? Task.FromResult(GetAllResult)
            : Task.FromException<PaginationResult>(ExceptionToThrow);
    }

    public Task UpdateAsyncInfo(BinnacleDTO entity)
    {
        UpdateCalls++;
        LastUpdatedDto = entity;

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

    public Task FollowupPartialAsync(BinnacleDTO entity)
    {
        FollowupPartialCalls++;
        LastFollowupPartialDto = entity;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task ResetActivitiesAsync(BinnacleDTO entity)
    {
        ResetActivitiesCalls++;
        LastResetActivitiesDto = entity;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task CancelBinnacleAsync(BinnacleDTO entity)
    {
        CancelBinnacleCalls++;
        LastCancelledDto = entity;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task FinishBinnacleAsync(BinnacleDTO entity)
    {
        FinishBinnacleCalls++;
        LastFinishedDto = entity;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }
}
