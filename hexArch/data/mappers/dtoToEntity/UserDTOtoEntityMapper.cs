using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace HexArch.Data.Mappers.DtoToEntity;

public class UserDTOtoEntityMapper : IMapper<UserDTO, UserEntity>
{
    public UserEntity Map(UserDTO obj)
    {
        return new UserEntity
        {
            Id = obj.Id,
            Name = obj.Name,
            Surname = obj.Surname,
            Nickname = obj.AdminNickname != null ? obj.AdminNickname : obj.Nickname,
            Password = obj.AdminPwd != null ? obj.AdminPwd : obj.Password,
            Role = obj.Role,
            Signature = obj.Signature
        };
    }
}