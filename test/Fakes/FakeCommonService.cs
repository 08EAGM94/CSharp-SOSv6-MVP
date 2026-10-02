using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;

namespace test.Fakes;

public class FakeCommonService : ICommonService<UserDTO>
{
    public int AddAsyncInfoCalls { get; private set; }
    public int GetAsyncInfoCalls { get; private set; }
    public int GetAsyncAllInfoCalls { get; private set; }
    public int UpdateAsyncInfoCalls { get; private set; }
    public int UpdateAsyncVisibilityCalls { get; private set; }

    public UserDTO? LastAddedDto { get; private set; }
    public ContactDTO? LastAddedContact { get; private set; }
    public UserDTO? LastRequestedDto { get; private set; }
    public UserDTO? LastUpdatedDto { get; private set; }
    public UserDTO? LastVisibilityDto { get; private set; }

    public UserDTO InfoResult { get; set; } = new UserDTO();
    public IEnumerable<UserDTO> AllResult { get; set; } = [];
    public Exception? ExceptionToThrow { get; set; }

    public Task AddAsyncInfo(UserDTO dto, ContactDTO? contact = null)
    {
        AddAsyncInfoCalls++;
        LastAddedDto = dto;
        LastAddedContact = contact;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task<UserDTO> GetAsyncInfo(UserDTO dto)
    {
        GetAsyncInfoCalls++;
        LastRequestedDto = dto;

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

    public Task UpdateAsyncInfo(UserDTO dto)
    {
        UpdateAsyncInfoCalls++;
        LastUpdatedDto = dto;

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
