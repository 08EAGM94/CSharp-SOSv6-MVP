using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SosMVP.Handlers;
using SosMVP.Security;

namespace SosMVP.Extensions;

public static class DeviceEndpointsExtensions
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var authenticatedEndpoints = endpoints.MapGroup(string.Empty).RequireAuthorization();

        var adminEndpoints = endpoints.MapGroup(string.Empty).RequireAuthorization(AdminAuthorization.PolicyName);

        authenticatedEndpoints.MapPost("/device/", DeviceHandlers.InsertDeviceAsync)
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithName("InsertDevice");

        authenticatedEndpoints.MapGet("/device/{id}", DeviceHandlers.GetDeviceAsync)
            .Produces<DeviceDTO>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetDevice");

        adminEndpoints.MapGet("/devicesent/{enterpriseId}", DeviceHandlers.GetDevicesByEnterpriseAsync)
            .Produces<IEnumerable<DeviceDTO>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithName("GetDevicesByEnterprise");

        adminEndpoints.MapPut("/device/{id}", DeviceHandlers.UpdateDevicesAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateDevices");

        adminEndpoints.MapPut("/devicev/{id}", DeviceHandlers.UpdateDevicesVisibilityAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateDevicesVisibility");

        authenticatedEndpoints.MapGet("/devicesentsct/{enterpriseId}", DeviceHandlers.GetDevicesByEnterpriseForSelectAsync)
            .Produces<IEnumerable<DeviceDTO>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("GetDevicesByEnterpriseForSelect");

        return endpoints;
    }
}
