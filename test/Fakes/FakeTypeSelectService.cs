using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;

namespace test.Fakes;

public class FakeTypeSelectService : ISelectService<TypeDTO>
{
    public int GetAsyncInfoForSelectsCalls { get; private set; }

    public IEnumerable<TypeDTO> SelectResult { get; set; } = [];

    public Exception? ExceptionToThrow { get; set; }

    public Task<IEnumerable<TypeDTO>> GetAsyncInfoForSelects()
    {
        GetAsyncInfoForSelectsCalls++;

        return ExceptionToThrow is null
            ? Task.FromResult(SelectResult)
            : Task.FromException<IEnumerable<TypeDTO>>(ExceptionToThrow);
    }
}