using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;

namespace test.Fakes;

public class FakeEnterpriseCommonService : ICommonService<EnterpriseDTO>
{
    public int AddAsyncInfoCalls { get; private set; }
    public int GetAsyncInfoCalls { get; private set; }
    public int GetAsyncAllInfoCalls { get; private set; }
    public int UpdateAsyncInfoCalls { get; private set; }
    public int UpdateAsyncVisibilityCalls { get; private set; }

    public EnterpriseDTO? LastAddedDto { get; private set; }
    public ContactDTO? LastAddedContact { get; private set; }
    public EnterpriseDTO? LastRequestedDto { get; private set; }
    public EnterpriseDTO? LastUpdatedDto { get; private set; }
    public EnterpriseDTO? LastVisibilityDto { get; private set; }

    public EnterpriseDTO InfoResult { get; set; } = new EnterpriseDTO();
    public IEnumerable<EnterpriseDTO> AllResult { get; set; } = [];
    public Exception? ExceptionToThrow { get; set; }

    public Task AddAsyncInfo(EnterpriseDTO dto, ContactDTO? contact = null)
    {
        AddAsyncInfoCalls++;
        LastAddedDto = dto;
        LastAddedContact = contact;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task<EnterpriseDTO> GetAsyncInfo(EnterpriseDTO dto)
    {
        GetAsyncInfoCalls++;
        LastRequestedDto = dto;

        return ExceptionToThrow is null
            ? Task.FromResult(InfoResult)
            : Task.FromException<EnterpriseDTO>(ExceptionToThrow);
    }

    public Task<IEnumerable<EnterpriseDTO>> GetAsyncAllInfo()
    {
        GetAsyncAllInfoCalls++;

        return ExceptionToThrow is null
            ? Task.FromResult(AllResult)
            : Task.FromException<IEnumerable<EnterpriseDTO>>(ExceptionToThrow);
    }

    public Task UpdateAsyncInfo(EnterpriseDTO dto)
    {
        UpdateAsyncInfoCalls++;
        LastUpdatedDto = dto;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task UpdateAsyncVisibility(EnterpriseDTO dto)
    {
        UpdateAsyncVisibilityCalls++;
        LastVisibilityDto = dto;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }
}