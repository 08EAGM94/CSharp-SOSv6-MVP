using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace test.Fakes;

public class FakeTypeRepository : IRepository<TypeEntity, TypeDTO>, ISelectRepository<TypeDTO>
{
    public int AddAsyncInfoCalls { get; private set; }
    public int GetAsyncInfoCalls { get; private set; }
    public int GetAsyncAllInfoCalls { get; private set; }
    public int UpdateAsyncInfoCalls { get; private set; }
    public int UpdateAsyncVisibilityCalls { get; private set; }
    public int GetAsyncInfoForSelectsCalls { get; private set; }

    public TypeEntity? LastAddedEntity { get; private set; }
    public ContactEntity? LastAddedContact { get; private set; }
    public TypeEntity? LastRequestedEntity { get; private set; }
    public TypeEntity? LastUpdatedEntity { get; private set; }
    public TypeDTO? LastVisibilityDto { get; private set; }

    public TypeDTO InfoResult { get; set; } = new TypeDTO();
    public IEnumerable<TypeDTO> AllResult { get; set; } = [];
    public IEnumerable<TypeDTO> SelectResult { get; set; } = [];
    public Exception? ExceptionToThrow { get; set; }

    public Task AddAsyncInfo(TypeEntity entity, ContactEntity? contact = null)
    {
        AddAsyncInfoCalls++;
        LastAddedEntity = entity;
        LastAddedContact = contact;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task<TypeDTO> GetAsyncInfo(TypeEntity entity)
    {
        GetAsyncInfoCalls++;
        LastRequestedEntity = entity;

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

    public Task UpdateAsyncInfo(TypeEntity entity)
    {
        UpdateAsyncInfoCalls++;
        LastUpdatedEntity = entity;

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

    public Task<IEnumerable<TypeDTO>> GetAsyncInfoForSelects()
    {
        GetAsyncInfoForSelectsCalls++;

        return ExceptionToThrow is null
            ? Task.FromResult(SelectResult)
            : Task.FromException<IEnumerable<TypeDTO>>(ExceptionToThrow);
    }
}