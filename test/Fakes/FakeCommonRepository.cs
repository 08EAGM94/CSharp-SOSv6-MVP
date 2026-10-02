using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace test.Fakes;

public class FakeCommonRepository : IRepository<UserEntity, UserDTO>
{
    public int AddAsyncInfoCalls { get; private set; }
    public int GetAsyncInfoCalls { get; private set; }
    public int GetAsyncAllInfoCalls { get; private set; }
    public int UpdateAsyncInfoCalls { get; private set; }
    public int UpdateAsyncVisibilityCalls { get; private set; }

    public UserEntity? LastAddedEntity { get; private set; }
    public ContactEntity? LastAddedContact { get; private set; }
    public UserEntity? LastRequestedEntity { get; private set; }
    public UserEntity? LastUpdatedEntity { get; private set; }
    public UserDTO? LastVisibilityDto { get; private set; }

    public UserDTO InfoResult { get; set; } = new UserDTO();
    public IEnumerable<UserDTO> AllResult { get; set; } = [];
    public Exception? ExceptionToThrow { get; set; }

    public Task AddAsyncInfo(UserEntity entity, ContactEntity? contact = null)
    {
        AddAsyncInfoCalls++;
        LastAddedEntity = entity;
        LastAddedContact = contact;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task<UserDTO> GetAsyncInfo(UserEntity entity)
    {
        GetAsyncInfoCalls++;
        LastRequestedEntity = entity;

        return ExceptionToThrow is null
            ? Task.FromResult(InfoResult)
            : Task.FromException<UserDTO>(ExceptionToThrow);
    }

    public Task<IEnumerable<UserDTO>> GetAsyncAllInfo()
    {
        GetAsyncAllInfoCalls++;

        return ExceptionToThrow is null
            ? Task.FromResult(AllResult)
            : Task.FromException<IEnumerable<UserDTO>>(ExceptionToThrow);
    }

    public Task UpdateAsyncInfo(UserEntity entity)
    {
        UpdateAsyncInfoCalls++;
        LastUpdatedEntity = entity;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task UpdateAsyncVisibility(UserDTO dto)
    {
        UpdateAsyncVisibilityCalls++;
        LastVisibilityDto = dto;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }
}
