using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;

namespace test.Fakes;

public class FakeSignatureService : ISignatureService<UserDTO>
{
    public int InsertSignatureCalls { get; private set; }
    public int GetSignatureCalls { get; private set; }

    public UserDTO? LastInsertedDto { get; private set; }
    public UserDTO? LastRequestedDto { get; private set; }

    public UserDTO GetResult { get; set; } = new UserDTO();
    public Exception? ExceptionToThrow { get; set; }

    public Task InsertSignature(UserDTO dto)
    {
        InsertSignatureCalls++;
        LastInsertedDto = dto;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task<UserDTO> GetSignature(UserDTO dto)
    {
        GetSignatureCalls++;
        LastRequestedDto = dto;

        return ExceptionToThrow is null
            ? Task.FromResult(GetResult)
            : Task.FromException<UserDTO>(ExceptionToThrow);
    }
}
