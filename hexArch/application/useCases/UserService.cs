using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace HexArch.Application.UseCases;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly IMapper<UserDTO, UserEntity> _toEntityMapper;

    public UserService(IUserRepository repository, IMapper<UserDTO, UserEntity> toEntityMapper)
    {
        _repository = repository;
        _toEntityMapper = toEntityMapper;
    }

    public Task<UserDTO> Login(UserDTO dto)
    {
        return _repository.Login(_toEntityMapper.Map(dto));
    }

    public bool AdminPwdConfirmation(UserDTO dto)
    {
        return _repository.AdminPwdConfirmation(_toEntityMapper.Map(dto));
    }
}