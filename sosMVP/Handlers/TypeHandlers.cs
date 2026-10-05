using System.Security.Claims;
using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Security;

namespace SosMVP.Handlers;

public static class TypeHandlers
{
    public static async Task<IResult> InsertTypeAsync(ICommonService<TypeDTO> commonService, TypeDTO dto)
    {
        // The use case fixes Visibility to "ENABLED" and validates the entity rules, so the handler
        // neither validates nor rewrites the received dto (RF-2.2, RF-2.3).
        await commonService.AddAsyncInfo(dto);

        return Results.StatusCode(StatusCodes.Status201Created);
    }

    public static async Task<IResult> GetTypeAsync(ICommonService<TypeDTO> commonService, int id)
    {
        // The route carries no body, so the route id is the only source of the request dto (RF-3.1).
        var type = await commonService.GetAsyncInfo(new TypeDTO { Id = id });

        // An existing type is answered with 200 even when its Visibility is "DISABLED": the handler
        // does not filter by visibility, so 404 remains exclusive for unknown ids (RF-3.5, CE-10).
        return Results.Ok(type);
    }

    public static async Task<IResult> GetTypesAsync(ICommonService<TypeDTO> commonService, ClaimsPrincipal principal)
    {
        // The complete listing governs the catalogue, so it is reserved for administrators: the role
        // is read from the claims of the session and any role other than "admin" — including a
        // missing or corrupted one — ends in 403 before the use case runs, so no record is read and
        // no visibility is consulted (RF-4.1, RF-4.4, RF-1.5, CE-8).
        if (AdminAuthorization.ReadRoleClaim(principal) != AdminAuthorization.AdminRole)
        {
            return Results.Text(
                "Solo un administrador puede consultar el catálogo de tipos.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        // The listing takes no dto nor parameter: it goes straight through the read-all port of the
        // use case, whose repository does not filter by visibility.
        var types = await commonService.GetAsyncAllInfo();

        // The collection is answered as it comes from the use case, so records with
        // Visibility = DISABLED are part of the response (RF-4.1, RF-4.2, CE-6b).
        return Results.Ok(types);
    }

    public static async Task<IResult> UpdateTypeAsync(ICommonService<TypeDTO> commonService, int id, TypeDTO dto)
    {
        // The route id always prevails over the Id sent in the body (RF-5.1, CE-1). The dto is
        // reused instead of being projected into a new one, so the rest of the body is kept
        // (RF-5.2).
        dto.Id = id;

        // UpdateAsyncInfo is void: an unknown id reaches the handler as KeyNotFoundException (404)
        // and a Type rule violation as EntityException (400), both handled by the centralized
        // translation, so nothing is caught here (RF-5.3, RF-5.4).
        await commonService.UpdateAsyncInfo(dto);

        return Results.NoContent();
    }

    public static async Task<IResult> UpdateTypeVisibilityAsync(ICommonService<TypeDTO> commonService, int id, TypeDTO dto)
    {
        // The route id always prevails over the Id sent in the body (RF-6.1, CE-1).
        var visibilityDto = new TypeDTO { Id = id, Visibility = dto.Visibility };

        // Only Id and Visibility are projected into a new dto instead of reusing the received one,
        // so any other property of the request (the Type) is ignored and never reaches the use
        // case: this endpoint cannot update the name of the record (RF-6.2, CE-2).
        await commonService.UpdateAsyncVisibility(visibilityDto);

        // UpdateAsyncVisibility is void: an unknown id reaches the handler as KeyNotFoundException
        // (404) and an invalid Visibility as ApplicationException (400), both handled by the
        // centralized translation, so nothing is caught here (RF-6.3, RF-6.4, RF-8.1, RF-8.3).
        return Results.NoContent();
    }

    public static async Task<IResult> GetTypesForSelectsAsync(ISelectService<TypeDTO> selectService)
    {
        // The reduced listing for selects takes no dto nor parameter: it goes through the select
        // port of the use case instead of the common read-all port used by GetTypesAsync, because
        // only this port reaches TypeRepository.GetAsyncInfoForSelects (RF-7.1).
        var types = await selectService.GetAsyncInfoForSelects();

        // The handler applies no visibility filter: the records with Visibility = DISABLED are
        // already excluded downstream by the select repository (Visibilidad == "ENABLED"), so the
        // collection is answered as it comes from the use case, neither filtered nor extended
        // (RF-7.2, RF-7.3, CE-6, CE-6c). Any business failure raised by the use case reaches the
        // centralized translation through the propagation of the exception (RF-8.3, RF-8.4).
        return Results.Ok(types);
    }
}
