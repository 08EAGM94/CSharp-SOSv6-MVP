using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;

namespace test.Fakes;

public class FakeUserService : IUserService
{
    public int LoginCalls { get; private set; }
    public int AdminPwdConfirmationCalls { get; private set; }

    public UserDTO? LastLoginDto { get; private set; }
    public UserDTO? LastAdminPwdDto { get; private set; }

    public UserDTO LoginResult { get; set; } = new UserDTO();
    public bool AdminPwdConfirmationResult { get; set; }
    public Exception? ExceptionToThrow { get; set; }

    public Task<UserDTO> Login(UserDTO dto)
    {
        LoginCalls++;
        LastLoginDto = dto;

        return ExceptionToThrow is null
            ? Task.FromResult(LoginResult)
            : Task.FromException<UserDTO>(ExceptionToThrow);
    }

    public bool AdminPwdConfirmation(UserDTO dto)
    {
        AdminPwdConfirmationCalls++;
        LastAdminPwdDto = dto;

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return AdminPwdConfirmationResult;
    }
}
