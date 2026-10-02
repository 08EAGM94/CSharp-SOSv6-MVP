using HexArch.Application.DTOs;

namespace HexArch.Application.Abstractions.PrimaryPorts;

public interface IUserService
{
    Task<UserDTO> Login(UserDTO dto);

    bool AdminPwdConfirmation(UserDTO dto);
}