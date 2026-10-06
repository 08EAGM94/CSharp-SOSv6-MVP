using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace HexArch.Data.Mappers.DtoToEntity;

public class BinnacleDTOtoEntityMapper : IMapper<BinnacleDTO, BinnacleEntity>
{
    public BinnacleEntity Map(BinnacleDTO obj)
    {
        return new BinnacleEntity
        {
            Id = obj.Id,
            UserId = obj.UserId,
            ContactId = obj.ContactId,
            Service = obj.Service,
            DeviceId = obj.DeviceId,
            Amount = obj.Amount,
            ActivitiesDone = obj.ActivitiesDone,
            Hints = obj.CancelDesc != null ? obj.CancelDesc : obj.Hints,
            CustomerSignature = obj.CustomerSignature,
            Status = obj.Status,
            StartingDate = obj.StartingDate,
            EndDate = obj.EndDate
        };
    }
}