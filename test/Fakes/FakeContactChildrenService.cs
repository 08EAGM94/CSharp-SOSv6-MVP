using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;

namespace test.Fakes;

public class FakeContactChildrenService : IEnterpriseChildrenService<ContactDTO>
{
    public int AddAsyncChildCalls { get; private set; }
    public int GetAsyncChildCalls { get; private set; }
    public int GetAsyncChildrenByEnterpriseCalls { get; private set; }
    public int GetAsyncChildrenByEnterForSelectCalls { get; private set; }
    public int UpdateAsyncChildCalls { get; private set; }
    public int UpdateAsyncVisibilityCalls { get; private set; }

    public ContactDTO? LastAddedDto { get; private set; }
    public ContactDTO? LastRequestedDto { get; private set; }
    public ContactDTO? LastEnterpriseDto { get; private set; }
    public ContactDTO? LastForSelectDto { get; private set; }
    public ContactDTO? LastUpdatedDto { get; private set; }
    public ContactDTO? LastVisibilityDto { get; private set; }

    public ContactDTO ChildResult { get; set; } = new ContactDTO();
    public IEnumerable<ContactDTO> ChildrenResult { get; set; } = [];
    public IEnumerable<ContactDTO> ForSelectResult { get; set; } = [];
    public Exception? ExceptionToThrow { get; set; }

    public int TotalCalls =>
        AddAsyncChildCalls +
        GetAsyncChildCalls +
        GetAsyncChildrenByEnterpriseCalls +
        GetAsyncChildrenByEnterForSelectCalls +
        UpdateAsyncChildCalls +
        UpdateAsyncVisibilityCalls;

    public Task AddAsyncChild(ContactDTO dto)
    {
        AddAsyncChildCalls++;
        LastAddedDto = dto;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task<ContactDTO> GetAsyncChild(ContactDTO dto)
    {
        GetAsyncChildCalls++;
        LastRequestedDto = dto;

        return ExceptionToThrow is null
            ? Task.FromResult(ChildResult)
            : Task.FromException<ContactDTO>(ExceptionToThrow);
    }

    public Task<IEnumerable<ContactDTO>> GetAsyncChildrenByEnterForSelect(ContactDTO dto)
    {
        GetAsyncChildrenByEnterForSelectCalls++;
        LastForSelectDto = dto;

        return ExceptionToThrow is null
            ? Task.FromResult(ForSelectResult)
            : Task.FromException<IEnumerable<ContactDTO>>(ExceptionToThrow);
    }

    public Task<IEnumerable<ContactDTO>> GetAsyncChildrenByEnterprise(ContactDTO dto)
    {
        GetAsyncChildrenByEnterpriseCalls++;
        LastEnterpriseDto = dto;

        return ExceptionToThrow is null
            ? Task.FromResult(ChildrenResult)
            : Task.FromException<IEnumerable<ContactDTO>>(ExceptionToThrow);
    }

    public Task UpdateAsyncChild(ContactDTO dto)
    {
        UpdateAsyncChildCalls++;
        LastUpdatedDto = dto;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task UpdateAsyncVisibility(ContactDTO dto)
    {
        UpdateAsyncVisibilityCalls++;
        LastVisibilityDto = dto;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }
}