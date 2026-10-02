using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace test.Fakes;

public class FakeUserRepository : IUserRepository
{
    public int LoginCalls { get; private set; }
    public int AdminPwdConfirmationCalls { get; private set; }

    public UserEntity? LastLoginEntity { get; private set; }
    public UserEntity? LastAdminPwdEntity { get; private set; }

    public UserDTO LoginResult { get; set; } = new UserDTO();
    public bool AdminPwdConfirmationResult { get; set; }
    public Exception? ExceptionToThrow { get; set; }

    public Task<UserDTO> Login(UserEntity entity)
    {
        LoginCalls++;
        LastLoginEntity = entity;

        return ExceptionToThrow is null
            ? Task.FromResult(LoginResult)
            : Task.FromException<UserDTO>(ExceptionToThrow);
    }

    public bool AdminPwdConfirmation(UserEntity entity)
    {
        AdminPwdConfirmationCalls++;
        LastAdminPwdEntity = entity;

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return AdminPwdConfirmationResult;
    }
}
