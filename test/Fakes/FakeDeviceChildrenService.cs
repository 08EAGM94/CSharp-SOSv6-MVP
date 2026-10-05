using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;

namespace test.Fakes;

public class FakeDeviceChildrenService : IEnterpriseChildrenService<DeviceDTO>
{
    public int AddAsyncChildCalls { get; private set; }
    public int GetAsyncChildCalls { get; private set; }
    public int GetAsyncChildrenByEnterpriseCalls { get; private set; }
    public int GetAsyncChildrenByEnterForSelectCalls { get; private set; }
    public int UpdateAsyncChildCalls { get; private set; }
    public int UpdateAsyncVisibilityCalls { get; private set; }

    public DeviceDTO? LastAddedDto { get; private set; }
    public DeviceDTO? LastRequestedDto { get; private set; }
    public DeviceDTO? LastEnterpriseDto { get; private set; }
    public DeviceDTO? LastForSelectDto { get; private set; }
    public DeviceDTO? LastUpdatedDto { get; private set; }
    public DeviceDTO? LastVisibilityDto { get; private set; }

    public DeviceDTO ChildResult { get; set; } = new DeviceDTO();
    public IEnumerable<DeviceDTO> ChildrenResult { get; set; } = [];
    public IEnumerable<DeviceDTO> ForSelectResult { get; set; } = [];
    public Exception? ExceptionToThrow { get; set; }

    public int TotalCalls =>
        AddAsyncChildCalls +
        GetAsyncChildCalls +
        GetAsyncChildrenByEnterpriseCalls +
        GetAsyncChildrenByEnterForSelectCalls +
        UpdateAsyncChildCalls +
        UpdateAsyncVisibilityCalls;

    public Task AddAsyncChild(DeviceDTO dto)
    {
        AddAsyncChildCalls++;
        LastAddedDto = dto;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task<DeviceDTO> GetAsyncChild(DeviceDTO dto)
    {
        GetAsyncChildCalls++;
        LastRequestedDto = dto;

        return ExceptionToThrow is null
            ? Task.FromResult(ChildResult)
            : Task.FromException<DeviceDTO>(ExceptionToThrow);
    }

    public Task<IEnumerable<DeviceDTO>> GetAsyncChildrenByEnterForSelect(DeviceDTO dto)
    {
        GetAsyncChildrenByEnterForSelectCalls++;
        LastForSelectDto = dto;

        return ExceptionToThrow is null
            ? Task.FromResult(ForSelectResult)
            : Task.FromException<IEnumerable<DeviceDTO>>(ExceptionToThrow);
    }

    public Task<IEnumerable<DeviceDTO>> GetAsyncChildrenByEnterprise(DeviceDTO dto)
    {
        GetAsyncChildrenByEnterpriseCalls++;
        LastEnterpriseDto = dto;

        return ExceptionToThrow is null
            ? Task.FromResult(ChildrenResult)
            : Task.FromException<IEnumerable<DeviceDTO>>(ExceptionToThrow);
    }

    public Task UpdateAsyncChild(DeviceDTO dto)
    {
        UpdateAsyncChildCalls++;
        LastUpdatedDto = dto;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task UpdateAsyncVisibility(DeviceDTO dto)
    {
        UpdateAsyncVisibilityCalls++;
        LastVisibilityDto = dto;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }
}
