using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;

namespace HexArch.Data.Mappers.DtoToEntity;

public class DeviceDTOtoEntityMapper : IMapper<DeviceDTO, DeviceEntity>
{
    public DeviceEntity Map(DeviceDTO obj)
    {
        return new DeviceEntity
        {
            Id = obj.Id,
            EnterpriseId = obj.EnterpriseId,
            TypeId = obj.TypeId,
            Brand = obj.Brand,
            Model = obj.Model,
            SerialNumber = obj.SerialNumber,
            InventoryNumber = obj.InventoryNumber
        };
    }
}