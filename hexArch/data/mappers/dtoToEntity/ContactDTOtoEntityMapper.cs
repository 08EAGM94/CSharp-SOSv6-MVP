using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace HexArch.Data.Mappers.DtoToEntity;

public class ContactDTOtoEntityMapper : IMapper<ContactDTO, ContactEntity>
{
    public ContactEntity Map(ContactDTO obj)
    {
        return new ContactEntity
        {
            Id = obj.Id,
            EnterpriseId = obj.EnterpriseId,
            FullName = obj.FullName
        };
    }
}