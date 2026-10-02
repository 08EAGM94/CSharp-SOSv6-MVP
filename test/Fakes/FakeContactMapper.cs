using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace test.Fakes;

public class FakeContactMapper : IMapper<ContactDTO, ContactEntity>
{
    public int MapCalls { get; private set; }

    public ContactDTO? LastMappedDto { get; private set; }

    public Func<ContactDTO, ContactEntity> MapFunc { get; set; } = _ => new ContactEntity();

    public ContactEntity Map(ContactDTO obj)
    {
        MapCalls++;
        LastMappedDto = obj;

        return MapFunc(obj);
    }
}
