using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace HexArch.Data.Mappers.DtoToEntity;

public class EnterpriseDTOtoEntityMapper : IMapper<EnterpriseDTO, EnterpriseEntity>
{
    public EnterpriseEntity Map(EnterpriseDTO obj)
    {
        return new EnterpriseEntity
        {
            Id = obj.Id,
            CommercialName = obj.CommercialName,
            TradeName = obj.TradeName,
            StreetNumber = obj.StreetNumber,
            BetweenStreets = obj.BetweenStreets,
            ContactingWith = obj.ContactingWith,
            Phones = obj.Phones,
            Schedule = obj.Schedule,
            Atention = obj.Atention,
            Neighborhood = obj.Neighborhood,
            Location = obj.Location,
            Email = obj.Email
        };
    }
}