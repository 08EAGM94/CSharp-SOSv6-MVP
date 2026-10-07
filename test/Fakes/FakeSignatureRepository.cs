using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace test.Fakes;

public class FakeSignatureRepository : ISignatureRepository<UserEntity, UserDTO>
{
    public int InsertSignatureCalls { get; private set; }
    public int GetSignatureCalls { get; private set; }

    public UserEntity? LastInsertedEntity { get; private set; }
    public UserEntity? LastRequestedEntity { get; private set; }

    public UserDTO GetResult { get; set; } = new UserDTO();
    public Exception? ExceptionToThrow { get; set; }

    public Task InsertSignature(UserEntity entity)
    {
        InsertSignatureCalls++;
        LastInsertedEntity = entity;

        return ExceptionToThrow is null
            ? Task.CompletedTask
            : Task.FromException(ExceptionToThrow);
    }

    public Task<UserDTO> GetSignature(UserEntity entity)
    {
        GetSignatureCalls++;
        LastRequestedEntity = entity;

        return ExceptionToThrow is null
            ? Task.FromResult(GetResult)
            : Task.FromException<UserDTO>(ExceptionToThrow);
    }
}
