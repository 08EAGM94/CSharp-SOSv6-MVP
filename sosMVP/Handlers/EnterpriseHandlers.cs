using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace SosMVP.Handlers;

public static class EnterpriseHandlers
{
    public static async Task<IResult> InsertEnterpriseAsync(ICommonService<EnterpriseDTO> commonService, InsertEnterpriseRequest request)
    {
        // RF-2.9: the body is a single object, so the contact is its optional part. If that part
        // comes without FullName (null) OR with FullName empty/whitespace, ignore the contact:
        // delegate passing contact as null.
        if (request.Contact is null || string.IsNullOrWhiteSpace(request.Contact.FullName))
        {
            await commonService.AddAsyncInfo(request.Enterprise, contact: null);
        }
        else
        {
            await commonService.AddAsyncInfo(request.Enterprise, request.Contact);
        }

        // Respond 201 without body and without Location header (RF-2).
        return Results.StatusCode(StatusCodes.Status201Created);
    }

    public static async Task<IResult> GetEnterpriseAsync(ICommonService<EnterpriseDTO> commonService, int id)
    {
        // Replace DTO Id with route id.
        var enterprise = await commonService.GetAsyncInfo(new EnterpriseDTO { Id = id });

        return Results.Ok(enterprise);
    }

    public static async Task<IResult> GetEnterprisesAsync(ICommonService<EnterpriseDTO> commonService)
    {
        // The handler does not filter by visibility (that lives in the query).
        var enterprises = await commonService.GetAsyncAllInfo();

        return Results.Ok(enterprises);
    }

    public static async Task<IResult> UpdateEnterpriseAsync(ICommonService<EnterpriseDTO> commonService, int id, EnterpriseDTO dto)
    {
        // Replace the Id of the DTO with the route id.
        dto.Id = id;

        await commonService.UpdateAsyncInfo(dto);

        return Results.NoContent();
    }

    public static async Task<IResult> UpdateEnterpriseVisibilityAsync(ICommonService<EnterpriseDTO> commonService, int id, EnterpriseDTO dto)
    {
        // The route id always prevails over the Id sent in the body (RF-6.1, CE-1).
        var visibilityDto = new EnterpriseDTO { Id = id, Visibility = dto.Visibility };

        // Only Id and Visibility are projected into a new dto instead of reusing the received one,
        // so any other property of the request is ignored and never reaches the use case (RF-6.2, CE-2).
        await commonService.UpdateAsyncVisibility(visibilityDto);

        return Results.NoContent();
    }

    public static async Task<IResult> GetEnterprisesForSelectsAsync(ISelectService<EnterpriseDTO> selectService)
    {
        // Must use ISelectService, not ICommonService.
        var enterprises = await selectService.GetAsyncInfoForSelects();

        // The handler does not filter anything.
        return Results.Ok(enterprises);
    }
}

/// <summary>
/// Body of POST /enterprise/ (RF-2.1, RF-2.2): a single object carrying the enterprise and, as its
/// optional part, the initial contact. A minimal API binds only one complex body per endpoint, so
/// both parts travel together inside this wrapper instead of as two separate parameters.
/// </summary>
public sealed record InsertEnterpriseRequest(EnterpriseDTO Enterprise, ContactDTO? Contact);
