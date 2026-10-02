using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace HexArch.Application.Abstractions.SecondaryPorts;

public interface IUserRepository
{
    Task<UserDTO> Login(UserEntity entity);

    bool AdminPwdConfirmation(UserEntity entity);
}