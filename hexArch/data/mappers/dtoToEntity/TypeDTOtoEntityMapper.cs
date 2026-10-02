using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace HexArch.Data.Mappers.DtoToEntity;

public class TypeDTOtoEntityMapper : IMapper<TypeDTO, TypeEntity>
{
    public TypeEntity Map(TypeDTO obj)
    {
        return new TypeEntity
        {
            Id = obj.Id,
            Type = obj.Type
        };
    }
}