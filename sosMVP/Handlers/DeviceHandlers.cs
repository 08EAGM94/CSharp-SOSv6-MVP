using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace SosMVP.Handlers;

public static class DeviceHandlers
{
    public static async Task<IResult> InsertDeviceAsync(IEnterpriseChildrenService<DeviceDTO> enterpriseChildrenService, DeviceDTO dto)
    {
        await enterpriseChildrenService.AddAsyncChild(dto);

        return Results.StatusCode(StatusCodes.Status201Created);
    }

    public static async Task<IResult> GetDeviceAsync(IEnterpriseChildrenService<DeviceDTO> enterpriseChildrenService, int id)
    {
        var device = await enterpriseChildrenService.GetAsyncChild(new DeviceDTO { Id = id });

        return Results.Ok(device);
    }

    public static async Task<IResult> GetDevicesByEnterpriseAsync(IEnterpriseChildrenService<DeviceDTO> enterpriseChildrenService, int enterpriseId)
    {
        var devices = await enterpriseChildrenService.GetAsyncChildrenByEnterprise(new DeviceDTO { EnterpriseId = enterpriseId });

        return Results.Ok(devices);
    }

    public static async Task<IResult> UpdateDevicesAsync(IEnterpriseChildrenService<DeviceDTO> enterpriseChildrenService, int id, DeviceDTO dto)
    {
        dto.Id = id;

        await enterpriseChildrenService.UpdateAsyncChild(dto);

        return Results.NoContent();
    }

    public static async Task<IResult> UpdateDevicesVisibilityAsync(IEnterpriseChildrenService<DeviceDTO> enterpriseChildrenService, int id, DeviceDTO dto)
    {
        var visibilityDto = new DeviceDTO { Id = id, Visibility = dto.Visibility };

        await enterpriseChildrenService.UpdateAsyncVisibility(visibilityDto);

        return Results.NoContent();
    }

    public static async Task<IResult> GetDevicesByEnterpriseForSelectAsync(IEnterpriseChildrenService<DeviceDTO> enterpriseChildrenService, int enterpriseId)
    {
        var devices = await enterpriseChildrenService.GetAsyncChildrenByEnterForSelect(new DeviceDTO { EnterpriseId = enterpriseId });

        return Results.Ok(devices);
    }
}
