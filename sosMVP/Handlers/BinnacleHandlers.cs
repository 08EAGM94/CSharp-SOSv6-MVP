using System.Security.Claims;
using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Security;

namespace SosMVP.Handlers;

public static class BinnacleHandlers
{
    public static async Task<IResult> InsertBinnacleAsync(IBinnacleService service, ClaimsPrincipal principal, BinnacleDTO dto)
    {
        var userId = AdminAuthorization.ReadUserIdClaim(principal);

        if (userId is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "La sesión no contiene un identificador de usuario válido.");
        }

        await service.AddAsyncInfo(WithSessionUser(dto, userId.Value));

        return Results.StatusCode(StatusCodes.Status201Created);
    }

    public static async Task<IResult> GetBinnacleAsync(IBinnacleService service, ClaimsPrincipal principal, int id)
    {
        var userId = AdminAuthorization.ReadUserIdClaim(principal);

        if (userId is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "La sesión no contiene un identificador de usuario válido.");
        }

        var binnacle = await service.GetAsyncInfo(new BinnacleDTO { Id = id, UserId = userId });

        return Results.Ok(binnacle);
    }

    public static async Task<IResult> FollowupListAsync(IBinnacleService service, ClaimsPrincipal principal, int page, int elemsKey)
    {
        var userId = AdminAuthorization.ReadUserIdClaim(principal);

        if (userId is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "La sesión no contiene un identificador de usuario válido.");
        }

        var binnacles = await service.GetAsyncAllInfo(
            new BinnacleDTO { UserId = userId },
            page,
            elemsKey,
            binnFilter: null,
            "FollowupList");

        return Results.Ok(binnacles);
    }

    public static async Task<IResult> BinnaclesReportAsync(IBinnacleService service, BinnaclesReportRequest request)
    {
        var binnacles = await service.GetAsyncAllInfo(
            entity: null,
            request.Page,
            request.ElemsKey,
            request.BinnFilter,
            "BinnaclesReport");

        return Results.Ok(binnacles);
    }

    public static async Task<IResult> UpdateBinnacleAsync(IBinnacleService service, int id, BinnacleDTO dto)
    {
        dto.Id = id;

        await service.UpdateAsyncInfo(dto);

        return Results.NoContent();
    }

    public static async Task<IResult> UpdateBinnacleVisibilityAsync(IBinnacleService service, int id, BinnacleDTO dto)
    {
        var visibilityDto = new BinnacleDTO { Id = id, Visibility = dto.Visibility };

        await service.UpdateAsyncVisibility(visibilityDto);

        return Results.NoContent();
    }

    public static async Task<IResult> FollowupPartialAsync(IBinnacleService service, ClaimsPrincipal principal, int id, BinnacleDTO dto)
    {
        var userId = AdminAuthorization.ReadUserIdClaim(principal);

        if (userId is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "La sesión no contiene un identificador de usuario válido.");
        }

        var sessionDto = WithSessionUser(dto, userId.Value);
        sessionDto.Id = id;

        await service.FollowupPartialAsync(sessionDto);

        return Results.NoContent();
    }

    public static async Task<IResult> ResetActivitiesAsync(IBinnacleService service, ClaimsPrincipal principal, int id)
    {
        var userId = AdminAuthorization.ReadUserIdClaim(principal);

        if (userId is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "La sesión no contiene un identificador de usuario válido.");
        }

        await service.ResetActivitiesAsync(new BinnacleDTO { Id = id, UserId = userId });

        return Results.NoContent();
    }

    public static async Task<IResult> CancelBinnacleAsync(IBinnacleService service, ClaimsPrincipal principal, int id, BinnacleDTO dto)
    {
        var userId = AdminAuthorization.ReadUserIdClaim(principal);

        if (userId is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "La sesión no contiene un identificador de usuario válido.");
        }

        var sessionDto = WithSessionUser(dto, userId.Value);
        sessionDto.Id = id;

        await service.CancelBinnacleAsync(sessionDto);

        return Results.NoContent();
    }

    public static async Task<IResult> FinishBinnacleAsync(IBinnacleService service, ClaimsPrincipal principal, int id, BinnacleDTO dto)
    {
        var userId = AdminAuthorization.ReadUserIdClaim(principal);

        if (userId is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "La sesión no contiene un identificador de usuario válido.");
        }

        var sessionDto = WithSessionUser(dto, userId.Value);
        sessionDto.Id = id;

        await service.FinishBinnacleAsync(sessionDto);

        return Results.NoContent();
    }

    public static BinnacleDTO WithSessionUser(BinnacleDTO dto, int sessionUserId)
    {
        return new BinnacleDTO
        {
            Id = dto.Id,
            UserId = sessionUserId,
            ContactId = dto.ContactId,
            Service = dto.Service,
            DeviceId = dto.DeviceId,
            Amount = dto.Amount,
            ActivitiesDone = dto.ActivitiesDone,
            Hints = dto.Hints,
            CustomerSignature = dto.CustomerSignature,
            Status = dto.Status,
            StartingDate = dto.StartingDate,
            EndDate = dto.EndDate,
            Visibility = dto.Visibility,
            DatesType = dto.DatesType,
            LeftDay = dto.LeftDay,
            RightDay = dto.RightDay,
            EnterpriseId = dto.EnterpriseId,
            CancelDesc = dto.CancelDesc,
            User = dto.User,
            Contact = dto.Contact,
            Device = dto.Device
        };
    }

    private static IResult Error(int statusCode, string message)
    {
        return Results.Text(message, statusCode: statusCode);
    }
}

public sealed record BinnaclesReportRequest(int Page, int ElemsKey, Dictionary<string, string> BinnFilter);
