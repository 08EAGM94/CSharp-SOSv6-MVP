using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace test.Fakes;

public class FakeUserMapper : IMapper<UserDTO, UserEntity>
{
    public int MapCalls { get; private set; }

    public UserDTO? LastMappedDto { get; private set; }

    public Func<UserDTO, UserEntity> MapFunc { get; set; } = _ => new UserEntity();

    public UserEntity Map(UserDTO obj)
    {
        MapCalls++;
        LastMappedDto = obj;

        return MapFunc(obj);
    }
}
