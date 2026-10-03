using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace test.Fakes;

public class FakeTypeMapper : IMapper<TypeDTO, TypeEntity>
{
    public int MapCalls { get; private set; }

    public TypeDTO? LastMappedDto { get; private set; }

    public Func<TypeDTO, TypeEntity> MapFunc { get; set; } = _ => new TypeEntity();

    public TypeEntity Map(TypeDTO obj)
    {
        MapCalls++;
        LastMappedDto = obj;

        return MapFunc(obj);
    }
}