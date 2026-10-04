using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;

namespace test.Fakes;

public class FakeEnterpriseSelectService : ISelectService<EnterpriseDTO>
{
    public int GetAsyncInfoForSelectsCalls { get; private set; }

    public IEnumerable<EnterpriseDTO> SelectResult { get; set; } = [];

    public Exception? ExceptionToThrow { get; set; }

    public Task<IEnumerable<EnterpriseDTO>> GetAsyncInfoForSelects()
    {
        GetAsyncInfoForSelectsCalls++;

        return ExceptionToThrow is null
            ? Task.FromResult(SelectResult)
            : Task.FromException<IEnumerable<EnterpriseDTO>>(ExceptionToThrow);
    }
}