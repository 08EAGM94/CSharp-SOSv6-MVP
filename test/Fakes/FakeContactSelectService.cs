using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;

namespace test.Fakes;

public class FakeContactSelectService : ISelectService<ContactDTO>
{
    public int GetAsyncInfoForSelectsCalls { get; private set; }

    public IEnumerable<ContactDTO> SelectResult { get; set; } = [];

    public Exception? ExceptionToThrow { get; set; }

    public Task<IEnumerable<ContactDTO>> GetAsyncInfoForSelects()
    {
        GetAsyncInfoForSelectsCalls++;

        return ExceptionToThrow is null
            ? Task.FromResult(SelectResult)
            : Task.FromException<IEnumerable<ContactDTO>>(ExceptionToThrow);
    }
}