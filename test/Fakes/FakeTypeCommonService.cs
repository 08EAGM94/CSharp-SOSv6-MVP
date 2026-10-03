using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;

namespace test.Fakes;

public class FakeTypeCommonService : ICommonService<TypeDTO>
{
    public int AddAsyncInfoCalls { get; private set; }
    public int GetAsyncInfoCalls { get; private set; }
    public int GetAsyncAllInfoCalls { get; private set; }
    public int UpdateAsyncInfoCalls { get; private set; }
    public int UpdateAsyncVisibilityCalls { get; private set; }

    public TypeDTO? LastAddedDto { get; private set; }
    public ContactDTO? LastAddedContact { get; private set; }
    public TypeDTO? LastRequestedDto { get; private set; }
    public TypeDTO? LastUpdatedDto { get; private set; }
    public TypeDTO? LastVisibilityDto { get; private set; }

    public TypeDTO InfoResult { get; set; } = new TypeDTO();
    public IEnumerable<TypeDTO> AllResult { get; set; } = [];
    public Exception? ExceptionToThrow { get; set; }

    public Task AddAsyncInfo(TypeDTO dto, ContactDTO? contact = null)
    {
        AddAsyncInfoCalls++;
        LastAddedDto = dto;
        LastAddedContact = contact;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task<TypeDTO> GetAsyncInfo(TypeDTO dto)
    {
        GetAsyncInfoCalls++;
        LastRequestedDto = dto;

        return ExceptionToThrow is null
            ? Task.FromResult(InfoResult)
            : Task.FromException<TypeDTO>(ExceptionToThrow);
    }

    public Task<IEnumerable<TypeDTO>> GetAsyncAllInfo()
    {
        GetAsyncAllInfoCalls++;

        return ExceptionToThrow is null
            ? Task.FromResult(AllResult)
            : Task.FromException<IEnumerable<TypeDTO>>(ExceptionToThrow);
    }

    public Task UpdateAsyncInfo(TypeDTO dto)
    {
        UpdateAsyncInfoCalls++;
        LastUpdatedDto = dto;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task UpdateAsyncVisibility(TypeDTO dto)
    {
        UpdateAsyncVisibilityCalls++;
        LastVisibilityDto = dto;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }
}